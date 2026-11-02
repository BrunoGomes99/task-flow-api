using System.Diagnostics.Metrics;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using TaskFlow.Application.DTOs;
using TaskFlow.Infrastructure.Caching;
using DomainTaskStatus = TaskFlow.Domain.Enums.TaskStatus;

namespace TaskFlow.Infrastructure.Tests.Caching;

public sealed class CacheMetricsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Get_WithStoredJson_CountsHit()
    {
        var task = new TaskDto(
            Guid.Parse("11111111-2222-3333-4444-555555555555"),
            "Cached title",
            "Body",
            DomainTaskStatus.Pending,
            null,
            new DateTime(2026, 10, 14, 15, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 10, 14, 15, 0, 0, DateTimeKind.Utc));
        using var metrics = new CacheMetrics();
        using var listener = new RecordingMeterListener(metrics.Meter);
        var cache = new RedisTaskCache(
            new StubStringCache { Value = JsonSerializer.Serialize(task, JsonOptions) },
            NullLogger<RedisTaskCache>.Instance,
            metrics);

        var loaded = await cache.GetAsync(Guid.NewGuid(), task.Id, CancellationToken.None);

        Assert.Equal(task, loaded);
        var measurement = Assert.Single(listener.Measurements);
        Assert.Equal("taskflow.cache.lookups", measurement.Name);
        Assert.Equal(1, measurement.Value);
        Assert.Equal("hit", Tag(measurement, "result"));
    }

    [Fact]
    public async Task Get_WithEmptyValue_CountsMiss_AndReturnsNull()
    {
        using var metrics = new CacheMetrics();
        using var listener = new RecordingMeterListener(metrics.Meter);
        var cache = new RedisTaskCache(
            new StubStringCache { Value = "" },
            NullLogger<RedisTaskCache>.Instance,
            metrics);

        var loaded = await cache.GetAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.Null(loaded);
        var measurement = Assert.Single(listener.Measurements);
        Assert.Equal("taskflow.cache.lookups", measurement.Name);
        Assert.Equal(1, measurement.Value);
        Assert.Equal("miss", Tag(measurement, "result"));
    }

    [Fact]
    public async Task Get_WhenStoreThrows_CountsError_AndReturnsNull()
    {
        using var metrics = new CacheMetrics();
        using var listener = new RecordingMeterListener(metrics.Meter);
        var cache = new RedisTaskCache(
            new StubStringCache { GetException = new InvalidOperationException("redis down") },
            NullLogger<RedisTaskCache>.Instance,
            metrics);

        var loaded = await cache.GetAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.Null(loaded);
        var measurement = Assert.Single(listener.Measurements);
        Assert.Equal("taskflow.cache.lookups", measurement.Name);
        Assert.Equal(1, measurement.Value);
        Assert.Equal("error", Tag(measurement, "result"));
    }

    private static string Tag(ObservedMeasurement measurement, string key)
    {
        var tag = Assert.Single(measurement.Tags, pair => pair.Key == key);
        return Assert.IsType<string>(tag.Value);
    }

    private sealed class StubStringCache : IRedisStringCache
    {
        public string? Value { get; init; }

        public Exception? GetException { get; init; }

        public Task<string?> GetAsync(string key, CancellationToken cancellationToken)
        {
            if (GetException is not null)
                throw GetException;

            return Task.FromResult(Value);
        }

        public Task SetAsync(string key, string value, TimeSpan absoluteTtl, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task DeleteAsync(string key, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class RecordingMeterListener : IDisposable
    {
        private readonly MeterListener _listener = new();
        private readonly List<ObservedMeasurement> _measurements = [];

        public RecordingMeterListener(Meter meter)
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (ReferenceEquals(instrument.Meter, meter) && instrument.Meter.Name == CacheMetrics.MeterName)
                    listener.EnableMeasurementEvents(instrument);
            };
            _listener.SetMeasurementEventCallback<long>(Record);
            _listener.SetMeasurementEventCallback<double>(Record);
            _listener.Start();
        }

        public IReadOnlyList<ObservedMeasurement> Measurements => _measurements;

        public void Dispose() => _listener.Dispose();

        private void Record<T>(
            Instrument instrument,
            T value,
            ReadOnlySpan<KeyValuePair<string, object?>> tags,
            object? state)
            where T : struct
        {
            _measurements.Add(new ObservedMeasurement(
                instrument.Name,
                Convert.ToDouble(value),
                tags.ToArray()));
        }
    }

    private sealed record ObservedMeasurement(
        string Name,
        double Value,
        IReadOnlyList<KeyValuePair<string, object?>> Tags);
}
