namespace Backup.Shared.Contracts.DTOs.Agents;

/// <summary>
/// Selectors the operator chooses in the "Delete agent" dialog. Each
/// flag defaults to <c>true</c> so an empty body is equivalent to the
/// pre-existing "purge everything" behaviour.
/// </summary>
public record DeleteAgentOptions(
    bool PurgeBackupHistory = true,
    bool PurgeStorageFiles = true,
    bool PurgeRestoreHistory = true);
