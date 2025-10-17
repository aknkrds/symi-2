using System.ComponentModel.DataAnnotations;

namespace Symi.Api.DTOs;

public record RegisterRequest(
    [Required, EmailAddress] string Email,
    [Required, MinLength(2), MaxLength(20), RegularExpression("^[A-Za-z0-9_]{2,20}$")] string Username,
    [Required, MinLength(8)] string Password,
    string? FullName,
    DateOnly? BirthDate
);

public record LoginRequest(
    [Required] string EmailOrUsername,
    [Required] string Password
);

public record RefreshRequest(
    [Required] string RefreshToken
);

public record TokenResponse(string AccessToken, string RefreshToken);

public record ErrorResponse(string Code, string Message);