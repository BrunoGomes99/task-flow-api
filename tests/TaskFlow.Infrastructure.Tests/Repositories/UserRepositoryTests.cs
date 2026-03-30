using Mongo2Go;
using TaskFlow.Domain.ValueObjects;
using TaskFlow.Infrastructure.Configuration;
using TaskFlow.Infrastructure.Persistence;
using TaskFlow.Infrastructure.Repositories;
using DomainUser = TaskFlow.Domain.Entities.User;

namespace TaskFlow.Infrastructure.Tests.Repositories;

public sealed class UserRepositoryTests
{
    [Fact]
    public async System.Threading.Tasks.Task AddAsync_ShouldPersistAndLoadUser_ByIdAndEmail()
    {
        using var runner = MongoDbRunner.Start();
        var settings = new MongoDbSettings
        {
            ConnectionString = runner.ConnectionString,
            DatabaseName = $"taskflow-users-{Guid.NewGuid():N}"
        };

        var context = new TaskFlowMongoContext(settings);
        var repository = new UserRepository(context);
        var user = new DomainUser("Alice", "Alice@Example.com", "HASH:password12");

        await repository.AddAsync(user, CancellationToken.None);

        var byId = await repository.GetByIdAsync(user.Id, CancellationToken.None);
        var byEmail = await repository.GetByEmailAsync(Email.Create("alice@example.com"), CancellationToken.None);
        var exists = await repository.ExistsByEmailAsync(Email.Create("alice@example.com"), CancellationToken.None);

        Assert.NotNull(byId);
        Assert.Equal(user.Id, byId!.Id);
        Assert.Equal("alice@example.com", byId.Email.Value);
        Assert.NotNull(byEmail);
        Assert.Equal(user.Id, byEmail!.Id);
        Assert.True(exists);
    }
}
