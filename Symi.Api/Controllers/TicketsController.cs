using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Symi.Api.Data;
using Symi.Api.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Symi.Api.Controllers;

[ApiController]
[Route("tickets")]
[Authorize]
public class TicketsController : ControllerBase
{
    private readonly AppDbContext _db;
    public TicketsController(AppDbContext db) { _db = db; }

    private Guid? GetUserId()
    {
        var sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(sub, out var id)) return id;
        return null;
    }

    [HttpGet("mine")]
    public async Task<IActionResult> Mine()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var orders = await _db.Orders.Where(o => o.UserId == userId && o.Status == "paid").Select(o => o.Id).ToListAsync();
        var tickets = await _db.Tickets
            .Where(t => orders.Contains(t.OrderId))
            .Select(t => new {
                t.Id,
                t.EventId,
                t.TicketTypeId,
                t.QrToken,
                t.Status,
                t.IssuedAt,
                t.UsedAt
            })
            .OrderBy(t => t.IssuedAt)
            .ToListAsync();
        return Ok(tickets);
    }
}