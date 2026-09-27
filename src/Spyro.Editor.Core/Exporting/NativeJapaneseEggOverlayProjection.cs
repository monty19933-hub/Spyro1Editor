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
/// Disk data stays source-relative and byte-identical. This normal-load receipt
/// does not allocate the collectible ledger or establish runtime/IRQ lifetime.
/// </summary>
internal sealed class NativeJapaneseEggOverlayProjection
{
    private readonly byte[] data, overlay, executable;
    private readonly string authoredPlan;
    private NativeJapaneseEggOverlayProjection(NativeJapaneseEggAuthoredClassesPlan authored, NativeOwnedOverlayAppendLayout layout)
    {
        data = authored.CopyLevelData(); overlay = authored.CopyOverlay(); executable = authored.CopyExecutable();
        if (layout.RuntimeBase != 0x8007AA38 || overlay.Length != 0xE800 ||
            layout.RetainedByteLength != overlay.Length || !layout.CopyRetainedPrefix().AsSpan().SequenceEqual(overlay) ||
            layout.FinalByteLength <= overlay.Length || layout.FinalByteLength % 0x800 != 0 ||
            authored.OverlayLayout.CopyAnchor != authored.Integration.FinalCopyBufferAddress)
            throw new InvalidDataException("Successor projection requires the complete exact E800 authored overlay prefix.");
        authoredPlan = authored.PlanSha256; SourceCertified = authored.SourceCertified;
        Layout = layout; CopyAnchor = layout.CopyAnchor;
        Delta = checked(CopyAnchor - authored.Integration.FinalCopyBufferAddress);
        var before = NativeLevelRuntimeMemoryReport.DecodeFixture(10, data, authored.Integration.FinalCopyBufferAddress);
        var after = NativeLevelRuntimeMemoryReport.DecodeFixture(10, data, CopyAnchor);
        if (before.ModelRamStart != authored.Projection.ProjectedModelRamStart ||
            before.SceneRamStart != authored.Projection.ProjectedSceneRamStart ||
            before.SceneReadEnd != authored.Projection.ProjectedSceneReadEnd ||
            before.NormalReservedBufferGapBytes != authored.Projection.ProjectedNormalGap ||
            after.ModelRamStart != before.ModelRamStart + Delta || after.SceneRamStart != before.SceneRamStart + Delta ||
            after.SceneReadEnd != before.SceneReadEnd + Delta || after.NormalReservedBufferGapBytes != before.NormalReservedBufferGapBytes - Delta ||
            after.PlacedMobyCount != before.PlacedMobyCount || after.DynamicMobyCapacity != before.DynamicMobyCapacity ||
            !after.KnownPostLoadExtentsDisjoint)
            throw new InvalidDataException("Successor overlay projection disagrees with the existing complete normal-load grammar or exceeds capacity.");
        ModelAddress = after.ModelRamStart; SceneAddress = after.SceneRamStart;
        SceneEnd = after.SceneReadEnd; NormalGap = after.NormalReservedBufferGapBytes;
        byte[] scene = authored.CopyScene();
        int modelOffset = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(0x10)));
        List<NativeJapaneseEggProjectedModel> models = [];
        foreach (var b in authored.Bindings)
        {
            uint root = checked(ModelAddress + (uint)(b.PackageDataOffset - modelOffset));
            uint row = checked(SceneAddress + (uint)b.RowSceneOffset);
            uint privateAddress = checked(SceneAddress + BinaryPrimitives.ReadUInt32LittleEndian(scene.AsSpan(b.RowSceneOffset)));
            var fields = b.Edit.Source.AssetAncestor.ProjectLoaderAddresses(root);
            if (root != b.ProjectedRoot + Delta || row != b.ProjectedRowAddress + Delta || privateAddress != b.ProjectedPrivateAddress + Delta ||
                (ulong)root + (uint)b.ByteLength > SceneAddress || (ulong)privateAddress + 12 > SceneEnd ||
                fields.Count != b.LoaderFields.Count)
                throw new InvalidDataException("Successor model/row/private projection lost the authored package owners.");
            for (int i = 0; i < fields.Count; i++)
                if (fields[i].SourceWord != b.LoaderFields[i].SourceWord ||
                    fields[i].PackageFieldOffset != b.LoaderFields[i].PackageFieldOffset ||
                    fields[i].RuntimeTargetAddress != b.LoaderFields[i].RuntimeTargetAddress + Delta)
                    throw new InvalidDataException("Successor loader projection changed disk pointers or lost a target shift.");
            models.Add(new(b.RowId, b.AuthoredDestinationClass, root, row, privateAddress, Array.AsReadOnly(fields.ToArray())));
        }
        Models = models.AsReadOnly();
        PlanSha256 = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            profile = "authored-egg-successor-overlay-projection-v1", authoredPlan, SourceCertified,
            CopyAnchor, Delta, ModelAddress, SceneAddress, SceneEnd, NormalGap,
            blocks = layout.Blocks.Select(b => new { b.Id, b.Owner.Offset, b.Owner.ByteLength, b.IsPadding })
        })));
    }
    internal NativeOwnedOverlayAppendLayout Layout { get; }
    internal bool SourceCertified { get; }
    internal uint CopyAnchor { get; }
    internal uint Delta { get; }
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
    internal void RequireCurrent(NativeJapaneseEggAuthoredClassesPlan authored)
    {
        ArgumentNullException.ThrowIfNull(authored);
        if (authored.SourceCertified != SourceCertified || authored.PlanSha256 != authoredPlan ||
            !authored.CopyLevelData().AsSpan().SequenceEqual(data) || !authored.CopyOverlay().AsSpan().SequenceEqual(overlay) ||
            !authored.CopyExecutable().AsSpan().SequenceEqual(executable))
            throw new InvalidDataException("Successor projection is stale for this exact authored data/overlay/executable.");
    }
}
