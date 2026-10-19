namespace TaskFlow.Application.Enums;

/// <summary>
/// What the consumer should do with the current delivery.
/// </summary>
public enum DueSoonDeliveryOutcome
{
    Acknowledge = 0,
    Requeue = 1,
}
