using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace TaskFlow.Application.Observability;

/// <summary>
/// Counts published reminders, delivery reasons, and due-soon scan duration.
/// Tags stay inside the closed sets from the Phase 4 design.
/// </summary>
public sealed class ReminderMetrics : IDisposable
{
    public const string MeterName = "TaskFlow.Reminders";

    private readonly Meter _meter;
    private readonly Counter<long> _published;
    private readonly Counter<long> _delivery;
    private readonly Histogram<double> _scanDuration;

    public ReminderMetrics()
    {
        _meter = new Meter(MeterName);
        _published = _meter.CreateCounter<long>("taskflow.reminders.published");
        _delivery = _meter.CreateCounter<long>("taskflow.reminders.delivery");
        _scanDuration = _meter.CreateHistogram<double>("taskflow.reminders.scan.duration", unit: "s");
    }

    public Meter Meter => _meter;

    public void RecordPublished(int count) => _published.Add(count);

    public void RecordDelivery(string reason)
    {
        var tags = new TagList { { "result", reason } };
        _delivery.Add(1, tags);
    }

    public void RecordScan(TimeSpan elapsed, bool succeeded)
    {
        var tags = new TagList { { "result", succeeded ? "success" : "failure" } };
        _scanDuration.Record(elapsed.TotalSeconds, tags);
    }

    public void Dispose() => _meter.Dispose();
}
