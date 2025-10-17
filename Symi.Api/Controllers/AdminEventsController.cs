using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Symi.Api.Data;

namespace Symi.Api.Controllers;

[ApiController]
[Route("admin/events")]
[Authorize(Policy = "Admin")]
public class AdminEventsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<AdminEventsController> _logger;

    public AdminEventsController(AppDbContext db, ILogger<AdminEventsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpPost("{id}/approve")]
    public async Task<IActionResult> Approve(Guid id)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.Id == id);
        if (ev == null) return NotFound();
        if (ev.Status != "pending") return Conflict(new { message = "Only pending events can be approved." });

        var adminIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
        Guid? adminId = Guid.TryParse(adminIdClaim, out var tmp) ? tmp : null;

        ev.Status = "approved";
        ev.ApprovedAt = DateTime.UtcNow;
        ev.ApprovedByUserId = adminId;
        ev.RejectionReason = null;
        await _db.SaveChangesAsync();
        return Ok(ev);
    }

    public record RejectRequest(string Reason);

    [HttpPost("{id}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectRequest req)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.Id == id);
        if (ev == null) return NotFound();
        if (ev.Status != "pending") return Conflict(new { message = "Only pending events can be rejected." });

        ev.Status = "rejected";
        ev.ApprovedAt = null;
        ev.ApprovedByUserId = null;
        ev.RejectionReason = req?.Reason ?? "";
        await _db.SaveChangesAsync();
        return Ok(ev);
    }
}