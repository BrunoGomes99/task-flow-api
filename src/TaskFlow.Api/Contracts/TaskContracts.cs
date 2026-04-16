using TaskFlow.Application.Enums;
using DomainTaskStatus = TaskFlow.Domain.Enums.TaskStatus;

namespace TaskFlow.Api.Contracts;

public sealed record CreateTaskRequest(
    string Title,
    string? Description,
    DomainTaskStatus Status = DomainTaskStatus.Pending,
    DateTime? DueDate = null);

public sealed record UpdateTaskRequest(string Title, string? Description);

public sealed record UpdateTaskStatusRequest(DomainTaskStatus Status);

public sealed class ListTasksQueryParameters
{
    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 20;

    public string? TitleContains { get; set; }

    public string? DescriptionContains { get; set; }

    public DomainTaskStatus? Status { get; set; }

    public DueDateOrder DueDateOrder { get; set; } = DueDateOrder.Ascending;
}
