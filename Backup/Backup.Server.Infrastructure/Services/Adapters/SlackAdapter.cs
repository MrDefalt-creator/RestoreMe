using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Backup.Server.Application.Notifications;
using Backup.Server.Domain.Entities;
using Backup.Server.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Backup.Server.Infrastructure.Services.Adapters;

/// <summary>
/// Slack incoming-webhook adapter. Posts a Block Kit payload — header
/// for the event title, mrkdwn section for the summary, and an optional
/// code-style block for the error detail. The webhook URL itself is the
/// only secret; Slack does not require per-message authentication.
/// </summary>
public class SlackAdapter : INotificationChannelAdapter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<SlackAdapter> _logger;

    public SlackAdapter(HttpClient httpClient, ILogger<SlackAdapter> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public NotificationChannelType ChannelType => NotificationChannelType.Slack;

    public async Task<DeliveryResult> SendAsync(
        NotificationChannel channel,
        NotificationEvent evt,
        CancellationToken cancellationToken = default)
    {
        SlackSettings settings;
        try
        {
            settings = JsonSerializer.Deserialize<SlackSettings>(channel.Settings, JsonOptions)
                ?? throw new InvalidOperationException("Empty settings payload");
        }
        catch (Exception ex)
        {
            return DeliveryResult.Failure($"Invalid Slack settings: {ex.Message}");
        }

        if (string.IsNullOrWhiteSpace(settings.WebhookUrl))
        {
            return DeliveryResult.Failure("Slack webhook URL is empty");
        }

        var payload = BuildPayload(evt);

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(settings.WebhookUrl, payload, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                return DeliveryResult.Failure($"Slack returned HTTP {(int)response.StatusCode}: {Trim(body)}");
            }

            return DeliveryResult.Ok();
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Slack delivery timed out for channel {ChannelId}", channel.Id);
            return DeliveryResult.Failure("Slack delivery timed out");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Slack delivery threw for channel {ChannelId}", channel.Id);
            return DeliveryResult.Failure(ex.Message);
        }
    }

    private static object BuildPayload(NotificationEvent evt)
    {
        var blocks = new List<object>
        {
            new
            {
                type = "header",
                text = new { type = "plain_text", text = evt.Title, emoji = false },
            },
            new
            {
                type = "section",
                text = new { type = "mrkdwn", text = evt.Summary },
            },
        };

        if (!string.IsNullOrWhiteSpace(evt.Detail))
        {
            blocks.Add(new
            {
                type = "section",
                text = new { type = "mrkdwn", text = $"```{evt.Detail}```" },
            });
        }

        // The top-level "text" is a fallback for clients that don't
        // render blocks (e.g. mobile notifications previews).
        return new
        {
            text = evt.Title,
            blocks,
        };
    }

    private static string Trim(string body) => body.Length > 200 ? body[..200] + "…" : body;
}
