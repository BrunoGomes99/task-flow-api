using TaskFlow.Domain.Enums;
using TaskFlow.Domain.SeedWork;

namespace TaskFlow.Domain.Entities;

/// <summary>
/// Audit-style record for a processed notification related to a task.
/// <see cref="ProcessedAt"/> is always the UTC instant when this entry is created (processing time).
/// Phase 1: domain model only (no repository or use cases).
/// </summary>
public class NotificationLog : Entity
{
    public Guid TaskId { get; private set; }
    public NotificationEventType EventType { get; private set; }
    public DateTime ProcessedAt { get; private set; }

    /// <param name="taskId">Related task id.</param>
    /// <param name="eventType">Event classification.</param>
    public NotificationLog(Guid taskId, NotificationEventType eventType)
    {
        ValidateTaskId(taskId);

        TaskId = taskId;
        EventType = eventType;
        ProcessedAt = DateTime.UtcNow;
    }

    private static void ValidateTaskId(Guid taskId)
    {
        if (taskId == Guid.Empty)
            throw new ArgumentException("TaskId cannot be empty.", nameof(taskId));
    }
}
