using Backup.Server.Application.Interfaces;

namespace Backup.Server.Application.Services;

/// <summary>
/// Detects online↔offline transitions for every agent and fires
/// notifications when state changes. The first sweep after a fresh
/// startup just records the current state in <see cref="Domain.Entities.Agent.LastNotifiedOnline"/>
/// without firing — that's why an empty LastNotifiedOnline is treated
/// as "no notification baseline yet".
/// </summary>
public class AgentHealthService
{
    // 3 minutes matches the "stale → offline" boundary in AgentService.
    // Using the stricter offline cutoff (instead of OnlineThreshold)
    // avoids false-positive notifications during a brief heartbeat gap.
    private static readonly TimeSpan OfflineAfter = TimeSpan.FromMinutes(3);

    private readonly IAgentRepository _agentRepository;
    private readonly INotificationService _notificationService;
    private readonly IAdminEventBroadcaster _eventBroadcaster;

    public AgentHealthService(
        IAgentRepository agentRepository,
        INotificationService notificationService,
        IAdminEventBroadcaster eventBroadcaster)
    {
        _agentRepository = agentRepository;
        _notificationService = notificationService;
        _eventBroadcaster = eventBroadcaster;
    }

    public async Task<int> SweepAsync(CancellationToken cancellationToken = default)
    {
        var agents = await _agentRepository.GetAllAgentsAsync();
        var now = DateTime.UtcNow;
        var transitions = 0;

        foreach (var agent in agents)
        {
            var currentlyOnline = agent.LastSeenAt.HasValue
                && (now - agent.LastSeenAt.Value) < OfflineAfter;

            // Baseline pass — establish the starting state without
            // surprising the operator with a fan-out on first boot.
            if (agent.LastNotifiedOnline is null)
            {
                agent.LastNotifiedOnline = currentlyOnline;
                await _agentRepository.UpdateAgent(agent);
                continue;
            }

            if (agent.LastNotifiedOnline == currentlyOnline)
            {
                continue;
            }

            if (currentlyOnline)
            {
                await _notificationService.NotifyAgentBackOnlineAsync(agent.Id, agent.Name, cancellationToken);
            }
            else
            {
                await _notificationService.NotifyAgentOfflineAsync(agent.Id, agent.Name, agent.LastSeenAt, cancellationToken);
            }

            agent.LastNotifiedOnline = currentlyOnline;
            await _agentRepository.UpdateAgent(agent);
            transitions++;
        }

        await _agentRepository.SaveChangesAsync();

        if (transitions > 0)
        {
            _eventBroadcaster.Publish(AdminEventTopic.Agents);
        }

        return transitions;
    }
}
