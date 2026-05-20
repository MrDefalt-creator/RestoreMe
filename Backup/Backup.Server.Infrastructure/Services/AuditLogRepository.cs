using Backup.Server.Application.Interfaces;
using Backup.Server.Domain.Entities;
using Backup.Server.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;

namespace Backup.Server.Infrastructure.Services;

public class AuditLogRepository : IAuditLogRepository
{
    private readonly AppDbContext _db;

    public AuditLogRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(AuditLog log)
    {
        await _db.AuditLogs.AddAsync(log);
    }

    public async Task SaveChangesAsync()
    {
        await _db.SaveChangesAsync();
    }

    public async Task<AuditLogQueryResult> QueryAsync(AuditLogQuery query, CancellationToken cancellationToken)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize switch
        {
            < 1 => 50,
            > 200 => 200,
            _ => query.PageSize
        };

        var logs = _db.AuditLogs.AsNoTracking().AsQueryable();

        if (query.FromUtc.HasValue)
            logs = logs.Where(l => l.OccurredAt >= query.FromUtc.Value);
        if (query.ToUtc.HasValue)
            logs = logs.Where(l => l.OccurredAt <= query.ToUtc.Value);
        if (!string.IsNullOrWhiteSpace(query.Action))
            logs = logs.Where(l => l.Action == query.Action);
        if (query.ActorId.HasValue)
            logs = logs.Where(l => l.ActorId == query.ActorId.Value);

        var total = await logs.CountAsync(cancellationToken);

        var paged = await logs
            .OrderByDescending(l => l.OccurredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .GroupJoin(
                _db.AppUsers.AsNoTracking(),
                log => log.ActorId,
                user => (Guid?)user.Id,
                (log, users) => new { log, users })
            .SelectMany(
                x => x.users.DefaultIfEmpty(),
                (x, user) => new AuditLogWithActor(x.log, user != null ? user.Username : null))
            .ToListAsync(cancellationToken);

        return new AuditLogQueryResult(paged, total);
    }
}
