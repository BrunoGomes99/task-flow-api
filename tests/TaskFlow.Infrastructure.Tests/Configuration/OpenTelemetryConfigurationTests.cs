using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using TaskFlow.Infrastructure.Configuration;
using TaskFlow.Infrastructure.Observability;

namespace TaskFlow.Infrastructure.Tests.Configuration;

public sealed class OpenTelemetryConfigurationTests
{
    private const string SecretHeader = "Authorization=Basic do-not-log-this-header";

    [Fact]
    public void AddTaskFlowOpenTelemetry_ShouldRegisterNothing_WhenDisabled()
    {
        var configuration = BuildConfiguration(enabled: "false");
        var services = new ServiceCollection();

        services.AddTaskFlowOpenTelemetry(configuration);

        Assert.Empty(services);
    }

    [Fact]
    public void AddTaskFlowOpenTelemetry_ShouldRegisterNothing_WhenDisabledEvenWithEndpointAndHeaders()
    {
        var configuration = BuildConfiguration(
            enabled: "false",
            endpoint: "https://otlp-gateway.example.invalid/otlp",
            headers: SecretHeader);
        var services = new ServiceCollection();

        services.AddTaskFlowOpenTelemetry(configuration);

        Assert.DoesNotContain(services, service => service.ServiceType == typeof(TracerProvider));
        Assert.DoesNotContain(services, service => service.ServiceType == typeof(MeterProvider));
        Assert.Empty(services);
    }

    [Fact]
    public void AddTaskFlowOpenTelemetry_ShouldRegisterNothing_WhenSectionMissing()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();

        services.AddTaskFlowOpenTelemetry(configuration);

        Assert.Empty(services);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddTaskFlowOpenTelemetry_ShouldThrow_WhenEnabledWithoutEndpoint(string? endpoint)
    {
        var configuration = BuildConfiguration(enabled: "true", endpoint: endpoint, headers: SecretHeader);
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddTaskFlowOpenTelemetry(configuration));

        Assert.Contains("OpenTelemetry:OtlpEndpoint", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("do-not-log-this-header", exception.Message, StringComparison.Ordinal);
        Assert.Empty(services);
    }

    [Fact]
    public void AddTaskFlowOpenTelemetry_ShouldRegisterTracesMetricsAndLogs_WhenEnabledWithEndpoint()
    {
        var configuration = BuildConfiguration(
            enabled: "true",
            endpoint: "https://otlp-gateway.example.invalid/otlp",
            headers: SecretHeader);
        var services = new ServiceCollection();

        services.AddTaskFlowOpenTelemetry(configuration);

        Assert.Contains(services, service => service.ServiceType == typeof(TracerProvider));
        Assert.Contains(services, service => service.ServiceType == typeof(MeterProvider));
        Assert.Contains(services, service => service.ServiceType == typeof(LoggerProvider));
    }

    [Fact]
    public void AddTaskFlowOpenTelemetry_ShouldNotWriteHeadersToTheConsole()
    {
        var originalOut = Console.Out;
        using var captured = new StringWriter();
        Console.SetOut(captured);
        try
        {
            var services = new ServiceCollection();
            services.AddTaskFlowOpenTelemetry(BuildConfiguration(
                enabled: "true",
                endpoint: "https://otlp-gateway.example.invalid/otlp",
                headers: SecretHeader));

            var disabled = new ServiceCollection();
            disabled.AddTaskFlowOpenTelemetry(BuildConfiguration(
                enabled: "false",
                endpoint: "https://otlp-gateway.example.invalid/otlp",
                headers: SecretHeader));

            var thrown = Assert.Throws<InvalidOperationException>(() =>
                new ServiceCollection().AddTaskFlowOpenTelemetry(BuildConfiguration(
                    enabled: "true",
                    endpoint: "",
                    headers: SecretHeader)));
            Assert.DoesNotContain("do-not-log-this-header", thrown.Message, StringComparison.Ordinal);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        Assert.DoesNotContain("do-not-log-this-header", captured.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://gateway.example.invalid/otlp", "v1/traces", "https://gateway.example.invalid/otlp/v1/traces")]
    [InlineData("https://gateway.example.invalid/otlp/", "v1/metrics", "https://gateway.example.invalid/otlp/v1/metrics")]
    [InlineData("http://collector.example.invalid:4318", "v1/logs", "http://collector.example.invalid:4318/v1/logs")]
    public void BuildSignalEndpoint_ShouldAppendTheSignalPath_ToTheConfiguredBase(string endpoint, string signalPath, string expected)
    {
        var uri = OpenTelemetryRegistration.BuildSignalEndpoint(endpoint, signalPath);

        Assert.Equal(expected, uri.AbsoluteUri);
    }

    [Fact]
    public void OpenTelemetrySettings_ShouldHaveSafeDefaults()
    {
        var settings = new OpenTelemetrySettings();

        Assert.Equal("OpenTelemetry", OpenTelemetrySettings.SectionName);
        Assert.False(settings.Enabled);
        Assert.Equal(string.Empty, settings.OtlpEndpoint);
        Assert.Equal(string.Empty, settings.Headers);
        Assert.Equal(string.Empty, settings.ServiceName);
        Assert.Equal("local", settings.Environment);
    }

    private static IConfiguration BuildConfiguration(string enabled, string? endpoint = null, string? headers = null) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{OpenTelemetrySettings.SectionName}:Enabled"] = enabled,
                [$"{OpenTelemetrySettings.SectionName}:OtlpEndpoint"] = endpoint,
                [$"{OpenTelemetrySettings.SectionName}:Headers"] = headers,
                [$"{OpenTelemetrySettings.SectionName}:ServiceName"] = "taskflow-test",
                [$"{OpenTelemetrySettings.SectionName}:Environment"] = "test",
            })
            .Build();
}
