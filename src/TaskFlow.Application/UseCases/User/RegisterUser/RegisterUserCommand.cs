using MediatR;

namespace TaskFlow.Application.UseCases.User.RegisterUser;

/// <summary>
/// Command to register a new user. Password is plain text here; the handler hashes before persisting.
/// </summary>
public sealed record RegisterUserCommand(string Name, string Email, string Password) : IRequest<RegisterUserResult>;

/// <summary>
/// Result of registration; returns the new user id.
/// </summary>
public sealed record RegisterUserResult(Guid Id);
