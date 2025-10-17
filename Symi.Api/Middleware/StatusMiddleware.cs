using Microsoft.EntityFrameworkCore;
using Symi.Api.Data;

namespace Symi.Api.Middleware;

public class StatusMiddleware
{
    private readonly RequestDelegate _next;

    private static readonly PathString[] _excluded =
    {
        new PathString("/auth/login"),
        new PathString("/auth/register"),
        new PathString("/auth/refresh"),
        new PathString("/health"),
        new PathString("/swagger"),
        new PathString("/openapi")
    };

    public StatusMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, AppDbContext db)
    {
        if (_excluded.Any(p => context.Request.Path.StartsWithSegments(p)))
        {
            await _next(context);
            return;
        }

        // Only enforce for authenticated users
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            var sub = context.User.Claims.FirstOrDefault(c => c.Type == System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
            if (Guid.TryParse(sub, out var userId))
            {
                var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
                if (user != null)
                {
                    if (user.Status == "banned" || user.Status == "frozen")
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        await context.Response.WriteAsJsonAsync(new { code = "account_restricted", message = "Account is banned or frozen." });
                        return;
                    }
                }
            }
        }
        await _next(context);
    }
}