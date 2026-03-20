using MediatR;
using TaskFlow.Application.Common.Exceptions;
using TaskFlow.Application.Interfaces;

namespace TaskFlow.Application.UseCases.Tasks.UpdateTask;

/// <summary>
/// Handles updating task title and description.
/// </summary>
public sealed class UpdateTaskCommandHandler : IRequestHandler<UpdateTaskCommand>
{
    private readonly ITaskRepository _taskRepository;

    public UpdateTaskCommandHandler(ITaskRepository taskRepository)
    {
        _taskRepository = taskRepository;
    }

    public async Task Handle(UpdateTaskCommand request, CancellationToken cancellationToken)
    {
        var task = await _taskRepository.GetByIdAsync(request.UserId, request.TaskId, cancellationToken);
        if (task is null)
            throw new NotFoundException("Task", request.TaskId);

        task.Update(request.Title, request.Description);
        await _taskRepository.UpdateAsync(task, cancellationToken);
    }
}
