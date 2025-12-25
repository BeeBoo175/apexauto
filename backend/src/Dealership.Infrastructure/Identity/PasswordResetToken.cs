using System.Security.Cryptography;
using System.Text;
using Dealership.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Dealership.Infrastructure.Identity;

public sealed class PasswordResetToken(IOptions<JwtSettings> settings)
{
    private const string Purpose = "apexauto/password-reset/v1";

    public string Create(PasswordResetChallenge challenge)
    {
        return $"{challenge.Id:N}.{Convert.ToHexStringLower(Sign(challenge))}";
    }

    public static bool TryReadId(string token, out Guid id)
    {
        id = Guid.Empty;
        return token.Length == 97 && token[32] == '.' && Guid.TryParseExact(token[..32], "N", out id);
    }

    public bool Verify(string token, PasswordResetChallenge challenge)
    {
        if (!TryReadId(token, out var id) || id != challenge.Id)
        {
            return false;
        }

        try
        {
            return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(token[33..]), Sign(challenge));
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private byte[] Sign(PasswordResetChallenge challenge)
    {
        var message = Encoding.UTF8.GetBytes(
            $"{Purpose}:{challenge.Id:N}:{challenge.UserId:N}:{challenge.ExpiresAt.ToUnixTimeSeconds()}:{challenge.RecipientEmail}");
        return HMACSHA256.HashData(Encoding.UTF8.GetBytes(settings.Value.Secret), message);
    }
}
