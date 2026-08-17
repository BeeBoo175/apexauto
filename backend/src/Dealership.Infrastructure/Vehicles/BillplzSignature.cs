using System.Security.Cryptography;
using System.Text;

namespace Dealership.Infrastructure.Vehicles;

// Billplz signs a callback or redirect with HMAC-SHA256 over its sorted key and value pairs. Nested names such as
// billplz[id] join their brackets, and the signature field itself is never part of the message.
public static class BillplzSignature
{
    private const string SignatureField = "x_signature";
    private const string NestedSignatureField = "billplzx_signature";

    public static string Compute(IEnumerable<KeyValuePair<string, string>> fields, string key)
    {
        var pairs = fields
            .Select(field => (Name: Normalize(field.Key), field.Value))
            .Where(field => !IsSignature(field.Name))
            .Select(field => field.Name + field.Value)
            // Lower-cased ordinal order, as the provider sorts: underscore then sorts before letters, which an
            // upper-casing comparison would reverse (paid_amount before paid).
            .OrderBy(pair => pair.ToLowerInvariant(), StringComparer.Ordinal);
        var message = Encoding.UTF8.GetBytes(string.Join('|', pairs));
        return Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), message));
    }

    public static bool Verify(IReadOnlyDictionary<string, string> fields, string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        var supplied = fields.FirstOrDefault(field => IsSignature(Normalize(field.Key))).Value;
        if (string.IsNullOrEmpty(supplied))
        {
            return false;
        }

        var expected = Encoding.ASCII.GetBytes(Compute(fields, key));
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(supplied.ToLowerInvariant()), expected);
    }

    // The redirect names it billplz[x_signature], which joins to billplzx_signature.
    private static bool IsSignature(string name) => name is SignatureField or NestedSignatureField;

    private static string Normalize(string name) => name.Replace("[", string.Empty).Replace("]", string.Empty);
}
