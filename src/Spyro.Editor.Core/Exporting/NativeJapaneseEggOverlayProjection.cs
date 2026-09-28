using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;

namespace Spyro.Editor.Core.Exporting;

internal sealed record NativeJapaneseEggProjectedModel(string RowId, ushort AuthoredClass,
    uint RootAddress, uint RowAddress, uint PrivateAddress, IReadOnlyList<NativeJapaneseEggLoaderAddress> LoaderFields);

/// <summary>
/// Exact authored-body reprojection for a separately owned successor overlay.
/// Retains the complete predecessor, not its apparent used bytes or padding.
/// The original route retains byte-identical source-relative disk data; the
/// closed controller-workspace route consumes its exact model-only insertion.
/// This normal-load receipt
/// does not allocate the collectible ledger or establish runtime/IRQ lifetime.
/// </summary>
internal sealed class NativeJapaneseEggOverlayProjection
{
    private readonly byte[] authoredData, data, overlay, executable;
    private readonly string authoredPlan;
    private NativeJapaneseEggOverlayProjection(NativeJapaneseEggAuthoredClassesPlan authored, NativeOwnedOverlayAppendLayout layout,
        NativeCollectibleSaveWorkspacePlan? workspace = null)
    {
        if (workspace is not null)
        {
            workspace.RequireCurrent(workspace.Ancestry);
            if (workspace.Profile != NativeCollectibleSaveWorkspacePlan.WorkspaceProfile.Controller || workspace.Controller is null ||
                !ReferenceEquals(workspace.Ancestry.Authored, authored) || !ReferenceEquals(workspace.Ancestry.Lifecycle.Layout, layout))
                throw new InvalidDataException("Workspace projection requires the exact source-issued controller ancestry and unchanged overlay owner.");
        }
        Workspace = workspace;
        authoredData = authored.CopyLevelData(); data = workspace?.CopyLevelData() ?? authoredData.ToArray();
        overlay = authored.CopyOverlay(); executable = authored.CopyExecutable();
        FinalDomain = workspace?.FinalDomain ?? authored.FinalDomain;
        FinalDataSha256 = workspace?.FinalDataSha256 ?? authored.FinalDataSha256;
        if (layout.RuntimeBase != 0x8007AA38 || overlay.Length != 0xE800 ||
            layout.RetainedByteLength != overlay.Length || !layout.CopyRetainedPrefix().AsSpan().SequenceEqual(overlay) ||
            layout.FinalByteLength <= overlay.Length || layout.FinalByteLength % 0x800 != 0 ||
            authored.OverlayLayout.CopyAnchor != authored.Integration.FinalCopyBufferAddress)
            throw new InvalidDataException("Successor projection requires the complete exact E800 authored overlay prefix.");
        authoredPlan = authored.PlanSha256; SourceCertified = authored.SourceCertified;
        Layout = layout; CopyAnchor = layout.CopyAnchor;
        ModelDelta = checked(CopyAnchor - authored.Integration.FinalCopyBufferAddress);
        SceneDelta = checked(ModelDelta + (uint)(workspace?.GrowthBytes ?? 0));
        var before = NativeLevelRuntimeMemoryReport.DecodeFixture(10, authoredData, authored.Integration.FinalCopyBufferAddress);
        var after = NativeLevelRuntimeMemoryReport.DecodeFixture(10, data, CopyAnchor);
        if (before.ModelRamStart != authored.Projection.ProjectedModelRamStart ||
            before.SceneRamStart != authored.Projection.ProjectedSceneRamStart ||
            before.SceneReadEnd != authored.Projection.ProjectedSceneReadEnd ||
            before.NormalReservedBufferGapBytes != authored.Projection.ProjectedNormalGap ||
            after.ModelRamStart != before.ModelRamStart + ModelDelta || after.SceneRamStart != before.SceneRamStart + SceneDelta ||
            after.SceneReadEnd != before.SceneReadEnd + SceneDelta || after.NormalReservedBufferGapBytes != before.NormalReservedBufferGapBytes - SceneDelta ||
            after.ModelBytesRead != before.ModelBytesRead + (workspace?.GrowthBytes ?? 0) || after.SceneBytesRead != before.SceneBytesRead ||
            after.UsedLevelDataBytes != before.UsedLevelDataBytes ||
            after.PlacedMobyCount != before.PlacedMobyCount || after.DynamicMobyCapacity != before.DynamicMobyCapacity ||
            !after.KnownPostLoadExtentsDisjoint)
            throw new InvalidDataException("Successor overlay projection disagrees with the existing complete normal-load grammar or exceeds capacity.");
        ModelAddress = after.ModelRamStart; SceneAddress = after.SceneRamStart;
        SceneEnd = after.SceneReadEnd; NormalGap = after.NormalReservedBufferGapBytes;
        byte[] scene = authored.CopyScene();
        int sceneOffset = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(0x18)));
        if (!data.AsSpan(sceneOffset, scene.Length).SequenceEqual(scene) ||
            (workspace is not null && !workspace.RevertExact(data).AsSpan().SequenceEqual(authoredData)))
            throw new InvalidDataException("Workspace projection must retain the complete exact authored scene and data inverse.");
        int modelOffset = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(0x10)));
        List<NativeJapaneseEggProjectedModel> models = [];
        foreach (var b in authored.Bindings)
        {
            uint root = checked(ModelAddress + (uint)(b.PackageDataOffset - modelOffset));
            uint row = checked(SceneAddress + (uint)b.RowSceneOffset);
            uint privateAddress = checked(SceneAddress + BinaryPrimitives.ReadUInt32LittleEndian(scene.AsSpan(b.RowSceneOffset)));
            var fields = b.Edit.Source.AssetAncestor.ProjectLoaderAddresses(root);
            if (root != b.ProjectedRoot + ModelDelta || row != b.ProjectedRowAddress + SceneDelta || privateAddress != b.ProjectedPrivateAddress + SceneDelta ||
                (ulong)root + (uint)b.ByteLength > SceneAddress || (ulong)privateAddress + 12 > SceneEnd ||
                fields.Count != b.LoaderFields.Count)
                throw new InvalidDataException("Successor model/row/private projection lost the authored package owners.");
            for (int i = 0; i < fields.Count; i++)
                if (fields[i].SourceWord != b.LoaderFields[i].SourceWord ||
                    fields[i].PackageFieldOffset != b.LoaderFields[i].PackageFieldOffset ||
                    fields[i].RuntimeTargetAddress != b.LoaderFields[i].RuntimeTargetAddress + ModelDelta)
                    throw new InvalidDataException("Successor loader projection changed disk pointers or lost a target shift.");
            models.Add(new(b.RowId, b.AuthoredDestinationClass, root, row, privateAddress, Array.AsReadOnly(fields.ToArray())));
        }
        Models = models.AsReadOnly();
        // Keep the legacy identity serialization byte-for-byte unchanged.
        PlanSha256 = workspace is null ? Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            profile = "authored-egg-successor-overlay-projection-v1", authoredPlan, SourceCertified,
            CopyAnchor, Delta, ModelAddress, SceneAddress, SceneEnd, NormalGap,
            blocks = layout.Blocks.Select(b => new { b.Id, b.Owner.Offset, b.Owner.ByteLength, b.IsPadding })
        }))) : Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            profile = "authored-egg-controller-workspace-projection-v1", authoredPlan, SourceCertified,
            workspace = workspace.PlanSha256, FinalDataSha256, CopyAnchor, ModelDelta, SceneDelta, ModelAddress, SceneAddress, SceneEnd, NormalGap,
            blocks = layout.Blocks.Select(b => new { b.Id, b.Owner.Offset, b.Owner.ByteLength, b.IsPadding })
        })));
    }
    internal NativeOwnedOverlayAppendLayout Layout { get; }
    internal bool SourceCertified { get; }
    internal uint CopyAnchor { get; }
    // Legacy consumers may only request a uniform displacement.
    internal uint Delta => ModelDelta == SceneDelta ? ModelDelta : throw new InvalidDataException("Workspace projection has separate model and scene displacements.");
    internal uint ModelDelta { get; }
    internal uint SceneDelta { get; }
    internal NativeCollectibleSaveWorkspacePlan? Workspace { get; }
    internal NativeAddressSpace FinalDomain { get; }
    internal string FinalDataSha256 { get; }
    internal byte[] CopyLevelData() => data.ToArray();
    internal uint ModelAddress { get; }
    internal uint SceneAddress { get; }
    internal uint SceneEnd { get; }
    internal long NormalGap { get; }
    internal string PlanSha256 { get; }
    internal IReadOnlyList<NativeJapaneseEggProjectedModel> Models { get; }
    internal bool NativeResidenceIssued => false;
    internal bool RuntimeFitProven => false;
    internal bool PublicationApproved => false;
    internal static NativeJapaneseEggOverlayProjection Create(NativeJapaneseEggAuthoredClassesPlan authored, NativeOwnedOverlayAppendLayout layout)
    {
        ArgumentNullException.ThrowIfNull(authored); ArgumentNullException.ThrowIfNull(layout);
        return new(authored, layout);
    }
    internal static NativeJapaneseEggOverlayProjection CreateForWorkspace(NativeCollectibleSaveWorkspacePlan workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        return new(workspace.Ancestry.Authored, workspace.Ancestry.Lifecycle.Layout, workspace);
    }
    internal void RequireWorkspaceCurrent(NativeCollectibleSaveWorkspacePlan workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        if (!ReferenceEquals(Workspace, workspace)) throw new InvalidDataException("Projection requires its exact controller workspace instance.");
        RequireCurrent(workspace.Ancestry.Authored);
    }
    internal void RequireCurrent(NativeJapaneseEggAuthoredClassesPlan authored)
    {
        ArgumentNullException.ThrowIfNull(authored);
        if (authored.SourceCertified != SourceCertified || authored.PlanSha256 != authoredPlan ||
            !authored.CopyLevelData().AsSpan().SequenceEqual(authoredData) || !authored.CopyOverlay().AsSpan().SequenceEqual(overlay) ||
            !authored.CopyExecutable().AsSpan().SequenceEqual(executable))
            throw new InvalidDataException("Successor projection is stale for this exact authored data/overlay/executable.");
        if (Workspace is not null)
        {
            Workspace.RequireCurrent(Workspace.Ancestry);
            if (!ReferenceEquals(authored, Workspace.Ancestry.Authored) || !Workspace.CopyLevelData().AsSpan().SequenceEqual(data) ||
                Workspace.FinalDataSha256 != FinalDataSha256 || !ReferenceEquals(Workspace.FinalDomain, FinalDomain))
                throw new InvalidDataException("Projection is stale for its exact final controller workspace.");
        }
    }
}
