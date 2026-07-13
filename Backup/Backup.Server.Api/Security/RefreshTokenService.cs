using System.Security.Cryptography;
using System.Text;
using Backup.Server.Application.Interfaces;
using Backup.Server.Domain.Entities;
using Backup.Server.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Backup.Server.Api.Security;

public sealed record IssuedRefreshToken(string RawToken, DateTime ExpiresAtUtc);
public sealed record RotationResult(bool Ok, string? RawToken, DateTime ExpiresAtUtc, Guid UserId, bool ReuseDetected, bool Persistent = false);

public class RefreshTokenService
{
    private readonly IRefreshTokenRepository _repo;
    private readonly JwtOptions _jwt;
    public RefreshTokenService(IRefreshTokenRepository repo, IOptions<JwtOptions> jwt) { _repo = repo; _jwt = jwt.Value; }

    public static string Hash(string rawToken)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))).ToLowerInvariant();

    private static string NewRawToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

    public async Task<IssuedRefreshToken> IssueAsync(Guid userId, Guid familyId, string? ua, string? ip, CancellationToken ct, bool persistent = false)
    {
        var raw = NewRawToken();
        var expires = DateTime.UtcNow.AddDays(_jwt.RefreshLifetimeDays);
        await _repo.AddAsync(new RefreshToken
        {
            Id = Guid.NewGuid(), UserId = userId, TokenHash = Hash(raw), FamilyId = familyId,
            ExpiresAtUtc = expires, UserAgent = ua, CreatedByIp = ip, Persistent = persistent,
        }, ct);
        return new IssuedRefreshToken(raw, expires);
    }

    public async Task<RotationResult> RotateAsync(string rawToken, string? ua, string? ip, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(rawToken))
            return new RotationResult(false, null, default, Guid.Empty, false);

        var now = DateTime.UtcNow;
        var hash = Hash(rawToken);

        // Generate the replacement up front so we have its hash to stamp atomically.
        var newRaw = NewRawToken();
        var newHash = Hash(newRaw);
        var expires = now.AddDays(_jwt.RefreshLifetimeDays);

        // The atomic claim and the child-token insert must land together: a crash
        // between them would leave the parent revoked with no replacement, forcing
        // a re-login. Wrap them in one transaction (rolls back on any exception).
        await using var tx = await _repo.BeginTransactionAsync(ct);

        // Single atomic UPDATE: only succeeds if the token is currently active.
        // Concurrent callers racing on the same token can't both win -> no forked family.
        var affected = await _repo.TryMarkRotatedAsync(hash, now, newHash, ct);
        if (affected == 1)
        {
            // We won the race; the old row is already revoked+stamped. Re-fetch just
            // to read FamilyId/UserId for the new child row.
            var current = await _repo.FindByHashAsync(hash, ct);
            await _repo.AddAsync(new RefreshToken
            {
                Id = Guid.NewGuid(), UserId = current!.UserId, TokenHash = newHash,
                FamilyId = current.FamilyId, ExpiresAtUtc = expires, UserAgent = ua, CreatedByIp = ip,
                // Carry the remember-me choice across rotation so a refresh never
                // upgrades a session-only cookie into a persistent one.
                Persistent = current.Persistent,
            }, ct);
            await _repo.SaveChangesAsync(ct);
            await _repo.CommitTransactionAsync(ct);
            return new RotationResult(true, newRaw, expires, current.UserId, false, current.Persistent);
        }

        // affected == 0: token was not active. Disambiguate why before deciding
        // whether this is an attack — burning the family on a benign event
        // force-logs-out a legitimate user and raises a false reuse alarm.
        var existing = await _repo.FindByHashAsync(hash, ct);
        if (existing is null)
        {
            // Unknown token: nothing to burn, no known family. (No writes made,
            // so the transaction rolls back on dispose.)
            return new RotationResult(false, null, default, Guid.Empty, false);
        }

        // Benign expiry: a token that simply aged out (never revoked) is not a
        // replay. A user returning after the refresh lifetime should just be
        // asked to sign in again — not flagged as an attacker.
        if (existing.RevokedAtUtc is null && existing.ExpiresAtUtc <= now)
        {
            return new RotationResult(false, null, default, existing.UserId, false);
        }

        // Benign race / retry: the token was rotated very recently (it has a
        // replacement and was revoked within the grace window). This is a
        // concurrent tab or a retried request after a lost Set-Cookie, not a
        // replay of a long-dead token. The caller's newer cookie (the child)
        // is still valid, so don't burn the family.
        var graceCutoff = now.AddSeconds(-_jwt.RefreshReuseGraceSeconds);
        if (existing.ReplacedByTokenHash is not null
            && existing.RevokedAtUtc is not null
            && existing.RevokedAtUtc.Value > graceCutoff)
        {
            return new RotationResult(false, null, default, existing.UserId, false);
        }

        // Otherwise this is a genuine replay of a dead token (revoked outside
        // the grace window, or revoked without ever being rotated — e.g. after
        // logout). Burn the whole family.
        await _repo.RevokeFamilyAsync(existing.FamilyId, now, ct);
        await _repo.SaveChangesAsync(ct);
        await _repo.CommitTransactionAsync(ct);
        return new RotationResult(false, null, default, existing.UserId, true);
    }
}
