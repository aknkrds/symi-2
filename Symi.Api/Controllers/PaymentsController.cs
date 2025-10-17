using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Symi.Api.Data;
using Symi.Api.DTOs;
using Symi.Api.Models;

namespace Symi.Api.Controllers;

[ApiController]
[Route("payments")] 
public class PaymentsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(AppDbContext db, IConfiguration config, ILogger<PaymentsController> logger)
    {
        _db = db;
        _config = config;
        _logger = logger;
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook([FromBody] WebhookPayload payload)
    {
        var signatureHeader = Request.Headers["X-Signature"].ToString();
        var idempotencyKey = Request.Headers["X-Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(signatureHeader) || string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Unauthorized(new { message = "Missing signature or idempotency key" });
        }

        var secret = _config["Payments:WebhookSecret"] ?? "sandbox-secret";
        var computed = ComputeHmacSha256(secret, System.Text.Json.JsonSerializer.Serialize(payload));
        if (!string.Equals(signatureHeader, computed, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Invalid webhook signature");
            return Unauthorized(new { message = "Invalid signature" });
        }

        // Idempotency: short-circuit if seen
        var exists = await _db.WebhookEvents.AnyAsync(w => w.IdempotencyKey == idempotencyKey);
        if (exists)
        {
            _logger.LogInformation("Webhook replay ignored: {Key}", idempotencyKey);
            return Ok(new { status = "ignored" });
        }
        _db.WebhookEvents.Add(new WebhookEvent
        {
            IdempotencyKey = idempotencyKey,
            Provider = "sandbox",
            ProviderEventId = payload.PaymentId,
            RawPayload = System.Text.Json.JsonSerializer.Serialize(payload),
            ReceivedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        // Find payment and order
        var payment = await _db.Payments.Include(p => p.Order).FirstOrDefaultAsync(p => p.ProviderPaymentId == payload.PaymentId);
        if (payment == null)
        {
            _logger.LogWarning("Payment not found for provider id {Pid}", payload.PaymentId);
            return NotFound(new { message = "Payment not found" });
        }

        if (payload.EventType == "payment_succeeded")
        {
            payment.Status = "succeeded";
            payment.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            // Order status -> paid and trigger ticket generation (idempotent)
            var order = payment.Order!;
            if (order.Status != "paid")
            {
                order.Status = "paid";
                order.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }

            await GenerateTicketsForOrder(order.Id);
            return Ok(new { status = "processed" });
        }
        else if (payload.EventType == "payment_failed")
        {
            payment.Status = "failed";
            payment.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return Ok(new { status = "processed" });
        }
        else
        {
            _logger.LogInformation("Unhandled event type {Type}", payload.EventType);
            return Ok(new { status = "ignored" });
        }
    }

    private async Task GenerateTicketsForOrder(Guid orderId)
    {
        var order = await _db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == orderId);
        if (order == null) return;
        // Idempotency: if tickets already exist for order, skip
        var existingTickets = await _db.Tickets.CountAsync(t => t.OrderId == orderId);
        if (existingTickets > 0) return;

        foreach (var item in order.Items)
        {
            for (int i = 0; i < item.Quantity; i++)
            {
                var ticket = new Ticket
                {
                    OrderId = order.Id,
                    EventId = order.EventId,
                    TicketTypeId = item.TicketTypeId,
                    QrToken = Guid.NewGuid().ToString("N"),
                    Status = "active",
                    IssuedAt = DateTime.UtcNow
                };
                _db.Tickets.Add(ticket);
            }
        }
        await _db.SaveChangesAsync();
    }

    private static string ComputeHmacSha256(string secret, string payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}