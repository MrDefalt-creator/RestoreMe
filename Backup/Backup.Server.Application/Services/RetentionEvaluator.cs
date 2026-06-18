using Backup.Server.Domain.Entities;

namespace Backup.Server.Application.Services;

/// <summary>
/// Why an artifact was selected for retention pruning. Surfaced in the audit log.
/// </summary>
public enum RetentionReason
{
    Age,
    Count,
    Size,
}

public sealed record RetentionDeletion(BackupArtifact Artifact, RetentionReason Reason);

/// <summary>
/// Pure, DB-free retention decision logic. Given the artifacts of the system,
/// returns which ones should be pruned per their policy's retention rules.
///
/// Semantics (per policy, newest-first by CreatedAt):
///   - The newest artifact is never deleted (a policy always keeps at least one copy).
///   - Keep-union (days OR count): when RetentionDays and/or RetentionMaxCount are set,
///     an artifact is protected if it is within RetentionDays OR among the newest
///     RetentionMaxCount. Unprotected artifacts are pruned (reason Age/Count).
///   - Size cap (hard): among the survivors, walking newest-first, any artifact whose
///     cumulative size exceeds RetentionMaxTotalBytes is pruned oldest-first (reason Size),
///     except the newest (floor).
/// A policy with no retention rule configured prunes nothing.
/// </summary>
public static class RetentionEvaluator
{
    public static List<RetentionDeletion> SelectForDeletion(
        IEnumerable<BackupArtifact> artifacts,
        DateTime utcNow)
    {
        var deletions = new List<RetentionDeletion>();

        var byPolicy = artifacts
            .Where(a => a.Job?.Policy != null)
            .GroupBy(a => a.Job!.Policy!.Id);

        foreach (var group in byPolicy)
        {
            var policy = group.First().Job!.Policy!;
            if (policy.RetentionDays is null
                && policy.RetentionMaxCount is null
                && policy.RetentionMaxTotalBytes is null)
            {
                continue;
            }

            // Newest first; index 0 is the protected floor.
            var ordered = group.OrderByDescending(a => a.CreatedAt).ToList();
            var deletedIds = new HashSet<Guid>();

            var hasKeepRule = policy.RetentionDays is not null || policy.RetentionMaxCount is not null;
            if (hasKeepRule)
            {
                for (var rank = 0; rank < ordered.Count; rank++)
                {
                    if (rank == 0)
                    {
                        continue; // floor: always keep newest
                    }

                    var artifact = ordered[rank];
                    var withinDays = policy.RetentionDays is not null
                        && artifact.CreatedAt >= utcNow.AddDays(-policy.RetentionDays.Value);
                    var withinCount = policy.RetentionMaxCount is not null
                        && rank < policy.RetentionMaxCount.Value;

                    if (withinDays || withinCount)
                    {
                        continue; // protected by a keep rule
                    }

                    // Reason: if a count rule pushed it out, attribute to Count, else Age.
                    var reason = policy.RetentionMaxCount is not null && rank >= policy.RetentionMaxCount.Value
                        ? RetentionReason.Count
                        : RetentionReason.Age;
                    deletions.Add(new RetentionDeletion(artifact, reason));
                    deletedIds.Add(artifact.Id);
                }
            }

            if (policy.RetentionMaxTotalBytes is not null)
            {
                long cumulative = 0;
                for (var rank = 0; rank < ordered.Count; rank++)
                {
                    var artifact = ordered[rank];
                    if (deletedIds.Contains(artifact.Id))
                    {
                        continue; // already pruned by a keep rule
                    }

                    cumulative += artifact.SizeBytes;
                    if (rank == 0)
                    {
                        continue; // floor: never prune newest for size
                    }

                    if (cumulative > policy.RetentionMaxTotalBytes.Value)
                    {
                        deletions.Add(new RetentionDeletion(artifact, RetentionReason.Size));
                        deletedIds.Add(artifact.Id);
                    }
                }
            }
        }

        return deletions;
    }
}
