using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mongo2Go;
using TaskFlow.Application.Interfaces;
using TaskFlow.Infrastructure.Configuration;
using TaskFlow.Infrastructure.Extensions;
using TaskFlow.Infrastructure.Persistence;
using TaskFlow.Infrastructure.Repositories;

namespace TaskFlow.Infrastructure.Tests.Configuration;

public sealed class InfrastructureRegistrationTests
{
    [Fact]
    public void AddInfrastructure_ShouldRegisterMongoContextRepositoriesAndSettings()
    {
        using var runner = MongoDbRunner.Start();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{MongoDbSettings.SectionName}:ConnectionString"] = runner.ConnectionString,
                [$"{MongoDbSettings.SectionName}:DatabaseName"] = $"taskflow-di-{Guid.NewGuid():N}"
            })
            .Build();

        var services = new ServiceCollection();

        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<TaskFlowMongoContext>());
        Assert.IsType<UserRepository>(provider.GetRequiredService<IUserRepository>());
        Assert.IsType<TaskRepository>(provider.GetRequiredService<ITaskRepository>());
        Assert.NotNull(provider.GetRequiredService<MongoIndexesInitializer>());
    }
}
