using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Symi.Api.Data;
using Symi.Api.DTOs;
using Symi.Api.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Symi.Api.Controllers;

[ApiController]
[Route("reports")]
public class ReportsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<ReportsController> _logger;

    public ReportsController(AppDbContext db, ILogger<ReportsController> logger)
    { _db = db; _logger = logger; }

    private Guid? GetUserId()
    {
        var sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(sub, out var id)) return id;
        return null;
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] CreateReportRequest req)
    {
        var userId = GetUserId(); if (userId == null) return Unauthorized();

        // Optional: Validate target existence
        if (req.TargetType == "post")
        {
            var exists = await _db.Posts.FindAsync(req.TargetId);
            if (exists == null) return NotFound(new { message = "Post not found" });
        }
        else if (req.TargetType == "comment")
        {
            var exists = await _db.Comments.FindAsync(req.TargetId);
            if (exists == null) return NotFound(new { message = "Comment not found" });
        }

        var report = new Report
        {
            UserId = userId.Value,
            TargetType = req.TargetType,
            TargetId = req.TargetId,
            Reason = req.Reason,
            Details = req.Details,
            Status = "open",
            CreatedAt = DateTime.UtcNow
        };
        _db.Reports.Add(report);
        await _db.SaveChangesAsync();
        return Created($"/reports/{report.Id}", report);
    }
}