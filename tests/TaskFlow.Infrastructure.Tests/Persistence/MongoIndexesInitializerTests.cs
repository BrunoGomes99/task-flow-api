using Mongo2Go;
using MongoDB.Driver;
using TaskFlow.Infrastructure.Configuration;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Infrastructure.Tests.Persistence;

public sealed class MongoIndexesInitializerTests
{
    [Fact]
    public async System.Threading.Tasks.Task InitializeAsync_ShouldCreateUserAndTaskIndexes()
    {
        using var runner = MongoDbRunner.Start();
        var settings = new MongoDbSettings
        {
            ConnectionString = runner.ConnectionString,
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
