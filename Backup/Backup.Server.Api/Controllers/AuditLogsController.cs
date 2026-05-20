using Backup.Server.Api.Security;
using Backup.Server.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backup.Server.Api.Controllers;

[ApiController]
[Route("api/audit-logs")]
[Authorize(Policy = AuthConstants.UserManagementPolicy)]
public class AuditLogsController : ControllerBase
{
    private readonly AuditLogService _service;

    public AuditLogsController(AuditLogService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Query(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? action,
        [FromQuery] Guid? actorId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.QueryAsync(from, to, action, actorId, page, pageSize, cancellationToken);
        return Ok(result);
    }
}
