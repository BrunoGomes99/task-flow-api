using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using TaskFlow.Application.UseCases.Reminders;
using TaskFlow.Application.UseCases.Reminders.DeliverDueSoonReminder;
using TaskFlow.Infrastructure.Configuration;

namespace TaskFlow.Infrastructure.Mail;

public sealed record ReminderEmailContent(string Text, string Html);

public sealed class ReminderEmailRenderer
{
    private const string TemplateResourceName = "TaskFlow.Infrastructure.Email.Templates.DueReminder.html";
    private static readonly HtmlEncoder Html = HtmlEncoder.Create(new TextEncoderSettings(UnicodeRanges.All));
    private static readonly TimeZoneInfo SaoPaulo = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    private readonly ReminderEmailSettings _settings;
    private readonly string _template;

    public ReminderEmailRenderer(ReminderEmailSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings;
        _template = LoadTemplate();
    }

    public ReminderEmailContent Render(DueReminderEmail email)
    {
        ArgumentNullException.ThrowIfNull(email);

        var dueDate = FormatDueDate(email.DueDateUtc);
        var appUrl = _settings.WebBaseUrl.TrimEnd('/');
        var html = Apply(_template, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["taskName"] = Html.Encode(email.Title),
            ["dueDate"] = Html.Encode(dueDate),
            ["taskUrl"] = Html.Encode(appUrl),
            ["logoUrl"] = Html.Encode(_settings.LogoUrl),
        });
        var text = $"Sua tarefa \"{email.Title}\" vence em {dueDate}. Acesse: {appUrl}";
        return new ReminderEmailContent(text, html);
    }

    private static string FormatDueDate(DateTime dueDateUtc)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DueInstant.NormalizeUtc(dueDateUtc), SaoPaulo);
        return local.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
    }

    private static string Apply(string template, IReadOnlyDictionary<string, string> values)
    {
        var result = new StringBuilder(template.Length);
        var index = 0;
        while (index < template.Length)
        {
            var start = template.IndexOf("{{", index, StringComparison.Ordinal);
            if (start < 0)
            {
                result.Append(template, index, template.Length - index);
                break;
            }

            var end = template.IndexOf("}}", start + 2, StringComparison.Ordinal);
            if (end < 0)
                throw new InvalidOperationException("Reminder email template has an unclosed placeholder.");

            result.Append(template, index, start - index);
            var key = template.Substring(start + 2, end - start - 2);
            if (!values.TryGetValue(key, out var value))
                throw new InvalidOperationException($"Unknown reminder email placeholder '{key}'.");

            result.Append(value);
            index = end + 2;
        }

        return result.ToString();
    }

    private static string LoadTemplate()
    {
        using var stream = typeof(ReminderEmailRenderer).Assembly.GetManifestResourceStream(TemplateResourceName)
            ?? throw new InvalidOperationException($"Missing embedded resource '{TemplateResourceName}'.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
