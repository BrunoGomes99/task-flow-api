namespace TaskFlow.Infrastructure.Caching;

/// <summary>
/// String store used by <see cref="RedisTaskCache"/>. The TTL passed to <see cref="SetAsync"/> is absolute:
/// a later read must not extend it.
/// </summary>
public interface IRedisStringCache
{
    Task<string?> GetAsync(string key, CancellationToken cancellationToken);

    Task SetAsync(string key, string value, TimeSpan absoluteTtl, CancellationToken cancellationToken);

    Task DeleteAsync(string key, CancellationToken cancellationToken);
}
