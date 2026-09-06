using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

internal static class NativeModelSoundCases
{
    internal static async Task RunAsync(NativeGlobalSoundSource source, NativePlayerAnimationSource player,
        string path, Action<bool, string> check, Action<Action, string> reject,
        Func<Func<Task>, string, Task> rejectAsync,
        Func<byte[], IReadOnlyList<NativeSkyRelocationPayload>, IReadOnlyList<NativeSoundOriginalFieldMutation>, byte[]> compose)
    {
        var inventory = NativeMobySourceInventory.Capture(path, LevelCatalog.Load(Environment.CurrentDirectory));
        var donor = source.Levels.Single(l => l.LevelId == 33);
        var target = source.Levels.Single(l => l.LevelId == 12);
        NativeGlobalSoundRequest electric = new(0x1F, NativeGlobalSoundMappingPolicy.ExplicitRebindUnresolvedConsumers);
        int[] selected = [2, 0, 1];
        var task = source.CompileModelSlotMergeAsync(path, inventory, 33, 12, 0x11B, selected);
        selected[0] = 15;
        var local = await task;
        check(local.Slots.Select(s => s.SlotIndex).SequenceEqual(new[] { 0, 1, 2 }), "model slot requests captured before await");
        check(local.Slots.Select(s => s.OriginalDescriptorIndex).SequenceEqual(new[] { 49, 50, 51 }), "source model bytes name descriptors, not global map keys");
        check(local.Slots.All(s => s.PackageWadOffset == 0x3106518 && s.PackageByteLength == 0x35B4 && s.ProfilePayloadByteLength == 0x35AC &&
            s.PackageSha256 == "070a57069c8747ada4caacc0394b93e9a970e5ba7ff21159584abdc7a0fd4faf" &&
            s.ProfilePayloadSha256 == "24df9ae279f32ba10a8e3bd25880d6989991b9225acf45755e6aaf985d66f529"), "whole registered package source ownership distinct from logical payload");
        check(local.FinalDataSha256 == "53d5746a7c08ed65e0f827ed89a51c7a0dd97cb76c4c5c1ead51aa84a32ec0f8" &&
            local.FinalSpuBankByteLength == 0x79000 && local.AppendedDescriptorCount == 3, "independent local-only whole-level golden");
        var localBank = new NativeLevelSoundLayout(local.CopyFinalLevelData()).Sound;
        check(local.GlobalBindings.Count == 0 && localBank.Map.SequenceEqual(target.Sound.Map), "all256 global map bytes protected in local-only transaction");
        check(local.Bindings.Select(b => (int)b.After).SequenceEqual(new[] { 80, 81, 82 }), "local-only target descriptor indices append after80 owners");
        var combined = await source.CompileModelSlotMergeAsync(path, inventory, 33, 12, 0x11B, [2, 1, 0], [new(0x26), electric]);
        check(combined.FinalDataSha256 == "9cd7dfd29bcf194a1a4b2d1a80c94f466dbbbed736159d9fd140bf113bb8fe0e", "independent combined global/local whole-level golden");
        check(combined.FinalSpuBankByteLength == 0x7B000 && combined.ExactAppendedSampleBytes == 0xA2C0 &&
            combined.SectorRoundedGrowth == 0xB000 && combined.AppendedDescriptorCount == 4, "combined exact and rounded allocation accounting");
        check(combined.AppendedSamples.Select(s => s.BankRelativeOffset).SequenceEqual(new[] { 0x70800, 0x72540, 0x77970, 0x79A40 }), "sorted donor40/49/50/51 allocation offsets");
        check(combined.Bindings.Select(b => (int)b.After).SequenceEqual(new[] { 81, 82, 83 }) &&
            combined.GlobalBindings.Single(b => b.GlobalSelector == 0x1F).FinalDescriptor == 80 &&
            !combined.GlobalBindings.Single(b => b.GlobalSelector == 0x26).MappingChanged, "separate local references and explicit electric global binding; native death resource reused");
        check(combined.GlobalBindings.All(b => !b.ConsumerOwnershipResolved) && !combined.CompleteConsumerOwnershipProven &&
            !combined.RuntimeFitProven && !combined.RuntimePlaybackProven && !combined.PublicationApproved, "local allocation grants no consumer/playback/publication authority");
        var finalSound = new NativeLevelSoundLayout(combined.CopyFinalLevelData()).Sound;
        check(finalSound.CopyBank().AsSpan(0, target.Sound.BankByteLength).SequenceEqual(target.Sound.CopyBank()), "all old bank bytes including padding preserved");
        check(finalSound.Descriptors.Take(80).Select(d => d.Sha256).SequenceEqual(target.Sound.Descriptors.Select(d => d.Sha256)), "all80 old descriptor indices and bytes exact");
        check(Enumerable.Range(0, 256).Where(i => i != 0x1F).All(i => finalSound.Map[i] == target.Sound.Map[i]), "all255 other global consumers retain their map bytes");
        foreach (var binding in combined.Bindings)
            check(NativeGlobalSoundMergeLayout.Equivalent(donor.Sound.Descriptors[binding.Before], finalSound.Descriptors[binding.After]), "actual local descriptor parameters and complete sample equivalence");
        byte[] returned = combined.CopyRetargetedSourcePackage(); returned[4] = 0;
        check(combined.CopyRetargetedSourcePackage()[4] == 81, "package output detached from issued plan");
        var returnedPayloads = combined.CopyInsertionPayloads(); returnedPayloads[0].Bytes[0] ^= 1;
        check(returnedPayloads[0].Bytes[0] != combined.CopyInsertionPayloads()[0].Bytes[0], "local allocation insertion buffers detached");
        await rejectAsync(() => source.CompileModelSlotMergeAsync(path, inventory, 33, 12, 0x11B, [0], [new(0x1F)]), "local request cannot grant default global rebind");
        await rejectAsync(() => source.CompileModelSlotMergeAsync(path, inventory, 33, 12, 0x11B, [0], [new(0x20, electric.MappingPolicy)]), "unrelated explicit selector rebind not accepted by bounded model adapter");
        await rejectAsync(() => source.CompileModelSlotMergeAsync(path, inventory, 33, 12, 0x11B, [3]), "native FF-unbound slot rejected");
        await rejectAsync(() => source.CompileModelSlotMergeAsync(path, inventory, 33, 12, 0, [0]), "class0 special player is not model slot schema");
        await rejectAsync(() => source.CompileModelSlotMergeAsync(path, inventory, 13, 12, 0x11B, [0]), "unsupported native package profile refused");
        await rejectAsync(() => source.CompileModelSlotMergeAsync(path, inventory, 33, 65, 0x11B, [0]), "no independent ID65 resource ownership");
        using FileStream image = File.OpenRead(path);
        var disc = DiscImage.DetectLayout(path);
        byte[] original = DiscImage.ReadFileBytes(image, disc, 37, target.DataWadOffset, target.DataByteLength);
        byte[] donorData = DiscImage.ReadFileBytes(image, disc, 37, donor.DataWadOffset, donor.DataByteLength);
        var space = new NativeAddressSpace(NativeAddressKind.LevelData, "donor", NativeAddressRevision.Original, donorData.Length);
        byte[] Read(NativeByteExtent extent)
        {
            if (extent.Space != space) throw new InvalidDataException("Detached read domain");
            return donorData.AsSpan((int)extent.Offset, (int)extent.ByteLength).ToArray();
        }
        var models = NativeMobyModelRegistrationSource.CaptureInventory(inventory.Levels.Single(l => l.LevelId == 33), space, Read);
        var package = NativeModelSoundPackageSource.Capture(donor, models, space, 0x11B, [0, 1, 2], Read);
        check(package.Slots.Count == 3, "direct canonical source issuer positive control");
        var detached = new NativeAddressSpace(NativeAddressKind.LevelData, "donor", NativeAddressRevision.Original, donorData.Length);
        reject(() => NativeModelSoundPackageSource.Capture(donor, models, detached, 0x11B, [0], Read), "equal labels cannot substitute canonical address domain");
        reject(() => NativeModelSoundPackageSource.Capture(target, models, space, 0x11B, [0], Read), "bank/model source mismatch");
        byte[] Altered(NativeByteExtent extent)
        {
            byte[] b = Read(extent); if (extent.ByteLength != donorData.Length) b[4] ^= 1; return b;
        }
        reject(() => NativeModelSoundPackageSource.Capture(donor, models, space, 0x11B, [0], Altered), "changed source slot byte cannot borrow original package hash");
        byte[] AlteredPadding(NativeByteExtent extent)
        {
            byte[] b = Read(extent); if (extent.ByteLength != donorData.Length) b[^1] = 1; return b;
        }
        reject(() => NativeModelSoundPackageSource.Capture(donor, models, space, 0x11B, [0], AlteredPadding), "nonzero registered-owner padding cannot borrow logical payload identity");
        reject(() => NativeModelSoundPackageSource.Capture(donor, models, space, 0x11B, [0], extent => Read(extent)[..^1]), "truncated source reader refused");
        byte[] playerAndSound = compose(original, combined.CopyInsertionPayloads().Concat([
            (await player.CompileMergeAsync(path, 33, 12, [8])).CopyInsertionPayload()! with { PatchIndex = 10 }]).ToArray(), combined.CopyNonHeaderMutations());
        check(NativeSoundBankCodec.Hash(playerAndSound) == "d0d83a7fb45204fba26f8248f3f6a3bae20c0de22b7596d98db6f5d6cbff0b90", "independent four sounds plus player8 composition golden");
        check(playerAndSound.Length == original.Length + 0xF000, "three-subfile growth including player8 exact");
        var texture = NativeDarkHollowWizardTexturePlan.Capture(path);
        byte[] currentTexture = texture.CopyWizardPackage(), composedPackage = combined.CopyTextureComposedPackage(texture);
        for (int i = 0; i < currentTexture.Length; i++)
            if (combined.Bindings.All(b => b.PackageFieldOffset != i)) check(currentTexture[i] == composedPackage[i], "every rebased texture/model byte outside sound slots survives");
        check(combined.Bindings.All(b => composedPackage[b.PackageFieldOffset] == b.After), "texture-rebased package receives only source-owned local slot changes");
        check(composedPackage.Length == 0x35B4 && composedPackage.AsSpan(0x35AC).SequenceEqual(package.CopyPackage().AsSpan(0x35AC)), "complete registered owner padding preserved beyond texture payload");
        reject(() => NativeModelSoundPackageSource.ApplyBindings(composedPackage, combined.Bindings), "repeat application detects upstream slot conflict");
        image.Position = 0;
        check(Convert.ToHexStringLower(await System.Security.Cryptography.SHA256.HashDataAsync(image)) == source.SourceImageSha256 &&
            image.Length == NativeGlobalSoundSource.OriginalImageByteLength && image.Position == image.Length, "all local source reads preserve full original identity");
        Console.WriteLine($"MODEL_SOUND_SOURCE_OK local={local.FinalDataSha256} combined={combined.FinalDataSha256} playerComposition={NativeSoundBankCodec.Hash(playerAndSound)} texturePackage={NativeSoundBankCodec.Hash(composedPackage)} runtime=false publication=false");
    }
}
