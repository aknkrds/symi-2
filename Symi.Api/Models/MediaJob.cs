using System.ComponentModel.DataAnnotations;

namespace Symi.Api.Models;

public class MediaJob
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(256)]
    public string ObjectKey { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Type { get; set; } = "image"; // image, video

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "pending"; // pending, processing, completed, failed

    public string? ThumbnailUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}