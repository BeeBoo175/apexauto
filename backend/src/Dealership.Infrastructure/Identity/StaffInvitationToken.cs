using System.Security.Cryptography;
using System.Text;
using Dealership.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Dealership.Infrastructure.Identity;

// The signature covers the invitation's current nonce and expiry, so a resend or email change retires older links.
public sealed class StaffInvitationToken(IOptions<JwtSettings> jwtSettings)
{
    private const string Purpose = "apexauto/staff-invitation/v1";

    public string Create(StaffInvitation invitation)
    {
        return $"{invitation.Id:N}.{Convert.ToHexStringLower(Sign(invitation))}";
    }

    public static bool TryReadId(string token, out Guid invitationId)
    {
        invitationId = Guid.Empty;
        return token.Length == 97 && token[32] == '.' && Guid.TryParseExact(token[..32], "N", out invitationId);
    }

    public bool Verify(string token, StaffInvitation invitation)
    {
        if (!TryReadId(token, out var invitationId) || invitationId != invitation.Id)
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

        return CryptographicOperations.FixedTimeEquals(supplied, Sign(invitation));
    }

    private byte[] Sign(StaffInvitation invitation)
    {
        var message = Encoding.UTF8.GetBytes(
            $"{Purpose}:{invitation.Id:N}:{invitation.TokenNonce:N}:{invitation.ExpiresAt.ToUnixTimeSeconds()}");
        return HMACSHA256.HashData(Encoding.UTF8.GetBytes(jwtSettings.Value.Secret), message);
    }
}
