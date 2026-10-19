using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Domain.Tests.Entities;

public sealed class NotificationLogTests
{
    [Fact]
    public void Constructor_ShouldSetReceiptFields_WhenValid()
    {
        var taskId = Guid.NewGuid();
        var dueDate = new DateTime(2026, 10, 18, 15, 0, 0, DateTimeKind.Utc);
        var before = DateTime.UtcNow;

        var log = new NotificationLog(taskId, NotificationEventType.DueSoon, dueDate);

        var after = DateTime.UtcNow;

        Assert.NotEqual(Guid.Empty, log.Id);
        Assert.Equal(taskId, log.TaskId);
        Assert.Equal(NotificationEventType.DueSoon, log.EventType);
        Assert.Equal(dueDate, log.DueDate);
        Assert.Equal(DateTimeKind.Utc, log.ProcessedAt.Kind);
        Assert.True(log.ProcessedAt >= before && log.ProcessedAt <= after);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenTaskIdEmpty()
    {
        var dueDate = new DateTime(2026, 10, 18, 15, 0, 0, DateTimeKind.Utc);

        Assert.Throws<ArgumentException>(() => new NotificationLog(Guid.Empty, NotificationEventType.DueSoon, dueDate));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenDueDateMissing()
    {
        Assert.Throws<ArgumentException>(() => new NotificationLog(Guid.NewGuid(), NotificationEventType.DueSoon, default));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenDueDateIsNotUtc()
    {
        var local = new DateTime(2026, 10, 18, 15, 0, 0, DateTimeKind.Local);

        Assert.Throws<ArgumentException>(() => new NotificationLog(Guid.NewGuid(), NotificationEventType.DueSoon, local));
    }
}
