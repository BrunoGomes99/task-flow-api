using System.Reflection;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.SeedWork;
using TaskFlow.Infrastructure.Persistence.Documents;

namespace TaskFlow.Infrastructure.Persistence.Mappers;

internal static class UserDocumentMapper
{
    public static UserDocument ToDocument(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return new UserDocument
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email.Value,
            PasswordHash = user.PasswordHash,
            CreatedAt = EnsureUtc(user.CreatedAt)
        };
    }

    public static User ToDomain(UserDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var user = new User(document.Name, document.Email, document.PasswordHash);
        SetProperty(user, nameof(Entity.Id), document.Id);
        SetProperty(user, nameof(User.CreatedAt), EnsureUtc(document.CreatedAt));
        return user;
    }

    private static void SetProperty<TTarget>(TTarget target, string propertyName, object value)
    {
        var property = typeof(TTarget).GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Property '{propertyName}' was not found on '{typeof(TTarget).Name}'.");

        var setter = property.GetSetMethod(nonPublic: true)
            ?? throw new InvalidOperationException($"Property '{propertyName}' does not have a setter.");

        setter.Invoke(target, [value]);
    }

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
