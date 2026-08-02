using MediatR;
using TaskFlow.Application.Common.Results;
using TaskFlow.Application.DTOs;

namespace TaskFlow.Application.UseCases.User.LoginUser;

/// <summary>
/// Command to authenticate with email and password. Returns JWT access token and expiry when successful.
/// </summary>
public sealed record LoginUserCommand(string Email, string Password) : IRequest<Result<LoginResponse>>;
