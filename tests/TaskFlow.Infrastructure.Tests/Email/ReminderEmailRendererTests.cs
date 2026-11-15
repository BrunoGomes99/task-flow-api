using TaskFlow.Application.UseCases.Reminders.DeliverDueSoonReminder;
using TaskFlow.Infrastructure.Configuration;
using TaskFlow.Infrastructure.Mail;

namespace TaskFlow.Infrastructure.Tests.Mail;

public sealed class ReminderEmailRendererTests
{
    private static readonly DateTime DueAtUtc = new(2026, 11, 21, 2, 59, 59, DateTimeKind.Utc);

    [Fact]
    public void Render_ShowsSaoPauloDate_EncodesTitle_AndLinksToTheApp()
    {
        var renderer = Renderer();
        var email = DueReminderEmail.Compose(
            "ana@example.com",
            "Relatório <b>A & B</b>",
            DueAtUtc);

        var content = renderer.Render(email);

        Assert.Equal(
            "Sua tarefa \"Relatório <b>A & B</b>\" vence em 20/11/2026. Acesse: http://localhost:4200",
            content.Text);
        Assert.Contains("Relatório &lt;b&gt;A &amp; B&lt;/b&gt;", content.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("<b>", content.Html, StringComparison.Ordinal);
        Assert.Contains("20/11/2026", content.Html, StringComparison.Ordinal);
        Assert.Contains("href=\"http://localhost:4200\"", content.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("/tasks/", content.Html, StringComparison.Ordinal);
        Assert.Contains(
            "https://cdn.example.com/email/logo.png?v=1&amp;x=2",
            content.Html,
            StringComparison.Ordinal);
        Assert.DoesNotContain("cid:", content.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("{{", content.Html, StringComparison.Ordinal);
        Assert.Contains("LEMBRETE DE VENCIMENTO", content.Html, StringComparison.Ordinal);
        Assert.Contains("Acessar minha tarefa", content.Html, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_DoesNotTreatTheTitleAsATemplatePlaceholder()
    {
        var email = DueReminderEmail.Compose("ana@example.com", "{{dueDate}}", DueAtUtc);

        var content = Renderer().Render(email);

        Assert.Contains("{{dueDate}}", content.Html, StringComparison.Ordinal);
        Assert.Equal(1, CountOf(content.Html, "20/11/2026"));
        Assert.Contains("{{dueDate}}", content.Text, StringComparison.Ordinal);
    }

    private static ReminderEmailRenderer Renderer() =>
        new(new ReminderEmailSettings
        {
            WebBaseUrl = "http://localhost:4200/",
            LogoUrl = "https://cdn.example.com/email/logo.png?v=1&x=2",
        });

    private static int CountOf(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}
