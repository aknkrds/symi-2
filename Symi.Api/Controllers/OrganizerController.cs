using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Symi.Api.Data;
using Symi.Api.DTOs;
using Symi.Api.Models;
using Symi.Api.Services;
using Symi.Api.Utils;

namespace Symi.Api.Controllers;

[ApiController]
[Route("organizers")]
public class OrganizerController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IStorageService _storage;
    private readonly IConfiguration _config;

    public OrganizerController(AppDbContext db, IStorageService storage, IConfiguration config)
    {
        _db = db;
        _storage = storage;
        _config = config;
    }

    [HttpPost("documents/presign")]
    [Authorize]
    public ActionResult<KycPresignResponse> Presign([FromBody] KycPresignRequest req)
    {
        var bucket = _config["S3:Bucket"] ?? "symi-bucket";
        var sub = User.Claims.FirstOrDefault(c => c.Type == System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub || c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(sub, out var userId)) return Unauthorized();
        var key = $"kyc/{userId}/{Guid.NewGuid()}-{req.Type}.bin";
        var url = _storage.GetPresignedUploadUrl(bucket, key, req.ContentType);
        return new KycPresignResponse(url, key);
    }

    [HttpPost("apply")]
    [Authorize]
    public async Task<IActionResult> Apply([FromBody] KycApplyRequest req)
    {
        var sub = User.Claims.FirstOrDefault(c => c.Type == System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub || c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(sub, out var userId)) return Unauthorized();

        if (!IbanValidator.IsValid(req.Iban))
        {
            return BadRequest(new ErrorResponse("invalid_iban", "IBAN is invalid"));
        }
        if (req.Documents == null || req.Documents.Count == 0)
        {
            return BadRequest(new ErrorResponse("missing_documents", "At least one document is required"));
        }

        var existing = await _db.OrganizerKycs.FirstOrDefaultAsync(k => k.UserId == userId);
        if (existing != null && existing.Status == "pending")
        {
            return Conflict(new ErrorResponse("kyc_pending", "An existing KYC application is already pending"));
        }

        var app = existing ?? new OrganizerKycApplication { UserId = userId };
        app.CompanyName = req.CompanyName;
        app.TaxNumber = req.TaxNumber;
        app.Iban = req.Iban;
        app.AuthorizedName = req.AuthorizedName;
        app.Status = "pending";
        app.DecisionReason = null;
        app.ReviewedAt = null;
        app.ReviewedByUserId = null;
        app.UpdatedAt = DateTime.UtcNow;

        if (existing == null)
        {
            _db.OrganizerKycs.Add(app);
        }
        else
        {
            // replace docs
            var oldDocs = _db.KycDocuments.Where(d => d.ApplicationId == app.Id);
            _db.KycDocuments.RemoveRange(oldDocs);
        }

        foreach (var d in req.Documents)
        {
            _db.KycDocuments.Add(new KycDocument
            {
                ApplicationId = app.Id,
                Type = d.Type,
                ObjectKey = d.ObjectKey,
                ContentType = d.ContentType
            });
        }

        await _db.SaveChangesAsync();

        // Audit
        _db.AuditLogs.Add(new AuditLog { UserId = userId, Action = "kyc.apply", Data = System.Text.Json.JsonSerializer.Serialize(req) });
        await _db.SaveChangesAsync();

        return Accepted(new { status = app.Status });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<OrganizerMeResponse>> Me()
    {
        var sub = User.Claims.FirstOrDefault(c => c.Type == System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub || c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(sub, out var userId)) return Unauthorized();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return Unauthorized();

        var app = await _db.OrganizerKycs.FirstOrDefaultAsync(k => k.UserId == userId);
        var currentContract = await _db.OrganizerContracts.FirstOrDefaultAsync(c => c.IsCurrent);
        var hasAccepted = currentContract == null ? true : await _db.OrganizerContractAcceptances.AnyAsync(a => a.UserId == userId && a.ContractId == currentContract.Id);

        return new OrganizerMeResponse(
            app?.Status ?? "none",
            currentContract != null && !hasAccepted,
            currentContract?.Version,
            user.OrganizerVerifiedAt
        );
    }

    [HttpGet("contract")]
    [Authorize]
    public async Task<IActionResult> GetContract()
    {
        var current = await _db.OrganizerContracts.FirstOrDefaultAsync(c => c.IsCurrent);
        if (current == null) return NotFound(new ErrorResponse("no_contract", "No organizer contract available"));
        return Ok(new { version = current.Version, body = current.Body });
    }

    [HttpPost("contract/accept")]
    [Authorize]
    public async Task<IActionResult> AcceptContract()
    {
        var sub = User.Claims.FirstOrDefault(c => c.Type == System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub || c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(sub, out var userId)) return Unauthorized();
        var current = await _db.OrganizerContracts.FirstOrDefaultAsync(c => c.IsCurrent);
        if (current == null) return NotFound(new ErrorResponse("no_contract", "No organizer contract available"));

        var exists = await _db.OrganizerContractAcceptances.FirstOrDefaultAsync(a => a.UserId == userId && a.ContractId == current.Id);
        if (exists != null) return NoContent();

        var acc = new OrganizerContractAcceptance
        {
            UserId = userId,
            ContractId = current.Id,
            AcceptedAt = DateTime.UtcNow,
            Ip = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString()
        };
        _db.OrganizerContractAcceptances.Add(acc);
        _db.AuditLogs.Add(new AuditLog { UserId = userId, Action = "contract.accept", Data = System.Text.Json.JsonSerializer.Serialize(new { contract = current.Version, ip = acc.Ip, ua = acc.UserAgent }) });
        await _db.SaveChangesAsync();
        return NoContent();
    }
}