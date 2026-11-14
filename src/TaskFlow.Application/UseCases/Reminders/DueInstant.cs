using TaskFlow.Domain.Time;

namespace TaskFlow.Application.UseCases.Reminders;

/// <summary>
/// Normalizes a due instant to UTC truncated to milliseconds, matching database storage.
/// Unspecified kind is treated as UTC, the same way task documents are mapped.
/// </summary>
public static class DueInstant
{
    public static DateTime NormalizeUtc(DateTime value) => UtcInstant.Normalize(value);
}
