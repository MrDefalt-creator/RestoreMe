using Backup.Server.Domain.Entities;

namespace Backup.Server.Application.Interfaces;

public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken token, CancellationToken ct = default);

    Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken ct = default);

    // Active = not revoked and not yet expired. Newest-first.
    Task<IReadOnlyList<RefreshToken>> GetActiveForUserAsync(Guid userId, CancellationToken ct = default);

    // Revokes every still-active token sharing the given rotation lineage
    // (reuse-detection path: one compromised token in a family kills the
    // whole chain).
    Task RevokeFamilyAsync(Guid familyId, DateTime nowUtc, CancellationToken ct = default);

    // Revokes every still-active token for the user (logout-everywhere /
    // password-change invalidation path).
    Task RevokeAllForUserAsync(Guid userId, DateTime nowUtc, CancellationToken ct = default);

    // Atomically revoke the token identified by tokenHash ONLY if it is currently active
    // (RevokedAtUtc == null && ExpiresAtUtc > nowUtc), stamping it as rotated. Returns rows
    // affected (1 = this caller won the rotation race; 0 = token was not active / not found).
    // This is a single DB UPDATE so concurrent callers cannot both win.
    Task<int> TryMarkRotatedAsync(string tokenHash, DateTime nowUtc, string replacedByTokenHash, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
