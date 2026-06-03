using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Backup.Server.Application.Notifications;
using Backup.Server.Domain.Entities;
using Backup.Server.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Backup.Server.Infrastructure.Services.Adapters;

/// <summary>
/// HMAC-signed generic webhook adapter. Replaces the previous standalone
/// WebhookNotificationService — same wire format and signature scheme so
/// existing receivers keep working, but the URL and secret now come from
/// per-channel Settings instead of NotificationOptions.
/// </summary>
public class GenericWebhookAdapter : INotificationChannelAdapter
{
    private const string SignatureHeader = "X-RestoreMe-Signature";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<GenericWebhookAdapter> _logger;

    public GenericWebhookAdapter(HttpClient httpClient, ILogger<GenericWebhookAdapter> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public NotificationChannelType ChannelType => NotificationChannelType.Webhook;

    public async Task<DeliveryResult> SendAsync(
        NotificationChannel channel,
        NotificationEvent evt,
        CancellationToken cancellationToken = default)
    {
        GenericWebhookSettings settings;
        try
        {
            settings = JsonSerializer.Deserialize<GenericWebhookSettings>(channel.Settings, JsonOptions)
                ?? throw new InvalidOperationException("Empty settings payload");
        }
        catch (Exception ex)
        {
            return DeliveryResult.Failure($"Invalid webhook settings: {ex.Message}");
        }

        if (string.IsNullOrWhiteSpace(settings.Url))
        {
            return DeliveryResult.Failure("Webhook URL is empty");
        }

        var payload = new
        {
            event_type = ToWireString(evt.Type),
            title = evt.Title,
            summary = evt.Summary,
            detail = evt.Detail,
            occurred_at = evt.OccurredAt,
            metadata = evt.Metadata,
        };

        try
        {
            var bodyBytes = JsonSerializer.SerializeToUtf8Bytes(payload);

            using var content = new ByteArrayContent(bodyBytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            using var request = new HttpRequestMessage(HttpMethod.Post, settings.Url)
            {
                Content = content,
            };

            if (!string.IsNullOrWhiteSpace(settings.Secret))
            {
                var signature = ComputeSignature(settings.Secret, bodyBytes);
                request.Headers.TryAddWithoutValidation(SignatureHeader, $"sha256={signature}");
            }

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return DeliveryResult.Failure($"Webhook returned HTTP {(int)response.StatusCode}");
            }

            return DeliveryResult.Ok();
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Webhook delivery timed out for channel {ChannelId}", channel.Id);
            return DeliveryResult.Failure("Webhook delivery timed out");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Webhook delivery threw for channel {ChannelId}", channel.Id);
            return DeliveryResult.Failure(ex.Message);
        }
    }

    private static string ComputeSignature(string secret, byte[] body)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(body);
        return Convert.ToHexStringLower(hash);
    }

    private static string ToWireString(NotificationEventType type) => type switch
    {
        NotificationEventType.BackupFailed => "backup_failed",
        NotificationEventType.RestoreFailed => "restore_failed",
        NotificationEventType.BackupCompleted => "backup_completed",
        NotificationEventType.AgentOffline => "agent_offline",
        NotificationEventType.AgentBackOnline => "agent_back_online",
        NotificationEventType.PolicyAutoDisabled => "policy_auto_disabled",
        _ => type.ToString().ToLowerInvariant(),
    };
}
