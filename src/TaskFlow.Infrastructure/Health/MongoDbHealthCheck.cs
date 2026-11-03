using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;
using TaskFlow.Infrastructure.Configuration;

namespace TaskFlow.Infrastructure.Health;

internal sealed class MongoDbHealthCheck : IHealthCheck
{
    private readonly IMongoClient _client;
    private readonly MongoDbSettings _settings;

    public MongoDbHealthCheck(IMongoClient client, MongoDbSettings settings)
    {
        _client = client;
        _settings = settings;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _client
                .GetDatabase(_settings.DatabaseName)
                .RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: cancellationToken);

            return DependencyHealth.Required(null, "mongodb");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return DependencyHealth.Required(exception, "mongodb");
        }
    }
}
