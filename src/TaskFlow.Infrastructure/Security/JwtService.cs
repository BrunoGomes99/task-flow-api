using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TaskFlow.Application.Interfaces;
using TaskFlow.Infrastructure.Configuration;

namespace TaskFlow.Infrastructure.Security;

public sealed class JwtService : IJwtService
{
    private readonly JwtSettings _settings;
    private readonly SigningCredentials _signingCredentials;
    private readonly JwtSecurityTokenHandler _tokenHandler = new();

    public JwtService(IOptions<JwtSettings> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _settings = options.Value;
        ValidateSettings(_settings);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret));
        _signingCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature);
    }

    public Task<(string AccessToken, int ExpiresInSeconds)> CreateAccessTokenAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var lifetimeSeconds = _settings.AccessTokenLifetimeSeconds;
        var expires = DateTime.UtcNow.AddSeconds(lifetimeSeconds);

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            ]),
            Expires = expires,
            Issuer = _settings.Issuer,
            Audience = _settings.Audience,
            SigningCredentials = _signingCredentials,
        };

        var token = _tokenHandler.CreateToken(descriptor);
        var accessToken = _tokenHandler.WriteToken(token);
        return Task.FromResult((accessToken, lifetimeSeconds));
    }

    private static void ValidateSettings(JwtSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Secret))
            throw new InvalidOperationException("Jwt:Secret must be configured.");

        if (Encoding.UTF8.GetByteCount(settings.Secret) < 32)
            throw new InvalidOperationException(
                "Jwt:Secret must be at least 32 bytes when UTF-8 encoded (use a longer key in configuration).");

        if (string.IsNullOrWhiteSpace(settings.Issuer))
            throw new InvalidOperationException("Jwt:Issuer must be configured.");

        if (string.IsNullOrWhiteSpace(settings.Audience))
            throw new InvalidOperationException("Jwt:Audience must be configured.");

        if (settings.AccessTokenLifetimeSeconds is < 60 or > 604_800)
            throw new InvalidOperationException(
                "Jwt:AccessTokenLifetimeSeconds must be between 60 and 604800 (inclusive).");
    }
}
