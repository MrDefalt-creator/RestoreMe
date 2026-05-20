namespace Backup.Shared.Contracts.DTOs.Agents;

public sealed record CreateInstallTokenRequest(string? PreApprovedName, int? TtlMinutes);
