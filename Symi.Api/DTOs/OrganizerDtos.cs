using System.ComponentModel.DataAnnotations;

namespace Symi.Api.DTOs;

public record KycPresignRequest(
    [Required] string Type,
    [Required] string ContentType
);

public record KycPresignResponse(string Url, string Key);

public record KycApplyRequest(
    [Required, MaxLength(140)] string CompanyName,
    [Required, MaxLength(50)] string TaxNumber,
    [Required, MaxLength(34)] string Iban,
    [Required, MaxLength(120)] string AuthorizedName,
    [Required] List<KycDocInput> Documents
);

public record KycDocInput(
    [Required] string Type,
    [Required] string ObjectKey,
    string? ContentType
);

public record OrganizerMeResponse(
    string KycStatus,
    bool NeedsContractAcceptance,
    string? CurrentContractVersion,
    DateTime? VerifiedAt
);