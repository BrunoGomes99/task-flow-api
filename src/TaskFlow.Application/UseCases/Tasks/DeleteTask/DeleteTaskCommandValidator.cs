using FluentValidation;

namespace TaskFlow.Application.UseCases.Tasks.DeleteTask;

/// <summary>
/// Validates DeleteTaskCommand. TaskId must not be empty.
/// </summary>
public sealed class DeleteTaskCommandValidator : AbstractValidator<DeleteTaskCommand>
{
    public DeleteTaskCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required.");

        RuleFor(x => x.TaskId)
            .NotEmpty().WithMessage("TaskId is required.");
    }
}
