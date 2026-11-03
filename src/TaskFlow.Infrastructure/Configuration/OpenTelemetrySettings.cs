namespace TaskFlow.Infrastructure.Configuration;

public sealed class OpenTelemetrySettings
{
    public const string SectionName = "OpenTelemetry";

    public bool Enabled { get; set; }

    public string OtlpEndpoint { get; set; } = string.Empty;

    public string Headers { get; set; } = string.Empty;

    public string ServiceName { get; set; } = string.Empty;

    public string Environment { get; set; } = "local";
}
