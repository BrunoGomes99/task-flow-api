using MediatR;
using TaskFlow.Application.Common.Exceptions;
using TaskFlow.Application.Interfaces;
using TaskFlow.Domain.Exceptions;
using DomainTaskStatus = TaskFlow.Domain.Enums.TaskStatus;

namespace TaskFlow.Application.UseCases.Tasks.UpdateTaskStatus;

/// <summary>
/// Handles task status transitions using domain methods.
/// Invalid transitions throw <see cref="TaskStatusTransitionException"/> and are mapped to HTTP 409 in the API.
/// </summary>
public sealed class UpdateTaskStatusCommandHandler : IRequestHandler<UpdateTaskStatusCommand>
{
    private readonly ITaskRepository _taskRepository;

    public UpdateTaskStatusCommandHandler(ITaskRepository taskRepository)
    {
        _taskRepository = taskRepository;
    }

    public async Task Handle(UpdateTaskStatusCommand request, CancellationToken cancellationToken)
    {
        var task = await _taskRepository.GetByIdAsync(request.UserId, request.TaskId, cancellationToken);
        if (task is null)
            throw new NotFoundException("Task", request.TaskId);

        switch (request.Status)
        {
            case DomainTaskStatus.Pending:
                task.SetPending();
                break;
            case DomainTaskStatus.InProgress:
                task.SetInProgress();
                break;
            case DomainTaskStatus.Completed:
                task.SetCompleted();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(request.Status), request.Status, "Unsupported task status.");
        }

        await _taskRepository.UpdateAsync(task, cancellationToken);
    }
}
