using MediatR;

namespace TaskFlow.Application.UseCases.Tasks.UpdateTask;

/// <summary>
/// Command to update a task's title and description. UserId is set by the API from JWT.
/// </summary>
public sealed record UpdateTaskCommand(
    Guid UserId,
    Guid TaskId,
    string Title,
    string? Description) : IRequest;
