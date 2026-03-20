using MediatR;
using TaskFlow.Application.DTOs;
using TaskFlow.Application.Interfaces;

namespace TaskFlow.Application.UseCases.Tasks.GetTaskById;

/// <summary>
/// Handles retrieving a single task by id for the authenticated user.
/// </summary>
public sealed class GetTaskByIdQueryHandler : IRequestHandler<GetTaskByIdQuery, TaskDto?>
{
    private readonly ITaskRepository _taskRepository;

    public GetTaskByIdQueryHandler(ITaskRepository taskRepository)
    {
        _taskRepository = taskRepository;
    }

    public async Task<TaskDto?> Handle(GetTaskByIdQuery request, CancellationToken cancellationToken)
    {
        var task = await _taskRepository.GetByIdAsync(request.UserId, request.TaskId, cancellationToken);
        return task is null ? null : TaskDto.FromDomain(task);
    }
}
