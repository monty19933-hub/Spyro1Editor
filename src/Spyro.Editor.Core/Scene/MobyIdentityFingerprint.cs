using System.Globalization;

namespace Spyro.Editor.Core.Scene;

public static class MobyIdentityFingerprint
{
    public const int CurrentVersion = 2;

    public static string BuildV2(Moby moby)
    {
        return $"class=0x{moby.SourceByte37:X2}{moby.SourceByte36:X2} " +
            $"radius50=0x{moby.Type:X2} update52=0x{moby.Flag4A:X2} " +
            $"drop53=0x{moby.Flag4B:X2} spec4F=0x{moby.SourceByte4F:X2}";
    }

    public static string BuildLegacyV1(Moby moby)
    {
        return $"type=0x{moby.Type:X2} b36=0x{moby.SourceByte36:X2} " +
            $"f4A=0x{moby.Flag4A:X2} f4B=0x{moby.Flag4B:X2} b4F=0x{moby.SourceByte4F:X2}";
    }

    public static bool TryParse(string? text, out MobyIdentityFingerprintParts parts)
    {
        parts = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);
        foreach (string token in text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string[] pair = token.Split('=', 2);
            if (pair.Length != 2)
                continue;
            if (!values.TryAdd(pair[0], pair[1]))
                return false;
        }

        bool classPresent = values.ContainsKey("class");
        bool lowBytePresent = values.ContainsKey("b36");
        bool highBytePresent = values.ContainsKey("b37");
        if (!classPresent && !lowBytePresent)
            return false;

        int nativeClass;
        bool hasNativeClass;
        if (classPresent)
        {
            if (!TryReadBounded(values, "class", 0xFFFF, out nativeClass))
                return false;
            hasNativeClass = true;

            if (lowBytePresent &&
                (!TryReadBounded(values, "b36", 0xFF, out int lowByte) || lowByte != (nativeClass & 0xFF)))
            {
                return false;
            }
            if (highBytePresent &&
                (!TryReadBounded(values, "b37", 0xFF, out int highByte) || highByte != ((nativeClass >> 8) & 0xFF)))
            {
                return false;
            }
        }
        else
        {
            if (!TryReadBounded(values, "b36", 0xFF, out int lowByte))
                return false;

            if (highBytePresent)
            {
                if (!TryReadBounded(values, "b37", 0xFF, out int highByte))
                    return false;
                nativeClass = (highByte << 8) | lowByte;
                hasNativeClass = true;
            }
            else
            {
                nativeClass = lowByte;
                hasNativeClass = false;
            }
        }

        if (!TryReadAliasedByte(values, "radius50", "type", out int renderRadius) ||
            !TryReadAliasedByte(values, "update52", "f4A", out int updateDistance) ||
            !TryReadAliasedByte(values, "drop53", "f4B", out int dropClass) ||
            !TryReadAliasedByte(values, "spec4F", "b4F", out int specularMetalType))
        {
            return false;
        }

        parts = new MobyIdentityFingerprintParts(
            NativeClass: nativeClass,
            HasFullNativeClass: hasNativeClass,
            RenderRadius: renderRadius,
            UpdateDistance: updateDistance,
            DropClass: dropClass,
            SpecularMetalType: specularMetalType);
        return true;
    }

    public static bool Matches(string? fingerprint, Moby moby, bool allowLegacyLowByte)
    {
        if (!TryParse(fingerprint, out MobyIdentityFingerprintParts parts))
            return false;

        int nativeClass = (moby.SourceByte37 << 8) | moby.SourceByte36;
        bool classMatches = parts.HasFullNativeClass
            ? nativeClass == parts.NativeClass
            : allowLegacyLowByte && moby.SourceByte36 == (parts.NativeClass & 0xFF);
        return classMatches &&
            moby.Type == parts.RenderRadius &&
            moby.Flag4A == parts.UpdateDistance &&
            moby.Flag4B == parts.DropClass &&
            moby.SourceByte4F == parts.SpecularMetalType;
    }

    public static bool IsLegacyV1(string? fingerprint)
    {
        return TryParse(fingerprint, out MobyIdentityFingerprintParts parts) && !parts.HasFullNativeClass;
    }

    private static bool TryReadHex(IReadOnlyDictionary<string, string> values, string key, out int value)
    {
        value = 0;
        if (!values.TryGetValue(key, out string? text))
            return false;

        text = text.Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return int.TryParse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryReadBounded(
        IReadOnlyDictionary<string, string> values,
        string key,
        int maximum,
        out int value)
    {
        return TryReadHex(values, key, out value) && value >= 0 && value <= maximum;
    }

    private static bool TryReadAliasedByte(
        IReadOnlyDictionary<string, string> values,
        string canonicalKey,
        string legacyKey,
        out int value)
    {
        value = 0;
        bool canonicalPresent = values.ContainsKey(canonicalKey);
        bool legacyPresent = values.ContainsKey(legacyKey);
        if (!canonicalPresent && !legacyPresent)
            return false;

        int canonicalValue = 0;
        int legacyValue = 0;
        if (canonicalPresent && !TryReadBounded(values, canonicalKey, 0xFF, out canonicalValue))
            return false;
        if (legacyPresent && !TryReadBounded(values, legacyKey, 0xFF, out legacyValue))
            return false;
        if (canonicalPresent && legacyPresent && canonicalValue != legacyValue)
            return false;

        value = canonicalPresent ? canonicalValue : legacyValue;
        return true;
    }
}

public readonly record struct MobyIdentityFingerprintParts(
    int NativeClass,
    bool HasFullNativeClass,
    int RenderRadius,
    int UpdateDistance,
    int DropClass,
    int SpecularMetalType);
