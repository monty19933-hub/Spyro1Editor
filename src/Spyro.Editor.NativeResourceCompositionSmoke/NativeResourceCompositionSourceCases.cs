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
        Console.WriteLine($"NATIVE_RESOURCE_COMPOSITION_NATIVE_GOLDENS plain={plain.FinalDataSha256} scene={withScene.FinalDataSha256} graph={withScene.GraphFingerprint} gap={ram.NormalReservedBufferGapBytes:X} growth={withScene.SectorRoundedGrowth:X}");
    }
    private static int I(byte[] b, int o) => checked((int)BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(o)));
}
