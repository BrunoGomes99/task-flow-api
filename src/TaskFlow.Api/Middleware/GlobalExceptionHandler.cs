using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Application.Common.Exceptions;
using TaskFlow.Domain.Exceptions;

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

        var (statusCode, problem) = MapException(exception, environment);

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken).ConfigureAwait(false);

        return true;
    }

    private static (int StatusCode, object Problem) MapException(Exception exception, IHostEnvironment environment)
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
                return (StatusCodes.Status400BadRequest, validationProblem);
            }

            case NotFoundException nf:
                return (StatusCodes.Status404NotFound, new ProblemDetails
                {
                    Title = "Not found",
                    Detail = nf.Message,
                    Status = StatusCodes.Status404NotFound,
                });

            case ConflictException cx:
                return (StatusCodes.Status409Conflict, new ProblemDetails
                {
                    Title = "Conflict",
                    Detail = cx.Message,
                    Status = StatusCodes.Status409Conflict,
                });

            case BusinessRuleViolationException br:
                return (StatusCodes.Status409Conflict, new ProblemDetails
                {
                    Title = "Conflict",
                    Detail = br.Message,
                    Status = StatusCodes.Status409Conflict,
                });

            case TaskStatusTransitionException tst:
                return (StatusCodes.Status409Conflict, new ProblemDetails
                {
                    Title = "Conflict",
                    Detail = tst.Message,
                    Status = StatusCodes.Status409Conflict,
                });

            case UnauthorizedAccessException ua:
                return (StatusCodes.Status401Unauthorized, new ProblemDetails
                {
                    Title = "Unauthorized",
                    Detail = ua.Message,
                    Status = StatusCodes.Status401Unauthorized,
                });

            case ArgumentException ae:
                return (StatusCodes.Status400BadRequest, new ProblemDetails
                {
                    Title = "Bad request",
                    Detail = ae.Message,
                    Status = StatusCodes.Status400BadRequest,
                });

            default:
                return (StatusCodes.Status500InternalServerError, new ProblemDetails
                {
                    Title = "Server error",
                    Detail = environment.IsDevelopment() ? exception.ToString() : "An unexpected error occurred.",
                    Status = StatusCodes.Status500InternalServerError,
                });
        }
    }
}
