using System.Diagnostics;

namespace TaskFlow.Api.Http;

internal static class TelemetryTrace
{
    private const string EmptyTraceId = "00000000000000000000000000000000";

    public static string Current(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var activity = Activity.Current;
        if (activity is null)
        {
            return httpContext.TraceIdentifier;
        }

        var traceId = activity.TraceId.ToString();
        if (traceId == EmptyTraceId)
        {
            return httpContext.TraceIdentifier;
        }

        return traceId;
    }
}
