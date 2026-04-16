namespace TaskFlow.Domain.Exceptions;

/// <summary>
/// Thrown when a task status transition is not allowed by domain rules.
/// Mapped at the API boundary to HTTP 409 Conflict.
/// </summary>
public sealed class TaskStatusTransitionException : Exception
{
    public TaskStatusTransitionException(string message) : base(message)
    {
    }
}
