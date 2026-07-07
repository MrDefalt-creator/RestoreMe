using Backup.Server.Application.Interfaces;
using Backup.Server.Application.Services;

namespace Backup.Server.Tests.Events;

/// <summary>
/// Unit tests for the SSE fan-out primitive. The contract that matters:
/// publishing never blocks or throws regardless of subscriber state, every
/// live subscriber sees the topic, and disposing a subscription completes
/// its reader so the SSE loop can exit.
/// </summary>
public sealed class AdminEventBroadcasterTests
{
    [Fact]
    public async Task Publish_ReachesEverySubscriber()
    {
        var broadcaster = new AdminEventBroadcaster();
        using var first = broadcaster.Subscribe();
        using var second = broadcaster.Subscribe();

        broadcaster.Publish(AdminEventTopic.Jobs);

        Assert.Equal(AdminEventTopic.Jobs, await first.Reader.ReadAsync(TestToken()));
        Assert.Equal(AdminEventTopic.Jobs, await second.Reader.ReadAsync(TestToken()));
    }

    [Fact]
    public void Publish_WithoutSubscribers_DoesNotThrow()
    {
        var broadcaster = new AdminEventBroadcaster();

        broadcaster.Publish(AdminEventTopic.Agents);
    }

    [Fact]
    public async Task DisposedSubscription_CompletesReader_AndStopsReceiving()
    {
        var broadcaster = new AdminEventBroadcaster();
        var subscription = broadcaster.Subscribe();

        subscription.Dispose();
        broadcaster.Publish(AdminEventTopic.Restores);

        Assert.False(await subscription.Reader.WaitToReadAsync(TestToken()));
    }

    [Fact]
    public void Publish_ToStalledSubscriber_NeverBlocks()
    {
        var broadcaster = new AdminEventBroadcaster();
        using var stalled = broadcaster.Subscribe();

        // Far beyond the per-subscriber queue capacity: a consumer that
        // stopped reading must cost the publisher nothing.
        for (var i = 0; i < 10_000; i++)
        {
            broadcaster.Publish(AdminEventTopic.Artifacts);
        }
    }

    [Fact]
    public void StalledSubscriber_DropsOldest_ButKeepsLatest()
    {
        var broadcaster = new AdminEventBroadcaster();
        using var subscription = broadcaster.Subscribe();

        for (var i = 0; i < 1_000; i++)
        {
            broadcaster.Publish(AdminEventTopic.Jobs);
        }
        broadcaster.Publish(AdminEventTopic.Policies);

        // Drain whatever survived the overflow: the newest publish must be
        // there — "you may lose old hints, never the most recent one".
        var seen = new List<AdminEventTopic>();
        while (subscription.Reader.TryRead(out var topic))
        {
            seen.Add(topic);
        }

        Assert.Contains(AdminEventTopic.Policies, seen);
        Assert.Equal(AdminEventTopic.Policies, seen[^1]);
    }

    [Fact]
    public async Task Subscribers_AreIndependent_SlowOneDoesNotAffectFastOne()
    {
        var broadcaster = new AdminEventBroadcaster();
        using var slow = broadcaster.Subscribe();
        using var fast = broadcaster.Subscribe();

        for (var i = 0; i < 500; i++)
        {
            broadcaster.Publish(AdminEventTopic.Jobs);
        }

        // The fast consumer still sees events even though its sibling's
        // queue overflowed long ago.
        Assert.True(await fast.Reader.WaitToReadAsync(TestToken()));
    }

    private static CancellationToken TestToken()
        => new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token;
}
