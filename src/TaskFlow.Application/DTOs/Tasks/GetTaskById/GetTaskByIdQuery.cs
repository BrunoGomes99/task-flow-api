using MediatR;

namespace TaskFlow.Application.DTOs.Tasks.GetTaskById;

/// <summary>
/// Query to get a single task by id. UserId is set by the API from JWT (multi-tenancy).
/// </summary>
public sealed record GetTaskByIdQuery(Guid UserId, Guid TaskId) : IRequest<TaskDto?>;
