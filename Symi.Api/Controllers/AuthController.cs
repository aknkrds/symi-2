using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Symi.Api.Data;
using Symi.Api.DTOs;
using Symi.Api.Models;
using Symi.Api.Services;
using System.Security.Cryptography;

namespace Symi.Api.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly PasswordHasher _hasher;
    private readonly JwtService _jwt;
    private readonly ILogger<AuthController> _logger;

    public AuthController(AppDbContext db, PasswordHasher hasher, JwtService jwt, ILogger<AuthController> logger)
    {
        _db = db;
        _hasher = hasher;
        _jwt = jwt;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        if (await _db.Users.AnyAsync(u => u.Email == req.Email))
            return Conflict(new ErrorResponse("email_taken", "Email already registered"));
        if (await _db.Users.AnyAsync(u => u.Username == req.Username))
            return Conflict(new ErrorResponse("username_taken", "Username already in use"));

        var (hash, salt) = _hasher.Hash(req.Password);
        var user = new User
        {
            Email = req.Email,
            Username = req.Username,
            FullName = req.FullName,
            BirthDate = req.BirthDate,
            PasswordHash = hash,
            PasswordSalt = salt,
            PasswordAlgorithm = "PBKDF2",
            Role = "user",
            Status = "active"
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var (access, refresh) = await _jwt.IssueTokensAsync(user);
        return Ok(new TokenResponse(access, refresh));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        try
        {
            _logger.LogInformation("Login attempt for {Id}", req.EmailOrUsername);
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == req.EmailOrUsername || u.Username == req.EmailOrUsername);
            if (user == null)
            {
                // Constant-time compare against dummy values to prevent timing disclosure
                var dummySalt = RandomNumberGenerator.GetBytes(16);
                var dummyHash = RandomNumberGenerator.GetBytes(32);
                _hasher.Verify(req.Password, dummyHash, dummySalt);
                _logger.LogWarning("Login failed: user not found for {Id}", req.EmailOrUsername);
                return Unauthorized(new ErrorResponse("invalid_credentials", "Invalid credentials"));
            }

            _logger.LogInformation("User resolved: {UserId}", user.Id);
            var ok = _hasher.Verify(req.Password, user.PasswordHash, user.PasswordSalt);
            if (!ok)
            {
                _logger.LogWarning("Login failed: bad password for user {UserId}", user.Id);
                return Unauthorized(new ErrorResponse("invalid_credentials", "Invalid credentials"));
            }

            _logger.LogInformation("Password OK, issuing tokens for {UserId}", user.Id);
            try
            {
                var (access, refresh) = await _jwt.IssueTokensAsync(user);
                _logger.LogInformation("Login succeeded for {UserId}", user.Id);
                return Ok(new TokenResponse(access, refresh));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "IssueTokens failed for {UserId}, falling back to ephemeral refresh", user.Id);
                var access = _jwt.CreateAccessToken(user);
                var refresh = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
                return Ok(new TokenResponse(access, refresh));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in Login for {Id}: {Message}", req.EmailOrUsername, ex.Message);
            return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse("error", "Internal Server Error"));
        }
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest req)
    {
        // Find refresh token owner by token hash
        var providedHash = _hasher.HashOpaqueToken(req.RefreshToken);
        var existing = await _db.RefreshTokens.Include(r => r.User)
            .FirstOrDefaultAsync(r => r.TokenHash == providedHash);
        if (existing == null || existing.User == null)
        {
            return Unauthorized(new ErrorResponse("invalid_refresh", "Refresh token invalid or revoked"));
        }

        var rotated = await _jwt.RotateRefreshTokenAsync(existing.User, req.RefreshToken);
        if (rotated == null)
        {
            return Unauthorized(new ErrorResponse("invalid_refresh", "Refresh token invalid or revoked"));
        }

        return Ok(new TokenResponse(rotated.Value.accessToken, rotated.Value.refreshToken));
    }
}