using System.Globalization;
using System.Text.Json;
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
    public const string AuditActionSent = "notification.sent";
    public const string AuditActionFailed = "notification.failed";

    private readonly INotificationChannelRepository _channelRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IReadOnlyDictionary<NotificationChannelType, INotificationChannelAdapter> _adapters;
    private readonly ILogger<NotificationDispatcher> _logger;

    public NotificationDispatcher(
        INotificationChannelRepository channelRepository,
        IAuditLogRepository auditLogRepository,
        IEnumerable<INotificationChannelAdapter> adapters,
        ILogger<NotificationDispatcher> logger)
    {
        _channelRepository = channelRepository;
        _auditLogRepository = auditLogRepository;
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

    public Task NotifyBackupCompletedAsync(
        Guid jobId, Guid policyId, Guid agentId, string policyName,
        CancellationToken cancellationToken = default)
    {
        var evt = new NotificationEvent(
            NotificationEventType.BackupCompleted,
            "Backup job completed",
            $"Policy '{policyName}' finished on agent {agentId}",
            null,
            DateTime.UtcNow,
            new Dictionary<string, string?>
            {
                ["jobId"] = jobId.ToString(),
                ["policyId"] = policyId.ToString(),
                ["agentId"] = agentId.ToString(),
                ["policyName"] = policyName,
            });

        return DispatchAsync(evt, cancellationToken);
    }

    public Task NotifyAgentOfflineAsync(
        Guid agentId, string agentName, DateTime? lastSeenAt,
        CancellationToken cancellationToken = default)
    {
        var detail = lastSeenAt.HasValue
            ? $"Last seen at {lastSeenAt.Value:u}"
            : "Agent has never reported a heartbeat";

        var evt = new NotificationEvent(
            NotificationEventType.AgentOffline,
            "Agent offline",
            $"Agent '{agentName}' stopped sending heartbeats",
            detail,
            DateTime.UtcNow,
            new Dictionary<string, string?>
            {
                ["agentId"] = agentId.ToString(),
                ["agentName"] = agentName,
                ["lastSeenAt"] = lastSeenAt?.ToString("o"),
            });

        return DispatchAsync(evt, cancellationToken);
    }

    public Task NotifyAgentBackOnlineAsync(
        Guid agentId, string agentName,
        CancellationToken cancellationToken = default)
    {
        var evt = new NotificationEvent(
            NotificationEventType.AgentBackOnline,
            "Agent back online",
            $"Agent '{agentName}' resumed sending heartbeats",
            null,
            DateTime.UtcNow,
            new Dictionary<string, string?>
            {
                ["agentId"] = agentId.ToString(),
                ["agentName"] = agentName,
            });

        return DispatchAsync(evt, cancellationToken);
    }

    public Task NotifyPolicyAutoDisabledAsync(
        Guid policyId, Guid agentId, string policyName, int failures, string? lastReason,
        CancellationToken cancellationToken = default)
    {
        var evt = new NotificationEvent(
            NotificationEventType.PolicyAutoDisabled,
            "Policy auto-disabled",
            $"Policy '{policyName}' auto-disabled after {failures} consecutive failures",
            string.IsNullOrWhiteSpace(lastReason) ? null : $"Last error: {lastReason}",
            DateTime.UtcNow,
            new Dictionary<string, string?>
            {
                ["policyId"] = policyId.ToString(),
                ["agentId"] = agentId.ToString(),
                ["policyName"] = policyName,
                ["failures"] = failures.ToString(CultureInfo.InvariantCulture),
                ["lastReason"] = lastReason,
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
        Guid? actorId,
        CancellationToken cancellationToken = default)
    {
        DeliveryResult result;
        if (!_adapters.TryGetValue(channel.Type, out var adapter))
        {
            result = DeliveryResult.Failure($"No adapter registered for channel type {channel.Type}");
        }
        else
        {
            try
            {
                result = await adapter.SendAsync(channel, evt, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Test send threw for channel {ChannelId} ({Name})", channel.Id, channel.Name);
                result = DeliveryResult.Failure(ex.Message);
            }
        }

        await RecordAuditAsync(channel, evt, result, actorId, cancellationToken);
        return result;
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

        var auditEntries = new List<AuditLog>(channels.Count);
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
                auditEntries.Add(BuildAuditEntry(channel, evt, DeliveryResult.Failure("No adapter registered"), actorId: null));
                continue;
            }

            DeliveryResult result;
            try
            {
                result = await adapter.SendAsync(channel, evt, cancellationToken);
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
                result = DeliveryResult.Failure(ex.Message);
            }

            auditEntries.Add(BuildAuditEntry(channel, evt, result, actorId: null));
        }

        // Persist all audit rows in one round trip so we don't lengthen
        // the critical path by N SaveChanges calls. The dispatch itself
        // is best-effort; an audit-log write failure is logged but
        // doesn't bubble.
        if (auditEntries.Count > 0)
        {
            try
            {
                foreach (var entry in auditEntries)
                {
                    await _auditLogRepository.AddAsync(entry);
                }
                await _auditLogRepository.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to persist notification audit entries for event {EventType}", evt.Type);
            }
        }
    }

    private async Task RecordAuditAsync(
        NotificationChannel channel,
        NotificationEvent evt,
        DeliveryResult result,
        Guid? actorId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _auditLogRepository.AddAsync(BuildAuditEntry(channel, evt, result, actorId));
            await _auditLogRepository.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to write notification audit for channel {ChannelId} event {EventType}",
                channel.Id,
                evt.Type);
        }
    }

    private static AuditLog BuildAuditEntry(
        NotificationChannel channel,
        NotificationEvent evt,
        DeliveryResult result,
        Guid? actorId)
    {
        // Details intentionally exclude the rendered message body. The
        // body can contain agent names, policy names, and error text
        // that don't belong in a long-retention audit table. Bot tokens
        // and webhook URLs are never reachable from this surface because
        // the channel object stays inside the dispatcher.
        var details = JsonSerializer.Serialize(new
        {
            channelName = channel.Name,
            channelType = channel.Type.ToString(),
            eventType = evt.Type.ToString(),
            success = result.Success,
            error = result.Error,
        });

        return new AuditLog
        {
            Id = Guid.NewGuid(),
            ActorId = actorId,
            Action = result.Success ? AuditActionSent : AuditActionFailed,
            TargetId = channel.Id,
            Details = details,
            OccurredAt = DateTime.UtcNow,
        };
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
