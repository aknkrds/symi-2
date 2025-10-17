using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Symi.Api.Models;

public class TicketType
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid EventId { get; set; }

    [ForeignKey(nameof(EventId))]
    public Event? Event { get; set; }

    [Required]
    [MaxLength(60)]
    public string Name { get; set; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal Price { get; set; }

    [Range(0, int.MaxValue)]
    public int Capacity { get; set; }

    [Range(1, int.MaxValue)]
    public int PerPersonLimit { get; set; } = 4;

    [Required]
    public DateTime SalesStart { get; set; }

    [Required]
    public DateTime SalesEnd { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}