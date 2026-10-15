using MediatR;
using TaskFlow.Application.Common.Results;
using TaskFlow.Application.Interfaces;

namespace TaskFlow.Application.UseCases.Tasks.UpdateTaskStatus;

/// <summary>
/// Handles task status transitions using domain methods.
/// Invalid transitions are represented as <see cref="Result"/> conflicts (HTTP 409 at the API boundary).
/// </summary>
public sealed class UpdateTaskStatusCommandHandler : IRequestHandler<UpdateTaskStatusCommand, Result>
{
    private readonly ITaskRepository _taskRepository;
    private readonly ITaskCacheInvalidator _taskCacheInvalidator;

    public UpdateTaskStatusCommandHandler(
        ITaskRepository taskRepository,
        ITaskCacheInvalidator taskCacheInvalidator)
    {
        _taskRepository = taskRepository;
        _taskCacheInvalidator = taskCacheInvalidator;
    }

    public async Task<Result> Handle(UpdateTaskStatusCommand request, CancellationToken cancellationToken)
    {
        var task = await _taskRepository.GetByIdAsync(request.UserId, request.TaskId, cancellationToken);
        if (task is null)
        {
            return Result.NotFound(
                ErrorCodes.TaskNotFound,
                "Task was not found.",
                resource: "task",
                id: request.TaskId.ToString("D"));
        }

        if (!task.TryChangeStatusTo(request.Status, out var transitionFailure))
        {
            return Result.Conflict(
                ErrorCodes.TaskStatusTransitionInvalid,
                transitionFailure,
                resource: "task",
                id: request.TaskId.ToString("D"));
        }

        await _taskRepository.UpdateAsync(task, cancellationToken);
        await _taskCacheInvalidator.InvalidateAsync(request.UserId, request.TaskId, cancellationToken);
        return Result.Success();
    }
}
