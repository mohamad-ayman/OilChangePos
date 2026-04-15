using Microsoft.AspNetCore.Mvc;

namespace OilChangePOS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { ok = true });
}
