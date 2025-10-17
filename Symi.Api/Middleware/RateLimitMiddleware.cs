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

        var limit = 60; // default policy: 60 req/min per IP per route
        var remaining = Math.Max(0, limit - (int)count);
        context.Response.Headers["RateLimit-Limit"] = limit.ToString();
        context.Response.Headers["RateLimit-Remaining"] = remaining.ToString();
        context.Response.Headers["RateLimit-Reset"] = ((int)ttl.TotalSeconds).ToString();

        if (count > limit)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await context.Response.WriteAsJsonAsync(new { code = "rate_limit_exceeded", message = "Too Many Requests" });
            return;
        }

        await _next(context);
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