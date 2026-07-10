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
        var now = DateTime.UtcNow;
        var current = await _repo.FindByHashAsync(Hash(rawToken), ct);
        if (current is null)
            return new RotationResult(false, null, default, Guid.Empty, false);

        // Reuse detection: a token presented after it was already rotated/revoked
        // means someone replayed a stolen copy -> burn the whole family.
        if (!current.IsActive(now))
        {
            await _repo.RevokeFamilyAsync(current.FamilyId, now, ct);
            await _repo.SaveChangesAsync(ct);
            return new RotationResult(false, null, default, current.UserId, true);
        }

        current.RevokedAtUtc = now;
        current.LastUsedAtUtc = now;
        var issued = await IssueAsync(current.UserId, current.FamilyId, ua, ip, ct);
        current.ReplacedByTokenHash = Hash(issued.RawToken);
        await _repo.SaveChangesAsync(ct);
        return new RotationResult(true, issued.RawToken, issued.ExpiresAtUtc, current.UserId, false);
    }
}
