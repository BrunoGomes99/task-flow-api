using TaskFlow.Application.Common;
using TaskFlow.Application.Enums;
using MediatR;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.DTOs.Tasks.ListTasks;

/// <summary>
/// Query to list tasks with pagination, filters, and ordering. UserId is set by the API from JWT.
/// PageSize max 20 is enforced by validation.
/// </summary>
public sealed record ListTasksQuery(
    Guid UserId,
    int PageNumber,
    int PageSize,
    string? TitleContains,
    string? DescriptionContains,
    TaskFlow.Domain.Enums.TaskStatus? Status,
    DueDateOrder DueDateOrder) : IRequest<PagedResult<TaskDto>>;
