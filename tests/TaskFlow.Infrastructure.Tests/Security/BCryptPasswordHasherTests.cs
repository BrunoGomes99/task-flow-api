using Microsoft.Extensions.Options;
using TaskFlow.Infrastructure.Configuration;
using TaskFlow.Infrastructure.Security;

namespace TaskFlow.Infrastructure.Tests.Security;

public sealed class BCryptPasswordHasherTests
{
    [Fact]
    public void Hash_ThenVerify_ReturnsTrue_ForSamePassword()
    {
        var hasher = CreateHasher(11);

        var hash = hasher.Hash("my-secret-password");

        Assert.NotNull(hash);
        Assert.NotEqual("my-secret-password", hash);
        Assert.True(hasher.Verify("my-secret-password", hash));
    }

    [Fact]
    public void Verify_ReturnsFalse_WhenPasswordWrong()
    {
        var hasher = CreateHasher(11);
        var hash = hasher.Hash("correct-horse-battery");

        Assert.False(hasher.Verify("wrong-password", hash));
    }

    [Fact]
    public void Verify_ReturnsFalse_WhenStoredHashEmpty()
    {
        var hasher = CreateHasher(11);

        Assert.False(hasher.Verify("any", "   "));
    }

    [Fact]
    public void Constructor_Throws_WhenWorkFactorOutOfRange()
    {
        var options = Options.Create(new PasswordHashingSettings { WorkFactor = 3 });

        Assert.Throws<InvalidOperationException>(() => new BCryptPasswordHasher(options));
    }

    private static BCryptPasswordHasher CreateHasher(int workFactor)
    {
        var options = Options.Create(new PasswordHashingSettings { WorkFactor = workFactor });
        return new BCryptPasswordHasher(options);
    }
}
