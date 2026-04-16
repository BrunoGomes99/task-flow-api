namespace TaskFlow.Application.Common.Exceptions;

/// <summary>
/// Thrown when a request cannot be completed due to a conflict with the current state
/// (e.g. uniqueness constraint, duplicate resource).
/// </summary>
public sealed class ConflictException : Exception
{
    public string? ResourceName { get; }
    public string? ResourceId { get; }

    public ConflictException(string message) : base(message)
    {
    }

    public ConflictException(string resourceName, string message, string? resourceId = null) : base(message)
    {
        ResourceName = resourceName;
        ResourceId = resourceId;
    }

    public ConflictException(string resourceName, string message, Guid resourceId)
        : this(resourceName, message, resourceId.ToString("D"))
    {
    }
}

