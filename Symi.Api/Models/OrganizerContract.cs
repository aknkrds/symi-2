using System.ComponentModel.DataAnnotations;

namespace Symi.Api.Models;

public class OrganizerContract
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(20)]
    public string Version { get; set; } = "v1";

    [Required]
    public string Body { get; set; } = string.Empty;

    public bool IsCurrent { get; set; } = true;

    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;
}

public class OrganizerContractAcceptance
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid UserId { get; set; }

    [Required]
    public Guid ContractId { get; set; }

    public DateTime AcceptedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(64)]
    public string? Ip { get; set; }

    [MaxLength(256)]
    public string? UserAgent { get; set; }
}