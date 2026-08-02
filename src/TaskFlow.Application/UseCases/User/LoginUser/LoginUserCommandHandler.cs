using MediatR;
using TaskFlow.Application.Common.Results;
using TaskFlow.Application.DTOs;
using TaskFlow.Application.Interfaces;
using TaskFlow.Domain.ValueObjects;

namespace TaskFlow.Application.UseCases.User.LoginUser;

/// <summary>
/// Handles login: loads user by email, verifies password, issues JWT.
/// </summary>
public sealed class LoginUserCommandHandler : IRequestHandler<LoginUserCommand, Result<LoginResponse>>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtService _jwtService;

    public LoginUserCommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IJwtService jwtService)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _jwtService = jwtService;
    }

    public async Task<Result<LoginResponse>> Handle(LoginUserCommand request, CancellationToken cancellationToken)
    {
        var email = Email.Create(request.Email);
        var user = await _userRepository.GetByEmailAsync(email, cancellationToken);

        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            return Result<LoginResponse>.Unauthorized(
                ErrorCodes.AuthInvalidCredentials,
                "Invalid email or password.",
                resource: "auth");
        }

        var (accessToken, expiresInSeconds) = await _jwtService.CreateAccessTokenAsync(user.Id, cancellationToken);
        return Result<LoginResponse>.Ok(new LoginResponse(accessToken, expiresInSeconds));
    }
}
