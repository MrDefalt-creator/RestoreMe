namespace Backup.Agent.Worker.Services;

public static class RestoreChecksumGate
{
    // An empty/absent expected checksum means a legacy artifact uploaded before
    // checksums were recorded — proceed (backward compatible). Otherwise the
    // computed hash must match (case-insensitive).
    public static bool ShouldProceed(string? expected, string? computed)
    {
        if (string.IsNullOrWhiteSpace(expected))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(computed)
            && string.Equals(expected, computed, StringComparison.OrdinalIgnoreCase);
    }
}
