using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Application.Interfaces;
using TaskFlow.Application.UseCases.Reminders.DeliverDueSoonReminder;
using TaskFlow.Application.UseCases.Reminders.PublishDueSoonReminders;
using TaskFlow.Infrastructure.Configuration;
using TaskFlow.Infrastructure.Mail;
using TaskFlow.Infrastructure.Messaging;

namespace TaskFlow.Infrastructure.Extensions;

public static class DueReminderServiceCollectionExtensions
{
    public static IServiceCollection AddDueSoonScheduler(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton(RequireRabbitMq(configuration));
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<DueSoonReminderPublish>();
        services.AddSingleton<IDueSoonPublisher, DueSoonPublisher>();
        return services;
    }

    public static IServiceCollection AddDueSoonNotifications(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton(RequireRabbitMq(configuration));
        services.AddScoped<DueSoonReminderDelivery>();

        var brevo = configuration.GetSection(BrevoSettings.SectionName).Get<BrevoSettings>() ?? new BrevoSettings();
        if (string.IsNullOrWhiteSpace(brevo.ApiKey))
            throw new InvalidOperationException("Brevo:ApiKey must be configured.");
        if (string.IsNullOrWhiteSpace(brevo.SenderEmail))
            throw new InvalidOperationException("Brevo:SenderEmail must be configured.");
        if (string.IsNullOrWhiteSpace(brevo.SenderName))
            brevo.SenderName = "TaskFlow";

        services.AddSingleton(brevo);
        services.AddHttpClient<IEmailSender, BrevoEmailSender>(client =>
        {
            client.BaseAddress = new Uri("https://api.brevo.com/");
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddHostedService<DueSoonConsumer>();
        return services;
    }

    private static RabbitMqSettings RequireRabbitMq(IConfiguration configuration)
    {
        var settings = configuration.GetSection(RabbitMqSettings.SectionName).Get<RabbitMqSettings>() ?? new RabbitMqSettings();
        if (string.IsNullOrWhiteSpace(settings.Host))
            throw new InvalidOperationException("RabbitMq:Host must be configured.");
        if (string.IsNullOrWhiteSpace(settings.User) || string.IsNullOrWhiteSpace(settings.Password))
            throw new InvalidOperationException("RabbitMq:User and RabbitMq:Password must be configured.");
        if (settings.Port <= 0)
            throw new InvalidOperationException("RabbitMq:Port must be a positive number.");

        return settings;
    }
}
