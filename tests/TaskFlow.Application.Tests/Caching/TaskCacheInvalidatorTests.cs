using Microsoft.Extensions.Logging;
using TaskFlow.Application.Caching;
using TaskFlow.Application.DTOs;
using TaskFlow.Application.Interfaces;

namespace TaskFlow.Application.Tests.Caching;

public sealed class TaskCacheInvalidatorTests
{
    [Fact]
    public async System.Threading.Tasks.Task Invalidate_ShouldStop_WhenSecondDeleteSucceeds()
    {
        var cache = new ScriptedTaskCache(false, true);
        var logger = new RecordingLogger<TaskCacheInvalidator>();
        var invalidator = new TaskCacheInvalidator(cache, logger, TimeSpan.Zero);
        var userId = Guid.NewGuid();
        var taskId = Guid.NewGuid();

        await invalidator.InvalidateAsync(userId, taskId, CancellationToken.None);

        Assert.Equal(2, cache.RemoveCalls);
        Assert.Empty(logger.Messages);
    }

    [Fact]
    public async System.Threading.Tasks.Task Invalidate_ShouldLogKeyAndComplete_WhenAllDeletesFail()
    {
        var cache = new ScriptedTaskCache(false, false, false);
        var logger = new RecordingLogger<TaskCacheInvalidator>();
        var invalidator = new TaskCacheInvalidator(cache, logger, TimeSpan.Zero);
        var userId = Guid.NewGuid();
        var taskId = Guid.NewGuid();

        await invalidator.InvalidateAsync(userId, taskId, CancellationToken.None);

        Assert.Equal(3, cache.RemoveCalls);
        var message = Assert.Single(logger.Messages);
        Assert.Contains(userId.ToString(), message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(taskId.ToString(), message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ScriptedTaskCache : ITaskCache
    {
        private readonly bool[] _results;
        private int _index;

        public ScriptedTaskCache(params bool[] results)
        {
            _results = results;
        }

        public int RemoveCalls { get; private set; }

        public System.Threading.Tasks.Task<TaskDto?> GetAsync(Guid userId, Guid taskId, CancellationToken cancellationToken = default)
            => System.Threading.Tasks.Task.FromResult<TaskDto?>(null);

        public System.Threading.Tasks.Task SetAsync(Guid userId, Guid taskId, TaskDto task, CancellationToken cancellationToken = default)
            => System.Threading.Tasks.Task.CompletedTask;

        public System.Threading.Tasks.Task<bool> RemoveAsync(Guid userId, Guid taskId, CancellationToken cancellationToken = default)
        {
            RemoveCalls++;
            var succeeded = _index < _results.Length && _results[_index];
            _index++;
            return System.Threading.Tasks.Task.FromResult(succeeded);
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Warning)
                Messages.Add(formatter(state, exception));
        }
    }
}
