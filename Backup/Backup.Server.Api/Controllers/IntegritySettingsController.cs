using Backup.Server.Api.Security;
using Backup.Server.Application.Services;
using Backup.Shared.Contracts.DTOs.Integrity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backup.Server.Api.Controllers;

[ApiController]
[Route("api/integrity-settings")]
[Authorize(Policy = AuthConstants.AdminReadPolicy)]
public class IntegritySettingsController : ControllerBase
{
    private readonly IntegritySettingsService _service;

    public IntegritySettingsController(IntegritySettingsService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
        => Ok(await _service.GetAsync(cancellationToken));

    [HttpPut]
    [Authorize(Policy = AuthConstants.AdminWritePolicy)]
    public async Task<IActionResult> Update([FromBody] UpdateIntegrityScrubSettingsRequest request, CancellationToken cancellationToken)
    {
        var actorId = User.TryGetUserId();
        return Ok(await _service.UpdateAsync(request, actorId, cancellationToken));
    }
}
