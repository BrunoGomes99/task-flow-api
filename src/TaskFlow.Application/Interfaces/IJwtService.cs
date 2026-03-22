namespace TaskFlow.Application.Interfaces;

/// <summary>
/// Issues JWT access tokens. User id must appear as claim <c>sub</c> per project specification. Token validation is configured at the API host (Bearer middleware).
/// Implemented in Infrastructure (signing key, algorithm, expiry from configuration).
/// </summary>
public interface IJwtService
{
    /// <summary>
    /// Creates a signed access token for the user. The returned lifetime in seconds must match the JWT <c>exp</c> claim
    /// and <see cref="TaskFlow.Application.DTOs.LoginResponse.ExpiresIn"/> in the login API response.
    /// </summary>
    /// <param name="userId">User id to embed as <c>sub</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Access token string and validity period in seconds.</returns>
    System.Threading.Tasks.Task<(string AccessToken, int ExpiresInSeconds)> CreateAccessTokenAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
