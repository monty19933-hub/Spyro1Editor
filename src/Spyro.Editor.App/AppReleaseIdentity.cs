using System.Reflection;
using Spyro.Editor.Core.Updates;

namespace Spyro.Editor.App;

internal static class AppReleaseIdentity
{
    private static readonly Lazy<EditorBetaReleaseVersion> PublicVersion = new(ReadPublicVersion);

    public static EditorBetaReleaseVersion BetaVersion => PublicVersion.Value;
    public static string DisplayName => BetaVersion.DisplayName;
    public static string InternalVersion => typeof(AppReleaseIdentity).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
        .InformationalVersion ?? "unknown";
    public static string DiagnosticVersion => $"{DisplayName} ({InternalVersion})";

    private static EditorBetaReleaseVersion ReadPublicVersion()
    {
        AssemblyMetadataAttribute[] metadata = typeof(AppReleaseIdentity).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .ToArray();
        string[] legacyValues = metadata
            .Where(attribute => string.Equals(
                attribute.Key,
                EditorAssemblyReleaseIdentityReader.BetaReleaseMetadataKey,
                StringComparison.Ordinal))
            .Select(attribute => attribute.Value ?? "")
            .ToArray();
        if (legacyValues.Length != 1 ||
            !int.TryParse(legacyValues[0], out int legacyMajor) ||
            legacyMajor <= 0 ||
            !string.Equals(
                legacyValues[0],
                legacyMajor.ToString(System.Globalization.CultureInfo.InvariantCulture),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"The app assembly is missing a valid {EditorAssemblyReleaseIdentityReader.BetaReleaseMetadataKey} value.");
        }

        string[] publicValues = metadata
            .Where(attribute => string.Equals(
                attribute.Key,
                EditorAssemblyReleaseIdentityReader.PublicReleaseMetadataKey,
                StringComparison.Ordinal))
            .Select(attribute => attribute.Value ?? "")
            .ToArray();
        EditorBetaReleaseVersion version;
        if (publicValues.Length == 0)
        {
            version = new EditorBetaReleaseVersion(legacyMajor);
        }
        else if (publicValues.Length != 1 ||
                 !EditorBetaReleaseVersion.TryParse(publicValues[0], out version) ||
                 !string.Equals(publicValues[0], version.CanonicalVersion, StringComparison.Ordinal) ||
                 version.Major != legacyMajor)
        {
            throw new InvalidOperationException(
                $"The app assembly has invalid or mismatched {EditorAssemblyReleaseIdentityReader.PublicReleaseMetadataKey} metadata.");
        }

        string[] schemaValues = metadata
            .Where(attribute => string.Equals(
                attribute.Key,
                EditorAssemblyReleaseIdentityReader.ReleaseManifestSchemaMetadataKey,
                StringComparison.Ordinal))
            .Select(attribute => attribute.Value ?? "")
            .ToArray();
        int expectedSchema = version.IsIncremental ? 2 : 1;
        if (schemaValues.Length > 0 &&
            (schemaValues.Length != 1 ||
             !int.TryParse(schemaValues[0], out int schema) ||
             !string.Equals(
                 schemaValues[0],
                 schema.ToString(System.Globalization.CultureInfo.InvariantCulture),
                 StringComparison.Ordinal) ||
             schema != expectedSchema))
        {
            throw new InvalidOperationException(
                $"The app assembly has invalid or mismatched {EditorAssemblyReleaseIdentityReader.ReleaseManifestSchemaMetadataKey} metadata.");
        }
        return version;
    }
}
