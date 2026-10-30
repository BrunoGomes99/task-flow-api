using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TaskFlow.Infrastructure.Health;

namespace TaskFlow.Infrastructure.Extensions;

public static class HealthCheckServiceCollectionExtensions
{
    public static IServiceCollection AddTaskFlowHealthChecks(this IServiceCollection services, TaskFlowHealthRole role)
    {
        ArgumentNullException.ThrowIfNull(services);

        var checks = services.AddHealthChecks()
            .AddCheck<MongoDbHealthCheck>("mongodb");

        if (role == TaskFlowHealthRole.Api)
        {
            checks.AddCheck<RedisHealthCheck>("redis", failureStatus: HealthStatus.Degraded);
        }

        if (role is TaskFlowHealthRole.Scheduler or TaskFlowHealthRole.Notifications)
        {
            checks.AddCheck<RabbitMqHealthCheck>("rabbitmq");
        }

        return services;
    }
}
