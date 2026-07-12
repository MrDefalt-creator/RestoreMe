using Backup.Server.Api.Controllers;
using Backup.Server.Api.Security;
using Backup.Server.Api.Services;
using Backup.Server.Domain.Entities;
using Backup.Server.Infrastructure.Configuration;
using Backup.Server.Infrastructure.Options;
using Backup.Server.Infrastructure.Services;
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
/// Controller-level tests for POST /api/auth/refresh. The project has no
/// WebApplicationFactory harness, so the endpoint is exercised by driving a
/// real AuthController (backed by the SQLite + DataProtection fixture) through
/// a DefaultHttpContext — enough to assert the cookie/rotation/audit contract.
/// </summary>
public sealed class RefreshEndpointTests : IAsyncLifetime
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

    private static AuthController CreateController(AppDbContext ctx, string? refreshCookie)
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
            refreshRepo,
            new MemoryCache(new MemoryCacheOptions()));

        var controller = new AuthController(
            authService, refreshService, refreshRepo, users, tokenService, audit, new FakeEnv());

        var httpContext = new DefaultHttpContext();
        if (refreshCookie is not null)
        {
            httpContext.Request.Headers.Cookie = $"refresh_token={refreshCookie}";
        }
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    private static async Task<Guid> SeedUser(AppDbContext ctx)
    {
        var userId = Guid.NewGuid();
        ctx.AppUsers.Add(new AppUser
        {
            Id = userId,
            Username = "refresh-endpoint-user",
            NormalizedUsername = "REFRESH-ENDPOINT-USER",
            PasswordHash = "irrelevant-hash",
            CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();
        return userId;
    }

    private static string? RefreshCookieValue(HttpResponse response)
    {
        foreach (var sc in response.Headers.SetCookie)
        {
            if (sc is not null && sc.StartsWith("refresh_token=", StringComparison.Ordinal))
            {
                var afterEq = sc.Substring("refresh_token=".Length);
                return afterEq.Split(';')[0];
            }
        }
        return null;
    }

    [Fact]
    public async Task Refresh_without_cookie_returns_401()
    {
        await using var ctx = CreateContext();
        var controller = CreateController(ctx, refreshCookie: null);

        var result = await controller.Refresh();

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task Refresh_with_valid_cookie_rotates_and_returns_200()
    {
        await using var seedCtx = CreateContext();
        var userId = await SeedUser(seedCtx);
        var issueService = new RefreshTokenService(new RefreshTokenRepository(seedCtx), Options.Create(new JwtOptions()));
        var issued = await issueService.IssueAsync(userId, Guid.NewGuid(), "agent-a", "127.0.0.1", CancellationToken.None);
        await seedCtx.SaveChangesAsync();

        await using var ctx = CreateContext();
        var controller = CreateController(ctx, refreshCookie: issued.RawToken);

        var result = await controller.Refresh();

        Assert.IsType<OkObjectResult>(result);

        // Response re-sets a rotated refresh cookie whose value differs from the old one.
        var newRaw = RefreshCookieValue(controller.Response);
        Assert.False(string.IsNullOrEmpty(newRaw));
        Assert.NotEqual(issued.RawToken, newRaw);

        // Old token is now inactive; exactly the rotated child is active.
        var repo = new RefreshTokenRepository(ctx);
        var active = await repo.GetActiveForUserAsync(userId);
        Assert.Single(active);
        Assert.Equal(RefreshTokenService.Hash(newRaw!), active[0].TokenHash);
    }

    [Fact]
    public async Task Refresh_replaying_a_rotated_token_returns_401_burns_family_and_audits()
    {
        await using var seedCtx = CreateContext();
        var userId = await SeedUser(seedCtx);
        var issueService = new RefreshTokenService(new RefreshTokenRepository(seedCtx), Options.Create(new JwtOptions()));
        var issued = await issueService.IssueAsync(userId, Guid.NewGuid(), "agent-a", "127.0.0.1", CancellationToken.None);
        await seedCtx.SaveChangesAsync();

        // First refresh: legitimate rotation.
        await using (var ctx1 = CreateContext())
        {
            var ok = await CreateController(ctx1, issued.RawToken).Refresh();
            Assert.IsType<OkObjectResult>(ok);
        }

        // Replay the now-rotated original token: must be rejected and burn the family.
        await using var ctx2 = CreateContext();
        var replay = await CreateController(ctx2, issued.RawToken).Refresh();
        Assert.IsType<UnauthorizedResult>(replay);

        var repo = new RefreshTokenRepository(ctx2);
        var active = await repo.GetActiveForUserAsync(userId);
        Assert.Empty(active);

        var audits = await ctx2.AuditLogs.Where(a => a.Action == "auth.refresh_reuse_detected").ToListAsync();
        Assert.Single(audits);
        Assert.Equal(userId, audits[0].ActorId);
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
