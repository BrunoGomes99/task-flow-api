using Testcontainers.MongoDb;

namespace TaskFlow.Infrastructure.Tests.Support;

/// <summary>
/// Shared MongoDB container for Infrastructure integration tests (one container per test collection).
/// Image tag matches local docker-compose (<c>mongo:8.0</c>).
/// </summary>
public sealed class MongoDbContainerFixture : IAsyncLifetime
{
    public const string CollectionName = "MongoDbTests";

    private MongoDbContainer? _container;

    public string ConnectionString =>
        _container?.GetConnectionString()
        ?? throw new InvalidOperationException("MongoDB container has not been started.");

    public async Task InitializeAsync()
    {
        _container = new MongoDbBuilder()
            .WithImage("mongo:8.0")
            .Build();

        await _container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}

[CollectionDefinition(MongoDbContainerFixture.CollectionName)]
public sealed class MongoDbCollection : ICollectionFixture<MongoDbContainerFixture>;
