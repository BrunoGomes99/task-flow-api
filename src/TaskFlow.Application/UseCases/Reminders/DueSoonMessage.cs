namespace TaskFlow.Application.UseCases.Reminders;

/// <summary>
/// Application contract for a due reminder. No broker types.
/// </summary>
public sealed record DueSoonMessage(Guid TaskId, Guid UserId, DateTime DueDate);
