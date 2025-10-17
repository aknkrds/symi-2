using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Symi.Api.Data;
using Symi.Api.Models;

namespace Symi.Api.Services;

public class JwtService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly PasswordHasher _hasher;

    public JwtService(AppDbContext db, IConfiguration config, PasswordHasher hasher)
    {
        _db = db;
        _config = config;
        _hasher = hasher;
    }

    public async Task<(string accessToken, string refreshToken)> IssueTokensAsync(User user)
    {
        var accessToken = CreateAccessToken(user);
        var refreshTokenRaw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var refreshTokenHash = _hasher.HashOpaqueToken(refreshTokenRaw);
        var refreshExpiresDays = int.TryParse(_config["Jwt:RefreshDays"], out var d) ? d : 14;

        var rt = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshTokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(refreshExpiresDays)
        };
        _db.RefreshTokens.Add(rt);
        await _db.SaveChangesAsync();
        return (accessToken, refreshTokenRaw);
    }

    public string CreateAccessToken(User user)
    {
        var accessMinutes = int.TryParse(_config["Jwt:AccessMinutes"], out var m) ? m : 15;
        var issuer = _config["Jwt:Issuer"] ?? "symi";
        var audience = _config["Jwt:Audience"] ?? "symi-client";
        var secret = _config["Jwt:AccessTokenSecret"] ?? "dev-access-secret-change-me";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role)
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(accessMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task<(string accessToken, string refreshToken)?> RotateRefreshTokenAsync(User user, string providedRefreshToken)
    {
        var providedHash = _hasher.HashOpaqueToken(providedRefreshToken);
        var existing = await _db.RefreshTokens
            .Where(r => r.UserId == user.Id && r.TokenHash == providedHash)
            .FirstOrDefaultAsync();
        if (existing == null) return null; // not found
        if (existing.RevokedAt != null || existing.Used || existing.ExpiresAt < DateTime.UtcNow)
        {
            return null; // invalid
        }

        existing.Used = true;
        existing.RevokedAt = DateTime.UtcNow;

        var access = CreateAccessToken(user);
        var newRaw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var newHash = _hasher.HashOpaqueToken(newRaw);
        var refreshExpiresDays = int.TryParse(_config["Jwt:RefreshDays"], out var d) ? d : 14;
        var replacement = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = newHash,
            ExpiresAt = DateTime.UtcNow.AddDays(refreshExpiresDays)
        };
        _db.RefreshTokens.Add(replacement);
        await _db.SaveChangesAsync();

        existing.ReplacedByTokenId = replacement.Id;
        await _db.SaveChangesAsync();

        return (access, newRaw);
    }
}