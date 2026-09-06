using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Exporting;

public sealed record NativeComposedModelResource(ushort ActorClassId, int RegistrationSlot, int FinalDataOffset,
    int ByteLength, string SourcePackageSha256, string FinalPackageSha256);

/// <summary>
/// One source-issued resource composition, not an actor behavior linker or publisher.
/// It never layers whole retail afterimages. Only declared original-address stages
/// are applied; other authored changes require their own typed source-bound stage.
/// </summary>
public sealed class NativeDarkHollowWizardResourcePlan
{
    private readonly byte[] _final, _particleDescriptor;
    private readonly NativeLevelResourceCompositionLayout _layout;
    private readonly NativeComposedMobyModelRegistrationPlan _registration;
    private readonly NativeMobyDependencyClosurePlan _graph;
    private NativeDarkHollowWizardResourcePlan(NativeMobySourceLevel targetLevel, byte[] target, byte[] donor,
        byte[] executable, byte[] peteHeader, byte[] peteCopy, NativeMobyModelRegistrationSource targetModels,
        NativeMobyModelRegistrationSource donorModels, NativeMobyModelLoaderCertificate loader,
        NativePlayerAnimationMergePlan player, NativeModelSoundMergePlan sound, NativeDarkHollowWizardTexturePlan texture,
        NativeDarkHollowWizardScenePlan? scene, Func<NativeByteExtent, byte[]> read, bool reverse,
        NativeDarkHollowWizardParticlePlan? particle)
    {
        SourceDataSha256 = targetLevel.SourceDataSha256; DonorDataSha256 = Hash(donor); IncludesSceneAppend = scene != null;
        ParticleDescriptorInstalled = particle != null;
        particle?.RequireOriginalAndTexture(target, texture);
        var donorRoot = donorModels.Registrations.Single(r => r.ActorId == 0x26);
        long boltEnd = donorRoot.Slot + 1 < donorModels.Registrations.Count ? donorModels.Registrations[donorRoot.Slot + 1].RootDataOffset : donorModels.ModelEnd;
        var boltSource = new NativeByteExtent(donorModels.Original, donorRoot.RootDataOffset, boltEnd - donorRoot.RootDataOffset);
        var wizardSlot = sound.Slots[0];
        var wizardSource = new NativeByteExtent(donorModels.Original, wizardSlot.PackageDataOffset, wizardSlot.PackageByteLength);
        byte[] bolt = texture.CopyLightningPackage(), wizard = sound.CopyTextureComposedPackage(texture);
        if (boltSource.ByteLength != 0x564 || Hash(read(boltSource)) != "9ea5c72ba675e9c6a81433f7408695399e19bc188b4b2a923069d71de601789a" ||
            wizardSource.ByteLength != 0x35B4 || Hash(read(wizardSource)) != wizardSlot.PackageSha256)
            throw new InvalidDataException("Combined generic packages do not own their complete registered source intervals.");
        byte[] expectedBolt = read(boltSource);
        foreach (var patch in texture.Patches.Where(p => p.Owner == "lightning"))
        {
            if (patch.Offset < 0 || patch.Offset + (long)patch.ByteLength > expectedBolt.Length ||
                !expectedBolt.AsSpan(patch.Offset, patch.ByteLength).SequenceEqual(patch.CopyBefore()))
                throw new InvalidDataException("Lightning texture patch changed its source package preimage.");
            patch.CopyAfter().CopyTo(expectedBolt, patch.Offset);
        }
        if (!expectedBolt.AsSpan().SequenceEqual(bolt)) throw new InvalidDataException("Lightning package contains undeclared edits.");
        var sourceSound = new NativeLevelSoundLayout(target).Sound;
        var donorSound = new NativeLevelSoundLayout(donor).Sound;
        if (!NativeGlobalSoundMergeLayout.Equivalent(sourceSound.Descriptors[sourceSound.Map[0x26]], donorSound.Descriptors[donorSound.Map[0x26]]))
            throw new InvalidDataException("Native death selector26 cannot be claimed as equivalent resident sound.");
        List<NativeSkyRelocationPayload> payloads = sound.CopyInsertionPayloads().Select(p => p with { PatchIndex = 20 + p.PatchIndex }).ToList();
        NativeSkyRelocationPayload animation = player.CopyInsertionPayload() ?? throw new InvalidDataException("Combined Wizard profile requires actual animation8 insertion.");
        payloads.Add(animation with { PatchIndex = 10 });
        if (particle != null) payloads.Add(particle.CopyOriginalAddressPayload(50));
        byte[] appended = bolt.Concat(wizard).ToArray();
        payloads.Add(new(30, 37, targetLevel.DataWadOffset + targetModels.ModelEnd, targetLevel.DataWadEntry,
            targetModels.ModelLength, 0, appended, 2, false));
        if (scene != null)
        {
            scene.VerifyOriginalAndFinal(target.AsSpan(targetLevel.SceneDataOffset, targetLevel.SceneByteLength), scene.CopyScene());
            payloads.Add(scene.CopyOriginalAddressPayload(40));
        }
        List<NativeResourceOriginalBytePatch> patches = [];
        if (particle != null) patches.AddRange(particle.CopyOriginalFieldPatches());
        foreach (var field in sound.CopyNonHeaderMutations())
            patches.Add(new("sound." + field.Id, field.DataOffset, Scalar(field.Before, field.ByteLength), Scalar(field.After, field.ByteLength)));
        foreach (var patch in texture.Patches.Where(p => p.Owner == "target-vram"))
            patches.Add(new($"texture.{patch.Offset:X}", 0x800 + patch.Offset, patch.CopyBefore(), patch.CopyAfter()));
        if (reverse) { payloads.Reverse(); patches.Reverse(); }
        _layout = new(targetModels, targetLevel.DataWadEntry, targetLevel.DataWadOffset, payloads, patches, read);
        var modelAppend = _layout.PayloadExtent(30);
        List<NativeMobyDependencyNode> nodes = [];
        AddPackage("resource.package.0026", boltSource, bolt, modelAppend.Offset);
        AddPackage("resource.package.011B", wizardSource, wizard, modelAppend.Offset + bolt.Length);
        var packagesGraph = NativeMobyDependencyClosureCompiler.Compile(nodes, nodes.Select(n => n.Id), read);
        var bindings = new[]
        {
            NativeMobyModelPackageBinding.Capture(donorModels, 0x26, packagesGraph, "resource.package.0026", "package", "source-relative-model-with-declared-texture-edits-v1", read),
            NativeMobyModelPackageBinding.Capture(donorModels, 0x11B, packagesGraph, "resource.package.011B", "package", "source-relative-model-with-declared-texture-and-local-sound-edits-v1", read)
        };
        _registration = NativeComposedMobyModelRegistrationPlan.Build(targetModels, loader, _layout, 30,
            reverse ? bindings.Reverse() : bindings, read);
        nodes.Add(_registration.HeaderNode());
        _graph = NativeMobyDependencyClosureCompiler.Compile(nodes, nodes.Select(n => n.Id), read);
        _final = _layout.CopyFinalBytes(); _graph.CopyPayload("resource.header").CopyTo(_final, 0);
        _registration.RequireFinal(_final, _graph);
        _ = new NativeLevelDataGrowthLayout(_final.AsSpan(0, 0x800), _final.Length,
            [new(0, 37, targetLevel.DataWadOffset + Integer(_final, 0x10) + Integer(_final, 0x14), targetLevel.DataWadEntry,
                Integer(_final, 0x14), 0, [], 2, false)]);
        byte[] geometry = peteCopy.AsSpan(0xAA8, 0x32374).ToArray();
        byte[] playerOnly = player.CopyFinalLevelData();
        var actualPlayer = NativePlayerAnimationStreamCodec.Decode(_final.AsSpan(Integer(_final, 0x50), Integer(_final, 0x54) - Integer(_final, 0x50)), geometry);
        var expectedPlayer = NativePlayerAnimationStreamCodec.Decode(playerOnly.AsSpan(Integer(playerOnly, 0x50), Integer(playerOnly, 0x54) - Integer(playerOnly, 0x50)), geometry);
        if (actualPlayer.Sha256 != expectedPlayer.Sha256) throw new InvalidDataException("Combined player stream differs from its source-issued insertion.");
        var actualSound = new NativeLevelSoundLayout(_final).Sound;
        var expectedSound = new NativeLevelSoundLayout(sound.CopyFinalLevelData()).Sound;
        if (actualSound.ComponentSha256 != expectedSound.ComponentSha256 || actualSound.BankSha256 != expectedSound.BankSha256 ||
            !_final.AsSpan(0x800, 0x80000).SequenceEqual(texture.CopyTexturePatchedTargetLevel().AsSpan(0x800, 0x80000)))
            throw new InvalidDataException("Combined sound/SPU/VRAM resources differ from their independent producer readbacks.");
        if (particle != null)
        {
            int particleAt = checked((int)_layout.MapOriginalRange(particle.OriginalComponentDataOffset, 4).Offset);
            byte[] expected = particle.CopyFinalComponent();
            if (!_final.AsSpan(particleAt, expected.Length).SequenceEqual(expected) ||
                NativeParticleDescriptorCodec.Decode(_final.AsSpan(particleAt, expected.Length)).Count != particle.FinalCount)
                throw new InvalidDataException("Combined particle component differs from its source-owned descriptor append.");
        }
        int finalScene = Integer(_final, 0x18);
        if (Integer(_final, 0x1C) != targetLevel.SceneByteLength || !_final.AsSpan(finalScene, targetLevel.SceneByteLength)
            .SequenceEqual(scene?.CopyScene() ?? target.AsSpan(targetLevel.SceneDataOffset, targetLevel.SceneByteLength).ToArray()))
            throw new InvalidDataException("Combined scene stage changed its source-issued outer extent or bytes.");
        FinalDataSha256 = Hash(_final); OriginalDataByteLength = target.Length; FinalDataByteLength = _final.Length;
        Resources = Array.AsReadOnly(_registration.Packages.Select(p => new NativeComposedModelResource(p.ActorId, p.Slot,
            (int)p.Package.Offset, (int)p.Package.ByteLength, Hash(read(p.ActorId == 0x26 ? boltSource : wizardSource)),
            Hash(_final.AsSpan((int)p.Package.Offset, (int)p.Package.ByteLength)))).ToArray());
        _particleDescriptor = texture.CopyParticle7Descriptor();
        var originalRam = NativeLevelRuntimeMemoryReport.CaptureOriginal(targetLevel, target, executable, peteHeader, peteCopy);
        RuntimeMemory = originalRam.AnalyzeResourceComposition(this);
        if (!RuntimeMemory.KnownPostLoadExtentsDisjoint) throw new InvalidDataException("Combined known post-load owners overlap the native reserved buffers.");
        return;

        void AddPackage(string id, NativeByteExtent original, byte[] bytes, long offset)
        {
            var preimage = new NativeMobyDependencyRead("package", original, Hash(read(original)));
            var destination = new NativeByteExtent(_layout.Final, offset, bytes.Length);
            nodes.Add(new(id, NativeMobyDependencyKind.ActorPackage, NativeMobyDependencyResolution.ImportFromSource,
                NativeMobyDependencySharing.Immutable, preimage.ExpectedSha256, [], [preimage],
                c => { _ = c.Read("package"); return new(bytes, [new(id, destination)], []); }));
        }
    }
    public string SourceImageSha256 => NativeMobySourceInventory.OriginalImageSha256;
    public int TargetLevelId => 12;
    public string SourceDataSha256 { get; }
    public string DonorDataSha256 { get; }
    public string FinalDataSha256 { get; }
    public int OriginalDataByteLength { get; }
    public int FinalDataByteLength { get; }
    public int SectorRoundedGrowth => FinalDataByteLength - OriginalDataByteLength;
    public bool IncludesSceneAppend { get; }
    public IReadOnlyList<NativeComposedModelResource> Resources { get; }
    public IReadOnlyList<NativeSoundProtectedExtent> Preservations => _layout.Preservations;
    public int GenericCountBefore => _registration.GenericCountBefore;
    public int GenericCountAfter => _registration.GenericCountAfter;
    public string GraphFingerprint => _graph.Fingerprint;
    public NativeLevelRuntimeMemoryReport RuntimeMemory { get; }
    public bool CompleteNativeClosureProven => false;
    public bool BehaviorInstalled => false;
    public bool ParticleDescriptorInstalled { get; }
    public bool PlacementApproved => false;
    public bool PeakTransientFitProven => false;
    public bool RuntimeFitProven => false;
    public bool PublicationApproved => false;
    public byte[] CopyFinalLevelData() => _final.ToArray();
    public byte[] CopyPendingParticleDescriptor() => _particleDescriptor.ToArray();
    public void RequireFinal(ReadOnlySpan<byte> final) => _registration.RequireFinal(final, _graph);

    public static Task<NativeDarkHollowWizardResourcePlan> CompileAsync(string originalImagePath, NativeMobySourceInventory inventory,
        NativePlayerAnimationMergePlan player, NativeModelSoundMergePlan sound, NativeDarkHollowWizardTexturePlan texture,
        NativeDarkHollowWizardScenePlan? scene = null, CancellationToken token = default) =>
        CompileCoreAsync(originalImagePath, inventory, player, sound, texture, scene, false, token);
    public static Task<NativeDarkHollowWizardResourcePlan> CompileWithParticleAsync(string path, NativeMobySourceInventory inventory,
        NativePlayerAnimationMergePlan player, NativeModelSoundMergePlan sound, NativeDarkHollowWizardTexturePlan texture,
        NativeDarkHollowWizardParticlePlan particle, NativeDarkHollowWizardScenePlan? scene = null, CancellationToken token = default) =>
        CompileCoreAsync(path, inventory, player, sound, texture, scene, false, token,
            particle ?? throw new ArgumentNullException(nameof(particle)));
    internal static Task<NativeDarkHollowWizardResourcePlan> CompileWithParticleReversedForSmokeAsync(string path, NativeMobySourceInventory inventory,
        NativePlayerAnimationMergePlan player, NativeModelSoundMergePlan sound, NativeDarkHollowWizardTexturePlan texture,
        NativeDarkHollowWizardParticlePlan particle, NativeDarkHollowWizardScenePlan? scene = null) =>
        CompileCoreAsync(path, inventory, player, sound, texture, scene, true, default, particle);
    internal static Task<NativeDarkHollowWizardResourcePlan> CompileReversedForSmokeAsync(string path, NativeMobySourceInventory inventory,
        NativePlayerAnimationMergePlan player, NativeModelSoundMergePlan sound, NativeDarkHollowWizardTexturePlan texture,
        NativeDarkHollowWizardScenePlan? scene = null) => CompileCoreAsync(path, inventory, player, sound, texture, scene, true, default);
    private static async Task<NativeDarkHollowWizardResourcePlan> CompileCoreAsync(string path, NativeMobySourceInventory inventory,
        NativePlayerAnimationMergePlan player, NativeModelSoundMergePlan sound, NativeDarkHollowWizardTexturePlan texture,
        NativeDarkHollowWizardScenePlan? scene, bool reverse, CancellationToken token, NativeDarkHollowWizardParticlePlan? particle = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path); ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(player); ArgumentNullException.ThrowIfNull(sound); ArgumentNullException.ThrowIfNull(texture);
        var targetLevel = inventory.Levels.Single(l => l.LevelId == 12); var donorLevel = inventory.Levels.Single(l => l.LevelId == 33);
        if (inventory.SourceImageSha256 != NativeMobySourceInventory.OriginalImageSha256 ||
            player.SourceImageSha256 != inventory.SourceImageSha256 || player.TargetLevelId != 12 || player.DonorLevelId != 33 ||
            !player.RequestedAnimationIds.SequenceEqual(new[] { 8 }) || !player.AddedAnimationIds.SequenceEqual(new[] { 8 }) ||
            player.OriginalDataSha256 != targetLevel.SourceDataSha256 || sound.SourceImageSha256 != inventory.SourceImageSha256 ||
            sound.TargetLevelId != 12 || sound.DonorLevelId != 33 || sound.OriginalDataSha256 != targetLevel.SourceDataSha256 ||
            !sound.Slots.Select(s => s.SlotIndex).SequenceEqual(new[] { 0, 1, 2 }) || sound.Slots.Any(s => s.SourceDataSha256 != donorLevel.SourceDataSha256) ||
            !sound.GlobalBindings.Any(b => b.GlobalSelector == 0x1F && b.MappingChanged) ||
            texture.SourceImageSha256 != inventory.SourceImageSha256 || texture.SourceDataSha256 != targetLevel.SourceDataSha256 ||
            texture.DonorDataSha256 != donorLevel.SourceDataSha256 ||
            (particle != null && (particle.SourceImageSha256 != inventory.SourceImageSha256 || particle.SourceDataSha256 != targetLevel.SourceDataSha256 || particle.DonorDataSha256 != donorLevel.SourceDataSha256)) ||
            (scene != null && (scene.SourceImageSha256 != inventory.SourceImageSha256 || scene.SourceDataSha256 != targetLevel.SourceDataSha256 || scene.DonorDataSha256 != donorLevel.SourceDataSha256)))
            throw new InvalidDataException("Combined resource stages do not share the exact source-issued Dark/Wizard profile and requested dependencies.");
        path = Path.GetFullPath(path);
        await using FileStream image = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        await RequireOriginalAsync(image, token);
        var disc = DiscImage.DetectLayout(path);
        var wad = DiscImage.FindRootFileRecord(image, disc, n => n.Equals("WAD.WAD", StringComparison.OrdinalIgnoreCase));
        var exe = DiscImage.FindRootFileRecord(image, disc, n => n.Equals("SCUS_942.28", StringComparison.OrdinalIgnoreCase));
        if (disc.SectorSize != 2352 || disc.UserOffset != 24 || wad.Lba != 37 || wad.Size != 0x6927000 || exe.Lba != 53875 || exe.Size != 0x66000)
            throw new InvalidDataException("Combined resource source ISO geometry/owners changed.");
        byte[] target = DiscImage.ReadFileBytes(image, disc, 37, targetLevel.DataWadOffset, targetLevel.SourceDataByteLength);
        byte[] donor = DiscImage.ReadFileBytes(image, disc, 37, donorLevel.DataWadOffset, donorLevel.SourceDataByteLength);
        byte[] executable = DiscImage.ReadFileBytes(image, disc, exe.Lba, 0, exe.Size);
        byte[] peteHeader = DiscImage.ReadFileBytes(image, disc, 37, 0x7B8800, 0x800), peteCopy = DiscImage.ReadFileBytes(image, disc, 37, 0x7B9000, 0x39C48);
        var loader = NativeMobyModelLoaderCertificate.CaptureRetail(executable, peteHeader, peteCopy);
        var targetDomain = new NativeAddressSpace(NativeAddressKind.LevelData, "dark-source", NativeAddressRevision.Original, target.Length);
        var donorDomain = new NativeAddressSpace(NativeAddressKind.LevelData, "wizard-source", NativeAddressRevision.Original, donor.Length);
        byte[] Read(NativeByteExtent extent)
        {
            byte[] bytes = extent.Space == targetDomain ? target : extent.Space == donorDomain ? donor
                : throw new InvalidDataException("Combined resource source reader rejects a detached address domain.");
            return bytes.AsSpan((int)extent.Offset, (int)extent.ByteLength).ToArray();
        }
        var targetModels = NativeMobyModelRegistrationSource.CaptureInventory(targetLevel, targetDomain, Read);
        var donorModels = NativeMobyModelRegistrationSource.CaptureInventory(donorLevel, donorDomain, Read);
        var result = new NativeDarkHollowWizardResourcePlan(targetLevel, target, donor, executable, peteHeader, peteCopy,
            targetModels, donorModels, loader, player, sound, texture, scene, Read, reverse, particle);
        await RequireOriginalAsync(image, token);
        return result;
    }
    private static byte[] Scalar(uint value, int length)
    {
        if (length != 1 && length != 4) throw new InvalidDataException("Combined sound scalar width is unsupported.");
        byte[] bytes = new byte[length]; if (length == 1) bytes[0] = checked((byte)value); else BinaryPrimitives.WriteUInt32LittleEndian(bytes, value); return bytes;
    }
    private static async Task RequireOriginalAsync(FileStream image, CancellationToken token)
    {
        if (image.Length != NativeMobySourceInventory.OriginalImageByteLength) throw new InvalidDataException("Combined resources require the original image length.");
        image.Position = 0;
        if (Convert.ToHexStringLower(await SHA256.HashDataAsync(image, token)) != NativeMobySourceInventory.OriginalImageSha256 ||
            image.Length != NativeMobySourceInventory.OriginalImageByteLength || image.Position != image.Length)
            throw new InvalidDataException("Combined resources require the unchanged original image identity.");
    }
    private static int Integer(ReadOnlySpan<byte> bytes, int offset) => checked((int)BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]));
    private static string Hash(ReadOnlySpan<byte> bytes) => NativeMobyModelLoaderCertificate.Hash(bytes);
}
