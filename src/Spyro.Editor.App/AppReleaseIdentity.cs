using System.Reflection;
using Spyro.Editor.Core.Updates;

namespace Spyro.Editor.App;

internal static class AppReleaseIdentity
{
    private static readonly Lazy<EditorBetaReleaseVersion> PublicVersion = new(ReadPublicVersion);

    public static EditorBetaReleaseVersion BetaVersion => PublicVersion.Value;
    public static EditorStableReleaseVersion? StableVersion { get; } = ReadStableVersion();
    public static bool IsStable => StableVersion != null;
    public static string DisplayName => StableVersion?.DisplayName ?? BetaVersion.DisplayName;
    private static EditorStableReleaseVersion? ReadStableVersion()
    {
        var metadata=typeof(AppReleaseIdentity).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().ToArray();
        var channel=metadata.Where(a=>a.Key==EditorStableAssemblyReleaseIdentityReader.ChannelMetadataKey).ToArray();
        var stable=metadata.Where(a=>a.Key==EditorStableAssemblyReleaseIdentityReader.PublicReleaseMetadataKey).ToArray();
        var schema=metadata.Where(a=>a.Key==EditorStableAssemblyReleaseIdentityReader.ReleaseManifestSchemaMetadataKey).ToArray();
        if(channel.Length==0&&stable.Length==0&&schema.Length==0)return null;
        if(channel.Length!=1||channel[0].Value!="stable"||stable.Length!=1||!EditorStableReleaseVersion.TryParse(stable[0].Value,out var version)||schema.Length!=1||schema[0].Value!="3")throw new InvalidOperationException("Invalid stable-channel assembly metadata.");
        string info=typeof(AppReleaseIdentity).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion??"";
        if(!EditorSemanticVersion.TryParse(info,out var build)||build.IsPrerelease||build.Major!=version.Major||build.Minor!=version.Minor||build.Patch!=version.Patch)throw new InvalidOperationException("Stable internal identity must match without beta/prerelease identifiers.");
        return version;
    }
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
