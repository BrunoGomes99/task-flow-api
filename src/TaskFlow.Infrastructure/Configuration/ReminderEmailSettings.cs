namespace TaskFlow.Infrastructure.Configuration;

public sealed class ReminderEmailSettings
{
    public const string SectionName = "ReminderEmail";

    public string WebBaseUrl { get; set; } = string.Empty;

    public string LogoUrl { get; set; } = string.Empty;
}
