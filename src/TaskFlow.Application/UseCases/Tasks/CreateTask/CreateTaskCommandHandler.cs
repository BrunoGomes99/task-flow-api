using MediatR;
using TaskFlow.Application.Common.Results;
using TaskFlow.Application.Interfaces;
using DomainTask = TaskFlow.Domain.Entities.Task;
using DomainTaskStatus = TaskFlow.Domain.Enums.TaskStatus;

namespace TaskFlow.Application.UseCases.Tasks.CreateTask;

/// <summary>
/// Handles task creation.
/// </summary>
public sealed class CreateTaskCommandHandler : IRequestHandler<CreateTaskCommand, Result<CreateTaskResult>>
{
    private readonly ITaskRepository _taskRepository;

    public CreateTaskCommandHandler(ITaskRepository taskRepository)
    {
        _taskRepository = taskRepository;
    }

    public async Task<Result<CreateTaskResult>> Handle(CreateTaskCommand request, CancellationToken cancellationToken)
    {
        var task = new DomainTask(
            request.UserId,
            request.Title,
            request.Description,
            DomainTaskStatus.Pending,
            request.DueDate);

        await _taskRepository.AddAsync(task, cancellationToken);
        return Result<CreateTaskResult>.Ok(new CreateTaskResult(task.Id));
    }
}
