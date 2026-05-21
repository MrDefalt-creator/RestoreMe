using System.Globalization;
using Backup.Server.Application.Interfaces;
using Backup.Server.Application.Notifications;
using Backup.Server.Domain.Entities;
using Backup.Server.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Backup.Server.Application.Services;

/// <summary>
/// Replaces the previous direct webhook call. Materializes a channel-
/// neutral NotificationEvent, fans it out to every enabled channel that
/// subscribes to the event type, and routes each call through the
/// matching adapter. Adapter failures never bubble — they're swallowed
/// per-channel so one broken Slack URL can't suppress Telegram delivery.
/// </summary>
public class NotificationDispatcher : INotificationService
{
    private readonly INotificationChannelRepository _channelRepository;
    private readonly IReadOnlyDictionary<NotificationChannelType, INotificationChannelAdapter> _adapters;
    private readonly ILogger<NotificationDispatcher> _logger;

    public NotificationDispatcher(
        INotificationChannelRepository channelRepository,
        IEnumerable<INotificationChannelAdapter> adapters,
        ILogger<NotificationDispatcher> logger)
    {
        _channelRepository = channelRepository;
        _adapters = adapters.ToDictionary(a => a.ChannelType);
        _logger = logger;
    }

    public Task NotifyBackupFailedAsync(
        Guid jobId, Guid policyId, Guid agentId, string errorMessage,
        CancellationToken cancellationToken = default)
    {
        var evt = new NotificationEvent(
            NotificationEventType.BackupFailed,
            "Backup job failed",
            $"Policy {policyId} on agent {agentId}",
            errorMessage,
            DateTime.UtcNow,
            new Dictionary<string, string?>
            {
                ["jobId"] = jobId.ToString(),
                ["policyId"] = policyId.ToString(),
                ["agentId"] = agentId.ToString(),
            });

        return DispatchAsync(evt, cancellationToken);
    }

    public Task NotifyRestoreFailedAsync(
        Guid jobId, Guid agentId, string errorMessage,
        CancellationToken cancellationToken = default)
    {
        var evt = new NotificationEvent(
            NotificationEventType.RestoreFailed,
            "Restore job failed",
            $"Agent {agentId}",
            errorMessage,
            DateTime.UtcNow,
            new Dictionary<string, string?>
            {
                ["jobId"] = jobId.ToString(),
                ["agentId"] = agentId.ToString(),
            });

        return DispatchAsync(evt, cancellationToken);
    }

    /// <summary>
    /// Sends an event to one explicit channel — used by the "Test channel"
    /// admin button. Bypasses the SubscribedEvents filter so operators
    /// can verify config even on a channel that hasn't opted into the
    /// test event type.
    /// </summary>
    public async Task<DeliveryResult> SendTestAsync(
        NotificationChannel channel,
        NotificationEvent evt,
        CancellationToken cancellationToken = default)
    {
        if (!_adapters.TryGetValue(channel.Type, out var adapter))
        {
            return DeliveryResult.Failure($"No adapter registered for channel type {channel.Type}");
        }

        try
        {
            return await adapter.SendAsync(channel, evt, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Test send threw for channel {ChannelId} ({Name})", channel.Id, channel.Name);
            return DeliveryResult.Failure(ex.Message);
        }
    }

    private async Task DispatchAsync(NotificationEvent evt, CancellationToken cancellationToken)
    {
        IReadOnlyList<NotificationChannel> channels;
        try
        {
            channels = await _channelRepository.GetEnabledAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Notification delivery is best-effort — never let a stale
            // DB connection cascade into the caller (a backup job that
            // just finished failing has more important things to do
            // than handle a notification crash).
            _logger.LogWarning(ex, "Failed to enumerate notification channels for event {EventType}", evt.Type);
            return;
        }

        foreach (var channel in channels)
        {
            if (!IsSubscribed(channel, evt.Type))
            {
                continue;
            }

            if (!_adapters.TryGetValue(channel.Type, out var adapter))
            {
                _logger.LogWarning(
                    "No adapter for channel {ChannelId} type {ChannelType} — skipping",
                    channel.Id,
                    channel.Type);
                continue;
            }

            try
            {
                var result = await adapter.SendAsync(channel, evt, cancellationToken);
                if (!result.Success)
                {
                    _logger.LogWarning(
                        "Notification delivery to {ChannelName} ({ChannelType}) failed: {Error}",
                        channel.Name,
                        channel.Type,
                        result.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Notification delivery to {ChannelName} ({ChannelType}) threw",
                    channel.Name,
                    channel.Type);
            }
        }
    }

    internal static bool IsSubscribed(NotificationChannel channel, NotificationEventType eventType)
    {
        // NULL or whitespace means "every event" — this matches the
        // default the UI sets on freshly created channels and keeps the
        // upgrade path trivial for the legacy single-webhook user.
        if (string.IsNullOrWhiteSpace(channel.SubscribedEvents))
        {
            return true;
        }

        var target = eventType.ToString();
        foreach (var token in channel.SubscribedEvents.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (string.Equals(token, target, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Tolerate numeric values too — older fixtures or hand-edits
            // sometimes carry the enum's underlying int.
            if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var asInt)
                && asInt == (int)eventType)
            {
                return true;
            }
        }

        return false;
    }
}
