using Backup.Server.Application.Interfaces;
using Backup.Server.Application.Notifications;
using Backup.Server.Domain.Entities;
using Backup.Server.Domain.Enums;
using Backup.Shared.Contracts.DTOs.Notifications;

namespace Backup.Server.Application.Services;

/// <summary>
/// CRUD + test-send orchestration for the admin notification channels
/// page. The repository owns the entity layer; this service handles
/// validation, enum parsing, secret retention on update, and the
/// "preserve existing settings on null" rule the editor relies on.
/// </summary>
public class NotificationChannelsService
{
    private readonly INotificationChannelRepository _repository;
    private readonly NotificationDispatcher _dispatcher;
    private readonly IAdminEventBroadcaster _eventBroadcaster;

    public NotificationChannelsService(
        INotificationChannelRepository repository,
        NotificationDispatcher dispatcher,
        IAdminEventBroadcaster eventBroadcaster)
    {
        _repository = repository;
        _dispatcher = dispatcher;
        _eventBroadcaster = eventBroadcaster;
    }

    public async Task<List<NotificationChannelDto>> ListAsync(CancellationToken cancellationToken)
    {
        var channels = await _repository.GetAllAsync(cancellationToken);
        return channels.Select(MapToDto).ToList();
    }

    public async Task<NotificationChannelDto> CreateAsync(CreateNotificationChannelRequest request, CancellationToken cancellationToken)
    {
        ValidateName(request.Name);
        var type = ParseChannelType(request.Type);
        ValidateSettings(request.Settings);

        var existing = await _repository.GetByNameAsync(request.Name.Trim(), cancellationToken);
        if (existing is not null)
        {
            throw new InvalidOperationException("A notification channel with this name already exists.");
        }

        var channel = new NotificationChannel
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Type = type,
            IsEnabled = request.IsEnabled,
            Settings = request.Settings,
            SubscribedEvents = NormalizeSubscribedEvents(request.SubscribedEvents),
            CreatedAt = DateTime.UtcNow,
        };

        await _repository.AddAsync(channel, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        _eventBroadcaster.Publish(AdminEventTopic.NotificationChannels);

        return MapToDto(channel);
    }

    public async Task<NotificationChannelDto> UpdateAsync(Guid channelId, UpdateNotificationChannelRequest request, CancellationToken cancellationToken)
    {
        var channel = await _repository.GetByIdAsync(channelId, cancellationToken)
            ?? throw new KeyNotFoundException($"Channel {channelId} not found.");

        ValidateName(request.Name);
        if (request.Settings is not null)
        {
            ValidateSettings(request.Settings);
        }

        var trimmedName = request.Name.Trim();
        if (!string.Equals(trimmedName, channel.Name, StringComparison.OrdinalIgnoreCase))
        {
            var clashing = await _repository.GetByNameAsync(trimmedName, cancellationToken);
            if (clashing is not null && clashing.Id != channelId)
            {
                throw new InvalidOperationException("A notification channel with this name already exists.");
            }
        }

        channel.Name = trimmedName;
        channel.IsEnabled = request.IsEnabled;
        channel.SubscribedEvents = NormalizeSubscribedEvents(request.SubscribedEvents);

        // Null Settings means "keep what's already encrypted at rest" so
        // operators can rename or toggle a channel without re-entering
        // bot tokens. Any non-null value (even "") replaces the blob.
        if (request.Settings is not null)
        {
            channel.Settings = request.Settings;
        }

        channel.UpdatedAt = DateTime.UtcNow;

        _repository.Update(channel);
        await _repository.SaveChangesAsync(cancellationToken);

        _eventBroadcaster.Publish(AdminEventTopic.NotificationChannels);

        return MapToDto(channel);
    }

    public async Task DeleteAsync(Guid channelId, CancellationToken cancellationToken)
    {
        var channel = await _repository.GetByIdAsync(channelId, cancellationToken)
            ?? throw new KeyNotFoundException($"Channel {channelId} not found.");

        _repository.Remove(channel);
        await _repository.SaveChangesAsync(cancellationToken);

        _eventBroadcaster.Publish(AdminEventTopic.NotificationChannels);
    }

    public async Task<TestNotificationChannelResponse> TestAsync(Guid channelId, Guid? actorId, CancellationToken cancellationToken)
    {
        var channel = await _repository.GetByIdAsync(channelId, cancellationToken)
            ?? throw new KeyNotFoundException($"Channel {channelId} not found.");

        var evt = new NotificationEvent(
            NotificationEventType.BackupCompleted,
            "Test notification",
            $"This is a test message from RestoreMe for channel '{channel.Name}'.",
            "If you can read this, the channel is configured correctly.",
            DateTime.UtcNow,
            new Dictionary<string, string?>
            {
                ["channelId"] = channelId.ToString(),
                ["test"] = "true",
            });

        var result = await _dispatcher.SendTestAsync(channel, evt, actorId, cancellationToken);
        return new TestNotificationChannelResponse(result.Success, result.Error);
    }

    private static NotificationChannelDto MapToDto(NotificationChannel channel) =>
        new(
            channel.Id,
            channel.Name,
            channel.Type.ToString(),
            channel.IsEnabled,
            ParseSubscribedEvents(channel.SubscribedEvents),
            channel.CreatedAt,
            channel.UpdatedAt);

    private static IReadOnlyList<string> ParseSubscribedEvents(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Array.Empty<string>();
        }

        return raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();
    }

    private static string? NormalizeSubscribedEvents(IReadOnlyList<string>? events)
    {
        if (events is null || events.Count == 0)
        {
            return null;
        }

        var normalized = new List<string>(events.Count);
        foreach (var raw in events)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            if (!Enum.TryParse<NotificationEventType>(raw.Trim(), ignoreCase: true, out var parsed))
            {
                throw new InvalidOperationException($"Unknown notification event type: {raw}");
            }

            normalized.Add(parsed.ToString());
        }

        return normalized.Count == 0 ? null : string.Join(',', normalized.Distinct(StringComparer.Ordinal));
    }

    private static NotificationChannelType ParseChannelType(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || !Enum.TryParse<NotificationChannelType>(raw, ignoreCase: true, out var parsed))
        {
            throw new InvalidOperationException($"Unknown notification channel type: {raw}");
        }
        return parsed;
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 150)
        {
            throw new InvalidOperationException("Channel name must be 1–150 characters.");
        }
    }

    private static void ValidateSettings(string settings)
    {
        if (string.IsNullOrWhiteSpace(settings))
        {
            throw new InvalidOperationException("Channel settings must not be empty.");
        }
        if (settings.Length > 4000)
        {
            throw new InvalidOperationException("Channel settings payload is too large.");
        }
    }
}
