using TaskFlow.Domain.Time;

namespace TaskFlow.Domain.Tests.Time;

public sealed class UtcInstantTests
{
    [Fact]
    public void Normalize_ShouldKeepUtcAndTruncateToMilliseconds()
    {
        var value = new DateTime(2026, 11, 21, 2, 59, 59, DateTimeKind.Utc).AddTicks(2500);

        var normalized = UtcInstant.Normalize(value);

        Assert.Equal(DateTimeKind.Utc, normalized.Kind);
        Assert.Equal(new DateTime(2026, 11, 21, 2, 59, 59, DateTimeKind.Utc), normalized);
    }

    [Fact]
    public void Normalize_ShouldTreatUnspecifiedAsUtc()
    {
        var value = new DateTime(2026, 11, 21, 2, 59, 59, DateTimeKind.Unspecified);

        var normalized = UtcInstant.Normalize(value);

        Assert.Equal(DateTimeKind.Utc, normalized.Kind);
        Assert.Equal(value.Ticks, normalized.Ticks);
    }

    [Fact]
    public void Normalize_ShouldConvertLocalToUtc()
    {
        var local = new DateTime(2026, 11, 20, 23, 59, 59, DateTimeKind.Local);
        var expected = UtcInstant.Normalize(local.ToUniversalTime());

        Assert.Equal(expected, UtcInstant.Normalize(local));
    }

    [Fact]
    public void Normalize_ShouldReturnNull_WhenValueIsNull()
    {
        Assert.Null(UtcInstant.Normalize(null));
    }
}
