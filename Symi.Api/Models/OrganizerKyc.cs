using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Symi.Api.Models;

public class OrganizerKycApplication
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid UserId { get; set; }

    [Required]
    [MaxLength(140)]
    public string CompanyName { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string TaxNumber { get; set; } = string.Empty;

    [Required]
    [MaxLength(34)]
    public string Iban { get; set; } = string.Empty;

    [Required]
    [MaxLength(120)]
    public string AuthorizedName { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "pending"; // pending, approved, rejected

    public string? DecisionReason { get; set; } // required if rejected

    public Guid? ReviewedByUserId { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<KycDocument> Documents { get; set; } = new List<KycDocument>();
}

public class KycDocument
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid ApplicationId { get; set; }

    [ForeignKey(nameof(ApplicationId))]
    public OrganizerKycApplication? Application { get; set; }

    [Required]
    [MaxLength(50)]
    public string Type { get; set; } = string.Empty; // identity, company_doc

    [Required]
    [MaxLength(256)]
    public string ObjectKey { get; set; } = string.Empty; // s3 object key

    [MaxLength(120)]
    public string? ContentType { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}