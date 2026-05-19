using System.Net;
using System.Net.Http.Json;
using Backup.Agent.Worker.Interfaces;
using Backup.Shared.Contracts.DTOs.Restore;

namespace Backup.Agent.Worker.ApiClients;

public class RestoreApiClient : IRestoreApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RestoreApiClient> _logger;

    public RestoreApiClient(HttpClient httpClient, ILogger<RestoreApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<PendingRestoreResponse?> GetPendingRestoreAsync(Guid agentId, CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetAsync("api/Restore/pending", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NoContent) return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PendingRestoreResponse>(cancellationToken: cancellationToken);
    }

    public async Task<string> RequestDownloadTicketAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var response = await _httpClient.PostAsync($"api/Restore/download_ticket/{jobId}", null, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<DownloadTicketResponse>(cancellationToken: cancellationToken);
        return result?.DownloadUrl ?? throw new InvalidOperationException("Download ticket response is empty.");
    }

    public async Task CompleteRestoreJobAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var response = await _httpClient.PostAsync($"api/Restore/complete/{jobId}", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task FailRestoreJobAsync(Guid jobId, string errorMessage, CancellationToken cancellationToken)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "api/Restore/failed",
            new FailRestoreJobRequest(jobId, errorMessage),
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
