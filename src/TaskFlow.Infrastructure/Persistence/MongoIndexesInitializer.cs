using MongoDB.Driver;
using TaskFlow.Infrastructure.Persistence.Documents;

namespace TaskFlow.Infrastructure.Persistence;

public sealed class MongoIndexesInitializer
{
    private readonly TaskFlowMongoContext _context;

    public MongoIndexesInitializer(TaskFlowMongoContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var userIndexes = new[]
        {
            new CreateIndexModel<UserDocument>(
                Builders<UserDocument>.IndexKeys.Ascending(x => x.Email),
                new CreateIndexOptions
                {
                    Unique = true,
                    Name = "Email_1"
                })
        };

        var taskIndexes = new[]
        {
            new CreateIndexModel<TaskDocument>(
                Builders<TaskDocument>.IndexKeys.Ascending(x => x.UserId),
                new CreateIndexOptions
                {
                    Name = "UserId_1"
                }),
            new CreateIndexModel<TaskDocument>(
                Builders<TaskDocument>.IndexKeys
                    .Ascending(x => x.UserId)
                    .Ascending(x => x.DueDate),
                new CreateIndexOptions
                {
                    Name = "UserId_1_DueDate_1"
                })
        };

        await _context.Users.Indexes.CreateManyAsync(userIndexes, cancellationToken);
        await _context.Tasks.Indexes.CreateManyAsync(taskIndexes, cancellationToken);
    }
}
