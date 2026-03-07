using TaskFlow.Application.Common;
using TaskFlow.Application.Enums;
using DomainTask = TaskFlow.Domain.Entities.Task;
using DomainTaskStatus = TaskFlow.Domain.Enums.TaskStatus;

namespace TaskFlow.Application.Interfaces;

/// <summary>
/// Repository for Task aggregate root. All operations are scoped by UserId for multi-tenancy.
/// Implemented in Infrastructure (e.g. MongoDB).
/// </summary>
public interface ITaskRepository
{
    /// <summary>
    /// Gets a task by id if it belongs to the given user.
    /// </summary>
    /// <param name="userId">The owner user id (from JWT).</param>
    /// <param name="taskId">The task id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The task if found and owned by the user; otherwise null.</returns>
    System.Threading.Tasks.Task<DomainTask?> GetByIdAsync(Guid userId, Guid taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new task. The task id is set by the domain; the implementation persists it.
    /// </summary>
    System.Threading.Tasks.Task AddAsync(DomainTask task, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing task. The task must belong to the user (enforced by use case/repository).
    /// </summary>
    System.Threading.Tasks.Task UpdateAsync(DomainTask task, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a task by id if it belongs to the given user.
    /// </summary>
    /// <param name="userId">The owner user id (from JWT).</param>
    /// <param name="taskId">The task id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if a task was deleted; false if not found or not owned by the user.</returns>
    System.Threading.Tasks.Task<bool> DeleteAsync(Guid userId, Guid taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a paginated list of tasks for the user with optional filters and ordering.
    /// </summary>
    /// <param name="userId">The owner user id (from JWT).</param>
    /// <param name="pageNumber">1-based page number.</param>
    /// <param name="pageSize">Page size (max 20 enforced by use case/validation).</param>
    /// <param name="titleContains">Optional filter: title contains this string (case-insensitive).</param>
    /// <param name="descriptionContains">Optional filter: description contains this string (case-insensitive).</param>
    /// <param name="status">Optional filter: exact status.</param>
    /// <param name="dueDateOrder">Order by due date ascending or descending.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    System.Threading.Tasks.Task<PagedResult<DomainTask>> GetPagedAsync(
        Guid userId,
        int pageNumber,
        int pageSize,
        string? titleContains,
        string? descriptionContains,
        DomainTaskStatus? status,
        DueDateOrder dueDateOrder,
        CancellationToken cancellationToken = default);
}
