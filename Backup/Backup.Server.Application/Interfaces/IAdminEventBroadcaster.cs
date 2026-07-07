using System.Threading.Channels;

namespace Backup.Server.Application.Interfaces;

/// <summary>
/// Entity groups the admin UI watches. An event carries no payload — it
/// only says "something in this group changed, re-query it". Keeping the
/// signal coarse avoids duplicating DTO mapping in the event path and
/// makes dropped/coalesced events harmless.
/// </summary>
public enum AdminEventTopic
{
    Jobs,
    Artifacts,
    Restores,
    Agents,
    Policies
}

/// <summary>
/// In-process fan-out from mutating services to connected admin event
/// streams (SSE). Publishing must never block or fail the mutation that
/// triggered it — delivery is best-effort, like notifications.
/// </summary>
public interface IAdminEventBroadcaster
{
    void Publish(AdminEventTopic topic);

    IAdminEventSubscription Subscribe();
}

/// <summary>
/// One consumer's view of the event stream. Dispose to unsubscribe;
/// the reader completes when the subscription is disposed.
/// </summary>
public interface IAdminEventSubscription : IDisposable
{
    ChannelReader<AdminEventTopic> Reader { get; }
}
