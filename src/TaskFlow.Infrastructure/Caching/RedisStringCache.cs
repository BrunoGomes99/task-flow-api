using StackExchange.Redis;

namespace TaskFlow.Infrastructure.Caching;

/// <summary>
/// StackExchange.Redis adapter. <c>StringSet</c> expiry is the lifetime from the write, and reads do not refresh it.
/// </summary>
public sealed class RedisStringCache : IRedisStringCache
{
    private readonly IConnectionMultiplexer _multiplexer;

    public RedisStringCache(IConnectionMultiplexer multiplexer)
    {
        _multiplexer = multiplexer;
    }

    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var value = await _multiplexer.GetDatabase().StringGetAsync(key);
        return value.IsNullOrEmpty ? null : value.ToString();
    }

    public async Task SetAsync(string key, string value, TimeSpan absoluteTtl, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _multiplexer.GetDatabase().StringSetAsync(key, value, absoluteTtl);
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _multiplexer.GetDatabase().KeyDeleteAsync(key);
    }
}
