using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.DTOs;

/// <summary>
/// Data transfer object for Task in API responses.
/// </summary>
public sealed record TaskDto(
    Guid Id,
    string Title,
    string Description,
    TaskFlow.Domain.Enums.TaskStatus Status,
    DateTime? DueDate,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    /// <summary>
    /// Maps a domain Task entity to TaskDto.
    /// </summary>
    public static TaskDto FromDomain(TaskFlow.Domain.Entities.Task task) => new(
        task.Id,
        task.Title,
        task.Description,
        task.Status,
        task.DueDate,
        task.CreatedAt,
        task.UpdatedAt);
}
