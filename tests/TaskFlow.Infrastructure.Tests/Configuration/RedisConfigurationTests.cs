using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Application.Interfaces;
using TaskFlow.Infrastructure.Caching;
using TaskFlow.Infrastructure.Configuration;
using TaskFlow.Infrastructure.Extensions;

namespace TaskFlow.Infrastructure.Tests.Configuration;

public sealed class RedisConfigurationTests
{
    [Fact]
    public void AddInfrastructure_ShouldThrow_WhenRedisConnectionStringMissing()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{MongoDbSettings.SectionName}:ConnectionString"] = "mongodb://localhost:27017",
                [$"{MongoDbSettings.SectionName}:DatabaseName"] = "TaskFlowDb"
            })
            .Build();
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddInfrastructure(configuration));

        Assert.Contains("Redis:ConnectionString", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddInfrastructure_ShouldRegisterRedisTaskCache_WithoutOpeningAConnection()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{MongoDbSettings.SectionName}:ConnectionString"] = "mongodb://localhost:27017",
                [$"{MongoDbSettings.SectionName}:DatabaseName"] = "TaskFlowDb",
                [$"{RedisSettings.SectionName}:ConnectionString"] = "127.0.0.1:1,abortConnect=false"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddInfrastructure(configuration);

        var descriptor = Assert.Single(services, service => service.ServiceType == typeof(ITaskCache));
        Assert.Equal(typeof(RedisTaskCache), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }
}
