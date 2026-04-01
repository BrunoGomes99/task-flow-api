namespace TaskFlow.Infrastructure.Configuration;

public sealed class PasswordHashingSettings
{
    public const string SectionName = "PasswordHashing";

    /// <summary>
    /// BCrypt cost parameter (4–31). Higher values increase CPU time per hash.
    /// </summary>
    public int WorkFactor { get; set; } = 12;
}
