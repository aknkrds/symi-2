using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Symi.Api.Data;
using Symi.Api.DTOs;
using System.Security.Claims;

namespace Symi.Api.Controllers;

[ApiController]
[Route("me")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<ProfileController> _logger;

    public ProfileController(AppDbContext db, ILogger<ProfileController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<MeResponse>> Get()
    {
        var sub = User.Claims.FirstOrDefault(c => c.Type == System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub || c.Type == ClaimTypes.NameIdentifier)?.Value;
        Models.User? user = null;
        if (!string.IsNullOrEmpty(sub) && Guid.TryParse(sub, out var userId))
        {
            user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        }
        else
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(username)) return Unauthorized();
            user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
        }
        if (user == null) return Unauthorized();
        return new MeResponse(user.Id, user.Email, user.Username, user.FullName, user.BirthDate, user.AvatarUrl, user.Role, user.Status);
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateProfileRequest req)
    {
        var sub = User.Claims.FirstOrDefault(c => c.Type == System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub || c.Type == ClaimTypes.NameIdentifier)?.Value;
        Models.User? user = null;
        if (!string.IsNullOrEmpty(sub) && Guid.TryParse(sub, out var userId))
        {
            user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        }
        else
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(username)) return Unauthorized();
            user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
        }
        if (user == null) return Unauthorized();

        if (!string.IsNullOrWhiteSpace(req.Username) && req.Username != user.Username)
        {
            var exists = await _db.Users.AnyAsync(u => u.Username == req.Username);
            if (exists)
                return Conflict(new ErrorResponse("username_taken", "Username already in use"));
            user.Username = req.Username!;
        }

        user.FullName = req.FullName ?? user.FullName;
        user.BirthDate = req.BirthDate ?? user.BirthDate;
        user.AvatarUrl = req.AvatarUrl ?? user.AvatarUrl;
        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        _db.AuditLogs.Add(new Models.AuditLog { UserId = user.Id, Action = "profile.update", Data = null });
        await _db.SaveChangesAsync();

        return NoContent();
    }
}