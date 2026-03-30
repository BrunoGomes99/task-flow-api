using MongoDB.Driver;
using TaskFlow.Infrastructure.Configuration;
using TaskFlow.Infrastructure.Persistence.Documents;

namespace TaskFlow.Infrastructure.Persistence;

public sealed class TaskFlowMongoContext
{
    private readonly IMongoDatabase _database;
    private readonly MongoDbSettings _settings;

    public TaskFlowMongoContext(MongoDbSettings settings)
        : this(new MongoClient(settings.ConnectionString), settings)
    {
    }

    public TaskFlowMongoContext(IMongoClient client, MongoDbSettings settings)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(settings);

        if (string.IsNullOrWhiteSpace(settings.ConnectionString))
            throw new ArgumentException("MongoDB connection string is required.", nameof(settings));

        if (string.IsNullOrWhiteSpace(settings.DatabaseName))
            throw new ArgumentException("MongoDB database name is required.", nameof(settings));

        _settings = settings;
        _database = client.GetDatabase(settings.DatabaseName);
    }

    public IMongoCollection<UserDocument> Users => _database.GetCollection<UserDocument>(_settings.UsersCollectionName);

    public IMongoCollection<TaskDocument> Tasks => _database.GetCollection<TaskDocument>(_settings.TasksCollectionName);
}
