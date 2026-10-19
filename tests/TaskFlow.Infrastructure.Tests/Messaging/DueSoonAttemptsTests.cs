using TaskFlow.Infrastructure.Messaging;

namespace TaskFlow.Infrastructure.Tests.Messaging;

public sealed class DueSoonAttemptsTests
{
    [Fact]
    public void Read_ReturnsOne_WhenHeaderMissing()
    {
        Assert.Equal(1, DueSoonAttempts.Read(null));
        Assert.Equal(1, DueSoonAttempts.Read(new Dictionary<string, object?>()));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void Read_ReturnsIntegerHeader(int attempt)
    {
        var headers = new Dictionary<string, object?>
        {
            [DueSoonTopology.AttemptHeader] = attempt,
        };

        Assert.Equal(attempt, DueSoonAttempts.Read(headers));
    }
}
