using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Symi.Api.Models;

public class Payment
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid OrderId { get; set; }

    [ForeignKey(nameof(OrderId))]
    public Order? Order { get; set; }

    [MaxLength(40)]
    public string Provider { get; set; } = "sandbox";

    [MaxLength(100)]
    public string? ProviderPaymentId { get; set; }

    [MaxLength(20)]
    public string Status { get; set; } = "pending"; // pending, succeeded, failed, canceled

    [MaxLength(64)]
    public string? IdempotencyKey { get; set; }

    [MaxLength(64)]
    public string? SignatureHash { get; set; }

    public string? RawPayload { get; set; }

    [Range(0, double.MaxValue)]
    public decimal Amount { get; set; }

    [MaxLength(10)]
    public string Currency { get; set; } = "TRY";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public class WebhookEvent
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(64)]
    public string IdempotencyKey { get; set; } = string.Empty;

    [MaxLength(40)]
    public string Provider { get; set; } = "sandbox";

    [MaxLength(100)]
    public string? ProviderEventId { get; set; }

    public string? RawPayload { get; set; }

    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
}