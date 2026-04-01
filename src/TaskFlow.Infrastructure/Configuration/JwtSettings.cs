namespace TaskFlow.Infrastructure.Configuration;

public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    /// <summary>
    /// Symmetric key for HMAC-SHA256 signing. Must be at least 32 UTF-8 bytes. Override via environment or user secrets in production.
    /// </summary>
    public string Secret { get; set; } = string.Empty;

    public string Issuer { get; set; } = "TaskFlow";

    public string Audience { get; set; } = "TaskFlow.Api";

    /// <summary>
    /// Access token lifetime in seconds; must align with the JWT <c>exp</c> claim and login response <c>ExpiresIn</c>.
    /// </summary>
    public int AccessTokenLifetimeSeconds { get; set; } = 3600;
}
