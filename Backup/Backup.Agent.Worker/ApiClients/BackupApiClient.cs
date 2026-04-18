using System.Net.Http.Json;
using Backup.Agent.Worker.Interfaces;
using Backup.Shared.Contracts.DTOs;
using Backup.Shared.Contracts.DTOs.Jobs;

namespace Backup.Agent.Worker.ApiClients;

public class BackupApiClient : IBackupApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BackupApiClient> _logger;

    public BackupApiClient(HttpClient httpClient, ILogger<BackupApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<Guid> StartBackupJobAsync(Guid agentId, Guid policyId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting backup job");

        var response = await _httpClient.PostAsJsonAsync(
            "/api/BackupJobs/start",
            new StartBackupJobRequest(agentId, policyId),
            cancellationToken
        );
        
        response.EnsureSuccessStatusCode();
        
        var result = await response.Content.ReadFromJsonAsync<StartBackupJobResponse>();

        if (result == null)
        {
            throw new Exception("StartBackupJobResponse is null");
        }
        
        _logger.LogInformation("Backup job started");
        
        return result.Id;
        
    }

    public async Task FinishBackupJobAsync(Guid jobId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Finishing backup job");
        
        var response = await _httpClient.PostAsync(
            $"/api/BackupJobs/complete/{jobId}",
            null,
            cancellationToken
        );
        
        response.EnsureSuccessStatusCode();
        
        _logger.LogInformation("Backup job finished");
    }

    public async Task FailBackupJobAsync(Guid jobId, string errorMessage, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Failing backup job");
        
        var response = await _httpClient.PostAsJsonAsync(
            $"/api/BackupJobs/failed/",
            new FailedBackupJobRequest(jobId, errorMessage),
            cancellationToken);
        
        response.EnsureSuccessStatusCode();
        
        _logger.LogInformation("Backup job failed");
    }

    public async Task AddArtifactAsync(Guid jobId, string fileName, string objectKey, long size, string checksum,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Adding artifact");

        var response = await _httpClient.PostAsJsonAsync(
            $"api/BackupJobs/add_artifact/",
            new AddArtifactBackupJobRequest(jobId, fileName, objectKey, size, checksum),
            cancellationToken
        );
        
        response.EnsureSuccessStatusCode();
        
        _logger.LogInformation("Artifact added");
    }

    public async Task MarkPolicyExecutedAsync(Guid policyId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Marking policy executed");

        var response = await _httpClient.PostAsync(
            $"api/BackupJobs/mark_policy_executed/{policyId}",
            null,
            cancellationToken);
        
        response.EnsureSuccessStatusCode();
        
        _logger.LogInformation("Policy executed");
    }

    public async Task<UploadTicketResponse> RequestUploadTicketAsync(
        RequestUploadTicketRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "api/BackupJobs/upload_ticket",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<UploadTicketResponse>(
            cancellationToken: cancellationToken);

        if (result == null)
        {
            throw new Exception("Upload ticket response is empty.");
        }

        return result;
    }
    
    
}