using Backup.Server.Api.Security;
using Backup.Server.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backup.Server.Api.Controllers;

/// <summary>
/// Server-Sent Events stream for the admin UI. Emits a named event per
/// <see cref="AdminEventTopic"/> whenever the corresponding entity group
/// changes, so the frontend can invalidate its query cache instead of
/// interval-polling every list endpoint.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class EventsController : ControllerBase
{
    // Frequent enough to keep reverse-proxy and browser idle timers from
    // cutting the connection; cheap enough to be negligible per client.
    private static readonly TimeSpan KeepAliveInterval = TimeSpan.FromSeconds(15);

    private readonly IAdminEventBroadcaster _broadcaster;

    public EventsController(IAdminEventBroadcaster broadcaster)
    {
        _broadcaster = broadcaster;
    }

    [Authorize(Policy = AuthConstants.AdminReadPolicy)]
    [HttpGet]
    public async Task Stream(CancellationToken cancellationToken)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        // Tell nginx-style proxies not to buffer the stream.
        Response.Headers["X-Accel-Buffering"] = "no";

        using var subscription = _broadcaster.Subscribe();

        try
        {
            // Client reconnect delay after a dropped connection.
            await Response.WriteAsync("retry: 5000\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);

            var keepAlive = Task.Delay(KeepAliveInterval, cancellationToken);

            while (!cancellationToken.IsCancellationRequested)
            {
                var readable = subscription.Reader.WaitToReadAsync(cancellationToken).AsTask();
                var winner = await Task.WhenAny(readable, keepAlive);

                if (winner == keepAlive)
                {
                    await Response.WriteAsync(": keep-alive\n\n", cancellationToken);
                    await Response.Body.FlushAsync(cancellationToken);
                    keepAlive = Task.Delay(KeepAliveInterval, cancellationToken);
                    continue;
                }

                if (!await readable)
                {
                    break;
                }

                // Drain everything already queued and emit each distinct
                // topic once — a burst of writes from one backup run
                // collapses into a single invalidation per entity group.
                var topics = new HashSet<AdminEventTopic>();
                while (subscription.Reader.TryRead(out var topic))
                {
                    topics.Add(topic);
                }

                foreach (var topic in topics)
                {
                    await Response.WriteAsync($"event: {TopicName(topic)}\ndata: {{}}\n\n", cancellationToken);
                }

                await Response.Body.FlushAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected — normal SSE lifecycle, not an error.
        }
    }

    private static string TopicName(AdminEventTopic topic) => topic switch
    {
        AdminEventTopic.Jobs => "jobs",
        AdminEventTopic.Artifacts => "artifacts",
        AdminEventTopic.Restores => "restores",
        AdminEventTopic.Agents => "agents",
        AdminEventTopic.Policies => "policies",
        _ => throw new ArgumentOutOfRangeException(nameof(topic))
    };
}
