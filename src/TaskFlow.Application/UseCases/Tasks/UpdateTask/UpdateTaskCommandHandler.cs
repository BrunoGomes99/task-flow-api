using MediatR;
using TaskFlow.Application.Common.Results;
using TaskFlow.Application.Interfaces;

namespace TaskFlow.Application.UseCases.Tasks.UpdateTask;

/// <summary>
/// Handles updating task title and description.
/// </summary>
public sealed class UpdateTaskCommandHandler : IRequestHandler<UpdateTaskCommand, Result>
{
    private readonly ITaskRepository _taskRepository;
    private readonly ITaskCacheInvalidator _taskCacheInvalidator;

    public UpdateTaskCommandHandler(ITaskRepository taskRepository, ITaskCacheInvalidator taskCacheInvalidator)
    {
        _taskRepository = taskRepository;
        _taskCacheInvalidator = taskCacheInvalidator;
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

        task.Update(request.Title, request.Description);
        await _taskRepository.UpdateAsync(task, cancellationToken);
        await _taskCacheInvalidator.InvalidateAsync(request.UserId, request.TaskId, cancellationToken);
        return Result.Success();
    }
}
