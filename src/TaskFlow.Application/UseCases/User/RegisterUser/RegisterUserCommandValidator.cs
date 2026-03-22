using FluentValidation;

namespace TaskFlow.Application.UseCases.User.RegisterUser;

/// <summary>
/// Validates RegisterUserCommand. Aligns with domain User and Email constraints where applicable.
/// </summary>
public sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    private const int NameMaxLength = 255;
    private const int PasswordMinLength = 8;
    private const int PasswordMaxLength = 255;

    public RegisterUserCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(NameMaxLength).WithMessage($"Name must not exceed {NameMaxLength} characters.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email is not valid.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(PasswordMinLength).WithMessage($"Password must be at least {PasswordMinLength} characters.")
            .MaximumLength(PasswordMaxLength).WithMessage($"Password must not exceed {PasswordMaxLength} characters.");
    }
}
