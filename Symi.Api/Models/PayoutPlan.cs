using System.ComponentModel.DataAnnotations;

namespace Symi.Api.Models;

public class PayoutPlan
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid EventId { get; set; }

    [MaxLength(20)]
    public string Status { get; set; } = "pending"; // pending, processed

    public DateTime ScheduledAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }

    public decimal? GrossAmount { get; set; }
    public decimal? CommissionAmount { get; set; }
    public decimal? VatAmount { get; set; }
    public decimal? NetAmount { get; set; }
}