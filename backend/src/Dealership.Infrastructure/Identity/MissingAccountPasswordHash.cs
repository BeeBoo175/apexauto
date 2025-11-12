using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;

namespace Dealership.Infrastructure.Identity;

public sealed class MissingAccountPasswordHash
{
    private const int DummyPasswordBytes = 32;
    private readonly AccountUser _user = new() { Id = Guid.NewGuid() };
    private readonly string _hash;

    public MissingAccountPasswordHash(IPasswordHasher<AccountUser> hasher)
    {
        // Pre-hashes a dummy secret so missing-account logins execute real verification work, mitigating timing-based user enumeration.
        _hash = hasher.HashPassword(_user, Convert.ToHexString(RandomNumberGenerator.GetBytes(DummyPasswordBytes)));
    }

    public void Verify(IPasswordHasher<AccountUser> hasher, string password)
    {
        // The result never authenticates this unpersisted identity; only the configured verification work matters.
        hasher.VerifyHashedPassword(_user, _hash, password);
    }
}
