using Backup.Server.Domain.Entities;

namespace Backup.Server.Application.Interfaces;

public sealed record AuditLogQuery(
    DateTime? FromUtc,
    DateTime? ToUtc,
    string? Action,
    Guid? ActorId,
    int Page,
    int PageSize);

public sealed record AuditLogQueryResult(
    IReadOnlyList<AuditLogWithActor> Items,
    int Total);

public sealed record AuditLogWithActor(AuditLog Log, string? ActorUsername);

public interface IAuditLogRepository
{
    Task AddAsync(AuditLog log);
    Task SaveChangesAsync();
    Task<AuditLogQueryResult> QueryAsync(AuditLogQuery query, CancellationToken cancellationToken);
}
