namespace TaskFlow.Domain.Validation;

/// <summary>
/// Common domain validations. Methods throw when the rule is violated.
/// </summary>
public static class DomainValidation
{
    /// <summary>
    /// Ensures the value is not null, empty, or whitespace-only.
    /// </summary>
    /// <param name="value">Value to validate.</param>
    /// <param name="paramName">Parameter name (for exception message).</param>
    /// <exception cref="ArgumentNullException">When <paramref name="value"/> is null.</exception>
    /// <exception cref="ArgumentException">When <paramref name="value"/> is empty or whitespace.</exception>
    public static void NotNullOrEmpty(string? value, string paramName)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value cannot be empty or whitespace.", paramName);
    }

    /// <summary>
    /// Ensures the value length is at least <paramref name="minLength"/>.
    /// </summary>
    /// <param name="value">Value to validate.</param>
    /// <param name="minLength">Minimum allowed length.</param>
    /// <param name="paramName">Parameter name (for exception message).</param>
    /// <exception cref="ArgumentOutOfRangeException">When length is less than <paramref name="minLength"/>.</exception>
    public static void MinLength(string value, int minLength, string paramName)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (value.Length < minLength)
            throw new ArgumentOutOfRangeException(paramName, value.Length,
                $"Length cannot be less than {minLength}. Current length: {value.Length}.");
    }

    /// <summary>
    /// Ensures the value length does not exceed <paramref name="maxLength"/>.
    /// </summary>
    /// <param name="value">Value to validate.</param>
    /// <param name="maxLength">Maximum allowed length.</param>
    /// <param name="paramName">Parameter name (for exception message).</param>
    /// <exception cref="ArgumentOutOfRangeException">When length exceeds <paramref name="maxLength"/>.</exception>
    public static void MaxLength(string value, int maxLength, string paramName)
    {
        if (value is null)
            return;

        if (value.Length > maxLength)
            throw new ArgumentOutOfRangeException(paramName, value.Length,
                $"Length cannot exceed {maxLength}. Current length: {value.Length}.");
    }
}
