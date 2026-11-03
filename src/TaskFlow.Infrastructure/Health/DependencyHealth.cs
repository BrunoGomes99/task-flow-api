using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace TaskFlow.Infrastructure.Health;

public static class DependencyHealth
{
    public static HealthCheckResult Redis(Exception? error)
    {
        return error is null
            ? HealthCheckResult.Healthy("redis")
            : HealthCheckResult.Degraded("redis", error);
    }

    public static HealthCheckResult Required(Exception? error, string name)
    {
        return error is null
            ? HealthCheckResult.Healthy(name)
            : HealthCheckResult.Unhealthy(name, error);
    }
}
