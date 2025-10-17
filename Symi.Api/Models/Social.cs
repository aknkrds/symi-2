using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Symi.Api.Models;

public class Post
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid UserId { get; set; }

    // Optional: post by organizer account
    public Guid? OrganizerUserId { get; set; }

    // Optional: associate with an event
    public Guid? EventId { get; set; }

    [MaxLength(1000)]
    public string? Text { get; set; }

    [MaxLength(256)]
    public string? MediaUrl { get; set; }

    [MaxLength(20)]
    public string? MediaType { get; set; } // image | video

    public int? DurationSec { get; set; } // for video

    [MaxLength(20)]
    public string Status { get; set; } = "active"; // active, deleted

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public class PostReaction
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid PostId { get; set; }

    [ForeignKey(nameof(PostId))]
    public Post? Post { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [MaxLength(20)]
    public string Type { get; set; } = "like"; // only like for MVP

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Comment
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid PostId { get; set; }

    [ForeignKey(nameof(PostId))]
    public Post? Post { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [Required]
    [MaxLength(500)]
    public string Text { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Status { get; set; } = "active"; // active, deleted

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Follow
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid UserId { get; set; }

    [Required]
    public Guid OrganizerUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Report
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid UserId { get; set; }

    [Required]
    [MaxLength(20)]
    public string TargetType { get; set; } = "post"; // post | comment

    [Required]
    public Guid TargetId { get; set; }

    [MaxLength(60)]
    public string Reason { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Details { get; set; }

    [MaxLength(20)]
    public string Status { get; set; } = "open"; // open, resolved

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}