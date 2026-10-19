using Testcontainers.MongoDb;

namespace TaskFlow.Infrastructure.Tests.Support;

/// <summary>
/// Shared MongoDB container for Infrastructure integration tests (one container per test collection).
/// Image tag matches local docker-compose (<c>mongo:8.0.30</c>). 8.0.30 starts on kernels 7.0.14 and newer; the floating 8.0 tag still refuses kernels 6.19 and above.
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
            .WithImage("mongo:8.0.30")
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
