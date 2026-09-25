using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Levels;

[assembly: InternalsVisibleTo("Spyro.Editor.NativeJapaneseSignFontResidentSmoke")]

namespace Spyro.Editor.Core.Exporting;

internal enum NativeJapaneseFontResidentRole { ImmutableAtlas, MutableModalBackup }
internal sealed record NativeJapaneseFontResidentOwner(NativeJapaneseFontResidentRole Role,
    NativeByteExtent FinalExtent, uint ModeledNormalRamStart, string InitialBytesSha256)
{
    internal uint ModeledNormalRamEnd => checked(ModeledNormalRamStart + (uint)FinalExtent.ByteLength);
}
internal sealed record NativeJapaneseFontResidentMemory(uint CopyBuffer, int UsedLevelDataBytes,
    int OriginalModelReadBytes, int FinalModelReadBytes, int SceneReadBytes, uint ModelStart,
    uint OriginalSceneStart, uint FinalSceneStart, uint OriginalSceneEnd, uint FinalSceneEnd,
    uint NormalReservedStart, long OriginalNormalGap, long FinalNormalGap);
internal sealed record NativeJapaneseFontResidentEvidence(uint Pc, int ByteLength, string Sha256, string Role);

/// <summary>
/// A detached, source-issued Dark Hollow level-data allocation. It appends an
/// immutable atlas and distinct mutable backup initialization image, not a model
/// or actor registration. RAM addresses project the unchanged normal loader;
/// they do not grant GPU/phase/transition/IRQ/stack or publication authority.
/// </summary>
internal sealed class NativeJapaneseSignFontResidentPlan
{
    internal const int OwnerBytes = 0xA000, GrowthBytes = 2 * OwnerBytes;
    private const int MaximumCurrentBytes = 0x800000;
    private const string OriginalDataHash = "a937aecb3626ffd2311ba136684a34c9f36877054b52557b4524fcacee0e2d97";
    private const string AtlasHash = "917a2c272859d99b573ee3f79b1a62ca847dff218802b2c65f80bb55cb888ff1";
    private readonly byte[] _original, _final, _atlas;
    private readonly NativeLevelDataGrowthLayout _growth;

    private NativeJapaneseSignFontResidentPlan(ReadOnlySpan<byte> current, ReadOnlySpan<byte> atlas,
        uint copyBuffer, NativeLevelRuntimeMemoryReport? sourceBaseline,
        string usaImage, string japanImage, bool sourceBound)
    {
        if (current.Length < 0x2800 || current.Length > MaximumCurrentBytes || current.Length % 0x800 != 0 || atlas.Length != OwnerBytes)
            throw new InvalidDataException("Font residency needs a complete bounded sector-aligned level and exact A000 atlas.");
        if (sourceBound != (sourceBaseline is not null) || sourceBaseline is not null &&
            (!sourceBaseline.NativeSourceCertified || sourceBaseline.LevelId != 12 || sourceBaseline.SourceDataSha256 != Hash(current) ||
             sourceBaseline.AnalyzedDataSha256 != sourceBaseline.SourceDataSha256 || sourceBaseline.CopyBufferAddress != copyBuffer))
            throw new InvalidDataException("Font resident source ancestry does not match its original RAM report.");
        _original = current.ToArray(); _atlas = atlas.ToArray(); SourceBound = sourceBound;
        UsaSourceImageSha256 = usaImage; JapaneseSourceImageSha256 = japanImage;
        OriginalDataSha256 = Hash(current); AtlasSha256 = Hash(atlas);
        OriginalDomain = new(NativeAddressKind.LevelData, (sourceBound ? "usa-original-darkhollow:" : "fixture:") + OriginalDataSha256,
            NativeAddressRevision.Original, current.Length);
        var registration = NativeMobyModelRegistrationSource.DecodeFixture(OriginalDomain, current);
        OriginalModelOffset = registration.ModelOffset; OriginalModelLength = registration.ModelLength;
        Registrations = registration.Registrations;
        byte[] append = new byte[GrowthBytes]; _atlas.CopyTo(append, 0);
        // Two semantic owners share one tail insertion, not two overlapping
        // zero-width writes and not a fabricated NativeModelTailBatch class.
        _growth = new(current[..0x800], current.Length,
            [new(0, 0, 0, 0, OriginalModelLength, 0, append, 2, false)]);
        if (_growth.Growth != GrowthBytes || _growth.Subfiles[2].Growth != GrowthBytes ||
            _growth.Subfiles[2].FinalOffset != OriginalModelOffset)
            throw new InvalidDataException("Outer geometry disagrees with the complete explicit font tail allocation.");
        _final = _growth.Expand(_original); FinalDataSha256 = Hash(_final);
        FinalDomain = new(NativeAddressKind.LevelData, "detached-darkhollow-font:" + FinalDataSha256,
            NativeAddressRevision.Final, _final.Length);
        var finalRegistration = NativeMobyModelRegistrationSource.DecodeFixture(
            new(NativeAddressKind.LevelData, "neutral-final-grammar:" + FinalDataSha256, NativeAddressRevision.Original, _final.Length), _final);
        if (!finalRegistration.Registrations.SequenceEqual(Registrations) ||
            !_final.AsSpan(0x50, 0x180).SequenceEqual(current.Slice(0x50, 0x180)))
            throw new InvalidDataException("A nonregistered font allocation must not add or change any class/root slot.");

        // DecodeFixture is reused only as the existing neutral complete-body
        // parser. Never relabel its final bytes as source-original/certified.
        var before = NativeLevelRuntimeMemoryReport.DecodeFixture(12, _original, copyBuffer);
        var after = NativeLevelRuntimeMemoryReport.DecodeFixture(12, _final, copyBuffer);
        if (sourceBaseline is not null && (before.ModelRamStart != sourceBaseline.ModelRamStart ||
            before.SceneReadEnd != sourceBaseline.SceneReadEnd || before.ModelBytesRead != sourceBaseline.ModelBytesRead ||
            before.NormalReservedBufferStart != sourceBaseline.NormalReservedBufferStart))
            throw new InvalidDataException("Neutral normal-load parser disagrees with its authenticated original source report.");
        if (before.UsedLevelDataBytes != after.UsedLevelDataBytes || before.ModelRamStart != after.ModelRamStart ||
            after.ModelBytesRead != before.ModelBytesRead + GrowthBytes || before.SceneBytesRead != after.SceneBytesRead ||
            after.SceneRamStart != before.SceneRamStart + GrowthBytes || after.SceneReadEnd != before.SceneReadEnd + GrowthBytes ||
            after.NormalReservedBufferGapBytes < 0)
            throw new InvalidDataException("Font resident extents exceed or disagree with the normal model/scene read bounds.");
        Memory = new(copyBuffer, before.UsedLevelDataBytes, before.ModelBytesRead, after.ModelBytesRead, before.SceneBytesRead,
            before.ModelRamStart, before.SceneRamStart, after.SceneRamStart, before.SceneReadEnd, after.SceneReadEnd,
            before.NormalReservedBufferStart, before.NormalReservedBufferGapBytes, after.NormalReservedBufferGapBytes);
        int atlasOffset = checked(OriginalModelOffset + OriginalModelLength);
        Atlas = new(NativeJapaneseFontResidentRole.ImmutableAtlas, new(FinalDomain, atlasOffset, OwnerBytes),
            before.SceneRamStart, AtlasSha256);
        Backup = new(NativeJapaneseFontResidentRole.MutableModalBackup, new(FinalDomain, atlasOffset + OwnerBytes, OwnerBytes),
            checked(before.SceneRamStart + OwnerBytes), Hash(new byte[OwnerBytes]));
        Owners = Array.AsReadOnly(new[] { Atlas, Backup });
        if (Atlas.FinalExtent.EndExclusive != Backup.FinalExtent.Offset || Backup.FinalExtent.EndExclusive != Word(_final, 0x18) ||
            Atlas.ModeledNormalRamEnd != Backup.ModeledNormalRamStart || Backup.ModeledNormalRamEnd != Memory.FinalSceneStart)
            throw new InvalidDataException("Font owners must exactly partition the new model tail in both file and normal RAM projections.");
        List<NativeRelocationRegion> regions = [new("header", new(OriginalDomain, 0, 0x800), new(FinalDomain, 0, 0x800), 0x800)];
        foreach (var subfile in _growth.Subfiles)
        {
            regions.Add(new("retained-subfile-" + subfile.Index,
                new(OriginalDomain, subfile.OriginalOffset, subfile.OriginalLength), new(FinalDomain, subfile.FinalOffset, subfile.OriginalLength), 0x800));
            if (!_final.AsSpan(subfile.FinalOffset, subfile.OriginalLength).SequenceEqual(current.Slice(subfile.OriginalOffset, subfile.OriginalLength)))
                throw new InvalidDataException("Font allocation altered an original descriptor-owned byte.");
        }
        regions.Add(new("immutable-native-font-atlas", null, Atlas.FinalExtent, 0x800));
        regions.Add(new("mutable-modal-backup-initialization", null, Backup.FinalExtent, 0x800));
        Layout = new(OriginalDomain, FinalDomain, regions);
        if (!CopyOwnerBytes(NativeJapaneseFontResidentRole.ImmutableAtlas).AsSpan().SequenceEqual(atlas) ||
            CopyOwnerBytes(NativeJapaneseFontResidentRole.MutableModalBackup).AsSpan().ContainsAnyExcept((byte)0))
            throw new InvalidDataException("Font tail readback differs from exact atlas or explicit backup initialization.");
        // Only decoded model length and later subfile offsets may change. The
        // inverse below checks every header and payload complement byte too.
        _ = RevertExact(_final);
        PlanSha256 = Hash(Encoding.UTF8.GetBytes($"jp-font-resident-v1|{sourceBound}|{usaImage}|{japanImage}|{OriginalDataSha256}|{FinalDataSha256}|{AtlasSha256}|{copyBuffer:X8}|{Memory.NormalReservedStart:X8}|atlas:A000:immutable|backup:A000:mutable-level-modal"));
    }

    internal bool SourceBound { get; }
    internal string UsaSourceImageSha256 { get; }
    internal string JapaneseSourceImageSha256 { get; }
    internal string OriginalDataSha256 { get; }
    internal string FinalDataSha256 { get; }
    internal string AtlasSha256 { get; }
    internal string PlanSha256 { get; }
    internal int OriginalModelOffset { get; }
    internal int OriginalModelLength { get; }
    internal NativeAddressSpace OriginalDomain { get; }
    internal NativeAddressSpace FinalDomain { get; }
    internal NativeRelocationMap Layout { get; }
    internal NativeJapaneseFontResidentOwner Atlas { get; }
    internal NativeJapaneseFontResidentOwner Backup { get; }
    internal IReadOnlyList<NativeJapaneseFontResidentOwner> Owners { get; }
    internal IReadOnlyList<NativeMobySourceRegistration> Registrations { get; }
    internal NativeJapaneseFontResidentMemory Memory { get; }
    internal bool NormalLoadAddressProjectionSourceBound => SourceBound;
    internal bool OriginalModelReferenceClosureProven => false;
    internal bool ModalPhaseLifetimeProven => false;
    internal bool CompleteRuntimeMemoryProven => false;
    internal bool VramInstallationApproved => false;
    internal bool PublicationApproved => false;
    internal byte[] CopyOriginalLevelData() => _original.ToArray();
    internal byte[] CopyLevelData() => _final.ToArray();
    internal byte[] CopyOwnerBytes(NativeJapaneseFontResidentRole role)
    {
        var owner = role switch { NativeJapaneseFontResidentRole.ImmutableAtlas => Atlas,
            NativeJapaneseFontResidentRole.MutableModalBackup => Backup,
            _ => throw new InvalidDataException("Unknown closed font owner role.") };
        return _final.AsSpan(checked((int)owner.FinalExtent.Offset), OwnerBytes).ToArray();
    }
    internal void RequireCurrentBody(ReadOnlySpan<byte> current)
    { if (!current.SequenceEqual(_original)) throw new InvalidDataException("Font allocation requires its exact complete original current-body preimage."); }
    internal void RequireFinal(ReadOnlySpan<byte> final)
    { if (!final.SequenceEqual(_final)) throw new InvalidDataException("Font allocation requires its complete exact detached afterimage, including mutable initialization and complements."); }
    internal byte[] RevertExact(ReadOnlySpan<byte> afterimage)
    {
        RequireFinal(afterimage);
        byte[] inverse = new byte[_original.Length]; afterimage[..0x800].CopyTo(inverse);
        foreach (var s in _growth.Subfiles)
        {
            afterimage.Slice(s.FinalOffset, s.OriginalLength).CopyTo(inverse.AsSpan(s.OriginalOffset));
            Put(inverse, s.Index * 8, (uint)s.OriginalOffset); Put(inverse, s.Index * 8 + 4, (uint)s.OriginalLength);
        }
        if (!inverse.AsSpan().SequenceEqual(_original)) throw new InvalidDataException("Font inverse failed complete original header/model/scene complement recovery.");
        return inverse;
    }
    internal void RequireNativeInstallation() => throw new InvalidDataException(
        "Detached CPU font/backup allocation is not modal installation: original model reference closure, overlay and scene composition, GPU drains/restore, every exit and peak runtime ownership remain unproved.");

    internal static NativeJapaneseSignFontResidentPlan Capture(NativeRegionalSourceSession japan, NativeRegionalSourceSession usa)
    {
        ArgumentNullException.ThrowIfNull(japan); ArgumentNullException.ThrowIfNull(usa);
        if (japan.Revision != NativeDiscRevision.JapanScps10083 || usa.Revision != NativeDiscRevision.UsaScus94228 ||
            usa.Entries[14] != new NativeRegionalWadEntry(14, 0xF01000, 0x28D800) ||
            usa.Entries[13] != new NativeRegionalWadEntry(13, 0xEF6000, 0xB000))
            throw new InvalidDataException("Font resident capture requires exact JP font and original USA Dark Hollow ownership.");
        byte[] current = usa.ReadWadEntry(14), overlay = usa.ReadWadEntry(13), exe = usa.ReadExecutableRange(0, usa.ExecutableByteLength);
        RequireHash(current, OriginalDataHash); RequireHash(overlay, "9920aa47b94005166f457b5b36df6dedd1f2400f91698bac21cdb01d8590ef32");
        foreach (var e in Evidence) RequireHash(exe.AsSpan(checked((int)(e.Pc - 0x8000F800)), e.ByteLength), e.Sha256);
        byte[] atlas = NativeJapaneseSignFontVramContract.Capture(japan, usa).CopyNativeAtlas(); RequireHash(atlas, AtlasHash);
        var definition = new LevelDefinition { Key = "darkhollow", DisplayName = "Dark Hollow", LevelId = 12, SourceWadEntry = 14,
            SourceRecordCount = 128, SourceTableRelativeOffset = "0x19CC38", SourceTableWadOffset = "0x109DC38" };
        var decoded = NativeMobySourceInventory.DecodeLevel(current, definition, 0xF01000);
        // Exact issuer checks precede this existing original-only report. The
        // complete native PETE disk snapshot authenticates bytes, not proof that
        // its overhang/prefetch equals initialized runtime RAM.
        var baseline = NativeLevelRuntimeMemoryReport.CaptureOriginal(decoded, current, exe,
            usa.ReadWadRange(0x7B8800, 0x800), usa.ReadWadRange(0x7B9000, 0x39C48));
        if (baseline.CopyBufferAddress != 0x80085594 || baseline.UsedLevelDataBytes != 0x38750 ||
            baseline.ModelBytesRead != 0x6A800 || baseline.SceneBytesRead != 0x11800 || baseline.SceneReadEnd != 0x80139CE4 ||
            baseline.NormalReservedBufferStart != 0x80187BB0)
            throw new InvalidDataException("Original Dark Hollow native resident footprint changed.");
        var result = new NativeJapaneseSignFontResidentPlan(current, atlas, baseline.CopyBufferAddress, baseline,
            usa.SourceImageSha256, japan.SourceImageSha256, true);
        japan.VerifyUnchanged(); usa.VerifyUnchanged(); return result;
    }
    internal static NativeJapaneseSignFontResidentPlan Fixture(ReadOnlySpan<byte> current, ReadOnlySpan<byte> atlas, uint copyBuffer = 0x80085594) =>
        new(current, atlas, copyBuffer, null, "fixture-usa", "fixture-japan", false);
    internal static IReadOnlyList<NativeJapaneseFontResidentEvidence> Evidence { get; } = Array.AsReadOnly(new[]
    {
        new NativeJapaneseFontResidentEvidence(0x8005A848, 0x30, "269f2fd5ac1eccfa03136565538bc84a9d22af3bb2ddef9353fd1c8ebad4a40a", "unchanged-Dark-Hollow-copy-anchor"),
        new NativeJapaneseFontResidentEvidence(0x800159FC, 0x48, "83ea25ec1c8183cfc9102d106ed5edad86807ce7c2f6e3606345bb367e7c7690", "complete-model-descriptor-CD-read"),
        new NativeJapaneseFontResidentEvidence(0x80015AD8, 0xA8, "4ef8e29a3058143b2905006924c846be8332409decdbe85f69582f4f846e7fa6", "registered-roots-and-scene-after-full-model-length"),
        new NativeJapaneseFontResidentEvidence(0x80015B80, 0x58, "edf30591c467d163e41cca4f58a8081e6ba68039326051bb1250c28d72bbc8f1", "scene-descriptor-CD-read"),
        new NativeJapaneseFontResidentEvidence(0x8005B6F8, 0xE0, "d0b4cf5a7cf9ee407ff413f7261bd5f92f4880d7cee8467852f4d757e6b84295", "normal-renderer-reserved-floor")
    });
    private static uint Word(ReadOnlySpan<byte> bytes, int at) => BinaryPrimitives.ReadUInt32LittleEndian(bytes[at..]);
    private static void Put(Span<byte> bytes, int at, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes[at..], value);
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void RequireHash(ReadOnlySpan<byte> bytes, string hash)
    { if (Hash(bytes) != hash) throw new InvalidDataException("Native font resident source identity changed."); }
}
