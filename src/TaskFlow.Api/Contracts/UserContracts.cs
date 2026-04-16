namespace TaskFlow.Api.Contracts;

public sealed record RegisterUserRequest(string Name, string Email, string Password);

public sealed record LoginUserRequest(string Email, string Password);

public sealed record RegisterUserResponse(Guid Id);
