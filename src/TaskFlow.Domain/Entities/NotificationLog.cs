using TaskFlow.Domain.Enums;
using TaskFlow.Domain.SeedWork;

namespace TaskFlow.Domain.Entities;

/// <summary>
/// Receipt that a due-date reminder email was accepted for one task and one due instant.
/// <see cref="ProcessedAt"/> is the UTC instant this receipt was stored.
/// <see cref="DueDate"/> is the task due instant the reminder covered, not the send time.
/// </summary>
public class NotificationLog : Entity
{
    public Guid TaskId { get; private set; }
    public NotificationEventType EventType { get; private set; }
    public DateTime DueDate { get; private set; }
    public DateTime ProcessedAt { get; private set; }

    /// <param name="taskId">Related task id.</param>
    /// <param name="eventType">Event classification.</param>
    /// <param name="dueDate">UTC due instant this receipt covers.</param>
    public NotificationLog(Guid taskId, NotificationEventType eventType, DateTime dueDate)
    {
        Validate(taskId, dueDate);

        TaskId = taskId;
        EventType = eventType;
        DueDate = dueDate;
        ProcessedAt = DateTime.UtcNow;
    }

    private static void Validate(Guid taskId, DateTime dueDate)
    {
        if (taskId == Guid.Empty)
            throw new ArgumentException("TaskId cannot be empty.", nameof(taskId));

        if (dueDate == default)
            throw new ArgumentException("DueDate cannot be empty.", nameof(dueDate));

        if (dueDate.Kind != DateTimeKind.Utc)
            throw new ArgumentException("DueDate must be UTC.", nameof(dueDate));
    }
}
