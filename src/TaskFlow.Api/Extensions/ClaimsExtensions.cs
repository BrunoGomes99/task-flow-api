using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace TaskFlow.Api.Extensions;

public static class ClaimsExtensions
{
    /// <summary>
    /// Reads the authenticated user id from JWT <c>sub</c> (or mapped name identifier when applicable).
    /// </summary>
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var sub = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? user.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(sub) || !Guid.TryParse(sub, out var userId))
            throw new InvalidOperationException("The access token is missing a valid 'sub' claim.");

        return userId;
    }
}
