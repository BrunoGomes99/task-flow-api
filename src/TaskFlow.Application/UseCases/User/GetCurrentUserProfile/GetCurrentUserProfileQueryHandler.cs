using MediatR;
using TaskFlow.Application.Common.Results;
using TaskFlow.Application.DTOs;
using TaskFlow.Application.Interfaces;

namespace TaskFlow.Application.UseCases.User.GetCurrentUserProfile;

/// <summary>
/// Loads the user by id from the repository and maps to <see cref="UserDto"/>.
/// </summary>
public sealed class GetCurrentUserProfileQueryHandler : IRequestHandler<GetCurrentUserProfileQuery, Result<UserDto>>
{
    private readonly IUserRepository _userRepository;

    public GetCurrentUserProfileQueryHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<Result<UserDto>> Handle(GetCurrentUserProfileQuery request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            return Result<UserDto>.NotFound(
                ErrorCodes.UserNotFound,
                "User was not found.",
                resource: "user",
                id: request.UserId.ToString("D"));
        }

        return Result<UserDto>.Ok(UserDto.FromDomain(user));
    }
}
