using TaskFlow.Application.Common;
using TaskFlow.Application.Common.Results;
using TaskFlow.Application.Tests.Support;
using TaskFlow.Application.UseCases.Tasks.CreateTask;
using TaskFlow.Application.UseCases.Tasks.DeleteTask;
using TaskFlow.Application.UseCases.Tasks.GetTaskById;
using TaskFlow.Application.UseCases.Tasks.GetTaskSummary;
using TaskFlow.Application.UseCases.Tasks.ListTasks;
using TaskFlow.Application.UseCases.Tasks.UpdateTask;
using TaskFlow.Application.UseCases.Tasks.UpdateTaskStatus;
using TaskFlow.Application.DTOs;
using TaskFlow.Application.Enums;
using TaskFlow.Application.Interfaces;
using DomainTask = TaskFlow.Domain.Entities.Task;
using DomainTaskStatus = TaskFlow.Domain.Enums.TaskStatus;

namespace TaskFlow.Application.Tests.UseCases;

public sealed class TaskUseCasesTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 23, 0, 0, TimeSpan.Zero);

    [Fact]
    public async System.Threading.Tasks.Task CreateTaskHandler_ShouldPersistAndReturnId()
    {
        var repository = new InMemoryTaskRepository();
        var handler = new CreateTaskCommandHandler(repository);
        var userId = Guid.NewGuid();

        var result = await handler.Handle(
            new CreateTaskCommand(userId, "Task title", "Task description", null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var persisted = await repository.GetByIdAsync(userId, result.Value!.Id, CancellationToken.None);
        Assert.NotNull(persisted);
        Assert.Equal("Task title", persisted!.Title);
        Assert.Equal(DomainTaskStatus.Pending, persisted.Status);
    }

    [Fact]
    public void CreateTaskValidator_ShouldRejectPastDueDate()
    {
        var validator = new CreateTaskCommandValidator(new FixedTimeProvider(Now));

        var result = validator.Validate(
            new CreateTaskCommand(Guid.NewGuid(), "Task title", null, Now.UtcDateTime.AddSeconds(-1)));

        Assert.Equal(nameof(CreateTaskCommand.DueDate), Assert.Single(result.Errors).PropertyName);
    }

    [Fact]
    public void CreateTaskValidator_ShouldRejectDueDateEqualToNow()
    {
        var validator = new CreateTaskCommandValidator(new FixedTimeProvider(Now));

        var result = validator.Validate(
            new CreateTaskCommand(Guid.NewGuid(), "Task title", null, Now.UtcDateTime));

        Assert.Equal(nameof(CreateTaskCommand.DueDate), Assert.Single(result.Errors).PropertyName);
    }

    [Fact]
    public void CreateTaskValidator_ShouldAcceptEndOfTodayInLocalTime()
    {
        var validator = new CreateTaskCommandValidator(new FixedTimeProvider(Now));
        var endOfTodayLocal = new DateTime(2026, 11, 14, 2, 59, 59, DateTimeKind.Utc);

        var result = validator.Validate(new CreateTaskCommand(Guid.NewGuid(), "Task title", null, endOfTodayLocal));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void CreateTaskValidator_ShouldAcceptMissingDueDate()
    {
        var validator = new CreateTaskCommandValidator(new FixedTimeProvider(Now));

        var result = validator.Validate(new CreateTaskCommand(Guid.NewGuid(), "Task title", null, null));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async System.Threading.Tasks.Task GetTaskByIdHandler_ShouldReturnNotFound_WhenTaskDoesNotExist()
    {
        var repository = new InMemoryTaskRepository();
        var handler = new GetTaskByIdQueryHandler(repository, new NoOpTaskCache());

        var result = await handler.Handle(
            new GetTaskByIdQuery(Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.TaskNotFound, result.Code);
    }

    [Fact]
    public async System.Threading.Tasks.Task ListTasksHandler_ShouldReturnMappedPagedResult()
    {
        var repository = new InMemoryTaskRepository();
        var userId = Guid.NewGuid();
        await repository.AddAsync(new DomainTask(userId, "Alpha", null), CancellationToken.None);
        await repository.AddAsync(new DomainTask(userId, "Beta", "Desc"), CancellationToken.None);
        var handler = new ListTasksQueryHandler(repository);

        var result = await handler.Handle(
            new ListTasksQuery(userId, 1, 20, null, null, null, DueDateOrder.Ascending),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.TotalCount);
        Assert.Equal(2, result.Value.Items.Count);
        Assert.Contains(result.Value.Items, x => x.Title == "Alpha");
    }

    [Fact]
    public async System.Threading.Tasks.Task UpdateTaskHandler_ShouldUpdateTitleDescriptionAndDueDate()
    {
        var repository = new InMemoryTaskRepository();
        var userId = Guid.NewGuid();
        var task = new DomainTask(userId, "Old", "Old desc");
        await repository.AddAsync(task, CancellationToken.None);
        var handler = CreateUpdateHandler(repository);
        var newDueDate = Now.UtcDateTime.AddDays(3);

        var updateResult = await handler.Handle(
            new UpdateTaskCommand(userId, task.Id, "New", "New desc", newDueDate),
            CancellationToken.None);

        Assert.True(updateResult.IsSuccess);

        var updated = await repository.GetByIdAsync(userId, task.Id, CancellationToken.None);
        Assert.NotNull(updated);
        Assert.Equal("New", updated!.Title);
        Assert.Equal("New desc", updated.Description);
        Assert.Equal(newDueDate, updated.DueDate);
    }

    [Fact]
    public async System.Threading.Tasks.Task UpdateTaskHandler_ShouldKeepOverdueDate_WhenItIsNotChanged()
    {
        var repository = new InMemoryTaskRepository();
        var userId = Guid.NewGuid();
        var overdue = Now.UtcDateTime.AddDays(-2);
        var task = new DomainTask(userId, "Overdue", null, DomainTaskStatus.Pending, overdue);
        await repository.AddAsync(task, CancellationToken.None);
        var handler = CreateUpdateHandler(repository);

        var result = await handler.Handle(
            new UpdateTaskCommand(userId, task.Id, "Renamed", null, overdue.AddTicks(5)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Renamed", task.Title);
        Assert.Equal(overdue, task.DueDate);
    }

    [Fact]
    public async System.Threading.Tasks.Task UpdateTaskHandler_ShouldRejectPastDueDate_WhenItChanges()
    {
        var repository = new InMemoryTaskRepository();
        var userId = Guid.NewGuid();
        var task = new DomainTask(userId, "Task", null, DomainTaskStatus.Pending, Now.UtcDateTime.AddDays(-2));
        await repository.AddAsync(task, CancellationToken.None);
        var handler = CreateUpdateHandler(repository);

        var result = await handler.Handle(
            new UpdateTaskCommand(userId, task.Id, "Renamed", null, Now.UtcDateTime.AddDays(-1)),
            CancellationToken.None);

        Assert.Equal(ApplicationResultKind.BadRequest, result.Kind);
        Assert.Equal(ErrorCodes.TaskDueDateInPast, result.Code);
        Assert.Equal("Task", task.Title);
    }

    [Fact]
    public async System.Threading.Tasks.Task UpdateTaskHandler_ShouldRejectDueDateEqualToNow_WhenItChanges()
    {
        var repository = new InMemoryTaskRepository();
        var userId = Guid.NewGuid();
        var task = new DomainTask(userId, "Task", null);
        await repository.AddAsync(task, CancellationToken.None);
        var handler = CreateUpdateHandler(repository);

        var result = await handler.Handle(
            new UpdateTaskCommand(userId, task.Id, "Task", null, Now.UtcDateTime),
            CancellationToken.None);

        Assert.Equal(ErrorCodes.TaskDueDateInPast, result.Code);
    }

    [Fact]
    public async System.Threading.Tasks.Task UpdateTaskHandler_ShouldClearDueDate_EvenWhenTaskIsOverdue()
    {
        var repository = new InMemoryTaskRepository();
        var userId = Guid.NewGuid();
        var task = new DomainTask(userId, "Task", null, DomainTaskStatus.Pending, Now.UtcDateTime.AddDays(-2));
        await repository.AddAsync(task, CancellationToken.None);
        var handler = CreateUpdateHandler(repository);

        var result = await handler.Handle(
            new UpdateTaskCommand(userId, task.Id, "Task", null, null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(task.DueDate);
    }

    [Fact]
    public async System.Threading.Tasks.Task UpdateTaskHandler_ShouldReturnLocked_WhenTaskIsCompleted()
    {
        var repository = new InMemoryTaskRepository();
        var userId = Guid.NewGuid();
        var task = new DomainTask(userId, "Done", null, DomainTaskStatus.Completed);
        await repository.AddAsync(task, CancellationToken.None);
        var handler = CreateUpdateHandler(repository);

        var result = await handler.Handle(
            new UpdateTaskCommand(userId, task.Id, "Changed", null, null),
            CancellationToken.None);

        Assert.Equal(ApplicationResultKind.Conflict, result.Kind);
        Assert.Equal(ErrorCodes.TaskLocked, result.Code);
        Assert.Equal("Done", task.Title);
    }

    private static UpdateTaskCommandHandler CreateUpdateHandler(InMemoryTaskRepository repository) =>
        new(repository, new NoOpTaskCacheInvalidator(), new FixedTimeProvider(Now));

    [Fact]
    public async System.Threading.Tasks.Task UpdateTaskStatusHandler_ShouldSetStatusUsingDomainRules()
    {
        var repository = new InMemoryTaskRepository();
        var userId = Guid.NewGuid();
        var task = new DomainTask(userId, "Task", null);
        await repository.AddAsync(task, CancellationToken.None);
        var handler = new UpdateTaskStatusCommandHandler(repository, new NoOpTaskCacheInvalidator());

        var statusResult = await handler.Handle(
            new UpdateTaskStatusCommand(userId, task.Id, DomainTaskStatus.InProgress),
            CancellationToken.None);

        Assert.True(statusResult.IsSuccess);

        var updated = await repository.GetByIdAsync(userId, task.Id, CancellationToken.None);
        Assert.NotNull(updated);
        Assert.Equal(DomainTaskStatus.InProgress, updated!.Status);
    }

    [Fact]
    public async System.Threading.Tasks.Task DeleteTaskHandler_ShouldDelete_WhenTaskExists()
    {
        var repository = new InMemoryTaskRepository();
        var userId = Guid.NewGuid();
        var task = new DomainTask(userId, "Task", null);
        await repository.AddAsync(task, CancellationToken.None);
        var handler = new DeleteTaskCommandHandler(repository, new NoOpTaskCacheInvalidator());

        var deleteResult = await handler.Handle(new DeleteTaskCommand(userId, task.Id), CancellationToken.None);

        Assert.True(deleteResult.IsSuccess);
        var existing = await repository.GetByIdAsync(userId, task.Id, CancellationToken.None);
        Assert.Null(existing);
    }

    [Fact]
    public async System.Threading.Tasks.Task GetTaskSummaryHandler_ShouldCountPerStatus_AndReportMissingStatusesAsZero()
    {
        var repository = new InMemoryTaskRepository();
        var userId = Guid.NewGuid();
        await repository.AddAsync(new DomainTask(userId, "One", null), CancellationToken.None);
        await repository.AddAsync(new DomainTask(userId, "Two", null), CancellationToken.None);
        var inProgress = new DomainTask(userId, "Three", null);
        inProgress.TryChangeStatusTo(DomainTaskStatus.InProgress, out _);
        await repository.AddAsync(inProgress, CancellationToken.None);
        await repository.AddAsync(new DomainTask(Guid.NewGuid(), "Other user", null), CancellationToken.None);
        var handler = new GetTaskSummaryQueryHandler(repository);

        var result = await handler.Handle(new GetTaskSummaryQuery(userId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new TaskSummaryDto(Pending: 2, InProgress: 1, Completed: 0, Total: 3), result.Value);
    }

    private sealed class NoOpTaskCacheInvalidator : ITaskCacheInvalidator
    {
        public System.Threading.Tasks.Task InvalidateAsync(Guid userId, Guid taskId, CancellationToken cancellationToken = default)
            => System.Threading.Tasks.Task.CompletedTask;
    }

    private sealed class NoOpTaskCache : ITaskCache
    {
        public System.Threading.Tasks.Task<TaskDto?> GetAsync(Guid userId, Guid taskId, CancellationToken cancellationToken = default)
            => System.Threading.Tasks.Task.FromResult<TaskDto?>(null);

        public System.Threading.Tasks.Task SetAsync(Guid userId, Guid taskId, TaskDto task, CancellationToken cancellationToken = default)
            => System.Threading.Tasks.Task.CompletedTask;

        public System.Threading.Tasks.Task<bool> RemoveAsync(Guid userId, Guid taskId, CancellationToken cancellationToken = default)
            => System.Threading.Tasks.Task.FromResult(true);
    }

    private sealed class InMemoryTaskRepository : ITaskRepository
    {
        private readonly List<DomainTask> _items = [];

        public System.Threading.Tasks.Task<DomainTask?> GetByIdAsync(Guid userId, Guid taskId, CancellationToken cancellationToken = default)
        {
            var item = _items.FirstOrDefault(x => x.UserId == userId && x.Id == taskId);
            return System.Threading.Tasks.Task.FromResult<DomainTask?>(item);
        }

        public System.Threading.Tasks.Task AddAsync(DomainTask task, CancellationToken cancellationToken = default)
        {
            _items.Add(task);
            return System.Threading.Tasks.Task.CompletedTask;
        }

        public System.Threading.Tasks.Task UpdateAsync(DomainTask task, CancellationToken cancellationToken = default)
            => System.Threading.Tasks.Task.CompletedTask;

        public System.Threading.Tasks.Task<bool> DeleteAsync(Guid userId, Guid taskId, CancellationToken cancellationToken = default)
        {
            var existing = _items.FirstOrDefault(x => x.UserId == userId && x.Id == taskId);
            if (existing is null) return System.Threading.Tasks.Task.FromResult(false);
            _items.Remove(existing);
            return System.Threading.Tasks.Task.FromResult(true);
        }

        public System.Threading.Tasks.Task<PagedResult<DomainTask>> GetPagedAsync(
            Guid userId,
            int pageNumber,
            int pageSize,
            string? titleContains,
            string? descriptionContains,
            DomainTaskStatus? status,
            DueDateOrder dueDateOrder,
            CancellationToken cancellationToken = default)
        {
            IEnumerable<DomainTask> query = _items.Where(x => x.UserId == userId);

            if (!string.IsNullOrWhiteSpace(titleContains))
                query = query.Where(x => x.Title.Contains(titleContains, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(descriptionContains))
                query = query.Where(x => x.Description.Contains(descriptionContains, StringComparison.OrdinalIgnoreCase));

            if (status.HasValue)
                query = query.Where(x => x.Status == status.Value);

            query = dueDateOrder == DueDateOrder.Descending
                ? query.OrderByDescending(x => x.DueDate)
                : query.OrderBy(x => x.DueDate);

            var total = query.Count();
            var items = query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
            var paged = new PagedResult<DomainTask>(items, pageNumber, pageSize, total);
            return System.Threading.Tasks.Task.FromResult(paged);
        }

        public System.Threading.Tasks.Task<IReadOnlyDictionary<DomainTaskStatus, int>> CountByStatusAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyDictionary<DomainTaskStatus, int> counts = _items
                .Where(x => x.UserId == userId)
                .GroupBy(x => x.Status)
                .ToDictionary(g => g.Key, g => g.Count());
            return System.Threading.Tasks.Task.FromResult(counts);
        }
    }
}
