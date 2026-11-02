namespace TaskFlow.Application.UseCases.Reminders.DeliverDueSoonReminder;

/// <summary>
/// Stable reasons for a due-reminder delivery. They describe the decision that already exists.
/// </summary>
public static class DueSoonDeliveryReasons
{
    public const string AlreadySent = "already_sent";
    public const string Skipped = "skipped";
    public const string Sent = "sent";
    public const string Requeue = "requeue";
    public const string DeadLetter = "dead_letter";
    public const string Discarded = "discarded";
}
