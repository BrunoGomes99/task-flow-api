using Testcontainers.MongoDb;

namespace TaskFlow.Infrastructure.Tests.Support;

/// <summary>
/// Shared MongoDB container for Infrastructure integration tests (one container per test collection).
/// Image tag matches local docker-compose (<c>mongo:8.0.30</c>).
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
        // 8.0.30 exits when uname reports 6.19 through 7.0.13 (SERVER-121912).
        // GitHub-hosted Ubuntu labels the fixed 7.0.14 kernel as 7.0.0, so the check refuses a good kernel.
        // Letting glibc register rseq first turns off TCMalloc's per-CPU caches and skips that exit.
        _container = new MongoDbBuilder("mongo:8.0.30")
            .WithEnvironment("GLIBC_TUNABLES", "glibc.pthread.rseq=1")
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
