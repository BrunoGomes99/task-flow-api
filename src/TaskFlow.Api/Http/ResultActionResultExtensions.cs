using Microsoft.AspNetCore.Mvc;
using TaskFlow.Application.Common.Results;

namespace TaskFlow.Api.Http;

internal static class ResultActionResultExtensions
{
    public static IActionResult ToActionResult(this Result result, HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (result.IsSuccess)
            return new NoContentResult();

        return MapFailure(
            httpContext,
            result.Kind,
            result.Message,
            result.Code,
            result.Resource,
            result.Id);
    }

    public static IActionResult ToHttpActionResult<T>(this Result<T> result, HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (result.IsSuccess)
            return new OkObjectResult(result.Value);

        return MapFailure(
            httpContext,
            result.Kind,
            result.Message,
            result.Code,
            result.Resource,
            result.Id);
    }

    private static ObjectResult MapFailure(
        HttpContext httpContext,
        ApplicationResultKind kind,
        string message,
        string code,
        string resource,
        string? id)
    {
        return kind switch
        {
            ApplicationResultKind.NotFound => Problem(
                httpContext,
                statusCode: StatusCodes.Status404NotFound,
                title: "Not found",
                detail: message,
                code: code,
                resource: resource,
                id: id),
            ApplicationResultKind.Conflict => Problem(
                httpContext,
                statusCode: StatusCodes.Status409Conflict,
                title: "Conflict",
                detail: message,
                code: code,
                resource: resource,
                id: id),
            ApplicationResultKind.Unauthorized => Problem(
                httpContext,
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized",
                detail: message,
                code: code,
                resource: resource,
                id: id),
            ApplicationResultKind.BadRequest => Problem(
                httpContext,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad request",
                detail: message,
                code: code,
                resource: resource,
                id: id),
            _ => Problem(
                httpContext,
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Server error",
                detail: "An unexpected error occurred.",
                code: ErrorCodes.ServerUnexpectedError,
                resource: string.Empty,
                id: null),
        };
    }

    private static ObjectResult Problem(
        HttpContext httpContext,
        int statusCode,
        string title,
        string detail,
        string code,
        string resource,
        string? id)
    {
        var problem = new ProblemDetails
        {
            Title = title,
            Detail = detail,
            Status = statusCode,
        };

        problem.ApplyTraceId(httpContext);
        problem.ApplyErrorCode(code);
        problem.ApplyResourceMetadata(resource, id);

        return new ObjectResult(problem) { StatusCode = statusCode };
    }
}
