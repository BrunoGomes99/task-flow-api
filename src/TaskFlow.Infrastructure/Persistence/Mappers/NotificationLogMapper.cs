using TaskFlow.Application.UseCases.Reminders;
using TaskFlow.Domain.Entities;
using TaskFlow.Infrastructure.Persistence.Documents;

namespace TaskFlow.Infrastructure.Persistence.Mappers
{
    internal static class NotificationLogMapper
    {
        public static NotificationLogDocument ToDocument(NotificationLog notificationLog)
        {
            ArgumentNullException.ThrowIfNull(notificationLog);

            return new NotificationLogDocument
            {
                Id = notificationLog.Id,
                TaskId = notificationLog.TaskId,
                EventType = notificationLog.EventType,
                DueDate = DueInstant.NormalizeUtc(notificationLog.DueDate),
                ProcessedAt = notificationLog.ProcessedAt.Kind == DateTimeKind.Utc
                    ? notificationLog.ProcessedAt
                    : DateTime.SpecifyKind(notificationLog.ProcessedAt, DateTimeKind.Utc)
            };
        }
    }
}
