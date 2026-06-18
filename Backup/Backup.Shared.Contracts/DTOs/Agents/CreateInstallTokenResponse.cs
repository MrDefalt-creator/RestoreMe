namespace Backup.Shared.Contracts.DTOs.Agents;

public sealed record CreateInstallTokenResponse(Guid Id, string Token, DateTime ExpiresAt);
