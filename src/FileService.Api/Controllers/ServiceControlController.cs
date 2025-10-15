using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

[ApiController]
[Route("api/control")]
public class ServiceControlController : ControllerBase
{
    [HttpGet("status")]
    public IActionResult Status()
    {
        return Ok(new { status = "running" });
    }

    [HttpPost("stop")]
    public IActionResult Stop()
    {
        return Ok(new { message = "Service stop requested (stub)." });
    }

    [HttpPost("start")]
    public IActionResult Start()
    {
        return Ok(new { message = "Service start requested (stub)." });
    }
}
