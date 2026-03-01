namespace TaskFlow.Domain.Entities;

/// <summary>
/// Base class for all domain entities.
/// Id is set to a new Guid on instantiation.
/// </summary>
public abstract class Entity
{
    public Guid Id { get; protected set; }

    protected Entity()
    {
        Id = Guid.NewGuid();
    }
}
