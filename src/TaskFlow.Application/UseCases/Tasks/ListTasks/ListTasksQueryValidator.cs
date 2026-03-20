using FluentValidation;

namespace TaskFlow.Application.UseCases.Tasks.ListTasks;

/// <summary>
/// Validates ListTasksQuery. PageSize max 20 per spec; PageNumber >= 1; TaskId not applicable (list by UserId).
/// </summary>
public sealed class ListTasksQueryValidator : AbstractValidator<ListTasksQuery>
{
    public const int MaxPageSize = 20;

    public ListTasksQueryValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required.");

        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1).WithMessage("PageNumber must be at least 1.");

        RuleFor(x => x.PageSize)
            .GreaterThanOrEqualTo(1).WithMessage("PageSize must be at least 1.")
            .LessThanOrEqualTo(MaxPageSize).WithMessage($"PageSize must not exceed {MaxPageSize}.");
    }
}
