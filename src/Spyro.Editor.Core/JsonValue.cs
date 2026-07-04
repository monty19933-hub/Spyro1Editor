using System.Globalization;
using System.Text.Json;

namespace Spyro.Editor.Core;

internal static class JsonValue
{
    public static string GetString(JsonElement element, string name, string fallback = "")
    {
        if (!element.TryGetProperty(name, out JsonElement value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return fallback;

        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? fallback : value.ToString();
    }

    public static int GetInt32(JsonElement element, string name, int fallback = 0)
    {
        long value = GetInt64(element, name, fallback);
        return value < int.MinValue || value > int.MaxValue ? fallback : (int)value;
    }

    public static long GetInt64(JsonElement element, string name, long fallback = 0)
    {
        if (!element.TryGetProperty(name, out JsonElement value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return fallback;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out long number))
            return number;

        string text = value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : value.ToString();
        text = text.Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            && long.TryParse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long hex))
            return hex;

        return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed) ? parsed : fallback;
    }

    public static float GetSingle(JsonElement element, string name, float fallback = 0)
    {
        if (!element.TryGetProperty(name, out JsonElement value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return fallback;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetSingle(out float number))
            return number;

        string text = value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : value.ToString();
        return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) ? parsed : fallback;
    }

    public static bool GetBoolean(JsonElement element, string name, bool fallback = false)
    {
        if (!element.TryGetProperty(name, out JsonElement value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return fallback;

        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
            return value.GetBoolean();

        return bool.TryParse(value.ToString(), out bool parsed) ? parsed : fallback;
    }
}
