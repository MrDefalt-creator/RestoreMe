using System.Security.Claims;
using Backup.Server.Api.Controllers;
using Backup.Server.Api.Security;
using Backup.Server.Api.Services;
using Backup.Server.Domain.Entities;
using Backup.Server.Infrastructure.Configuration;
using Backup.Server.Infrastructure.Options;
using Backup.Server.Infrastructure.Services;
using Backup.Shared.Contracts.DTOs.Auth;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

namespace Backup.Server.Tests.Auth;

/// <summary>
/// Controller-level tests for the self-service session endpoints (logout,
/// logout-all, list, revoke). Drives a real AuthController with an
/// authenticated ClaimsPrincipal over the SQLite + DataProtection fixture.
/// Route-level [Authorize] enforcement is a pipeline concern and is covered by
/// the policy attributes, not these unit tests.
/// </summary>
public sealed class SessionsEndpointTests : IAsyncLifetime
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

    private static AuthController CreateController(AppDbContext ctx, Guid? authUserId, string? refreshCookie)
    {
        var jwt = Options.Create(new JwtOptions());
        var tokenService = new TokenService(jwt);
        var users = new AppUserRepository(ctx);
        var audit = new AuditLogRepository(ctx);
        var refreshRepo = new RefreshTokenRepository(ctx);
        var refreshService = new RefreshTokenService(refreshRepo, jwt);
        var authService = new AuthService(
            users,
            new PasswordHasher<AppUser>(),
            tokenService,
            audit,
            new MemoryCache(new MemoryCacheOptions()));

        var controller = new AuthController(
            authService, refreshService, refreshRepo, users, tokenService, audit, new FakeEnv());

        var httpContext = new DefaultHttpContext();
        if (refreshCookie is not null)
        {
            httpContext.Request.Headers.Cookie = $"refresh_token={refreshCookie}";
        }
        if (authUserId is not null)
        {
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, authUserId.Value.ToString()) }, "test"));
        }
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    private static async Task<Guid> SeedUser(AppDbContext ctx, string suffix)
    {
        var userId = Guid.NewGuid();
        ctx.AppUsers.Add(new AppUser
        {
            Id = userId,
            Username = $"session-user-{suffix}",
            NormalizedUsername = $"SESSION-USER-{suffix.ToUpperInvariant()}",
            PasswordHash = "irrelevant-hash",
            CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();
        return userId;
    }

    // Issues an independent session (own family) and returns its raw token.
    private static async Task<string> IssueSession(AppDbContext ctx, Guid userId, string ua)
    {
        var svc = new RefreshTokenService(new RefreshTokenRepository(ctx), Options.Create(new JwtOptions()));
        var issued = await svc.IssueAsync(userId, Guid.NewGuid(), ua, "127.0.0.1", CancellationToken.None);
        await ctx.SaveChangesAsync();
        return issued.RawToken;
    }

    [Fact]
    public async Task Sessions_lists_active_and_flags_current()
    {
        await using var seedCtx = CreateContext();
        var userId = await SeedUser(seedCtx, "a");
        var current = await IssueSession(seedCtx, userId, "device-1");
        await IssueSession(seedCtx, userId, "device-2");

        await using var ctx = CreateContext();
        var result = await CreateController(ctx, userId, refreshCookie: current).Sessions();

        var ok = Assert.IsType<OkObjectResult>(result);
        var sessions = Assert.IsAssignableFrom<IReadOnlyList<SessionDto>>(ok.Value);
        Assert.Equal(2, sessions.Count);
        Assert.Single(sessions, s => s.Current);
        Assert.Contains(sessions, s => s.Current && s.UserAgent == "device-1");
    }

    [Fact]
    public async Task RevokeSession_revokes_own_session_by_id()
    {
        await using var seedCtx = CreateContext();
        var userId = await SeedUser(seedCtx, "b");
        await IssueSession(seedCtx, userId, "keep");
        await IssueSession(seedCtx, userId, "drop");

        await using var ctx = CreateContext();
        var repo = new RefreshTokenRepository(ctx);
        var active = await repo.GetActiveForUserAsync(userId);
        var drop = active.First(s => s.UserAgent == "drop");

        var result = await CreateController(ctx, userId, refreshCookie: null).RevokeSession(drop.Id);
        Assert.IsType<NoContentResult>(result);

        var remaining = await repo.GetActiveForUserAsync(userId);
        Assert.Single(remaining);
        Assert.Equal("keep", remaining[0].UserAgent);
    }

    [Fact]
    public async Task RevokeSession_of_another_user_returns_404_and_leaves_it_active()
    {
        await using var seedCtx = CreateContext();
        var owner = await SeedUser(seedCtx, "owner");
        var attacker = await SeedUser(seedCtx, "attacker");
        await IssueSession(seedCtx, owner, "owner-device");

        await using var ctx = CreateContext();
        var repo = new RefreshTokenRepository(ctx);
        var ownerSession = (await repo.GetActiveForUserAsync(owner)).Single();

        // attacker (authenticated as themselves) tries to revoke owner's session
        var result = await CreateController(ctx, attacker, refreshCookie: null).RevokeSession(ownerSession.Id);
        Assert.IsType<NotFoundResult>(result);

        Assert.Single(await repo.GetActiveForUserAsync(owner));
    }

    [Fact]
    public async Task Logout_revokes_current_session()
    {
        await using var seedCtx = CreateContext();
        var userId = await SeedUser(seedCtx, "c");
        var raw = await IssueSession(seedCtx, userId, "device");

        await using var ctx = CreateContext();
        var result = await CreateController(ctx, userId, refreshCookie: raw).Logout();
        Assert.IsType<NoContentResult>(result);

        var repo = new RefreshTokenRepository(ctx);
        Assert.Empty(await repo.GetActiveForUserAsync(userId));
    }

    [Fact]
    public async Task LogoutAll_revokes_every_session()
    {
        await using var seedCtx = CreateContext();
        var userId = await SeedUser(seedCtx, "d");
        var raw = await IssueSession(seedCtx, userId, "device-1");
        await IssueSession(seedCtx, userId, "device-2");

        await using var ctx = CreateContext();
        var result = await CreateController(ctx, userId, refreshCookie: raw).LogoutAll();
        Assert.IsType<NoContentResult>(result);

        var repo = new RefreshTokenRepository(ctx);
        Assert.Empty(await repo.GetActiveForUserAsync(userId));
    }

    private sealed class FakeEnv : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "tests";
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
