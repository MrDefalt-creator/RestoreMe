using Backup.Server.Application.Interfaces;
using Backup.Server.Domain.Entities;
using Backup.Server.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;

namespace Backup.Server.Infrastructure.Services;

public class AgentInstallTokenRepository : IAgentInstallTokenRepository
{
    private readonly AppDbContext _db;

    public AgentInstallTokenRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(AgentInstallToken token, CancellationToken cancellationToken)
    {
        await _db.AgentInstallTokens.AddAsync(token, cancellationToken);
    }

    public Task<AgentInstallToken?> FindUsableByHashAsync(byte[] tokenHash, CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;
        return _db.AgentInstallTokens
            .FirstOrDefaultAsync(
                t => t.TokenHash == tokenHash
                     && t.UsedAt == null
                     && t.ExpiresAt > nowUtc,
                cancellationToken);
    }

    public Task<int> DeleteExpiredUnusedAsync(DateTime expiredBeforeUtc, CancellationToken cancellationToken)
    {
        return _db.AgentInstallTokens
            .Where(t => t.UsedAt == null && t.ExpiresAt < expiredBeforeUtc)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _db.SaveChangesAsync(cancellationToken);
    }
}
