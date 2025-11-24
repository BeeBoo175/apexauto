using System.Collections.Concurrent;
using Dealership.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Dealership.IntegrationTests;

internal sealed class PasswordVerificationLog
{
    public ConcurrentQueue<string> CreatedHashes { get; } = new();

    public ConcurrentQueue<(string Hash, PasswordVerificationResult Result)> Verifications { get; } = new();
}

internal sealed class RecordingPasswordHasher(IOptions<PasswordHasherOptions> options, PasswordVerificationLog log)
    : IPasswordHasher<AccountUser>
{
    private readonly PasswordHasher<AccountUser> _inner = new(options);

    public string HashPassword(AccountUser user, string password)
    {
        var hash = _inner.HashPassword(user, password);
        log.CreatedHashes.Enqueue(hash);
        return hash;
    }

    public PasswordVerificationResult VerifyHashedPassword(AccountUser user, string hashedPassword, string providedPassword)
    {
        var result = _inner.VerifyHashedPassword(user, hashedPassword, providedPassword);
        log.Verifications.Enqueue((hashedPassword, result));
        return result;
    }
}
