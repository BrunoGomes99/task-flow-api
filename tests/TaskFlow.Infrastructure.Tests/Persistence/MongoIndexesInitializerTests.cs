using MongoDB.Driver;
using TaskFlow.Infrastructure.Configuration;
using TaskFlow.Infrastructure.Persistence;
using TaskFlow.Infrastructure.Tests.Support;

namespace TaskFlow.Infrastructure.Tests.Persistence;

[Collection(MongoDbContainerFixture.CollectionName)]
public sealed class MongoIndexesInitializerTests
{
    private readonly MongoDbContainerFixture _mongoDb;

    public MongoIndexesInitializerTests(MongoDbContainerFixture mongoDb)
    {
        _mongoDb = mongoDb;
    }

    [Fact]
    public async System.Threading.Tasks.Task InitializeAsync_ShouldCreateUserAndTaskIndexes()
    {
        var settings = new MongoDbSettings
        {
            ConnectionString = _mongoDb.ConnectionString,
            DatabaseName = $"taskflow-indexes-{Guid.NewGuid():N}"
        };

        var context = new TaskFlowMongoContext(settings);
        var initializer = new MongoIndexesInitializer(context);

        await initializer.InitializeAsync(CancellationToken.None);

        var userIndexes = await context.Users.Indexes.ListAsync(CancellationToken.None);
        var taskIndexes = await context.Tasks.Indexes.ListAsync(CancellationToken.None);

        var userIndexNames = (await userIndexes.ToListAsync(CancellationToken.None))
            .Select(index => index["name"].AsString)
            .ToList();

        var taskIndexNames = (await taskIndexes.ToListAsync(CancellationToken.None))
            .Select(index => index["name"].AsString)
            .ToList();

        Assert.Contains("Email_1", userIndexNames);
        Assert.Contains("UserId_1", taskIndexNames);
        Assert.Contains("UserId_1_DueDate_1", taskIndexNames);
    }
}
