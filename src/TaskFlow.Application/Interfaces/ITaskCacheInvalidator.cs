namespace TaskFlow.Application.Interfaces;

/// <summary>
/// Deletes one task cache entry after a successful MongoDB write.
/// A cache failure is logged and does not fail the business operation.
/// </summary>
public interface ITaskCacheInvalidator
{
    /// <summary>
    /// Removes the cache entry for this user and task. Retries a failed delete.
    /// Completes even when every attempt fails.
    /// </summary>
    Task InvalidateAsync(Guid userId, Guid taskId, CancellationToken cancellationToken = default);
}
