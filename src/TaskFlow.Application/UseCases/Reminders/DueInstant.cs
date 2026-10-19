namespace TaskFlow.Application.UseCases.Reminders;

/// <summary>
/// Normalizes a due instant to UTC truncated to milliseconds, matching database storage.
/// Unspecified kind is treated as UTC, the same way task documents are mapped.
/// </summary>
public static class DueInstant
{
    public static DateTime NormalizeUtc(DateTime value)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };

        var ticks = utc.Ticks - (utc.Ticks % TimeSpan.TicksPerMillisecond);
        return new DateTime(ticks, DateTimeKind.Utc);
    }
}
