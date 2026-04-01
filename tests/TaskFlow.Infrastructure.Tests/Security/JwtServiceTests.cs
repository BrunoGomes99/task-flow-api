using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TaskFlow.Infrastructure.Configuration;
using TaskFlow.Infrastructure.Security;

namespace TaskFlow.Infrastructure.Tests.Security;

public sealed class JwtServiceTests
{
    [Fact]
    public async System.Threading.Tasks.Task CreateAccessTokenAsync_ShouldReturnSignedJwt_WithSubAndExpiresIn()
    {
        var settings = ValidSettings();
        var service = new JwtService(Options.Create(settings));
        var userId = Guid.Parse("a1111111-2222-3333-4444-555555555555");

        var (token, expiresIn) = await service.CreateAccessTokenAsync(userId, CancellationToken.None);

        Assert.Equal(settings.AccessTokenLifetimeSeconds, expiresIn);
        Assert.False(string.IsNullOrEmpty(token));

        var handler = new JwtSecurityTokenHandler();
        var parameters = ValidationParameters(settings);
        var principal = handler.ValidateToken(token, parameters, out var validatedToken);
        var jwt = (JwtSecurityToken)validatedToken;

        var sub = jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value;
        Assert.Equal(userId.ToString(), sub);
        Assert.Equal(userId.ToString(), principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.Equal(settings.Issuer, jwt.Issuer);
        Assert.Contains(settings.Audience, jwt.Audiences);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenSecretTooShort()
    {
        var settings = ValidSettings();
        settings.Secret = "short";

        var ex = Assert.Throws<InvalidOperationException>(() => new JwtService(Options.Create(settings)));
        Assert.Contains("32 bytes", ex.Message, StringComparison.Ordinal);
    }

    private static JwtSettings ValidSettings()
    {
        return new JwtSettings
        {
            Secret = "TaskFlow_Test_Secret_Minimum_32_Bytes_OK!",
            Issuer = "TaskFlow.Test",
            Audience = "TaskFlow.Api.Test",
            AccessTokenLifetimeSeconds = 120,
        };
    }

    private static TokenValidationParameters ValidationParameters(JwtSettings settings)
    {
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = settings.Issuer,
            ValidateAudience = true,
            ValidAudience = settings.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Secret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
        };
    }
}
