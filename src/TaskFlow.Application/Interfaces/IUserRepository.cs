using TaskFlow.Domain.Entities;
using TaskFlow.Domain.ValueObjects;
using DomainUser = TaskFlow.Domain.Entities.User;

namespace TaskFlow.Application.Interfaces;

/// <summary>
/// Repository for User aggregate root. Authentication (password verification, JWT) is handled in Application via use cases and <c>IJwtService</c>, not here.
/// Implemented in Infrastructure (e.g. MongoDB).
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// Adds a new user. The user id is set by the domain; the implementation persists it.
    /// </summary>
    System.Threading.Tasks.Task AddAsync(DomainUser user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a user by id (e.g. for profile from JWT <c>sub</c>).
    /// </summary>
    /// <param name="userId">User id from the authenticated principal.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The user if found; otherwise null.</returns>
    System.Threading.Tasks.Task<DomainUser?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a user by normalized email (e.g. for login after lookup).
    /// </summary>
    /// <param name="email">Email value object (normalized in <see cref="Email.Create"/>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The user if found; otherwise null.</returns>
    System.Threading.Tasks.Task<DomainUser?> GetByEmailAsync(Email email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns whether an account with this email already exists (e.g. register validation).
    /// </summary>
    System.Threading.Tasks.Task<bool> ExistsByEmailAsync(Email email, CancellationToken cancellationToken = default);
}
