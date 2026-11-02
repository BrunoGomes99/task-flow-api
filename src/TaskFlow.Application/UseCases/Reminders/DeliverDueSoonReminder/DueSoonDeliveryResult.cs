using TaskFlow.Application.Enums;

namespace TaskFlow.Application.UseCases.Reminders.DeliverDueSoonReminder;

/// <summary>
/// Broker outcome plus the stable reason that describes it.
/// </summary>
public readonly record struct DueSoonDeliveryResult(DueSoonDeliveryOutcome Outcome, string Reason);
