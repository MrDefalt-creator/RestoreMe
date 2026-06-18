namespace Backup.Shared.Contracts.DTOs.Audit;

public sealed record AuditLogEntryDto(
    Guid Id,
    Guid? ActorId,
    string? ActorUsername,
    string Action,
    Guid? TargetId,
    string? Details,
    DateTime OccurredAt);
