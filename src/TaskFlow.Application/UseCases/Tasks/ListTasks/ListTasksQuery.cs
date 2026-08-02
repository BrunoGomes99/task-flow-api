using MediatR;
using TaskFlow.Application.Common;
using TaskFlow.Application.Common.Results;
using TaskFlow.Application.DTOs;
using TaskFlow.Application.Enums;

namespace TaskFlow.Application.UseCases.Tasks.ListTasks;

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
    DueDateOrder DueDateOrder) : IRequest<Result<PagedResult<TaskDto>>>;
