namespace TaskFlow.Application.UseCases.Reminders.DeliverDueSoonReminder;

public sealed record DueReminderEmail(
    string To,
    string Subject,
    string Title,
    DateTime DueDateUtc)
{
    public static DueReminderEmail Compose(string to, string title, DateTime dueDateUtc)
    {
        return new DueReminderEmail(
            to,
            $"Lembrete: {title} vence em cerca de 24 horas",
            title,
            DueInstant.NormalizeUtc(dueDateUtc));
    }
}
