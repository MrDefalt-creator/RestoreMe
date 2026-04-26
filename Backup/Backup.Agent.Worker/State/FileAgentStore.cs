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

    private async Task<AgentState?> LoadStateAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_fileName))
        {
            return null;
        }

        await using var stream = File.OpenRead(_fileName);
        return await JsonSerializer.DeserializeAsync<AgentState>(stream, cancellationToken: cancellationToken);
    }

    private async Task SaveStateAsync(AgentState state, CancellationToken cancellationToken)
    {
        await using var stream = File.Create(_fileName);
        await JsonSerializer.SerializeAsync(stream, state, cancellationToken: cancellationToken);
    }

    public async Task<Guid?> TryGetAgentIdAsync(CancellationToken cancellationToken)
    {
        var state = await LoadStateAsync(cancellationToken);

        if (state == null || state.AgentId == Guid.Empty)
        {
            return null;
        }
        
        return state.AgentId;
    }

    public async Task<string?> TryGetServerAddressAsync(CancellationToken cancellationToken)
    {
        var state = await LoadStateAsync(cancellationToken);

        if (state == null || string.IsNullOrWhiteSpace(state.ServerAddress))
        {
            return null;
        }

        return state.ServerAddress;
    }

    public async Task SaveAgentIdAsync(Guid agentId, CancellationToken cancellationToken)
    {
        var state = await LoadStateAsync(cancellationToken) ?? new AgentState();
        state.AgentId = agentId;

        await SaveStateAsync(state, cancellationToken);
    }

    public async Task SaveServerAddressAsync(string serverAddress, CancellationToken cancellationToken)
    {
        var state = await LoadStateAsync(cancellationToken) ?? new AgentState();
        state.ServerAddress = serverAddress;

        await SaveStateAsync(state, cancellationToken);
    }
}
