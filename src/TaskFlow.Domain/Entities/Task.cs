using System.Collections.Frozen;
using System.Collections.Generic;
using TaskFlow.Domain.SeedWork;
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

    private static readonly FrozenDictionary<Enums.TaskStatus, FrozenSet<Enums.TaskStatus>> AllowedTransitions =
        new Dictionary<Enums.TaskStatus, Enums.TaskStatus[]>
        {
            [Enums.TaskStatus.Pending] = [Enums.TaskStatus.InProgress, Enums.TaskStatus.Completed],
            [Enums.TaskStatus.InProgress] = [Enums.TaskStatus.Pending, Enums.TaskStatus.Completed],
            [Enums.TaskStatus.Completed] = [],
        }.ToFrozenDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.ToFrozenSet());

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
    /// Applies a status transition when allowed by aggregate rules. Single entry point for status changes.
    /// </summary>
    /// <returns><c>true</c> when the aggregate is already in <paramref name="target"/> (idempotent) or when the transition was applied; <c>false</c> when the transition is not allowed.</returns>
    public bool TryChangeStatusTo(Enums.TaskStatus target, out string failureMessage)
    {
        if (!ValidateTransitionTo(target, out failureMessage))
            return false;

        CommitTransitionTo(target);
        return true;
    }

    /// <summary>
    /// Updates the task title and description. Validates the new values before applying.
    /// </summary>
    /// <param name="title">The new title.</param>
    /// <param name="description">The new description (null is treated as empty).</param>
    public void Update(string title, string? description)
    {
        description ??= string.Empty;
        Validate(title, description);

        Title = title;
        Description = description;
        UpdatedAt = DateTime.UtcNow;
    }

    private bool ValidateTransitionTo(Enums.TaskStatus target, out string failureMessage)
    {
        if (Status == Enums.TaskStatus.Completed)
        {
            failureMessage = "Task is already completed. Completing a task cannot be undone.";
            return false;
        }

        if (Status == target)
        {
            failureMessage = string.Empty;
            return true;
        }

        if (!AllowedTransitions.TryGetValue(Status, out var allowed) || !allowed.Contains(target))
        {
            failureMessage = target switch
            {
                Enums.TaskStatus.Pending =>
                    $"Cannot set status to Pending from {Status}. Only InProgress tasks can be set back to Pending.",
                Enums.TaskStatus.InProgress =>
                    $"Cannot set status to InProgress from {Status}. Only Pending tasks can be set to InProgress.",
                Enums.TaskStatus.Completed =>
                    $"Cannot set status to Completed from {Status}.",
                _ => $"Cannot transition from {Status} to {target}.",
            };

            return false;
        }

        failureMessage = string.Empty;
        return true;
    }

    private void CommitTransitionTo(Enums.TaskStatus target)
    {
        if (Status == target)
            return;

        Validate(Title, Description);
        Status = target;
        UpdatedAt = DateTime.UtcNow;
    }

    private static void Validate(string title, string description)
    {
        DomainValidation.NotNullOrEmpty(title, nameof(title));
        DomainValidation.MinLength(title, TitleMinLength, nameof(title));
        DomainValidation.MaxLength(title, TitleMaxLength, nameof(title));
        DomainValidation.MaxLength(description, DescriptionMaxLength, nameof(description));
    }
}
