namespace Backup.Server.Domain.Entities;

public class AuditLog
{
    public Guid Id { get; set; }
    public Guid? ActorId { get; set; }
    public string Action { get; set; } = null!;
    public Guid? TargetId { get; set; }
    public string? Details { get; set; }
    public DateTime OccurredAt { get; set; }
}
