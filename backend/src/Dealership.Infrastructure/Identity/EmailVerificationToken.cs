using System.Security.Cryptography;
using System.Text;
using Dealership.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Dealership.Infrastructure.Identity;

public sealed class EmailVerificationToken(IOptions<JwtSettings> jwtSettings)
{
    private const string Purpose = "apexauto/email-verification/v1";

    public string Create(EmailVerificationChallenge challenge)
    {
        var signature = Sign(challenge);
        return $"{challenge.Id:N}.{Convert.ToHexStringLower(signature)}";
    }

    public static bool TryReadId(string token, out Guid challengeId)
    {
        challengeId = Guid.Empty;
        return token.Length == 97 && token[32] == '.' && Guid.TryParseExact(token[..32], "N", out challengeId);
    }

    public bool Verify(string token, EmailVerificationChallenge challenge)
    {
        if (!TryReadId(token, out var challengeId) || challengeId != challenge.Id)
        {
            return false;
        }

        byte[] supplied;

        try
        {
            supplied = Convert.FromHexString(token[33..]);
        }
        catch (FormatException)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(supplied, Sign(challenge));
    }

    private byte[] Sign(EmailVerificationChallenge challenge)
    {
        var message = Encoding.UTF8.GetBytes($"{Purpose}:{challenge.Id:N}:{challenge.UserId:N}:{challenge.ExpiresAt.ToUnixTimeSeconds()}");
        return HMACSHA256.HashData(Encoding.UTF8.GetBytes(jwtSettings.Value.Secret), message);
    }
}
