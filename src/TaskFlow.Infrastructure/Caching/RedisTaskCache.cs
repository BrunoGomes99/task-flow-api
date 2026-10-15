using System.Text.Json;
using Microsoft.Extensions.Logging;
using TaskFlow.Application.DTOs;
using TaskFlow.Application.Interfaces;

namespace TaskFlow.Infrastructure.Caching;

/// <summary>
/// Redis Cache Aside store for one task. Keys include the owner and the task id.
/// Entries expire 60 seconds after they are written.
/// </summary>
public sealed class RedisTaskCache : ITaskCache
{
    public static readonly TimeSpan AbsoluteTtl = TimeSpan.FromSeconds(60);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IRedisStringCache _store;
    private readonly ILogger<RedisTaskCache> _logger;

    public RedisTaskCache(IRedisStringCache store, ILogger<RedisTaskCache> logger)
    {
        _store = store;
        _logger = logger;
    }

    public async Task<TaskDto?> GetAsync(Guid userId, Guid taskId, CancellationToken cancellationToken = default)
    {
        try
        {
            var json = await _store.GetAsync(CacheKey(userId, taskId), cancellationToken);
            if (string.IsNullOrWhiteSpace(json))
                return null;

            return JsonSerializer.Deserialize<TaskDto>(json, JsonOptions);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Task cache read failed. UserId={UserId} TaskId={TaskId}",
                userId,
                taskId);
            return null;
        }
    }

    public async Task SetAsync(Guid userId, Guid taskId, TaskDto task, CancellationToken cancellationToken = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(task, JsonOptions);
            await _store.SetAsync(CacheKey(userId, taskId), json, AbsoluteTtl, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Task cache write failed. UserId={UserId} TaskId={TaskId}",
                userId,
                taskId);
        }
    }

    public async Task<bool> RemoveAsync(Guid userId, Guid taskId, CancellationToken cancellationToken = default)
    {
        try
        {
            await _store.DeleteAsync(CacheKey(userId, taskId), cancellationToken);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Task cache delete failed. UserId={UserId} TaskId={TaskId}",
                userId,
                taskId);
            return false;
        }
    }

    internal static string CacheKey(Guid userId, Guid taskId) => $"task:{userId:D}:{taskId:D}";
}
