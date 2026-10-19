namespace TaskFlow.Infrastructure.Configuration;

public sealed class BrevoSettings
{
    public const string SectionName = "Brevo";

    public string ApiKey { get; set; } = string.Empty;

    public string SenderEmail { get; set; } = string.Empty;

    public string SenderName { get; set; } = "TaskFlow";
}
