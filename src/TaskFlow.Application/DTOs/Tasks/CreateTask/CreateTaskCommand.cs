using MediatR;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.DTOs.Tasks.CreateTask;

/// <summary>
/// Command to create a new task. UserId is set by the API from JWT.
/// </summary>
public sealed record CreateTaskCommand(
    Guid UserId,
    string Title,
    string? Description,
    TaskFlow.Domain.Enums.TaskStatus Status = TaskFlow.Domain.Enums.TaskStatus.Pending,
    DateTime? DueDate = null) : IRequest<CreateTaskResult>;

/// <summary>
/// Result of creating a task; returns the new task id.
/// </summary>
public sealed record CreateTaskResult(Guid Id);
