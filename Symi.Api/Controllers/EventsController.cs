using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Symi.Api.Data;
using Symi.Api.DTOs;
using Symi.Api.Models;
using Symi.Api.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Symi.Api.Controllers;

[ApiController]
[Route("events")]
public class EventsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IStorageService _storage;
    private readonly IConfiguration _config;

    public EventsController(AppDbContext db, IStorageService storage, IConfiguration config)
    {
        _db = db;
        _storage = storage;
        _config = config;
    }

    private Guid? GetUserId()
    {
        var sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(sub, out var id)) return id;
        return null;
    }

    private static bool IsReadyToPublish(Event e)
    {
        return !string.IsNullOrWhiteSpace(e.Title)
            && !string.IsNullOrWhiteSpace(e.Category)
            && !string.IsNullOrWhiteSpace(e.City)
            && !string.IsNullOrWhiteSpace(e.Venue);
    }

    // Create event (draft)
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] CreateEventRequest req)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var ev = new Event
        {
            OrganizerUserId = userId.Value,
            Title = req.Title,
            Description = req.Description,
            Category = req.Category,
            City = req.City,
            Venue = req.Venue,
            AddressLine = req.AddressLine,
            Latitude = req.Latitude,
            Longitude = req.Longitude,
            CoverUrl = req.CoverUrl,
            Status = "draft",
            CreatedAt = DateTime.UtcNow
        };
        _db.Events.Add(ev);
        await _db.SaveChangesAsync();
        return Created($"/events/{ev.Id}", ev);
    }

    // Get event (published visible to all; owner can view any)
    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var ev = await _db.Events.Include(e => e.Sessions).Include(e => e.TicketTypes).FirstOrDefaultAsync(e => e.Id == id);
        if (ev == null) return NotFound();
        var userId = GetUserId();
        if (ev.Status != "published" && (userId == null || ev.OrganizerUserId != userId))
        {
            return NotFound();
        }
        return Ok(ev);
    }

    // Update event (only owner; not allowed if published)
    [HttpPut("{id}")]
    [Authorize]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateEventRequest req)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.Id == id);
        if (ev == null) return NotFound();
        if (ev.OrganizerUserId != userId) return Forbid();
        if (ev.Status == "published") return Conflict(new { message = "Published event cannot be modified." });

        if (req.Title != null) ev.Title = req.Title;
        if (req.Description != null) ev.Description = req.Description;
        if (req.Category != null) ev.Category = req.Category;
        if (req.City != null) ev.City = req.City;
        if (req.Venue != null) ev.Venue = req.Venue;
        if (req.AddressLine != null) ev.AddressLine = req.AddressLine;
        if (req.Latitude != null) ev.Latitude = req.Latitude;
        if (req.Longitude != null) ev.Longitude = req.Longitude;
        if (req.CoverUrl != null) ev.CoverUrl = req.CoverUrl;
        ev.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(ev);
    }

    // Submit for approval (draft/rejected -> pending)
    [HttpPost("{id}/submit")]
    [Authorize]
    public async Task<IActionResult> Submit(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.Id == id);
        if (ev == null) return NotFound();
        if (ev.OrganizerUserId != userId) return Forbid();
        if (ev.Status != "draft" && ev.Status != "rejected") return Conflict(new { message = "Only draft/rejected events can be submitted." });

        ev.Status = "pending";
        ev.RejectionReason = null;
        ev.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(ev);
    }

    // Publish (only after approved; require required fields)
    [HttpPost("{id}/publish")]
    [Authorize]
    public async Task<IActionResult> Publish(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.Id == id);
        if (ev == null) return NotFound();
        if (ev.OrganizerUserId != userId) return Forbid();
        if (ev.Status != "approved") return Conflict(new { message = "Event must be approved before publishing." });
        if (!IsReadyToPublish(ev)) return BadRequest(new { message = "Missing required fields for publish." });

        ev.Status = "published";
        ev.PublishedAt = DateTime.UtcNow;
        ev.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(ev);
    }

    // Search (published only)
    [HttpGet("search")]
    [AllowAnonymous]
    public async Task<IActionResult> Search([FromQuery] SearchEventsRequest req)
    {
        var q = _db.Events.AsQueryable();
        q = q.Where(e => e.Status == "published");
        if (!string.IsNullOrWhiteSpace(req.City)) q = q.Where(e => e.City == req.City);
        if (!string.IsNullOrWhiteSpace(req.Category)) q = q.Where(e => e.Category == req.Category);
        if (req.StartDate != null || req.EndDate != null)
        {
            var start = req.StartDate ?? DateTime.MinValue;
            var end = req.EndDate ?? DateTime.MaxValue;
            q = q.Where(e => _db.EventSessions.Any(s => s.EventId == e.Id && s.StartAt <= end && (s.EndAt ?? s.StartAt) >= start));
        }
        var list = await q.OrderBy(e => e.PublishedAt).Take(100).ToListAsync();
        return Ok(list);
    }

    // Add session
    [HttpPost("{id}/sessions")]
    [Authorize]
    public async Task<IActionResult> AddSession(Guid id, [FromBody] CreateSessionRequest req)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.Id == id);
        if (ev == null) return NotFound();
        if (ev.OrganizerUserId != userId) return Forbid();
        if (ev.Status == "published") return Conflict(new { message = "Cannot add session to published event." });
        if (req.EndAt != null && req.EndAt < req.StartAt) return BadRequest(new { message = "EndAt must be after StartAt." });

        var s = new EventSession { EventId = id, StartAt = req.StartAt, EndAt = req.EndAt, CreatedAt = DateTime.UtcNow };
        _db.EventSessions.Add(s);
        await _db.SaveChangesAsync();
        return Created($"/events/{id}/sessions/{s.Id}", s);
    }

    // Add ticket type
    [HttpPost("{id}/tickets")]
    [Authorize]
    public async Task<IActionResult> AddTicketType(Guid id, [FromBody] CreateTicketTypeRequest req)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.Id == id);
        if (ev == null) return NotFound();
        if (ev.OrganizerUserId != userId) return Forbid();
        if (ev.Status == "published") return Conflict(new { message = "Cannot add ticket type to published event." });
        if (req.SalesEnd <= req.SalesStart) return BadRequest(new { message = "SalesEnd must be after SalesStart." });

        var t = new TicketType
        {
            EventId = id,
            Name = req.Name,
            Price = req.Price,
            Capacity = req.Capacity,
            PerPersonLimit = req.PerPersonLimit,
            SalesStart = req.SalesStart,
            SalesEnd = req.SalesEnd,
            CreatedAt = DateTime.UtcNow
        };
        _db.TicketTypes.Add(t);
        await _db.SaveChangesAsync();
        return Created($"/events/{id}/tickets/{t.Id}", t);
    }

    // Media presign
    [HttpPost("{id}/media/presign")]
    [Authorize]
    public IActionResult Presign(Guid id, [FromBody] EventMediaPresignRequest req)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var ev = _db.Events.FirstOrDefault(e => e.Id == id);
        if (ev == null) return NotFound();
        if (ev.OrganizerUserId != userId) return Forbid();
    
        var bucket = _config["Storage:Bucket"] ?? "symi-media";
        var key = $"events/{id}/original/{Guid.NewGuid()}";
        var url = _storage.GetPresignedUploadUrl(bucket, key, req.ContentType, TimeSpan.FromMinutes(15));
        return Ok(new EventMediaPresignResponse(url, key));
    }

    // Media upload completed -> enqueue thumbnail job
    [HttpPost("{id}/media/complete")]
    [Authorize]
    public async Task<IActionResult> MediaComplete(Guid id, [FromBody] MediaCompleteRequest req)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.Id == id);
        if (ev == null) return NotFound();
        if (ev.OrganizerUserId != userId) return Forbid();

        var job = new MediaJob
        {
            ObjectKey = req.ObjectKey,
            Type = string.IsNullOrWhiteSpace(req.Type) ? "image" : req.Type!,
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };
        _db.MediaJobs.Add(job);
        await _db.SaveChangesAsync();
        return Accepted(new { jobId = job.Id, status = job.Status });
    }
}