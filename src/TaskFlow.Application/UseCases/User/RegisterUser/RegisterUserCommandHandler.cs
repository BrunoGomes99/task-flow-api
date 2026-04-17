using MediatR;
using TaskFlow.Application.Common.Results;
using TaskFlow.Application.Interfaces;
using TaskFlow.Domain.ValueObjects;
using DomainUser = TaskFlow.Domain.Entities.User;

namespace TaskFlow.Application.UseCases.User.RegisterUser;

/// <summary>
/// Handles user registration: uniqueness check, password hashing, persistence.
/// </summary>
public sealed class RegisterUserCommandHandler : IRequestHandler<RegisterUserCommand, Result<RegisterUserResult>>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;

    public RegisterUserCommandHandler(IUserRepository userRepository, IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
    }

    public async Task<Result<RegisterUserResult>> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        var email = Email.Create(request.Email);

        if (await _userRepository.ExistsByEmailAsync(email, cancellationToken))
        {
            return Result<RegisterUserResult>.Conflict(
                ErrorCodes.UserEmailAlreadyInUse,
                "This email is already registered.",
                resource: "user");
        }

        var passwordHash = _passwordHasher.Hash(request.Password);
        var user = new DomainUser(request.Name, request.Email, passwordHash);

        await _userRepository.AddAsync(user, cancellationToken);
        return Result<RegisterUserResult>.Ok(new RegisterUserResult(user.Id));
    }
}
