using System.Text;
using Spyro.Editor.Core.Analysis;

namespace Spyro.Editor.Core.Exporting;

/// <summary>A source-issued model-local byte selector, not a global sound-map key.</summary>
public sealed class NativeModelSoundSlotSource
{
    internal NativeModelSoundSlotSource(NativeGlobalSoundSourceLevel level, ushort actor, int packageOffset,
        int packageLength, string packageHash, int slot, NativeSoundDescriptor descriptor)
    {
        SourceLevelId = level.LevelId; SourceDataSha256 = level.DataSha256; ActorClassId = actor;
        PackageDataOffset = packageOffset; PackageWadOffset = level.DataWadOffset + packageOffset;
        PackageByteLength = packageLength; PackageSha256 = packageHash; SlotIndex = slot;
        OriginalDescriptorIndex = descriptor.Index; DescriptorSha256 = descriptor.Sha256;
        SampleSha256 = descriptor.Sample.Sha256; SampleWadOffset = level.BankWadOffset + descriptor.SampleOffset;
        SampleByteLength = descriptor.Sample.ByteLength;
    }
    public string SourceImageSha256 => NativeGlobalSoundSource.OriginalImageSha256;
    public int SourceLevelId { get; }
    public string SourceDataSha256 { get; }
    public ushort ActorClassId { get; }
    public int PackageDataOffset { get; }
    public long PackageWadOffset { get; }
    public int PackageByteLength { get; }
    public string PackageSha256 { get; }
    public int ProfilePayloadByteLength => 0x35AC;
    public string ProfilePayloadSha256 => "24df9ae279f32ba10a8e3bd25880d6989991b9225acf45755e6aaf985d66f529";
    public int SlotIndex { get; }
    public int PackageFieldOffset => 4 + SlotIndex;
    public int OriginalDescriptorIndex { get; }
    public string DescriptorSha256 { get; }
    public string SampleSha256 { get; }
    public long SampleWadOffset { get; }
    public int SampleByteLength { get; }
}

public sealed record NativeModelSoundSlotBinding(int SlotIndex, int PackageFieldOffset, byte Before, byte After);

/// <summary>
/// Resource allocation plus detached model-local selector edits. Final level bytes are
/// retail-derived and contain sound changes only; they do not install the donor model.
/// A texture-composed package remains a detached package, not a registered native actor.
/// </summary>
public sealed class NativeModelSoundMergePlan
{
    private readonly NativeGlobalSoundMergeLayout _layout;
    private readonly NativeModelSoundPackageSource _package;
    internal NativeModelSoundMergePlan(NativeGlobalSoundSource source, NativeModelSoundPackageSource package,
        int target, NativeGlobalSoundMergeLayout layout)
    {
        _layout = layout; _package = package; SourceImageSha256 = source.SourceImageSha256;
        DonorLevelId = package.Slots[0].SourceLevelId; TargetLevelId = target;
        Bindings = Array.AsReadOnly(package.Slots.Select(slot =>
        {
            int index = layout.DescriptorAllocations.Single(a => a.SourceDescriptorIndex == slot.OriginalDescriptorIndex).TargetDescriptorIndex;
            // FF is unbound in this exact native package profile. This is not a claim
            // that the global descriptor table has a hardware capacity of only 255.
            if (index >= 255) throw new InvalidDataException("This model-slot profile cannot emit its FF unbound representation as a resource selector.");
            return new NativeModelSoundSlotBinding(slot.SlotIndex, slot.PackageFieldOffset, (byte)slot.OriginalDescriptorIndex, (byte)index);
        }).ToArray());
        PlanSha256 = NativeSoundBankCodec.Hash(Encoding.UTF8.GetBytes($"model-sound-v1|{SourceImageSha256}|{DonorLevelId}|{target}|{package.PackageSha256}|{layout.OriginalSha256}|{layout.FinalSha256}|" +
            string.Join(';', Bindings.Select(b => $"{b.PackageFieldOffset}:{b.Before}:{b.After}")) + "|" +
            string.Join(';', layout.Requests.Select(r => $"{r.GlobalSelector}:{(int)r.MappingPolicy}"))));
    }
    public string SourceImageSha256 { get; }
    public int DonorLevelId { get; }
    public int TargetLevelId { get; }
    public string PlanSha256 { get; }
    public string OriginalDataSha256 => _layout.OriginalSha256;
    public string FinalDataSha256 => _layout.FinalSha256;
    public int ExactAppendedSampleBytes => _layout.AppendedSampleBytes;
    public int AppendedDescriptorCount => _layout.AppendedDescriptorCount;
    public int SectorRoundedGrowth => _layout.FinalLength - _layout.OriginalLength;
    public int FinalSpuBankByteLength => _layout.FinalBankLength;
    public IReadOnlyList<NativeModelSoundSlotSource> Slots => _package.Slots;
    public IReadOnlyList<NativeModelSoundSlotBinding> Bindings { get; }
    public IReadOnlyList<NativeGlobalSoundBinding> GlobalBindings => _layout.Bindings;
    public IReadOnlyList<NativeGlobalSoundRequest> GlobalRequests => _layout.Requests;
    public IReadOnlyList<NativeSoundSamplePlacement> AppendedSamples => _layout.Samples;
    public IReadOnlyList<NativeSoundProtectedExtent> Preservations => _layout.Preservations;
    public IReadOnlyList<NativeSoundFieldChange> FieldChanges => _layout.FieldChanges;
    public bool SpuUploadDomainFitProven => FinalSpuBankByteLength <= NativeSoundBankCodec.SpuUploadByteLength;
    public bool CompleteConsumerOwnershipProven => false;
    public bool RuntimeFitProven => false;
    public bool RuntimePlaybackProven => false;
    public bool PublicationApproved => false;
    public byte[] CopyFinalLevelData() => _layout.CopyFinalBytes();
    public byte[] CopyRetargetedSourcePackage() => NativeModelSoundPackageSource.ApplyBindings(_package.CopyPackage(), Bindings);
    public byte[] CopyTextureComposedPackage(NativeDarkHollowWizardTexturePlan texture)
    {
        ArgumentNullException.ThrowIfNull(texture);
        if (TargetLevelId != 12 || texture.SourceDataSha256 != OriginalDataSha256 ||
            texture.DonorDataSha256 != Slots[0].SourceDataSha256)
            throw new InvalidDataException("Texture and sound packages do not share the exact donor/destination source owners.");
        byte[] texturePayload = texture.CopyWizardPackage(), expected = _package.CopyPackage();
        if (texturePayload.Length != Slots[0].ProfilePayloadByteLength)
            throw new InvalidDataException("Texture package payload length changed from its source-bound model schema.");
        // The texture producer emits the logical payload. Retain the separately
        // bound eight-byte registered-owner padding rather than silently dropping it.
        byte[] current = _package.CopyPackage(); texturePayload.CopyTo(current, 0);
        foreach (var patch in texture.Patches.Where(p => p.Owner == "wizard"))
        {
            if (patch.Offset < 0 || patch.ByteLength <= 0 || patch.Offset + (long)patch.ByteLength > expected.Length ||
                Bindings.Any(b => b.PackageFieldOffset >= patch.Offset && b.PackageFieldOffset < patch.Offset + patch.ByteLength) ||
                !expected.AsSpan(patch.Offset, patch.ByteLength).SequenceEqual(patch.CopyBefore()))
                throw new InvalidDataException("Texture patch overlaps a sound slot or changes its exact package preimage.");
            patch.CopyAfter().CopyTo(expected, patch.Offset);
        }
        if (!current.AsSpan().SequenceEqual(expected)) throw new InvalidDataException("Texture package contains undeclared upstream edits.");
        return NativeModelSoundPackageSource.ApplyBindings(current, Bindings);
    }
    internal IReadOnlyList<NativeSkyRelocationPayload> CopyInsertionPayloads() => _layout.CopyInsertionPayloads();
    internal IReadOnlyList<NativeSoundOriginalFieldMutation> CopyNonHeaderMutations() => _layout.CopyNonHeaderMutations();
}

internal sealed class NativeModelSoundPackageSource
{
    private readonly byte[] _package;
    private NativeModelSoundPackageSource(byte[] package, IEnumerable<NativeModelSoundSlotSource> slots)
    { _package = package.ToArray(); PackageSha256 = NativeSoundBankCodec.Hash(package); Slots = Array.AsReadOnly(slots.ToArray()); }
    internal string PackageSha256 { get; }
    internal IReadOnlyList<NativeModelSoundSlotSource> Slots { get; }
    internal byte[] CopyPackage() => _package.ToArray();
    internal static int[] SnapshotSlots(IReadOnlyList<int> slots)
    {
        int[] copy = NativeGlobalSoundMergeLayout.BoundedSnapshot(slots, 1, 16, "model sound slot");
        if (copy.Any(s => s is < 0 or >= 16) || copy.Distinct().Count() != copy.Length)
            throw new InvalidDataException("Model sound requests require distinct local slots 0..15.");
        return copy.Order().ToArray();
    }
    internal static NativeModelSoundPackageSource Capture(NativeGlobalSoundSourceLevel sound,
        NativeMobyModelRegistrationSource models, NativeAddressSpace original, ushort actor,
        IReadOnlyList<int> slots, Func<NativeByteExtent, byte[]> read)
    {
        int[] selected = SnapshotSlots(slots);
        if (models.Original != original || sound.DataSha256 != models.SourceSha256 || sound.DataByteLength != original.ByteLength)
            throw new InvalidDataException("Model and sound source owners are detached or differ in complete level bytes.");
        models.RequireSource(read);
        if (sound.LevelId != 33 || actor != 0x11B)
            throw new InvalidDataException("Model sound slot schema is currently certified only for the source-native Wizard33 class11B package.");
        var root = models.Registrations.SingleOrDefault(r => r.ActorId == actor && r.Slot > 0)
            ?? throw new InvalidDataException("Requested class has no source-owned generic model registration.");
        long end = root.Slot + 1 < models.Registrations.Count ? models.Registrations[root.Slot + 1].RootDataOffset : models.ModelEnd;
        var extent = new NativeByteExtent(original, root.RootDataOffset, end - root.RootDataOffset);
        byte[] package = read(extent);
        const string hash = "070a57069c8747ada4caacc0394b93e9a970e5ba7ff21159584abdc7a0fd4faf";
        if (package == null || package.Length != extent.ByteLength || package.Length != 0x35B4 || NativeSoundBankCodec.Hash(package) != hash ||
            NativeSoundBankCodec.Hash(package.AsSpan(0, 0x35AC)) != "24df9ae279f32ba10a8e3bd25880d6989991b9225acf45755e6aaf985d66f529" ||
            package.AsSpan(0x35AC).ContainsAnyExcept((byte)0))
            throw new InvalidDataException("Complete source model package/header differs from the exact animated sound-slot profile.");
        List<NativeModelSoundSlotSource> issued = [];
        foreach (int slot in selected)
        {
            int index = package[4 + slot];
            if (index == 255 || index >= sound.Sound.Descriptors.Count)
                throw new InvalidDataException("Requested native model slot is FF-unbound or outside its donor descriptor owner.");
            issued.Add(new(sound, actor, checked((int)extent.Offset), package.Length, hash, slot, sound.Sound.Descriptors[index]));
        }
        return new(package, issued);
    }
    internal static byte[] ApplyBindings(ReadOnlySpan<byte> current, IReadOnlyList<NativeModelSoundSlotBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        int length = current.Length;
        if (bindings.Count is 0 or > 16 || bindings.Any(b => b == null || b.SlotIndex is < 0 or >= 16 ||
            b.PackageFieldOffset != 4 + b.SlotIndex || b.PackageFieldOffset >= length || b.After == 255) ||
            bindings.Select(b => b.PackageFieldOffset).Distinct().Count() != bindings.Count)
            throw new InvalidDataException("Model sound binding has an unsupported or duplicate package byte owner.");
        byte[] result = current.ToArray();
        foreach (var binding in bindings)
        {
            if (result[binding.PackageFieldOffset] != binding.Before) throw new InvalidDataException("Model sound slot preimage was changed by another producer.");
            result[binding.PackageFieldOffset] = binding.After;
        }
        return result;
    }
}

public sealed partial class NativeGlobalSoundSource
{
    private static readonly IReadOnlyList<NativeGlobalSoundSourceCertificate> ModelCertificates = Array.AsReadOnly<NativeGlobalSoundSourceCertificate>(
    [
        new("model-slot-direct-descriptor-consumer", 0x28D1C, 0xA0, "af902cc21e59f2794bc459d6cfaf528f36c28b73f39fc4221c1fe6d384331e91"),
        new("signed-frame-slot-and-queued-descriptor-playback", 0x42AC0, 0x204, "2a3dcb9fa1ba59e0d2e740bcbd912b18155266fee95cf2c2e2f580cfeef72324")
    ]);
    public IReadOnlyList<NativeGlobalSoundSourceCertificate> ModelSlotNativeCertificates => ModelCertificates;

    public async Task<NativeModelSoundMergePlan> CompileModelSlotMergeAsync(string originalImagePath,
        NativeMobySourceInventory inventory, int donorLevelId, int targetLevelId, ushort actorClassId,
        IReadOnlyList<int> slots, IReadOnlyList<NativeGlobalSoundRequest>? globalRequests = null, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(originalImagePath); ArgumentNullException.ThrowIfNull(inventory);
        int[] selected = NativeModelSoundPackageSource.SnapshotSlots(slots);
        var globals = NativeGlobalSoundMergeLayout.Snapshot(globalRequests ?? [], true);
        if (globals.Any(r => r.MappingPolicy == NativeGlobalSoundMappingPolicy.ExplicitRebindUnresolvedConsumers &&
            (donorLevelId != 33 || targetLevelId != 12 || r.GlobalSelector != 0x1F)))
            throw new InvalidDataException("This model-sound adapter permits an unresolved explicit rebind only for Wizard33 electric selector1F into Dark12; other globals must require equivalence.");
        var donor = Levels.SingleOrDefault(l => l.LevelId == donorLevelId) ?? throw new InvalidDataException("Model sound donor was not captured.");
        var target = Levels.SingleOrDefault(l => l.LevelId == targetLevelId) ?? throw new InvalidDataException("Model sound target has no captured existing-level ownership.");
        var modelLevel = inventory.Levels.SingleOrDefault(l => l.LevelId == donorLevelId);
        if (inventory.SourceImageSha256 != SourceImageSha256 || modelLevel == null || modelLevel.SourceDataSha256 != donor.DataSha256 ||
            modelLevel.DataWadOffset != donor.DataWadOffset || modelLevel.SourceDataByteLength != donor.DataByteLength || modelLevel.DataWadEntry != donor.DataWadEntry)
            throw new InvalidDataException("Model inventory does not share the captured native sound source owner.");
        string path = Path.GetFullPath(originalImagePath);
        await using FileStream image = Open(path);
        await RequireOriginalAsync(image, token);
        DiscLayout disc = RequireLayout(path, image);
        byte[] executable = DiscImage.ReadFileBytes(image, disc, 53875, 0, 0x66000);
        foreach (var certificate in ModelCertificates)
            RequireHash(executable.AsSpan(certificate.ExecutableOffset, certificate.ByteLength), certificate.Sha256, certificate.Id);
        byte[] donorData = DiscImage.ReadFileBytes(image, disc, 37, donor.DataWadOffset, donor.DataByteLength);
        var original = new NativeAddressSpace(NativeAddressKind.LevelData, $"native-sound-donor-{donorLevelId}", NativeAddressRevision.Original, donorData.Length);
        byte[] Read(NativeByteExtent extent)
        {
            if (extent.Space != original) throw new InvalidDataException("Model slot reader requires the canonical source address domain.");
            return donorData.AsSpan(checked((int)extent.Offset), checked((int)extent.ByteLength)).ToArray();
        }
        var models = NativeMobyModelRegistrationSource.CaptureInventory(modelLevel, original, Read);
        var package = NativeModelSoundPackageSource.Capture(donor, models, original, actorClassId, selected, Read);
        byte[] targetData = DiscImage.ReadFileBytes(image, disc, 37, target.DataWadOffset, target.DataByteLength);
        RequireHash(targetData, target.DataSha256, "captured model-sound destination");
        var layout = new NativeGlobalSoundMergeLayout(targetData, donor.Sound, globals,
            package.Slots.Select(s => s.OriginalDescriptorIndex).Distinct().Order().ToArray(), target.DataWadEntry, target.DataWadOffset);
        var plan = new NativeModelSoundMergePlan(this, package, targetLevelId, layout);
        await RequireOriginalAsync(image, token);
        return plan;
    }
}
