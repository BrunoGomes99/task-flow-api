using TaskFlow.Application.Common;
using TaskFlow.Application.Common.Results;
using TaskFlow.Application.Enums;
using TaskFlow.Application.Interfaces;
using TaskFlow.Application.UseCases.Tasks.DeleteTask;
using TaskFlow.Application.UseCases.Tasks.UpdateTask;
using TaskFlow.Application.UseCases.Tasks.UpdateTaskStatus;
using DomainTask = TaskFlow.Domain.Entities.Task;
using DomainTaskStatus = TaskFlow.Domain.Enums.TaskStatus;

namespace TaskFlow.Application.Tests.UseCases;

public sealed class TaskCacheInvalidationTests
{
    [Fact]
    public async System.Threading.Tasks.Task UpdateTask_ShouldInvalidate_AfterMongoWrite()
    {
        var userId = Guid.NewGuid();
        var task = new DomainTask(userId, "Old title", "Old desc");
        var events = new List<string>();
        var repository = new TrackingTaskRepository(events);
        await repository.AddAsync(task, CancellationToken.None);
        var invalidator = new RecordingInvalidator(events);
        var handler = new UpdateTaskCommandHandler(repository, invalidator);

        var result = await handler.Handle(
            new UpdateTaskCommand(userId, task.Id, "New title", "New desc"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(["persisted", "invalidated"], events);
        Assert.Equal((userId, task.Id), Assert.Single(invalidator.Calls));
    }

    [Fact]
    public async System.Threading.Tasks.Task UpdateTask_ShouldNotInvalidate_WhenTaskDoesNotExist()
    {
        var invalidator = new RecordingInvalidator([]);
        var handler = new UpdateTaskCommandHandler(new TrackingTaskRepository([]), invalidator);

        var result = await handler.Handle(
            new UpdateTaskCommand(Guid.NewGuid(), Guid.NewGuid(), "New title", "New desc"),
            CancellationToken.None);

        Assert.Equal(ErrorCodes.TaskNotFound, result.Code);
        Assert.Empty(invalidator.Calls);
    }

    [Fact]
    public async System.Threading.Tasks.Task UpdateTaskStatus_ShouldInvalidate_AfterMongoWrite()
    {
        var userId = Guid.NewGuid();
        var task = new DomainTask(userId, "Task title", null);
        var events = new List<string>();
        var repository = new TrackingTaskRepository(events);
        await repository.AddAsync(task, CancellationToken.None);
        var invalidator = new RecordingInvalidator(events);
        var handler = new UpdateTaskStatusCommandHandler(repository, invalidator);

        var result = await handler.Handle(
            new UpdateTaskStatusCommand(userId, task.Id, DomainTaskStatus.InProgress),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(["persisted", "invalidated"], events);
    }

    [Fact]
    public async System.Threading.Tasks.Task UpdateTaskStatus_ShouldNotInvalidate_WhenTransitionIsRejected()
    {
        var userId = Guid.NewGuid();
        var task = new DomainTask(userId, "Task title", null, DomainTaskStatus.Completed);
        var repository = new TrackingTaskRepository([]);
        await repository.AddAsync(task, CancellationToken.None);
        var invalidator = new RecordingInvalidator([]);
        var handler = new UpdateTaskStatusCommandHandler(repository, invalidator);

        var result = await handler.Handle(
            new UpdateTaskStatusCommand(userId, task.Id, DomainTaskStatus.Pending),
            CancellationToken.None);

        Assert.Equal(ErrorCodes.TaskStatusTransitionInvalid, result.Code);
        Assert.Empty(invalidator.Calls);
    }

    [Fact]
    public async System.Threading.Tasks.Task DeleteTask_ShouldInvalidate_AfterMongoWrite()
    {
        var userId = Guid.NewGuid();
        var task = new DomainTask(userId, "Task title", null);
        var events = new List<string>();
        var repository = new TrackingTaskRepository(events);
        await repository.AddAsync(task, CancellationToken.None);
        var invalidator = new RecordingInvalidator(events);
        var handler = new DeleteTaskCommandHandler(repository, invalidator);

        var result = await handler.Handle(new DeleteTaskCommand(userId, task.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(["persisted", "invalidated"], events);
        Assert.Equal((userId, task.Id), Assert.Single(invalidator.Calls));
    }

    [Fact]
    public async System.Threading.Tasks.Task DeleteTask_ShouldNotInvalidate_WhenTaskDoesNotExist()
    {
        var invalidator = new RecordingInvalidator([]);
        var handler = new DeleteTaskCommandHandler(new TrackingTaskRepository([]), invalidator);

        var result = await handler.Handle(new DeleteTaskCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(ErrorCodes.TaskNotFound, result.Code);
        Assert.Empty(invalidator.Calls);
    }

    private sealed class RecordingInvalidator : ITaskCacheInvalidator
    {
        private readonly List<string> _events;

        public RecordingInvalidator(List<string> events) => _events = events;

        public List<(Guid UserId, Guid TaskId)> Calls { get; } = [];

        public System.Threading.Tasks.Task InvalidateAsync(Guid userId, Guid taskId, CancellationToken cancellationToken = default)
        {
            Calls.Add((userId, taskId));
            _events.Add("invalidated");
            return System.Threading.Tasks.Task.CompletedTask;
        }
    }

    private sealed class TrackingTaskRepository : ITaskRepository
    {
        private readonly List<string> _events;
        private readonly List<DomainTask> _items = [];

        public TrackingTaskRepository(List<string> events) => _events = events;

        public System.Threading.Tasks.Task<DomainTask?> GetByIdAsync(Guid userId, Guid taskId, CancellationToken cancellationToken = default)
        {
            var item = _items.FirstOrDefault(x => x.UserId == userId && x.Id == taskId);
            return System.Threading.Tasks.Task.FromResult(item);
        }

        public System.Threading.Tasks.Task AddAsync(DomainTask task, CancellationToken cancellationToken = default)
        {
            _items.Add(task);
            return System.Threading.Tasks.Task.CompletedTask;
        }

        public System.Threading.Tasks.Task UpdateAsync(DomainTask task, CancellationToken cancellationToken = default)
        {
            _events.Add("persisted");
            return System.Threading.Tasks.Task.CompletedTask;
        }

        public System.Threading.Tasks.Task<bool> DeleteAsync(Guid userId, Guid taskId, CancellationToken cancellationToken = default)
        {
            var existing = _items.FirstOrDefault(x => x.UserId == userId && x.Id == taskId);
            if (existing is null)
                return System.Threading.Tasks.Task.FromResult(false);

            _items.Remove(existing);
            _events.Add("persisted");
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
            => throw new NotSupportedException();
    }
}
