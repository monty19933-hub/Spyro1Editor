using System.Buffers.Binary;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;

namespace Spyro.Editor.Core.Exporting;

public enum NativeWizardFamilySoundNamespace { LocalModelSlot, GlobalSelector }
public sealed record NativeWizardFamilyAssetOwner(string Id, int BlockOffset, int ByteLength, string Sha256);
public sealed record NativeWizardFamilySourceSpan(string OwnerId, int OwnerOffset, long SourceWadOffset, int ByteLength, string Sha256);
public sealed record NativeWizardFamilyTexture(string Id, string PixelOwnerId, string PaletteOwnerId,
    int SourcePixelOffset, int SourcePaletteOffset, int RowBytes, int Rows, ushort SourceClut, ushort SourcePage);
public sealed record NativeWizardFamilySoundRequirement(string Id, NativeWizardFamilySoundNamespace Namespace,
    int Selector, int SourceDescriptorIndex, string DescriptorOwnerId, string SampleOwnerId,
    int? ExactTownDescriptorIndex, bool SupportedOneShotImport);
public sealed record NativeWizardFamilySceneReference(string OwnerId, int FieldOffset,
    string TargetOwnerId, int TargetOwnerOffset, uint SourceEncodedValue);
public sealed record NativeWizardFamilySoundReference(string OwnerId, int FieldOffset, int ByteWidth,
    string TargetOwnerId, uint SourceEncodedValue);

/// <summary>A source-encoded patch field, not a target placement or a rewritten descriptor.</summary>
public sealed class NativeWizardFamilyTextureReference
{
    private readonly byte[] _source;
    internal NativeWizardFamilyTextureReference(string owner, int offset, string texture, ReadOnlySpan<byte> source)
    { OwnerId = owner; FieldOffset = offset; TextureId = texture; _source = source.ToArray(); }
    public string OwnerId { get; }
    public int FieldOffset { get; }
    public string TextureId { get; }
    public int ByteLength => _source.Length;
    public byte[] CopySourceBytes() => _source.ToArray();
}

/// <summary>
/// Exact Wizard Peak33/T6 source-form detached assets. Every encoding retains
/// its ORIGINAL source domain; this is not a relocated VRAM/SPU namespace, model
/// registration, scene, behavior, fresh level, runtime or publication capability.
/// No caller bytes, profile flags or expected hashes can construct this plan.
/// </summary>
public sealed partial class NativeWizardFamilyAssetPlan
{
    private readonly byte[] _block;
    private readonly NativeWizardFamilyAssetSource _source;
    private NativeWizardFamilyAssetPlan(NativeWizardFamilyAssetSource source)
    {
        _source = source;
        byte[] donor = source.CopyDonor();
        SourceVariantId = source.Row.SourceVariantId;
        AnimationTextureHeaderSha256 = source.AnimationTextureHeaderSha256;
        SharedGeometrySha256 = source.SharedGeometrySha256;
        var owners = new List<NativeWizardFamilyAssetOwner>();
        var reads = new List<NativeWizardFamilySourceSpan>();
        var textures = new List<NativeWizardFamilyTexture>();
        var textureRefs = new List<NativeWizardFamilyTextureReference>();
        var sounds = new List<NativeWizardFamilySoundRequirement>();
        var soundRefs = new List<NativeWizardFamilySoundReference>();
        var output = new List<byte>();
        byte[] wizard = Package(0x11B, 0x19B518, 0x35B4), bolt = Package(0x26, 0x1CAC88, 0x564);
        AddDirect("wizard.model", 0x19B518, wizard);
        AddDirect("bolt.model", 0x1CAC88, bolt);
        AddDirect("player.animation8", source.AnimationDataOffset, source.Animation.CopyRecord());
        AddDirect("wizard.row", 0x1D4B88, source.Row.CopyRecord());
        AddDirect("wizard.private", 0x1DAB50, donor.AsSpan(0x1DAB50, 0x50).ToArray());
        byte[] particle = donor.AsSpan(0x173518, 12).ToArray();
        var particleTable = NativeParticleDescriptorCodec.Decode(donor.AsSpan(source.DonorSound.ParticleOffset,
            source.DonorSound.SoundOffset - source.DonorSound.ParticleOffset));
        if (NativeParticleDescriptorCodec.RequireRecord(particle) != 7 || particleTable.Ids.Count < 2 || particleTable.Ids[1] != 7)
            throw new InvalidDataException("Source descriptor07 does not own the second complete native record.");
        AddDirect("particle7", 0x173518, particle);
        RequireScenePointers();
        AddModelTextures("wizard.model", wizard, 51, [0x238, 0x700]);
        AddModelTextures("bolt.model", bolt, 9, [0x1A8, 0x260]);
        AddTexture("particle7", Half(particle, 6), Half(particle, 10), Math.Min(particle[4], particle[8]),
            Math.Min(particle[5], particle[9]), Math.Max(particle[4], particle[8]), Math.Max(particle[5], particle[9]));
        textureRefs.Add(new("particle7", 4, "particle7", particle.AsSpan(4, 8)));
        if (textures.Count != 5 || textures.Sum(t => t.RowBytes * t.Rows) != 2056 || textureRefs.Count != 61)
            throw new InvalidDataException("Complete Wizard source texture owner accounting changed.");

        var donorSound = source.DonorSound.Sound; var townSound = source.TownSound.Sound;
        if (source.DonorSound.SoundOffset != 0x1735B4 || source.TownSound.SoundOffset != 0x172610 ||
            townSound.Descriptors.Count != 76 || townSound.Map[0x1F] != 0 || townSound.Map[0x26] != 22 ||
            !wizard.AsSpan(4, 3).SequenceEqual(new byte[] {49,50,51}) || donorSound.Map[0x1F] != 40 || donorSound.Map[0x26] != 22)
            throw new InvalidDataException("Fixed local/global sound namespaces changed.");
        AddSound("local0", NativeWizardFamilySoundNamespace.LocalModelSlot, 0, 49, null, true);
        AddSound("local1", NativeWizardFamilySoundNamespace.LocalModelSlot, 1, 50, 66, true);
        AddSound("local2", NativeWizardFamilySoundNamespace.LocalModelSlot, 2, 51, 67, true);
        AddSound("global1F", NativeWizardFamilySoundNamespace.GlobalSelector, 0x1F, 40, null, true);
        AddSound("global26", NativeWizardFamilySoundNamespace.GlobalSelector, 0x26, 22, 22, false);
        if (output.Count != 0x13274 || owners.Count != 26)
            throw new InvalidDataException("Detached fixed-family byte budget/owner count changed.");
        _block = output.ToArray();
        foreach (var owner in owners)
        {
            int cursor = 0;
            foreach (var read in reads.Where(r => r.OwnerId == owner.Id))
            {
                int at = checked((int)(read.SourceWadOffset - NativeWizardFamilyAssetSource.DonorWadOffset));
                if (read.OwnerOffset != cursor || read.ByteLength <= 0 || cursor + (long)read.ByteLength > owner.ByteLength ||
                    !_block.AsSpan(owner.BlockOffset + cursor, read.ByteLength).SequenceEqual(donor.AsSpan(at, read.ByteLength)))
                    throw new InvalidDataException("Detached owner lost its exact contiguous/strided source provenance.");
                cursor += read.ByteLength;
            }
            if (cursor != owner.ByteLength) throw new InvalidDataException("Detached asset has an unowned source complement.");
        }
        Owners = owners.AsReadOnly(); SourceSpans = reads.AsReadOnly(); Textures = textures.AsReadOnly();
        TextureReferences = textureRefs.AsReadOnly(); SoundRequirements = sounds.AsReadOnly(); SoundReferences = soundRefs.AsReadOnly();
        AnimationReferences = Array.AsReadOnly(source.Animation.References.ToArray());
        SceneReferences = Array.AsReadOnly(new[]
        {
            new NativeWizardFamilySceneReference("wizard.row", 0, "wizard.private", 0, 0xC350),
            new NativeWizardFamilySceneReference("wizard.private", 0, "wizard.private", 0x28, 0xC378)
        });
        BlockSha256 = Hash(_block);
        PlanSha256 = Hash(JsonSerializer.SerializeToUtf8Bytes(new
        {
            Profile = "native-wizard33-T6-source-form-assets-v1", SourceImageSha256, SourceVariantId,
            source.EngineContractSha256, BlockSha256, Owners, SourceSpans, Textures,
            TextureFields = TextureReferences.Select(r => new {r.OwnerId, r.FieldOffset, r.TextureId, r.ByteLength, Sha256=Hash(r.CopySourceBytes())}),
            SoundRequirements, SoundReferences, SceneReferences, AnimationReferences, AnimationTextureHeaderSha256, SharedGeometrySha256
        }));
        return;

        byte[] Package(ushort cls, int offset, int length)
        {
            var root = source.Models.Registrations.Single(r => r.ActorId == cls);
            long end = root.Slot + 1 < source.Models.Registrations.Count ? source.Models.Registrations[root.Slot + 1].RootDataOffset : source.Models.ModelEnd;
            if (root.Slot <= 0 || root.RootDataOffset != offset || end != offset + length)
                throw new InvalidDataException("Detached package must retain its complete actual registered source interval.");
            return donor.AsSpan(offset, length).ToArray();
        }
        void Add(string id, ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length <= 0 || (bytes.Length & 3) != 0 || bytes.Length > 0x20000 - output.Count || owners.Any(o => o.Id == id))
                throw new InvalidDataException("Detached asset exceeded its bounded unique aligned owner budget.");
            owners.Add(new(id, output.Count, bytes.Length, Hash(bytes))); output.AddRange(bytes.ToArray());
        }
        void Span(string id, int ownerAt, int sourceAt, int length) => reads.Add(new(id, ownerAt,
            NativeWizardFamilyAssetSource.DonorWadOffset + sourceAt, length, Hash(donor.AsSpan(sourceAt, length))));
        void AddDirect(string id, int sourceAt, byte[] bytes)
        {
            if (!bytes.AsSpan().SequenceEqual(donor.AsSpan(sourceAt, bytes.Length))) throw new InvalidDataException("Detached source afterimage substitution.");
            Add(id, bytes); Span(id, 0, sourceAt, bytes.Length);
        }
        void AddModelTextures(string id, byte[] package, int count, int[] faces)
        {
            var layout = NativeMobyModelTextureLayout.Decode(package);
            if (layout.References.Count != count || !layout.FaceTableOffsets.SequenceEqual(faces))
                throw new InvalidDataException("Fixed family native renderer field schema changed.");
            foreach (var group in layout.References.GroupBy(r => (r.Palette, r.Page)).OrderBy(g => g.Key.Palette).ThenBy(g => g.Key.Page))
            {
                byte[][] fields = group.Select(r => r.CopyBytes()).ToArray();
                int[] offsets = [0,4,8,10];
                string tile = $"{id}:{group.Key.Palette:X4}:{group.Key.Page:X4}";
                AddTexture(tile, group.Key.Palette, group.Key.Page, fields.Min(b => offsets.Min(o => b[o])),
                    fields.Min(b => offsets.Min(o => b[o+1])), fields.Max(b => offsets.Max(o => b[o])), fields.Max(b => offsets.Max(o => b[o+1])));
                foreach (var r in group) textureRefs.Add(new(id, r.PackageOffset, tile, r.CopyBytes()));
            }
        }
        void AddTexture(string id, ushort clut, ushort page, int u0, int v0, int u1, int v1)
        {
            var f = NativeWizardFamilyAssetSource.ValidateFootprint(clut, page, u0, v0, u1, v1);
            byte[] pixels = new byte[f.RowBytes * f.Rows];
            for (int r = 0; r < f.Rows; r++)
            {
                int at = 0x800 + f.PixelOffset + r * 1024;
                donor.AsSpan(at, f.RowBytes).CopyTo(pixels.AsSpan(r * f.RowBytes));
                Span(id + ".pixels", r * f.RowBytes, at, f.RowBytes);
            }
            Add(id + ".pixels", pixels);
            AddDirect(id + ".clut", 0x800 + f.PaletteOffset, donor.AsSpan(0x800 + f.PaletteOffset, 32).ToArray());
            textures.Add(new(id, id + ".pixels", id + ".clut", f.PixelOffset, f.PaletteOffset, f.RowBytes, f.Rows, clut, page));
        }
        void AddSound(string id, NativeWizardFamilySoundNamespace ns, int selector, int index, int? expectedEquivalent, bool import)
        {
            var descriptor = donorSound.Descriptors[index];
            var equivalents = townSound.Descriptors.Where(d => NativeGlobalSoundMergeLayout.Equivalent(d, descriptor)).Select(d => d.Index).ToArray();
            if (!equivalents.SequenceEqual(expectedEquivalent is int e ? [e] : Array.Empty<int>()) ||
                descriptor.IsSupportedOneShot != import || !descriptor.Sample.IsSimpleOneShot || descriptor.Sample.ByteLength > 0x10000 ||
                (!import && (id != "global26" || Word(descriptor.CopyDescriptor(), 12) != 0x20028)))
                throw new InvalidDataException("Sound source/equivalence/import policy changed; resident control words cannot be normalized.");
            AddDirect(id + ".descriptor", source.DonorSound.SoundOffset + 0x108 + index * 20, descriptor.CopyDescriptor());
            AddDirect(id + ".sample", checked(source.DonorSound.BankOffset + (int)descriptor.SampleOffset), descriptor.Sample.CopyBytes());
            sounds.Add(new(id, ns, selector, index, id + ".descriptor", id + ".sample", expectedEquivalent, import));
            soundRefs.Add(new(id + ".descriptor", 0, 4, id + ".sample", descriptor.SampleOffset));
            if (ns == NativeWizardFamilySoundNamespace.LocalModelSlot)
                soundRefs.Add(new("wizard.model", 4 + selector, 1, id + ".descriptor", (uint)index));
        }
        void RequireScenePointers()
        {
            ReadOnlySpan<byte> scene = donor.AsSpan(0x1CE800, 0x11800);
            if (Word(scene, 0x6388) != 0xC350 || Word(scene, 0xC350) != 0xC378 || Word(scene, 0xFC3C) != 191)
                throw new InvalidDataException("Fixed donor scene pointer schema changed.");
            int[] fixups = Enumerable.Range(0,191).Select(i => checked((int)Word(donor,0x1CE800+0xFC40+4*i))).ToArray();
            if (!fixups.Where(f => f >= 0x6388 && f < 0x63E0).SequenceEqual(new[] {0x6388}) ||
                !fixups.Where(f => f >= 0xC350 && f < 0xC3A0).SequenceEqual(new[] {0xC350}))
                throw new InvalidDataException("An unclosed row/private native pointer field appeared.");
        }
    }

    public string SourceImageSha256 => NativeMobySourceInventory.OriginalImageSha256;
    public int DonorLevelId => 33;
    public int DonorTrueIndex => 6;
    public string SourceVariantId { get; }
    public string BlockSha256 { get; }
    public string PlanSha256 { get; }
    public string AnimationTextureHeaderSha256 { get; }
    public string SharedGeometrySha256 { get; }
    public IReadOnlyList<NativeWizardFamilyAssetOwner> Owners { get; }
    public IReadOnlyList<NativeWizardFamilySourceSpan> SourceSpans { get; }
    public IReadOnlyList<NativeWizardFamilyTexture> Textures { get; }
    public IReadOnlyList<NativeWizardFamilyTextureReference> TextureReferences { get; }
    public IReadOnlyList<NativeWizardFamilySoundRequirement> SoundRequirements { get; }
    public IReadOnlyList<NativeWizardFamilySoundReference> SoundReferences { get; }
    public IReadOnlyList<NativeWizardFamilySceneReference> SceneReferences { get; }
    public IReadOnlyList<NativePlayerAnimationReference> AnimationReferences { get; }
    public byte[] CopySourceAssetBlock() => _block.ToArray();
    public byte[] CopyOwner(string id)
    {
        var owner = Owners.SingleOrDefault(o => o.Id == id) ?? throw new InvalidDataException("Unknown detached Wizard source owner.");
        return _block.AsSpan(owner.BlockOffset, owner.ByteLength).ToArray();
    }
    public void RequireExactSourceAssetBlock(ReadOnlySpan<byte> bytes)
    { if (!bytes.SequenceEqual(_block)) throw new InvalidDataException("Detached Wizard source-form owner/complement changed."); }
    public static async Task<NativeWizardFamilyAssetPlan> CaptureAsync(string originalBin, CancellationToken token = default)
    {
        var source = await NativeWizardFamilyAssetSource.CaptureAsync(originalBin, token);
        token.ThrowIfCancellationRequested();
        var result = new NativeWizardFamilyAssetPlan(source);
        token.ThrowIfCancellationRequested();
        return result;
    }
    private static uint Word(ReadOnlySpan<byte> b,int at) => NativeWizardFamilyAssetSource.Word(b,at);
    private static ushort Half(ReadOnlySpan<byte> b,int at) => BinaryPrimitives.ReadUInt16LittleEndian(b[at..]);
    private static string Hash(ReadOnlySpan<byte> b) => NativeWizardFamilyAssetSource.Hash(b);
}
