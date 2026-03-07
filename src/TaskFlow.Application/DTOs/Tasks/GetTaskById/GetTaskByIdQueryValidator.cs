using FluentValidation;

namespace TaskFlow.Application.DTOs.Tasks.GetTaskById;

/// <summary>
/// Validates GetTaskByIdQuery. TaskId must not be empty.
/// </summary>
public sealed class GetTaskByIdQueryValidator : AbstractValidator<GetTaskByIdQuery>
{
    public GetTaskByIdQueryValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required.");

        RuleFor(x => x.TaskId)
            .NotEmpty().WithMessage("TaskId is required.");
    }
}
