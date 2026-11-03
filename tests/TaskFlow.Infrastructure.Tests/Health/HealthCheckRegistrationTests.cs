using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using TaskFlow.Infrastructure.Extensions;
using TaskFlow.Infrastructure.Health;

namespace TaskFlow.Infrastructure.Tests.Health;

public sealed class HealthCheckRegistrationTests
{
    [Fact]
    public void Api_RegistersMongoAndRedis_AndRedisFailureIsDegraded()
    {
        var registrations = RegistrationsFor(TaskFlowHealthRole.Api);

        Assert.Contains(registrations, registration => registration.Name == "mongodb");
        Assert.DoesNotContain(registrations, registration => registration.Name == "rabbitmq");

        var redis = Assert.Single(registrations, registration => registration.Name == "redis");
        Assert.Equal(HealthStatus.Degraded, redis.FailureStatus);
    }

    [Theory]
    [InlineData(TaskFlowHealthRole.Scheduler)]
    [InlineData(TaskFlowHealthRole.Notifications)]
    public void Workers_RegisterMongoAndRabbitMq_AndNotRedis(TaskFlowHealthRole role)
    {
        var registrations = RegistrationsFor(role);

        Assert.Contains(registrations, registration => registration.Name == "mongodb");
        Assert.DoesNotContain(registrations, registration => registration.Name == "redis");

        var rabbit = Assert.Single(registrations, registration => registration.Name == "rabbitmq");
        Assert.Equal(HealthStatus.Unhealthy, rabbit.FailureStatus);
    }

    private static ICollection<HealthCheckRegistration> RegistrationsFor(TaskFlowHealthRole role)
    {
        var services = new ServiceCollection();
        services.AddTaskFlowHealthChecks(role);

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;
    }
}
