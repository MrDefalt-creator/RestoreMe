using Backup.Server.Api.Security;
using Backup.Server.Application.Interfaces;
using Backup.Server.Domain.Entities;
using Backup.Server.Domain.Enums;
using Backup.Shared.Contracts.DTOs.Users;
using Microsoft.AspNetCore.Identity;

namespace Backup.Server.Api.Services;

public class UsersService
{
    private readonly IAppUserRepository _appUserRepository;
    private readonly IPasswordHasher<AppUser> _passwordHasher;
    private readonly IAuditLogRepository _auditLogRepository;

    public UsersService(
        IAppUserRepository appUserRepository,
        IPasswordHasher<AppUser> passwordHasher,
        IAuditLogRepository auditLogRepository)
    {
        _appUserRepository = appUserRepository;
        _passwordHasher = passwordHasher;
        _auditLogRepository = auditLogRepository;
    }

    public async Task<List<AdminUserDto>> GetUsersAsync()
    {
        var users = await _appUserRepository.GetAllAsync();
        return users.Select(MapUser).ToList();
    }

    public async Task<AdminUserDto> CreateUserAsync(Guid actorId, CreateUserRequest request)
    {
        var normalizedUsername = AuthService.NormalizeUsername(request.Username);
        var existing = await _appUserRepository.GetByNormalizedUsernameAsync(normalizedUsername);
        if (existing != null)
        {
            throw new InvalidOperationException("User with the same username already exists.");
        }

        ValidatePassword(request.Password);
        var role = ParseRole(request.Role);
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            Username = request.Username.Trim(),
            NormalizedUsername = normalizedUsername,
            Role = role,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        await _appUserRepository.AddAsync(user);
        await _auditLogRepository.AddAsync(Audit(actorId, "user.create", user.Id, $"username={user.Username} role={request.Role}"));
        await _appUserRepository.SaveChangesAsync();

        return MapUser(user);
    }

    public async Task<AdminUserDto> UpdateRoleAsync(Guid actorUserId, Guid userId, string role)
    {
        EnsureDifferentActor(actorUserId, userId, "You cannot change the role of the current signed-in account.");
        var user = await GetUserByIdAsync(userId);
        var parsedRole = ParseRole(role);
        await EnsureAdminAvailabilityAsync(user, user.IsActive && parsedRole != AppUserRole.Admin);
        user.Role = parsedRole;
        await _appUserRepository.UpdateAsync(user);
        await _auditLogRepository.AddAsync(Audit(actorUserId, "user.role_change", userId, $"new_role={role}"));
        await _appUserRepository.SaveChangesAsync();
        return MapUser(user);
    }

    public async Task<AdminUserDto> UpdateStatusAsync(Guid actorUserId, Guid userId, bool isActive)
    {
        EnsureDifferentActor(actorUserId, userId, "You cannot change the status of the current signed-in account.");
        var user = await GetUserByIdAsync(userId);
        await EnsureAdminAvailabilityAsync(user, user.Role == AppUserRole.Admin && user.IsActive && !isActive);
        user.IsActive = isActive;
        await _appUserRepository.UpdateAsync(user);
        await _auditLogRepository.AddAsync(Audit(actorUserId, "user.status_change", userId, $"is_active={isActive}"));
        await _appUserRepository.SaveChangesAsync();
        return MapUser(user);
    }

    public async Task SetPasswordAsync(Guid actorId, Guid userId, string newPassword)
    {
        ValidatePassword(newPassword);
        var user = await GetUserByIdAsync(userId);
        user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);
        user.SecurityStamp = Guid.NewGuid();
        // Admin reset = target user signs in once and must pick their own
        // password before they can use anything else.
        user.MustChangePassword = true;
        await _appUserRepository.UpdateAsync(user);
        await _auditLogRepository.AddAsync(Audit(actorId, "user.password_reset", userId));
        await _appUserRepository.SaveChangesAsync();
    }

    public async Task DeleteUserAsync(Guid actorUserId, Guid userId)
    {
        EnsureDifferentActor(actorUserId, userId, "You cannot delete the current signed-in account.");
        var user = await GetUserByIdAsync(userId);
        await EnsureAdminAvailabilityAsync(user, user.Role == AppUserRole.Admin && user.IsActive);
        await _appUserRepository.DeleteAsync(user);
        await _auditLogRepository.AddAsync(Audit(actorUserId, "user.delete", userId, $"username={user.Username}"));
        await _appUserRepository.SaveChangesAsync();
    }

    private async Task<AppUser> GetUserByIdAsync(Guid userId)
    {
        var user = await _appUserRepository.GetByIdAsync(userId);
        if (user == null)
        {
            throw new KeyNotFoundException("User not found.");
        }

        return user;
    }

    private async Task EnsureAdminAvailabilityAsync(AppUser user, bool adminAccessWouldBeRemoved)
    {
        if (!adminAccessWouldBeRemoved)
        {
            return;
        }

        var activeAdminCount = await _appUserRepository.CountActiveByRoleAsync(AppUserRole.Admin);
        if (activeAdminCount <= 1)
        {
            throw new InvalidOperationException("At least one active administrator must remain in the system.");
        }
    }

    private static void EnsureDifferentActor(Guid actorUserId, Guid targetUserId, string message)
    {
        if (actorUserId == targetUserId)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static AppUserRole ParseRole(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "viewer" => AppUserRole.Viewer,
            "operator" => AppUserRole.Operator,
            "admin" => AppUserRole.Admin,
            _ => throw new InvalidOperationException($"Unsupported user role '{value}'.")
        };
    }

    private static void ValidatePassword(string password)
    {
        if (password.Length < 8)
            throw new InvalidOperationException("Password must be at least 8 characters long.");
        if (!password.Any(char.IsUpper))
            throw new InvalidOperationException("Password must contain at least one uppercase letter.");
        if (!password.Any(char.IsDigit))
            throw new InvalidOperationException("Password must contain at least one digit.");
    }

    private static AuditLog Audit(Guid actorId, string action, Guid? targetId = null, string? details = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            ActorId = actorId,
            Action = action,
            TargetId = targetId,
            Details = details,
            OccurredAt = DateTime.UtcNow
        };

    private static AdminUserDto MapUser(AppUser user)
    {
        var role = user.Role switch
        {
            AppUserRole.Viewer => "viewer",
            AppUserRole.Operator => "operator",
            AppUserRole.Admin => "admin",
            _ => throw new ArgumentOutOfRangeException(nameof(user.Role), user.Role, null)
        };

        return new AdminUserDto(user.Id, user.Username, role, user.IsActive, user.CreatedAt);
    }
}
