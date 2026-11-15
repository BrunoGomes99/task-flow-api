namespace TaskFlow.Domain.Time;

/// <summary>
/// Normalizes an instant to UTC truncated to milliseconds, matching MongoDB storage.
/// <see cref="DateTimeKind.Unspecified"/> is treated as UTC.
/// </summary>
public static class UtcInstant
{
    public static DateTime Normalize(DateTime value)
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

    public static DateTime? Normalize(DateTime? value) =>
        value is null ? null : Normalize(value.Value);
}
