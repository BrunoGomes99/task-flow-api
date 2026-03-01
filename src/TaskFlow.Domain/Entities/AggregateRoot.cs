namespace TaskFlow.Domain.Entities;

/// <summary>
/// Base class for aggregate roots.
/// Inherits from Entity and marks the entity as the root of an aggregate in the domain.
/// </summary>
public abstract class AggregateRoot : Entity
{
    protected AggregateRoot()
    {
    }
}
