namespace TaskFlow.Application.Common.Results;

/// <summary>
/// Command outcome without payload. Success is represented as <see cref="ApplicationResultKind.Success"/>.
/// </summary>
public readonly struct Result
{
    public ApplicationResultKind Kind { get; }

    public string Code { get; }

    public string Message { get; }

    public string Resource { get; }

    public string? Id { get; }

    public bool IsSuccess => Kind == ApplicationResultKind.Success;

    private Result(ApplicationResultKind kind, string code, string message, string resource, string? id)
    {
        Kind = kind;
        Code = code;
        Message = message;
        Resource = resource;
        Id = id;
    }

    public static Result Success() =>
        new(ApplicationResultKind.Success, string.Empty, string.Empty, string.Empty, null);

    public static Result NotFound(string code, string message, string resource, string? id = null) =>
        new(ApplicationResultKind.NotFound, code, message, resource, id);

    public static Result Conflict(string code, string message, string resource, string? id = null) =>
        new(ApplicationResultKind.Conflict, code, message, resource, id);

    public static Result Unauthorized(string code, string message, string resource, string? id = null) =>
        new(ApplicationResultKind.Unauthorized, code, message, resource, id);

    public static Result BadRequest(string code, string message, string resource, string? id = null) =>
        new(ApplicationResultKind.BadRequest, code, message, resource, id);
}
