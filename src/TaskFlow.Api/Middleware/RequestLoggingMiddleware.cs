using System.Diagnostics;
using TaskFlow.Api.Http;

namespace TaskFlow.Api.Middleware;

public sealed class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (IsHealthPath(httpContext.Request.Path))
        {
            await next(httpContext).ConfigureAwait(false);
            return;
        }

        var traceId = TelemetryTrace.Current(httpContext);
        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["TraceId"] = traceId,
        });

        var stopwatch = Stopwatch.StartNew();
        try
        {
            await next(httpContext).ConfigureAwait(false);
        }
        finally
        {
            stopwatch.Stop();
            logger.LogInformation(
                "HTTP {Method} {Route} responded {StatusCode} in {ElapsedMilliseconds} ms",
                httpContext.Request.Method,
                ResolveRoute(httpContext),
                httpContext.Response.StatusCode,
                stopwatch.ElapsedMilliseconds);
        }
    }

    private static bool IsHealthPath(PathString path)
    {
        return path.Equals("/health", StringComparison.Ordinal)
            || path.Equals("/health/live", StringComparison.Ordinal)
            || path.Equals("/health/ready", StringComparison.Ordinal);
    }

    private static string ResolveRoute(HttpContext httpContext)
    {
        if (httpContext.GetEndpoint() is RouteEndpoint endpoint
            && !string.IsNullOrWhiteSpace(endpoint.RoutePattern.RawText))
        {
            return endpoint.RoutePattern.RawText;
        }

        return httpContext.Request.Path.Value ?? string.Empty;
    }
}
