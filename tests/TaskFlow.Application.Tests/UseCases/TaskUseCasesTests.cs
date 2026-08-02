using TaskFlow.Application.Common;
using TaskFlow.Application.Common.Results;
using TaskFlow.Application.UseCases.Tasks.CreateTask;
using TaskFlow.Application.UseCases.Tasks.DeleteTask;
using TaskFlow.Application.UseCases.Tasks.GetTaskById;
using TaskFlow.Application.UseCases.Tasks.ListTasks;
using TaskFlow.Application.UseCases.Tasks.UpdateTask;
using TaskFlow.Application.UseCases.Tasks.UpdateTaskStatus;
using TaskFlow.Application.Enums;
using TaskFlow.Application.Interfaces;
using DomainTask = TaskFlow.Domain.Entities.Task;
using DomainTaskStatus = TaskFlow.Domain.Enums.TaskStatus;

namespace TaskFlow.Application.Tests.UseCases;

public sealed class TaskUseCasesTests
{
    [Fact]
    public async System.Threading.Tasks.Task CreateTaskHandler_ShouldPersistAndReturnId()
    {
        var repository = new InMemoryTaskRepository();
        var handler = new CreateTaskCommandHandler(repository);
        var userId = Guid.NewGuid();

        var result = await handler.Handle(
            new CreateTaskCommand(userId, "Task title", "Task description", DomainTaskStatus.Pending, null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var persisted = await repository.GetByIdAsync(userId, result.Value!.Id, CancellationToken.None);
        Assert.NotNull(persisted);
        Assert.Equal("Task title", persisted!.Title);
    }

    [Fact]
    public async System.Threading.Tasks.Task GetTaskByIdHandler_ShouldReturnNotFound_WhenTaskDoesNotExist()
    {
        var repository = new InMemoryTaskRepository();
        var handler = new GetTaskByIdQueryHandler(repository);

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
    public async System.Threading.Tasks.Task UpdateTaskHandler_ShouldUpdateTitleAndDescription()
    {
        var repository = new InMemoryTaskRepository();
        var userId = Guid.NewGuid();
        var task = new DomainTask(userId, "Old", "Old desc");
        await repository.AddAsync(task, CancellationToken.None);
        var handler = new UpdateTaskCommandHandler(repository);

        var updateResult = await handler.Handle(
            new UpdateTaskCommand(userId, task.Id, "New", "New desc"),
            CancellationToken.None);

        Assert.True(updateResult.IsSuccess);

        var updated = await repository.GetByIdAsync(userId, task.Id, CancellationToken.None);
        Assert.NotNull(updated);
        Assert.Equal("New", updated!.Title);
        Assert.Equal("New desc", updated.Description);
    }

    [Fact]
    public async System.Threading.Tasks.Task UpdateTaskStatusHandler_ShouldSetStatusUsingDomainRules()
    {
        var repository = new InMemoryTaskRepository();
        var userId = Guid.NewGuid();
        var task = new DomainTask(userId, "Task", null);
        await repository.AddAsync(task, CancellationToken.None);
        var handler = new UpdateTaskStatusCommandHandler(repository);

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
        var handler = new DeleteTaskCommandHandler(repository);

        var deleteResult = await handler.Handle(new DeleteTaskCommand(userId, task.Id), CancellationToken.None);

        Assert.True(deleteResult.IsSuccess);
        var existing = await repository.GetByIdAsync(userId, task.Id, CancellationToken.None);
        Assert.Null(existing);
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
    }
}
