using System.Diagnostics;
using TaskFlow.Application.UseCases.Reminders.PublishDueSoonReminders;

namespace TaskFlow.Scheduler;

public sealed class DueSoonScanHostedService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<DueSoonScanHostedService> _logger;

    public DueSoonScanHostedService(IServiceScopeFactory scopes, ILogger<DueSoonScanHostedService> logger)
    {
        _scopes = scopes;
        _logger = logger;
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
        try
        {
            var stopwatch = Stopwatch.StartNew();
            await using var scope = _scopes.CreateAsyncScope();
            var scanner = scope.ServiceProvider.GetRequiredService<DueSoonReminderPublish>();
            var summary = await scanner.PublishAsync(cancellationToken);
            stopwatch.Stop();
            _logger.LogInformation(
                "DueSoon scan published {Published} reminders and skipped {Skipped} in {ElapsedMilliseconds} ms",
                summary.Published,
                summary.Skipped,
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "DueSoon scan failed");
        }
    }
}
