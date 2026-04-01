using Microsoft.Extensions.Options;
using TaskFlow.Application.Interfaces;
using TaskFlow.Infrastructure.Configuration;

namespace TaskFlow.Infrastructure.Security;

public sealed class BCryptPasswordHasher : IPasswordHasher
{
    private readonly int _workFactor;

    public BCryptPasswordHasher(IOptions<PasswordHashingSettings> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var wf = options.Value.WorkFactor;
        if (wf is < 4 or > 31)
            throw new InvalidOperationException(
                $"PasswordHashing:WorkFactor must be between 4 and 31 (inclusive). Current: {wf}.");
        _workFactor = wf;
    }

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        return BCrypt.Net.BCrypt.HashPassword(password, _workFactor);
    }

    public bool Verify(string password, string passwordHash)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        if (string.IsNullOrWhiteSpace(passwordHash))
            return false;
        return BCrypt.Net.BCrypt.Verify(password, passwordHash);
    }
}
