namespace Backup.Agent.Worker.Options;

public sealed record AgentOptions
{
    public const string SectionName = "Agent";

    public Guid? AgentId { get; init; }
    public int HeartbeatIntervalSeconds { get; init; } = 15;
    public int PolicySyncIntervalSeconds { get; init; } = 60;
}