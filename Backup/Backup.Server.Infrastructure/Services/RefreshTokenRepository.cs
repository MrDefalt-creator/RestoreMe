using Backup.Server.Application.Interfaces;
using Backup.Server.Domain.Entities;
using Backup.Server.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;

namespace Backup.Server.Infrastructure.Services;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly AppDbContext _db;

    public RefreshTokenRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(RefreshToken token, CancellationToken ct = default)
        => await _db.RefreshTokens.AddAsync(token, ct);

    public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken ct = default)
        => _db.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == tokenHash, ct);

    public async Task<IReadOnlyList<RefreshToken>> GetActiveForUserAsync(Guid userId, CancellationToken ct = default)
        => await _db.RefreshTokens
            .Where(x => x.UserId == userId && x.RevokedAtUtc == null && x.ExpiresAtUtc > DateTime.UtcNow)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(ct);

    public async Task RevokeFamilyAsync(Guid familyId, DateTime nowUtc, CancellationToken ct = default)
    {
        var rows = await _db.RefreshTokens
            .Where(x => x.FamilyId == familyId && x.RevokedAtUtc == null)
            .ToListAsync(ct);
        foreach (var r in rows) r.RevokedAtUtc = nowUtc;
    }

    public async Task RevokeAllForUserAsync(Guid userId, DateTime nowUtc, CancellationToken ct = default)
    {
        var rows = await _db.RefreshTokens
            .Where(x => x.UserId == userId && x.RevokedAtUtc == null)
            .ToListAsync(ct);
        foreach (var r in rows) r.RevokedAtUtc = nowUtc;
    }

    public Task<int> TryMarkRotatedAsync(string tokenHash, DateTime nowUtc, string replacedByTokenHash, CancellationToken ct = default)
        => _db.RefreshTokens
            .Where(x => x.TokenHash == tokenHash && x.RevokedAtUtc == null && x.ExpiresAtUtc > nowUtc)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.RevokedAtUtc, nowUtc)
                .SetProperty(x => x.LastUsedAtUtc, nowUtc)
                .SetProperty(x => x.ReplacedByTokenHash, replacedByTokenHash), ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
