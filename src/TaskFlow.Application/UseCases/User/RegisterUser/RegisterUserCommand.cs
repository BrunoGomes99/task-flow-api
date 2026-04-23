using MediatR;
using TaskFlow.Application.Common.Results;

namespace TaskFlow.Application.UseCases.User.RegisterUser;

/// <summary>
/// Command to register a new user. Password is plain text here; the handler hashes before persisting.
/// </summary>
public sealed record RegisterUserCommand(string Name, string Email, string Password) : IRequest<Result<RegisterUserResult>>;

/// <summary>
/// Result of registration; returns the new user id.
/// </summary>
public sealed record RegisterUserResult(Guid Id);
