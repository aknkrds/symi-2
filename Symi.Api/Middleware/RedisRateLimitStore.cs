using StackExchange.Redis;

namespace Symi.Api.Middleware;

public class RedisRateLimitStore : IRateLimitStore
{
    private readonly IDatabase _db;

    public RedisRateLimitStore(IConnectionMultiplexer mux)
    {
        _db = mux.GetDatabase();
    }

    public async Task<(long count, TimeSpan ttl)> IncrementAsync(string key, TimeSpan window)
    {
        var count = await _db.StringIncrementAsync(key);
        var ttl = await _db.KeyTimeToLiveAsync(key);
        if (ttl == null)
        {
            await _db.KeyExpireAsync(key, window);
            ttl = window;
        }
        return (count, ttl!.Value);
    }
}