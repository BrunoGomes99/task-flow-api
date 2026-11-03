using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;

namespace TaskFlow.Infrastructure.Observability;

public static class AmqpTracePropagation
{
    public const string MessagingSourceName = "TaskFlow.Messaging";

    private const string TraceParentHeader = "traceparent";
    private const string TraceStateHeader = "tracestate";

    public static readonly ActivitySource Source = new(MessagingSourceName);

    static AmqpTracePropagation()
    {
        // The SDK installs the W3C trace-context propagator as the default one when it initializes;
        // initialize it here so the helper behaves the same before and after the host builds the SDK.
        RuntimeHelpers.RunClassConstructor(typeof(Sdk).TypeHandle);
    }

    public static void Inject(IDictionary<string, object?> headers, Activity? activity = null)
    {
        ArgumentNullException.ThrowIfNull(headers);

        var current = activity ?? Activity.Current;
        if (current is null)
            return;

        Propagators.DefaultTextMapPropagator.Inject(
            new PropagationContext(current.Context, default(Baggage)),
            headers,
            static (carrier, key, value) => carrier[key] = value);
    }

    public static PropagationContext Extract(IDictionary<string, object?>? headers)
    {
        if (headers is null)
            return default;

        return Propagators.DefaultTextMapPropagator.Extract(default, headers, GetValues);
    }

    public static Activity? StartConsumer(string name, IDictionary<string, object?>? headers)
    {
        var parent = Extract(headers).ActivityContext;
        return parent == default
            ? Source.StartActivity(name, ActivityKind.Consumer)
            : Source.StartActivity(name, ActivityKind.Consumer, parent);
    }

    public static void CopyContext(IDictionary<string, object?>? incoming, IDictionary<string, object?> outgoing)
    {
        ArgumentNullException.ThrowIfNull(outgoing);

        if (incoming is null)
            return;

        foreach (var key in new[] { TraceParentHeader, TraceStateHeader })
        {
            if (incoming.TryGetValue(key, out var value) && ReadText(value) is { Length: > 0 } text)
                outgoing[key] = text;
        }
    }

    private static IEnumerable<string>? GetValues(IDictionary<string, object?> headers, string key)
    {
        if (!headers.TryGetValue(key, out var value) || ReadText(value) is not { } text)
            return null;

        return [text];
    }

    private static string? ReadText(object? value) => value switch
    {
        string text => text,
        byte[] bytes => Encoding.UTF8.GetString(bytes),
        _ => null,
    };
}
