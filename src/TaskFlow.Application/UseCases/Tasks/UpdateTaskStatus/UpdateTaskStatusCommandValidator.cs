using FluentValidation;

namespace TaskFlow.Application.UseCases.Tasks.UpdateTaskStatus;

/// <summary>
/// Validates <see cref="UpdateTaskStatusCommand"/> before the handler runs.
/// </summary>
public sealed class UpdateTaskStatusCommandValidator : AbstractValidator<UpdateTaskStatusCommand>
{
    public UpdateTaskStatusCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required.");

        RuleFor(x => x.TaskId)
            .NotEmpty().WithMessage("TaskId is required.");

        RuleFor(x => x.Status)
            .Must(s => Enum.IsDefined(s))
            .WithMessage("Unsupported task status.");
    }
}
