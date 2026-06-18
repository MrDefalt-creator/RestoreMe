namespace Backup.Agent.Worker.Startup;

// Resolved file-system layout for agent state. Computed once at process
// start so the same paths flow into the state store, the DataProtection
// key ring, and the --reset-state cleanup.
public sealed class AgentStateLocation
{
    public required string Directory { get; init; }
    public required string StateFilePath { get; init; }
    public required string KeyRingDirectory { get; init; }
    public required string Source { get; init; }
}
