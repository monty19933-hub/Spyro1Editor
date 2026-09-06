using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;

internal static class NormalUploadStagingCases
{
    internal static void Run(Action<bool, string> check, Action<Action, string> reject)
    {
        byte[] upload = Enumerable.Range(0, 0xDE000).Select(i => (byte)(i * 73 + (i >> 8) + 19)).ToArray();
        Array.Fill(upload, (byte)0xA7, 0x80000, 0x5E000);
        var layout = NativeId65NormalUploadTransferLayout.Compile(upload);
        byte[] actual = layout.CopyTransferSourceImage();
        byte[] expected = upload.AsSpan(0x80000).ToArray().Concat(upload.AsSpan(0x5E000, 0x21000).ToArray()).ToArray();
        check(actual.Length == 0x7F000 && actual.SequenceEqual(expected),
            "transfer source is bank plus paired VRAM residue, not padded bank or retail replacement");
        check(!layout.SourceCertified && layout.TransferSourceSha256 == Hash(expected),
            "neutral exact bytes and hashes never issue original-source authority");
        check(layout.Owners.Count == 2 && layout.Owners[0].Offset == 0 && layout.Owners[0].ByteLength == 0x5E000 &&
            layout.Owners[0].PairedUploadOffset == 0x80000 && layout.Owners[1].Offset == 0x5E000 &&
            layout.Owners[1].ByteLength == 0x21000 && layout.Owners[1].PairedUploadOffset == 0x5E000,
            "two exact typed owners partition the entire detached transfer source");
        foreach (var owner in layout.Owners)
        {
            check(Hash(actual.AsSpan(owner.Offset, owner.ByteLength)) == owner.Sha256,
                "owner digest binds its whole output and exact paired-source offset");
            check(actual.AsSpan(owner.Offset, owner.ByteLength).SequenceEqual(upload.AsSpan(owner.PairedUploadOffset, owner.ByteLength)),
                "relative owners materialize only their declared paired input bytes");
        }
        check(layout.SourceReads.Count == 2 && layout.SourceReads[0].Backend == NativeId65UploadBackend.CpuFifo &&
            layout.SourceReads[0].ByteLength == 0x7EFF0 && layout.SourceReads[1].Backend == NativeId65UploadBackend.Dma &&
            layout.SourceReads[1].ByteLength == 0x7F000 && layout.SourceReads.All(r => r.Offset == 0),
            "CPU request and programmed DMA footprint remain distinct source domains");
        foreach (int at in new[] { 0, 0x5DFFF, 0x5E000, 0x7EFEF, 0x7EFF0, 0x7EFFF })
        {
            check(actual[at] == upload[at < 0x5E000 ? at + 0x80000 : at], "exact phase-owner boundary byte");
            byte[] corrupt = actual.ToArray();
            corrupt[at] ^= 1;
            reject(() => layout.RequireExactTransferSource(corrupt), "changed bank, residue or DMA-only final byte refuses");
        }
        layout.RequireExactTransferSource(expected);
        reject(() => layout.RequireExactTransferSource(expected[..^1]), "truncated DMA-only extent refuses");
        reject(() => layout.RequireExactTransferSource(expected.Concat(new byte[1]).ToArray()), "unowned extra byte refuses");
        reject(() => layout.RequireExactTransferSource(expected[..0x7EFF0]), "CPU-sized source cannot masquerade as DMA source");
        byte[] zeroPadded = expected.ToArray();
        zeroPadded.AsSpan(0x5E000).Clear();
        reject(() => layout.RequireExactTransferSource(zeroPadded), "arbitrary zero residue is not approved");
        foreach (int length in new[] { 0, 0x5E000, 0x80000, 0xDDFFF, 0xDE001, 0x100000 })
            reject(() => NativeId65NormalUploadTransferLayout.Compile(new byte[length]), "complete bounded paired upload required before copying");
        byte[] copy = layout.CopyTransferSourceImage();
        copy[0] ^= 1;
        upload[0x80000] ^= 1;
        check(layout.CopyTransferSourceImage().SequenceEqual(expected), "input and returned copies cannot mutate accepted bytes");
        upload[0x80000] ^= 1;
        var repeat = NativeId65NormalUploadTransferLayout.Compile(upload);
        check(repeat.TransferSourceSha256 == layout.TransferSourceSha256 && repeat.PairedUploadSha256 == layout.PairedUploadSha256,
            "neutral repeat is deterministic across every output byte");
        upload[0x5E000] ^= 1;
        var changed = NativeId65NormalUploadTransferLayout.Compile(upload);
        check(changed.TransferSourceSha256 != layout.TransferSourceSha256 && changed.CopyTransferSourceImage()[0x5E000] != expected[0x5E000],
            "changed paired VRAM affects actual residue rather than retaining an old snapshot");
        reject(() => NativeId65NormalUploadStagingSource.ValidateSourceExecutable([]), "missing exact consumer ownership refuses");
        reject(() => NativeId65NormalUploadStaging.CaptureAsync("unopened-missing.bin", null!).GetAwaiter().GetResult(),
            "missing private issued bundle refuses before source I/O");
    }

    internal static async Task Source(string image, NativeId65NormalResourceBundle first,
        NativeId65NormalResourceBundle second, Action<bool, string> check, Action<Action, string> reject)
    {
        var lists = await NativeId65ZeroSceneAnimationLists.CaptureAsync(image);
        var chain = NativeId65NormalResourceChain.Compile(first, lists);
        var fresh = await NativeId65FreshSceneBundle.CaptureAsync(image, chain, new(8960, 8960, 9216, 64), 16);
        byte[] priorUpload = fresh.CopyUploadPayload(), priorScene = fresh.CopyScene();
        byte[] priorData = fresh.CopyDataComponents(), priorModel = fresh.CopyModelPayload();
        var stage = await NativeId65NormalUploadStaging.CaptureAsync(image, fresh);
        check(stage.SourceCertified && !stage.RuntimeVerified && !stage.PublicationApproved && !stage.IndependentLevelSupported &&
            !stage.CompleteLevelResourcesProven && !stage.CompleteSpuStateProven && !stage.SpuWrapSemanticsProven &&
            !stage.ReservedLow16PreservationProven && !stage.PeakRuntimeMemoryProven && !stage.TransferTimingProven,
            "source-issued transfer bytes retain all hardware/timing/full-level authority exclusions");
        check(stage.CpuRequestedByteLength == 0x7EFF0 && stage.DmaProgrammedSourceByteLength == 0x7F000 &&
            stage.LinearSpuDestination == 0x1010 && stage.DmaLinearSpuEnd == 0x80010,
            "linear programmed endpoint exposes rather than hides low16 wrap obligation");
        check(stage.TransferSourceSha256 == "626ae349b98cb4bea955ac53020586f645f872225085a67bc809f46f4ab0fb07",
            "all emitted staging bytes match independent raw-sector reconstruction");
        byte[] actual = stage.CopyTransferSourceImage();
        check(actual.AsSpan(0, 0x5E000).SequenceEqual(priorUpload.AsSpan(0x80000)) &&
            actual.AsSpan(0x5E000).SequenceEqual(priorUpload.AsSpan(0x5E000, 0x21000)),
            "source-issued output has exact retained audio and same-issued presentation residue");
        check(Convert.ToHexStringLower(actual.AsSpan(0x7EFF0)) == "3232b131122e903191311232f231322e",
            "DMA-only final16 are real nonzero presentation bytes, not asserted safe silence");
        check(stage.UpstreamPlanSha256 == fresh.PlanSha256 && stage.PairedUploadSha256 == fresh.UploadSha256,
            "stage identity binds the actual preceding private producer and complete paired upload");
        check(priorUpload.SequenceEqual(fresh.CopyUploadPayload()) && priorScene.SequenceEqual(fresh.CopyScene()) &&
            priorData.SequenceEqual(fresh.CopyDataComponents()) && priorModel.SequenceEqual(fresh.CopyModelPayload()),
            "staging does not alter old upload/scene/data/model bytes or their authorities");
        var repeated = await NativeId65NormalUploadStaging.CaptureAsync(image, fresh);
        check(repeated.PlanSha256 == stage.PlanSha256 && repeated.CopyTransferSourceImage().SequenceEqual(actual),
            "fresh exact source recapture repeats the same immutable stage");
        var other = await NativeId65FreshSceneBundle.CaptureAsync(image, NativeId65NormalResourceChain.Compile(second, lists),
            new(8976, 8960, 9216, 128), 16);
        var otherStage = await NativeId65NormalUploadStaging.CaptureAsync(image, other);
        check(otherStage.PlanSha256 != stage.PlanSha256 && otherStage.TransferSourceSha256 == stage.TransferSourceSha256,
            "distinct upstream authored intents remain distinct plans even when transfer bytes are equal");
        foreach (int at in new[] { 0, 0x5DFFF, 0x5E000, 0x7EFEF, 0x7EFF0, 0x7EFFF })
        {
            byte[] corrupt = actual.ToArray();
            corrupt[at] ^= 1;
            reject(() => stage.RequireExactTransferSource(corrupt), "source stage rejects corruption in every phase and last16 owner");
        }
        reject(() => stage.RequireExactTransferSource(actual[..^1]), "source stage refuses missing last byte");
        reject(() => stage.RequireExactTransferSource(actual.Concat(new byte[1]).ToArray()), "source stage refuses extra byte");
        var source = await NativeId65NormalUploadStagingSource.CaptureAsync(image, CancellationToken.None);
        byte[] executable = source.CopyExecutable();
        NativeId65NormalUploadStagingSource.ValidateSourceExecutable(executable);
        foreach (var evidence in stage.Evidence.Where(e => e.Domain == "EXE"))
        {
            byte[] corrupt = executable.ToArray();
            corrupt[checked((int)evidence.Offset)] ^= 1;
            reject(() => NativeId65NormalUploadStagingSource.ValidateSourceExecutable(corrupt),
                "altered native transfer, boot, pointer or return-delay certificate refuses");
        }
        foreach (int at in new[] { 0, 0x603C + 0x18, 0x4CBD4, 0x4CC1C, 0x63D94, executable.Length - 1 })
        {
            byte[] corrupt = executable.ToArray();
            corrupt[at] ^= 1;
            reject(() => NativeId65NormalUploadStagingSource.ValidateSourceExecutable(corrupt),
                "full EXE ownership includes changed consumer delay and unused complement");
        }
        NativeWindows(executable, check, reject);
        Console.WriteLine($"NATIVE_ID65_NORMAL_UPLOAD_STAGING_SOURCE_OK bytes={actual.Length} sha256={stage.TransferSourceSha256} plan={stage.PlanSha256}");
    }

    private static void NativeWindows(byte[] executable, Action<bool, string> check, Action<Action, string> reject)
    {
        var request = new FreshSceneMachine();
        request.Code(0x8001583C, executable.AsSpan(0x603C, 0x20));
        request.Execute(0x8001583C, 0x8005CB24);
        check(request.Registers[4] == 0x1010 && request.Registers[31] == 0x80015844,
            "native address call executes exact delay-slot 1010 argument");
        request.Map(0x800785DC, Words(0x80100000), false);
        request.Execute(0x80015844, 0x8005CAC4);
        check(request.Registers[4] == 0x80100000 && request.Registers[5] == 0x7EFF0,
            "native normal write uses same copy buffer and delay-slot fixed length");
        foreach (uint size in new uint[] { 0, 1, 0x40, 0x5E000, 0x7EFEF, 0x7EFF0, 0x7F000, uint.MaxValue })
        {
            var cap = new FreshSceneMachine();
            cap.Code(0x8005CAC4, executable.AsSpan(0x4D2C4, 0x60));
            cap.Map(0x3000, new byte[0x100], true);
            cap.Registers[29] = 0x3100;
            cap.Registers[4] = 0x1000;
            cap.Registers[5] = size;
            cap.Execute(0x8005CAC4, 0x8005C450);
            check(cap.Registers[4] == 0x1000 && cap.Registers[5] == Math.Min(size, 0x7EFF0u),
                "actual unsigned wrapper cap and call delay preserve the fixed normal request");
        }
        foreach (uint size in new uint[] { 0x80000, 0x5E000 })
        {
            var cd = new FreshSceneMachine();
            cd.Code(0x80016724, executable.AsSpan(0x6F24, 0x3C));
            cd.Map(0x80076B94, new byte[0x28], true);
            cd.Registers[19] = size;
            cd.Registers[20] = 0x80100000;
            cd.Execute(0x80016724, 0x8006606C);
            check(cd.Registers[4] == size / 0x800 && cd.Registers[5] == 0x80100000 && cd.Registers[6] == 0x80,
                "actual aligned CD-request rounding programs only the exact VRAM/bank sector extent");
        }
        foreach (uint mode in new uint[] { 0, 1, 2, uint.MaxValue })
        {
            var backend = SpuMachine(executable);
            backend.Registers[4] = 0x1000;
            backend.Registers[5] = 0x7EFF0;
            backend.SeedWord(0x80073570, mode);
            backend.SeedWord(0x8007356C, 0x202);
            backend.SeedWord(0x8007357C, 3);
            backend.Execute(0x8005C450, mode == 0 ? 0x8005C1C0 : 0x8005BE88);
            check(mode == 0 ? backend.Registers[4] == 2 && backend.Registers[5] == 0x1010 :
                backend.Registers[4] == 0x1000 && backend.Registers[5] == 0x7EFF0,
                "native backend selects CPU for any nonzero mode and DMA address programming only for zero");
        }
        foreach (uint size in new uint[] { 1, 2, 63, 64, 65, 0x5E000, 0x7EFEF, 0x7EFF0, 0x7F000 })
        {
            foreach (uint direction in new uint[] { 0, 1 })
            {
                var dma = SpuMachine(executable);
                dma.Registers[4] = 3;
                dma.Registers[5] = 0x80100000;
                dma.Registers[6] = size;
                dma.SeedWord(0x800735A4, direction);
                dma.SeedWord(0x1F801DA8, direction == 0 ? 0x00200000u : 0x00300000u);
                dma.Execute(0x8005C1C0, FreshSceneMachine.ReturnPc);
                uint blocks = size / 64 + (size % 64 == 0 ? 0u : 1u);
                check(dma.ReadWord(0x1F8010C0) == 0x80100000 &&
                    dma.ReadWord(0x1F8010C4) == (blocks << 16 | 16) &&
                    dma.ReadWord(0x1F8010C8) == (direction == 0 ? 0x01000201u : 0x01000200u),
                    "actual DMA argument traversal, rounding and direction register programming includes every delay");
            }
        }
        // Execute each exact scalar FIFO chunk, not the intervening hardware
        // wait/helper calls. Aggregate native reads cover the whole fixed even
        // request; odd/generic transfer sizes remain outside this profile.
        int total = 0;
        int chunks = 0;
        while (total < 0x7EFF0)
        {
            int amount = Math.Min(64, 0x7EFF0 - total);
            var cpu = new FreshSceneMachine();
            cpu.Code(0x8005BEC8, executable.AsSpan(0x4C6C8, 0x40));
            byte[] bytes = Enumerable.Range(0, amount).Select(i => (byte)((total + i) * 73 + 19)).ToArray();
            cpu.Map(0x1000, bytes, false);
            cpu.Map(0x80073554, Words(0x1F801C00), false);
            cpu.Map(0x1F801DA8, new byte[2], true);
            cpu.Registers[17] = (uint)(0x7EFF0 - total);
            cpu.Registers[18] = 0x1000;
            cpu.Execute(0x8005BEC8, 0x8005BF08);
            check(cpu.Registers[16] == amount && cpu.Registers[18] == 0x1000 + amount &&
                cpu.HalfwordReadCount == amount / 2 && cpu.LastHalfwordReadAddress == 0x1000 + amount - 2 &&
                cpu.ReadByte(0x1F801DA8) == bytes[^2] && cpu.ReadByte(0x1F801DA9) == bytes[^1],
                "actual fixed-even FIFO chunk has exact read extent, source cursor and delayed halfword value");
            total += amount;
            chunks++;
        }
        check(total == 0x7EFF0 && chunks == 8128, "all native CPU chunks stop before the DMA-only final16");
        var shortCpu = new FreshSceneMachine();
        shortCpu.Code(0x8005BEC8, executable.AsSpan(0x4C6C8, 0x40));
        shortCpu.Map(0x1000, new byte[47], false);
        shortCpu.Map(0x80073554, Words(0x1F801C00), false);
        shortCpu.Map(0x1F801DA8, new byte[2], true);
        shortCpu.Registers[17] = 48;
        shortCpu.Registers[18] = 0x1000;
        reject(() => shortCpu.Execute(0x8005BEC8, 0x8005BF08), "native final-halfword read refuses an incomplete source owner");
        BootAndAddress(executable, check);
    }

    private static FreshSceneMachine SpuMachine(byte[] executable)
    {
        var machine = new FreshSceneMachine();
        machine.Code(0x8005C1C0, executable.AsSpan(0x4C9C0, 0x290));
        machine.Code(0x8005C450, executable.AsSpan(0x4CC50, 0x88));
        machine.Code(0x8005C588, executable.AsSpan(0x4CD88, 0xA4));
        machine.Code(0x8005C6C8, executable.AsSpan(0x4CEC8, 0x58));
        machine.Code(0x8005CB24, executable.AsSpan(0x4D324, 0x8C));
        machine.Map(0x80073554, executable.AsSpan(0x63D54, 0x60), true);
        machine.Map(0x1F801C00, new byte[0x200], true);
        machine.Map(0x1F8010C0, new byte[12], true);
        machine.Map(0x1F801014, new byte[4], true);
        machine.Map(0x3000, new byte[0x200], true);
        machine.Registers[29] = 0x3100;
        machine.Registers[31] = FreshSceneMachine.ReturnPc;
        return machine;
    }

    private static void BootAndAddress(byte[] executable, Action<bool, string> check)
    {
        var boot = SpuMachine(executable);
        boot.Code(0x8005BCD0, executable.AsSpan(0x4C4D0, 0x104));
        boot.Map(0x800777A8, new byte[20], true);
        boot.Registers[4] = 0;
        boot.Registers[16] = 0;
        boot.Execute(0x8005BCD0, 0x8005BE88);
        check(boot.ReadWord(0x80073578) == 2 && boot.ReadWord(0x8007357C) == 3 &&
            boot.ReadWord(0x80073580) == 8 && boot.ReadWord(0x80073584) == 7,
            "actual boot source installs eight-byte address units and rounding globals");
        check(boot.Registers[4] == 0x80073594 && boot.Registers[5] == 16 &&
            (boot.ReadWord(0x8007356C) & 0xFFFF) == 0x200,
            "actual boot CPU call uses sixteen default bytes at SPU1000, not SPU0");
        for (uint i = 0; i < 16; i++)
            check(boot.ReadByte(0x80073594 + i) == 7, "source boot block is exact07 bytes without a silence claim");
        boot.Execute(0x8005BD94, 0x8005BDD4);
        for (uint voice = 0; voice < 24; voice++)
            check((boot.ReadWord(0x1F801C04 + voice * 16) >> 16) == 0x200,
                "all24 native initial voice starts reference1000 rather than low16");
        foreach (uint address in new uint[] { 0, 0x1000, 0x100F, 0x1010, 0x1011, 0x7FFF8, 0x7FFF9, uint.MaxValue })
        {
            boot.Registers[4] = address;
            boot.Registers[31] = FreshSceneMachine.ReturnPc;
            boot.Execute(0x8005CB24, FreshSceneMachine.ReturnPc);
            uint expected = address >= 0x1010 && address <= 0x7FFF8 ? (address + 7) & ~7u : 0u;
            check(boot.Registers[2] == expected, "actual native address guard and rounding preserve unsupported lower-owner uncertainty");
        }
    }

    private static byte[] Words(params uint[] values)
    {
        byte[] bytes = new byte[values.Length * 4];
        for (int i = 0; i < values.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i * 4), values[i]);
        return bytes;
    }

    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
