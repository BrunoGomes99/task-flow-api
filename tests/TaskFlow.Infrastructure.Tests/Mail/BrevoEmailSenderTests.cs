using System.Net;
using System.Text.Json;
using TaskFlow.Application.UseCases.Reminders.DeliverDueSoonReminder;
using TaskFlow.Infrastructure.Configuration;
using TaskFlow.Infrastructure.Mail;

namespace TaskFlow.Infrastructure.Tests.Mail;

public sealed class BrevoEmailSenderTests
{
    [Fact]
    public async System.Threading.Tasks.Task SendAsync_PostsHtmlAndTextContent()
    {
        var settings = new ReminderEmailSettings
        {
            WebBaseUrl = "http://localhost:4200",
            LogoUrl = "https://cdn.example.com/email/logo.png",
        };
        var renderer = new ReminderEmailRenderer(settings);
        var email = DueReminderEmail.Compose(
            "ana@example.com",
            "Relatório",
            new DateTime(2026, 11, 21, 2, 59, 59, DateTimeKind.Utc));
        var expected = renderer.Render(email);
        var handler = new CaptureHandler();
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.brevo.com/") };
        var sender = new BrevoEmailSender(
            httpClient,
            new BrevoSettings
            {
                ApiKey = "key",
                SenderEmail = "noreply@taskflow.test",
                SenderName = "TaskFlow",
            },
            renderer);

        var sent = await sender.SendAsync(email);

        Assert.True(sent);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal("https://api.brevo.com/v3/smtp/email", handler.RequestUri?.ToString());
        using var document = JsonDocument.Parse(handler.Body);
        Assert.Equal(expected.Text, document.RootElement.GetProperty("textContent").GetString());
        Assert.Equal(expected.Html, document.RootElement.GetProperty("htmlContent").GetString());
        Assert.Equal("key", handler.ApiKey);
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public string Body { get; private set; } = string.Empty;

        public string? ApiKey { get; private set; }

        public HttpMethod? Method { get; private set; }

        public Uri? RequestUri { get; private set; }

        protected override async System.Threading.Tasks.Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Method = request.Method;
            RequestUri = request.RequestUri;
            ApiKey = request.Headers.TryGetValues("api-key", out var values) ? values.Single() : null;
            Body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.Created);
        }
    }
}
