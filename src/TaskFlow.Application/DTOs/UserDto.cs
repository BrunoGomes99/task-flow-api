namespace TaskFlow.Application.DTOs;

/// <summary>
/// Data transfer object for authenticated user profile (no credentials).
/// </summary>
public sealed record UserDto(Guid Id, string Name, string Email, DateTime CreatedAt)
{
    /// <summary>
    /// Maps a domain User to UserDto.
    /// </summary>
    public static UserDto FromDomain(TaskFlow.Domain.Entities.User user) => new(
        user.Id,
        user.Name,
        user.Email.Value,
        user.CreatedAt);
}
