using MediatR;
using TaskFlow.Application.Common.Results;
using TaskFlow.Application.DTOs;
using TaskFlow.Application.Interfaces;

namespace TaskFlow.Application.UseCases.Tasks.GetTaskById;

/// <summary>
/// Handles retrieving a single task by id for the authenticated user.
/// Reads the cache first. A cache miss loads MongoDB. Cache failures stay inside <see cref="ITaskCache"/>.
/// </summary>
public sealed class GetTaskByIdQueryHandler : IRequestHandler<GetTaskByIdQuery, Result<TaskDto>>
{
    private readonly ITaskRepository _taskRepository;
    private readonly ITaskCache _taskCache;

    public GetTaskByIdQueryHandler(ITaskRepository taskRepository, ITaskCache taskCache)
    {
        _taskRepository = taskRepository;
        _taskCache = taskCache;
    }

    public async Task<Result<TaskDto>> Handle(GetTaskByIdQuery request, CancellationToken cancellationToken)
    {
        var cached = await _taskCache.GetAsync(request.UserId, request.TaskId, cancellationToken);
        if (cached is not null)
            return Result<TaskDto>.Ok(cached);

        var task = await _taskRepository.GetByIdAsync(request.UserId, request.TaskId, cancellationToken);
        if (task is null)
        {
            return Result<TaskDto>.NotFound(
                ErrorCodes.TaskNotFound,
                "Task was not found.",
                resource: "task",
                id: request.TaskId.ToString("D"));
        }

        var dto = TaskDto.FromDomain(task);
        await _taskCache.SetAsync(request.UserId, request.TaskId, dto, cancellationToken);
        return Result<TaskDto>.Ok(dto);
    }
}
