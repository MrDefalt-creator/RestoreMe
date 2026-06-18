using Backup.Server.Application.Interfaces;
using Backup.Shared.Contracts.DTOs.Audit;

namespace Backup.Server.Application.Services;

public class AuditLogService
{
    private readonly IAuditLogRepository _repository;

    public AuditLogService(IAuditLogRepository repository)
    {
        _repository = repository;
    }

    public async Task<AuditLogPageDto> QueryAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        string? action,
        Guid? actorId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var requestedPage = page < 1 ? 1 : page;
        var requestedPageSize = pageSize switch
        {
            < 1 => 50,
            > 200 => 200,
            _ => pageSize
        };

        var query = new AuditLogQuery(fromUtc, toUtc, action, actorId, requestedPage, requestedPageSize);
        var result = await _repository.QueryAsync(query, cancellationToken);

        var items = result.Items
            .Select(x => new AuditLogEntryDto(
                x.Log.Id,
                x.Log.ActorId,
                x.ActorUsername,
                x.Log.Action,
                x.Log.TargetId,
                x.Log.Details,
                x.Log.OccurredAt))
            .ToList();

        return new AuditLogPageDto(items, result.Total, requestedPage, requestedPageSize);
    }
}
