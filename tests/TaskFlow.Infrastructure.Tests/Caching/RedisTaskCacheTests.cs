using Microsoft.Extensions.Logging.Abstractions;
using TaskFlow.Application.DTOs;
using TaskFlow.Infrastructure.Caching;
using DomainTaskStatus = TaskFlow.Domain.Enums.TaskStatus;

namespace TaskFlow.Infrastructure.Tests.Caching;

public sealed class RedisTaskCacheTests
{
    [Fact]
    public async Task SetAndGet_ShouldUseUserAndTaskKey_WithAbsoluteTtlOf60Seconds()
    {
        var store = new RecordingRedisStringCache();
        var cache = new RedisTaskCache(store, NullLogger<RedisTaskCache>.Instance);
        var userId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var taskId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var createdAt = new DateTime(2026, 10, 14, 15, 0, 0, DateTimeKind.Utc);
        var dto = new TaskDto(taskId, "Cached title", "Body", DomainTaskStatus.InProgress, createdAt, createdAt, createdAt);

        await cache.SetAsync(userId, taskId, dto, CancellationToken.None);
        var setCallsAfterWrite = store.SetCalls;
        var loaded = await cache.GetAsync(userId, taskId, CancellationToken.None);
        var otherUser = await cache.GetAsync(Guid.NewGuid(), taskId, CancellationToken.None);

        Assert.Equal($"task:{userId:D}:{taskId:D}", store.LastSetKey);
        Assert.Equal(TimeSpan.FromSeconds(60), store.LastSetTtl);
        Assert.Equal(dto, loaded);
        Assert.Null(otherUser);
        Assert.Equal(setCallsAfterWrite, store.SetCalls);
    }

    [Fact]
    public async Task Get_ShouldReturnNull_WhenStoreThrows()
    {
        var store = new RecordingRedisStringCache { GetException = new InvalidOperationException("redis down") };
        var cache = new RedisTaskCache(store, NullLogger<RedisTaskCache>.Instance);

        var loaded = await cache.GetAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.Null(loaded);
    }

    [Fact]
    public async Task Set_ShouldSwallowStoreExceptions()
    {
        var store = new RecordingRedisStringCache { SetException = new InvalidOperationException("redis down") };
        var cache = new RedisTaskCache(store, NullLogger<RedisTaskCache>.Instance);
        var dto = new TaskDto(Guid.NewGuid(), "Title", "", DomainTaskStatus.Pending, null, DateTime.UtcNow, DateTime.UtcNow);

        var act = () => cache.SetAsync(Guid.NewGuid(), dto.Id, dto, CancellationToken.None);

        await act();
    }

    [Fact]
    public async Task Remove_ShouldReturnFalse_WhenStoreThrows_AndTrueWhenDeleteCompletes()
    {
        var failing = new RecordingRedisStringCache { DeleteException = new InvalidOperationException("redis down") };
        var failingCache = new RedisTaskCache(failing, NullLogger<RedisTaskCache>.Instance);
        var healthy = new RedisTaskCache(new RecordingRedisStringCache(), NullLogger<RedisTaskCache>.Instance);
        var userId = Guid.NewGuid();
        var taskId = Guid.NewGuid();

        var failed = await failingCache.RemoveAsync(userId, taskId, CancellationToken.None);
        var removed = await healthy.RemoveAsync(userId, taskId, CancellationToken.None);

        Assert.False(failed);
        Assert.True(removed);
        Assert.Equal($"task:{userId:D}:{taskId:D}", failing.LastDeleteKey);
    }

    private sealed class RecordingRedisStringCache : IRedisStringCache
    {
        private readonly Dictionary<string, string> _values = [];

        public string? LastSetKey { get; private set; }

        public TimeSpan? LastSetTtl { get; private set; }

        public string? LastDeleteKey { get; private set; }

        public int SetCalls { get; private set; }

        public Exception? GetException { get; init; }

        public Exception? SetException { get; init; }

        public Exception? DeleteException { get; init; }

        public Task<string?> GetAsync(string key, CancellationToken cancellationToken)
        {
            if (GetException is not null)
                throw GetException;

            _values.TryGetValue(key, out var value);
            return Task.FromResult(value);
        }

        public Task SetAsync(string key, string value, TimeSpan absoluteTtl, CancellationToken cancellationToken)
        {
            SetCalls++;
            LastSetKey = key;
            LastSetTtl = absoluteTtl;
            if (SetException is not null)
                throw SetException;

            _values[key] = value;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string key, CancellationToken cancellationToken)
        {
            LastDeleteKey = key;
            if (DeleteException is not null)
                throw DeleteException;

            _values.Remove(key);
            return Task.CompletedTask;
        }
    }
}
