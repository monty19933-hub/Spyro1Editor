using System.Globalization;

namespace Spyro.Editor.Core.Updates;

public sealed record EditorBetaReleaseVersion : IComparable<EditorBetaReleaseVersion>
{
    public const string DisplayPrefix = "Spyro Editor Beta V";
    public const string PackagePrefix = "SpyroEditor-Beta-V";
    public const string TagPrefix = "beta-v";

    public EditorBetaReleaseVersion(int number)
    {
        if (number <= 0)
            throw new ArgumentOutOfRangeException(nameof(number), "A public beta release number must be positive.");
        Number = number;
    }

    public int Number { get; }
    public string DisplayName => $"{DisplayPrefix}{Number}";
    public string PackageStem => $"{PackagePrefix}{Number}";
    public string ReleaseTag => $"{TagPrefix}{Number}";
    public string ShortName => $"Beta V{Number}";

    public int CompareTo(EditorBetaReleaseVersion? other) =>
        other == null ? 1 : Number.CompareTo(other.Number);

    public static bool TryParseDisplayName(string? text, out EditorBetaReleaseVersion version)
        => TryParseExactNumber(text, DisplayPrefix, out version);

    public static bool TryParseReleaseTag(string? text, out EditorBetaReleaseVersion version)
        => TryParseExactNumber(text, TagPrefix, out version);

    private static bool TryParseExactNumber(
        string? text,
        string prefix,
        out EditorBetaReleaseVersion version)
    {
        version = null!;
        string value = (text ?? "").Trim();
        if (!value.StartsWith(prefix, StringComparison.Ordinal))
            return false;
        string numberText = value[prefix.Length..];
        if (!int.TryParse(numberText, NumberStyles.None, CultureInfo.InvariantCulture, out int number) ||
            number <= 0 ||
            !string.Equals(numberText, number.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
        {
            return false;
        }

        version = new EditorBetaReleaseVersion(number);
        return true;
    }

    public override string ToString() => ShortName;
}
