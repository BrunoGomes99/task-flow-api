using MongoDB.Driver;
using TaskFlow.Application.Interfaces;
using TaskFlow.Application.UseCases.Reminders;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Persistence;
using TaskFlow.Infrastructure.Persistence.Documents;
using TaskFlow.Infrastructure.Persistence.Mappers;

namespace TaskFlow.Infrastructure.Repositories;

public sealed class NotificationLogRepository : INotificationLogRepository
{
    private readonly IMongoCollection<NotificationLogDocument> _notificationLogs;

    public NotificationLogRepository(TaskFlowMongoContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _notificationLogs = context.NotificationLogs;
    }

    public async Task<bool> ExistsAsync(
        Guid taskId,
        NotificationEventType eventType,
        DateTime dueDate,
        CancellationToken cancellationToken = default)
    {
        var due = DueInstant.NormalizeUtc(dueDate);
        var count = await _notificationLogs.CountDocumentsAsync(
            x => x.TaskId == taskId && x.EventType == eventType && x.DueDate == due,
            cancellationToken: cancellationToken);

        return count > 0;
    }

    public async System.Threading.Tasks.Task InsertAsync(
        NotificationLog log,
        CancellationToken cancellationToken = default)
    {  
        try
        {
            await _notificationLogs.InsertOneAsync(NotificationLogMapper.ToDocument(log), cancellationToken: cancellationToken);
        }
        catch (MongoWriteException)
        {
            // A duplicate receipt or another rejected write must not throw, or the consumer would requeue and send e-mail again.
        }
    }
}
