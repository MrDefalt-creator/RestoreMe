namespace Backup.Agent.Worker.State;

public class AgentState
{
    public Guid AgentId { get; set; }
    public string ServerAddress { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
}
