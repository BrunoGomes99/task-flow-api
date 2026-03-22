using MediatR;
using TaskFlow.Application.DTOs;
using TaskFlow.Application.Interfaces;

namespace TaskFlow.Application.UseCases.User.GetCurrentUserProfile;

/// <summary>
/// Loads the user by id from the repository and maps to <see cref="UserDto"/>.
/// </summary>
public sealed class GetCurrentUserProfileQueryHandler : IRequestHandler<GetCurrentUserProfileQuery, UserDto?>
{
    private readonly IUserRepository _userRepository;

    public GetCurrentUserProfileQueryHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<UserDto?> Handle(GetCurrentUserProfileQuery request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        return user is null ? null : UserDto.FromDomain(user);
    }
}
