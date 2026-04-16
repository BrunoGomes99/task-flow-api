using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using TaskFlow.Infrastructure.Configuration;

namespace TaskFlow.Api.Extensions;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddTaskFlowJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var jwt = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>();
        if (jwt is null || string.IsNullOrWhiteSpace(jwt.Secret))
            throw new InvalidOperationException("Jwt configuration is missing or incomplete.");

        if (Encoding.UTF8.GetByteCount(jwt.Secret) < 32)
            throw new InvalidOperationException("Jwt:Secret must be at least 32 bytes when UTF-8 encoded.");

        if (string.IsNullOrWhiteSpace(jwt.Issuer) || string.IsNullOrWhiteSpace(jwt.Audience))
            throw new InvalidOperationException("Jwt:Issuer and Jwt:Audience must be configured.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                    NameClaimType = JwtRegisteredClaimNames.Sub,
                };
            });

        services.AddAuthorization();
        return services;
    }
}
