namespace TaskFlow.Application.UseCases.Reminders.PublishDueSoonReminders;

/// <summary>
/// Published and skipped counts for one finished due-reminder scan.
/// </summary>
public readonly record struct DueSoonPublishSummary(int Published, int Skipped);
