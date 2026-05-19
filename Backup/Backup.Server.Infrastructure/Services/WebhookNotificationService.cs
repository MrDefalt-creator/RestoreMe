using System.Net.Http.Json;
using Backup.Server.Application.Interfaces;
using Backup.Server.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Backup.Server.Infrastructure.Services;

public class WebhookNotificationService : INotificationService
{
    private readonly HttpClient _httpClient;
    private readonly NotificationOptions _options;
    private readonly ILogger<WebhookNotificationService> _logger;

    public WebhookNotificationService(
        HttpClient httpClient,
        IOptions<NotificationOptions> options,
        ILogger<WebhookNotificationService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task NotifyBackupFailedAsync(
        Guid jobId, Guid policyId, Guid agentId, string errorMessage,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.FailureWebhookUrl)) return;

        try
        {
            var payload = new
            {
                event_type = "backup_failed",
                job_id = jobId,
                policy_id = policyId,
                agent_id = agentId,
                error_message = errorMessage,
                failed_at = DateTime.UtcNow
            };

            using var response = await _httpClient.PostAsJsonAsync(_options.FailureWebhookUrl, payload, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Webhook notification returned {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send backup failure webhook notification for job {JobId}", jobId);
        }
    }

    public async Task NotifyRestoreFailedAsync(
        Guid jobId, Guid agentId, string errorMessage,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.FailureWebhookUrl)) return;

        try
        {
            var payload = new
            {
                event_type = "restore_failed",
                job_id = jobId,
                agent_id = agentId,
                error_message = errorMessage,
                failed_at = DateTime.UtcNow
            };

            using var response = await _httpClient.PostAsJsonAsync(_options.FailureWebhookUrl, payload, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Webhook notification returned {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send restore failure webhook notification for job {JobId}", jobId);
        }
    }
}
