using TaskFlow.Application.Interfaces;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.UseCases.Reminders.PublishDueSoonReminders;

/// <summary>
/// Selects tasks due in the next 23 to 24 hours and publishes one <see cref="DueSoonMessage"/> each.
/// </summary>
public sealed class DueSoonReminderPublish
{
    private readonly ITaskReminderRepository _tasks;
    private readonly INotificationLogRepository _notificationLogs;
    private readonly IDueSoonPublisher _publisher;
    private readonly TimeProvider _time;

    public DueSoonReminderPublish(
        ITaskReminderRepository tasks,
        INotificationLogRepository notificationLogs,
        IDueSoonPublisher publisher,
        TimeProvider time)
    {
        _tasks = tasks;
        _notificationLogs = notificationLogs;
        _publisher = publisher;
        _time = time;
    }

    public async Task<DueSoonPublishSummary> PublishAsync(CancellationToken cancellationToken = default)
    {
        var now = DueInstant.NormalizeUtc(_time.GetUtcNow().UtcDateTime);
        var windowStart = now.AddHours(23);
        var windowEnd = now.AddHours(24);
        var published = 0;
        var skipped = 0;

        var candidates = await _tasks.ListInWindowAsync(windowStart, windowEnd, cancellationToken);
        foreach (var candidate in candidates)
        {
            var due = DueInstant.NormalizeUtc(candidate.DueDate!.Value);
            var alreadySent = await _notificationLogs.ExistsAsync(
                candidate.Id,
                NotificationEventType.DueSoon,
                due,
                cancellationToken);

            if (alreadySent)
            {
                skipped++;
                continue;
            }

            await _publisher.PublishAsync(
                new DueSoonMessage(candidate.Id, candidate.UserId, due),
                cancellationToken);
            published++;
        }

        return new DueSoonPublishSummary(published, skipped);
    }
}
