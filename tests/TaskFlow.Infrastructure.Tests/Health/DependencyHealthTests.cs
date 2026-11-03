using Microsoft.Extensions.Diagnostics.HealthChecks;
using TaskFlow.Infrastructure.Health;

namespace TaskFlow.Infrastructure.Tests.Health;

public sealed class DependencyHealthTests
{
    [Fact]
    public void Redis_WithException_ReturnsDegraded()
    {
        var result = DependencyHealth.Redis(new InvalidOperationException("redis down"));

        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    [Fact]
    public void Redis_WithNull_ReturnsHealthy()
    {
        var result = DependencyHealth.Redis(null);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public void Required_WithException_ReturnsUnhealthy()
    {
        var result = DependencyHealth.Required(new InvalidOperationException("dependency down"), "mongodb");

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }
}
