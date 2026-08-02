namespace TaskFlow.Application.Common.Results;

/// <summary>
/// Stable machine-readable error codes returned in API <c>ProblemDetails.Extensions["code"]</c>.
/// </summary>
public static class ErrorCodes
{
    public const string AuthInvalidCredentials = "auth.invalid_credentials";
    public const string AuthMissingOrInvalidSub = "auth.missing_or_invalid_sub";

    public const string UserEmailAlreadyInUse = "user.email_already_in_use";
    public const string UserNotFound = "user.not_found";

    public const string TaskNotFound = "task.not_found";
    public const string TaskStatusTransitionInvalid = "task.status_transition_invalid";

    public const string RequestValidationFailed = "request.validation_failed";
    public const string RequestInvalidArgument = "request.invalid_argument";

    public const string ServerUnexpectedError = "server.unexpected_error";
}
