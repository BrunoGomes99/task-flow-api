using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using TaskFlow.Api.Configuration;

namespace TaskFlow.Api.Extensions;

public static class CorsExtensions
{
    public const string PolicyName = "TaskFlow";

    public static IServiceCollection AddTaskFlowCors(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var settings = configuration.GetSection(CorsSettings.SectionName).Get<CorsSettings>();
        var origins = Normalize(settings?.AllowedOrigins ?? []);

        services.AddCors(options =>
        {
            options.AddPolicy(PolicyName, policy =>
            {
                if (origins.Length > 0)
                    policy.WithOrigins(origins);

                policy.WithMethods(
                        HttpMethods.Get,
                        HttpMethods.Post,
                        HttpMethods.Put,
                        HttpMethods.Patch,
                        HttpMethods.Delete)
                    .WithHeaders(HeaderNames.Authorization, HeaderNames.ContentType, HeaderNames.Accept)
                    .DisallowCredentials();
            });
        });

        return services;
    }

    private static string[] Normalize(IEnumerable<string> origins)
    {
        return origins
            .Select(origin => origin.Trim().TrimEnd('/'))
            .Where(origin => origin.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}
