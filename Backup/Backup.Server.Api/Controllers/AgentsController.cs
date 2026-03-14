using Microsoft.AspNetCore.Mvc;

namespace Backup.Server.Api.Controllers;


[ApiController]
[Route("api/[controller]")]
public class AgentsController : ControllerBase
{

    [HttpPost("register")]
    public async Task<IActionResult> Register()
    {
        
        return Ok();
    }
    
}