namespace Spyro.Editor.Core.Exporting;

public sealed record NativeSkyLinkedPortalExpansionRisk(
    string TargetLevelKey,
    string TargetLevelName,
    string ReplacementName,
    int ReplacementByteLength,
    string StorageLevelKey,
    string StorageLevelName,
    int StorageBlockIndex,
    int NativeCapacityBytes)
{
    public int GrowthBytes => ReplacementByteLength - NativeCapacityBytes;
}

public sealed class NativeSkyLinkedPortalCapacityException : InvalidOperationException
{
    public NativeSkyLinkedPortalCapacityException(NativeSkyLinkedPortalExpansionRisk risk)
        : base(BuildMessage(risk))
    {
        Risk = risk;
    }

    public NativeSkyLinkedPortalExpansionRisk Risk { get; }

    private static string BuildMessage(NativeSkyLinkedPortalExpansionRisk risk)
    {
        return
            $"Unsafe linked portal sky expansion blocked: {risk.TargetLevelName} <- {risk.ReplacementName} needs {risk.ReplacementByteLength:N0} bytes, " +
            $"but its linked {risk.StorageLevelName} portal copy (block {risk.StorageBlockIndex}) has {risk.NativeCapacityBytes:N0} bytes of native capacity " +
            $"(+{risk.GrowthBytes:N0} bytes). Growing linked homeworld portal skies is not runtime-proven and can crash the game while loading " +
            $"{risk.StorageLevelName}. Choose a {risk.TargetLevelName} sky source no larger than {risk.NativeCapacityBytes:N0} bytes, or reset that saved sky edit. " +
            "No sky BIN/CUE or sky patch plan was written.";
    }
}

public static class NativeSkyLinkedPortalSafety
{
    public static IReadOnlyList<NativeSkyLinkedPortalExpansionRisk> FindUnprovenExpansions(
        Spyro1LevelSkyBlockLayout targetLayout,
        int replacementByteLength,
        string replacementName)
    {
        if (replacementByteLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(replacementByteLength), "Replacement sky length must be positive.");

        string sourceName = string.IsNullOrWhiteSpace(replacementName) ? "replacement sky" : replacementName.Trim();
        return targetLayout.LinkedPrimarySkyCopies
            .Where(reference =>
                (!string.Equals(reference.LevelKey, targetLayout.Key, StringComparison.OrdinalIgnoreCase) || reference.BlockIndex != 0) &&
                replacementByteLength > reference.ByteLength)
            .Select(reference => new NativeSkyLinkedPortalExpansionRisk(
                TargetLevelKey: targetLayout.Key,
                TargetLevelName: targetLayout.DisplayName,
                ReplacementName: sourceName,
                ReplacementByteLength: replacementByteLength,
                StorageLevelKey: reference.LevelKey,
                StorageLevelName: reference.LevelName,
                StorageBlockIndex: reference.BlockIndex,
                NativeCapacityBytes: reference.ByteLength))
            .OrderBy(risk => risk.NativeCapacityBytes)
            .ThenBy(risk => risk.StorageLevelName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(risk => risk.StorageBlockIndex)
            .ToArray();
    }

    public static void ThrowIfUnprovenExpansion(
        Spyro1LevelSkyBlockLayout targetLayout,
        int replacementByteLength,
        string replacementName)
    {
        NativeSkyLinkedPortalExpansionRisk? risk = FindUnprovenExpansions(
                targetLayout,
                replacementByteLength,
                replacementName)
            .FirstOrDefault();
        if (risk != null)
            throw new NativeSkyLinkedPortalCapacityException(risk);
    }
}
