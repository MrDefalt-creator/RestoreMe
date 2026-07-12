using Backup.Server.Api.Security;
using Backup.Server.Api.Services;
using Backup.Server.Application.Interfaces;
using Backup.Server.Domain.Entities;
using Backup.Server.Domain.Enums;
using Backup.Server.Infrastructure.Configuration;
using Backup.Server.Infrastructure.Options;
using Backup.Server.Infrastructure.Services;
using Backup.Shared.Contracts.DTOs.Auth;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Backup.Server.Tests.Auth;

/// <summary>
/// Verifies the refresh-token revocation hooks: self password-change (AuthService),
/// admin reset / disable / admin revoke-sessions (UsersService). Delete relies on
/// the ON DELETE CASCADE FK and is covered by the schema, not re-tested here.
/// </summary>
public sealed class RevocationHooksTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private DbContextOptions<AppDbContext> _options = null!;
    private IDataProtectionProvider _dataProtection = null!;

    public Task InitializeAsync()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        _dataProtection = new EphemeralDataProtectionProvider();

        using var ctx = CreateContext();
        ctx.Database.EnsureCreated();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _connection.Dispose();
        return Task.CompletedTask;
    }

    private AppDbContext CreateContext() => new(_options, _dataProtection);

    private static readonly PasswordHasher<AppUser> Hasher = new();

    private static AuthService CreateAuthService(AppDbContext ctx) => new(
        new AppUserRepository(ctx),
        Hasher,
        new TokenService(Options.Create(new JwtOptions())),
        new AuditLogRepository(ctx),
        new RefreshTokenRepository(ctx),
        new MemoryCache(new MemoryCacheOptions()));

    private static UsersService CreateUsersService(AppDbContext ctx) => new(
        new AppUserRepository(ctx),
        Hasher,
        new AuditLogRepository(ctx),
        new RefreshTokenRepository(ctx),
        new NoopBroadcaster());

    private static async Task<Guid> SeedUser(AppDbContext ctx, string password, AppUserRole role = AppUserRole.Operator)
    {
        var userId = Guid.NewGuid();
        var user = new AppUser
        {
            Id = userId,
            Username = $"hook-user-{userId:N}",
            NormalizedUsername = $"HOOK-USER-{userId:N}".ToUpperInvariant(),
            Role = role,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        user.PasswordHash = Hasher.HashPassword(user, password);
        ctx.AppUsers.Add(user);
        await ctx.SaveChangesAsync();
        return userId;
    }

    private static async Task IssueSession(AppDbContext ctx, Guid userId)
    {
        var svc = new RefreshTokenService(new RefreshTokenRepository(ctx), Options.Create(new JwtOptions()));
        await svc.IssueAsync(userId, Guid.NewGuid(), "agent", "127.0.0.1", CancellationToken.None);
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task Changing_password_purges_refresh_tokens()
    {
        await using var ctx = CreateContext();
        var userId = await SeedUser(ctx, "OldPass1!");
        await IssueSession(ctx, userId);
        await IssueSession(ctx, userId);

        await CreateAuthService(ctx).ChangePasswordAsync(userId,
            new ChangePasswordRequest("OldPass1!", "NewPass9#"));

        var repo = new RefreshTokenRepository(ctx);
        Assert.Empty(await repo.GetActiveForUserAsync(userId));
    }

    [Fact]
    public async Task Admin_reset_purges_refresh_tokens()
    {
        await using var ctx = CreateContext();
        var adminId = await SeedUser(ctx, "AdminPass1!", AppUserRole.Admin);
        var targetId = await SeedUser(ctx, "TargetPass1!");
        await IssueSession(ctx, targetId);

        await CreateUsersService(ctx).SetPasswordAsync(adminId, targetId, "ResetPass9#");

        var repo = new RefreshTokenRepository(ctx);
        Assert.Empty(await repo.GetActiveForUserAsync(targetId));
    }

    [Fact]
    public async Task Disabling_user_purges_refresh_tokens()
    {
        await using var ctx = CreateContext();
        var adminId = await SeedUser(ctx, "AdminPass1!", AppUserRole.Admin);
        var targetId = await SeedUser(ctx, "TargetPass1!");
        await IssueSession(ctx, targetId);

        await CreateUsersService(ctx).UpdateStatusAsync(adminId, targetId, isActive: false);

        var repo = new RefreshTokenRepository(ctx);
        Assert.Empty(await repo.GetActiveForUserAsync(targetId));
    }

    [Fact]
    public async Task Admin_revoke_sessions_purges_and_audits()
    {
        await using var ctx = CreateContext();
        var adminId = await SeedUser(ctx, "AdminPass1!", AppUserRole.Admin);
        var targetId = await SeedUser(ctx, "TargetPass1!");
        await IssueSession(ctx, targetId);

        await CreateUsersService(ctx).RevokeSessionsAsync(adminId, targetId);

        var repo = new RefreshTokenRepository(ctx);
        Assert.Empty(await repo.GetActiveForUserAsync(targetId));

        var audits = await ctx.AuditLogs.Where(a => a.Action == "auth.sessions_revoked").ToListAsync();
        Assert.Single(audits);
        Assert.Equal(adminId, audits[0].ActorId);
        Assert.Equal(targetId, audits[0].TargetId);
    }

    [Fact]
    public async Task Admin_revoke_sessions_for_unknown_user_throws()
    {
        await using var ctx = CreateContext();
        var adminId = await SeedUser(ctx, "AdminPass1!", AppUserRole.Admin);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            CreateUsersService(ctx).RevokeSessionsAsync(adminId, Guid.NewGuid()));
    }

    private sealed class NoopBroadcaster : IAdminEventBroadcaster
    {
        public void Publish(AdminEventTopic topic) { }
        public IAdminEventSubscription Subscribe() => throw new NotSupportedException();
    }
}
