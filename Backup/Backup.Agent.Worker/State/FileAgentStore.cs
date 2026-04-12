using System.Text.Json;

namespace Backup.Agent.Worker.State;

public class FileAgentStore : IAgentState
{
    private readonly string _fileName;

    public FileAgentStore()
    {
        var stateDir = Path.Combine(AppContext.BaseDirectory, "state");
        Directory.CreateDirectory(stateDir);
        
        _fileName = Path.Combine(stateDir, "agent-state.json");
    }

    public async Task<Guid?> TryGetAgentIdAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_fileName))
        {
            return null;
        }
        
        await using var stream = File.OpenRead(_fileName);
        var state = await JsonSerializer.DeserializeAsync<AgentState>(stream, cancellationToken: cancellationToken);

        if (state == null || state.AgentId == Guid.Empty)
        {
            return null;
        }
        
        return state.AgentId;
    }

    public async Task SaveAgentIdAsync(Guid agentId, CancellationToken cancellationToken)
    {
        var state = new AgentState
        {
            AgentId = agentId,
        };
        
        await using var stream = File.Create(_fileName);
        await JsonSerializer.SerializeAsync(stream, state, cancellationToken: cancellationToken);
    }
}