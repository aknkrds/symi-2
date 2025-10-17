using System.Net;
using System.Collections.Concurrent;
using Microsoft.Extensions.Primitives;

namespace Symi.Api.Middleware;

public interface IRateLimitStore
{
    Task<(long count, TimeSpan ttl)> IncrementAsync(string key, TimeSpan window);
}

public class InMemoryRateLimitStore : IRateLimitStore
{
    private readonly ConcurrentDictionary<string, (long count, DateTime expiry)> _store = new();

    public Task<(long count, TimeSpan ttl)> IncrementAsync(string key, TimeSpan window)
    {
        var now = DateTime.UtcNow;
        var entry = _store.AddOrUpdate(key,
            _ => (1, now.Add(window)),
            (_, existing) => existing.expiry < now ? (1, now.Add(window)) : (existing.count + 1, existing.expiry));
        var ttl = entry.expiry - now;
        return Task.FromResult((entry.count, ttl));
    }
}

public class RateLimitMiddleware
{
    private readonly RequestDelegate _next;

    private static readonly PathString[] _excludePaths =
    {
        new PathString("/health"),
        new PathString("/swagger"),
        new PathString("/openapi")
    };

    public RateLimitMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IRateLimitStore store, ILogger<RateLimitMiddleware> logger)
    {
        // Exclude some paths
        if (_excludePaths.Any(p => context.Request.Path.StartsWithSegments(p)))
        {
            await _next(context);
            return;
        }

        var ip = GetClientIp(context);
        var route = context.Request.Path.ToString().ToLowerInvariant();
        var key = $"rl:{ip}:{route}";
        var window = TimeSpan.FromMinutes(1);
        var (count, ttl) = await store.IncrementAsync(key, window);

        // Config-based limits
        var config = context.RequestServices.GetRequiredService<IConfiguration>();
        var defaultLimit = int.TryParse(config["RateLimit:DefaultPerMinute"], out var dl) ? dl : 60;
        var routeLimit = int.TryParse(config[$"RateLimit:Routes:{route}"], out var rl) ? rl : defaultLimit;

        var remaining = Math.Max(0, routeLimit - (int)count);
        context.Response.Headers["RateLimit-Limit"] = routeLimit.ToString();
        context.Response.Headers["RateLimit-Remaining"] = remaining.ToString();
        context.Response.Headers["RateLimit-Reset"] = ((int)ttl.TotalSeconds).ToString();

        if (count > routeLimit)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers["Retry-After"] = Math.Max(1, (int)Math.Ceiling(ttl.TotalSeconds)).ToString();
            await context.Response.WriteAsJsonAsync(new { code = "rate_limit_exceeded", message = "Too Many Requests" });
            return;
        }

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Downstream failure at {Route}: {Message}", route, ex.Message);
            throw;
        }
    }

    private static string GetClientIp(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("X-Forwarded-For", out StringValues forwarded))
        {
            var ip = forwarded.ToString().Split(',').FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(ip)) return ip.Trim();
        }
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}