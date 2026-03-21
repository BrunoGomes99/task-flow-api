using MediatR;
using TaskFlow.Application.DTOs;

namespace TaskFlow.Application.UseCases.User.GetCurrentUserProfile;

/// <summary>
/// Query for the authenticated user's profile. UserId must come from JWT <c>sub</c> only (set by the API).
/// </summary>
public sealed record GetCurrentUserProfileQuery(Guid UserId) : IRequest<UserDto?>;
