using FluentValidation;

namespace TaskFlow.Application.UseCases.Tasks.GetTaskSummary;

/// <summary>
/// Validates GetTaskSummaryQuery.
/// </summary>
public sealed class GetTaskSummaryQueryValidator : AbstractValidator<GetTaskSummaryQuery>
{
    public GetTaskSummaryQueryValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required.");
    }
}
