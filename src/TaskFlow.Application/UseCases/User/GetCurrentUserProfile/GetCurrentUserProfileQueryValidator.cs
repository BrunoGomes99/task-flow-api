using FluentValidation;

namespace TaskFlow.Application.UseCases.User.GetCurrentUserProfile;

/// <summary>
/// Validates GetCurrentUserProfileQuery.
/// </summary>
public sealed class GetCurrentUserProfileQueryValidator : AbstractValidator<GetCurrentUserProfileQuery>
{
    public GetCurrentUserProfileQueryValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required.");
    }
}
