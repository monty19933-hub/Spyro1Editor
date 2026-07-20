using System.Globalization;

namespace Spyro.Editor.Core.Updates;

public sealed record EditorBetaReleaseVersion : IComparable<EditorBetaReleaseVersion>
{
    public const string DisplayPrefix = "Spyro Editor Beta V";
    public const string PackagePrefix = "SpyroEditor-Beta-V";
    public const string TagPrefix = "beta-v";

    public EditorBetaReleaseVersion(int major, int minor = 0)
    {
        if (major <= 0)
            throw new ArgumentOutOfRangeException(nameof(major), "A public beta release major version must be positive.");
        if (minor < 0)
            throw new ArgumentOutOfRangeException(nameof(minor), "A public beta release minor version cannot be negative.");
        Major = major;
        Minor = minor;
    }

    public int Major { get; }
    public int Minor { get; }
    public int Number => Major;
    public bool IsIncremental => Minor > 0;
    public string CanonicalVersion => Minor == 0
        ? Major.ToString(CultureInfo.InvariantCulture)
        : $"{Major.ToString(CultureInfo.InvariantCulture)}.{Minor.ToString(CultureInfo.InvariantCulture)}";
    public string DisplayName => $"{DisplayPrefix}{CanonicalVersion}";
    public string PackageStem => $"{PackagePrefix}{CanonicalVersion}";
    public string ReleaseTag => $"{TagPrefix}{CanonicalVersion}";
    public string ShortName => $"Beta V{CanonicalVersion}";

    public int CompareTo(EditorBetaReleaseVersion? other)
    {
        if (other == null)
            return 1;
        int majorComparison = Major.CompareTo(other.Major);
        return majorComparison != 0 ? majorComparison : Minor.CompareTo(other.Minor);
    }

    public static bool TryParse(string? text, out EditorBetaReleaseVersion version) =>
        TryParseCanonicalVersion((text ?? "").Trim(), out version);

    public static bool TryParseDisplayName(string? text, out EditorBetaReleaseVersion version)
        => TryParseExactVersion(text, DisplayPrefix, out version);

    public static bool TryParseReleaseTag(string? text, out EditorBetaReleaseVersion version)
        => TryParseExactVersion(text, TagPrefix, out version);

    private static bool TryParseExactVersion(
        string? text,
        string prefix,
        out EditorBetaReleaseVersion version)
    {
        version = null!;
        string value = text ?? "";
        if (!value.StartsWith(prefix, StringComparison.Ordinal))
            return false;
        return TryParseCanonicalVersion(value[prefix.Length..], out version);
    }

    private static bool TryParseCanonicalVersion(string value, out EditorBetaReleaseVersion version)
    {
        version = null!;
        string[] components = value.Split('.', StringSplitOptions.None);
        if (components.Length is < 1 or > 2 ||
            !TryParseCanonicalComponent(components[0], allowZero: false, out int major))
        {
            return false;
        }

        int minor = 0;
        if (components.Length == 2 &&
            !TryParseCanonicalComponent(components[1], allowZero: false, out minor))
        {
            return false;
        }

        version = new EditorBetaReleaseVersion(major, minor);
        return true;
    }

    private static bool TryParseCanonicalComponent(string value, bool allowZero, out int number)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out number) ||
            number < (allowZero ? 0 : 1) ||
            !string.Equals(value, number.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
        {
            number = 0;
            return false;
        }
        return true;
    }

    public override string ToString() => ShortName;
}
