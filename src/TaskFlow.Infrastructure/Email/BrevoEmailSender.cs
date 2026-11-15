using System.Net.Http.Json;
using TaskFlow.Application.Interfaces;
using TaskFlow.Application.UseCases.Reminders.DeliverDueSoonReminder;
using TaskFlow.Infrastructure.Configuration;

namespace TaskFlow.Infrastructure.Mail;

public sealed class BrevoEmailSender : IEmailSender
{
    private readonly HttpClient _httpClient;
    private readonly BrevoSettings _settings;
    private readonly ReminderEmailRenderer _renderer;

    public BrevoEmailSender(HttpClient httpClient, BrevoSettings settings, ReminderEmailRenderer renderer)
    {
        _httpClient = httpClient;
        _settings = settings;
        _renderer = renderer;
    }

    public async Task<bool> SendAsync(DueReminderEmail email, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(email);

        using var request = new HttpRequestMessage(HttpMethod.Post, "v3/smtp/email");
        request.Headers.TryAddWithoutValidation("api-key", _settings.ApiKey);
        var content = _renderer.Render(email);
        request.Content = JsonContent.Create(new
        {
            sender = new { email = _settings.SenderEmail, name = _settings.SenderName },
            to = new[] { new { email = email.To } },
            subject = email.Subject,
            textContent = content.Text,
            htmlContent = content.Html,
        });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode;
    }
}
