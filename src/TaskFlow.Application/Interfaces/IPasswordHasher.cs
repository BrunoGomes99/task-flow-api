namespace TaskFlow.Application.Interfaces;

/// <summary>
/// Password hashing and verification for registration and login. Plain passwords never leave the application boundary into the domain.
/// Implemented in Infrastructure (e.g. BCrypt) per project specification.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Produces a one-way hash suitable for storage on <see cref="Domain.Entities.User"/>.
    /// </summary>
    /// <param name="password">Plain-text password from registration.</param>
    /// <returns>Encoded hash string to persist as <c>PasswordHash</c>.</returns>
    string Hash(string password);

    /// <summary>
    /// Verifies a plain-text password against a stored hash (e.g. on login).
    /// </summary>
    /// <param name="password">Plain-text password from the client.</param>
    /// <param name="passwordHash">Stored hash from the user aggregate.</param>
    /// <returns>True if the password matches the hash.</returns>
    bool Verify(string password, string passwordHash);
}
