using Backup.Server.Api.Security;
using Backup.Server.Domain.Entities;
using Backup.Server.Infrastructure.Configuration;
using Backup.Server.Infrastructure.Options;
using Backup.Server.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Backup.Server.Tests.Auth;

/// <summary>
/// Integration tests for RefreshTokenService. Mirrors the SQLite in-memory +
/// EphemeralDataProtectionProvider fixture used by RefreshTokenRepositoryTests,
/// but drives a real RefreshTokenRepository through the service so
/// FindByHashAsync/RevokeFamilyAsync actually persist across rotation steps.
/// </summary>
public sealed class RefreshTokenServiceTests : IAsyncLifetime
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

    private static RefreshTokenService CreateService(AppDbContext ctx, int reuseGraceSeconds = 30)
    {
        var repo = new RefreshTokenRepository(ctx);
        return new RefreshTokenService(
            repo,
            Options.Create(new JwtOptions { RefreshReuseGraceSeconds = reuseGraceSeconds }));
    }

    // Backdates the revocation of a specific token so a subsequent replay falls
    // outside the reuse grace window (simulates an attacker replaying a rotated
    // token minutes/hours later rather than in a same-instant race).
    private static async Task BackdateRevocation(AppDbContext ctx, string rawToken, TimeSpan age)
    {
        var hash = RefreshTokenService.Hash(rawToken);
        var row = await ctx.RefreshTokens.FirstAsync(x => x.TokenHash == hash);
        row.RevokedAtUtc = DateTime.UtcNow - age;
        await ctx.SaveChangesAsync();
    }

    private static async Task<Guid> SeedUser(AppDbContext ctx)
    {
        var userId = Guid.NewGuid();
        ctx.AppUsers.Add(new AppUser
        {
            Id = userId,
            Username = "refresh-svc-user",
            NormalizedUsername = "REFRESH-SVC-USER",
            PasswordHash = "irrelevant-hash",
            CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();
        return userId;
    }

    [Fact]
    public async Task Persistent_flag_is_carried_across_rotation()
    {
        // A remember-me session must stay persistent across every rotation, and a
        // session-only one must never be silently upgraded.
        await using var ctx = CreateContext();
        var service = CreateService(ctx);
        var userId = await SeedUser(ctx);

        var remembered = await service.IssueAsync(userId, Guid.NewGuid(), "a", "127.0.0.1", CancellationToken.None, persistent: true);
        var sessionOnly = await service.IssueAsync(userId, Guid.NewGuid(), "b", "127.0.0.1", CancellationToken.None, persistent: false);
        await ctx.SaveChangesAsync();

        var rememberedRotation = await service.RotateAsync(remembered.RawToken, "a", "127.0.0.1", CancellationToken.None);
        var sessionRotation = await service.RotateAsync(sessionOnly.RawToken, "b", "127.0.0.1", CancellationToken.None);

        Assert.True(rememberedRotation.Ok);
        Assert.True(rememberedRotation.Persistent);
        Assert.True(sessionRotation.Ok);
        Assert.False(sessionRotation.Persistent);

        // And the persisted child rows reflect the same choice.
        var childHash = RefreshTokenService.Hash(rememberedRotation.RawToken!);
        var child = await ctx.RefreshTokens.AsNoTracking().FirstAsync(x => x.TokenHash == childHash);
        Assert.True(child.Persistent);
    }

    [Fact]
    public void Hash_is_deterministic_hex_64()
    {
        var h = RefreshTokenService.Hash("abc");
        Assert.Equal(64, h.Length);
        Assert.Equal(h, RefreshTokenService.Hash("abc"));
    }

    [Fact]
    public async Task Replaying_a_stale_rotated_token_revokes_the_family()
    {
        // Arrange: issue T1, rotate -> T2 (T1 now revoked+ReplacedBy), then age
        // T1's revocation past the grace window so the replay reads as a genuine
        // reuse rather than a same-instant race.
        await using var ctx = CreateContext();
        var service = CreateService(ctx);
        var userId = await SeedUser(ctx);
        var familyId = Guid.NewGuid();

        var t1 = await service.IssueAsync(userId, familyId, "agent-a", "127.0.0.1", CancellationToken.None);
        await ctx.SaveChangesAsync();

        var rotation1 = await service.RotateAsync(t1.RawToken, "agent-a", "127.0.0.1", CancellationToken.None);
        Assert.True(rotation1.Ok);
        Assert.False(rotation1.ReuseDetected);
        Assert.NotNull(rotation1.RawToken);

        await BackdateRevocation(ctx, t1.RawToken, TimeSpan.FromMinutes(5));

        // Act: replay T1, long after it was rotated away.
        var replay = await service.RotateAsync(t1.RawToken, "attacker", "10.0.0.1", CancellationToken.None);

        // Assert: reuse detected, rotation rejected, and the WHOLE family
        // (including the legitimately-rotated T2) is now burned.
        Assert.False(replay.Ok);
        Assert.True(replay.ReuseDetected);
        Assert.Null(replay.RawToken);

        var repo = new RefreshTokenRepository(ctx);
        var active = await repo.GetActiveForUserAsync(userId);
        Assert.Empty(active);
    }

    [Fact]
    public async Task Rotating_a_token_yields_exactly_one_active_child_and_stale_replay_burns_the_family()
    {
        // Deterministic proxy for the rotation race invariant: a token can be
        // rotated at most once. After T1 -> T2, exactly one active token exists
        // (T2). Replaying a stale T1 must be rejected and burn the family, leaving
        // zero active tokens - never two forked children from one parent.
        await using var ctx = CreateContext();
        var service = CreateService(ctx);
        var userId = await SeedUser(ctx);
        var familyId = Guid.NewGuid();

        var t1 = await service.IssueAsync(userId, familyId, "agent-a", "127.0.0.1", CancellationToken.None);
        await ctx.SaveChangesAsync();

        var rotation1 = await service.RotateAsync(t1.RawToken, "agent-a", "127.0.0.1", CancellationToken.None);
        Assert.True(rotation1.Ok);
        Assert.False(rotation1.ReuseDetected);
        Assert.NotNull(rotation1.RawToken);

        var repo = new RefreshTokenRepository(ctx);
        var activeAfterFirstRotation = await repo.GetActiveForUserAsync(userId);
        Assert.Single(activeAfterFirstRotation);
        Assert.Equal(RefreshTokenService.Hash(rotation1.RawToken!), activeAfterFirstRotation[0].TokenHash);

        // Replaying the already-rotated (and now stale) T1 must fail and burn the family.
        await BackdateRevocation(ctx, t1.RawToken, TimeSpan.FromMinutes(5));
        var replay = await service.RotateAsync(t1.RawToken, "attacker", "10.0.0.1", CancellationToken.None);
        Assert.False(replay.Ok);
        Assert.True(replay.ReuseDetected);

        var activeAfterReplay = await repo.GetActiveForUserAsync(userId);
        Assert.Empty(activeAfterReplay);
    }

    [Fact]
    public async Task Immediate_replay_within_grace_is_benign_and_keeps_the_family()
    {
        // A concurrent tab / retried request presents the parent token moments
        // after it was rotated. This is a race, not a replay: the rotation must
        // be rejected (no new token) but the family must NOT be burned, and no
        // reuse alarm is raised. The freshly-minted child stays active.
        await using var ctx = CreateContext();
        var service = CreateService(ctx);
        var userId = await SeedUser(ctx);
        var familyId = Guid.NewGuid();

        var t1 = await service.IssueAsync(userId, familyId, "agent-a", "127.0.0.1", CancellationToken.None);
        await ctx.SaveChangesAsync();

        var rotation1 = await service.RotateAsync(t1.RawToken, "agent-a", "127.0.0.1", CancellationToken.None);
        Assert.True(rotation1.Ok);

        var replay = await service.RotateAsync(t1.RawToken, "agent-a", "127.0.0.1", CancellationToken.None);

        Assert.False(replay.Ok);
        Assert.False(replay.ReuseDetected);
        Assert.Null(replay.RawToken);

        // The legitimate child (T2) is untouched — user is NOT logged out.
        var repo = new RefreshTokenRepository(ctx);
        var active = await repo.GetActiveForUserAsync(userId);
        Assert.Single(active);
        Assert.Equal(RefreshTokenService.Hash(rotation1.RawToken!), active[0].TokenHash);
    }

    [Fact]
    public async Task Expired_token_is_not_treated_as_reuse()
    {
        // A user returning after the refresh lifetime presents an aged-out token.
        // It was never revoked, so it is a benign expiry (plain rejection), not a
        // replay: no family burn, no reuse alarm.
        await using var ctx = CreateContext();
        var service = CreateService(ctx);
        var userId = await SeedUser(ctx);
        var familyId = Guid.NewGuid();

        var t1 = await service.IssueAsync(userId, familyId, "agent-a", "127.0.0.1", CancellationToken.None);
        await ctx.SaveChangesAsync();

        // Age the token out.
        var hash = RefreshTokenService.Hash(t1.RawToken);
        var row = await ctx.RefreshTokens.FirstAsync(x => x.TokenHash == hash);
        row.ExpiresAtUtc = DateTime.UtcNow.AddDays(-1);
        await ctx.SaveChangesAsync();

        var result = await service.RotateAsync(t1.RawToken, "agent-a", "127.0.0.1", CancellationToken.None);

        Assert.False(result.Ok);
        Assert.False(result.ReuseDetected);
        Assert.Equal(userId, result.UserId);
    }

    [Fact]
    public async Task Replaying_a_logged_out_token_burns_the_family_even_within_grace()
    {
        // A token revoked WITHOUT rotation (e.g. logout / logout-all) has no
        // replacement. Re-presenting it is a replay regardless of timing, so it
        // burns the family immediately (the grace window only forgives rotated
        // tokens, which have a live child to protect).
        await using var ctx = CreateContext();
        var service = CreateService(ctx);
        var userId = await SeedUser(ctx);
        var familyId = Guid.NewGuid();

        var t1 = await service.IssueAsync(userId, familyId, "agent-a", "127.0.0.1", CancellationToken.None);
        await ctx.SaveChangesAsync();

        // Revoke it as logout would: RevokedAtUtc set, ReplacedByTokenHash null.
        var hash = RefreshTokenService.Hash(t1.RawToken);
        var row = await ctx.RefreshTokens.FirstAsync(x => x.TokenHash == hash);
        row.RevokedAtUtc = DateTime.UtcNow;
        await ctx.SaveChangesAsync();

        var replay = await service.RotateAsync(t1.RawToken, "attacker", "10.0.0.1", CancellationToken.None);

        Assert.False(replay.Ok);
        Assert.True(replay.ReuseDetected);
    }

    [Fact]
    public async Task TryMarkRotated_twice_on_same_active_token_wins_exactly_once()
    {
        // Directly exercises the atomicity guarantee at the repository level: the
        // conditional UPDATE - not an in-memory IsActive() check - is what gates
        // rotation. Two callers who both read the token as active (no re-fetch
        // between calls) must NOT both win: the first claim returns 1, the second
        // returns 0. This test would fail against a read-modify-write implementation.
        await using var ctx = CreateContext();
        var service = CreateService(ctx);
        var userId = await SeedUser(ctx);
        var familyId = Guid.NewGuid();

        var t1 = await service.IssueAsync(userId, familyId, "agent-a", "127.0.0.1", CancellationToken.None);
        await ctx.SaveChangesAsync();

        var repo = new RefreshTokenRepository(ctx);
        var now = DateTime.UtcNow;
        var hash = RefreshTokenService.Hash(t1.RawToken);

        var first = await repo.TryMarkRotatedAsync(hash, now, RefreshTokenService.Hash("child-a"), CancellationToken.None);
        var second = await repo.TryMarkRotatedAsync(hash, now, RefreshTokenService.Hash("child-b"), CancellationToken.None);

        Assert.Equal(1, first);
        Assert.Equal(0, second);
    }
}
