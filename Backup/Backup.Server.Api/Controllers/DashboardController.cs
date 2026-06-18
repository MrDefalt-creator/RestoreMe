using Backup.Server.Api.Security;
using Backup.Server.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backup.Server.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly DashboardMetricsService _metrics;
    private readonly DashboardSummaryService _summary;

    public DashboardController(DashboardMetricsService metrics, DashboardSummaryService summary)
    {
        _metrics = metrics;
        _summary = summary;
    }

    /// <summary>
    /// Aggregated dashboard payload — success-rate per day, cumulative
    /// storage growth, top failing policies, and engine breakdown. The
    /// frontend hands every collection straight to recharts.
    /// </summary>
    [Authorize(Policy = AuthConstants.AdminReadPolicy)]
    [HttpGet("metrics")]
    public async Task<IActionResult> Metrics([FromQuery] string period = "30d")
    {
        // Whitelisted period values keep the surface area predictable and
        // play well with the segmented selector in the UI. Unknown values
        // fall back to 30 days rather than 400.
        var periodDays = period switch
        {
            "7d" => 7,
            "30d" => 30,
            "90d" => 90,
            _ => 30,
        };

        var metrics = await _metrics.GetMetricsAsync(periodDays);
        return Ok(metrics);
    }

    /// <summary>
    /// Instant dashboard snapshot — agent / policy / job / artifact counts,
    /// the 7-day job-volume strip, the unresolved-failure list and the
    /// latest-activity preview. Replaces the previous client-side pattern
    /// of fetching every list endpoint just to count things.
    /// </summary>
    [Authorize(Policy = AuthConstants.AdminReadPolicy)]
    [HttpGet("summary")]
    public async Task<IActionResult> Summary()
    {
        var summary = await _summary.GetSummaryAsync();
        return Ok(summary);
    }
}
