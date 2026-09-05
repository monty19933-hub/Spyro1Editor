namespace Spyro.Editor.Core.Exporting;

[Flags]
public enum StructuralAddressChanges
{
    None = 0,
    LevelDataComponents = 1,
    NativeSceneSubfiles = 2,
    FollowingWadEntries = 4,
    ExecutableExtent = 8
}

public sealed record StructuralExportAddressEffect(
    string LevelKey, StructuralAddressChanges Changes, int WadGrowthBytes)
{
    /// <summary>Derive address effects from the selected writer's checked invariants, not output size alone.</summary>
    public static StructuralExportAddressEffect FromPrivateTexturePlan(NativeTerrainTexturePrivateRecordBatchPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (!plan.SourcePreimagesVerified || !plan.ExclusiveImageWriterSelected)
            throw new InvalidDataException("Private texture address effects require a source-verified exclusive writer.");
        if (plan.WriterKind == NativeTerrainTexturePrivateImageWriterKind.FixedTail &&
            plan.FixedTailPlan is { FixedSubfileBoundaryPreserved: true } fixedTail &&
            plan.SectorRelocationPlan == null &&
            fixedTail.Append is { FixedSubfileBoundaryPreserved: true, ArchiveHeadersRequireNoFixup: true, ShiftedComponentChainReparsed: true } append &&
            append.Patch is { SourceByteLength: > 0 } patch && patch.SourceByteLength == patch.OutputByteLength)
        {
            // The level-data component chain moves, but the separate native
            // scene subfile and all outer WAD/ISO extents retain their addresses.
            return new(plan.TargetLevelKey, StructuralAddressChanges.LevelDataComponents, 0);
        }
        if (plan.WriterKind == NativeTerrainTexturePrivateImageWriterKind.SectorRelocation &&
            plan.FixedTailPlan == null &&
            plan.SectorRelocationPlan is { StructuralSectorGrowthVerified: true } sector &&
            sector.Structural is { SectorGrowthBytes: > 0 } structural &&
            structural.SectorGrowthBytes % 2048 == 0 &&
            structural.ExpandedWadSize - (long)structural.OriginalWadSize == structural.SectorGrowthBytes)
        {
            return new(plan.TargetLevelKey, StructuralAddressChanges.LevelDataComponents |
                StructuralAddressChanges.NativeSceneSubfiles | StructuralAddressChanges.FollowingWadEntries |
                StructuralAddressChanges.ExecutableExtent, structural.SectorGrowthBytes);
        }
        throw new InvalidDataException("Private texture writer has no consistent checked address-preservation effect.");
    }
}

public sealed record StructuralSavedWriterScope(
    string LevelName,
    bool IsPrivateTextureDestination,
    bool HasObjectEdits,
    bool HasTerrainEdits,
    bool HasCustomTerrainTextures,
    bool HasNativeTerrainTextureRelocations,
    bool HasLevelTextEdit,
    bool HasLevelMusicEdit,
    bool HasSkyboxEdit);

/// <summary>
/// Interim composition boundary, not a relocation map. The private texture
/// transaction already rebases its included terrain writes. Other exporters
/// must earn an explicit post-relocation contract before running after it.
/// Existing fixed-tail composition is retained because scene/outer addresses
/// are proved unchanged and its level-data consumers reparse the live chain.
/// </summary>
public static class StructuralExportCompositionPolicy
{
    private const StructuralAddressChanges OuterChanges = StructuralAddressChanges.NativeSceneSubfiles |
        StructuralAddressChanges.FollowingWadEntries | StructuralAddressChanges.ExecutableExtent;

    public static void RequireCompatible(IReadOnlyList<StructuralExportAddressEffect> effects,
        IReadOnlyList<StructuralSavedWriterScope> targets)
    {
        string? blocker = FindBlocker(effects, targets);
        if (blocker != null) throw new InvalidOperationException(blocker);
    }

    public static string? FindBlocker(IReadOnlyList<StructuralExportAddressEffect> effects,
        IReadOnlyList<StructuralSavedWriterScope> targets)
    {
        ArgumentNullException.ThrowIfNull(effects);
        ArgumentNullException.ThrowIfNull(targets);
        HashSet<string> keys = new(StringComparer.OrdinalIgnoreCase);
        int growth = 0;
        int relocating = 0;
        foreach (StructuralExportAddressEffect effect in effects)
        {
            ArgumentNullException.ThrowIfNull(effect);
            if (string.IsNullOrWhiteSpace(effect.LevelKey) || !keys.Add(effect.LevelKey) ||
                effect.WadGrowthBytes < 0 || effect.WadGrowthBytes % 2048 != 0 ||
                (effect.Changes & ~(OuterChanges | StructuralAddressChanges.LevelDataComponents)) != 0)
                throw new InvalidDataException("Malformed or duplicate structural address effect.");
            bool changesOuter = (effect.Changes & OuterChanges) != 0;
            if (changesOuter != (effect.WadGrowthBytes > 0) ||
                changesOuter && (effect.Changes & OuterChanges) != OuterChanges)
                throw new InvalidDataException("Structural growth and scene/archive address effects disagree.");
            growth = checked(growth + effect.WadGrowthBytes);
            if (changesOuter) relocating++;
        }
        if (relocating == 0) return null;

        List<string> blocked = [];
        foreach (StructuralSavedWriterScope target in targets)
        {
            ArgumentNullException.ThrowIfNull(target);
            List<string> writers = [];
            if (target.HasObjectEdits) writers.Add("object/path/chest edits");
            if (target.HasCustomTerrainTextures) writers.Add("custom/imported-image terrain textures");
            if (target.HasSkyboxEdit) writers.Add("skybox/environment edits");
            // Name/music exporters resolve live EXE extents, but their broader
            // composed acceptance remains deliberately unchanged in this fix.
            if (target.HasLevelTextEdit) writers.Add("level-name edits");
            if (target.HasLevelMusicEdit) writers.Add("music edits");
            if (!target.IsPrivateTextureDestination && target.HasTerrainEdits)
                writers.Add("ordinary terrain edits outside the private-texture transaction");
            if (!target.IsPrivateTextureDestination && target.HasNativeTerrainTextureRelocations)
                writers.Add("native texture edits outside the private-texture transaction");
            if (writers.Count > 0) blocked.Add($"{target.LevelName}: {string.Join(", ", writers)}");
        }
        return blocked.Count == 0 ? null :
            $"The private-texture build relocates native scene/WAD/ISO addresses by +0x{growth:X} for {relocating} destination(s). " +
            $"The following saved writers have no verified post-relocation composition contract: {string.Join("; ", blocked)}. " +
            "Build the private textures separately until those writers support relocation. Ordinary terrain and native-texture edits for the private destinations are already folded into the transaction.";
    }
}
