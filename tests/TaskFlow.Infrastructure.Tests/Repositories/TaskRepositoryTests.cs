using Mongo2Go;
using TaskFlow.Application.Enums;
using TaskFlow.Infrastructure.Configuration;
using TaskFlow.Infrastructure.Persistence;
using TaskFlow.Infrastructure.Repositories;
using DomainTask = TaskFlow.Domain.Entities.Task;
using DomainTaskStatus = TaskFlow.Domain.Enums.TaskStatus;

namespace TaskFlow.Infrastructure.Tests.Repositories;

public sealed class TaskRepositoryTests
{
    [Fact]
    public async System.Threading.Tasks.Task GetPagedAsync_ShouldFilterOrderAndPaginateWithinUserScope()
    {
        using var runner = MongoDbRunner.Start();
        var settings = new MongoDbSettings
        {
            ConnectionString = runner.ConnectionString,
            DatabaseName = $"taskflow-tasks-{Guid.NewGuid():N}"
        };

        var context = new TaskFlowMongoContext(settings);
        var repository = new TaskRepository(context);
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        await repository.AddAsync(new DomainTask(userId, "Alpha task", "first desc", DomainTaskStatus.Pending, new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc)));
        await repository.AddAsync(new DomainTask(userId, "Beta task", "second desc", DomainTaskStatus.InProgress, new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc)));
        await repository.AddAsync(new DomainTask(userId, "Gamma", "other", DomainTaskStatus.InProgress, new DateTime(2026, 4, 30, 0, 0, 0, DateTimeKind.Utc)));
        await repository.AddAsync(new DomainTask(otherUserId, "Beta task", "second desc", DomainTaskStatus.InProgress, new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc)));

        var result = await repository.GetPagedAsync(
            userId,
            pageNumber: 1,
            pageSize: 1,
            titleContains: "task",
            descriptionContains: "desc",
            status: DomainTaskStatus.InProgress,
            dueDateOrder: DueDateOrder.Descending,
            cancellationToken: CancellationToken.None);

        Assert.Equal(1, result.PageNumber);
        Assert.Equal(1, result.PageSize);
        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal("Beta task", result.Items[0].Title);
        Assert.Equal(userId, result.Items[0].UserId);
    }

    [Fact]
    public async System.Threading.Tasks.Task DeleteAsync_ShouldRemoveOnlyOwnedTask()
    {
        using var runner = MongoDbRunner.Start();
        var settings = new MongoDbSettings
        {
            ConnectionString = runner.ConnectionString,
            DatabaseName = $"taskflow-delete-{Guid.NewGuid():N}"
        };

        var context = new TaskFlowMongoContext(settings);
        var repository = new TaskRepository(context);
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var task = new DomainTask(ownerId, "Delete me", null);

        await repository.AddAsync(task, CancellationToken.None);

        var deletedWithWrongUser = await repository.DeleteAsync(otherUserId, task.Id, CancellationToken.None);
        var deletedWithOwner = await repository.DeleteAsync(ownerId, task.Id, CancellationToken.None);
        var existing = await repository.GetByIdAsync(ownerId, task.Id, CancellationToken.None);

        Assert.False(deletedWithWrongUser);
        Assert.True(deletedWithOwner);
        Assert.Null(existing);
    }
}
