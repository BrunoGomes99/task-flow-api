using System.Diagnostics;
using System.Text;
using TaskFlow.Infrastructure.Observability;

namespace TaskFlow.Infrastructure.Tests.Observability;

public sealed class AmqpTracePropagationTests
{
    [Fact]
    public void Inject_ShouldWriteTraceparent_WhenAnActivityIsCurrent()
    {
        using var listener = CreateListener();
        using var source = new ActivitySource(AmqpTracePropagation.MessagingSourceName);
        using var activity = source.StartActivity("DueSoon publish", ActivityKind.Producer);
        Assert.NotNull(activity);
        var headers = new Dictionary<string, object?>();

        AmqpTracePropagation.Inject(headers, activity);

        var traceparent = Assert.IsType<string>(headers["traceparent"]);
        Assert.Contains(activity.TraceId.ToHexString(), traceparent, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_ShouldReturnTheSameTraceId_ForInjectedHeaders()
    {
        using var listener = CreateListener();
        using var source = new ActivitySource(AmqpTracePropagation.MessagingSourceName);
        using var activity = source.StartActivity("DueSoon publish", ActivityKind.Producer);
        Assert.NotNull(activity);
        var headers = new Dictionary<string, object?>();
        AmqpTracePropagation.Inject(headers, activity);

        var context = AmqpTracePropagation.Extract(headers);

        Assert.Equal(activity.TraceId, context.ActivityContext.TraceId);
        Assert.Equal(activity.SpanId, context.ActivityContext.SpanId);
    }

    [Fact]
    public void Extract_ShouldNotThrow_WhenHeadersAreNull()
    {
        var context = AmqpTracePropagation.Extract(null);

        Assert.Equal(default, context.ActivityContext);
    }

    [Fact]
    public void Extract_ShouldReadByteArrayTraceparent_LikeAString()
    {
        const string traceparent = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01";
        var headers = new Dictionary<string, object?>
        {
            ["traceparent"] = Encoding.UTF8.GetBytes(traceparent),
        };

        var context = AmqpTracePropagation.Extract(headers);

        Assert.Equal("0af7651916cd43dd8448eb211c80319c", context.ActivityContext.TraceId.ToHexString());
    }

    [Fact]
    public void Extract_ShouldIgnoreAnUnreadableTraceparent()
    {
        var headers = new Dictionary<string, object?>
        {
            ["traceparent"] = 42,
        };

        var context = AmqpTracePropagation.Extract(headers);

        Assert.Equal(default, context.ActivityContext);
    }

    [Fact]
    public void CopyContext_ShouldCopyTraceparentAndTracestate_OnlyWhenPresent()
    {
        var incoming = new Dictionary<string, object?>
        {
            ["traceparent"] = Encoding.UTF8.GetBytes("00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01"),
            ["tracestate"] = "vendor=value",
            ["x-reminder-attempt"] = 2,
        };
        var outgoing = new Dictionary<string, object?>();

        AmqpTracePropagation.CopyContext(incoming, outgoing);

        Assert.Equal("00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01", outgoing["traceparent"]);
        Assert.Equal("vendor=value", outgoing["tracestate"]);
        Assert.False(outgoing.ContainsKey("x-reminder-attempt"));
    }

    private static ActivityListener CreateListener()
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AmqpTracePropagation.MessagingSourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }
}
