using FluentValidation;

namespace TaskFlow.Application.DTOs.Tasks.UpdateTask;

/// <summary>
/// Validates UpdateTaskCommand. TaskId must not be empty. Title/Description align with Domain constraints.
/// </summary>
public sealed class UpdateTaskCommandValidator : AbstractValidator<UpdateTaskCommand>
{
    private const int TitleMinLength = 3;
    private const int TitleMaxLength = 200;
    private const int DescriptionMaxLength = 2000;

    public UpdateTaskCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required.");

        RuleFor(x => x.TaskId)
            .NotEmpty().WithMessage("TaskId is required.");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MinimumLength(TitleMinLength).WithMessage($"Title must be at least {TitleMinLength} characters.")
            .MaximumLength(TitleMaxLength).WithMessage($"Title must not exceed {TitleMaxLength} characters.");

        RuleFor(x => x.Description)
            .MaximumLength(DescriptionMaxLength).When(x => x.Description is not null)
            .WithMessage($"Description must not exceed {DescriptionMaxLength} characters.");
    }
}
