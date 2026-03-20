using MediatR;
using TaskFlow.Application.Interfaces;
using DomainTask = TaskFlow.Domain.Entities.Task;

namespace TaskFlow.Application.UseCases.Tasks.CreateTask;

/// <summary>
/// Handles task creation.
/// </summary>
public sealed class CreateTaskCommandHandler : IRequestHandler<CreateTaskCommand, CreateTaskResult>
{
    private readonly ITaskRepository _taskRepository;

    public CreateTaskCommandHandler(ITaskRepository taskRepository)
    {
        _taskRepository = taskRepository;
    }

    public async Task<CreateTaskResult> Handle(CreateTaskCommand request, CancellationToken cancellationToken)
    {
        var task = new DomainTask(
            request.UserId,
            request.Title,
            request.Description,
            request.Status,
            request.DueDate);

        await _taskRepository.AddAsync(task, cancellationToken);
        return new CreateTaskResult(task.Id);
    }
}
