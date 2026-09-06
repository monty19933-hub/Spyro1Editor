using System.Buffers.Binary;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

internal static class NativeResourceCompositionSourceCases
{
    internal static async Task RunAsync(string path, Action<bool, string> check, Action<Action, string> reject)
    {
        var inventory = NativeMobySourceInventory.Capture(path, LevelCatalog.Load(Environment.CurrentDirectory));
        var playerSource = await NativePlayerAnimationSource.CaptureAsync(path);
        var player = await playerSource.CompileMergeAsync(path, 33, 12, [8]);
        var soundSource = await NativeGlobalSoundSource.CaptureAsync(path);
        var sound = await soundSource.CompileModelSlotMergeAsync(path, inventory, 33, 12, 0x11B, [0, 1, 2],
            [new(0x1F, NativeGlobalSoundMappingPolicy.ExplicitRebindUnresolvedConsumers), new(0x26)]);
        var texture = NativeDarkHollowWizardTexturePlan.Capture(path);
        var scene = NativeDarkHollowWizardScenePlan.Capture(path);
        var plain = await NativeDarkHollowWizardResourcePlan.CompileAsync(path, inventory, player, sound, texture);
        var withScene = await NativeDarkHollowWizardResourcePlan.CompileAsync(path, inventory, player, sound, texture, scene);
        var reversed = await NativeDarkHollowWizardResourcePlan.CompileReversedForSmokeAsync(path, inventory, player, sound, texture, scene);
        check(plain.FinalDataSha256 == "0ca779c580d580c219526d6fa98515bedaffa418db7d9c973d8d04c33ab9d9f4" &&
            withScene.FinalDataSha256 == "24ff0bc281e06c0344af00e4d7883331b8a73ee13b3ca4a564fe1a03477d9e86", "independent raw-sector original-address splice oracle goldens");
        check(withScene.FinalDataSha256 == reversed.FinalDataSha256 && withScene.GraphFingerprint == reversed.GraphFingerprint, "native stage ordering cannot choose output or graph");
        check(plain.SectorRoundedGrowth == 0x13000 && withScene.SectorRoundedGrowth == 0x13000, "native three-owner once-rounded total growth");
        check(plain.GenericCountBefore == 34 && plain.GenericCountAfter == 36 && plain.Resources.Select(r => r.ActorClassId).SequenceEqual(new ushort[] { 0x26, 0x11B }), "native34 original generic resources preserved plus2 new");
        check(plain.Resources.Select(r => r.ByteLength).SequenceEqual(new[] { 0x564, 0x35B4 }), "full registered package owners including Wizard padding");
        check(!plain.IncludesSceneAppend && withScene.IncludesSceneAppend && !withScene.BehaviorInstalled && !withScene.ParticleDescriptorInstalled &&
            !withScene.CompleteNativeClosureProven && !withScene.PlacementApproved && !withScene.RuntimeFitProven && !withScene.PeakTransientFitProven && !withScene.PublicationApproved,
            "resource composition does not promote behavior/effect/placement/runtime/publication");
        byte[] output = withScene.CopyFinalLevelData(); withScene.RequireFinal(output);
        int model = I(output, 0x10), sceneOffset = I(output, 0x18);
        check(I(output, 0x14) == 0x8000 + I(scene.CopyOriginalLevel(), 0x14), "model rounds combined7934 exact growth once to8000");
        check(output.AsSpan(sceneOffset, scene.SceneByteLength).SequenceEqual(scene.CopyScene()), "source-issued entire scene stage survived model/sound relocation");
        check(output.AsSpan(0x800, 0x80000).SequenceEqual(texture.CopyTexturePatchedTargetLevel().AsSpan(0x800, 0x80000)), "all texture stage VRAM bytes survive composition");
        foreach (var resource in withScene.Resources)
            check(resource.FinalPackageSha256 == NativeMobyModelLoaderCertificate.Hash(output.AsSpan(resource.FinalDataOffset, resource.ByteLength)), "final registered package readback");
        check(withScene.Resources.Single(r => r.ActorClassId == 0x11B).FinalPackageSha256 == "e3cd4d67f39eb9375a8d17b4c2830050f33b5523c3421e7eba828af9f6eabe6e", "texture plus local-sound Wizard complete package golden");
        var ram = withScene.RuntimeMemory;
        int start = I(output, 8), cursor = start;
        for (int i = 0; i < 6; i++) cursor += I(output, cursor);
        int portals = I(output, cursor); cursor += 4;
        for (int i = 0; i < portals; i++) { cursor += 0x34 + I(output, cursor + 4) * 12; cursor += I(output, cursor); }
        cursor += I(output, cursor); cursor += I(output, cursor);
        check(ram.UsedLevelDataBytes == cursor - start && ram.ModelBytesRead == I(output, 0x14) && ram.SceneBytesRead == I(output, 0x1C), "independent final native data/CD owner accounting");
        check(ram.SceneReadEnd == ram.CopyBufferAddress + cursor - start + I(output, 0x14) + I(output, 0x1C) &&
            ram.NormalReservedBufferGapBytes == ram.NormalReservedBufferStart - (long)ram.SceneReadEnd && ram.DynamicMobyCapacity == 90 && ram.PlacedMobyCount == 129,
            "final normal-load memory and dynamic-pool accounting matches independent owners");
        check(ram.NativeSourceCertified && ram.KnownPostLoadExtentsDisjoint && !ram.RuntimeFitProven && !ram.PeakTransientFitProven && !ram.PublicationApproved, "typed source RAM report does not promote peak fit");
        byte[] bad = output.ToArray(); bad[0x50] ^= 4; reject(() => withScene.RequireFinal(bad), "final root0 corruption");
        bad = output.ToArray(); bad[model + 4] ^= 1; reject(() => withScene.RequireFinal(bad), "old model corruption");
        bad = output.ToArray(); bad[sceneOffset] ^= 1; reject(() => withScene.RequireFinal(bad), "authored scene corruption");
        output[^1] ^= 1; check(withScene.CopyFinalLevelData()[^1] != output[^1], "public final bytes immutable");
        var wrongPlayer = await playerSource.CompileMergeAsync(path, 33, 13, [8]);
        reject(() => NativeDarkHollowWizardResourcePlan.CompileAsync(path, inventory, wrongPlayer, sound, texture).GetAwaiter().GetResult(), "stage source owner mismatch");
        var localOnly = await soundSource.CompileModelSlotMergeAsync(path, inventory, 33, 12, 0x11B, [0, 1, 2]);
        reject(() => NativeDarkHollowWizardResourcePlan.CompileAsync(path, inventory, player, localOnly, texture).GetAwaiter().GetResult(), "missing requested global electric resource");
        var particle = await NativeDarkHollowWizardParticlePlan.CaptureAsync(path, texture);
        check(particle.SourceComponents.Count == 35 && particle.SourceComponents.Sum(c => c.RecordCount) == 428, "all35 original particle tables and428 records captured");
        foreach (var component in particle.SourceComponents)
            check(NativeParticleDescriptorCodec.Decode(component.CopyComponent()).CopyBytes().AsSpan().SequenceEqual(component.CopyComponent()), "all35 exact native particle roundtrips");
        check(particle.OriginalComponentDataOffset == 0x128F7C && particle.OriginalCount == 11 && particle.FinalCount == 12 &&
            particle.CopyOriginalComponent().Length == 0x8C && particle.CopyFinalComponent().Length == 0x98, "native particle append uses real count/length owner");
        check(particle.CopyFinalComponent().AsSpan(8, 0x84).SequenceEqual(particle.CopyOriginalComponent().AsSpan(8)) &&
            particle.CopyDescriptor().AsSpan().SequenceEqual(texture.CopyParticle7Descriptor()), "all eleven original descriptors retained with source-issued relocated7");
        check(!particle.BehaviorInstalled && !particle.CompleteNativeClosureProven && !particle.PublicationApproved, "particle descriptor is not callback or runtime closure");
        var emitted = await NativeDarkHollowWizardResourcePlan.CompileWithParticleAsync(path, inventory, player, sound, texture, particle, scene);
        var emittedPlain = await NativeDarkHollowWizardResourcePlan.CompileWithParticleAsync(path, inventory, player, sound, texture, particle);
        var emittedReverse = await NativeDarkHollowWizardResourcePlan.CompileWithParticleReversedForSmokeAsync(path, inventory, player, sound, texture, particle, scene);
        var lifetime = NativeDarkHollowWizardParticleBehaviorSource.Capture(path).LifetimeContract;
        lifetime.RequireResources(emitted); lifetime.RequireResources(emittedPlain);
        check(true, "single-frame proof consumes actual installed descriptor with and without scene stage");
        reject(() => lifetime.RequireResources(withScene), "pending descriptor metadata cannot replace actual particle installation");
        check(emittedPlain.FinalDataSha256 == "4080e940c46ac9c53712cc97285396ecdcae59e0a369c4e8971eeb49207de8d4" &&
            emitted.FinalDataSha256 == "26e8b7f0bebb49c5c39c0e42ea680428ddd01c6e901ebc53e619d6f39c02be91", "independent original-sector particle stage golden with and without scene");
        check(emitted.FinalDataSha256 == emittedReverse.FinalDataSha256 && emitted.GraphFingerprint == emittedReverse.GraphFingerprint, "particle stage enumeration cannot choose final bytes or registration graph");
        byte[] particleData = emitted.CopyFinalLevelData(); int dataShift = I(particleData, 8) - I(scene.CopyOriginalLevel(), 8);
        int particleAt = particle.OriginalComponentDataOffset + dataShift;
        check(emitted.ParticleDescriptorInstalled && !emitted.BehaviorInstalled && !emitted.CompleteNativeClosureProven && !emitted.RuntimeFitProven && !emitted.PublicationApproved,
            "resource descriptor installed without particle behavior promotion");
        check(particleData.AsSpan(particleAt, 0x98).SequenceEqual(particle.CopyFinalComponent()), "source-issued particle body installed in common layout");
        check(emitted.SectorRoundedGrowth == withScene.SectorRoundedGrowth && I(particleData, 0x10) == model && I(particleData, 0x18) == sceneOffset,
            "particle and sound share one rounded subfile owner without moving models again");
        var relocatedSound = new NativeLevelSoundLayout(particleData).Sound;
        var beforeSound = new NativeLevelSoundLayout(withScene.CopyFinalLevelData()).Sound;
        check(relocatedSound.ComponentSha256 == beforeSound.ComponentSha256 && relocatedSound.BankSha256 == beforeSound.BankSha256,
            "all sound component fields map across particle insertion without semantic change");
        check(particleData.AsSpan(model).SequenceEqual(withScene.CopyFinalLevelData().AsSpan(model)) &&
            particleData.AsSpan(0x800, 0x80000).SequenceEqual(withScene.CopyFinalLevelData().AsSpan(0x800, 0x80000)), "complete models authored scene and VRAM complement survive particle append");
        check(emitted.RuntimeMemory.SceneReadEnd == ram.SceneReadEnd + 12 && emitted.RuntimeMemory.NormalReservedBufferGapBytes == ram.NormalReservedBufferGapBytes - 12,
            "particle used-data bytes appear in native memory accounting");
        foreach (int field in new[] { particleAt, particleAt + 4, particleAt + 8, particleAt + 0x8C, particleAt + 0x98 })
        {
            byte[] corrupt = particleData.ToArray(); corrupt[field] ^= 1;
            reject(() => emitted.RequireFinal(corrupt), "particle count old record appended record and relocated sound corruption refused");
        }
        byte[] wrongSource = scene.CopyOriginalLevel(); wrongSource[particle.OriginalComponentDataOffset + 8] ^= 1;
        reject(() => particle.RequireOriginalAndTexture(wrongSource, texture), "stale source particle owner refused");
        check(typeof(NativeDarkHollowWizardParticlePlan).GetConstructors().Length == 0 && typeof(NativeParticleSourceComponent).GetConstructors().Length == 0,
            "particle native authority cannot be caller constructed");
        Console.WriteLine($"NATIVE_PARTICLE_COMPOSITION_GOLDEN plain={emittedPlain.FinalDataSha256} scene={emitted.FinalDataSha256} gap={emitted.RuntimeMemory.NormalReservedBufferGapBytes:X}");
        Console.WriteLine($"NATIVE_RESOURCE_COMPOSITION_NATIVE_GOLDENS plain={plain.FinalDataSha256} scene={withScene.FinalDataSha256} graph={withScene.GraphFingerprint} gap={ram.NormalReservedBufferGapBytes:X} growth={withScene.SectorRoundedGrowth:X}");
    }
    private static int I(byte[] b, int o) => checked((int)BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(o)));
}
