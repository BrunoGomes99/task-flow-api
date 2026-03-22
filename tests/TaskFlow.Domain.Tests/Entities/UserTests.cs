using TaskFlow.Domain.Entities;

namespace TaskFlow.Domain.Tests.Entities;

public sealed class UserTests
{
    private const string ValidHash = "12345678";

    [Fact]
    public void Constructor_ShouldCreateUser_WhenInputsValid()
    {
        var user = new User("  Alice  ", "USER@Example.COM", ValidHash);

        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal("Alice", user.Name);
        Assert.Equal("user@example.com", user.Email.Value);
        Assert.Equal(ValidHash, user.PasswordHash);
        Assert.Equal(DateTimeKind.Utc, user.CreatedAt.Kind);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenNameIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new User(null!, "user@example.com", ValidHash));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenNameIsEmptyOrWhitespace()
    {
        Assert.Throws<ArgumentException>(() => new User("", "user@example.com", ValidHash));
        Assert.Throws<ArgumentException>(() => new User("   ", "user@example.com", ValidHash));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenNameExceedsMaxLength()
    {
        var longName = new string('a', 256);

        Assert.Throws<ArgumentOutOfRangeException>(() => new User(longName, "user@example.com", ValidHash));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenPasswordHashIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new User("Name", "user@example.com", null!));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenPasswordHashIsEmptyOrWhitespace()
    {
        Assert.Throws<ArgumentException>(() => new User("Name", "user@example.com", ""));
        Assert.Throws<ArgumentException>(() => new User("Name", "user@example.com", "   "));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenPasswordHashIsTooShort()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new User("Name", "user@example.com", "1234567"));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenPasswordHashIsTooLong()
    {
        var longHash = new string('h', 256);

        Assert.Throws<ArgumentOutOfRangeException>(() => new User("Name", "user@example.com", longHash));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenEmailIsInvalid()
    {
        Assert.Throws<ArgumentException>(() => new User("Name", "not-an-email", ValidHash));
    }

    [Fact]
    public void Constructor_ShouldAcceptPasswordHashAtMinimumLength()
    {
        var user = new User("Name", "user@example.com", "12345678");

        Assert.Equal(8, user.PasswordHash.Length);
    }
}
