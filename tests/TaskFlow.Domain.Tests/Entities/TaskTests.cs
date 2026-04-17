using DomainTask = TaskFlow.Domain.Entities.Task;
using DomainTaskStatus = TaskFlow.Domain.Enums.TaskStatus;
namespace TaskFlow.Domain.Tests.Entities;

public sealed class TaskTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public void Constructor_ShouldCreateTask_WhenInputsValid()
    {
        var before = DateTime.UtcNow;
        var task = new DomainTask(UserId, "ABC", "Description", DomainTaskStatus.Pending, null);
        var after = DateTime.UtcNow;

        Assert.NotEqual(Guid.Empty, task.Id);
        Assert.Equal(UserId, task.UserId);
        Assert.Equal("ABC", task.Title);
        Assert.Equal("Description", task.Description);
        Assert.Equal(DomainTaskStatus.Pending, task.Status);
        Assert.Null(task.DueDate);
        Assert.Equal(DateTimeKind.Utc, task.CreatedAt.Kind);
        Assert.Equal(DateTimeKind.Utc, task.UpdatedAt.Kind);
        Assert.True(task.CreatedAt >= before && task.CreatedAt <= after);
        Assert.Equal(task.CreatedAt, task.UpdatedAt);
    }

    [Fact]
    public void Constructor_ShouldTreatNullDescriptionAsEmpty()
    {
        var task = new DomainTask(UserId, "ABC", null);

        Assert.Equal(string.Empty, task.Description);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenTitleIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new DomainTask(UserId, null!, null));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenTitleIsEmptyOrWhitespace()
    {
        Assert.Throws<ArgumentException>(() => new DomainTask(UserId, "", null));
        Assert.Throws<ArgumentException>(() => new DomainTask(UserId, "   ", null));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenTitleIsTooShort()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DomainTask(UserId, "AB", null));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenTitleExceedsMaxLength()
    {
        var longTitle = new string('a', 201);

        Assert.Throws<ArgumentOutOfRangeException>(() => new DomainTask(UserId, longTitle, null));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenDescriptionExceedsMaxLength()
    {
        var longDesc = new string('d', 2001);

        Assert.Throws<ArgumentOutOfRangeException>(() => new DomainTask(UserId, "ABC", longDesc));
    }

    [Fact]
    public void TryChangeStatusTo_ShouldTransitionToInProgress_WhenPending()
    {
        var task = new DomainTask(UserId, "ABC", null);

        Assert.True(task.TryChangeStatusTo(DomainTaskStatus.InProgress, out _));

        Assert.Equal(DomainTaskStatus.InProgress, task.Status);
        Assert.Equal(DateTimeKind.Utc, task.UpdatedAt.Kind);
    }

    [Fact]
    public void TryChangeStatusTo_ShouldBeIdempotent_WhenAlreadyInProgress()
    {
        var task = new DomainTask(UserId, "ABC", null, DomainTaskStatus.InProgress, null);

        Assert.True(task.TryChangeStatusTo(DomainTaskStatus.InProgress, out _));

        Assert.Equal(DomainTaskStatus.InProgress, task.Status);
    }

    [Fact]
    public void TryChangeStatusTo_ShouldTransitionToPending_WhenInProgress()
    {
        var task = new DomainTask(UserId, "ABC", null);

        Assert.True(task.TryChangeStatusTo(DomainTaskStatus.InProgress, out _));
        Assert.True(task.TryChangeStatusTo(DomainTaskStatus.Pending, out _));

        Assert.Equal(DomainTaskStatus.Pending, task.Status);
    }

    [Fact]
    public void TryChangeStatusTo_ShouldBeIdempotent_WhenAlreadyPending()
    {
        var task = new DomainTask(UserId, "ABC", null);

        Assert.True(task.TryChangeStatusTo(DomainTaskStatus.Pending, out _));

        Assert.Equal(DomainTaskStatus.Pending, task.Status);
    }

    [Fact]
    public void TryChangeStatusTo_ShouldTransitionToCompleted_WhenPending()
    {
        var task = new DomainTask(UserId, "ABC", null);

        Assert.True(task.TryChangeStatusTo(DomainTaskStatus.Completed, out _));

        Assert.Equal(DomainTaskStatus.Completed, task.Status);
    }

    [Fact]
    public void TryChangeStatusTo_ShouldTransitionToCompleted_WhenInProgress()
    {
        var task = new DomainTask(UserId, "ABC", null);

        Assert.True(task.TryChangeStatusTo(DomainTaskStatus.InProgress, out _));
        Assert.True(task.TryChangeStatusTo(DomainTaskStatus.Completed, out _));

        Assert.Equal(DomainTaskStatus.Completed, task.Status);
    }

    [Fact]
    public void TryChangeStatusTo_ShouldFail_WhenAlreadyCompleted()
    {
        var task = new DomainTask(UserId, "ABC", null);

        Assert.True(task.TryChangeStatusTo(DomainTaskStatus.Completed, out _));

        Assert.False(task.TryChangeStatusTo(DomainTaskStatus.Completed, out var failure));
        Assert.Contains("already completed", failure, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Update_ShouldApplyTitleAndDescription_WhenValid()
    {
        var task = new DomainTask(UserId, "ABC", "Old");

        task.Update("New title here", "New desc");

        Assert.Equal("New title here", task.Title);
        Assert.Equal("New desc", task.Description);
    }

    [Fact]
    public void Update_ShouldThrow_WhenNewTitleInvalid()
    {
        var task = new DomainTask(UserId, "ABC", null);

        Assert.Throws<ArgumentOutOfRangeException>(() => task.Update("AB", null));
    }
}
