using System.ComponentModel.DataAnnotations;

namespace Symi.Api.DTOs;

public record CreatePostRequest(
    [MaxLength(1000)] string? Text,
    [MaxLength(256)] string? MediaUrl,
    [MaxLength(20)] string? MediaType, // image | video
    int? DurationSec,
    Guid? EventId
);

public record ReactRequest(
    [Required, RegularExpression("^(like|unlike)$")] string Action
);

public record CreateCommentRequest(
    [Required, MaxLength(500)] string Text
);

public record CreateReportRequest(
    [Required, RegularExpression("^(post|comment)$")] string TargetType,
    [Required] Guid TargetId,
    [Required, MaxLength(60)] string Reason,
    [MaxLength(1000)] string? Details
);

public record FeedQuery(
    string? City,
    double? Lat,
    double? Lng,
    int Page = 1,
    int PageSize = 20
);

public record FeedItemDto(
    Guid Id,
    Guid UserId,
    Guid? OrganizerUserId,
    Guid? EventId,
    string? Text,
    string? MediaUrl,
    string? MediaType,
    int? DurationSec,
    DateTime CreatedAt,
    int ReactionCount,
    int CommentCount,
    bool ReactedByMe
);