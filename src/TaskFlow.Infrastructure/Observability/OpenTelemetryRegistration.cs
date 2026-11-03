using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using TaskFlow.Application.Behaviors;
using TaskFlow.Application.Observability;
using TaskFlow.Infrastructure.Caching;
using TaskFlow.Infrastructure.Configuration;

namespace TaskFlow.Infrastructure.Observability;

public static class OpenTelemetryRegistration
{
    public static IServiceCollection AddTaskFlowOpenTelemetry(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var settings = configuration
            .GetSection(OpenTelemetrySettings.SectionName)
            .Get<OpenTelemetrySettings>() ?? new OpenTelemetrySettings();

        if (!settings.Enabled)
            return services;

        if (string.IsNullOrWhiteSpace(settings.OtlpEndpoint))
            throw new InvalidOperationException("OpenTelemetry:OtlpEndpoint must be configured when OpenTelemetry:Enabled is true.");

        var baseEndpoint = settings.OtlpEndpoint;
        var headers = settings.Headers ?? string.Empty;

        void ConfigureExporter(OtlpExporterOptions options, string signalPath)
        {
            options.Endpoint = BuildSignalEndpoint(baseEndpoint, signalPath);
            options.Headers = headers;
            options.Protocol = OtlpExportProtocol.HttpProtobuf;
        }

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddAttributes(BuildResourceAttributes(settings)))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddSource(MongoDB.Driver.MongoTelemetry.ActivitySourceName)
                .AddSource(TracingBehavior.ActivitySourceName)
                .AddSource(AmqpTracePropagation.MessagingSourceName)
                .AddOtlpExporter(options => ConfigureExporter(options, "v1/traces")))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddMeter(ReminderMetrics.MeterName)
                .AddMeter(CacheMetrics.MeterName)
                .AddOtlpExporter((exporter, _) => ConfigureExporter(exporter, "v1/metrics")))
            .WithLogging(logging => logging
                .AddOtlpExporter(options => ConfigureExporter(options, "v1/logs")));

        return services;
    }

    // The SDK only appends the signal path (v1/traces, ...) to endpoints it reads from the environment;
    // an Endpoint assigned in code is used as-is, so the path is added here.
    public static Uri BuildSignalEndpoint(string endpoint, string signalPath) =>
        new(endpoint.TrimEnd('/') + "/" + signalPath.TrimStart('/'));

    private static List<KeyValuePair<string, object>> BuildResourceAttributes(OpenTelemetrySettings settings)
    {
        var attributes = new List<KeyValuePair<string, object>>
        {
            new("service.name", settings.ServiceName ?? string.Empty),
            new(
                "deployment.environment",
                string.IsNullOrWhiteSpace(settings.Environment) ? "local" : settings.Environment),
        };

        var version = System.Environment.GetEnvironmentVariable("SERVICE_VERSION");
        if (!string.IsNullOrEmpty(version))
            attributes.Add(new("service.version", version));

        return attributes;
    }
}
