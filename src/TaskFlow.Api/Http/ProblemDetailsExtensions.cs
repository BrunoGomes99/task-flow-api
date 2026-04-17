using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace TaskFlow.Api.Http;

internal static class ProblemDetailsExtensions
{
    public static void ApplyTraceId(this ProblemDetails problem, HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentNullException.ThrowIfNull(httpContext);

        problem.Extensions["traceId"] = httpContext.TraceIdentifier;
    }

    public static void ApplyErrorCode(this ProblemDetails problem, string code)
    {
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        problem.Extensions["code"] = code;
    }

    public static void ApplyResourceMetadata(this ProblemDetails problem, string resource, string? id)
    {
        ArgumentNullException.ThrowIfNull(problem);

        if (!string.IsNullOrWhiteSpace(resource))
            problem.Extensions["resource"] = resource;

        if (!string.IsNullOrWhiteSpace(id))
            problem.Extensions["id"] = id;
    }
}
