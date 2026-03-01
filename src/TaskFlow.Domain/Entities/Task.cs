using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Validation;

namespace TaskFlow.Domain.Entities;

/// <summary>
/// Task entity (aggregate root).
/// Represents a user task, with multi-tenancy via UserId.
/// </summary>
public class Task : AggregateRoot
{
    private const int TitleMinLength = 3;
    private const int TitleMaxLength = 200;
    private const int DescriptionMaxLength = 2000;

    public Guid UserId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public Enums.TaskStatus Status { get; private set; }
    public DateTime? DueDate { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public Task(
        Guid userId,
        string title,
        string? description,
        Enums.TaskStatus status = Enums.TaskStatus.Pending,
        DateTime? dueDate = null)
    {
        description ??= string.Empty;
        Validate(title, description);

        UserId = userId;
        Title = title;
        Description = description;
        Status = status;
        DueDate = dueDate;
        var now = DateTime.UtcNow;
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>
    /// Sets the task status to Pending. Only allowed when current status is InProgress.
    /// </summary>
    /// <exception cref="InvalidOperationException">When the transition from current status is not allowed.</exception>
    public void SetPending()
    {
        if (Status != Enums.TaskStatus.InProgress)
            throw new InvalidOperationException(
                $"Cannot set status to Pending from {Status}. Only InProgress tasks can be set back to Pending.");

        Status = Enums.TaskStatus.Pending;
        UpdatedAt = DateTime.UtcNow;
        Validate(Title, Description);
    }

    /// <summary>
    /// Sets the task status to InProgress. Only allowed when current status is Pending.
    /// </summary>
    /// <exception cref="InvalidOperationException">When the transition from current status is not allowed.</exception>
    public void SetInProgress()
    {
        if (Status != Enums.TaskStatus.Pending)
            throw new InvalidOperationException(
                $"Cannot set status to InProgress from {Status}. Only Pending tasks can be set to InProgress.");

        Status = Enums.TaskStatus.InProgress;
        UpdatedAt = DateTime.UtcNow;
        Validate(Title, Description);
    }

    /// <summary>
    /// Sets the task status to Completed. Only allowed when current status is Pending or InProgress. Completing a task cannot be undone.
    /// </summary>
    /// <exception cref="InvalidOperationException">When the task is already Completed or the transition is not allowed.</exception>
    public void SetCompleted()
    {
        if (Status == Enums.TaskStatus.Completed)
            throw new InvalidOperationException("Task is already completed. Completing a task cannot be undone.");

        if (Status != Enums.TaskStatus.Pending && Status != Enums.TaskStatus.InProgress)
            throw new InvalidOperationException($"Cannot set status to Completed from {Status}.");

        Status = Enums.TaskStatus.Completed;
        UpdatedAt = DateTime.UtcNow;
        Validate(Title, Description);
    }

    /// <summary>
    /// Updates the task title and description. Validates the new values before applying.
    /// </summary>
    /// <param name="title">The new title.</param>
    /// <param name="description">The new description (null is treated as empty).</param>
    public void Update(string title, string? description)
    {
        description ??= string.Empty;

        Title = title;
        Description = description;
        UpdatedAt = DateTime.UtcNow;
        Validate(Title, Description);
    }

    private static void Validate(string title, string description)
    {
        DomainValidation.NotNullOrEmpty(title, nameof(title));
        DomainValidation.MinLength(title, TitleMinLength, nameof(title));
        DomainValidation.MaxLength(title, TitleMaxLength, nameof(title));
        DomainValidation.MaxLength(description, DescriptionMaxLength, nameof(description));
    }
}
