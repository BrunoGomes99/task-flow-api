using TaskFlow.Application.Enums;
using TaskFlow.Application.Interfaces;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using DomainTaskStatus = TaskFlow.Domain.Enums.TaskStatus;

namespace TaskFlow.Application.UseCases.Reminders.DeliverDueSoonReminder;

/// <summary>
/// Decides whether a delivered <see cref="DueSoonMessage"/> is finished, and sends the email when it is not.
/// </summary>
public sealed class DueSoonReminderDelivery
{
    private readonly ITaskReminderRepository _tasks;
    private readonly INotificationLogRepository _notificationLogs;
    private readonly IUserRepository _users;
    private readonly IEmailSender _email;

    public DueSoonReminderDelivery(
        ITaskReminderRepository tasks,
        INotificationLogRepository notificationLogs,
        IUserRepository users,
        IEmailSender email)
    {
        _tasks = tasks;
        _notificationLogs = notificationLogs;
        _users = users;
        _email = email;
    }

    public async Task<DueSoonDeliveryOutcome> DeliverAsync(
        DueSoonMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var due = DueInstant.NormalizeUtc(message.DueDate);
        if (await _notificationLogs.ExistsAsync(message.TaskId, NotificationEventType.DueSoon, due, cancellationToken))
            return DueSoonDeliveryOutcome.Acknowledge;

        var task = await _tasks.FindByIdAsync(message.TaskId, cancellationToken);
        if (!CanRemind(task, message.UserId, due))
            return DueSoonDeliveryOutcome.Acknowledge;

        var user = await _users.GetByIdAsync(message.UserId, cancellationToken);
        if (user is null || string.IsNullOrWhiteSpace(user.Email.Value))
            return DueSoonDeliveryOutcome.Requeue;

        var sent = await _email.SendAsync(
            DueReminderEmail.Compose(user.Email.Value, task!.Title, due),
            cancellationToken);

        if (!sent)
            return DueSoonDeliveryOutcome.Requeue;

        await _notificationLogs.InsertAsync(
            new NotificationLog(message.TaskId, NotificationEventType.DueSoon, due),
            cancellationToken);

        return DueSoonDeliveryOutcome.Acknowledge;
    }

    private static bool CanRemind(Domain.Entities.Task? task, Guid userId, DateTime due)
    {
        if (task is null || task.UserId != userId)
            return false;

        if (task.Status == DomainTaskStatus.Completed || task.DueDate is null)
            return false;

        return DueInstant.NormalizeUtc(task.DueDate.Value) == due;
    }
}
