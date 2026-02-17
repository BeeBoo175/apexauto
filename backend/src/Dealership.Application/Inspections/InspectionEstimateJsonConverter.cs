using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dealership.Application.Inspections;

public sealed class InspectionEstimateJsonConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.Number)
        {
            throw new JsonException("Repair estimate must be a JSON number.");
        }

        using var token = JsonDocument.ParseValue(ref reader);
        var raw = token.RootElement.GetRawText();
        var exponentIndex = raw.IndexOfAny(['e', 'E']);
        var mantissa = exponentIndex < 0 ? raw : raw[..exponentIndex];
        var digits = mantissa.Replace(".", "", StringComparison.Ordinal).TrimStart('-').TrimStart('0');
        if (digits.Length == 0)
        {
            return 0;
        }

        if (raw.StartsWith('-') || !int.TryParse(exponentIndex < 0 ? "0" : raw[(exponentIndex + 1)..],
            NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var exponent))
        {
            throw new JsonException("Repair estimate requires nonnegative exact MYR minor units.");
        }

        var significant = digits.TrimEnd('0');
        var decimalIndex = mantissa.IndexOf('.');
        var fractionLength = decimalIndex < 0 ? 0 : mantissa.Length - decimalIndex - 1;
        var power = (long)exponent - fractionLength + digits.Length - significant.Length;
        if (power < -2 || significant.Length + power > 29 || significant.Length > 29 ||
            !decimal.TryParse(significant, NumberStyles.None, CultureInfo.InvariantCulture, out _) ||
            !decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var amount) ||
            decimal.Round(amount, 2) != amount)
        {
            throw new JsonException("Repair estimate exceeds exact MYR precision.");
        }

        return amount;
    }

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) => writer.WriteNumberValue(value);
}
