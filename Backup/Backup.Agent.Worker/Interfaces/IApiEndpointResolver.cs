namespace Backup.Agent.Worker.Interfaces;

public interface IApiEndpointResolver
{
    Task<ResolvedApiEndpoint> ResolveAsync(CancellationToken cancellationToken);

    // Synchronous resolver used by HttpClientFactory delegates. The
    // underlying lookup hits local state (file or in-memory) so it stays
    // O(disk read); keeping it sync avoids GetAwaiter().GetResult()
    // deadlock surfaces in DI factories.
    ResolvedApiEndpoint Resolve();
}

public sealed record ResolvedApiEndpoint(string BaseUrl, string Source);
