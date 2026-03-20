namespace TaskFlow.Application.Common.Exceptions;

/// <summary>
/// Thrown when a requested resource cannot be resolved (e.g. missing identifier or not visible to the caller).
/// Use <paramref name="resourceName"/> to identify the kind of resource (e.g. "Task", "User").
/// </summary>
public sealed class NotFoundException : Exception
{
    public string ResourceName { get; }
    public string? ResourceId { get; }

    public NotFoundException(string resourceName, string? resourceId = null)
        : base(BuildMessage(resourceName, resourceId))
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);
        ResourceName = resourceName;
        ResourceId = resourceId;
    }

    public NotFoundException(string resourceName, Guid resourceId)
        : this(resourceName, resourceId.ToString("D"))
    {
    }

    private static string BuildMessage(string resourceName, string? resourceId)
    {
        return string.IsNullOrEmpty(resourceId)
            ? $"{resourceName} was not found."
            : $"{resourceName} '{resourceId}' was not found.";
    }
}
