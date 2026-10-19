using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Configuration;
using TaskFlow.Infrastructure.Persistence;
using TaskFlow.Infrastructure.Repositories;
using TaskFlow.Infrastructure.Tests.Support;
using DomainTask = TaskFlow.Domain.Entities.Task;
using DomainTaskStatus = TaskFlow.Domain.Enums.TaskStatus;
using NotificationLog = TaskFlow.Domain.Entities.NotificationLog;

namespace TaskFlow.Infrastructure.Tests.Repositories;

[Collection(MongoDbContainerFixture.CollectionName)]
public sealed class DueReminderRepositoryTests
{
    private readonly MongoDbContainerFixture _mongoDb;

    public DueReminderRepositoryTests(MongoDbContainerFixture mongoDb)
    {
        _mongoDb = mongoDb;
    }

    [Fact]
    public async System.Threading.Tasks.Task ListInWindow_ReturnsOnlyOpenTasksInsideTheWindow()
    {
        var context = NewContext();
        var tasks = new TaskRepository(context);
        var query = new TaskReminderRepository(context);
        var start = new DateTime(2026, 10, 17, 14, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 10, 17, 15, 0, 0, DateTimeKind.Utc);
        var inside = new DomainTask(Guid.NewGuid(), "Dentro", "x", DomainTaskStatus.Pending, start.AddMinutes(10));
        var outside = new DomainTask(Guid.NewGuid(), "Fora", "x", DomainTaskStatus.Pending, end.AddMinutes(1));
        var completed = new DomainTask(Guid.NewGuid(), "Feita", "x", DomainTaskStatus.Pending, start.AddMinutes(20));
        Assert.True(completed.TryChangeStatusTo(DomainTaskStatus.Completed, out _));
        var noDue = new DomainTask(Guid.NewGuid(), "Sem prazo", "x");

        await tasks.AddAsync(inside, CancellationToken.None);
        await tasks.AddAsync(outside, CancellationToken.None);
        await tasks.AddAsync(completed, CancellationToken.None);
        await tasks.AddAsync(noDue, CancellationToken.None);

        var found = await query.ListInWindowAsync(start, end, CancellationToken.None);

        var match = Assert.Single(found);
        Assert.Equal(inside.Id, match.Id);
        Assert.Equal(inside.UserId, match.UserId);
        Assert.Equal("Dentro", match.Title);
    }

    [Fact]
    public async System.Threading.Tasks.Task Insert_SecondReceiptForSameDueInstant_KeepsTheFirst()
    {
        var context = NewContext();
        var repository = new NotificationLogRepository(context);
        var due = new DateTime(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc);
        var taskId = Guid.NewGuid();

        await repository.InsertAsync(
            new NotificationLog(taskId, NotificationEventType.DueSoon, due),
            CancellationToken.None);
        await repository.InsertAsync(
            new NotificationLog(taskId, NotificationEventType.DueSoon, due),
            CancellationToken.None);

        Assert.True(await repository.ExistsAsync(taskId, NotificationEventType.DueSoon, due, CancellationToken.None));
    }

    private TaskFlowMongoContext NewContext()
    {
        var settings = new MongoDbSettings
        {
            ConnectionString = _mongoDb.ConnectionString,
            DatabaseName = $"taskflow-reminder-{Guid.NewGuid():N}",
        };
        var context = new TaskFlowMongoContext(settings);
        new MongoIndexesInitializer(context).InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();
        return context;
    }
}
