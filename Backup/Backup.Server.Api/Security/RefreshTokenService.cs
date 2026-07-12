using System.Security.Cryptography;
using System.Text;
using Backup.Server.Application.Interfaces;
using Backup.Server.Domain.Entities;
using Backup.Server.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Backup.Server.Api.Security;

public sealed record IssuedRefreshToken(string RawToken, DateTime ExpiresAtUtc);
public sealed record RotationResult(bool Ok, string? RawToken, DateTime ExpiresAtUtc, Guid UserId, bool ReuseDetected);

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

    public async Task<IssuedRefreshToken> IssueAsync(Guid userId, Guid familyId, string? ua, string? ip, CancellationToken ct)
    {
        var raw = NewRawToken();
        var expires = DateTime.UtcNow.AddDays(_jwt.RefreshLifetimeDays);
        await _repo.AddAsync(new RefreshToken
        {
            Id = Guid.NewGuid(), UserId = userId, TokenHash = Hash(raw), FamilyId = familyId,
            ExpiresAtUtc = expires, UserAgent = ua, CreatedByIp = ip,
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
            }, ct);
            await _repo.SaveChangesAsync(ct);
            await _repo.CommitTransactionAsync(ct);
            return new RotationResult(true, newRaw, expires, current.UserId, false);
        }

        // affected == 0: token was not active (never existed, or already
        // rotated/revoked/expired). If it exists at all, this is a replay of a
        // dead token -> burn the whole family.
        var existing = await _repo.FindByHashAsync(hash, ct);
        if (existing is null)
            return new RotationResult(false, null, default, Guid.Empty, false);

        await _repo.RevokeFamilyAsync(existing.FamilyId, now, ct);
        await _repo.SaveChangesAsync(ct);
        await _repo.CommitTransactionAsync(ct);
        return new RotationResult(false, null, default, existing.UserId, true);
    }
}
