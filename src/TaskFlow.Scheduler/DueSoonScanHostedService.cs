using System.Diagnostics;
using TaskFlow.Application.Observability;
using TaskFlow.Application.UseCases.Reminders.PublishDueSoonReminders;

namespace TaskFlow.Scheduler;

public sealed class DueSoonScanHostedService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<DueSoonScanHostedService> _logger;
    private readonly ReminderMetrics _metrics;

    public DueSoonScanHostedService(
        IServiceScopeFactory scopes,
        ILogger<DueSoonScanHostedService> logger,
        ReminderMetrics metrics)
    {
        _scopes = scopes;
        _logger = logger;
        _metrics = metrics;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        await ScanAsync(stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
            await ScanAsync(stoppingToken);
    }

    private async Task ScanAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            var scanner = scope.ServiceProvider.GetRequiredService<DueSoonReminderPublish>();
            var summary = await scanner.PublishAsync(cancellationToken);
            stopwatch.Stop();
            _logger.LogInformation(
                "DueSoon scan published {Published} reminders and skipped {Skipped} in {ElapsedMilliseconds} ms",
                summary.Published,
                summary.Skipped,
                stopwatch.ElapsedMilliseconds);
            _metrics.RecordPublished(summary.Published);
            _metrics.RecordScan(stopwatch.Elapsed, succeeded: true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "DueSoon scan failed");
            _metrics.RecordScan(stopwatch.Elapsed, succeeded: false);
        }
    }
}
