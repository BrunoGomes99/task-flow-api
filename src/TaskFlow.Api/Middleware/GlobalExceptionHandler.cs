using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Http;
using TaskFlow.Application.Common.Results;
namespace TaskFlow.Api.Middleware;

public sealed class GlobalExceptionHandler(IHostEnvironment environment) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        var (statusCode, problem) = MapException(exception, environment, httpContext);

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken).ConfigureAwait(false);

        return true;
    }

    private static (int StatusCode, object Problem) MapException(
        Exception exception,
        IHostEnvironment environment,
        HttpContext httpContext)
    {
        switch (exception)
        {
            case ValidationException vex:
            {
                var errors = vex.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

                var validationProblem = new ValidationProblemDetails(errors)
                {
                    Title = "One or more validation errors occurred.",
                    Status = StatusCodes.Status400BadRequest,
                };

                validationProblem.ApplyTraceId(httpContext);
                validationProblem.ApplyErrorCode(ErrorCodes.RequestValidationFailed);
                return (StatusCodes.Status400BadRequest, validationProblem);
            }

            case UnauthorizedAccessException ua:
            {
                var problem = new ProblemDetails
                {
                    Title = "Unauthorized",
                    Detail = ua.Message,
                    Status = StatusCodes.Status401Unauthorized,
                };

                problem.ApplyTraceId(httpContext);
                problem.ApplyErrorCode(MapUnauthorizedCode(ua));
                problem.ApplyResourceMetadata("auth", id: null);
                return (StatusCodes.Status401Unauthorized, problem);
            }

            case ArgumentException ae:
            {
                var problem = new ProblemDetails
                {
                    Title = "Bad request",
                    Detail = ae.Message,
                    Status = StatusCodes.Status400BadRequest,
                };

                problem.ApplyTraceId(httpContext);
                problem.ApplyErrorCode(ErrorCodes.RequestInvalidArgument);
                return (StatusCodes.Status400BadRequest, problem);
            }

            default:
            {
                var problem = new ProblemDetails
                {
                    Title = "Server error",
                    Detail = environment.IsDevelopment() ? exception.ToString() : "An unexpected error occurred.",
                    Status = StatusCodes.Status500InternalServerError,
                };

                problem.ApplyTraceId(httpContext);
                problem.ApplyErrorCode(ErrorCodes.ServerUnexpectedError);
                return (StatusCodes.Status500InternalServerError, problem);
            }
        }
    }

    private static string MapUnauthorizedCode(UnauthorizedAccessException ua)
    {
        return ua.Message.Contains("sub", StringComparison.OrdinalIgnoreCase)
            ? ErrorCodes.AuthMissingOrInvalidSub
            : ErrorCodes.AuthInvalidCredentials;
    }
}
