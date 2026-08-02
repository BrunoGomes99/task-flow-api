using MediatR;
using TaskFlow.Application.Common.Results;

namespace TaskFlow.Application.UseCases.Tasks.CreateTask;

/// <summary>
/// Command to create a new task. UserId is set by the API from JWT.
/// </summary>
public sealed record CreateTaskCommand(
    Guid UserId,
    string Title,
    string? Description,
    TaskFlow.Domain.Enums.TaskStatus Status = TaskFlow.Domain.Enums.TaskStatus.Pending,
    DateTime? DueDate = null) : IRequest<Result<CreateTaskResult>>;

/// <summary>
/// Result of creating a task; returns the new task id.
/// </summary>
public sealed record CreateTaskResult(Guid Id);
