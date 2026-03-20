using FluentValidation;

namespace TaskFlow.Application.UseCases.Tasks.CreateTask;

/// <summary>
/// Validates CreateTaskCommand. Aligns with Domain Task constraints (Title 3–200, Description max 2000).
/// </summary>
public sealed class CreateTaskCommandValidator : AbstractValidator<CreateTaskCommand>
{
    private const int TitleMinLength = 3;
    private const int TitleMaxLength = 200;
    private const int DescriptionMaxLength = 2000;

    public CreateTaskCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required.");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MinimumLength(TitleMinLength).WithMessage($"Title must be at least {TitleMinLength} characters.")
            .MaximumLength(TitleMaxLength).WithMessage($"Title must not exceed {TitleMaxLength} characters.");

        RuleFor(x => x.Description)
            .MaximumLength(DescriptionMaxLength).When(x => x.Description is not null)
            .WithMessage($"Description must not exceed {DescriptionMaxLength} characters.");
    }
}
