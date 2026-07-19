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
        string value = typeof(AppReleaseIdentity).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => string.Equals(
                attribute.Key,
                EditorAssemblyReleaseIdentityReader.BetaReleaseMetadataKey,
                StringComparison.Ordinal))?
            .Value ?? "";
        if (!int.TryParse(value, out int number) || number <= 0)
            throw new InvalidOperationException(
                $"The app assembly is missing a valid {EditorAssemblyReleaseIdentityReader.BetaReleaseMetadataKey} value.");
        return new EditorBetaReleaseVersion(number);
    }
}
