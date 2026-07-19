using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Exporting;

public static class SpringChestInGamePatchBuilder
{
    private const int RecordStride = 0x58;
    private const uint ExeDestination = 0x80010000;
    private const uint HookAddress = 0x80012230;
    private const uint OriginalHookCallTarget = 0x8003385C;
    private const uint PayloadAddress = 0x8007314C;
    private const uint EntryHookPayloadAddress = 0x80073924;
    private const uint AwardTreasureRoutineAddress = 0x800420D4;
    private const uint TotalJewelsAddress = 0x80075860;
    private const uint CurrentLevelTreasureIndexAddress = 0x80075964;
    private const uint LevelTreasureTableAddress = 0x80077420;
    private const uint InventoryTotalTreasureAddress = 0x8007C130;
    private const int PayloadReserveBytes = 0x400;
    private const int PayloadStateOffset = 0x3F4;
    private const int ScratchSaveOffset = 0x340;
    private const uint ScratchSaveAddress = PayloadAddress + (uint)ScratchSaveOffset;
    private const int RewardZOffset = 0x0558;

    public static IReadOnlyList<MobySourcePatch> BuildSmallCaveMinimalPopPatches(
        string sourceImagePath,
        LevelDefinition level,
        int controllerIndex,
        int shellIndex,
        int spawnGemIndex,
        int controllerActorId,
        int rewardGemIdByte = 0x55,
        int runtimeVariantId = 0x01AC,
        bool refreshRewardUntilListed = true,
        bool clearRewardRuntimeWord = false,
        int fallbackCleanupFrames = 0,
        bool useLooseGemRowShape = false,
        bool useVisibleGemTypeWord = false,
        int rewardZOffset = RewardZOffset,
        bool preservePreinitializedRewardRow = false,
        int uncollectedReturnFrames = 0,
        int rewardReturnDropPerFrame = 0,
        int rewardReturnDropStartFrames = 0,
        int rewardRiseFrames = 0,
        int rewardRisePerFrame = 0,
        bool maskAirborneRewardValue = false,
        bool rearmOnEarlyRewardPickup = false,
        bool unlinkAirborneRewardFromActiveList = false,
        bool parkReturnedRewardAsInert = false,
        bool monitorReturnedRewardPickupList = false,
        bool restoreReturnedRewardOnHit = false,
        bool fullQuarantineReturnedReward = false,
        bool delayReturnedRewardCollection = false,
        bool delayReturnedRewardTypeWord = false,
        bool scriptRewardMotionWithoutPickupList = false,
        bool blankReturnedRewardRow = false,
        bool preserveRewardVisualScaffold = false,
        bool restoreDonorGemShapeOnPop = false,
        bool leaveDonorGemPickupTypeBlankOnPop = false,
        bool leaveDonorGemTailWordBlankOnPop = false,
        bool useNativeStateZeroShellWords = false,
        bool useNativeReadyShellWords = false,
        bool useNativePreHitShellLifecycleWords = false,
        bool useNativeSpringEffectRow = false,
        bool useBlueGemNativeSpringEffectVisual = false,
        bool useBlueGemNativeSpringEffectOrdinal = false,
        bool manualSpyroTouchCollectNativeEffectVisual = false,
        bool manualAwardTreasureCounters = false,
        bool manualAwardNativeTreasureRoutine = false,
        bool manualAwardFixedTreasureWords = false,
        bool manualAnimateRewardPopArc = false)
    {
        if (!TryGetLevelPointer(level.Key, out uint levelPointer))
            return [];
        if (rewardGemIdByte is < 0x53 or > 0x57)
            throw new ArgumentOutOfRangeException(nameof(rewardGemIdByte), "Spring chest reward gem byte must be 0x53..0x57.");
        if (runtimeVariantId is < 0 or > 0xFFFF)
            throw new ArgumentOutOfRangeException(nameof(runtimeVariantId), "Spring chest runtime variant id must fit in a halfword.");

        DiscLayout layout = DiscImage.DetectLayout(sourceImagePath);
        using FileStream stream = File.OpenRead(sourceImagePath);
        DiscFileRecord exe = DiscImage.FindRootFileRecord(stream, layout, name =>
            name.StartsWith("SCUS", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("SCES", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("SCPS", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("SLUS", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("SLES", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("SLPS", StringComparison.OrdinalIgnoreCase));

        long hookFileOffset = 0x800 + ((long)HookAddress - ExeDestination);
        long payloadFileOffset = 0x800 + ((long)PayloadAddress - ExeDestination);
        if (hookFileOffset < 0x800 || payloadFileOffset < 0x800 || payloadFileOffset + PayloadReserveBytes > exe.Size)
            throw new InvalidOperationException("Spring Chest helper code cave is outside the executable body.");

        byte[] existingHookBytes = DiscImage.ReadFileBytes(stream, layout, exe.Lba, hookFileOffset, 4);
        uint expectedHookWord = NewJalWord(OriginalHookCallTarget);
        uint originalHookWord = BitConverter.ToUInt32(existingHookBytes, 0);
        if (originalHookWord != expectedHookWord)
            throw new InvalidOperationException($"Spring Chest helper hook site expected 0x{expectedHookWord:X8}, got 0x{originalHookWord:X8}.");

        byte[] existingPayloadBytes = DiscImage.ReadFileBytes(stream, layout, exe.Lba, payloadFileOffset, PayloadReserveBytes);
        if (existingPayloadBytes.Any(value => value != 0))
            throw new InvalidOperationException($"Spring Chest helper payload cave 0x{PayloadAddress:X8} is not blank.");

        byte[] hookBytes = BitConverter.GetBytes(NewJalWord(PayloadAddress));
        byte[] payloadBlock = new byte[PayloadReserveBytes];
        byte[] payload = BuildSmallCaveMinimalPopPayload(levelPointer, controllerIndex, shellIndex, spawnGemIndex, controllerActorId, rewardGemIdByte, runtimeVariantId, refreshRewardUntilListed, clearRewardRuntimeWord, fallbackCleanupFrames, useLooseGemRowShape, useVisibleGemTypeWord, rewardZOffset, preservePreinitializedRewardRow, uncollectedReturnFrames, rewardReturnDropPerFrame, rewardReturnDropStartFrames, rewardRiseFrames, rewardRisePerFrame, maskAirborneRewardValue, rearmOnEarlyRewardPickup, unlinkAirborneRewardFromActiveList, parkReturnedRewardAsInert, monitorReturnedRewardPickupList, restoreReturnedRewardOnHit, fullQuarantineReturnedReward, delayReturnedRewardCollection, delayReturnedRewardTypeWord, scriptRewardMotionWithoutPickupList, blankReturnedRewardRow, preserveRewardVisualScaffold, restoreDonorGemShapeOnPop, leaveDonorGemPickupTypeBlankOnPop, leaveDonorGemTailWordBlankOnPop, useNativeStateZeroShellWords, useNativeReadyShellWords, useNativePreHitShellLifecycleWords, useNativeSpringEffectRow, useBlueGemNativeSpringEffectVisual, useBlueGemNativeSpringEffectOrdinal, manualSpyroTouchCollectNativeEffectVisual, manualAwardTreasureCounters, manualAwardNativeTreasureRoutine, manualAwardFixedTreasureWords, manualAnimateRewardPopArc);
        if (payload.Length > PayloadStateOffset)
            throw new InvalidOperationException($"Spring Chest helper payload is {payload.Length} bytes and would overlap state offset 0x{PayloadStateOffset:X}.");
        Array.Copy(payload, payloadBlock, payload.Length);

        return
        [
            new MobySourcePatch(
                Label: $"{level.Key}-spring-chest-helper-hook",
                Kind: "spring-chest-helper-hook",
                LevelKey: level.Key,
                MobyLabel: "Spring Chest helper",
                TrueIndex: -1,
                RecordOffset: "exe-hook",
                WadRelativeOffset: $"exe:0x{HookAddress:X8}",
                ImageOffset: $"0x{DiscImage.ConvertFileOffsetToImageOffset(layout, exe.Lba, hookFileOffset):X}",
                ByteLength: hookBytes.Length,
                BeforeHexPreview: ToHex(existingHookBytes),
                AfterHexPreview: ToHex(hookBytes),
                Description: $"Patch the main loop hook at 0x{HookAddress:X8} so {level.DisplayName} can run the Spring Chest pop/collect helper."),
            new MobySourcePatch(
                Label: $"{level.Key}-spring-chest-helper-payload",
                Kind: "spring-chest-helper-payload",
                LevelKey: level.Key,
                MobyLabel: "Spring Chest helper",
                TrueIndex: -1,
                RecordOffset: "exe-payload",
                WadRelativeOffset: $"exe:0x{PayloadAddress:X8}",
                ImageOffset: $"0x{DiscImage.ConvertFileOffsetToImageOffset(layout, exe.Lba, payloadFileOffset):X}",
                ByteLength: payloadBlock.Length,
                BeforeHexPreview: ToHex(existingPayloadBytes),
                AfterHexPreview: ToHex(payloadBlock),
                Description: refreshRewardUntilListed
                    ? $"Inject the small Spring Chest helper payload at 0x{PayloadAddress:X8}; it arms controller T{controllerIndex}, shell T{shellIndex}, and reward row T{spawnGemIndex} with runtime variant 0x{runtimeVariantId:X4}."
                    : useNativeSpringEffectRow
                        ? $"Inject the small native-effect-only Spring Chest helper payload at 0x{PayloadAddress:X8}; it arms controller T{controllerIndex}, shell T{shellIndex}, writes native after-hit effect row T{spawnGemIndex} as {(useBlueGemNativeSpringEffectVisual ? "blue-gem visual id 0x0055" : "actor/type 0x0022/0x20")}{(useBlueGemNativeSpringEffectOrdinal ? " with blue gem ordinal/value byte" : "")}, scripts its short motion without the pickup list{(manualSpyroTouchCollectNativeEffectVisual ? ", and manually finishes the chest when Spyro touches the visual" : "")}{(manualAwardNativeTreasureRoutine ? ", calls the game's treasure-award routine" : manualAwardTreasureCounters ? ", writes experimental treasure counters" : "")}, then blanks the scratch row again."
                    : blankReturnedRewardRow && uncollectedReturnFrames > 0
                        ? $"Inject the small blank-scratch reward spring-arc return-loop Spring Chest helper payload at 0x{PayloadAddress:X8}; it arms controller T{controllerIndex}, shell T{shellIndex}, rebuilds reward row T{spawnGemIndex} with runtime variant 0x{runtimeVariantId:X4} only during the pop{(restoreDonorGemShapeOnPop ? " using the donor standalone-gem shape" : "")}{(leaveDonorGemPickupTypeBlankOnPop ? " while leaving the pickup/type words blank" : "")}{(leaveDonorGemTailWordBlankOnPop ? " while leaving the final pickup tail word blank" : "")}, raises the visible reward by 0x{rewardRisePerFrame:X} per frame for 0x{rewardRiseFrames:X} frame(s), lowers it by 0x{rewardReturnDropPerFrame:X} per frame after 0x{rewardReturnDropStartFrames:X} frame(s), returns uncollected rewards after 0x{uncollectedReturnFrames:X} frame(s){(maskAirborneRewardValue ? ", masks the reward value byte while airborne" : "")}{(unlinkAirborneRewardFromActiveList ? ", unlinks the reward from the active pickup list while airborne and keeps scanning for relisted pickup entries" : "")}{(monitorReturnedRewardPickupList ? ", keeps a post-return unlink sweep active after the reward returns" : "")}{(scriptRewardMotionWithoutPickupList ? ", scripts the reward motion without waiting for the active pickup list" : "")}, blanks the returned reward row while parked{(preserveRewardVisualScaffold ? " while preserving the donor gem visual scaffold" : "")}, and applies a fallback chest cleanup timer at Z offset 0x{rewardZOffset:X}."
                    : preservePreinitializedRewardRow
                        ? uncollectedReturnFrames > 0
                            ? rewardRiseFrames > 0
                                ? $"Inject the small preinitialized-reward spring-arc return-loop Spring Chest helper payload at 0x{PayloadAddress:X8}; it arms controller T{controllerIndex}, shell T{shellIndex}, moves already-initialized reward row T{spawnGemIndex} with runtime variant 0x{runtimeVariantId:X4} without refreshing the reward row after it is spawned or rewriting the reward row's visible gem identity, raises the visible reward by 0x{rewardRisePerFrame:X} per frame for 0x{rewardRiseFrames:X} frame(s), lowers it by 0x{rewardReturnDropPerFrame:X} per frame after 0x{rewardReturnDropStartFrames:X} frame(s), returns uncollected rewards after 0x{uncollectedReturnFrames:X} frame(s){(maskAirborneRewardValue ? ", masks the reward value byte while airborne" : "")}{(rearmOnEarlyRewardPickup ? ", rearms instead of cleaning up if the reward is picked up early" : "")}{(unlinkAirborneRewardFromActiveList ? ", unlinks the reward from the active pickup list while airborne and keeps scanning for relisted pickup entries" : "")}{(parkReturnedRewardAsInert ? ", parks the returned reward as a fully quarantined inert hidden row" : "")}{(monitorReturnedRewardPickupList ? ", keeps a post-return unlink sweep active after the reward returns" : "")}{(restoreReturnedRewardOnHit ? ", restores the quarantined reward identity only when the chest is hit again" : "")}{(fullQuarantineReturnedReward ? ", fully clears the returned reward identity while parked" : "")}{(delayReturnedRewardCollection ? ", delays restoring the reward value until the gem has risen" : "")}{(delayReturnedRewardTypeWord ? ", delays restoring the reward type until the gem has risen" : "")}{(scriptRewardMotionWithoutPickupList ? ", scripts the reward motion without waiting for the active pickup list" : "")}{(blankReturnedRewardRow ? ", blanks the returned reward row while parked" : "")}, and applies a fallback chest cleanup timer at Z offset 0x{rewardZOffset:X}."
                                : $"Inject the small preinitialized-reward return-loop Spring Chest helper payload at 0x{PayloadAddress:X8}; it arms controller T{controllerIndex}, shell T{shellIndex}, moves already-initialized reward row T{spawnGemIndex} with runtime variant 0x{runtimeVariantId:X4} without refreshing the reward row after it is spawned or rewriting the reward row's visible gem identity, lowers the visible reward by 0x{rewardReturnDropPerFrame:X} per frame after 0x{rewardReturnDropStartFrames:X} frame(s), returns uncollected rewards after 0x{uncollectedReturnFrames:X} frame(s), and applies a fallback chest cleanup timer at Z offset 0x{rewardZOffset:X}."
                            : $"Inject the small preinitialized-reward Spring Chest helper payload at 0x{PayloadAddress:X8}; it arms controller T{controllerIndex}, shell T{shellIndex}, and moves already-initialized reward row T{spawnGemIndex} with runtime variant 0x{runtimeVariantId:X4} without refreshing the reward row after it is spawned or rewriting the reward row's visible gem identity, clears no reward runtime fields, and applies a fallback chest cleanup timer at Z offset 0x{rewardZOffset:X}."
                    : useVisibleGemTypeWord
                        ? $"Inject the small visible one-shot Spring Chest helper payload at 0x{PayloadAddress:X8}; it arms controller T{controllerIndex}, shell T{shellIndex}, and reward row T{spawnGemIndex} with runtime variant 0x{runtimeVariantId:X4} without refreshing the reward row after it is spawned, clears the reward runtime visibility word, applies a fallback chest cleanup timer, and uses the loose-gem row flags with the visible-gem type word for the reward at Z offset 0x{rewardZOffset:X}."
                        : clearRewardRuntimeWord || fallbackCleanupFrames > 0 || useLooseGemRowShape
                            ? $"Inject the small visible one-shot Spring Chest helper payload at 0x{PayloadAddress:X8}; it arms controller T{controllerIndex}, shell T{shellIndex}, and reward row T{spawnGemIndex} with runtime variant 0x{runtimeVariantId:X4} without refreshing the reward row after it is spawned, clears the reward runtime visibility word, applies a fallback chest cleanup timer, and uses the loose-gem row shape for the reward."
                        : $"Inject the small one-shot Spring Chest helper payload at 0x{PayloadAddress:X8}; it arms controller T{controllerIndex}, shell T{shellIndex}, and reward row T{spawnGemIndex} with runtime variant 0x{runtimeVariantId:X4} without refreshing the reward row after it is spawned.")
        ];
    }

    public static IReadOnlyList<MobySourcePatch> BuildSmallCaveArmOnlyPatches(
        string sourceImagePath,
        LevelDefinition level,
        int controllerIndex,
        int shellIndex,
        int controllerActorId,
        int rewardGemIdByte = 0x55,
        int runtimeVariantId = 0x01AC)
    {
        if (!TryGetLevelPointer(level.Key, out uint levelPointer))
            return [];
        if (rewardGemIdByte is < 0x53 or > 0x57)
            throw new ArgumentOutOfRangeException(nameof(rewardGemIdByte), "Spring chest reward gem byte must be 0x53..0x57.");
        if (runtimeVariantId is < 0 or > 0xFFFF)
            throw new ArgumentOutOfRangeException(nameof(runtimeVariantId), "Spring chest runtime variant id must fit in a halfword.");

        DiscLayout layout = DiscImage.DetectLayout(sourceImagePath);
        using FileStream stream = File.OpenRead(sourceImagePath);
        DiscFileRecord exe = DiscImage.FindRootFileRecord(stream, layout, name =>
            name.StartsWith("SCUS", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("SCES", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("SCPS", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("SLUS", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("SLES", StringComparison.OrdinalIgnoreCase));

        long hookFileOffset = 0x800 + ((long)HookAddress - ExeDestination);
        long payloadFileOffset = 0x800 + ((long)PayloadAddress - ExeDestination);
        if (hookFileOffset < 0x800 || payloadFileOffset < 0x800 || payloadFileOffset + PayloadReserveBytes > exe.Size)
            throw new InvalidOperationException("Spring Chest helper code cave is outside the executable body.");

        byte[] existingHookBytes = DiscImage.ReadFileBytes(stream, layout, exe.Lba, hookFileOffset, 4);
        uint expectedHookWord = NewJalWord(OriginalHookCallTarget);
        uint originalHookWord = BitConverter.ToUInt32(existingHookBytes, 0);
        if (originalHookWord != expectedHookWord)
            throw new InvalidOperationException($"Spring Chest arm-only helper hook site expected 0x{expectedHookWord:X8}, got 0x{originalHookWord:X8}.");

        byte[] existingPayloadBytes = DiscImage.ReadFileBytes(stream, layout, exe.Lba, payloadFileOffset, PayloadReserveBytes);
        if (existingPayloadBytes.Any(value => value != 0))
            throw new InvalidOperationException($"Spring Chest arm-only helper payload cave 0x{PayloadAddress:X8} is not blank.");

        byte[] hookBytes = BitConverter.GetBytes(NewJalWord(PayloadAddress));
        byte[] payloadBlock = new byte[PayloadReserveBytes];
        byte[] payload = BuildSmallCaveArmOnlyPayload(levelPointer, controllerIndex, shellIndex, controllerActorId, rewardGemIdByte, runtimeVariantId);
        if (payload.Length > PayloadStateOffset)
            throw new InvalidOperationException($"Spring Chest arm-only helper payload is {payload.Length} bytes and would overlap state offset 0x{PayloadStateOffset:X}.");
        Array.Copy(payload, payloadBlock, payload.Length);

        return
        [
            new MobySourcePatch(
                Label: $"{level.Key}-spring-chest-arm-only-helper-hook",
                Kind: "spring-chest-arm-only-helper-hook",
                LevelKey: level.Key,
                MobyLabel: "Spring Chest arm-only helper",
                TrueIndex: -1,
                RecordOffset: "exe-hook",
                WadRelativeOffset: $"exe:0x{HookAddress:X8}",
                ImageOffset: $"0x{DiscImage.ConvertFileOffsetToImageOffset(layout, exe.Lba, hookFileOffset):X}",
                ByteLength: hookBytes.Length,
                BeforeHexPreview: ToHex(existingHookBytes),
                AfterHexPreview: ToHex(hookBytes),
                Description: $"Patch the main loop hook at 0x{HookAddress:X8} so Stone Hill can run the Spring Chest arm-only helper."),
            new MobySourcePatch(
                Label: $"{level.Key}-spring-chest-arm-only-helper-payload",
                Kind: "spring-chest-arm-only-helper-payload",
                LevelKey: level.Key,
                MobyLabel: "Spring Chest arm-only helper",
                TrueIndex: -1,
                RecordOffset: "exe-payload",
                WadRelativeOffset: $"exe:0x{PayloadAddress:X8}",
                ImageOffset: $"0x{DiscImage.ConvertFileOffsetToImageOffset(layout, exe.Lba, payloadFileOffset):X}",
                ByteLength: payloadBlock.Length,
                BeforeHexPreview: ToHex(existingPayloadBytes),
                AfterHexPreview: ToHex(payloadBlock),
                Description: $"Inject the arm-only Spring Chest helper payload at 0x{PayloadAddress:X8}; it links controller T{controllerIndex} and shell T{shellIndex} once with runtime variant 0x{runtimeVariantId:X4}, without reward spawning.")
        ];
    }

    public static IReadOnlyList<MobySourcePatch> BuildSmallCaveRowRepairPatches(
        string sourceImagePath,
        LevelDefinition level,
        int firstIndex,
        int secondIndex,
        int firstRuntimeVariantId = 0x01A6,
        int secondRuntimeVariantId = 0x01A7)
    {
        if (!TryGetLevelPointer(level.Key, out uint levelPointer))
            return [];
        if (firstRuntimeVariantId is < 0 or > 0xFFFF || secondRuntimeVariantId is < 0 or > 0xFFFF)
            throw new ArgumentOutOfRangeException(nameof(firstRuntimeVariantId), "Spring chest runtime variant ids must fit in a halfword.");

        DiscLayout layout = DiscImage.DetectLayout(sourceImagePath);
        using FileStream stream = File.OpenRead(sourceImagePath);
        DiscFileRecord exe = DiscImage.FindRootFileRecord(stream, layout, name =>
            name.StartsWith("SCUS", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("SCES", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("SCPS", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("SLUS", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("SLES", StringComparison.OrdinalIgnoreCase));

        long hookFileOffset = 0x800 + ((long)HookAddress - ExeDestination);
        long payloadFileOffset = 0x800 + ((long)PayloadAddress - ExeDestination);
        if (hookFileOffset < 0x800 || payloadFileOffset < 0x800 || payloadFileOffset + PayloadReserveBytes > exe.Size)
            throw new InvalidOperationException("Spring Chest row-repair helper code cave is outside the executable body.");

        byte[] existingHookBytes = DiscImage.ReadFileBytes(stream, layout, exe.Lba, hookFileOffset, 4);
        uint expectedHookWord = NewJalWord(OriginalHookCallTarget);
        uint originalHookWord = BitConverter.ToUInt32(existingHookBytes, 0);
        if (originalHookWord != expectedHookWord)
            throw new InvalidOperationException($"Spring Chest row-repair helper hook site expected 0x{expectedHookWord:X8}, got 0x{originalHookWord:X8}.");

        byte[] existingPayloadBytes = DiscImage.ReadFileBytes(stream, layout, exe.Lba, payloadFileOffset, PayloadReserveBytes);
        if (existingPayloadBytes.Any(value => value != 0))
            throw new InvalidOperationException($"Spring Chest row-repair helper payload cave 0x{PayloadAddress:X8} is not blank.");

        byte[] hookBytes = BitConverter.GetBytes(NewJalWord(PayloadAddress));
        byte[] payloadBlock = new byte[PayloadReserveBytes];
        byte[] payload = BuildSmallCaveRowRepairPayload(levelPointer, firstIndex, secondIndex, firstRuntimeVariantId, secondRuntimeVariantId);
        if (payload.Length > PayloadStateOffset)
            throw new InvalidOperationException($"Spring Chest row-repair helper payload is {payload.Length} bytes and would overlap state offset 0x{PayloadStateOffset:X}.");
        Array.Copy(payload, payloadBlock, payload.Length);

        return
        [
            new MobySourcePatch(
                Label: $"{level.Key}-spring-chest-row-repair-helper-hook",
                Kind: "spring-chest-row-repair-helper-hook",
                LevelKey: level.Key,
                MobyLabel: "Spring Chest row-repair helper",
                TrueIndex: -1,
                RecordOffset: "exe-hook",
                WadRelativeOffset: $"exe:0x{HookAddress:X8}",
                ImageOffset: $"0x{DiscImage.ConvertFileOffsetToImageOffset(layout, exe.Lba, hookFileOffset):X}",
                ByteLength: hookBytes.Length,
                BeforeHexPreview: ToHex(existingHookBytes),
                AfterHexPreview: ToHex(hookBytes),
                Description: $"Patch the main loop hook at 0x{HookAddress:X8} so Stone Hill can repair imported Spring Chest rows once after load."),
            new MobySourcePatch(
                Label: $"{level.Key}-spring-chest-row-repair-helper-payload",
                Kind: "spring-chest-row-repair-helper-payload",
                LevelKey: level.Key,
                MobyLabel: "Spring Chest row-repair helper",
                TrueIndex: -1,
                RecordOffset: "exe-payload",
                WadRelativeOffset: $"exe:0x{PayloadAddress:X8}",
                ImageOffset: $"0x{DiscImage.ConvertFileOffsetToImageOffset(layout, exe.Lba, payloadFileOffset):X}",
                ByteLength: payloadBlock.Length,
                BeforeHexPreview: ToHex(existingPayloadBytes),
                AfterHexPreview: ToHex(payloadBlock),
                Description: $"Inject a one-shot Spring Chest row repair at 0x{PayloadAddress:X8}; it restores T{firstIndex}/T{secondIndex} to native variants 0x{firstRuntimeVariantId:X4}/0x{secondRuntimeVariantId:X4} after Stone Hill's loader initializes them.")
        ];
    }

    public static IReadOnlyList<MobySourcePatch> BuildSmallCaveHookOnlyPatches(
        string sourceImagePath,
        LevelDefinition level)
    {
        return BuildSmallCavePassThroughHookPatches(
            sourceImagePath,
            level,
            kindPrefix: "spring-chest-hook-only-helper",
            labelSuffix: "hook-only helper",
            descriptionSuffix: "it only calls the original game function and returns.",
            payload: BuildSmallCaveHookOnlyPayload());
    }

    public static IReadOnlyList<MobySourcePatch> BuildSmallCaveStackHookOnlyPatches(
        string sourceImagePath,
        LevelDefinition level)
    {
        return BuildSmallCavePassThroughHookPatches(
            sourceImagePath,
            level,
            kindPrefix: "spring-chest-stack-hook-only-helper",
            labelSuffix: "stack hook-only helper",
            descriptionSuffix: "it only calls the original game function, saving RA on the normal stack instead of the payload scratch area.",
            payload: BuildSmallCaveStackHookOnlyPayload());
    }

    public static IReadOnlyList<MobySourcePatch> BuildSmallCaveEntryHookOnlyPatches(
        string sourceImagePath,
        LevelDefinition level)
    {
        if (!TryGetLevelPointer(level.Key, out _))
            return [];

        ExecutablePatchSafety.GuardPatchRange(
            EntryHookPayloadAddress,
            PayloadReserveBytes,
            "Historical Spring Chest entry-hook payload");

        DiscLayout layout = DiscImage.DetectLayout(sourceImagePath);
        using FileStream stream = File.OpenRead(sourceImagePath);
        DiscFileRecord exe = DiscImage.FindRootFileRecord(stream, layout, name =>
            name.StartsWith("SCUS", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("SCES", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("SCPS", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("SLUS", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("SLES", StringComparison.OrdinalIgnoreCase));

        long hookFileOffset = 0x800 + ((long)OriginalHookCallTarget - ExeDestination);
        long payloadFileOffset = 0x800 + ((long)EntryHookPayloadAddress - ExeDestination);
        if (hookFileOffset < 0x800 || payloadFileOffset < 0x800 || payloadFileOffset + PayloadReserveBytes > exe.Size)
            throw new InvalidOperationException("Spring Chest entry-hook payload cave is outside the executable body.");

        byte[] existingHookBytes = DiscImage.ReadFileBytes(stream, layout, exe.Lba, hookFileOffset, 8);
        uint originalFirstWord = BitConverter.ToUInt32(existingHookBytes, 0);
        uint originalSecondWord = BitConverter.ToUInt32(existingHookBytes, 4);
        if (originalFirstWord != 0x27BDFFE0 || originalSecondWord != 0xAFBF0018)
            throw new InvalidOperationException($"Spring Chest entry-hook expected function prologue 0x27BDFFE0 0xAFBF0018, got 0x{originalFirstWord:X8} 0x{originalSecondWord:X8}.");

        byte[] existingPayloadBytes = DiscImage.ReadFileBytes(stream, layout, exe.Lba, payloadFileOffset, PayloadReserveBytes);
        if (existingPayloadBytes.Any(value => value != 0))
            throw new InvalidOperationException($"Spring Chest entry-hook payload cave 0x{EntryHookPayloadAddress:X8} is not blank.");

        byte[] hookBytes = new byte[8];
        Array.Copy(BitConverter.GetBytes(NewJWord(EntryHookPayloadAddress)), 0, hookBytes, 0, 4);

        byte[] payloadBlock = new byte[PayloadReserveBytes];
        byte[] payload = BuildSmallCaveEntryHookOnlyPayload(existingHookBytes);
        if (payload.Length > PayloadStateOffset)
            throw new InvalidOperationException($"Spring Chest entry-hook helper payload is {payload.Length} bytes and would overlap state offset 0x{PayloadStateOffset:X}.");
        Array.Copy(payload, payloadBlock, payload.Length);

        return
        [
            new MobySourcePatch(
                Label: $"{level.Key}-spring-chest-entry-hook-only-helper-hook",
                Kind: "spring-chest-entry-hook-only-helper-hook",
                LevelKey: level.Key,
                MobyLabel: "Spring Chest entry hook-only helper",
                TrueIndex: -1,
                RecordOffset: "exe-entry-hook",
                WadRelativeOffset: $"exe:0x{OriginalHookCallTarget:X8}",
                ImageOffset: $"0x{DiscImage.ConvertFileOffsetToImageOffset(layout, exe.Lba, hookFileOffset):X}",
                ByteLength: hookBytes.Length,
                BeforeHexPreview: ToHex(existingHookBytes),
                AfterHexPreview: ToHex(hookBytes),
                Description: $"Patch the original routine entry at 0x{OriginalHookCallTarget:X8} to trampoline through 0x{EntryHookPayloadAddress:X8} while preserving the normal caller return address."),
            new MobySourcePatch(
                Label: $"{level.Key}-spring-chest-entry-hook-only-helper-payload",
                Kind: "spring-chest-entry-hook-only-helper-payload",
                LevelKey: level.Key,
                MobyLabel: "Spring Chest entry hook-only helper",
                TrueIndex: -1,
                RecordOffset: "exe-entry-payload",
                WadRelativeOffset: $"exe:0x{EntryHookPayloadAddress:X8}",
                ImageOffset: $"0x{DiscImage.ConvertFileOffsetToImageOffset(layout, exe.Lba, payloadFileOffset):X}",
                ByteLength: payloadBlock.Length,
                BeforeHexPreview: ToHex(existingPayloadBytes),
                AfterHexPreview: ToHex(payloadBlock),
                Description: $"Inject a pass-through entry trampoline at 0x{EntryHookPayloadAddress:X8}; it runs the original first two instructions and jumps back without writing chest fields.")
        ];
    }

    private static IReadOnlyList<MobySourcePatch> BuildSmallCavePassThroughHookPatches(
        string sourceImagePath,
        LevelDefinition level,
        string kindPrefix,
        string labelSuffix,
        string descriptionSuffix,
        byte[] payload)
    {
        if (!TryGetLevelPointer(level.Key, out _))
            return [];

        DiscLayout layout = DiscImage.DetectLayout(sourceImagePath);
        using FileStream stream = File.OpenRead(sourceImagePath);
        DiscFileRecord exe = DiscImage.FindRootFileRecord(stream, layout, name =>
            name.StartsWith("SCUS", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("SCES", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("SCPS", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("SLUS", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("SLES", StringComparison.OrdinalIgnoreCase));

        long hookFileOffset = 0x800 + ((long)HookAddress - ExeDestination);
        long payloadFileOffset = 0x800 + ((long)PayloadAddress - ExeDestination);
        if (hookFileOffset < 0x800 || payloadFileOffset < 0x800 || payloadFileOffset + PayloadReserveBytes > exe.Size)
            throw new InvalidOperationException("Spring Chest helper code cave is outside the executable body.");

        byte[] existingHookBytes = DiscImage.ReadFileBytes(stream, layout, exe.Lba, hookFileOffset, 4);
        uint expectedHookWord = NewJalWord(OriginalHookCallTarget);
        uint originalHookWord = BitConverter.ToUInt32(existingHookBytes, 0);
        if (originalHookWord != expectedHookWord)
            throw new InvalidOperationException($"Spring Chest hook-only helper hook site expected 0x{expectedHookWord:X8}, got 0x{originalHookWord:X8}.");

        byte[] existingPayloadBytes = DiscImage.ReadFileBytes(stream, layout, exe.Lba, payloadFileOffset, PayloadReserveBytes);
        if (existingPayloadBytes.Any(value => value != 0))
            throw new InvalidOperationException($"Spring Chest hook-only helper payload cave 0x{PayloadAddress:X8} is not blank.");

        byte[] hookBytes = BitConverter.GetBytes(NewJalWord(PayloadAddress));
        byte[] payloadBlock = new byte[PayloadReserveBytes];
        if (payload.Length > PayloadStateOffset)
            throw new InvalidOperationException($"Spring Chest hook-only helper payload is {payload.Length} bytes and would overlap state offset 0x{PayloadStateOffset:X}.");
        Array.Copy(payload, payloadBlock, payload.Length);

        return
        [
            new MobySourcePatch(
                Label: $"{level.Key}-{kindPrefix}-hook",
                Kind: $"{kindPrefix}-hook",
                LevelKey: level.Key,
                MobyLabel: $"Spring Chest {labelSuffix}",
                TrueIndex: -1,
                RecordOffset: "exe-hook",
                WadRelativeOffset: $"exe:0x{HookAddress:X8}",
                ImageOffset: $"0x{DiscImage.ConvertFileOffsetToImageOffset(layout, exe.Lba, hookFileOffset):X}",
                ByteLength: hookBytes.Length,
                BeforeHexPreview: ToHex(existingHookBytes),
                AfterHexPreview: ToHex(hookBytes),
                Description: $"Patch the main loop hook at 0x{HookAddress:X8} so Stone Hill can run a pass-through Spring Chest {labelSuffix}."),
            new MobySourcePatch(
                Label: $"{level.Key}-{kindPrefix}-payload",
                Kind: $"{kindPrefix}-payload",
                LevelKey: level.Key,
                MobyLabel: $"Spring Chest {labelSuffix}",
                TrueIndex: -1,
                RecordOffset: "exe-payload",
                WadRelativeOffset: $"exe:0x{PayloadAddress:X8}",
                ImageOffset: $"0x{DiscImage.ConvertFileOffsetToImageOffset(layout, exe.Lba, payloadFileOffset):X}",
                ByteLength: payloadBlock.Length,
                BeforeHexPreview: ToHex(existingPayloadBytes),
                AfterHexPreview: ToHex(payloadBlock),
                Description: $"Inject a pass-through Spring Chest helper payload at 0x{PayloadAddress:X8}; {descriptionSuffix}")
        ];
    }

    private static bool TryGetLevelPointer(string levelKey, out uint levelPointer)
    {
        string key = LevelCatalog.NormalizeKey(levelKey);
        if (string.Equals(key, "stonehill", StringComparison.OrdinalIgnoreCase))
        {
            levelPointer = 0x80173658;
            return true;
        }

        if (string.Equals(key, "artisans", StringComparison.OrdinalIgnoreCase))
        {
            levelPointer = 0x8016D3E8;
            return true;
        }

        levelPointer = 0;
        return false;
    }

    private static byte[] BuildSmallCaveMinimalPopPayload(uint levelPointer, int controllerIndex, int shellIndex, int spawnGemIndex, int controllerActorId, int rewardGemIdByte, int runtimeVariantId, bool refreshRewardUntilListed, bool clearRewardRuntimeWord, int fallbackCleanupFrames, bool useLooseGemRowShape, bool useVisibleGemTypeWord, int rewardZOffset, bool preservePreinitializedRewardRow, int uncollectedReturnFrames, int rewardReturnDropPerFrame, int rewardReturnDropStartFrames, int rewardRiseFrames, int rewardRisePerFrame, bool maskAirborneRewardValue, bool rearmOnEarlyRewardPickup, bool unlinkAirborneRewardFromActiveList, bool parkReturnedRewardAsInert, bool monitorReturnedRewardPickupList, bool restoreReturnedRewardOnHit, bool fullQuarantineReturnedReward, bool delayReturnedRewardCollection, bool delayReturnedRewardTypeWord, bool scriptRewardMotionWithoutPickupList, bool blankReturnedRewardRow, bool preserveRewardVisualScaffold, bool restoreDonorGemShapeOnPop, bool leaveDonorGemPickupTypeBlankOnPop, bool leaveDonorGemTailWordBlankOnPop, bool useNativeStateZeroShellWords, bool useNativeReadyShellWords, bool useNativePreHitShellLifecycleWords, bool useNativeSpringEffectRow, bool useBlueGemNativeSpringEffectVisual, bool useBlueGemNativeSpringEffectOrdinal, bool manualSpyroTouchCollectNativeEffectVisual, bool manualAwardTreasureCounters, bool manualAwardNativeTreasureRoutine, bool manualAwardFixedTreasureWords, bool manualAnimateRewardPopArc)
    {
        uint controllerAddress = levelPointer + ((uint)controllerIndex * RecordStride);
        uint shellAddress = levelPointer + ((uint)shellIndex * RecordStride);
        uint spawnGemAddress = levelPointer + ((uint)spawnGemIndex * RecordStride);
        uint rewardOrdinal = (uint)(rewardGemIdByte - 0x52);
        uint shellArmedWord = ((uint)rewardGemIdByte << 24) | (useNativeReadyShellWords || useNativePreHitShellLifecycleWords ? 0x00FF0020u : useNativeStateZeroShellWords ? 0x00100020u : 0x00100120u);
        uint shellPoppedWord = ((uint)rewardGemIdByte << 24) | (useNativeReadyShellWords || useNativePreHitShellLifecycleWords ? 0x00100020u : useNativeStateZeroShellWords ? 0x00400020u : 0x00400120u);
        uint stateAddress = PayloadAddress + PayloadStateOffset;

        MipsEmitter asm = new(PayloadAddress);
        string[] minimalSave = ["v0", "v1", "a0", "a1", "t0", "t1", "t2", "t3", "t4", "t5", "t6", "t7"];

        asm.Addiu("sp", "sp", -0x50);
        asm.Sw("ra", 0, "sp");
        asm.Jal(OriginalHookCallTarget);
        asm.Nop();
        for (int i = 0; i < minimalSave.Length; i++)
            asm.Sw(minimalSave[i], 4 + (i * 4), "sp");

        asm.Li("t0", 0x80075828);
        asm.Lw("t1", 0, "t0");
        asm.Li("t2", levelPointer);
        asm.Bne("t1", "t2", "minimal_done");
        asm.Li("t0", controllerAddress);
        asm.Lhu("t1", 0x36, "t0");
        asm.Li("t2", (uint)controllerActorId);
        asm.Bne("t1", "t2", "minimal_done");
        asm.Li("t3", shellAddress);
        asm.Lhu("t1", 0x36, "t3");
        asm.Li("t2", 0x0149);
        asm.Bne("t1", "t2", "minimal_done");
        asm.Li("t7", stateAddress);
        asm.Lw("t4", 0, "t7");
        asm.Li("t5", 2);
        asm.Beq("t4", "t5", manualAwardTreasureCounters ? "minimal_hold_award" : monitorReturnedRewardPickupList ? "minimal_post_return_unlink" : "minimal_done");
        asm.Li("t5", 1);
        asm.Beq("t4", "t5", "minimal_wait_collect");
        asm.Nop();

        asm.L("minimal_arm_start");
        asm.Li("t2", (uint)runtimeVariantId);
        asm.Sh("t2", 0x34, "t0");
        asm.Li("t1", controllerAddress);
        asm.Sw("t1", 0x04, "t3");
        asm.Sh("t2", 0x34, "t3");
        asm.Li("t2", shellArmedWord);
        asm.Sw("t2", 0x50, "t3");
        asm.Lw("t4", 0x18, "t0");
        asm.Lw("t5", 0x18, "t3");
        asm.OrReg("t4", "t4", "t5");
        asm.Beq("t4", "zero", "minimal_done");
        asm.Li("t5", 0);
        asm.L("minimal_write_spawn");
        asm.Li("t6", spawnGemAddress);
        asm.Sw("zero", 0x04, "t6");
        asm.Sw("zero", 0x08, "t6");
        asm.Lw("t1", 0x0C, "t3");
        asm.Sw("t1", 0x0C, "t6");
        asm.Lw("t1", 0x10, "t3");
        asm.Sw("t1", 0x10, "t6");
        asm.Lw("t1", 0x14, "t3");
        asm.Addiu("t1", "t1", rewardZOffset);
        asm.Sw("t1", 0x14, "t6");
        if (!preservePreinitializedRewardRow)
        {
            asm.Sw("zero", 0x18, "t6");
            if (clearRewardRuntimeWord)
                asm.Sw("zero", 0x1C, "t6");
            if (useNativeSpringEffectRow)
            {
                asm.Li("t1", useBlueGemNativeSpringEffectVisual ? 0x0055FFFFu : 0x0022FFFFu);
                asm.Sw("t1", 0x34, "t6");
                asm.Sw("zero", 0x38, "t6");
                asm.Sw("zero", 0x3C, "t6");
                asm.Li("t1", 0x00FF0000);
                asm.Sw("t1", 0x48, "t6");
                if (useBlueGemNativeSpringEffectOrdinal)
                {
                    asm.Li("t1", rewardOrdinal << 24);
                    asm.Sw("t1", 0x4C, "t6");
                }
                else
                {
                    asm.Sw("zero", 0x4C, "t6");
                }
                asm.Li("t1", 0xFFFF0020);
                asm.Sw("t1", 0x50, "t6");
                asm.Li("t1", 0x002C007F);
                asm.Sw("t1", 0x54, "t6");
            }
            else if (restoreDonorGemShapeOnPop)
            {
                asm.Li("t1", 0x80000000u);
                asm.Sw("t1", 0x1C, "t6");
                asm.Sh("zero", 0x34, "t6");
                asm.Li("t1", (uint)rewardGemIdByte);
                asm.Sh("t1", 0x36, "t6");
                asm.Li("t1", 0x7D);
                asm.Sb("t1", 0x3A, "t6");
                asm.Li("t1", 0x00FF0000);
                asm.Sw("t1", 0x48, "t6");
                asm.Li("t1", rewardOrdinal << 24);
                asm.Sw("t1", 0x4C, "t6");
                if (!leaveDonorGemPickupTypeBlankOnPop)
                {
                    asm.Li("t1", 0xFF400018u);
                    asm.Sw("t1", 0x50, "t6");
                    if (!leaveDonorGemTailWordBlankOnPop)
                    {
                        asm.Li("t1", 0x0000107F);
                        asm.Sw("t1", 0x54, "t6");
                    }
                }
            }
            else
            {
                asm.Li("t1", 0xFFFF);
                asm.Sh("t1", 0x34, "t6");
                asm.Li("t1", (uint)rewardGemIdByte);
                asm.Sh("t1", 0x36, "t6");
                asm.Sb("zero", 0x3A, "t6");
                asm.Sb("zero", 0x48, "t6");
                asm.Li("t1", useLooseGemRowShape ? 0u : 1u);
                asm.Sb("t1", 0x49, "t6");
                asm.Li("t1", useLooseGemRowShape ? 1u : 0x18u);
                asm.Sb("t1", 0x4A, "t6");
                asm.Sb("zero", 0x4B, "t6");
                asm.Sb("zero", 0x4C, "t6");
                asm.Sb("zero", 0x4D, "t6");
                asm.Sb("zero", 0x4E, "t6");
                asm.Li("t1", rewardOrdinal);
                asm.Sb("t1", 0x4F, "t6");
                asm.Li("t1", useVisibleGemTypeWord ? 0xFF400118u : useLooseGemRowShape ? 0xFF400020u : 0xFF400118u);
                asm.Sw("t1", 0x50, "t6");
                asm.Li("t1", 0x0000107F);
                asm.Sw("t1", 0x54, "t6");
            }
        }
        else if (parkReturnedRewardAsInert && (!monitorReturnedRewardPickupList || restoreReturnedRewardOnHit))
        {
            asm.Li("t1", (uint)rewardGemIdByte);
            asm.Sh("t1", 0x36, "t6");
            if (!restoreReturnedRewardOnHit || (fullQuarantineReturnedReward && !delayReturnedRewardCollection))
            {
                asm.Li("t1", rewardOrdinal);
                asm.Sb("t1", 0x4F, "t6");
            }
            if ((!restoreReturnedRewardOnHit || fullQuarantineReturnedReward) && !delayReturnedRewardTypeWord)
            {
                asm.Li("t1", useVisibleGemTypeWord ? 0xFF400118u : 0xFF400020u);
                asm.Sw("t1", 0x50, "t6");
            }
        }
        asm.Bne("t5", "zero", "minimal_done");
        asm.Li("t1", 0xFD);

        if (maskAirborneRewardValue || delayReturnedRewardCollection)
            asm.Sb("zero", 0x4F, "t6");
        if (delayReturnedRewardTypeWord)
            asm.Sw("zero", 0x50, "t6");

        if (useNativePreHitShellLifecycleWords)
        {
            asm.Li("t1", 0x007D0000);
            asm.Sw("t1", 0x38, "t3");
            asm.Li("t1", 0x90FF0100u);
            asm.Sw("t1", 0x48, "t3");
            asm.Li("t1", 0x00A00030);
            asm.Sw("t1", 0x4C, "t3");
            asm.Li("t1", 0x0000207F);
            asm.Sw("t1", 0x54, "t3");
        }
        else
        {
            asm.Sb("t1", 0x3A, "t3");
            asm.Li("t1", 1);
            asm.Sb("t1", 0x3C, "t3");
            asm.Sb("t1", 0x3D, "t3");
            asm.Sb("t1", 0x3F, "t3");
            asm.Sb("t1", 0x48, "t3");
        }
        asm.Li("t1", shellPoppedWord);
        asm.Sw("t1", 0x50, "t3");
        asm.Sw("zero", 0x18, "t3");
        asm.Sw("zero", 0x18, "t0");
        asm.Li("t1", 1);
        asm.Sw("t1", 0, "t7");
        asm.Sw("zero", 4, "t7");
        if (!scriptRewardMotionWithoutPickupList)
            asm.Sw("zero", 8, "t7");
        asm.JmpLabel("minimal_done");
        asm.Nop();

        asm.L("minimal_wait_collect");
        asm.Li("t6", spawnGemAddress);
        asm.Li("t1", 0x80072000);
        asm.Addiu("t2", "t1", 0x400);
        asm.L("minimal_scan_loop");
        asm.Lw("t4", 0, "t1");
        asm.Beq("t4", "t6", "minimal_gem_listed");
        asm.Addiu("t1", "t1", 4);
        asm.Sltu("t5", "t1", "t2");
        asm.Bne("t5", "zero", "minimal_scan_loop");
        if ((scriptRewardMotionWithoutPickupList && uncollectedReturnFrames > 0) || manualSpyroTouchCollectNativeEffectVisual)
        {
            asm.JmpLabel("minimal_begin_reward_motion");
            asm.Nop();
        }
        if (unlinkAirborneRewardFromActiveList)
        {
            asm.Lw("t4", 8, "t7");
            asm.Bne("t4", "zero", "minimal_gem_already_unlinked");
            asm.Nop();
        }
        asm.Lw("t4", 8, "t7");
        if (refreshRewardUntilListed)
        {
            asm.Beq("t4", "zero", "minimal_refresh_reward");
            asm.Nop();
        }
        else if (fallbackCleanupFrames > 0)
        {
            if (rearmOnEarlyRewardPickup && uncollectedReturnFrames > 0)
            {
                asm.Beq("t4", "zero", "minimal_fallback_not_listed");
                asm.Nop();
                asm.Lw("t4", 4, "t7");
                asm.Li("t5", (uint)uncollectedReturnFrames);
                asm.Sltu("t5", "t4", "t5");
                asm.Bne("t5", "zero", "minimal_return_reward");
                asm.Nop();
                asm.JmpLabel("minimal_cleanup_shell");
                asm.Nop();
                asm.L("minimal_fallback_not_listed");
            }
            else
            {
                asm.Bne("t4", "zero", "minimal_cleanup_shell");
                asm.Nop();
            }

            asm.Lw("t4", 4, "t7");
            asm.Addiu("t4", "t4", 1);
            asm.Sw("t4", 4, "t7");
            asm.Li("t5", (uint)fallbackCleanupFrames);
            asm.Sltu("t5", "t4", "t5");
            asm.Bne("t5", "zero", "minimal_done");
            asm.Nop();
            asm.JmpLabel("minimal_cleanup_shell");
            asm.Nop();
        }
        else
        {
            asm.Beq("t4", "zero", "minimal_done");
            asm.Nop();
        }

        asm.L("minimal_cleanup_shell");
        asm.Lw("t1", 0x0C, "t3");
        asm.Lw("t2", 0x10, "t3");
        asm.Lui("t4", 0x0020);
        asm.Addu("t1", "t1", "t4");
        asm.Addu("t2", "t2", "t4");
        asm.Sw("t1", 0x0C, "t3");
        asm.Sw("t2", 0x10, "t3");
        asm.Sw("zero", 0x14, "t3");
        asm.Sw("zero", 0x18, "t3");
        asm.Sw("t1", 0x0C, "t0");
        asm.Sw("t2", 0x10, "t0");
        asm.Sw("zero", 0x14, "t0");
        asm.Sw("zero", 0x18, "t0");
        if (manualAwardNativeTreasureRoutine)
        {
            asm.Li("a0", (uint)(GemValueForRewardByte(rewardGemIdByte) << 16));
            asm.Li("a1", spawnGemAddress);
            asm.Jal(AwardTreasureRoutineAddress);
            asm.Nop();
            asm.Li("t7", stateAddress);
        }
        if (manualAwardTreasureCounters)
        {
            EmitAddFiveToHalfwordAndStore(asm, TotalJewelsAddress, 8);
            EmitAddFiveToHalfwordAndStore(asm, InventoryTotalTreasureAddress, 12);
        }
        if (manualAwardFixedTreasureWords)
        {
            EmitAddFixedTreasureWords(asm, GemValueForRewardByte(rewardGemIdByte));
        }
        if (manualSpyroTouchCollectNativeEffectVisual)
        {
            asm.Li("t6", spawnGemAddress);
            asm.Sw("zero", 0x14, "t6");
            asm.Sw("zero", 0x34, "t6");
            asm.Sw("zero", 0x48, "t6");
            asm.Sw("zero", 0x4C, "t6");
            asm.Sw("zero", 0x50, "t6");
            asm.Sw("zero", 0x54, "t6");
        }
        asm.Li("t1", 2);
        asm.Sw("t1", 0, "t7");
        asm.JmpLabel("minimal_done");
        asm.Nop();

        if (manualAwardTreasureCounters)
        {
            asm.L("minimal_hold_award");
            EmitWriteStoredHalfword(asm, TotalJewelsAddress, 8);
            EmitWriteStoredHalfword(asm, InventoryTotalTreasureAddress, 12);
            asm.JmpLabel("minimal_done");
            asm.Nop();
        }

        if (refreshRewardUntilListed)
        {
            asm.L("minimal_refresh_reward");
            asm.Li("t5", 1);
            asm.JmpLabel("minimal_write_spawn");
            asm.Nop();
        }

        asm.L("minimal_gem_listed");
        if (unlinkAirborneRewardFromActiveList)
        {
            asm.Sw("zero", -4, "t1");
            asm.L("minimal_gem_already_unlinked");
        }
        asm.L("minimal_begin_reward_motion");
        if (manualSpyroTouchCollectNativeEffectVisual)
        {
            asm.Lw("t4", 4, "t7");
            asm.Addiu("t4", "t4", 1);
            asm.Sw("t4", 4, "t7");
            if (manualAnimateRewardPopArc)
            {
                EmitManualRewardPopArc(asm, spawnGemAddress, rewardZOffset);
                asm.Li("t5", 48u);
                asm.Sltu("t5", "t4", "t5");
                asm.Bne("t5", "zero", "minimal_manual_collect_window");
                asm.Nop();
                asm.Sw("zero", 0x50, "t6");
                asm.Sw("zero", 0x3C, "t3");
                asm.Sw("zero", 0x18, "t3");
                asm.JmpLabel("minimal_done");
                asm.Sw("zero", 0, "t7");
            }
            if (manualAnimateRewardPopArc)
                asm.L("minimal_manual_collect_window");
            EmitCompactSpyroTouchCollectCheck(asm, spawnGemAddress, "minimal_cleanup_shell", "minimal_after_motion_touch_check");
        }
        if (manualSpyroTouchCollectNativeEffectVisual)
            asm.L("minimal_after_motion_touch_check");
        if (!scriptRewardMotionWithoutPickupList)
        {
            asm.Li("t4", 1);
            asm.Sw("t4", 8, "t7");
        }
        if (uncollectedReturnFrames > 0)
        {
            asm.Lw("t4", 4, "t7");
            asm.Addiu("t4", "t4", 1);
            asm.Sw("t4", 4, "t7");
            if (rewardRiseFrames > 0 && rewardRisePerFrame > 0)
            {
                asm.Li("t5", (uint)rewardRiseFrames);
                asm.Sltu("t5", "t4", "t5");
                asm.Beq("t5", "zero", "minimal_after_rise_arc");
                asm.Li("t6", spawnGemAddress);
                asm.Lw("t1", 0x14, "t6");
                asm.Addiu("t1", "t1", rewardRisePerFrame);
                asm.Sw("t1", 0x14, "t6");
                asm.JmpLabel("minimal_done");
                asm.Nop();
                asm.L("minimal_after_rise_arc");
                if (delayReturnedRewardCollection)
                {
                    asm.Li("t1", rewardOrdinal);
                    asm.Sb("t1", 0x4F, "t6");
                }
                if (delayReturnedRewardTypeWord)
                {
                    asm.Li("t1", useVisibleGemTypeWord ? 0xFF400118u : 0xFF400020u);
                    asm.Sw("t1", 0x50, "t6");
                }
            }

            if (rewardReturnDropPerFrame > 0)
            {
                if (rewardReturnDropStartFrames > 0)
                {
                    asm.Li("t5", (uint)rewardReturnDropStartFrames);
                    asm.Sltu("t5", "t4", "t5");
                    asm.Bne("t5", "zero", "minimal_check_return_timeout");
                    asm.Nop();
                }

                asm.Li("t6", spawnGemAddress);
                asm.Lw("t1", 0x14, "t6");
                asm.Addiu("t1", "t1", -rewardReturnDropPerFrame);
                asm.Sw("t1", 0x14, "t6");
            }

            asm.L("minimal_check_return_timeout");
            asm.Li("t5", (uint)uncollectedReturnFrames);
            asm.Sltu("t5", "t4", "t5");
            asm.Bne("t5", "zero", "minimal_done");
            asm.Nop();
            asm.JmpLabel("minimal_return_reward");
            asm.Nop();
        }

        if (uncollectedReturnFrames > 0)
        {
            asm.L("minimal_return_reward");
            asm.Li("t6", spawnGemAddress);
            if (maskAirborneRewardValue)
            {
                asm.Li("t1", rewardOrdinal);
                asm.Sb("t1", 0x4F, "t6");
            }
            asm.Sw("zero", 0x0C, "t6");
            asm.Sw("zero", 0x10, "t6");
            asm.Li("t1", 0xFFFF0000u);
            asm.Sw("t1", 0x14, "t6");
            asm.Sw("zero", 0x18, "t6");
            if (blankReturnedRewardRow)
            {
                if (!preserveRewardVisualScaffold)
                    asm.Sw("zero", 0x1C, "t6");
                asm.Sw("zero", 0x34, "t6");
                asm.Sw("zero", 0x38, "t6");
                asm.Sw("zero", 0x3C, "t6");
                asm.Sw("zero", 0x48, "t6");
                asm.Sw("zero", 0x4C, "t6");
                asm.Sw("zero", 0x50, "t6");
                asm.Sw("zero", 0x54, "t6");
            }
            else if (parkReturnedRewardAsInert)
            {
                asm.Sh("zero", 0x36, "t6");
                if (!restoreReturnedRewardOnHit || fullQuarantineReturnedReward)
                {
                    asm.Sb("zero", 0x4F, "t6");
                    asm.Sw("zero", 0x50, "t6");
                }
            }
            if (!useNativePreHitShellLifecycleWords)
            {
                asm.Sb("zero", 0x3A, "t3");
                asm.Sb("zero", 0x3C, "t3");
                asm.Sb("zero", 0x3D, "t3");
                asm.Sb("zero", 0x3F, "t3");
                asm.Sb("zero", 0x48, "t3");
            }
            asm.Li("t1", shellArmedWord);
            asm.Sw("t1", 0x50, "t3");
            asm.Sw("zero", 0x18, "t3");
            asm.Sw("zero", 0x18, "t0");
            if (monitorReturnedRewardPickupList)
            {
                asm.Li("t1", 2);
                asm.Sw("t1", 0, "t7");
            }
            else
            {
                asm.Sw("zero", 0, "t7");
            }
            if (!monitorReturnedRewardPickupList)
            {
                asm.Sw("zero", 4, "t7");
                if (!scriptRewardMotionWithoutPickupList)
                    asm.Sw("zero", 8, "t7");
            }
        }

        if (monitorReturnedRewardPickupList)
        {
            asm.L("minimal_post_return_unlink");
            asm.Li("t6", spawnGemAddress);
            asm.Li("t1", 0x80072000);
            asm.Addiu("t2", "t1", 0x400);
            asm.L("minimal_post_return_scan");
            asm.Lw("t4", 0, "t1");
            asm.Bne("t4", "t6", "minimal_post_return_next");
            asm.Addiu("t1", "t1", 4);
            asm.Sw("zero", -4, "t1");
            asm.L("minimal_post_return_next");
            asm.Sltu("t5", "t1", "t2");
            asm.Bne("t5", "zero", "minimal_post_return_scan");
            asm.Nop();
            asm.JmpLabel("minimal_arm_start");
            asm.Nop();
        }

        asm.L("minimal_done");
        for (int i = 0; i < minimalSave.Length; i++)
            asm.Lw(minimalSave[i], 4 + (i * 4), "sp");
        asm.Lw("ra", 0, "sp");
        asm.Addiu("sp", "sp", 0x50);
        asm.Jr("ra");
        asm.Nop();

        return asm.ToBytes();
    }

    private static void EmitCompactSpyroTouchCollectCheck(MipsEmitter asm, uint spawnGemAddress, string collectLabel, string continueLabel)
    {
        const uint spyroZAddress = 0x80078A60;
        const int zThreshold = 0x0180;

        _ = spawnGemAddress;
        asm.Lw("t2", 0x14, "t6");
        asm.Addiu("t2", "t2", -zThreshold);
        asm.Li("t1", spyroZAddress);
        asm.Lw("t4", 0, "t1");
        asm.Slt("t5", "t4", "t2");
        asm.Bne("t5", "zero", continueLabel);
        asm.Nop();
        asm.JmpLabel(collectLabel);
        asm.Nop();
    }

    private static void EmitAddFiveToHalfword(MipsEmitter asm, uint address)
    {
        asm.Li("t6", address);
        asm.Lhu("t1", 0, "t6");
        asm.Addiu("t1", "t1", 5);
        asm.Sh("t1", 0, "t6");
    }

    private static int GemValueForRewardByte(int rewardGemIdByte) => rewardGemIdByte switch
    {
        0x53 => 1,
        0x54 => 2,
        0x55 => 5,
        0x56 => 10,
        0x57 => 25,
        _ => 1
    };

    private static void EmitAddFiveToHalfwordAndStore(MipsEmitter asm, uint address, int stateOffset)
    {
        asm.Li("t6", address);
        asm.Lhu("t1", 0, "t6");
        asm.Addiu("t1", "t1", 5);
        asm.Sh("t1", 0, "t6");
        asm.Sw("t1", stateOffset, "t7");
    }

    private static void EmitWriteStoredHalfword(MipsEmitter asm, uint address, int stateOffset)
    {
        asm.Lw("t1", stateOffset, "t7");
        asm.Li("t6", address);
        asm.Sh("t1", 0, "t6");
    }

    private static void EmitAddFixedTreasureWords(MipsEmitter asm, int gemValue)
    {
        uint fixedValue = (uint)gemValue;

        asm.Li("t1", TotalJewelsAddress);
        asm.Lw("t2", 0, "t1");
        asm.Li("t3", fixedValue);
        asm.Addu("t2", "t2", "t3");
        asm.Sw("t2", 0, "t1");

        asm.Li("t1", CurrentLevelTreasureIndexAddress);
        asm.Lw("t2", 0, "t1");
        asm.Addu("t2", "t2", "t2");
        asm.Addu("t2", "t2", "t2");
        asm.Li("t1", LevelTreasureTableAddress);
        asm.Addu("t1", "t1", "t2");
        asm.Lw("t2", 0, "t1");
        asm.Li("t3", fixedValue);
        asm.Addu("t2", "t2", "t3");
        asm.Sw("t2", 0, "t1");
    }

    private static void EmitManualRewardPopArc(MipsEmitter asm, uint spawnGemAddress, int rewardZOffset)
    {
        _ = spawnGemAddress;
        asm.Li("t5", 24u);
        asm.Sltu("t5", "t4", "t5");
        asm.Bne("t5", "zero", "minimal_manual_arc_rise");
        asm.Li("t5", 48u);
        asm.Subu("t5", "t5", "t4");
        asm.JmpLabel("minimal_manual_arc_apply");
        asm.Sll("t5", "t5", 6);
        asm.L("minimal_manual_arc_rise");
        asm.Sll("t5", "t4", 6);
        asm.L("minimal_manual_arc_apply");
        asm.Lw("t1", 0x14, "t3");
        asm.Addiu("t1", "t1", rewardZOffset);
        asm.Addu("t1", "t1", "t5");
        asm.Sw("t1", 0x14, "t6");
    }

    private static byte[] BuildSmallCaveArmOnlyPayload(uint levelPointer, int controllerIndex, int shellIndex, int controllerActorId, int rewardGemIdByte, int runtimeVariantId)
    {
        uint controllerAddress = levelPointer + ((uint)controllerIndex * RecordStride);
        uint shellAddress = levelPointer + ((uint)shellIndex * RecordStride);
        uint shellArmedWord = ((uint)rewardGemIdByte << 24) | 0x00100120u;
        uint stateAddress = PayloadAddress + PayloadStateOffset;

        MipsEmitter asm = new(PayloadAddress);
        string[] minimalSave = ["v0", "v1", "a0", "a1", "t0", "t1", "t2", "t3", "t4", "t5", "t6", "t7"];

        asm.Li("at", ScratchSaveAddress);
        asm.Sw("ra", 0, "at");
        asm.Jal(OriginalHookCallTarget);
        asm.Nop();
        asm.Li("at", ScratchSaveAddress);
        for (int i = 0; i < minimalSave.Length; i++)
            asm.Sw(minimalSave[i], 4 + (i * 4), "at");

        asm.Li("t0", 0x80075828);
        asm.Lw("t1", 0, "t0");
        asm.Li("t2", levelPointer);
        asm.Bne("t1", "t2", "arm_done");
        asm.Nop();

        asm.Li("t0", controllerAddress);
        asm.Lhu("t1", 0x36, "t0");
        asm.Li("t2", (uint)controllerActorId);
        asm.Bne("t1", "t2", "arm_done");
        asm.Nop();
        asm.Li("t3", shellAddress);
        asm.Lhu("t1", 0x36, "t3");
        asm.Li("t2", 0x0149);
        asm.Bne("t1", "t2", "arm_done");
        asm.Nop();

        asm.Li("t7", stateAddress);
        asm.Lw("t4", 0, "t7");
        asm.Bne("t4", "zero", "arm_done");
        asm.Nop();

        asm.Li("t1", levelPointer);
        asm.Sw("t1", 0x04, "t0");
        asm.Li("t2", (uint)runtimeVariantId);
        asm.Sh("t2", 0x34, "t0");
        asm.Li("t2", 0x53100020);
        asm.Sw("t2", 0x50, "t0");
        asm.Li("t1", controllerAddress);
        asm.Sw("t1", 0x04, "t3");
        asm.Li("t2", (uint)runtimeVariantId);
        asm.Sh("t2", 0x34, "t3");
        asm.Li("t2", shellArmedWord);
        asm.Sw("t2", 0x50, "t3");
        asm.Li("t1", 1);
        asm.Sw("t1", 0, "t7");

        asm.L("arm_done");
        asm.Li("at", ScratchSaveAddress);
        for (int i = 0; i < minimalSave.Length; i++)
            asm.Lw(minimalSave[i], 4 + (i * 4), "at");
        asm.Lw("ra", 0, "at");
        asm.Jr("ra");
        asm.Nop();

        return asm.ToBytes();
    }

    private static byte[] BuildSmallCaveRowRepairPayload(uint levelPointer, int firstIndex, int secondIndex, int firstRuntimeVariantId, int secondRuntimeVariantId)
    {
        uint firstAddress = levelPointer + ((uint)firstIndex * RecordStride);
        uint secondAddress = levelPointer + ((uint)secondIndex * RecordStride);
        uint stateAddress = PayloadAddress + PayloadStateOffset;

        MipsEmitter asm = new(PayloadAddress);
        string[] saved = ["v0", "v1", "a0", "a1", "t0", "t1", "t2", "t3", "t4"];

        asm.Li("at", ScratchSaveAddress);
        asm.Sw("ra", 0, "at");
        asm.Jal(OriginalHookCallTarget);
        asm.Nop();
        asm.Li("at", ScratchSaveAddress);
        for (int i = 0; i < saved.Length; i++)
            asm.Sw(saved[i], 4 + (i * 4), "at");

        asm.Li("t0", 0x80075828);
        asm.Lw("t1", 0, "t0");
        asm.Li("t2", levelPointer);
        asm.Beq("t1", "t2", "repair_in_stonehill");
        asm.Nop();
        asm.Li("t4", stateAddress);
        asm.Sw("zero", 0, "t4");
        asm.JmpLabel("repair_done");
        asm.Nop();

        asm.L("repair_in_stonehill");
        asm.Li("t4", stateAddress);
        asm.Lw("t1", 0, "t4");
        asm.Bne("t1", "zero", "repair_done");
        asm.Nop();

        asm.Li("t0", firstAddress);
        asm.Lhu("t1", 0x36, "t0");
        asm.Li("t2", 0x0149);
        asm.Bne("t1", "t2", "repair_done");
        asm.Nop();
        asm.Li("t3", secondAddress);
        asm.Lhu("t1", 0x36, "t3");
        asm.Bne("t1", "t2", "repair_done");
        asm.Nop();

        asm.Li("t1", (uint)firstRuntimeVariantId);
        asm.Sh("t1", 0x34, "t0");
        asm.Li("t1", (uint)secondRuntimeVariantId);
        asm.Sh("t1", 0x34, "t3");
        asm.Sb("zero", 0x51, "t0");
        asm.Sb("zero", 0x51, "t3");
        asm.Li("t1", 0x01);
        asm.Sb("t1", 0x49, "t0");
        asm.Sb("t1", 0x49, "t3");
        asm.Li("t1", 0x10);
        asm.Sb("t1", 0x52, "t0");
        asm.Sb("t1", 0x52, "t3");
        asm.Li("t1", 0x30);
        asm.Sb("t1", 0x4C, "t0");
        asm.Sb("t1", 0x4C, "t3");
        asm.Sb("zero", 0x4D, "t0");
        asm.Sb("zero", 0x4D, "t3");
        asm.Li("t1", 0x20);
        asm.Sb("t1", 0x55, "t0");
        asm.Sb("t1", 0x55, "t3");
        asm.Li("t1", 1);
        asm.Sw("t1", 0, "t4");

        asm.L("repair_done");
        asm.Li("at", ScratchSaveAddress);
        for (int i = 0; i < saved.Length; i++)
            asm.Lw(saved[i], 4 + (i * 4), "at");
        asm.Lw("ra", 0, "at");
        asm.Jr("ra");
        asm.Nop();

        return asm.ToBytes();
    }

    private static byte[] BuildSmallCaveHookOnlyPayload()
    {
        MipsEmitter asm = new(PayloadAddress);

        asm.Li("at", ScratchSaveAddress);
        asm.Sw("ra", 0, "at");
        asm.Jal(OriginalHookCallTarget);
        asm.Nop();
        asm.Li("at", ScratchSaveAddress);
        asm.Lw("ra", 0, "at");
        asm.Jr("ra");
        asm.Nop();

        return asm.ToBytes();
    }

    private static byte[] BuildSmallCaveStackHookOnlyPayload()
    {
        MipsEmitter asm = new(PayloadAddress);

        asm.Addiu("sp", "sp", -0x10);
        asm.Sw("ra", 0, "sp");
        asm.Jal(OriginalHookCallTarget);
        asm.Nop();
        asm.Lw("ra", 0, "sp");
        asm.Addiu("sp", "sp", 0x10);
        asm.Jr("ra");
        asm.Nop();

        return asm.ToBytes();
    }

    private static byte[] BuildSmallCaveEntryHookOnlyPayload(byte[] originalEntryBytes)
    {
        byte[] payload = new byte[16];
        Array.Copy(originalEntryBytes, 0, payload, 0, 8);
        Array.Copy(BitConverter.GetBytes(NewJWord(OriginalHookCallTarget + 8)), 0, payload, 8, 4);
        return payload;
    }

    private static uint NewJWord(uint address) => (2u << 26) | ((address >> 2) & 0x03FFFFFFu);

    private static uint NewJalWord(uint address) => (3u << 26) | ((address >> 2) & 0x03FFFFFFu);

    private static string ToHex(byte[] bytes) => string.Join(' ', bytes.Select(value => value.ToString("X2")));

    private sealed class MipsEmitter
    {
        private static readonly IReadOnlyDictionary<string, int> Registers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["zero"] = 0,
            ["at"] = 1,
            ["v0"] = 2,
            ["v1"] = 3,
            ["a0"] = 4,
            ["a1"] = 5,
            ["a2"] = 6,
            ["a3"] = 7,
            ["t0"] = 8,
            ["t1"] = 9,
            ["t2"] = 10,
            ["t3"] = 11,
            ["t4"] = 12,
            ["t5"] = 13,
            ["t6"] = 14,
            ["t7"] = 15,
            ["t8"] = 24,
            ["t9"] = 25,
            ["sp"] = 29,
            ["ra"] = 31
        };

        private readonly uint baseAddress;
        private readonly List<uint> words = [];
        private readonly Dictionary<string, int> labels = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<Fixup> fixups = [];

        public MipsEmitter(uint baseAddress)
        {
            this.baseAddress = baseAddress;
        }

        public void L(string name) => labels[name] = words.Count;
        public void Nop() => words.Add(0);
        public void Addiu(string rt, string rs, int imm) => EmitIType(9, rs, rt, imm);
        public void Lui(string rt, int imm) => EmitIType(15, "zero", rt, imm);
        public void Ori(string rt, string rs, long imm) => EmitIType(13, rs, rt, imm);
        public void Lw(string rt, int offset, string rs)
        {
            EmitIType(35, rs, rt, offset);
            Nop();
        }

        public void Sw(string rt, int offset, string rs) => EmitIType(43, rs, rt, offset);
        public void Lhu(string rt, int offset, string rs)
        {
            EmitIType(37, rs, rt, offset);
            Nop();
        }

        public void Sh(string rt, int offset, string rs) => EmitIType(41, rs, rt, offset);
        public void Sb(string rt, int offset, string rs) => EmitIType(40, rs, rt, offset);
        public void Sll(string rd, string rt, int shamt) => EmitRType("zero", rt, rd, shamt, 0);
        public void Addu(string rd, string rs, string rt) => EmitRType(rs, rt, rd, 0, 33);
        public void Subu(string rd, string rs, string rt) => EmitRType(rs, rt, rd, 0, 35);
        public void OrReg(string rd, string rs, string rt) => EmitRType(rs, rt, rd, 0, 37);
        public void Slt(string rd, string rs, string rt) => EmitRType(rs, rt, rd, 0, 42);
        public void Sltu(string rd, string rs, string rt) => EmitRType(rs, rt, rd, 0, 43);
        public void Jr(string rs) => EmitRType(rs, "zero", "zero", 0, 8);
        public void Beq(string rs, string rt, string label) => Branch(4, rs, rt, label);
        public void Bne(string rs, string rt, string label) => Branch(5, rs, rt, label);
        public void Jal(uint address) => words.Add(NewJalWord(address));
        public void JmpLabel(string label) => Jump(2, label);

        public void Li(string rt, uint value)
        {
            int hi = (int)((value >> 16) & 0xFFFF);
            int lo = (int)(value & 0xFFFF);
            if (hi == 0)
            {
                Ori(rt, "zero", lo);
                return;
            }

            Lui(rt, hi);
            if (lo != 0)
                Ori(rt, rt, lo);
        }

        public byte[] ToBytes()
        {
            ResolveFixups();
            byte[] bytes = new byte[words.Count * 4];
            for (int i = 0; i < words.Count; i++)
                Array.Copy(BitConverter.GetBytes(words[i]), 0, bytes, i * 4, 4);
            return bytes;
        }

        private void Branch(int op, string rs, string rt, string label)
        {
            fixups.Add(new Fixup("b", words.Count, op, rs, rt, label));
            words.Add(0);
        }

        private void Jump(int op, string label)
        {
            fixups.Add(new Fixup("j", words.Count, op, "", "", label));
            words.Add(0);
        }

        private void ResolveFixups()
        {
            foreach (Fixup fixup in fixups)
            {
                if (!labels.TryGetValue(fixup.Label, out int targetIndex))
                    throw new InvalidOperationException($"Unknown MIPS label '{fixup.Label}'.");

                if (fixup.Kind == "b")
                {
                    int rel = targetIndex - (fixup.Index + 1);
                    if (rel is < short.MinValue or > short.MaxValue)
                        throw new InvalidOperationException($"MIPS branch to '{fixup.Label}' is out of range.");
                    words[fixup.Index] = IType(fixup.Op, Registers[fixup.Rs], Registers[fixup.Rt], rel);
                }
                else
                {
                    uint address = baseAddress + ((uint)targetIndex * 4);
                    words[fixup.Index] = ((uint)fixup.Op << 26) | ((address >> 2) & 0x03FFFFFFu);
                }
            }
        }

        private void EmitIType(int op, string rs, string rt, long imm) => words.Add(IType(op, Registers[rs], Registers[rt], imm));

        private void EmitRType(string rs, string rt, string rd, int sh, int fn)
        {
            uint word = ((uint)Registers[rs] << 21) |
                ((uint)Registers[rt] << 16) |
                ((uint)Registers[rd] << 11) |
                (((uint)sh & 0x1F) << 6) |
                ((uint)fn & 0x3F);
            words.Add(word);
        }

        private static uint IType(int op, int rs, int rt, long imm)
        {
            return ((uint)op << 26) |
                ((uint)rs << 21) |
                ((uint)rt << 16) |
                ((uint)imm & 0xFFFF);
        }

        private sealed record Fixup(string Kind, int Index, int Op, string Rs, string Rt, string Label);
    }
}
