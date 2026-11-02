using System.Diagnostics.Metrics;
using TaskFlow.Application.Observability;
using TaskFlow.Application.UseCases.Reminders.DeliverDueSoonReminder;

namespace TaskFlow.Application.Tests.Observability;

public sealed class ReminderMetricsTests
{
    [Fact]
    public void RecordPublished_AddsTheCount_WithoutTags()
    {
        using var metrics = new ReminderMetrics();
        using var listener = new RecordingMeterListener(metrics.Meter);

        metrics.RecordPublished(2);

        var measurement = Assert.Single(listener.Measurements);
        Assert.Equal("taskflow.reminders.published", measurement.Name);
        Assert.Equal(2, measurement.Value);
        Assert.Empty(measurement.Tags);
        Assert.Empty(measurement.InstrumentTags);
    }

    [Fact]
    public void RecordDelivery_AddsOne_WithSentResult()
    {
        using var metrics = new ReminderMetrics();
        using var listener = new RecordingMeterListener(metrics.Meter);

        metrics.RecordDelivery(DueSoonDeliveryReasons.Sent);

        var measurement = Assert.Single(listener.Measurements);
        Assert.Equal("taskflow.reminders.delivery", measurement.Name);
        Assert.Equal(1, measurement.Value);
        Assert.Equal(DueSoonDeliveryReasons.Sent, Tag(measurement, "result"));
    }

    [Fact]
    public void RecordScan_RecordsElapsedSeconds_WithSuccess()
    {
        using var metrics = new ReminderMetrics();
        using var listener = new RecordingMeterListener(metrics.Meter);

        metrics.RecordScan(TimeSpan.FromMilliseconds(1500), succeeded: true);

        var measurement = Assert.Single(listener.Measurements);
        Assert.Equal("taskflow.reminders.scan.duration", measurement.Name);
        Assert.Equal("s", measurement.Unit);
        Assert.Equal(1.5, measurement.Value);
        Assert.Equal("success", Tag(measurement, "result"));
    }

    [Fact]
    public void RecordScan_RecordsElapsedSeconds_WithFailure()
    {
        using var metrics = new ReminderMetrics();
        using var listener = new RecordingMeterListener(metrics.Meter);

        metrics.RecordScan(TimeSpan.FromSeconds(4), succeeded: false);

        var measurement = Assert.Single(listener.Measurements);
        Assert.Equal("taskflow.reminders.scan.duration", measurement.Name);
        Assert.Equal("s", measurement.Unit);
        Assert.Equal(4, measurement.Value);
        Assert.Equal("failure", Tag(measurement, "result"));
    }

    private static string Tag(ObservedMeasurement measurement, string key)
    {
        var tag = Assert.Single(measurement.Tags, pair => pair.Key == key);
        return Assert.IsType<string>(tag.Value);
    }

    private sealed class RecordingMeterListener : IDisposable
    {
        private readonly MeterListener _listener = new();
        private readonly List<ObservedMeasurement> _measurements = [];

        public RecordingMeterListener(Meter meter)
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (ReferenceEquals(instrument.Meter, meter) && instrument.Meter.Name == ReminderMetrics.MeterName)
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
                instrument.Unit,
                Convert.ToDouble(value),
                tags.ToArray(),
                instrument.Tags?.ToArray() ?? []));
        }
    }

    private sealed record ObservedMeasurement(
        string Name,
        string? Unit,
        double Value,
        IReadOnlyList<KeyValuePair<string, object?>> Tags,
        IReadOnlyList<KeyValuePair<string, object?>> InstrumentTags);
}
