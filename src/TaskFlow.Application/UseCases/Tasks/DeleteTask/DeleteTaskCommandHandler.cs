using MediatR;
using TaskFlow.Application.Common.Results;
using TaskFlow.Application.Interfaces;

namespace TaskFlow.Application.UseCases.Tasks.DeleteTask;

/// <summary>
/// Handles task deletion for the authenticated user.
/// </summary>
public sealed class DeleteTaskCommandHandler : IRequestHandler<DeleteTaskCommand, Result>
{
    private readonly ITaskRepository _taskRepository;

    public DeleteTaskCommandHandler(ITaskRepository taskRepository)
    {
        _taskRepository = taskRepository;
    }

    public async Task<Result> Handle(DeleteTaskCommand request, CancellationToken cancellationToken)
    {
        var deleted = await _taskRepository.DeleteAsync(request.UserId, request.TaskId, cancellationToken);
        if (!deleted)
        {
            return Result.NotFound(
                ErrorCodes.TaskNotFound,
                "Task was not found.",
                resource: "task",
                id: request.TaskId.ToString("D"));
        }

        return Result.Success();
    }
}
