using MediatR;
using TaskFlow.Application.Common.Results;
using TaskFlow.Application.DTOs;
using TaskFlow.Application.Interfaces;
using DomainTaskStatus = TaskFlow.Domain.Enums.TaskStatus;

namespace TaskFlow.Application.UseCases.Tasks.GetTaskSummary;

/// <summary>
/// Handles the per-status task count. Statuses without tasks are reported as zero.
/// </summary>
public sealed class GetTaskSummaryQueryHandler : IRequestHandler<GetTaskSummaryQuery, Result<TaskSummaryDto>>
{
    private readonly ITaskRepository _taskRepository;

    public GetTaskSummaryQueryHandler(ITaskRepository taskRepository)
    {
        _taskRepository = taskRepository;
    }

    public async Task<Result<TaskSummaryDto>> Handle(GetTaskSummaryQuery request, CancellationToken cancellationToken)
    {
        var counts = await _taskRepository.CountByStatusAsync(request.UserId, cancellationToken);

        var pending = counts.GetValueOrDefault(DomainTaskStatus.Pending);
        var inProgress = counts.GetValueOrDefault(DomainTaskStatus.InProgress);
        var completed = counts.GetValueOrDefault(DomainTaskStatus.Completed);

        return Result<TaskSummaryDto>.Ok(
            new TaskSummaryDto(pending, inProgress, completed, pending + inProgress + completed));
    }
}
