using TaskFlow.Application.DTOs;

namespace TaskFlow.Application.Interfaces;

/// <summary>
/// Cache for a single task owned by a user.
/// A Redis outage is translated by the implementation: get returns null, set completes, remove returns false.
/// </summary>
public interface ITaskCache
{
    /// <summary>
    /// Returns the cached task for this user and task, or null on a miss or cache read failure.
    /// </summary>
    Task<TaskDto?> GetAsync(Guid userId, Guid taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores the task. A cache write failure does not throw.
    /// </summary>
    Task SetAsync(Guid userId, Guid taskId, TaskDto task, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the cached task. Returns false when the cache cannot confirm the delete.
    /// </summary>
    Task<bool> RemoveAsync(Guid userId, Guid taskId, CancellationToken cancellationToken = default);
}
