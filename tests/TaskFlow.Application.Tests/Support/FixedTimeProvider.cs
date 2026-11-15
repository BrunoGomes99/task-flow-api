namespace TaskFlow.Application.Tests.Support;

internal sealed class FixedTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _utc;

    public FixedTimeProvider(DateTimeOffset utc) => _utc = utc;

    public override DateTimeOffset GetUtcNow() => _utc;
}
