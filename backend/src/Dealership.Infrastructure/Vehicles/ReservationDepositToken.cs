using System.Security.Cryptography;
using System.Text;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Dealership.Infrastructure.Vehicles;

// The link in the deposit request mail names the request and signs it, so confirming needs no sign-in and the
// mail row holds no secret. The signature covers the customer and the deadline, which cannot change afterwards.
public sealed class ReservationDepositToken(IOptions<JwtSettings> jwtSettings)
{
    private const string Purpose = "apexauto/reservation-deposit/v1";

    public string Create(ReservationDepositRequest request)
    {
        return $"{request.Id:N}.{Convert.ToHexStringLower(Sign(request))}";
    }

    public static bool TryReadId(string token, out Guid requestId)
    {
        requestId = Guid.Empty;
        return token.Length == 97 && token[32] == '.' && Guid.TryParseExact(token[..32], "N", out requestId);
    }

    public bool Verify(string token, ReservationDepositRequest request)
    {
        if (!TryReadId(token, out var requestId) || requestId != request.Id)
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

        return CryptographicOperations.FixedTimeEquals(supplied, Sign(request));
    }

    private byte[] Sign(ReservationDepositRequest request)
    {
        var message = Encoding.UTF8.GetBytes(
            $"{Purpose}:{request.Id:N}:{request.CustomerId:N}:{request.ExpiresAt.ToUnixTimeSeconds()}");
        // A key derived for this purpose, so the session signing secret never signs deposit links directly.
        var key = HKDF.DeriveKey(HashAlgorithmName.SHA256, Encoding.UTF8.GetBytes(jwtSettings.Value.Secret), 32,
            info: Encoding.UTF8.GetBytes(Purpose));
        return HMACSHA256.HashData(key, message);
    }
}
