using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace TaskFlow.Infrastructure.Caching;

/// <summary>
/// Counts cache lookups as hit, miss, or error.
/// </summary>
public sealed class CacheMetrics : IDisposable
{
    public const string MeterName = "TaskFlow.Cache";

    private readonly Meter _meter;
    private readonly Counter<long> _lookups;

    public CacheMetrics()
    {
        _meter = new Meter(MeterName);
        _lookups = _meter.CreateCounter<long>("taskflow.cache.lookups");
    }

    public Meter Meter => _meter;

    public void RecordHit() => Record("hit");

    public void RecordMiss() => Record("miss");

    public void RecordError() => Record("error");

    public void Dispose() => _meter.Dispose();

    private void Record(string result)
    {
        var tags = new TagList { { "result", result } };
        _lookups.Add(1, tags);
    }
}
