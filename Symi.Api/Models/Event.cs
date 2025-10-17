using System.ComponentModel.DataAnnotations;

namespace Symi.Api.Models;

public class Event
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid OrganizerUserId { get; set; }

    [Required]
    [MaxLength(120)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Required]
    [MaxLength(40)]
    public string Category { get; set; } = string.Empty;

    [Required]
    [MaxLength(80)]
    public string City { get; set; } = string.Empty;

    [Required]
    [MaxLength(120)]
    public string Venue { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? AddressLine { get; set; }

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public string? CoverUrl { get; set; }

    // draft → pending → approved/rejected → published
    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "draft";

    public DateTime? ApprovedAt { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public string? RejectionReason { get; set; }

    public DateTime? PublishedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<EventSession> Sessions { get; set; } = new List<EventSession>();
    public ICollection<TicketType> TicketTypes { get; set; } = new List<TicketType>();
}