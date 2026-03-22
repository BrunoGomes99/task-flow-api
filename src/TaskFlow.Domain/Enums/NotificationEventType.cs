namespace TaskFlow.Domain.Enums;

/// <summary>
/// Kind of notification event persisted in <see cref="Entities.NotificationLog"/>.
/// Aligns with planned messaging events (Phase 2).
/// </summary>
public enum NotificationEventType
{
    TaskCreated = 0,
    TaskCompleted = 1,
}
