using Microsoft.Extensions.Logging;
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
    private readonly ILogger<DueSoonReminderDelivery> _logger;

    public DueSoonReminderDelivery(
        ITaskReminderRepository tasks,
        INotificationLogRepository notificationLogs,
        IUserRepository users,
        IEmailSender email,
        ILogger<DueSoonReminderDelivery> logger)
    {
        _tasks = tasks;
        _notificationLogs = notificationLogs;
        _users = users;
        _email = email;
        _logger = logger;
    }

    public async Task<DueSoonDeliveryResult> DeliverAsync(
        DueSoonMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var due = DueInstant.NormalizeUtc(message.DueDate);
        if (await _notificationLogs.ExistsAsync(message.TaskId, NotificationEventType.DueSoon, due, cancellationToken))
            return Finish(DueSoonDeliveryOutcome.Acknowledge, DueSoonDeliveryReasons.AlreadySent);

        var task = await _tasks.FindByIdAsync(message.TaskId, cancellationToken);
        if (!CanRemind(task, message.UserId, due))
            return Finish(DueSoonDeliveryOutcome.Acknowledge, DueSoonDeliveryReasons.Skipped);

        var user = await _users.GetByIdAsync(message.UserId, cancellationToken);
        if (user is null || string.IsNullOrWhiteSpace(user.Email.Value))
            return Finish(DueSoonDeliveryOutcome.Requeue, DueSoonDeliveryReasons.Requeue);

        var sent = await _email.SendAsync(
            DueReminderEmail.Compose(user.Email.Value, task!.Title, due),
            cancellationToken);

        if (!sent)
            return Finish(DueSoonDeliveryOutcome.Requeue, DueSoonDeliveryReasons.Requeue);

        await _notificationLogs.InsertAsync(
            new NotificationLog(message.TaskId, NotificationEventType.DueSoon, due),
            cancellationToken);

        return Finish(DueSoonDeliveryOutcome.Acknowledge, DueSoonDeliveryReasons.Sent);

        DueSoonDeliveryResult Finish(DueSoonDeliveryOutcome outcome, string reason)
        {
            _logger.LogInformation(
                "DueSoon delivery for task {TaskId} due {DueDate} finished with {Result}",
                message.TaskId,
                due,
                reason);
            return new DueSoonDeliveryResult(outcome, reason);
        }
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
