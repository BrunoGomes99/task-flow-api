using MongoDB.Driver;
using TaskFlow.Application.Interfaces;
using TaskFlow.Application.UseCases.Reminders;
using TaskFlow.Infrastructure.Persistence;
using TaskFlow.Infrastructure.Persistence.Documents;
using TaskFlow.Infrastructure.Persistence.Mappers;
using DomainTask = TaskFlow.Domain.Entities.Task;
using DomainTaskStatus = TaskFlow.Domain.Enums.TaskStatus;

namespace TaskFlow.Infrastructure.Repositories;

public sealed class TaskReminderRepository : ITaskReminderRepository
{
    private readonly IMongoCollection<TaskDocument> _tasks;

    public TaskReminderRepository(TaskFlowMongoContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _tasks = context.Tasks;
    }

    public async Task<IReadOnlyList<DomainTask>> ListInWindowAsync(
        DateTime windowStartInclusiveUtc,
        DateTime windowEndExclusiveUtc,
        CancellationToken cancellationToken = default)
    {
        var start = DueInstant.NormalizeUtc(windowStartInclusiveUtc);
        var end = DueInstant.NormalizeUtc(windowEndExclusiveUtc);
        var filter = Builders<TaskDocument>.Filter.And(
            Builders<TaskDocument>.Filter.Ne(x => x.DueDate, null),
            Builders<TaskDocument>.Filter.Gte(x => x.DueDate, start),
            Builders<TaskDocument>.Filter.Lt(x => x.DueDate, end),
            Builders<TaskDocument>.Filter.Ne(x => x.Status, DomainTaskStatus.Completed));

        var documents = await _tasks.Find(filter).ToListAsync(cancellationToken);
        return documents
            .Where(document => document.DueDate is not null)
            .Select(TaskDocumentMapper.ToDomain)
            .ToArray();
    }

    public async Task<DomainTask?> FindByIdAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        var document = await _tasks.Find(x => x.Id == taskId).FirstOrDefaultAsync(cancellationToken);
        return document is null ? null : TaskDocumentMapper.ToDomain(document);
    }
}
