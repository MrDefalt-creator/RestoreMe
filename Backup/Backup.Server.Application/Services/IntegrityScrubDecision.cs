namespace Backup.Server.Application.Services;

public enum ScrubOutcome
{
    Skipped,
    Verified,
    Failed,
}

/// <summary>
/// Pure decision for a single artifact scrub: over the size cap -> Skipped;
/// computed SHA256 equals the expected (case-insensitive) -> Verified; else Failed.
/// </summary>
public static class IntegrityScrubDecision
{
    public static ScrubOutcome Evaluate(
        long sizeBytes,
        long? maxBytes,
        string expectedChecksum,
        string? computedChecksum)
    {
        if (maxBytes is not null && sizeBytes > maxBytes.Value)
        {
            return ScrubOutcome.Skipped;
        }

        if (!string.IsNullOrWhiteSpace(computedChecksum)
            && string.Equals(computedChecksum, expectedChecksum, StringComparison.OrdinalIgnoreCase))
        {
            return ScrubOutcome.Verified;
        }

        return ScrubOutcome.Failed;
    }
}
