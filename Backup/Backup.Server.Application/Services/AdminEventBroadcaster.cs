using System.Collections.Concurrent;
using System.Threading.Channels;
using Backup.Server.Application.Interfaces;

namespace Backup.Server.Application.Services;

/// <summary>
/// Channel-based fan-out, registered as a singleton. Each subscriber gets
/// its own bounded channel; a slow or stalled SSE connection drops its
/// oldest pending topics instead of back-pressuring the publisher, so a
/// wedged browser tab can never stall a backup job's status write.
/// </summary>
public sealed class AdminEventBroadcaster : IAdminEventBroadcaster
{
    // Five topics exist, so 64 pending entries per subscriber is reached
    // only when a consumer has stopped reading entirely — and then losing
    // the oldest "please re-query" hints is harmless.
    private const int SubscriberQueueCapacity = 64;

    private readonly ConcurrentDictionary<Guid, Channel<AdminEventTopic>> _subscribers = new();

    public void Publish(AdminEventTopic topic)
    {
        foreach (var channel in _subscribers.Values)
        {
            channel.Writer.TryWrite(topic);
        }
    }

    public IAdminEventSubscription Subscribe()
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateBounded<AdminEventTopic>(new BoundedChannelOptions(SubscriberQueueCapacity)
        {
            SingleReader = true,
            FullMode = BoundedChannelFullMode.DropOldest
        });

        _subscribers[id] = channel;
        return new Subscription(this, id, channel);
    }

    private void Unsubscribe(Guid id, Channel<AdminEventTopic> channel)
    {
        _subscribers.TryRemove(id, out _);
        channel.Writer.TryComplete();
    }

    private sealed class Subscription : IAdminEventSubscription
    {
        private readonly AdminEventBroadcaster _owner;
        private readonly Guid _id;
        private readonly Channel<AdminEventTopic> _channel;
        private int _disposed;

        public Subscription(AdminEventBroadcaster owner, Guid id, Channel<AdminEventTopic> channel)
        {
            _owner = owner;
            _id = id;
            _channel = channel;
        }

        public ChannelReader<AdminEventTopic> Reader => _channel.Reader;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _owner.Unsubscribe(_id, _channel);
            }
        }
    }
}
