using TaskFlow.Application.UseCases.Reminders.DeliverDueSoonReminder;

namespace TaskFlow.Application.Interfaces;

public interface IEmailSender
{
    /// <summary>
    /// Returns true when the provider accepts the message.
    /// </summary>
    Task<bool> SendAsync(DueReminderEmail email, CancellationToken cancellationToken = default);
}
