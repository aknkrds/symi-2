using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Symi.Api.Data;
using Symi.Api.Models;

namespace Symi.Api.Controllers;

[ApiController]
[Route("admin/kyc")]
[Authorize(Policy = "Admin")]
public class AdminKycController : ControllerBase
{
    private readonly AppDbContext _db;
    public AdminKycController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status = "pending")
    {
        var q = _db.OrganizerKycs.AsQueryable();
        if (!string.IsNullOrEmpty(status)) q = q.Where(k => k.Status == status);
        var items = await q.OrderBy(k => k.CreatedAt).ToListAsync();
        return Ok(items);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Detail(Guid id)
    {
        var app = await _db.OrganizerKycs.Include(a => a.Documents).FirstOrDefaultAsync(a => a.Id == id);
        if (app == null) return NotFound();
        return Ok(app);
    }

    [HttpPost("{id}/approve")]
    public async Task<IActionResult> Approve(Guid id)
    {
        var app = await _db.OrganizerKycs.FirstOrDefaultAsync(a => a.Id == id);
        if (app == null) return NotFound();
        app.Status = "approved";
        app.ReviewedAt = DateTime.UtcNow;
        var adminSub = User.Claims.FirstOrDefault(c => c.Type == System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
        if (Guid.TryParse(adminSub, out var adminId)) app.ReviewedByUserId = adminId;
        app.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // Mark user verified timestamp
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == app.UserId);
        if (user != null)
        {
            user.OrganizerVerifiedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        // Audit
        _db.AuditLogs.Add(new AuditLog { UserId = app.UserId, Action = "kyc.approve", Data = System.Text.Json.JsonSerializer.Serialize(new { applicationId = app.Id, reviewedBy = app.ReviewedByUserId }) });
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectBody body)
    {
        if (string.IsNullOrWhiteSpace(body.Reason)) return BadRequest(new { code = "reason_required", message = "Rejection reason is required" });
        var app = await _db.OrganizerKycs.FirstOrDefaultAsync(a => a.Id == id);
        if (app == null) return NotFound();
        app.Status = "rejected";
        app.DecisionReason = body.Reason;
        app.ReviewedAt = DateTime.UtcNow;
        var adminSub = User.Claims.FirstOrDefault(c => c.Type == System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
        if (Guid.TryParse(adminSub, out var adminId)) app.ReviewedByUserId = adminId;
        app.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        _db.AuditLogs.Add(new AuditLog { UserId = app.UserId, Action = "kyc.reject", Data = System.Text.Json.JsonSerializer.Serialize(new { applicationId = app.Id, reason = body.Reason, reviewedBy = app.ReviewedByUserId }) });
        await _db.SaveChangesAsync();
        return NoContent();
    }

    public record RejectBody(string Reason);
}