using MediatR;
using TaskFlow.Application.Common.Exceptions;
using TaskFlow.Application.Interfaces;
using TaskFlow.Domain.ValueObjects;
using DomainUser = TaskFlow.Domain.Entities.User;

namespace TaskFlow.Application.UseCases.User.RegisterUser;

/// <summary>
/// Handles user registration: uniqueness check, password hashing, persistence.
/// </summary>
public sealed class RegisterUserCommandHandler : IRequestHandler<RegisterUserCommand, RegisterUserResult>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;

    public RegisterUserCommandHandler(IUserRepository userRepository, IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
    }

    public async Task<RegisterUserResult> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        var email = Email.Create(request.Email);

        if (await _userRepository.ExistsByEmailAsync(email, cancellationToken))
            throw new ConflictException("User", "This email is already registered.");

        var passwordHash = _passwordHasher.Hash(request.Password);
        var user = new DomainUser(request.Name, request.Email, passwordHash);

        await _userRepository.AddAsync(user, cancellationToken);
        return new RegisterUserResult(user.Id);
    }
}
