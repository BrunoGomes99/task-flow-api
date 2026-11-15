using MediatR;
using TaskFlow.Application.Common.Results;
using TaskFlow.Application.Interfaces;
using TaskFlow.Domain.Time;

namespace TaskFlow.Application.UseCases.Tasks.UpdateTask;

/// <summary>
/// Handles replacing task title, description and due date.
/// A completed task is locked. A past due date is rejected only when it differs from the stored one.
/// </summary>
public sealed class UpdateTaskCommandHandler : IRequestHandler<UpdateTaskCommand, Result>
{
    private readonly ITaskRepository _taskRepository;
    private readonly ITaskCacheInvalidator _taskCacheInvalidator;
    private readonly TimeProvider _time;

    public UpdateTaskCommandHandler(
        ITaskRepository taskRepository,
        ITaskCacheInvalidator taskCacheInvalidator,
        TimeProvider time)
    {
        _taskRepository = taskRepository;
        _taskCacheInvalidator = taskCacheInvalidator;
        _time = time;
    }

    public async Task<Result> Handle(UpdateTaskCommand request, CancellationToken cancellationToken)
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

        if (!task.CanBeEdited())
        {
            return Result.Conflict(
                ErrorCodes.TaskLocked,
                "Task is completed and can no longer be edited.",
                resource: "task",
                id: request.TaskId.ToString("D"));
        }

        var newDueDate = UtcInstant.Normalize(request.DueDate);
        if (newDueDate != task.DueDate && newDueDate <= _time.GetUtcNow().UtcDateTime)
        {
            return Result.BadRequest(
                ErrorCodes.TaskDueDateInPast,
                "Due date cannot be in the past.",
                resource: "task",
                id: request.TaskId.ToString("D"));
        }

        task.Update(request.Title, request.Description, newDueDate);
        await _taskRepository.UpdateAsync(task, cancellationToken);
        await _taskCacheInvalidator.InvalidateAsync(request.UserId, request.TaskId, cancellationToken);
        return Result.Success();
    }
}
