using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Backup.Server.Application.Notifications;
using Backup.Server.Domain.Entities;
using Backup.Server.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Backup.Server.Infrastructure.Services.Adapters;

/// <summary>
/// Telegram Bot API adapter. Calls sendMessage with a Markdown-formatted
/// body so the title is bold and the error context lands in a fenced
/// code block. Operators need to talk to @BotFather once to get a token
/// and grab the chat id (e.g. via @userinfobot) — that pair is stored
/// in the channel Settings JSON.
/// </summary>
public class TelegramAdapter : INotificationChannelAdapter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<TelegramAdapter> _logger;

    public TelegramAdapter(HttpClient httpClient, ILogger<TelegramAdapter> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public NotificationChannelType ChannelType => NotificationChannelType.Telegram;

    public async Task<DeliveryResult> SendAsync(
        NotificationChannel channel,
        NotificationEvent evt,
        CancellationToken cancellationToken = default)
    {
        TelegramSettings settings;
        try
        {
            settings = JsonSerializer.Deserialize<TelegramSettings>(channel.Settings, JsonOptions)
                ?? throw new InvalidOperationException("Empty settings payload");
        }
        catch (Exception ex)
        {
            return DeliveryResult.Failure($"Invalid Telegram settings: {ex.Message}");
        }

        if (string.IsNullOrWhiteSpace(settings.BotToken) || string.IsNullOrWhiteSpace(settings.ChatId))
        {
            return DeliveryResult.Failure("Telegram bot token and chat id are required");
        }

        var text = BuildMessage(evt);
        var url = $"https://api.telegram.org/bot{settings.BotToken}/sendMessage";

        var payload = new
        {
            chat_id = settings.ChatId,
            text,
            parse_mode = "Markdown",
            disable_web_page_preview = true,
        };

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(url, payload, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                return DeliveryResult.Failure($"Telegram returned HTTP {(int)response.StatusCode}: {Trim(body)}");
            }

            return DeliveryResult.Ok();
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Telegram delivery timed out for channel {ChannelId}", channel.Id);
            return DeliveryResult.Failure("Telegram delivery timed out");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Telegram delivery threw for channel {ChannelId}", channel.Id);
            return DeliveryResult.Failure(ex.Message);
        }
    }

    private static string BuildMessage(NotificationEvent evt)
    {
        var lines = new List<string>
        {
            $"*{EscapeMarkdown(evt.Title)}*",
            $"_{EscapeMarkdown(evt.Summary)}_",
        };

        if (!string.IsNullOrWhiteSpace(evt.Detail))
        {
            lines.Add(string.Empty);
            lines.Add("```");
            lines.Add(evt.Detail);
            lines.Add("```");
        }

        return string.Join('\n', lines);
    }

    private static string EscapeMarkdown(string value)
    {
        // Legacy Markdown mode only treats *, _, `, [ as control chars.
        // Escaping these keeps policy names with underscores from
        // turning half a message into italics.
        var sb = new System.Text.StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (ch is '*' or '_' or '`' or '[')
            {
                sb.Append('\\');
            }
            sb.Append(ch);
        }
        return sb.ToString();
    }

    private static string Trim(string body) => body.Length > 200 ? body[..200] + "…" : body;
}
