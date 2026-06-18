using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Backup.Server.Application.Notifications;
using Backup.Server.Domain.Entities;
using Backup.Server.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Backup.Server.Infrastructure.Services.Adapters;

/// <summary>
/// Discord webhook adapter. Sends a single embed per event with the
/// title at the top, the summary in the description, the error detail
/// in a code-block field, and the colour bar tinted to the event tone
/// (red for failures, green for completion, amber for agent health).
/// </summary>
public class DiscordAdapter : INotificationChannelAdapter
{
    private const int ColourRed = 0xE74C3C;
    private const int ColourGreen = 0x2ECC71;
    private const int ColourAmber = 0xE67E22;
    private const int ColourBlue = 0x3498DB;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<DiscordAdapter> _logger;

    public DiscordAdapter(HttpClient httpClient, ILogger<DiscordAdapter> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public NotificationChannelType ChannelType => NotificationChannelType.Discord;

    public async Task<DeliveryResult> SendAsync(
        NotificationChannel channel,
        NotificationEvent evt,
        CancellationToken cancellationToken = default)
    {
        DiscordSettings settings;
        try
        {
            settings = JsonSerializer.Deserialize<DiscordSettings>(channel.Settings, JsonOptions)
                ?? throw new InvalidOperationException("Empty settings payload");
        }
        catch (Exception ex)
        {
            return DeliveryResult.Failure($"Invalid Discord settings: {ex.Message}");
        }

        if (string.IsNullOrWhiteSpace(settings.WebhookUrl))
        {
            return DeliveryResult.Failure("Discord webhook URL is empty");
        }

        var payload = BuildPayload(evt);

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(settings.WebhookUrl, payload, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                return DeliveryResult.Failure($"Discord returned HTTP {(int)response.StatusCode}: {Trim(body)}");
            }

            return DeliveryResult.Ok();
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Discord delivery timed out for channel {ChannelId}", channel.Id);
            return DeliveryResult.Failure("Discord delivery timed out");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Discord delivery threw for channel {ChannelId}", channel.Id);
            return DeliveryResult.Failure(ex.Message);
        }
    }

    private static object BuildPayload(NotificationEvent evt)
    {
        var fields = new List<object>();
        if (!string.IsNullOrWhiteSpace(evt.Detail))
        {
            fields.Add(new
            {
                name = "Detail",
                value = $"```{Truncate(evt.Detail!, 1000)}```",
            });
        }

        var embed = new
        {
            title = Truncate(evt.Title, 256),
            description = Truncate(evt.Summary, 2000),
            color = PickColour(evt.Type),
            timestamp = evt.OccurredAt.ToString("o"),
            fields = fields.Count > 0 ? fields : null,
        };

        return new { embeds = new[] { embed } };
    }

    private static int PickColour(NotificationEventType type) => type switch
    {
        NotificationEventType.BackupFailed => ColourRed,
        NotificationEventType.RestoreFailed => ColourRed,
        NotificationEventType.AgentOffline => ColourAmber,
        NotificationEventType.PolicyAutoDisabled => ColourAmber,
        NotificationEventType.BackupCompleted => ColourGreen,
        NotificationEventType.AgentBackOnline => ColourGreen,
        _ => ColourBlue,
    };

    private static string Truncate(string value, int max)
    {
        return value.Length <= max ? value : value[..(max - 1)] + "…";
    }

    private static string Trim(string body) => body.Length > 200 ? body[..200] + "…" : body;
}
