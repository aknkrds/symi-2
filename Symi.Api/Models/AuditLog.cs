using System.ComponentModel.DataAnnotations;

namespace Symi.Api.Models;

public class AuditLog
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid UserId { get; set; }

    [Required]
    public string Action { get; set; } = string.Empty; // e.g., profile.update

    public string? Data { get; set; } // JSON snapshot of changes

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}