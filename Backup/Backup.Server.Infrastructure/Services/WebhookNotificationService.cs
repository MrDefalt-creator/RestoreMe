using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Backup.Server.Application.Interfaces;
using Backup.Server.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Backup.Server.Infrastructure.Services;

public class WebhookNotificationService : INotificationService
{
    private const string SignatureHeader = "X-RestoreMe-Signature";

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

    public Task NotifyBackupFailedAsync(
        Guid jobId, Guid policyId, Guid agentId, string errorMessage,
        CancellationToken cancellationToken = default)
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

        return SendAsync(payload, jobId, cancellationToken);
    }

    public Task NotifyRestoreFailedAsync(
        Guid jobId, Guid agentId, string errorMessage,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            event_type = "restore_failed",
            job_id = jobId,
            agent_id = agentId,
            error_message = errorMessage,
            failed_at = DateTime.UtcNow
        };

        return SendAsync(payload, jobId, cancellationToken);
    }

    private async Task SendAsync(object payload, Guid jobId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.FailureWebhookUrl)) return;

        try
        {
            // Serialize once so the bytes used for HMAC and the bytes on the wire
            // are byte-for-byte identical — receivers HMAC the raw body.
            var bodyBytes = JsonSerializer.SerializeToUtf8Bytes(payload);

            using var content = new ByteArrayContent(bodyBytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            using var request = new HttpRequestMessage(HttpMethod.Post, _options.FailureWebhookUrl)
            {
                Content = content,
            };

            if (!string.IsNullOrWhiteSpace(_options.WebhookSecret))
            {
                var signature = ComputeSignature(_options.WebhookSecret, bodyBytes);
                request.Headers.TryAddWithoutValidation(SignatureHeader, $"sha256={signature}");
            }

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Webhook notification returned {StatusCode}", response.StatusCode);
            }
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Webhook notification timed out for job {JobId}", jobId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send webhook notification for job {JobId}", jobId);
        }
    }

    private static string ComputeSignature(string secret, byte[] body)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(body);
        return Convert.ToHexStringLower(hash);
    }
}
