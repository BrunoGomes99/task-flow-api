using DomainTask = TaskFlow.Domain.Entities.Task;

namespace TaskFlow.Application.Interfaces;

/// <summary>
/// Cross-tenant reads used only by reminder workers. User HTTP use cases stay on <see cref="ITaskRepository"/>.
/// </summary>
public interface ITaskReminderRepository
{
    Task<IReadOnlyList<DomainTask>> ListInWindowAsync(
        DateTime windowStartInclusiveUtc,
        DateTime windowEndExclusiveUtc,
        CancellationToken cancellationToken = default);

    Task<DomainTask?> FindByIdAsync(Guid taskId, CancellationToken cancellationToken = default);
}
