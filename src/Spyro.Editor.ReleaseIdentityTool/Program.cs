using Spyro.Editor.Core.Updates;

if (args.Length != 3 ||
    !EditorBetaReleaseVersion.TryParse(args[1], out EditorBetaReleaseVersion expectedVersion) ||
    string.IsNullOrWhiteSpace(args[2]))
{
    Console.Error.WriteLine("Usage: Spyro.Editor.ReleaseIdentityTool <Spyro.Editor.App.dll> <public-beta-version> <internal-version>");
    return 2;
}

string assemblyPath = Path.GetFullPath(args[0]);
if (!File.Exists(assemblyPath))
{
    Console.Error.WriteLine($"Application assembly not found: {assemblyPath}");
    return 1;
}

try
{
    await using FileStream input = new(assemblyPath, FileMode.Open, FileAccess.Read, FileShare.Read);
    EditorAssemblyReleaseIdentity identity = EditorAssemblyReleaseIdentityReader.Read(input, assemblyPath);
    if (!string.Equals(identity.AssemblyName, EditorAssemblyReleaseIdentityReader.ExpectedAssemblyName, StringComparison.Ordinal) ||
        identity.PublicBetaVersion != expectedVersion.Major ||
        identity.PublicVersion != expectedVersion ||
        !identity.HasExplicitPublicVersion ||
        !identity.HasExplicitReleaseManifestSchema ||
        identity.ReleaseManifestSchema != (expectedVersion.IsIncremental ? 2 : 1) ||
        !string.Equals(identity.InternalVersion, args[2], StringComparison.Ordinal))
    {
        Console.Error.WriteLine(
            $"Release identity mismatch: assembly={identity.AssemblyName}, publicBeta={identity.PublicBetaVersion}, publicVersion={identity.PublicVersion.CanonicalVersion}, manifestSchema={identity.ReleaseManifestSchema}, internalVersion={identity.InternalVersion}; " +
            $"expected assembly={EditorAssemblyReleaseIdentityReader.ExpectedAssemblyName}, publicBeta={expectedVersion.Major}, publicVersion={expectedVersion.CanonicalVersion}, manifestSchema={(expectedVersion.IsIncremental ? 2 : 1)}, internalVersion={args[2]}.");
        return 1;
    }

    Console.WriteLine(
        $"Verified assembly identity: {identity.AssemblyName}; {identity.PublicVersion.DisplayName}; legacy beta {identity.PublicBetaVersion}; manifest schema {identity.ReleaseManifestSchema}; {identity.InternalVersion}; {assemblyPath}");
    return 0;
}
catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException)
{
    Console.Error.WriteLine($"Release identity verification failed: {exception.Message}");
    return 1;
}
