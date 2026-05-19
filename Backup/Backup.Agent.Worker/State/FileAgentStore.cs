using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace Backup.Agent.Worker.State;

public class FileAgentStore : IAgentState
{
    private readonly string _fileName;
    private readonly IDataProtector _protector;

    public FileAgentStore(IDataProtectionProvider dataProtectionProvider)
    {
        _protector = dataProtectionProvider.CreateProtector("AgentState.v1");

        var stateDir = Path.Combine(AppContext.BaseDirectory, "state");
        Directory.CreateDirectory(stateDir);

        _fileName = Path.Combine(stateDir, "agent-state.json");
    }

    private async Task<AgentState?> LoadStateAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_fileName)) return null;

        try
        {
            var cipherText = await File.ReadAllTextAsync(_fileName, cancellationToken);
            var json = _protector.Unprotect(cipherText);
            return JsonSerializer.Deserialize<AgentState>(json);
        }
        catch
        {
            return null;
        }
    }

    private async Task SaveStateAsync(AgentState state, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(state);
        var cipherText = _protector.Protect(json);
        await File.WriteAllTextAsync(_fileName, cipherText, Encoding.UTF8, cancellationToken);
    }

    public async Task<Guid?> TryGetAgentIdAsync(CancellationToken cancellationToken)
    {
        var state = await LoadStateAsync(cancellationToken);
        if (state == null || state.AgentId == Guid.Empty) return null;
        return state.AgentId;
    }

    public async Task<string?> TryGetServerAddressAsync(CancellationToken cancellationToken)
    {
        var state = await LoadStateAsync(cancellationToken);
        if (state == null || string.IsNullOrWhiteSpace(state.ServerAddress)) return null;
        return state.ServerAddress;
    }

    public async Task<string?> TryGetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var state = await LoadStateAsync(cancellationToken);
        if (state == null || string.IsNullOrWhiteSpace(state.AccessToken)) return null;
        return state.AccessToken;
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

    public async Task SaveAccessTokenAsync(string accessToken, CancellationToken cancellationToken)
    {
        var state = await LoadStateAsync(cancellationToken) ?? new AgentState();
        state.AccessToken = accessToken;
        await SaveStateAsync(state, cancellationToken);
    }
}
