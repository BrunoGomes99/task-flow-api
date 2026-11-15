using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Application.Interfaces;
using TaskFlow.Infrastructure.Configuration;
using TaskFlow.Infrastructure.Extensions;
using TaskFlow.Infrastructure.Mail;

namespace TaskFlow.Infrastructure.Tests.Configuration;

public sealed class ReminderEmailConfigurationTests
{
    [Fact]
    public void AddDueSoonNotifications_RegistersTheHtmlEmailSender()
    {
        var services = new ServiceCollection();

        services.AddDueSoonNotifications(Configuration());

        using var provider = services.BuildServiceProvider();

        Assert.IsType<BrevoEmailSender>(provider.GetRequiredService<IEmailSender>());
    }

    [Fact]
    public void AddDueSoonNotifications_RequiresAnAbsoluteLogoUrl()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddDueSoonNotifications(Configuration(logoUrl: "")));

        Assert.Contains("ReminderEmail:LogoUrl", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddDueSoonNotifications_RequiresAnAbsoluteWebBaseUrl()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddDueSoonNotifications(Configuration(webBaseUrl: "tasks")));

        Assert.Contains("ReminderEmail:WebBaseUrl", exception.Message, StringComparison.Ordinal);
    }

    private static IConfiguration Configuration(
        string webBaseUrl = "http://localhost:4200",
        string logoUrl = "https://cdn.example.com/email/logo.png") =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RabbitMq:Host"] = "localhost",
                ["RabbitMq:Port"] = "5672",
                ["RabbitMq:User"] = "taskflow",
                ["RabbitMq:Password"] = "taskflow",
                ["Brevo:ApiKey"] = "key",
                ["Brevo:SenderEmail"] = "noreply@taskflow.test",
                [$"{ReminderEmailSettings.SectionName}:WebBaseUrl"] = webBaseUrl,
                [$"{ReminderEmailSettings.SectionName}:LogoUrl"] = logoUrl,
            })
            .Build();
}
