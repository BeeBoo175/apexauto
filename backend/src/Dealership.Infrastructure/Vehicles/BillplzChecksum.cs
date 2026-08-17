using System.Security.Cryptography;
using System.Text;

namespace Dealership.Infrastructure.Vehicles;

// Billplz v5 signs a request or callback with HMAC-SHA512 over specific values joined with no separator, as lowercase hex.
public static class BillplzChecksum
{
    public static string Compute(string key, params string[] values)
    {
        var message = Encoding.UTF8.GetBytes(string.Concat(values));
        return Convert.ToHexStringLower(HMACSHA512.HashData(Encoding.UTF8.GetBytes(key), message));
    }

    // A payment order callback signs [id, bank_account_number, status, total, reference_id, epoch].
    public static bool VerifyPayoutCallback(IReadOnlyDictionary<string, string> fields, string key)
    {
        if (string.IsNullOrEmpty(key) || !fields.TryGetValue("checksum", out var supplied) || string.IsNullOrEmpty(supplied))
        {
            return false;
        }

        string Field(string name) => fields.TryGetValue(name, out var value) ? value : string.Empty;
        var expected = Compute(key, Field("id"), Field("bank_account_number"), Field("status"), Field("total"),
            Field("reference_id"), Field("epoch"));
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(supplied.ToLowerInvariant()),
            Encoding.ASCII.GetBytes(expected));
    }
}
