using System.Globalization;
using Spyro.Editor.Core.Updates;

if (args.Length != 3 ||
    !int.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out int expectedBeta) ||
    expectedBeta <= 0 ||
    string.IsNullOrWhiteSpace(args[2]))
{
    Console.Error.WriteLine("Usage: Spyro.Editor.ReleaseIdentityTool <Spyro.Editor.App.dll> <public-beta-number> <internal-version>");
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
        identity.PublicBetaVersion != expectedBeta ||
        !string.Equals(identity.InternalVersion, args[2], StringComparison.Ordinal))
    {
        Console.Error.WriteLine(
            $"Release identity mismatch: assembly={identity.AssemblyName}, publicBeta={identity.PublicBetaVersion}, internalVersion={identity.InternalVersion}; " +
            $"expected assembly={EditorAssemblyReleaseIdentityReader.ExpectedAssemblyName}, publicBeta={expectedBeta}, internalVersion={args[2]}.");
        return 1;
    }

    Console.WriteLine(
        $"Verified assembly identity: {identity.AssemblyName}; Spyro Editor Beta V{identity.PublicBetaVersion}; {identity.InternalVersion}; {assemblyPath}");
    return 0;
}
catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException)
{
    Console.Error.WriteLine($"Release identity verification failed: {exception.Message}");
    return 1;
}
