namespace Backup.Shared.Contracts.DTOs.Audit;

public sealed record AuditLogPageDto(
    IReadOnlyList<AuditLogEntryDto> Items,
    int Total,
    int Page,
    int PageSize);
