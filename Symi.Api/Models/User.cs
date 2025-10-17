using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Symi.Api.Models;

public class User
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(3), MaxLength(20)]
    [RegularExpression("^[A-Za-z0-9_]{3,20}$")]
    public string Username { get; set; } = string.Empty;

    [MaxLength(120)]
    public string? FullName { get; set; }

    public DateOnly? BirthDate { get; set; }

    public string? AvatarUrl { get; set; }

    [Required]
    public string Role { get; set; } = "user"; // user, organizer, moderator, admin

    [Required]
    public string Status { get; set; } = "active"; // active, banned, frozen

    [Required]
    public byte[] PasswordHash { get; set; } = Array.Empty<byte>();

    [Required]
    public byte[] PasswordSalt { get; set; } = Array.Empty<byte>();

    [Required]
    public string PasswordAlgorithm { get; set; } = "PBKDF2";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public DateTime? OrganizerVerifiedAt { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}