using Microsoft.Extensions.Logging;
using TaskFlow.Application.Interfaces;

namespace TaskFlow.Application.Caching;

/// <summary>
/// Deletes a task cache key up to three times. A false result is a failed attempt.
/// After the last failure, the key is logged and the business operation still succeeds.
/// </summary>
public sealed class TaskCacheInvalidator : ITaskCacheInvalidator
{
    public const int MaxAttempts = 3;

    public static readonly TimeSpan DefaultPause = TimeSpan.FromMilliseconds(50);

    private readonly ITaskCache _cache;
    private readonly ILogger<TaskCacheInvalidator> _logger;
    private readonly TimeSpan _pauseBetweenAttempts;

    public TaskCacheInvalidator(ITaskCache cache, ILogger<TaskCacheInvalidator> logger)
        : this(cache, logger, DefaultPause)
    {
    }

    public TaskCacheInvalidator(
        ITaskCache cache,
        ILogger<TaskCacheInvalidator> logger,
        TimeSpan pauseBetweenAttempts)
    {
        _cache = cache;
        _logger = logger;
        _pauseBetweenAttempts = pauseBetweenAttempts;
    }

    public async Task InvalidateAsync(Guid userId, Guid taskId, CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            if (await _cache.RemoveAsync(userId, taskId, cancellationToken))
                return;

            if (attempt < MaxAttempts)
                await Task.Delay(_pauseBetweenAttempts, cancellationToken);
        }

        _logger.LogWarning(
            "Failed to delete task cache key after {Attempts} attempts. UserId={UserId} TaskId={TaskId}",
            MaxAttempts,
            userId,
            taskId);
    }
}
