using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Symi.Api.Data;
using Symi.Api.DTOs;
using Symi.Api.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Symi.Api.Controllers;

[ApiController]
[Route("posts")]
public class PostsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<PostsController> _logger;

    private static readonly string[] BannedWords = new[] { "terror", "hate", "violence", "drugs" };

    public PostsController(AppDbContext db, ILogger<PostsController> logger)
    { _db = db; _logger = logger; }

    private Guid? GetUserId()
    {
        var sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(sub, out var id)) return id;
        return null;
    }

    private static bool ViolatesPolicy(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var lower = text.ToLowerInvariant();
        return BannedWords.Any(b => lower.Contains(b));
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] CreatePostRequest req)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(req.Text) && string.IsNullOrWhiteSpace(req.MediaUrl))
            return BadRequest(new { message = "Text or media is required" });

        if (req.MediaType != null && req.MediaType != "image" && req.MediaType != "video")
            return BadRequest(new { message = "MediaType must be image or video" });

        if (req.MediaType == "video" && (req.DurationSec == null || req.DurationSec > 60 || req.DurationSec <= 0))
            return BadRequest(new { message = "Video duration must be 1-60 seconds" });

        if (ViolatesPolicy(req.Text))
            return BadRequest(new { message = "Content violates policy" });

        var post = new Post
        {
            UserId = userId.Value,
            Text = req.Text,
            MediaUrl = req.MediaUrl,
            MediaType = req.MediaType,
            DurationSec = req.DurationSec,
            EventId = req.EventId,
            Status = "active",
            CreatedAt = DateTime.UtcNow
        };
        _db.Posts.Add(post);
        await _db.SaveChangesAsync();
        return Created($"/posts/{post.Id}", post);
    }

    [HttpPost("{id}/react")]
    [Authorize]
    public async Task<IActionResult> React(Guid id, [FromBody] ReactRequest req)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var post = await _db.Posts.FirstOrDefaultAsync(p => p.Id == id && p.Status == "active");
        if (post == null) return NotFound();

        var existing = await _db.PostReactions.FirstOrDefaultAsync(r => r.PostId == id && r.UserId == userId);
        if (req.Action == "like")
        {
            if (existing == null)
            {
                _db.PostReactions.Add(new PostReaction { PostId = id, UserId = userId.Value, Type = "like" });
                await _db.SaveChangesAsync();
            }
        }
        else // unlike
        {
            if (existing != null)
            {
                _db.PostReactions.Remove(existing);
                await _db.SaveChangesAsync();
            }
        }

        var reactionCount = await _db.PostReactions.CountAsync(r => r.PostId == id);
        return Ok(new { status = "ok", reactionCount });
    }

    [HttpPost("{id}/comments")]
    [Authorize]
    public async Task<IActionResult> Comment(Guid id, [FromBody] CreateCommentRequest req)
    {
        var userId = GetUserId(); if (userId == null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(req.Text)) return BadRequest(new { message = "Text required" });
        if (req.Text.Length > 500) return BadRequest(new { message = "Comment too long" });
        if (ViolatesPolicy(req.Text)) return BadRequest(new { message = "Content violates policy" });

        var post = await _db.Posts.FirstOrDefaultAsync(p => p.Id == id && p.Status == "active");
        if (post == null) return NotFound();

        var c = new Comment { PostId = id, UserId = userId.Value, Text = req.Text, Status = "active" };
        _db.Comments.Add(c);
        await _db.SaveChangesAsync();
        return Created($"/posts/{id}/comments/{c.Id}", c);
    }
}