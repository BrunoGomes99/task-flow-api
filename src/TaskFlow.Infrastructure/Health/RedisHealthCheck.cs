using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace TaskFlow.Infrastructure.Health;

internal sealed class RedisHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer _multiplexer;

    public RedisHealthCheck(IConnectionMultiplexer multiplexer)
    {
        _multiplexer = multiplexer;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _multiplexer.GetDatabase().PingAsync().WaitAsync(cancellationToken);
            return DependencyHealth.Redis(null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return DependencyHealth.Redis(exception);
        }
    }
}
