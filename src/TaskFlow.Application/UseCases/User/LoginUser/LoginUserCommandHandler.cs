using MediatR;
using TaskFlow.Application.DTOs;
using TaskFlow.Application.Interfaces;
using TaskFlow.Domain.ValueObjects;

namespace TaskFlow.Application.UseCases.User.LoginUser;

/// <summary>
/// Handles login: loads user by email, verifies password, issues JWT.
/// </summary>
public sealed class LoginUserCommandHandler : IRequestHandler<LoginUserCommand, LoginResponse>
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

    public async Task<LoginResponse> Handle(LoginUserCommand request, CancellationToken cancellationToken)
    {
        var email = Email.Create(request.Email);
        var user = await _userRepository.GetByEmailAsync(email, cancellationToken);

        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid email or password.");

        var (accessToken, expiresInSeconds) = await _jwtService.CreateAccessTokenAsync(user.Id, cancellationToken);
        return new LoginResponse(accessToken, expiresInSeconds);
    }
}
