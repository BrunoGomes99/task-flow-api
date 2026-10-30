using Microsoft.Extensions.Diagnostics.HealthChecks;
using TaskFlow.Infrastructure.Configuration;
using TaskFlow.Infrastructure.Messaging;

namespace TaskFlow.Infrastructure.Health;

internal sealed class RabbitMqHealthCheck : IHealthCheck
{
    private readonly RabbitMqSettings _settings;

    public RabbitMqHealthCheck(RabbitMqSettings settings)
    {
        _settings = settings;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var factory = RabbitMqConnectionFactory.Create(_settings);
            await using var connection = await factory.CreateConnectionAsync(cancellationToken);
            return DependencyHealth.Required(null, "rabbitmq");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return DependencyHealth.Required(exception, "rabbitmq");
        }
    }
}
