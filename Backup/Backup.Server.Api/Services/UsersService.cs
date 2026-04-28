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

    public UsersService(
        IAppUserRepository appUserRepository,
        IPasswordHasher<AppUser> passwordHasher)
    {
        _appUserRepository = appUserRepository;
        _passwordHasher = passwordHasher;
    }

    public async Task<List<AdminUserDto>> GetUsersAsync()
    {
        var users = await _appUserRepository.GetAllAsync();
        return users.Select(MapUser).ToList();
    }

    public async Task<AdminUserDto> CreateUserAsync(CreateUserRequest request)
    {
        var normalizedUsername = AuthService.NormalizeUsername(request.Username);
        var existing = await _appUserRepository.GetByNormalizedUsernameAsync(normalizedUsername);
        if (existing != null)
        {
            throw new InvalidOperationException("User with the same username already exists.");
        }

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
        await _appUserRepository.SaveChangesAsync();
        return MapUser(user);
    }

    public async Task SetPasswordAsync(Guid userId, string newPassword)
    {
        var user = await GetUserByIdAsync(userId);
        user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);
        await _appUserRepository.UpdateAsync(user);
        await _appUserRepository.SaveChangesAsync();
    }

    public async Task DeleteUserAsync(Guid actorUserId, Guid userId)
    {
        EnsureDifferentActor(actorUserId, userId, "You cannot delete the current signed-in account.");
        var user = await GetUserByIdAsync(userId);
        await EnsureAdminAvailabilityAsync(user, user.Role == AppUserRole.Admin && user.IsActive);
        await _appUserRepository.DeleteAsync(user);
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
