using FluentValidation;

namespace TaskFlow.Application.UseCases.Tasks.UpdateTaskStatus;

/// <summary>
/// Validates UpdateTaskStatusCommand. TaskId must not be empty.
/// </summary>
public sealed class UpdateTaskStatusCommandValidator : AbstractValidator<UpdateTaskStatusCommand>
{
    public UpdateTaskStatusCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required.");

        RuleFor(x => x.TaskId)
            .NotEmpty().WithMessage("TaskId is required.");
    }
}
