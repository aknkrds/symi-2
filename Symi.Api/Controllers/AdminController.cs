using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Symi.Api.Controllers;

[ApiController]
[Route("admin")]
public class AdminController : ControllerBase
{
    [HttpGet("ping")]
    [Authorize(Policy = "Admin")]
    public IActionResult Ping()
    {
        return Ok(new { message = "pong" });
    }
}