namespace Backup.Server.Application.Interfaces;

// Shared paged-query shape for admin list endpoints (jobs / artifacts /
// agents). SortBy is a per-repository whitelist key; unknown values fall
// back to the repository's default ordering rather than erroring, so a
// stale frontend can never break the list view.
public sealed record PagedQuery(
    int Page,
    int PageSize,
    string? SortBy,
    bool SortDescending)
{
    // Clamps raw query-string input the same way AuditLogService does:
    // page >= 1, pageSize 1..200 (default 50), any sortDir other than
    // "asc" means descending.
    public static PagedQuery Normalize(int? page, int? pageSize, string? sortBy, string? sortDir)
    {
        var normalizedPage = page is null or < 1 ? 1 : page.Value;
        var normalizedPageSize = pageSize switch
        {
            null or < 1 => 50,
            > 200 => 200,
            _ => pageSize.Value,
        };
        var descending = !string.Equals(sortDir, "asc", StringComparison.OrdinalIgnoreCase);
        return new PagedQuery(normalizedPage, normalizedPageSize, sortBy, descending);
    }
}

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Total);
