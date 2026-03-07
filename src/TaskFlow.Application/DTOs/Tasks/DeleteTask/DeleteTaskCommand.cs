using MediatR;

namespace TaskFlow.Application.DTOs.Tasks.DeleteTask;

/// <summary>
/// Command to delete a task. UserId is set by the API from JWT.
/// </summary>
public sealed record DeleteTaskCommand(Guid UserId, Guid TaskId) : IRequest<bool>;
