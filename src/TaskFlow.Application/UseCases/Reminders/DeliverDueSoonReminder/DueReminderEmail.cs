using System.Globalization;

namespace TaskFlow.Application.UseCases.Reminders.DeliverDueSoonReminder;

public sealed record DueReminderEmail(string To, string Subject, string Body)
{
    public static DueReminderEmail Compose(string to, string title, DateTime dueDateUtc)
    {
        var due = DueInstant.NormalizeUtc(dueDateUtc).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        return new DueReminderEmail(
            to,
            $"Lembrete: {title} vence em cerca de 24 horas",
            $"A tarefa \"{title}\" vence em {due} UTC.");
    }
}
