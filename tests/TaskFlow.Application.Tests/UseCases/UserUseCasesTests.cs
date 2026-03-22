using TaskFlow.Application.Interfaces;
using TaskFlow.Application.UseCases.User.GetCurrentUserProfile;
using TaskFlow.Application.UseCases.User.LoginUser;
using TaskFlow.Application.UseCases.User.RegisterUser;
using TaskFlow.Domain.ValueObjects;
using DomainUser = TaskFlow.Domain.Entities.User;

namespace TaskFlow.Application.Tests.UseCases;

public sealed class UserUseCasesTests
{
    private static readonly FakePasswordHasher PasswordHasher = new();
    private static readonly FakeJwtService JwtService = new();

    [Fact]
    public async System.Threading.Tasks.Task RegisterUserHandler_ShouldPersistAndReturnId_WhenEmailIsNew()
    {
        var repository = new InMemoryUserRepository();
        var handler = new RegisterUserCommandHandler(repository, PasswordHasher);

        var result = await handler.Handle(
            new RegisterUserCommand("Alice", "alice@example.com", "password12"),
            CancellationToken.None);

        var persisted = await repository.GetByIdAsync(result.Id, CancellationToken.None);
        Assert.NotNull(persisted);
        Assert.Equal("alice@example.com", persisted!.Email.Value);
        Assert.Equal(PasswordHasher.Hash("password12"), persisted.PasswordHash);
    }

    [Fact]
    public async System.Threading.Tasks.Task RegisterUserHandler_ShouldThrow_WhenEmailAlreadyExists()
    {
        var repository = new InMemoryUserRepository();
        var existing = new DomainUser("Existing", "taken@example.com", PasswordHasher.Hash("password12"));
        await repository.AddAsync(existing, CancellationToken.None);
        var handler = new RegisterUserCommandHandler(repository, PasswordHasher);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new RegisterUserCommand("Other", "taken@example.com", "password12"),
            CancellationToken.None));

        Assert.Equal("This email is already registered.", ex.Message);
    }

    [Fact]
    public async System.Threading.Tasks.Task LoginUserHandler_ShouldReturnLoginResponse_WhenCredentialsValid()
    {
        var repository = new InMemoryUserRepository();
        var hash = PasswordHasher.Hash("secret12345");
        var user = new DomainUser("Bob", "bob@example.com", hash);
        await repository.AddAsync(user, CancellationToken.None);
        var handler = new LoginUserCommandHandler(repository, PasswordHasher, JwtService);

        var result = await handler.Handle(
            new LoginUserCommand("bob@example.com", "secret12345"),
            CancellationToken.None);

        Assert.Equal(JwtService.FormatToken(user.Id), result.AccessToken);
        Assert.Equal(FakeJwtService.DefaultExpiresInSeconds, result.ExpiresIn);
    }

    [Fact]
    public async System.Threading.Tasks.Task LoginUserHandler_ShouldThrowUnauthorized_WhenUserNotFound()
    {
        var repository = new InMemoryUserRepository();
        var handler = new LoginUserCommandHandler(repository, PasswordHasher, JwtService);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(
            new LoginUserCommand("nobody@example.com", "password12"),
            CancellationToken.None));
    }

    [Fact]
    public async System.Threading.Tasks.Task LoginUserHandler_ShouldThrowUnauthorized_WhenPasswordInvalid()
    {
        var repository = new InMemoryUserRepository();
        var user = new DomainUser("Bob", "bob@example.com", PasswordHasher.Hash("correctpass1"));
        await repository.AddAsync(user, CancellationToken.None);
        var handler = new LoginUserCommandHandler(repository, PasswordHasher, JwtService);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(
            new LoginUserCommand("bob@example.com", "wrongpassword"),
            CancellationToken.None));
    }

    [Fact]
    public async System.Threading.Tasks.Task GetCurrentUserProfileHandler_ShouldReturnUserDto_WhenUserExists()
    {
        var repository = new InMemoryUserRepository();
        var user = new DomainUser("Carol", "carol@example.com", PasswordHasher.Hash("password12"));
        await repository.AddAsync(user, CancellationToken.None);
        var handler = new GetCurrentUserProfileQueryHandler(repository);

        var dto = await handler.Handle(new GetCurrentUserProfileQuery(user.Id), CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal(user.Id, dto!.Id);
        Assert.Equal("Carol", dto.Name);
        Assert.Equal("carol@example.com", dto.Email);
    }

    [Fact]
    public async System.Threading.Tasks.Task GetCurrentUserProfileHandler_ShouldReturnNull_WhenUserNotFound()
    {
        var repository = new InMemoryUserRepository();
        var handler = new GetCurrentUserProfileQueryHandler(repository);

        var dto = await handler.Handle(new GetCurrentUserProfileQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Null(dto);
    }

    private sealed class InMemoryUserRepository : IUserRepository
    {
        private readonly List<DomainUser> _users = [];

        public System.Threading.Tasks.Task AddAsync(DomainUser user, CancellationToken cancellationToken = default)
        {
            _users.Add(user);
            return System.Threading.Tasks.Task.CompletedTask;
        }

        public System.Threading.Tasks.Task<DomainUser?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var user = _users.FirstOrDefault(u => u.Id == userId);
            return System.Threading.Tasks.Task.FromResult<DomainUser?>(user);
        }

        public System.Threading.Tasks.Task<DomainUser?> GetByEmailAsync(Email email, CancellationToken cancellationToken = default)
        {
            var user = _users.FirstOrDefault(u => u.Email.Value == email.Value);
            return System.Threading.Tasks.Task.FromResult<DomainUser?>(user);
        }

        public System.Threading.Tasks.Task<bool> ExistsByEmailAsync(Email email, CancellationToken cancellationToken = default)
        {
            var exists = _users.Any(u => u.Email.Value == email.Value);
            return System.Threading.Tasks.Task.FromResult(exists);
        }
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => $"HASH:{password}";

        public bool Verify(string password, string passwordHash) => Hash(password) == passwordHash;
    }

    private sealed class FakeJwtService : IJwtService
    {
        public const int DefaultExpiresInSeconds = 3600;

        public System.Threading.Tasks.Task<(string AccessToken, int ExpiresInSeconds)> CreateAccessTokenAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            return System.Threading.Tasks.Task.FromResult((FormatToken(userId), DefaultExpiresInSeconds));
        }

        public string FormatToken(Guid userId) => $"jwt-{userId:N}";
    }
}
