using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Interfaces;

public interface INotificationLogRepository
{
    Task<bool> ExistsAsync(
        Guid taskId,
        NotificationEventType eventType,
        DateTime dueDate,
        CancellationToken cancellationToken = default);

    System.Threading.Tasks.Task InsertAsync(
        NotificationLog log,
        CancellationToken cancellationToken = default);
}
