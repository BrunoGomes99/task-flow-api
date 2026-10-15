using TaskFlow.Application.Common;
using TaskFlow.Application.Common.Results;
using TaskFlow.Application.DTOs;
using TaskFlow.Application.Enums;
using TaskFlow.Application.Interfaces;
using TaskFlow.Application.UseCases.Tasks.GetTaskById;
using DomainTask = TaskFlow.Domain.Entities.Task;
using DomainTaskStatus = TaskFlow.Domain.Enums.TaskStatus;

namespace TaskFlow.Application.Tests.UseCases;

public sealed class TaskCacheAsideTests
{
    [Fact]
    public async System.Threading.Tasks.Task GetTaskById_ShouldReturnCachedTask_ForMatchingUserAndTask_WithoutReadingRepository()
    {
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var createdAt = new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);
        var cached = new TaskDto(taskId, "Cached", "Desc", DomainTaskStatus.Pending, null, createdAt, createdAt);
        var cache = new FakeTaskCache();
        cache.Seed(userId, taskId, cached);
        cache.Seed(otherUserId, taskId, cached with { Title = "Other user" });
        var handler = new GetTaskByIdQueryHandler(new ThrowingTaskRepository(), cache);

        var result = await handler.Handle(new GetTaskByIdQuery(userId, taskId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Cached", result.Value!.Title);
        Assert.Equal((userId, taskId), cache.LastGet);
    }

    [Fact]
    public async System.Threading.Tasks.Task GetTaskById_ShouldLoadRepositoryAndStoreDto_WhenCacheMisses()
    {
        var repository = new InMemoryTaskRepository();
        var userId = Guid.NewGuid();
        var task = new DomainTask(userId, "From mongo", "Body");
        await repository.AddAsync(task, CancellationToken.None);
        var cache = new FakeTaskCache();
        var handler = new GetTaskByIdQueryHandler(repository, cache);

        var result = await handler.Handle(new GetTaskByIdQuery(userId, task.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("From mongo", result.Value!.Title);
        Assert.Equal("From mongo", cache.Stored[(userId, task.Id)].Title);
        Assert.Equal(task.Id, cache.Stored[(userId, task.Id)].Id);
    }

    [Fact]
    public async System.Threading.Tasks.Task GetTaskById_ShouldNotStore_WhenTaskDoesNotExist()
    {
        var cache = new FakeTaskCache();
        var handler = new GetTaskByIdQueryHandler(new InMemoryTaskRepository(), cache);

        var result = await handler.Handle(new GetTaskByIdQuery(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.TaskNotFound, result.Code);
        Assert.Empty(cache.Stored);
    }

    private sealed class FakeTaskCache : ITaskCache
    {
        public Dictionary<(Guid UserId, Guid TaskId), TaskDto> Stored { get; } = [];

        public (Guid UserId, Guid TaskId)? LastGet { get; private set; }

        public void Seed(Guid userId, Guid taskId, TaskDto task) => Stored[(userId, taskId)] = task;

        public System.Threading.Tasks.Task<TaskDto?> GetAsync(Guid userId, Guid taskId, CancellationToken cancellationToken = default)
        {
            LastGet = (userId, taskId);
            Stored.TryGetValue((userId, taskId), out var task);
            return System.Threading.Tasks.Task.FromResult(task);
        }

        public System.Threading.Tasks.Task SetAsync(Guid userId, Guid taskId, TaskDto task, CancellationToken cancellationToken = default)
        {
            Stored[(userId, taskId)] = task;
            return System.Threading.Tasks.Task.CompletedTask;
        }

        public System.Threading.Tasks.Task<bool> RemoveAsync(Guid userId, Guid taskId, CancellationToken cancellationToken = default)
            => System.Threading.Tasks.Task.FromResult(true);
    }

    private sealed class ThrowingTaskRepository : ITaskRepository
    {
        public System.Threading.Tasks.Task<DomainTask?> GetByIdAsync(Guid userId, Guid taskId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Repository should not be called on a cache hit.");

        public System.Threading.Tasks.Task AddAsync(DomainTask task, CancellationToken cancellationToken = default)
            => System.Threading.Tasks.Task.CompletedTask;

        public System.Threading.Tasks.Task UpdateAsync(DomainTask task, CancellationToken cancellationToken = default)
            => System.Threading.Tasks.Task.CompletedTask;

        public System.Threading.Tasks.Task<bool> DeleteAsync(Guid userId, Guid taskId, CancellationToken cancellationToken = default)
            => System.Threading.Tasks.Task.FromResult(false);

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

    private sealed class InMemoryTaskRepository : ITaskRepository
    {
        private readonly List<DomainTask> _items = [];

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
            => System.Threading.Tasks.Task.CompletedTask;

        public System.Threading.Tasks.Task<bool> DeleteAsync(Guid userId, Guid taskId, CancellationToken cancellationToken = default)
            => System.Threading.Tasks.Task.FromResult(false);

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
