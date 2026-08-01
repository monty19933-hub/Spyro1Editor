using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Spyro.Editor.Core.Exporting;

internal sealed record NativeSkyOcclusionPatchPlan(
    int ExecutableLba,
    int ExecutableSize,
    int ExecutableFileOffset,
    long ImageOffset,
    string RuntimeAddress,
    string BeforeHex,
    string AfterHex,
    int PayloadExecutableFileOffset,
    long PayloadImageOffset,
    string PayloadRuntimeAddress,
    int PayloadByteLength,
    string PayloadBeforeSha256,
    string PayloadAfterSha256,
    IReadOnlyList<int> ScopedLevelIds,
    int PatchCount,
    int TotalWrittenBytes,
    int ChangedByteCount,
    byte[] HookBefore,
    byte[] HookAfter,
    byte[] PayloadBefore,
    byte[] PayloadAfter);

internal static class NativeSkyOcclusionPatcher
{
    private const uint RendererOcclusionGroupLoadAddress = 0x80051F90;
    private const uint ExpectedInstruction = 0x8CC40000; // lw a0, 0(a2)
    private const uint LegacyRenderAllSectorsInstruction = 0x2404FFFF; // addiu a0, zero, -1
    private const uint ScopedPayloadAddress = 0x80073400;
    private const int ScopedPayloadBytes = 0x100;
    private const int ScopedLevelTableOffset = 0x80;
    private const int MaxLevelId = 64;
    private const int PsxExeHeaderBytes = 0x800;

    // 0x8007314C-0x8007354B is the separately runtime-proven helper region.
    // The level-aware object-grade helper occupies the earlier portion and
    // leaves this 0x100-byte tail zero. Sky export runs after environment grade
    // in normal combined builds and claims only this disjoint tail. Any other
    // owner is rejected by exact preimage validation below.
    private static readonly uint[] ScopedPayloadCode =
    [
        0x3C088007, // lui   t0, 0x8007
        0x8D09596C, // lw    t1, 0x596C(t0)       ; g_LevelId
        0x2D2A0041, // sltiu t2, t1, 65
        0x11400009, // beq   t2, zero, native
        0x00000000, // nop
        0x3C088007, // lui   t0, 0x8007
        0x25083480, // addiu t0, t0, 0x3480       ; scoped byte table
        0x01094021, // addu  t0, t0, t1
        0x91080000, // lbu   t0, 0(t0)
        0x11000003, // beq   t0, zero, native
        0x00000000, // nop
        0x03E00008, // jr    ra
        0x2404FFFF, // addiu a0, zero, -1         ; edited destination only
        0x03E00008, // native: jr ra
        0x8CC40000  // lw    a0, 0(a2)            ; replay retail instruction
    ];

    public static NativeSkyOcclusionPatchPlan BuildPlan(
        string sourceImagePath,
        IReadOnlyCollection<int> destinationLevelIds,
        int? outputExecutableLba = null)
    {
        int[] requestedLevelIds = destinationLevelIds
            .Distinct()
            .OrderBy(value => value)
            .ToArray();
        if (requestedLevelIds.Length == 0)
            throw new InvalidOperationException("A scoped sky-visibility patch needs at least one edited destination level.");
        if (requestedLevelIds.Any(value => value is < 0 or > MaxLevelId))
        {
            throw new InvalidOperationException(
                $"Scoped sky visibility supports retail level ids 0-{MaxLevelId}; received {string.Join(", ", requestedLevelIds)}.");
        }

        DiscLayout layout = DiscImage.DetectLayout(sourceImagePath);
        using FileStream image = File.Open(sourceImagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        DiscFileRecord executable = DiscImage.FindRootFileRecord(image, layout, IsExecutableName);
        byte[] header = DiscImage.ReadFileBytes(image, layout, executable.Lba, 0, PsxExeHeaderBytes);
        if (header.Length < 0x20 || !Encoding.ASCII.GetString(header, 0, 8).StartsWith("PS-X EXE", StringComparison.Ordinal))
            throw new InvalidDataException("The Spyro executable does not have a valid PS-X EXE header.");

        uint loadAddress = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(0x18, 4));
        uint codeSize = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(0x1C, 4));
        int hookFileOffset = RuntimeAddressToFileOffset(RendererOcclusionGroupLoadAddress, loadAddress, codeSize, executable.Size, "sky renderer hook");
        int payloadFileOffset = RuntimeAddressToFileOffset(ScopedPayloadAddress, loadAddress, codeSize, executable.Size, "scoped sky payload");
        ExecutablePatchSafety.GuardPatchRange(RendererOcclusionGroupLoadAddress, 4, "Scoped sky renderer hook");
        ExecutablePatchSafety.GuardPatchRange(ScopedPayloadAddress, ScopedPayloadBytes, "Scoped sky visibility payload");

        byte[] hookBefore = DiscImage.ReadFileBytes(image, layout, executable.Lba, hookFileOffset, 4);
        uint instruction = BinaryPrimitives.ReadUInt32LittleEndian(hookBefore);
        uint scopedHookInstruction = EncodeJal(ScopedPayloadAddress);
        if (instruction is not ExpectedInstruction and not LegacyRenderAllSectorsInstruction && instruction != scopedHookInstruction)
        {
            throw new InvalidDataException(
                $"The selected executable has an unsupported sky renderer instruction at 0x{RendererOcclusionGroupLoadAddress:X8}: 0x{instruction:X8}.");
        }

        byte[] payloadBefore = DiscImage.ReadFileBytes(image, layout, executable.Lba, payloadFileOffset, ScopedPayloadBytes);
        HashSet<int> scopedLevelIds = requestedLevelIds.ToHashSet();
        if (instruction == scopedHookInstruction)
        {
            foreach (int existingLevelId in ReadAndValidatePayload(payloadBefore))
                scopedLevelIds.Add(existingLevelId);
        }
        else if (payloadBefore.Any(value => value != 0))
        {
            throw new InvalidDataException(
                $"The guarded scoped sky payload range at 0x{ScopedPayloadAddress:X8} is already owned by another runtime helper.");
        }

        int[] finalLevelIds = scopedLevelIds.OrderBy(value => value).ToArray();
        byte[] hookAfter = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(hookAfter, scopedHookInstruction);
        byte[] payloadAfter = BuildPayload(finalLevelIds);
        // Validate the exact code and table we are about to install rather than
        // treating a zero preimage as sufficient runtime proof.
        int[] payloadLevelIds = ReadAndValidatePayload(payloadAfter);
        if (!payloadLevelIds.SequenceEqual(finalLevelIds))
            throw new InvalidDataException("The scoped sky payload failed its level-table self-check.");

        int finalLba = outputExecutableLba ?? executable.Lba;
        bool hookChanges = !hookBefore.AsSpan().SequenceEqual(hookAfter);
        bool payloadChanges = !payloadBefore.AsSpan().SequenceEqual(payloadAfter);
        return new NativeSkyOcclusionPatchPlan(
            ExecutableLba: finalLba,
            ExecutableSize: executable.Size,
            ExecutableFileOffset: hookFileOffset,
            ImageOffset: DiscImage.ConvertFileOffsetToImageOffset(layout, finalLba, hookFileOffset),
            RuntimeAddress: $"0x{RendererOcclusionGroupLoadAddress:X8}",
            BeforeHex: ToHex(hookBefore),
            AfterHex: ToHex(hookAfter),
            PayloadExecutableFileOffset: payloadFileOffset,
            PayloadImageOffset: DiscImage.ConvertFileOffsetToImageOffset(layout, finalLba, payloadFileOffset),
            PayloadRuntimeAddress: $"0x{ScopedPayloadAddress:X8}",
            PayloadByteLength: ScopedPayloadBytes,
            PayloadBeforeSha256: Hash(payloadBefore),
            PayloadAfterSha256: Hash(payloadAfter),
            ScopedLevelIds: finalLevelIds,
            PatchCount: (hookChanges ? 1 : 0) + (payloadChanges ? 1 : 0),
            TotalWrittenBytes: (hookChanges ? hookAfter.Length : 0) + (payloadChanges ? payloadAfter.Length : 0),
            ChangedByteCount: CountChangedBytes(hookBefore, hookAfter) + CountChangedBytes(payloadBefore, payloadAfter),
            HookBefore: hookBefore,
            HookAfter: hookAfter,
            PayloadBefore: payloadBefore,
            PayloadAfter: payloadAfter);
    }

    public static void Apply(string outputImagePath, NativeSkyOcclusionPatchPlan plan)
    {
        DiscLayout layout = DiscImage.DetectLayout(outputImagePath);
        using FileStream image = File.Open(outputImagePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
        DiscFileRecord executable = DiscImage.FindRootFileRecord(image, layout, IsExecutableName);
        if (executable.Lba != plan.ExecutableLba || executable.Size != plan.ExecutableSize)
            throw new InvalidDataException("The output executable extent does not match the scoped sky visibility patch plan.");

        byte[] hookBefore = DiscImage.ReadFileBytes(image, layout, executable.Lba, plan.ExecutableFileOffset, plan.HookBefore.Length);
        byte[] payloadBefore = DiscImage.ReadFileBytes(image, layout, executable.Lba, plan.PayloadExecutableFileOffset, plan.PayloadBefore.Length);
        if (!hookBefore.AsSpan().SequenceEqual(plan.HookBefore))
        {
            throw new InvalidDataException(
                $"The output executable changed before the scoped sky hook at {plan.RuntimeAddress}: expected {ToHex(plan.HookBefore)}, found {ToHex(hookBefore)}.");
        }
        if (!payloadBefore.AsSpan().SequenceEqual(plan.PayloadBefore))
            throw new InvalidDataException($"The output executable changed before the scoped sky payload at {plan.PayloadRuntimeAddress}.");

        // Install and verify the dormant payload before routing the renderer to
        // it. A failed hook write therefore cannot leave an active partial shim.
        if (!payloadBefore.AsSpan().SequenceEqual(plan.PayloadAfter))
            DiscImage.WriteFileBytes(image, layout, executable.Lba, plan.PayloadExecutableFileOffset, plan.PayloadAfter);
        if (!hookBefore.AsSpan().SequenceEqual(plan.HookAfter))
            DiscImage.WriteFileBytes(image, layout, executable.Lba, plan.ExecutableFileOffset, plan.HookAfter);
        image.Flush();

        byte[] hookReadback = DiscImage.ReadFileBytes(image, layout, executable.Lba, plan.ExecutableFileOffset, plan.HookAfter.Length);
        byte[] payloadReadback = DiscImage.ReadFileBytes(image, layout, executable.Lba, plan.PayloadExecutableFileOffset, plan.PayloadAfter.Length);
        if (!hookReadback.AsSpan().SequenceEqual(plan.HookAfter) ||
            !payloadReadback.AsSpan().SequenceEqual(plan.PayloadAfter))
        {
            throw new InvalidDataException("The scoped sky visibility executable patch failed readback verification.");
        }
        int[] readbackLevelIds = ReadAndValidatePayload(payloadReadback);
        if (!readbackLevelIds.SequenceEqual(plan.ScopedLevelIds))
            throw new InvalidDataException("The scoped sky visibility level table failed readback verification.");
    }

    private static byte[] BuildPayload(IReadOnlyCollection<int> levelIds)
    {
        byte[] payload = new byte[ScopedPayloadBytes];
        for (int index = 0; index < ScopedPayloadCode.Length; index++)
            BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(index * 4, 4), ScopedPayloadCode[index]);
        foreach (int levelId in levelIds)
            payload[ScopedLevelTableOffset + levelId] = 1;
        return payload;
    }

    private static int[] ReadAndValidatePayload(byte[] payload)
    {
        if (payload.Length != ScopedPayloadBytes)
            throw new InvalidDataException($"The scoped sky payload is {payload.Length} bytes; expected {ScopedPayloadBytes}.");
        for (int index = 0; index < ScopedPayloadCode.Length; index++)
        {
            uint actual = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(index * 4, 4));
            if (actual != ScopedPayloadCode[index])
            {
                throw new InvalidDataException(
                    $"The scoped sky payload code changed at word {index}: expected 0x{ScopedPayloadCode[index]:X8}, got 0x{actual:X8}.");
            }
        }
        if (payload.AsSpan(ScopedPayloadCode.Length * 4, ScopedLevelTableOffset - (ScopedPayloadCode.Length * 4)).ContainsAnyExcept((byte)0) ||
            payload.AsSpan(ScopedLevelTableOffset + MaxLevelId + 1).ContainsAnyExcept((byte)0))
        {
            throw new InvalidDataException("The scoped sky payload contains unexpected bytes outside its code and level table.");
        }

        List<int> levelIds = [];
        for (int levelId = 0; levelId <= MaxLevelId; levelId++)
        {
            byte value = payload[ScopedLevelTableOffset + levelId];
            if (value > 1)
                throw new InvalidDataException($"The scoped sky level-table entry {levelId} is not Boolean.");
            if (value == 1)
                levelIds.Add(levelId);
        }
        return levelIds.ToArray();
    }

    private static int RuntimeAddressToFileOffset(uint runtimeAddress, uint loadAddress, uint codeSize, int executableSize, string label)
    {
        if (runtimeAddress < loadAddress)
            throw new InvalidDataException($"The {label} address is below the executable load address.");
        uint codeOffset = runtimeAddress - loadAddress;
        if (codeOffset + 4 > codeSize)
            throw new InvalidDataException($"The {label} address is outside the executable code range.");
        int fileOffset = checked(PsxExeHeaderBytes + (int)codeOffset);
        int requiredBytes = runtimeAddress == ScopedPayloadAddress ? ScopedPayloadBytes : 4;
        if (fileOffset + requiredBytes > executableSize)
            throw new InvalidDataException($"The {label} range is outside the executable file extent.");
        return fileOffset;
    }

    private static uint EncodeJal(uint address) => 0x0C000000u | ((address >> 2) & 0x03FFFFFFu);

    private static int CountChangedBytes(byte[] before, byte[] after) =>
        before.Where((value, index) => value != after[index]).Count();

    private static bool IsExecutableName(string name) =>
        name.StartsWith("SCUS_", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("SLUS_", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("SLES_", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("SCES_", StringComparison.OrdinalIgnoreCase);

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private static string ToHex(byte[] bytes) =>
        string.Join(' ', bytes.Select(value => value.ToString("X2", CultureInfo.InvariantCulture)));
}
