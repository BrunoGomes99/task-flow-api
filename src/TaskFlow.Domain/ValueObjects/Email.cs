using System.Text.RegularExpressions;
using TaskFlow.Domain.Validation;

namespace TaskFlow.Domain.ValueObjects;

/// <summary>
/// Email value object. Normalizes input (trim + lowercase invariant) and enforces format and max length.
/// </summary>
public sealed record Email
{
    public const int MaxLength = 100;

    public string Value { get; }

    private Email(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Creates an <see cref="Email"/> from the raw string after normalization and validation.
    /// </summary>
    /// <param name="email">Raw email from user input.</param>
    /// <returns>Normalized email value object.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="email"/> is null.</exception>
    /// <exception cref="ArgumentException">When empty after trim, too long, or invalid format.</exception>
    /// <exception cref="ArgumentOutOfRangeException">When length exceeds <see cref="MaxLength"/>.</exception>
    public static Email Create(string email)
    {
        DomainValidation.NotNull(email, nameof(email));
        var normalized = email.Trim().ToLowerInvariant();

        Validate(normalized);
        return new Email(normalized);
    }

    /// <summary>
    /// Validates a normalized (trimmed + lowercased) email string.
    /// </summary>
    private static void Validate(string normalizedEmail)
    {
        DomainValidation.NotNullOrEmpty(normalizedEmail, "email");
        DomainValidation.MaxLength(normalizedEmail, MaxLength, "email");

        if (!EmailRegex.IsMatch(normalizedEmail))
            throw new ArgumentException("Invalid email format.", "email");
    }

    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.CultureInvariant);

    public override string ToString() => Value;
}
