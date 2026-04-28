using Backup.Server.Api.Security;
using Backup.Server.Application.Interfaces;
using Backup.Server.Domain.Entities;
using Backup.Server.Domain.Enums;
using Backup.Server.Infrastructure.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Backup.Server.Api.Services;

public class SecuritySeedService
{
    private readonly IAppUserRepository _appUserRepository;
    private readonly IPasswordHasher<AppUser> _passwordHasher;
    private readonly SecuritySeedOptions _securitySeedOptions;
    private readonly ILogger<SecuritySeedService> _logger;

    public SecuritySeedService(
        IAppUserRepository appUserRepository,
        IPasswordHasher<AppUser> passwordHasher,
        IOptions<SecuritySeedOptions> securitySeedOptions,
        ILogger<SecuritySeedService> logger)
    {
        _appUserRepository = appUserRepository;
        _passwordHasher = passwordHasher;
        _securitySeedOptions = securitySeedOptions.Value;
        _logger = logger;
    }

    public async Task EnsureSeedUsersAsync()
    {
        if (await _appUserRepository.CountAsync() > 0)
        {
            return;
        }

        foreach (var seedUser in _securitySeedOptions.Users)
        {
            if (string.IsNullOrWhiteSpace(seedUser.Username) || string.IsNullOrWhiteSpace(seedUser.Password))
            {
                continue;
            }

            var normalizedUsername = AuthService.NormalizeUsername(seedUser.Username);
            var existing = await _appUserRepository.GetByNormalizedUsernameAsync(normalizedUsername);
            if (existing != null)
            {
                continue;
            }

            var user = new AppUser
            {
                Id = Guid.NewGuid(),
                Username = seedUser.Username.Trim(),
                NormalizedUsername = normalizedUsername,
                Role = ParseRole(seedUser.Role),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            user.PasswordHash = _passwordHasher.HashPassword(user, seedUser.Password);

            await _appUserRepository.AddAsync(user);
            _logger.LogInformation("Seeded security user {Username} with role {Role}.", user.Username, user.Role);
        }

        await _appUserRepository.SaveChangesAsync();
    }

    private static AppUserRole ParseRole(string role)
    {
        return role.Trim().ToLowerInvariant() switch
        {
            "viewer" => AppUserRole.Viewer,
            "operator" => AppUserRole.Operator,
            "admin" => AppUserRole.Admin,
            _ => throw new InvalidOperationException($"Unsupported seed role '{role}'.")
        };
    }
}
