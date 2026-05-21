using Backup.Server.Api.Security;
using Backup.Server.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backup.Server.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly DashboardMetricsService _service;

    public DashboardController(DashboardMetricsService service)
    {
        _service = service;
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

        var metrics = await _service.GetMetricsAsync(periodDays);
        return Ok(metrics);
    }
}
