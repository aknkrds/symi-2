using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Symi.Api.Data;
using Symi.Api.DTOs;
using Symi.Api.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Symi.Api.Controllers;

[ApiController]
[Route("feed")]
public class FeedController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;

    public FeedController(AppDbContext db, IMemoryCache cache)
    { _db = db; _cache = cache; }

    private Guid? GetUserId()
    {
        var sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(sub, out var id)) return id;
        return null;
    }

    private static double Haversine(double lat1, double lon1, double lat2, double lon2)
    {
        double R = 6371; // km
        double dLat = (lat2 - lat1) * Math.PI / 180.0;
        double dLon = (lon2 - lon1) * Math.PI / 180.0;
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Get([FromQuery] FeedQuery q)
    {
        var userId = GetUserId(); if (userId == null) return Unauthorized();

        var key = $"feed:{userId}:{q.City}:{q.Lat}:{q.Lng}:{q.Page}:{q.PageSize}";
        if (_cache.TryGetValue(key, out List<FeedItemDto>? cached))
        {
            return Ok(cached);
        }

        var followedOrganizerIds = await _db.Follows.Where(f => f.UserId == userId).Select(f => f.OrganizerUserId).ToListAsync();

        var posts = _db.Posts.Where(p => p.Status == "active");
        var sourceA = posts.Where(p => p.OrganizerUserId != null && followedOrganizerIds.Contains(p.OrganizerUserId.Value));

        IQueryable<Guid> nearEventIds = Enumerable.Empty<Guid>().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q.City))
        {
            nearEventIds = _db.Events.Where(e => e.Status == "published" && e.City == q.City).Select(e => e.Id);
        }
        else if (q.Lat != null && q.Lng != null)
        {
            // Rough filter by distance <= 25km using lat/lng in memory (fallback)
            var evs = await _db.Events.Where(e => e.Status == "published" && e.Latitude != null && e.Longitude != null).ToListAsync();
            var nearby = evs.Where(e => Haversine(q.Lat.Value, q.Lng.Value, e.Latitude!.Value, e.Longitude!.Value) <= 25.0).Select(e => e.Id).ToList();
            nearEventIds = nearby.AsQueryable();
        }
        var sourceB = posts.Where(p => p.EventId != null && nearEventIds.Contains(p.EventId.Value));

        var mixed = sourceA.Concat(sourceB).OrderByDescending(p => p.CreatedAt);
        var paged = await mixed.Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).ToListAsync();

        // Enrich with counts and reaction status
        var postIds = paged.Select(p => p.Id).ToList();
        var reactions = await _db.PostReactions.Where(r => postIds.Contains(r.PostId)).ToListAsync();
        var comments = await _db.Comments.Where(c => postIds.Contains(c.PostId)).ToListAsync();
        var items = paged.Select(p => new FeedItemDto(
            p.Id,
            p.UserId,
            p.OrganizerUserId,
            p.EventId,
            p.Text,
            p.MediaUrl,
            p.MediaType,
            p.DurationSec,
            p.CreatedAt,
            reactions.Count(r => r.PostId == p.Id),
            comments.Count(c => c.PostId == p.Id),
            reactions.Any(r => r.PostId == p.Id && r.UserId == userId)
        )).ToList();

        _cache.Set(key, items, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30)
        });

        return Ok(items);
    }
}