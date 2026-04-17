namespace TaskFlow.Application.Common.Results;

/// <summary>
/// Query outcome with payload on success.
/// </summary>
public readonly struct Result<T>
{
    public ApplicationResultKind Kind { get; }

    public T? Value { get; }

    public string Code { get; }

    public string Message { get; }

    public string Resource { get; }

    public string? Id { get; }
    public bool IsSuccess => Kind == ApplicationResultKind.Success;

    private Result(ApplicationResultKind kind, T? value, string code, string message, string resource, string? id)
    {
        Kind = kind;
        Value = value;
        Code = code;
        Message = message;
        Resource = resource;
        Id = id;
    }

    public static Result<T> Ok(T value) =>
        new(ApplicationResultKind.Success, value, string.Empty, string.Empty, string.Empty, null);

    public static Result<T> NotFound(string code, string message, string resource, string? id = null) =>
        new(ApplicationResultKind.NotFound, default, code, message, resource, id);

    public static Result<T> Conflict(string code, string message, string resource, string? id = null) =>
        new(ApplicationResultKind.Conflict, default, code, message, resource, id);

    public static Result<T> Unauthorized(string code, string message, string resource, string? id = null) =>
        new(ApplicationResultKind.Unauthorized, default, code, message, resource, id);

    public static Result<T> BadRequest(string code, string message, string resource, string? id = null) =>
        new(ApplicationResultKind.BadRequest, default, code, message, resource, id);
}
