using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Symi.Api.Models;

public class RefreshToken
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid UserId { get; set; }

    public User? User { get; set; }

    [Required]
    public string TokenHash { get; set; } = string.Empty; // store hashed token

    public DateTime ExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public Guid? ReplacedByTokenId { get; set; }

    public bool Used { get; set; } = false; // single-use

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}