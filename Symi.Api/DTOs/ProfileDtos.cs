using System.ComponentModel.DataAnnotations;

namespace Symi.Api.DTOs;

public record MeResponse(Guid Id, string Email, string Username, string? FullName, DateOnly? BirthDate, string? AvatarUrl, string Role, string Status);

public class UpdateProfileRequest
{
    [MinLength(2), MaxLength(20)]
    [RegularExpression("^[A-Za-z0-9_]{2,20}$")] 
    public string? Username { get; set; }

    [MaxLength(120)]
    public string? FullName { get; set; }

    public DateOnly? BirthDate { get; set; }

    [Url]
    public string? AvatarUrl { get; set; }
}

public record PresignRequest([Required] string ContentType, [Required] string Key);

public record PresignResponse(string Url, DateTime ExpiresAt);