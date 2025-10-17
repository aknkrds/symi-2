using System.ComponentModel.DataAnnotations;

namespace Symi.Api.DTOs;

public record CreateEventRequest(
    [Required, MaxLength(120)] string Title,
    [MaxLength(2000)] string? Description,
    [Required, MaxLength(40)] string Category,
    [Required, MaxLength(80)] string City,
    [Required, MaxLength(120)] string Venue,
    [MaxLength(200)] string? AddressLine,
    double? Latitude,
    double? Longitude,
    string? CoverUrl
);

public record UpdateEventRequest(
    [MaxLength(120)] string? Title,
    [MaxLength(2000)] string? Description,
    [MaxLength(40)] string? Category,
    [MaxLength(80)] string? City,
    [MaxLength(120)] string? Venue,
    [MaxLength(200)] string? AddressLine,
    double? Latitude,
    double? Longitude,
    string? CoverUrl
);

public record CreateSessionRequest(
    [Required] DateTime StartAt,
    DateTime? EndAt
);

public record CreateTicketTypeRequest(
    [Required, MaxLength(60)] string Name,
    [Range(0, double.MaxValue)] decimal Price,
    [Range(0, int.MaxValue)] int Capacity,
    [Range(1, int.MaxValue)] int PerPersonLimit,
    [Required] DateTime SalesStart,
    [Required] DateTime SalesEnd
);

public record SearchEventsRequest(
    string? City,
    string? Category,
    DateTime? StartDate,
    DateTime? EndDate
);

public record EventMediaPresignRequest(
    [Required] string ContentType
);

public record EventMediaPresignResponse(
    string UploadUrl,
    string ObjectKey
);

public record MediaCompleteRequest(
    [Required] string ObjectKey,
    string? Type
);