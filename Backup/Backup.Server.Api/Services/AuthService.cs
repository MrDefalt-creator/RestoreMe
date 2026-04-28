using Backup.Server.Api.Security;
using Backup.Server.Application.Interfaces;
using Backup.Server.Domain.Entities;
using Backup.Shared.Contracts.DTOs.Auth;
using Microsoft.AspNetCore.Identity;

namespace Backup.Server.Api.Services;

public class AuthService
{
    private readonly IAppUserRepository _appUserRepository;
    private readonly IPasswordHasher<AppUser> _passwordHasher;
    private readonly TokenService _tokenService;

    public AuthService(
        IAppUserRepository appUserRepository,
        IPasswordHasher<AppUser> passwordHasher,
        TokenService tokenService)
    {
        _appUserRepository = appUserRepository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
    }

    public async Task<AuthResponse> LoginAsync(string username, string password)
    {
        var normalizedUsername = NormalizeUsername(username);
        var user = await _appUserRepository.GetByNormalizedUsernameAsync(normalizedUsername);

        if (user == null || !user.IsActive)
        {
            throw new UnauthorizedAccessException("Invalid username or password.");
        }

        var passwordVerification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (passwordVerification == PasswordVerificationResult.Failed)
        {
            throw new UnauthorizedAccessException("Invalid username or password.");
        }

        return _tokenService.CreateUserAuthResponse(user);
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

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
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

        user.PasswordHash = _passwordHasher.HashPassword(user, request.NewPassword);
        await _appUserRepository.UpdateAsync(user);
        await _appUserRepository.SaveChangesAsync();
    }

    public static string NormalizeUsername(string username)
    {
        return username.Trim().ToUpperInvariant();
    }
}
