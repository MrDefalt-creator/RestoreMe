using Backup.Server.Domain.Entities;

namespace Backup.Server.Application.Interfaces;

public interface IAgentInstallTokenRepository
{
    Task AddAsync(AgentInstallToken token, CancellationToken cancellationToken);

    // Returns the token row if the hash matches an unused, unexpired token.
    // Returns null otherwise. The atomic UsedAt update is done by the
    // service layer in the same SaveChanges call.
    Task<AgentInstallToken?> FindUsableByHashAsync(byte[] tokenHash, CancellationToken cancellationToken);

    // Deletes rows whose ExpiresAt was more than `graceBeforeUtc` ago and
    // which were never used. Used rows are kept so audit history stays
    // intact.
    Task<int> DeleteExpiredUnusedAsync(DateTime expiredBeforeUtc, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
