using MediatR;
using TaskFlow.Application.Common.Results;

namespace TaskFlow.Application.UseCases.Tasks.UpdateTaskStatus;

/// <summary>
/// Command to update only the task status (PATCH). UserId is set by the API from JWT.
/// </summary>
public sealed record UpdateTaskStatusCommand(
    Guid UserId,
    Guid TaskId,
    TaskFlow.Domain.Enums.TaskStatus Status) : IRequest<Result>;
