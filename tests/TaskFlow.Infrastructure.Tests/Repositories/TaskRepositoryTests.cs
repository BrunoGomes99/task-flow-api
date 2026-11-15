using TaskFlow.Application.Enums;
using TaskFlow.Infrastructure.Configuration;
using TaskFlow.Infrastructure.Persistence;
using TaskFlow.Infrastructure.Persistence.Documents;
using TaskFlow.Infrastructure.Repositories;
using TaskFlow.Infrastructure.Tests.Support;
using DomainTask = TaskFlow.Domain.Entities.Task;
using DomainTaskStatus = TaskFlow.Domain.Enums.TaskStatus;

namespace TaskFlow.Infrastructure.Tests.Repositories;

[Collection(MongoDbContainerFixture.CollectionName)]
public sealed class TaskRepositoryTests
{
    private readonly MongoDbContainerFixture _mongoDb;

    public TaskRepositoryTests(MongoDbContainerFixture mongoDb)
    {
        _mongoDb = mongoDb;
    }

    [Fact]
    public async System.Threading.Tasks.Task GetPagedAsync_ShouldFilterOrderAndPaginateWithinUserScope()
    {
        var settings = new MongoDbSettings
        {
            ConnectionString = _mongoDb.ConnectionString,
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

    [Theory]
    [InlineData(DueDateOrder.Ascending)]
    [InlineData(DueDateOrder.Descending)]
    public async System.Threading.Tasks.Task GetPagedAsync_ShouldPlaceTasksWithoutDueDateLast(DueDateOrder dueDateOrder)
    {
        var repository = CreateRepository("taskflow-order");
        var userId = Guid.NewGuid();
        var soon = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        var later = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);

        await repository.AddAsync(new DomainTask(userId, "Soon", null, dueDate: soon));
        await repository.AddAsync(new DomainTask(userId, "Later", null, dueDate: later));
        await repository.AddAsync(new DomainTask(userId, "Undated", null));

        var result = await repository.GetPagedAsync(
            userId,
            pageNumber: 1,
            pageSize: 20,
            titleContains: null,
            descriptionContains: null,
            status: null,
            dueDateOrder,
            CancellationToken.None);

        var expected = dueDateOrder == DueDateOrder.Ascending
            ? new[] { "Soon", "Later", "Undated" }
            : new[] { "Later", "Soon", "Undated" };

        Assert.Equal(expected, result.Items.Select(task => task.Title).ToArray());
    }

    [Theory]
    [InlineData(DueDateOrder.Ascending)]
    [InlineData(DueDateOrder.Descending)]
    public async System.Threading.Tasks.Task GetPagedAsync_ShouldKeepPagesStable_WhenDueDatesMatch(DueDateOrder dueDateOrder)
    {
        var (repository, context) = CreateRepositoryWithContext("taskflow-stable-order");
        var userId = Guid.NewGuid();
        var sharedDueDate = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc);
        var older = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var newer = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var lowId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var highId = Guid.Parse("00000000-0000-0000-0000-000000000002");

        await context.Tasks.InsertManyAsync(
        [
            TaskDocument(lowId, userId, "Early old low", sharedDueDate, older),
            TaskDocument(highId, userId, "Early old high", sharedDueDate, older),
            TaskDocument(Guid.NewGuid(), userId, "Early new", sharedDueDate, newer),
            TaskDocument(Guid.NewGuid(), userId, "Late", new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc), older),
            TaskDocument(Guid.NewGuid(), userId, "Undated", null, newer)
        ]);

        var expected = dueDateOrder == DueDateOrder.Ascending
            ? new[] { "Early new", "Early old low", "Early old high", "Late", "Undated" }
            : new[] { "Late", "Early new", "Early old low", "Early old high", "Undated" };

        var pages = new List<string>();
        for (var pageNumber = 1; pageNumber <= 3; pageNumber++)
        {
            var page = await repository.GetPagedAsync(
                userId,
                pageNumber,
                pageSize: 2,
                titleContains: null,
                descriptionContains: null,
                status: null,
                dueDateOrder,
                CancellationToken.None);

            Assert.Equal(5, page.TotalCount);
            pages.AddRange(page.Items.Select(task => task.Title));
        }

        Assert.Equal(expected, pages);
    }

    [Fact]
    public async System.Threading.Tasks.Task DeleteAsync_ShouldRemoveOnlyOwnedTask()
    {
        var settings = new MongoDbSettings
        {
            ConnectionString = _mongoDb.ConnectionString,
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

    [Fact]
    public async System.Threading.Tasks.Task CountByStatusAsync_ShouldCountOnlyTheUsersTasksPerStatus()
    {
        var repository = CreateRepository("taskflow-summary");
        var userId = Guid.NewGuid();

        await repository.AddAsync(new DomainTask(userId, "Pending one", null));
        await repository.AddAsync(new DomainTask(userId, "Pending two", null));
        await repository.AddAsync(new DomainTask(userId, "Done", null, DomainTaskStatus.Completed));
        await repository.AddAsync(new DomainTask(Guid.NewGuid(), "Other user", null, DomainTaskStatus.InProgress));

        var counts = await repository.CountByStatusAsync(userId, CancellationToken.None);

        Assert.Equal(2, counts[DomainTaskStatus.Pending]);
        Assert.Equal(1, counts[DomainTaskStatus.Completed]);
        Assert.False(counts.ContainsKey(DomainTaskStatus.InProgress));
    }

    private TaskRepository CreateRepository(string databasePrefix) =>
        CreateRepositoryWithContext(databasePrefix).Repository;

    private (TaskRepository Repository, TaskFlowMongoContext Context) CreateRepositoryWithContext(string databasePrefix)
    {
        var settings = new MongoDbSettings
        {
            ConnectionString = _mongoDb.ConnectionString,
            DatabaseName = $"{databasePrefix}-{Guid.NewGuid():N}"
        };
        var context = new TaskFlowMongoContext(settings);
        return (new TaskRepository(context), context);
    }

    private static TaskDocument TaskDocument(
        Guid id,
        Guid userId,
        string title,
        DateTime? dueDate,
        DateTime createdAt) =>
        new()
        {
            Id = id,
            UserId = userId,
            Title = title,
            Description = string.Empty,
            Status = DomainTaskStatus.Pending,
            DueDate = dueDate,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };
}
