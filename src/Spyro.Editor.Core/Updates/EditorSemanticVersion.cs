namespace Spyro.Editor.Core.Updates;

public sealed record EditorSemanticVersion(
    int Major,
    int Minor,
    int Patch,
    IReadOnlyList<string> PrereleaseIdentifiers) : IComparable<EditorSemanticVersion>
{
    public bool IsPrerelease => PrereleaseIdentifiers.Count > 0;

    public static bool TryParse(string? text, out EditorSemanticVersion version)
    {
        version = new EditorSemanticVersion(0, 0, 0, Array.Empty<string>());
        string value = (text ?? "").Trim();
        if (value.StartsWith('v') || value.StartsWith('V'))
            value = value[1..];
        int metadata = value.IndexOf('+');
        if (metadata >= 0)
            value = value[..metadata];
        string[] releaseAndPrerelease = value.Split('-', 2, StringSplitOptions.TrimEntries);
        string[] numbers = releaseAndPrerelease[0].Split('.', StringSplitOptions.TrimEntries);
        if (numbers.Length != 3 ||
            !int.TryParse(numbers[0], out int major) || major < 0 ||
            !int.TryParse(numbers[1], out int minor) || minor < 0 ||
            !int.TryParse(numbers[2], out int patch) || patch < 0)
        {
            return false;
        }

        string[] prerelease = releaseAndPrerelease.Length == 1
            ? Array.Empty<string>()
            : releaseAndPrerelease[1]
                .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (releaseAndPrerelease.Length > 1 && prerelease.Length == 0)
            return false;
        if (prerelease.Any(identifier => !identifier.All(character => char.IsAsciiLetterOrDigit(character) || character == '-')))
            return false;
        version = new EditorSemanticVersion(major, minor, patch, prerelease);
        return true;
    }

    public int CompareTo(EditorSemanticVersion? other)
    {
        if (other == null)
            return 1;
        int core = Major.CompareTo(other.Major);
        if (core == 0)
            core = Minor.CompareTo(other.Minor);
        if (core == 0)
            core = Patch.CompareTo(other.Patch);
        if (core != 0)
            return core;
        if (!IsPrerelease && !other.IsPrerelease)
            return 0;
        if (!IsPrerelease)
            return 1;
        if (!other.IsPrerelease)
            return -1;

        int shared = Math.Min(PrereleaseIdentifiers.Count, other.PrereleaseIdentifiers.Count);
        for (int index = 0; index < shared; index++)
        {
            string left = PrereleaseIdentifiers[index];
            string right = other.PrereleaseIdentifiers[index];
            bool leftNumber = int.TryParse(left, out int leftValue);
            bool rightNumber = int.TryParse(right, out int rightValue);
            int result = leftNumber && rightNumber
                ? leftValue.CompareTo(rightValue)
                : leftNumber
                    ? -1
                    : rightNumber
                        ? 1
                        : string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
            if (result != 0)
                return result;
        }
        return PrereleaseIdentifiers.Count.CompareTo(other.PrereleaseIdentifiers.Count);
    }

    public override string ToString()
    {
        string core = $"{Major}.{Minor}.{Patch}";
        return IsPrerelease ? $"{core}-{string.Join('.', PrereleaseIdentifiers)}" : core;
    }
}
