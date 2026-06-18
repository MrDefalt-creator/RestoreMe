using Backup.Server.Application.Interfaces;
using Backup.Server.Domain.Entities;
using Backup.Shared.Contracts.DTOs.Integrity;

namespace Backup.Server.Application.Services;

public class IntegritySettingsService
{
    private readonly IIntegrityScrubSettingsRepository _repo;
    private readonly IAuditLogRepository _auditLogRepository;

    public IntegritySettingsService(
        IIntegrityScrubSettingsRepository repo,
        IAuditLogRepository auditLogRepository)
    {
        _repo = repo;
        _auditLogRepository = auditLogRepository;
    }

    public async Task<IntegrityScrubSettingsDto> GetAsync(CancellationToken cancellationToken)
    {
        var settings = await _repo.GetOrCreateAsync(cancellationToken);
        return Map(settings);
    }

    public async Task<IntegrityScrubSettingsDto> UpdateAsync(
        UpdateIntegrityScrubSettingsRequest request, Guid? actorId, CancellationToken cancellationToken)
    {
        var settings = await _repo.GetOrCreateAsync(cancellationToken);
        settings.IsEnabled = request.IsEnabled;
        settings.IntervalDays = Math.Max(1, request.IntervalDays);
        settings.RunAtMinutesUtc = Math.Clamp(request.RunAtMinutesUtc, 0, 24 * 60 - 1);
        settings.BatchSize = Math.Max(1, request.BatchSize);
        settings.NextRunAt = IntegrityScheduleCalculator.ComputeNextRun(
            DateTime.UtcNow, settings.IntervalDays, settings.RunAtMinutesUtc);

        await _repo.UpdateAsync(settings, cancellationToken);

        await _auditLogRepository.AddAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            ActorId = actorId,
            Action = "integrity.settings_updated",
            TargetId = null,
            Details = $"enabled={settings.IsEnabled} intervalDays={settings.IntervalDays} runAtMinutesUtc={settings.RunAtMinutesUtc} batchSize={settings.BatchSize}",
            OccurredAt = DateTime.UtcNow,
        });
        await _auditLogRepository.SaveChangesAsync();

        return Map(settings);
    }

    private static IntegrityScrubSettingsDto Map(IntegrityScrubSettings s) =>
        new(s.IsEnabled, s.IntervalDays, s.RunAtMinutesUtc, s.BatchSize, s.LastRunAt, s.NextRunAt);
}
