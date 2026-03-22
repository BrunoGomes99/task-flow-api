using TaskFlow.Domain.SeedWork;
using TaskFlow.Domain.Validation;
using TaskFlow.Domain.ValueObjects;

namespace TaskFlow.Domain.Entities;

/// <summary>
/// User aggregate root. Holds identity and credentials for authentication.
/// </summary>
public class User : AggregateRoot
{
    private const int NameMaxLength = 255;
    private const int PasswordHashMinLength = 8;
    private const int PasswordHashMaxLength = 255;

    public string Name { get; private set; } = string.Empty;
    public Email Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// Creates a new user. Password must already be hashed (never store plain text).
    /// </summary>
    /// <param name="name">Display name.</param>
    /// <param name="email">Email address (validated and normalized via <see cref="Email"/>).</param>
    /// <param name="passwordHash">Pre-hashed password.</param>
    public User(string name, string email, string passwordHash)
    {
        Validate(name, passwordHash, out var trimmedName);
        Email = Email.Create(email);
        Name = trimmedName;
        PasswordHash = passwordHash;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Validates raw inputs before the user is materialized.
    /// </summary>
    private static void Validate(string name, string passwordHash, out string trimmedName)
    {
        DomainValidation.NotNull(name, nameof(name));        

        trimmedName = name.Trim();
        DomainValidation.NotNullOrEmpty(trimmedName, nameof(name));
        DomainValidation.MaxLength(trimmedName, NameMaxLength, nameof(name));

        DomainValidation.NotNullOrEmpty(passwordHash, nameof(passwordHash));
        DomainValidation.MinLength(passwordHash, PasswordHashMinLength, nameof(passwordHash));
        DomainValidation.MaxLength(passwordHash, PasswordHashMaxLength, nameof(passwordHash));
    }
}
