using MediatR;
using TaskFlow.Application.Common;
using TaskFlow.Application.Common.Results;
using TaskFlow.Application.DTOs;
using TaskFlow.Application.Interfaces;

namespace TaskFlow.Application.UseCases.Tasks.ListTasks;

/// <summary>
/// Handles paginated task listing for the authenticated user.
/// </summary>
public sealed class ListTasksQueryHandler : IRequestHandler<ListTasksQuery, Result<PagedResult<TaskDto>>>
{
    private readonly ITaskRepository _taskRepository;

    public ListTasksQueryHandler(ITaskRepository taskRepository)
    {
        _taskRepository = taskRepository;
    }

    public async Task<Result<PagedResult<TaskDto>>> Handle(ListTasksQuery request, CancellationToken cancellationToken)
    {
        var paged = await _taskRepository.GetPagedAsync(
            request.UserId,
            request.PageNumber,
            request.PageSize,
            request.TitleContains,
            request.DescriptionContains,
            request.Status,
            request.DueDateOrder,
            cancellationToken);

        var items = paged.Items.Select(TaskDto.FromDomain).ToList();
        return Result<PagedResult<TaskDto>>.Ok(new PagedResult<TaskDto>(items, paged.PageNumber, paged.PageSize, paged.TotalCount));
    }
}
