using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;
using TaskFlow.Application.Common;
using TaskFlow.Application.Enums;
using TaskFlow.Application.Interfaces;
using TaskFlow.Infrastructure.Persistence;
using TaskFlow.Infrastructure.Persistence.Documents;
using TaskFlow.Infrastructure.Persistence.Mappers;
using DomainTask = TaskFlow.Domain.Entities.Task;
using DomainTaskStatus = TaskFlow.Domain.Enums.TaskStatus;

namespace TaskFlow.Infrastructure.Repositories;

public sealed class TaskRepository : ITaskRepository
{
    private readonly IMongoCollection<TaskDocument> _tasks;

    public TaskRepository(TaskFlowMongoContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _tasks = context.Tasks;
    }

    public async Task<DomainTask?> GetByIdAsync(Guid userId, Guid taskId, CancellationToken cancellationToken = default)
    {
        var document = await _tasks.Find(x => x.UserId == userId && x.Id == taskId).FirstOrDefaultAsync(cancellationToken);
        return document is null ? null : TaskDocumentMapper.ToDomain(document);
    }

    public async Task AddAsync(DomainTask task, CancellationToken cancellationToken = default)
    {
        await _tasks.InsertOneAsync(TaskDocumentMapper.ToDocument(task), cancellationToken: cancellationToken);
    }

    public async Task UpdateAsync(DomainTask task, CancellationToken cancellationToken = default)
    {
        var filter = Builders<TaskDocument>.Filter.Where(x => x.Id == task.Id && x.UserId == task.UserId);
        await _tasks.ReplaceOneAsync(filter, TaskDocumentMapper.ToDocument(task), cancellationToken: cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid taskId, CancellationToken cancellationToken = default)
    {
        var result = await _tasks.DeleteOneAsync(x => x.UserId == userId && x.Id == taskId, cancellationToken);
        return result.DeletedCount > 0;
    }

    public async Task<PagedResult<DomainTask>> GetPagedAsync(
        Guid userId,
        int pageNumber,
        int pageSize,
        string? titleContains,
        string? descriptionContains,
        DomainTaskStatus? status,
        DueDateOrder dueDateOrder,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<TaskDocument>.Filter;
        var filters = new List<FilterDefinition<TaskDocument>>
        {
            builder.Eq(x => x.UserId, userId)
        };

        if (!string.IsNullOrWhiteSpace(titleContains))
            filters.Add(builder.Regex(x => x.Title, ContainsInsensitive(titleContains)));

        if (!string.IsNullOrWhiteSpace(descriptionContains))
            filters.Add(builder.Regex(x => x.Description, ContainsInsensitive(descriptionContains)));

        if (status.HasValue)
            filters.Add(builder.Eq(x => x.Status, status.Value));

        var filter = builder.And(filters);
        var dueDateDirection = dueDateOrder == DueDateOrder.Descending ? -1 : 1;
        var sort = new BsonDocument
        {
            { "hasDueDate", -1 },
            { "DueDate", dueDateDirection },
            { "CreatedAt", -1 },
            { "_id", 1 }
        };

        var totalCount = (int)await _tasks.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var documents = await _tasks
            .Aggregate()
            .Match(filter)
            .AppendStage<BsonDocument>(new BsonDocument("$addFields", new BsonDocument(
                "hasDueDate",
                new BsonDocument("$cond", new BsonArray
                {
                    new BsonDocument("$ne", new BsonArray { "$DueDate", BsonNull.Value }),
                    1,
                    0
                }))))
            .Sort(sort)
            .AppendStage<BsonDocument>(new BsonDocument("$unset", "hasDueDate"))
            .Skip((pageNumber - 1) * pageSize)
            .Limit(pageSize)
            .As<TaskDocument>()
            .ToListAsync(cancellationToken);

        var items = documents.Select(TaskDocumentMapper.ToDomain).ToList();
        return new PagedResult<DomainTask>(items, pageNumber, pageSize, totalCount);
    }

    public async Task<IReadOnlyDictionary<DomainTaskStatus, int>> CountByStatusAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var groups = await _tasks
            .Aggregate()
            .Match(x => x.UserId == userId)
            .Group(x => x.Status, g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return groups.ToDictionary(g => g.Status, g => g.Count);
    }

    private static BsonRegularExpression ContainsInsensitive(string value) =>
        new(Regex.Escape(value), "i");
}
