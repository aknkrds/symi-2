using System.ComponentModel.DataAnnotations;

namespace Symi.Api.DTOs;

public record CheckoutItem(
    [Required] Guid TicketTypeId,
    [Range(1, int.MaxValue)] int Quantity
);

public record CheckoutRequest(
    [Required] Guid EventId,
    [Required, MinLength(1)] List<CheckoutItem> Items,
    string? Currency
);

public record CheckoutResponse(
    Guid OrderId,
    string Provider,
    string PaymentToken,
    string RedirectUrl
);

public record WebhookPayload(
    [Required] string EventType, // payment_succeeded, payment_failed
    [Required] string PaymentId,
    string? OrderId,
    decimal Amount,
    string? Currency
);