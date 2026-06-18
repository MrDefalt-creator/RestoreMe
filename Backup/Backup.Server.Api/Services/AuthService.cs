using Backup.Server.Api.Security;
using Backup.Server.Application.Interfaces;
using Backup.Server.Domain.Entities;
using Backup.Shared.Contracts.DTOs.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;

namespace Backup.Server.Api.Services;

public sealed class AccountLockedException : UnauthorizedAccessException
{
    public DateTime LockedUntilUtc { get; }

    public AccountLockedException(DateTime lockedUntilUtc)
        : base("Account is temporarily locked due to repeated failed sign-in attempts.")
    {
        LockedUntilUtc = lockedUntilUtc;
    }
}

public class AuthService
{
    // Policy knobs — kept here so any future Settings:Auth section can
    // bind to them without refactoring call sites.
    public const int MaxFailedAttemptsBeforeLockout = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private readonly IAppUserRepository _appUserRepository;
    private readonly IPasswordHasher<AppUser> _passwordHasher;
    private readonly TokenService _tokenService;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IMemoryCache _memoryCache;

    public AuthService(
        IAppUserRepository appUserRepository,
        IPasswordHasher<AppUser> passwordHasher,
        TokenService tokenService,
        IAuditLogRepository auditLogRepository,
        IMemoryCache memoryCache)
    {
        _appUserRepository = appUserRepository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _auditLogRepository = auditLogRepository;
        _memoryCache = memoryCache;
    }

    public async Task<AuthResponse> LoginAsync(string username, string password)
    {
        var normalizedUsername = NormalizeUsername(username);
        var user = await _appUserRepository.GetByNormalizedUsernameAsync(normalizedUsername);

        if (user == null || !user.IsActive)
        {
            throw new UnauthorizedAccessException("Invalid username or password.");
        }

        if (user.LockedUntilUtc.HasValue && user.LockedUntilUtc.Value > DateTime.UtcNow)
        {
            throw new AccountLockedException(user.LockedUntilUtc.Value);
        }

        var passwordVerification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (passwordVerification == PasswordVerificationResult.Failed)
        {
            await RegisterFailedAttemptAsync(user);
            throw new UnauthorizedAccessException("Invalid username or password.");
        }

        // Reset the counter on first successful login since either a clean
        // window or a previous lockout expiry.
        if (user.FailedLoginAttempts != 0 || user.LockedUntilUtc.HasValue)
        {
            user.FailedLoginAttempts = 0;
            user.LockedUntilUtc = null;
            await _appUserRepository.UpdateAsync(user);
            await _appUserRepository.SaveChangesAsync();
        }

        return _tokenService.CreateUserAuthResponse(user);
    }

    private async Task RegisterFailedAttemptAsync(AppUser user)
    {
        user.FailedLoginAttempts++;
        if (user.FailedLoginAttempts >= MaxFailedAttemptsBeforeLockout)
        {
            user.LockedUntilUtc = DateTime.UtcNow.Add(LockoutDuration);
            await _auditLogRepository.AddAsync(new AuditLog
            {
                Id = Guid.NewGuid(),
                ActorId = user.Id,
                Action = "auth.lockout",
                TargetId = user.Id,
                Details = $"locked_until={user.LockedUntilUtc:O} attempts={user.FailedLoginAttempts}",
                OccurredAt = DateTime.UtcNow,
            });
        }
        await _appUserRepository.UpdateAsync(user);
        await _appUserRepository.SaveChangesAsync();
    }

    public async Task<CurrentUserResponse> GetCurrentUserAsync(Guid userId)
    {
        var user = await _appUserRepository.GetByIdAsync(userId);
        if (user == null || !user.IsActive)
        {
            throw new KeyNotFoundException("User not found.");
        }

        return _tokenService.CreateUserAuthResponse(user).User;
    }

    public async Task<AuthResponse> ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
    {
        var user = await _appUserRepository.GetByIdAsync(userId);
        if (user == null || !user.IsActive)
        {
            throw new KeyNotFoundException("User not found.");
        }

        var verificationResult = _passwordHasher.VerifyHashedPassword(
            user,
            user.PasswordHash,
            request.CurrentPassword);

        if (verificationResult == PasswordVerificationResult.Failed)
        {
            throw new UnauthorizedAccessException("Current password is incorrect.");
        }

        UsersService.ValidatePassword(request.NewPassword);
        UsersService.EnsurePasswordNotReused(user, request.NewPassword, _passwordHasher);

        UsersService.RecordPasswordInHistory(user, user.PasswordHash);
        user.PasswordHash = _passwordHasher.HashPassword(user, request.NewPassword);
        user.SecurityStamp = Guid.NewGuid();
        user.MustChangePassword = false;
        user.FailedLoginAttempts = 0;
        user.LockedUntilUtc = null;
        await _appUserRepository.UpdateAsync(user);
        await _appUserRepository.SaveChangesAsync();

        // Drop the cached security stamp so the previously issued token can't
        // outlive the rotation (cache TTL is 30 s and would otherwise keep
        // accepting the old token until it naturally expired). The caller
        // gets a fresh token below so the user's session continues without
        // an involuntary logout.
        _memoryCache.Remove($"sec-stamp:{user.Id}");

        return _tokenService.CreateUserAuthResponse(user);
    }

    public static string NormalizeUsername(string username)
    {
        return username.Trim().ToUpperInvariant();
    }
}
