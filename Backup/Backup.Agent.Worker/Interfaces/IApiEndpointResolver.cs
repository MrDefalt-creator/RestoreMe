namespace Backup.Agent.Worker.Interfaces;

public interface IApiEndpointResolver
{
    Task<ResolvedApiEndpoint> ResolveAsync(CancellationToken cancellationToken);
}

public sealed record ResolvedApiEndpoint(string BaseUrl, string Source);
