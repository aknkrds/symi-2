using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Symi.Api.Models;

public class Ticket
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid OrderId { get; set; }

    [ForeignKey(nameof(OrderId))]
    public Order? Order { get; set; }

    [Required]
    public Guid EventId { get; set; }

    [Required]
    public Guid TicketTypeId { get; set; }

    [ForeignKey(nameof(TicketTypeId))]
    public TicketType? TicketType { get; set; }

    [Required]
    [MaxLength(64)]
    public string QrToken { get; set; } = Guid.NewGuid().ToString("N");

    [MaxLength(20)]
    public string Status { get; set; } = "active"; // active, used, refunded

    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UsedAt { get; set; }
}