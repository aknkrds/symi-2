using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Symi.Api.Data;
using Symi.Api.DTOs;
using Symi.Api.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Symi.Api.Controllers;

[ApiController]
[Route("checkout")]
public class CheckoutController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public CheckoutController(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    private Guid? GetUserId()
    {
        var sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(sub, out var id)) return id;
        return null;
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] CheckoutRequest req)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var eventExists = await _db.Events.AnyAsync(e => e.Id == req.EventId);
        if (!eventExists) return BadRequest(new { message = "Event not found" });
        if (req.Items == null || req.Items.Count == 0) return BadRequest(new { message = "No items" });

        // Validate ticket types belong to event and capacity
        decimal total = 0m;
        var items = new List<OrderItem>();
        foreach (var item in req.Items)
        {
            var tt = await _db.TicketTypes.FirstOrDefaultAsync(t => t.Id == item.TicketTypeId && t.EventId == req.EventId);
            if (tt == null) return BadRequest(new { message = "Invalid ticket type" });
            if (item.Quantity <= 0) return BadRequest(new { message = "Quantity must be positive" });
            // Capacity check: sold count + requested <= capacity
            var soldCount = await _db.Tickets.CountAsync(t => t.TicketTypeId == tt.Id);
            if (soldCount + item.Quantity > tt.Capacity) return Conflict(new { message = "Not enough capacity" });
            items.Add(new OrderItem { TicketTypeId = tt.Id, Quantity = item.Quantity, UnitPrice = tt.Price });
            total += tt.Price * item.Quantity;
        }

        var order = new Order
        {
            UserId = userId.Value,
            EventId = req.EventId,
            Currency = string.IsNullOrWhiteSpace(req.Currency) ? "TRY" : req.Currency!,
            TotalAmount = total,
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        foreach (var it in items)
        {
            it.OrderId = order.Id;
            _db.OrderItems.Add(it);
        }
        await _db.SaveChangesAsync();

        // Create pending payment
        var token = Guid.NewGuid().ToString("N");
        var payment = new Payment
        {
            OrderId = order.Id,
            Provider = "sandbox",
            ProviderPaymentId = token,
            Status = "pending",
            Amount = total,
            Currency = order.Currency,
            CreatedAt = DateTime.UtcNow
        };
        _db.Payments.Add(payment);
        await _db.SaveChangesAsync();

        var redirectBase = _config["Payments:SandboxRedirectBase"] ?? "https://sandbox.payments.local/redirect";
        var redirectUrl = $"{redirectBase}?token={Uri.EscapeDataString(token)}&order={order.Id}";

        return Ok(new CheckoutResponse(order.Id, payment.Provider, token, redirectUrl));
    }
}