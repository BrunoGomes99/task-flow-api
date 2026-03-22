using FluentValidation;

namespace TaskFlow.Application.UseCases.User.LoginUser;

/// <summary>
/// Validates LoginUserCommand. Detailed password rules are enforced at registration; login only requires non-empty fields.
/// </summary>
public sealed class LoginUserCommandValidator : AbstractValidator<LoginUserCommand>
{
    public LoginUserCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email is not valid.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.");
    }
}
