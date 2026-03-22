using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Domain.Tests.Entities;

public sealed class NotificationLogTests
{
    [Fact]
    public void Constructor_ShouldSetProcessedAtToUtcNow_WhenValid()
    {
        var taskId = Guid.NewGuid();
        var before = DateTime.UtcNow;

        var log = new NotificationLog(taskId, NotificationEventType.TaskCreated);

        var after = DateTime.UtcNow;

        Assert.NotEqual(Guid.Empty, log.Id);
        Assert.Equal(taskId, log.TaskId);
        Assert.Equal(NotificationEventType.TaskCreated, log.EventType);
        Assert.Equal(DateTimeKind.Utc, log.ProcessedAt.Kind);
        Assert.True(log.ProcessedAt >= before && log.ProcessedAt <= after);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenTaskIdEmpty()
    {
        Assert.Throws<ArgumentException>(() => new NotificationLog(Guid.Empty, NotificationEventType.TaskCompleted));
    }
}
