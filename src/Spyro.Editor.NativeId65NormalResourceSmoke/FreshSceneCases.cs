using System.Buffers.Binary;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;

internal static class FreshSceneCases
{
    internal static void Run(Action<bool, string> check, Action<Action, string> reject)
    {
        var spawn = new NativeId65SceneSpawn(8960, 8960, 9216, 64);
        byte[] tiles = new byte[120];
        byte[] lists = NativeId65ZeroSceneAnimationLists.CreateNeutral().CopyComponents();
        foreach (int slots in new[] { 1, 2, 16, 32 })
        {
            var scene = NativeId65FreshSceneLayout.Compile(spawn, slots, tiles, lists);
            byte[] bytes = scene.CopyScene();
            check(scene.DynamicArenaByteLength == 0x70 * slots + 0x58 && scene.NativeNominalCapacity == slots,
                "native floor capacity includes disjoint complete row sentinel owner");
            check(scene.PlacedCount == 0 && scene.BucketByteLength == 0x2000 && scene.FixupCount == 0,
                "empty placed rows still own dynamic storage and both spatial Moby tables");
            check(Word(bytes, scene.MobyComponentOffset) == 8 + scene.DynamicArenaByteLength &&
                Word(bytes, scene.MobyComponentOffset + 4) == 0 && scene.MobyComponentOffset == 0xCC,
                "actual fresh Moby component follows exact scene+88 prefix");
            check(bytes.AsSpan(0x88, 0x44).SequenceEqual(lists) && bytes.AsSpan(0x10, 120).SequenceEqual(tiles),
                "neutral byte layout preserves exact passed header/prefix without source authority");
            check(Word(bytes, 0) == 8960 && Word(bytes, 4) == 8960 && Word(bytes, 8) == 9216 &&
                bytes[14] == 64 && bytes[12] == 0 && bytes[13] == 0 && bytes[15] == 0,
                "authored spawn fields have exact native widths and explicit remaining bytes");
            check(bytes.Length % 0x800 == 0 && !scene.SourceCertified && !scene.RuntimeVerified,
                "neutral physical layout has no source/runtime authority");
            scene.RequireExactScene(bytes);
            check(NativeId65FreshSceneLayout.DecodeCanonical(bytes).CopyScene().SequenceEqual(bytes),
                "independent bounded field decode re-encodes the complete fresh scene grammar");
            foreach (var owner in scene.Owners)
            {
                byte[] corrupt = bytes.ToArray();
                corrupt[owner.Offset] ^= 1;
                reject(() => scene.RequireExactScene(corrupt), "every fresh scene byte owner is bound");
            }
            foreach (int offset in new[] { 12, 13, 15, 0xCC, 0xD0, scene.DynamicArenaOffset,
                scene.BucketOffset - 4, scene.BucketOffset, scene.BucketOffset + 0x2000, bytes.Length - 1 })
            {
                byte[] corrupt = bytes.ToArray();
                corrupt[offset] ^= 1;
                reject(() => NativeId65FreshSceneLayout.DecodeCanonical(corrupt),
                    "decoder rejects noncanonical fields, nonzero arena/buckets/fixups and changed padding");
            }
            byte[] detached = scene.CopyScene();
            detached[0] ^= 1;
            check(scene.CopyScene()[0] == bytes[0], "neutral scene snapshots cannot mutate the plan");
        }
        foreach (int slots in new[] { -1, 0, 33, int.MaxValue })
            reject(() => NativeId65FreshSceneLayout.Compile(spawn, slots, tiles, lists), "bounded authored dynamic slot budget");
        foreach (var bad in new[] { spawn with { X = -1 }, spawn with { Y = 262144 }, spawn with { Z = int.MaxValue }, spawn with { Heading = 256 } })
            reject(() => NativeId65FreshSceneLayout.Compile(bad, 16, tiles, lists), "native bounded unsigned bucket and heading grammar");
        reject(() => NativeId65FreshSceneLayout.Compile(spawn, 16, tiles[..^1], lists), "full fifteen Tiledef owner required");
        reject(() => NativeId65FreshSceneLayout.Compile(spawn, 16, tiles, lists[..^4]), "paired list guard missing");
        reject(() => NativeId65FreshSceneLayout.DecodeCanonical(new byte[0x800]), "truncated complete fresh scene owner refuses before walking");
        reject(() => NativeId65FreshSceneLayout.DecodeCanonical(new byte[0x3800]), "unbounded scene owner refuses before allocation");
        byte[] wrong = lists.ToArray();
        wrong[4] = 1;
        reject(() => NativeId65FreshSceneLayout.Compile(spawn, 16, tiles, wrong), "nonzero scene list cannot bypass paired resource grammar");

        byte[] tile = Convert.FromHexString("0000201C1F00AC80");
        var full = NativeId65SceneHeaderTextureOwners.DecodeAvailableTiledef(14, tile);
        check(full.PixelRows.Count == 256 && full.PixelRows.All(r => r.ByteLength == 256) &&
            full.PaletteRows.Count == 8 && full.PaletteRows.All(r => r.ByteLength == 512),
            "native dynamic UV owner is a complete8bpp page and eight256-color fade CLUT rows");
        check(full.PixelRows[0].VramRelativeOffset == 512 && full.PaletteRows[0].VramRelativeOffset == 112 * 1024 &&
            full.PaletteRows[7].VramRelativeOffset == 119 * 1024,
            "8bpp header texture uses fixed right-half byte/address domains");
        var four = NativeId65SceneHeaderTextureOwners.DecodeAvailableTiledef(1, Convert.FromHexString("20A07D683FA05F80"));
        check(four.PixelRows.Count == 256 && four.PixelRows.All(r => r.ByteLength == 128) &&
            four.PaletteRows.Count == 8 && four.PaletteRows.All(r => r.ByteLength == 32),
            "stored equal V values do not collapse native shadow ownership to one row");
        var conflict = new NativeId65VramAssembly(new byte[0x80000]);
        conflict.CopyDetachedRange("independent-zero-owner", full.PixelRows[0].VramRelativeOffset, new byte[1]);
        reject(() => conflict.CopySourceRange(full.PixelRows[0].VramRelativeOffset, full.PixelRows[0].ByteLength),
            "new full header page cannot consume a distinct detached owner even when both source and destination bytes are zero");
        var reverseConflict = new NativeId65VramAssembly(new byte[0x80000]);
        reverseConflict.CopySourceRange(full.PaletteRows[0].VramRelativeOffset, full.PaletteRows[0].ByteLength);
        reject(() => reverseConflict.CopyDetachedRange("independent-zero-palette", full.PaletteRows[0].VramRelativeOffset, new byte[1]),
            "the complete retained fade CLUT bank remains protected from a later detached allocation");
        foreach (ushort page in new ushort[] { 0, 0x80, 0x8017, 0x811F, 0x821F, 0x80FF, 0x8010 })
        {
            byte[] corrupt = tile.ToArray();
            BinaryPrimitives.WriteUInt16LittleEndian(corrupt.AsSpan(6), page);
            reject(() => NativeId65SceneHeaderTextureOwners.DecodeAvailableTiledef(14, corrupt),
                "unknown modes/tags/left-half/cross-row pages refuse");
        }
        byte[] badPalette = tile.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(badPalette.AsSpan(2), 0x1C3F);
        reject(() => NativeId65SceneHeaderTextureOwners.DecodeAvailableTiledef(14, badPalette), "complete256-entry palette cannot cross row");
        BinaryPrimitives.WriteUInt16LittleEndian(badPalette.AsSpan(2), (ushort)(505 * 64 + 32));
        reject(() => NativeId65SceneHeaderTextureOwners.DecodeAvailableTiledef(14, badPalette), "eight-row native fade bank cannot cross VRAM height");
        byte[] uvCarry = tile.ToArray();
        uvCarry[0] = 255;
        uvCarry[1] = 255;
        reject(() => NativeId65SceneHeaderTextureOwners.DecodeAvailableTiledef(14, uvCarry),
            "unsupported UV plus low native fade offset cannot carry into unowned CLUT selection");
        reject(() => NativeId65SceneHeaderTextureOwners.DecodeAvailableTiledef(13, new byte[8]),
            "unavailable source superflame is not an owned VRAM0 texture");
        reject(() => NativeId65SceneHeaderTextureOwners.DecodeAvailableTiledef(14, tile[..^1]), "exact8-byte descriptor required");
        foreach (int index in new[] { -1, 15, int.MaxValue })
            reject(() => NativeId65SceneHeaderTextureOwners.DecodeAvailableTiledef(index, tile), "native fifteen-slot header namespace is bounded");
        reject(() => NativeId65FreshSceneBundle.CaptureAsync("unopened-missing-source.bin", null!, spawn, 16).GetAwaiter().GetResult(),
            "missing private issued resource chain refuses before opening an image");
        reject(() => NativeId65FreshSceneSource.ValidateSourceExecutable([]), "missing native scene/allocator consumers refuse");
        FreshSceneMachine.SelfTest(check, reject);
    }

    internal static async Task Source(string image, NativeId65NormalResourceBundle first,
        NativeId65NormalResourceBundle second, Action<bool, string> check, Action<Action, string> reject)
    {
        var lists = await NativeId65ZeroSceneAnimationLists.CaptureAsync(image);
        var chain = NativeId65NormalResourceChain.Compile(first, lists);
        var spawn = new NativeId65SceneSpawn(8960, 8960, 9216, 64);
        var plan = await NativeId65FreshSceneBundle.CaptureAsync(image, chain, spawn, 16);
        check(plan.SourceCertified && !plan.RuntimeVerified && !plan.PublicationApproved &&
            !plan.CompleteLevelResourcesProven && !plan.CompleteHeaderTextureConsumerClosureProven,
            "actual fresh scene/atlas bytes do not promote unavailable texture/state/runtime gates");
        check(plan.HeaderTextures.UnavailableDescriptorIds.SequenceEqual(new[] { 13 }) &&
            plan.HeaderTextures.DescriptorCount == 15,
            "all native header descriptors retained with unavailable superflame explicit");
        check(plan.EntryProfile.RequiresFreshNormalLoader && !plan.EntryProfile.SuperflameAvailable &&
            !plan.EntryProfile.CheckpointStateSupported && !plan.EntryProfile.ForeignStateSupported &&
            !plan.EntryProfile.PowerGrantingBehaviorSupported && !plan.EntryProfile.AllStateClosureProven,
            "source-issued conditional entry profile does not promote restored state or granting behavior");
        reject(() => plan.HeaderTextures.RequireAvailableDescriptor(13), "no fake source texture for zero superflame");
        check(plan.CopyScene().Length == 0x3000 && plan.SceneLayout.NativeNominalCapacity == 16,
            "actual fresh scene capacity/sector output matches authored budget");
        check(plan.SourceVramOwnedByteCount == 104576, "complete page/fade owners match independent source byte union");
        check(plan.SceneSha256 == "4a6e7b5c6c53199b8a7aa6ae0d24ac9bce32634aa259d5f49535282080641433" &&
            plan.UploadSha256 == "b769a9a298285aa402956fdbd5721132736388c6f3f1dc1bfcd7e15a9afeb66d",
            "fresh scene and complete header-page upload match independent raw-sector assembly");
        check(plan.CopyDataComponents().SequenceEqual(chain.CopyDataComponents()) &&
            plan.CopyModelPayload().SequenceEqual(chain.CopyModelPayload()), "accepted data/model bytes unchanged");
        byte[] upload = plan.CopyUploadPayload();
        check(upload.AsSpan(0x80000).SequenceEqual(chain.CopyUploadPayload().AsSpan(0x80000)),
            "complete native Town sound baseline unchanged");
        foreach (var owner in first.Owners.Where(o => o.Domain == NativeId65ResourceDomain.UploadPayload &&
            o.Offset < 0x80000 && o.Provenance != "fresh-zero-upload-background"))
            check(upload.AsSpan(owner.Offset, owner.ByteLength).SequenceEqual(chain.CopyUploadPayload().AsSpan(owner.Offset, owner.ByteLength)),
                "prior protected pixels including zero values remain byte-exact");
        plan.RequireExactOutputs(plan.CopyScene(), upload);
        foreach (var owner in plan.SceneLayout.Owners)
        {
            byte[] corrupt = plan.CopyScene();
            corrupt[owner.Offset] ^= 1;
            reject(() => plan.RequireExactOutputs(corrupt, upload), "all fresh scene owners bound in source-issued output");
        }
        foreach (var evidence in plan.Evidence.Where(e => e.Domain == "EXE"))
        {
            byte[] corrupt = first.Surface.CopySourceExecutable();
            corrupt[(int)evidence.Offset] ^= 1;
            reject(() => NativeId65FreshSceneSource.ValidateSourceExecutable(corrupt), "source consumer mutation refuses");
        }
        FreshSceneNativeCases.Execute(first.Surface.CopySourceExecutable(), plan.SceneLayout, check, reject);
        var repeat = await NativeId65FreshSceneBundle.CaptureAsync(image, chain, spawn, 16);
        check(repeat.PlanSha256 == plan.PlanSha256, "fresh scene repeat capture/compile deterministic");
        var alternate = await NativeId65FreshSceneBundle.CaptureAsync(image,
            NativeId65NormalResourceChain.Compile(second, lists), spawn with { Heading = 128, X = 8976 }, 16);
        check(alternate.SceneSha256 != plan.SceneSha256 && alternate.UploadSha256 == plan.UploadSha256 &&
            alternate.DataSha256 != plan.DataSha256, "independent authored spawn and floor edits preserve shared presentation bytes");
        check(alternate.SceneSha256 == "268b36fdb7136d533c01a93a71fd62a71bc9ec58799558698b14ba1f21b18380",
            "second fresh scene matches independent raw-sector assembly");
        Console.WriteLine($"NATIVE_ID65_FRESH_SCENE_SOURCE_OK scene={plan.SceneSha256} upload={plan.UploadSha256} plan={plan.PlanSha256}");
    }

    private static uint Word(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
}

internal static class FreshSceneNativeCases
{
    internal static void Execute(byte[] executable, NativeId65FreshSceneLayout scene,
        Action<bool, string> check, Action<Action, string> reject)
    {
        const uint basis = 0x80100000;
        byte[] sourceScene = scene.CopyScene();
        foreach (int slots in new[] { 1, 2, 16, 32 })
        {
            var layout = NativeId65FreshSceneLayout.Compile(scene.Spawn, slots,
                sourceScene.AsSpan(0x10, 120), sourceScene.AsSpan(0x88, 0x44));
            var machine = new FreshSceneMachine();
            machine.Code(0x80013F24, executable.AsSpan(0x4724, 0x164));
            machine.Code(0x800524C4, executable.AsSpan(0x42CC4, 0xA4));
            machine.Map(basis, layout.CopyScene(), false);
            uint arena = basis + (uint)layout.DynamicArenaOffset;
            uint end = arena + (uint)layout.DynamicArenaByteLength;
            machine.Allow(arena, layout.DynamicArenaByteLength);
            foreach (uint global in new uint[] { 0x80075828, 0x80075890, 0x8007573C, 0x800756A4,
                0x800756E8, 0x80075930, 0x800756A8, 0x800757F8, 0x800757B4, 0x80075778 })
                machine.Map(global, new byte[4], true);
            machine.Registers[16] = basis + 0xCC;
            machine.Registers[5] = basis + 0xCC;
            machine.Registers[18] = basis;
            machine.Execute(0x80013F24, 0x80014084);
            check(machine.ReadWord(0x800756A8) == slots && machine.ReadWord(0x800756A4) == 0,
                "actual native multiply/high-word capacity formula produces authored N before allocation");
            check(machine.ReadWord(0x80075828) == arena && machine.ReadWord(0x80075890) == arena &&
                machine.ReadWord(0x8007573C) == arena && machine.ReadWord(0x80075930) == end,
                "native loader installs exact upward-row and downward-property owners");
            check(machine.ReadByte(arena + 0x48) == 0xFF && machine.ReadByte(end - 1) == 0xFF,
                "both native dynamic sentinels are within explicit storage");
            check(machine.ReadWord(0x80075778) == basis + layout.BucketOffset && machine.ReadWord(0x800757B4) == 0,
                "native zero pointer list and two spatial bucket tables install exact bases");
            for (int i = 0; i < slots; i++)
            {
                machine.Registers[31] = FreshSceneMachine.ReturnPc;
                machine.Execute(0x800524C4, FreshSceneMachine.ReturnPc);
                uint row = arena + (uint)(i * 0x58);
                uint properties = end - (uint)((i + 1) * 0x18);
                check(machine.Registers[2] == row && machine.ReadWord(row) == properties,
                    "actual allocator returns a fresh58-byte row and records its separate18-byte properties pointer");
                check(machine.ReadWord(0x800756A4) == i + 1 &&
                    machine.ReadWord(0x8007573C) == row + 0x58 && machine.ReadWord(0x80075930) == properties,
                    "actual allocator advances both cursors and native count exactly");
                check(machine.ReadByte(row + 0x58 + 0x48) == 0xFF && machine.ReadByte(properties - 1) == 0xFF,
                    "next native sentinels do not overlap any already allocated row or properties owner");
                // Allocated bytes remain readable, but a later allocation is
                // forbidden to overwrite another actor's row or properties.
                machine.Protect(row, 0x58);
                machine.Protect(properties, 0x18);
            }
            machine.Registers[31] = FreshSceneMachine.ReturnPc;
            reject(() => machine.Execute(0x800524C4, FreshSceneMachine.ReturnPc),
                "native N+1 allocation is unsafe: it overwrites an allocated owner or escapes arena, not a native capacity refusal");
        }
        HeaderCopies(executable, sourceScene, check);
        FreshNormalEntry(executable, sourceScene, check);
        HeaderFade(executable, sourceScene, check);
        SpatialBuckets(executable, scene, check);
    }

    private static void FreshNormalEntry(byte[] executable, byte[] scene, Action<bool, string> check)
    {
        const uint flame = 0x800786C8;
        var reset = new FreshSceneMachine();
        reset.Code(0x80013B68, executable.AsSpan(0x4368, 0x14));
        reset.Code(0x80016914, executable.AsSpan(0x7114, 0x1C));
        reset.Map(flame - 4, Enumerable.Repeat((byte)0xA5, 0x140).ToArray(), false);
        reset.Allow(flame, 0x138);
        reset.Registers[4] = 0xDEADBEEF;
        reset.Registers[5] = 0x12345678;
        reset.Registers[6] = uint.MaxValue;
        reset.Execute(0x80013B68, 0x80013B7C);
        check(reset.WordStoreCount == 78 && Enumerable.Range(0, 78).All(i => reset.ReadWord(flame + (uint)(4 * i)) == 0),
            "actual fresh loader arguments, JAL delay and native fill helper clear exactly78 owned words");
        check(reset.ReadWord(flame - 4) == 0xA5A5A5A5 && reset.ReadWord(flame + 0x138) == 0xA5A5A5A5 &&
            reset.ReadWord(0x80078764) == 0 && reset.ReadWord(0x80078768) == 0,
            "native reset clears active/duration state without touching either adjacent word");

        foreach (uint active in new uint[] { 0, 1, uint.MaxValue })
        {
            var selector = new FreshSceneMachine();
            selector.Code(0x80058DB4, executable.AsSpan(0x495B4, 0x10));
            // This scalar entry is immediately after a separately bound GTE
            // transfer. No GTE execution or flag provenance is inferred here.
            selector.Code(0x80058E74, executable.AsSpan(0x49674, 0xC));
            selector.Code(0x80059260, executable.AsSpan(0x49A60, 0x24));
            byte[] values = new byte[0xA0];
            scene.AsSpan(0x10, 8).CopyTo(values.AsSpan(0x10));
            scene.AsSpan(0x78, 8).CopyTo(values.AsSpan(0x18));
            BinaryPrimitives.WriteUInt32LittleEndian(values.AsSpan(0x9C), active);
            selector.Map(flame, values, false);
            selector.Registers[31] = 0xDEADBEEF;
            selector.Registers[18] = ~active;
            selector.Execute(0x80058DB4, 0x80058DC4);
            selector.Execute(0x80058E74, 0x80058E80);
            check(selector.Registers[31] == flame && selector.Registers[18] == active,
                "actual renderer base and delayed active load select the source global, not caller-labelled metadata");
            selector.Registers[1] = 0;
            selector.Registers[2] = 0x5678;
            selector.Registers[5] = 0x1234;
            selector.Execute(0x80059260, 0x80059284);
            int at = active == 0 ? 0x10 : 0x78;
            check(selector.Registers[3] == BinaryPrimitives.ReadUInt32LittleEndian(scene.AsSpan(at)) &&
                selector.Registers[4] == BinaryPrimitives.ReadUInt32LittleEndian(scene.AsSpan(at + 4)),
                "actual branch delay selects normal descriptor0 only for zero active; nonzero reads unavailable13 without a null guard");
        }

        foreach (uint duration in new uint[] { 0, 1, 1000, uint.MaxValue })
        foreach (uint random in new uint[] { 0, 1, 0xA5C37E91 })
        foreach (int path in new[] { 0, 1 })
        {
            var start = new FreshSceneMachine();
            start.Map(flame, Enumerable.Repeat((byte)0xA5, 0xA4).ToArray(), true);
            start.SeedWord(0x80078768, duration);
            if (path == 0)
            {
                start.Code(0x80049A68, executable.AsSpan(0x3A268, 0xC));
                start.Map(0x1000, new byte[4], true);
                start.Registers[17] = 0x1000;
                start.Registers[2] = 0;
                start.Execute(0x80049A68, 0x80049A74);
                check(start.Registers[16] == 1 && start.ReadWord(0x1000) == 1,
                    "first native start's branch delay establishes s0=1 on its supported entry");
            }
            uint pc = path == 0 ? 0x80049AD8u : 0x80049CE4u;
            start.Code(pc, executable.AsSpan((int)(pc - 0x8000F800), 0x34));
            start.Registers[2] = random;
            start.Execute(pc, pc + 0x34);
            check(start.ReadWord(0x80078764) == (duration == 0 ? 0u : 1u) &&
                start.ReadWord(0x80078768) == duration && start.ReadByte(0x80078763) == (random & 1),
                "both actual native starts retain zero duration and derive active solely from duration despite unrelated random-bit store");
        }

        var update = new FreshSceneMachine();
        update.Code(0x80048D18, executable.AsSpan(0x39518, 0x40));
        update.Map(0x80078768, new byte[4], false);
        update.Map(0x1000, new byte[0xA0], true);
        update.Registers[29] = 0x1000;
        update.Execute(0x80048D18, 0x80048EB4);
        check(update.Registers[17] == 0x80078768 && update.ReadWord(0x80078768) == 0,
            "native zero-duration branch leaves before frame-step subtraction; no checkpoint or indirect writer closure is inferred");
    }

    private static void SpatialBuckets(byte[] executable, NativeId65FreshSceneLayout scene,
        Action<bool, string> check)
    {
        var machine = new FreshSceneMachine();
        machine.Code(0x8005270C, executable.AsSpan(0x42F0C, 0x40));
        machine.Map(0x2000, new byte[0x58], true);
        machine.Map(0x8000, scene.CopyScene().AsSpan(scene.BucketOffset, 0x2000), false);
        machine.Map(0x80075778, new byte[] { 0x00, 0x80, 0x00, 0x00 }, false);
        for (int y = 0; y < 32; y++)
        for (int x = 0; x < 32; x++)
        foreach (int edge in new[] { 0, 8191 })
        foreach (int bank in new[] { 0, 1 })
        {
            machine.SeedWord(0x200C, (uint)(x * 8192 + edge));
            machine.SeedWord(0x2010, (uint)(y * 8192 + edge));
            machine.Registers[1] = bank == 0 ? uint.MaxValue : 0;
            machine.Registers[4] = 0x2000;
            machine.Execute(0x8005270C, 0x8005274C);
            int index = x + 32 * y + bank * 1024;
            check(machine.Registers[2] == 0x8000 + index * 4 && machine.ReadWord(machine.Registers[2]) == 0 &&
                (machine.ReadByte(0x2034) | machine.ReadByte(0x2035) << 8) == index,
                "actual native bounded coordinate indexing reaches every owned zero bucket in both banks");
        }
    }

    private static void HeaderFade(byte[] executable, byte[] scene, Action<bool, string> check)
    {
        // Scalar subpaths between the source GTE transfer instructions. These
        // tests never claim to emulate the GTE or generate the native inputs.
        foreach (int input in Enumerable.Range(1, 1023))
        {
            var machine = new FreshSceneMachine();
            machine.Code(0x800215B0, executable.AsSpan(0x11DB0, 0x14));
            machine.Code(0x800215DC, executable.AsSpan(0x11DDC, 8));
            machine.Registers[2] = (uint)input;
            machine.Execute(0x800215B0, 0x800215C4);
            machine.Execute(0x800215DC, 0x800215E4);
            check(machine.Registers[18] == (uint)(((1024 - input) * 4 >> 9) << 22),
                "actual positive native fade arithmetic selects only CLUT rows0..7");
        }
        foreach (uint input in new uint[] { 1024, 1025, 4096, int.MaxValue })
        {
            var machine = new FreshSceneMachine();
            machine.Code(0x800215B0, executable.AsSpan(0x11DB0, 0x14));
            machine.Code(0x80021730, executable.AsSpan(0x11F30, 0xC));
            machine.Map(0x1000, new byte[0x1C], false);
            machine.Registers[31] = 0x1000;
            machine.Registers[2] = input;
            machine.Execute(0x800215B0, 0x8002173C);
            check(machine.Registers[18] == 0,
                "native upper branch resets fade offset rather than selecting an out-of-bank CLUT for input>=1024");
        }
        foreach (int input in new[] { -32768, -4097, -4096, -4095, -2049, -2048, -2047, -1, 0 })
        foreach (int context in new[] { -1, 0, 1 })
        {
            var machine = new FreshSceneMachine();
            machine.Code(0x80021674, executable.AsSpan(0x11E74, 0x34));
            machine.Map(0x1000, new byte[0x24], false);
            machine.Registers[31] = 0x1000;
            machine.Registers[1] = unchecked((uint)context);
            machine.Registers[2] = unchecked((uint)(input - 0x400));
            machine.Execute(0x80021674, 0x800216A8, 0x8002173C);
            int expected = context <= 0 || input >= -2048 ? 0 : input <= -4096 ? 4094 : -(input + 2048) * 2;
            check(machine.Registers[18] == expected,
                "actual alternate native fade path yields zero or bounded low UV offset without an extra CLUT row");
        }
        foreach (uint offset in Enumerable.Range(0, 8).Select(i => (uint)i << 22).Concat(new uint[] { 2, 256, 4094 }))
        {
            var machine = new FreshSceneMachine();
            machine.Code(0x80022294, executable.AsSpan(0x12A94, 0x2C));
            machine.Map(0x1000, scene.AsSpan(0x80, 8), false);
            machine.Map(0x2000, new byte[0x28], true);
            machine.Registers[4] = 0x1000;
            machine.Registers[5] = 0xFFFF;
            machine.Registers[6] = 0x1234;
            machine.Registers[7] = 0xABCD;
            machine.Registers[18] = offset;
            machine.Registers[24] = 0x2000;
            machine.Execute(0x80022294, 0x80021D58);
            uint source = BinaryPrimitives.ReadUInt32LittleEndian(scene.AsSpan(0x80));
            check(machine.ReadWord(0x200C) == ((source + offset) & 0xFFFF0000) + 0xFFFF &&
                (machine.ReadWord(0x2018) >> 16) == BinaryPrimitives.ReadUInt16LittleEndian(scene.AsSpan(0x86)),
                "actual specular output preserves fixed page and selects only the owned CLUT bank despite dynamic UV replacement");
        }
    }

    private static void HeaderCopies(byte[] executable, byte[] scene, Action<bool, string> check)
    {
        const uint basis = 0x80100000;
        var machine = new FreshSceneMachine();
        machine.Code(0x80013B88, executable.AsSpan(0x4388, 0xEC));
        machine.Map(basis, scene, false);
        machine.Code(0x8001377C, executable.AsSpan(0x3F7C, 0x3C));
        machine.Map(0x800778D8, new byte[16], true);
        machine.Map(0x800756D0, new byte[] { 0x91, 0x7E, 0xC3, 0xA5 }, false);
        machine.Registers[16] = basis;
        machine.Execute(0x8001377C, 0x800137B4);
        for (int i = 0; i < 3; i++)
            check(machine.ReadWord(0x800778D8 + (uint)(i * 4)) == BinaryPrimitives.ReadUInt32LittleEndian(scene.AsSpan(i * 4)),
                "actual ordinary fallback reads authored spawn coordinate words without entering other transition branches");
        check(machine.ReadWord(0x800778E4) == scene[14], "native spawn heading is the unsigned byte at scene+E");
        var fields = new Dictionary<int, uint> { [0] = 0x800786D8, [1] = 0x80075EF8,
            [13] = 0x800786E0, [14] = 0x800756F0 };
        for (int i = 3; i <= 12; i++) fields.Add(i, 0x8007850C + (uint)((i - 3) * 8));
        foreach (uint global in fields.Values) machine.Map(global, new byte[8], true);
        machine.Registers[16] = basis + 0x10;
        // Execute the real first Tiledef copy up to the following memset call.
        // The separately bound call's delay slot advances s0 by8; neither its
        // scratch destination nor its implementation is claimed here.
        machine.Execute(0x80013B88, 0x80013BA4);
        machine.Registers[16] = basis + 0x18;
        machine.Execute(0x80013BAC, 0x80013C74);
        foreach (var (index, global) in fields)
        {
            check(machine.ReadWord(global) == BinaryPrimitives.ReadUInt32LittleEndian(scene.AsSpan(0x10 + index * 8)) &&
                machine.ReadWord(global + 4) == BinaryPrimitives.ReadUInt32LittleEndian(scene.AsSpan(0x14 + index * 8)),
                "actual native copy installs exact header words; unused slot2 is not spuriously installed");
        }
    }
}

/// <summary>A small exact-owner MIPS fixture, not a native CPU/GPU runtime.</summary>
internal sealed class FreshSceneMachine
{
    internal const uint ReturnPc = 0x800FFFF0;
    internal readonly uint[] Registers = new uint[32];
    private readonly Dictionary<uint, uint> _code = [];
    private readonly Dictionary<uint, byte> _memory = [];
    private readonly HashSet<uint> _writable = [];
    private uint _hi;
    internal int WordStoreCount { get; private set; }
    internal int HalfwordReadCount { get; private set; }
    internal uint LastHalfwordReadAddress { get; private set; }

    internal void Code(uint pc, ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length % 4 != 0 || bytes.Length > 0x2000 || pc % 4 != 0)
            throw new InvalidDataException("Unsupported source code fixture owner.");
        for (int i = 0; i < bytes.Length; i += 4)
            _code.Add(pc + (uint)i, BinaryPrimitives.ReadUInt32LittleEndian(bytes[i..]));
    }

    internal void Map(uint address, ReadOnlySpan<byte> bytes, bool writable)
    {
        if (bytes.Length > 0x8000 || _memory.Count + bytes.Length > 0x10000)
            throw new InvalidDataException("Fixture memory owner exceeds bounded scene budget.");
        for (int i = 0; i < bytes.Length; i++) _memory.Add(checked(address + (uint)i), bytes[i]);
        if (writable) Allow(address, bytes.Length);
    }

    internal void Allow(uint address, int length)
    {
        if (length <= 0 || length > 0x8000) throw new InvalidDataException("Fixture writable bound.");
        for (int i = 0; i < length; i++)
        {
            uint at = checked(address + (uint)i);
            if (!_memory.ContainsKey(at)) throw new InvalidDataException("Writable fixture storage has no owner.");
            _writable.Add(at);
        }
    }

    internal void Protect(uint address, int length)
    {
        for (int i = 0; i < length; i++) _writable.Remove(address + (uint)i);
    }

    internal byte ReadByte(uint address) => _memory.TryGetValue(address, out byte value)
        ? value : throw new InvalidDataException($"Native read has no byte owner at {address:X8}.");

    internal uint ReadWord(uint address)
    {
        if (address % 4 != 0) throw new InvalidDataException("Unaligned native fixture read.");
        uint value = 0;
        for (int i = 0; i < 4; i++) value |= (uint)ReadByte(address + (uint)i) << (8 * i);
        return value;
    }

    private void WriteByte(uint address, byte value)
    {
        if (!_writable.Contains(address)) throw new InvalidDataException($"Native write crossed an exact byte owner at {address:X8}.");
        _memory[address] = value;
    }

    private void WriteWord(uint address, uint value)
    {
        if (address % 4 != 0) throw new InvalidDataException("Unaligned native fixture write.");
        for (int i = 0; i < 4; i++) WriteByte(address + (uint)i, (byte)(value >> (8 * i)));
        WordStoreCount++;
    }

    internal void SeedWord(uint address, uint value) => WriteWord(address, value);

    internal void Execute(uint pc, uint stop, uint alternateStop = 0)
    {
        uint? branch = null;
        (int Register, uint Value)? load = null;
        for (int steps = 0; steps < 4096; steps++)
        {
            if (pc == stop || (alternateStop != 0 && pc == alternateStop))
            {
                if (branch != null || load != null) throw new InvalidDataException("Pending native delay at fixture stop.");
                return;
            }
            if (!_code.TryGetValue(pc, out uint word)) throw new InvalidDataException($"Unbound native instruction {pc:X8}.");
            int rs = (int)(word >> 21) & 31, rt = (int)(word >> 16) & 31, rd = (int)(word >> 11) & 31;
            int shift = (int)(word >> 6) & 31;
            short immediate = (short)word;
            uint? nextBranch = null;
            (int Register, uint Value)? nextLoad = null, write = null;
            uint address = unchecked(Registers[rs] + (uint)immediate);
            switch (word >> 26)
            {
                case 0:
                    switch (word & 63)
                    {
                        case 0: write = (rd, Registers[rt] << shift); break;
                        case 2: write = (rd, Registers[rt] >> shift); break;
                        case 3: write = (rd, unchecked((uint)((int)Registers[rt] >> shift))); break;
                        case 4: write = (rd, Registers[rt] << (int)(Registers[rs] & 31)); break;
                        case 6: write = (rd, Registers[rt] >> (int)(Registers[rs] & 31)); break;
                        case 8: nextBranch = Registers[rs]; break;
                        case 0x10: write = (rd, _hi); break;
                        case 0x19: _hi = (uint)(((ulong)Registers[rs] * Registers[rt]) >> 32); break;
                        case 0x1B:
                            if (Registers[rt] == 0) throw new InvalidDataException("Unsupported native fixture division by zero.");
                            _hi = Registers[rs] % Registers[rt];
                            break;
                        case 0x20: write = (rd, Add(Registers[rs], Registers[rt])); break;
                        case 0x21: write = (rd, unchecked(Registers[rs] + Registers[rt])); break;
                        case 0x22: write = (rd, Subtract(Registers[rs], Registers[rt])); break;
                        case 0x23: write = (rd, unchecked(Registers[rs] - Registers[rt])); break;
                        case 0x24: write = (rd, Registers[rs] & Registers[rt]); break;
                        case 0x25: write = (rd, Registers[rs] | Registers[rt]); break;
                        case 0x27: write = (rd, ~(Registers[rs] | Registers[rt])); break;
                        case 0x2A: write = (rd, (int)Registers[rs] < (int)Registers[rt] ? 1u : 0u); break;
                        case 0x2B: write = (rd, Registers[rs] < Registers[rt] ? 1u : 0u); break;
                        default: throw new InvalidDataException("Unsupported native fixture SPECIAL.");
                    }
                    break;
                case 1:
                    if (rt is not (0 or 1)) throw new InvalidDataException("Unsupported native REGIMM.");
                    nextBranch = ((int)Registers[rs] < 0) == (rt == 0)
                        ? unchecked(pc + 4 + (uint)(immediate * 4)) : pc + 8;
                    break;
                case 2: nextBranch = ((pc + 4) & 0xF0000000) | ((word & 0x3FFFFFF) << 2); break;
                case 3:
                    write = (31, pc + 8);
                    nextBranch = ((pc + 4) & 0xF0000000) | ((word & 0x3FFFFFF) << 2);
                    break;
                case 4: nextBranch = Registers[rs] == Registers[rt] ? unchecked(pc + 4 + (uint)(immediate * 4)) : pc + 8; break;
                case 5: nextBranch = Registers[rs] != Registers[rt] ? unchecked(pc + 4 + (uint)(immediate * 4)) : pc + 8; break;
                case 6: nextBranch = (int)Registers[rs] <= 0 ? unchecked(pc + 4 + (uint)(immediate * 4)) : pc + 8; break;
                case 7: nextBranch = (int)Registers[rs] > 0 ? unchecked(pc + 4 + (uint)(immediate * 4)) : pc + 8; break;
                case 8: write = (rt, Add(Registers[rs], unchecked((uint)immediate))); break;
                case 9: write = (rt, address); break;
                case 10: write = (rt, (int)Registers[rs] < immediate ? 1u : 0u); break;
                case 11: write = (rt, Registers[rs] < unchecked((uint)immediate) ? 1u : 0u); break;
                case 12: write = (rt, Registers[rs] & (ushort)immediate); break;
                case 13: write = (rt, Registers[rs] | (ushort)immediate); break;
                case 15: write = (rt, (uint)(ushort)immediate << 16); break;
                case 32: nextLoad = (rt, unchecked((uint)(sbyte)ReadByte(address))); break;
                case 35: nextLoad = (rt, ReadWord(address)); break;
                case 36: nextLoad = (rt, ReadByte(address)); break;
                case 37:
                    if (address % 2 != 0) throw new InvalidDataException("Unaligned native halfword read.");
                    nextLoad = (rt, (uint)(ReadByte(address) | ReadByte(address + 1) << 8));
                    HalfwordReadCount++;
                    LastHalfwordReadAddress = address;
                    break;
                case 40: WriteByte(address, (byte)Registers[rt]); break;
                case 41:
                    if (address % 2 != 0) throw new InvalidDataException("Unaligned native halfword write.");
                    WriteByte(address, (byte)Registers[rt]);
                    WriteByte(address + 1, (byte)(Registers[rt] >> 8));
                    break;
                case 43: WriteWord(address, Registers[rt]); break;
                default: throw new InvalidDataException("Unsupported native fixture instruction.");
            }
            if (load is { } retired && retired.Register != 0 && write?.Register != retired.Register)
                Registers[retired.Register] = retired.Value;
            if (write is { } changed && changed.Register != 0) Registers[changed.Register] = changed.Value;
            Registers[0] = 0;
            if (branch != null && nextBranch != null) throw new InvalidDataException("Native branch in delay slot.");
            pc = branch ?? pc + 4;
            branch = nextBranch;
            load = nextLoad;
        }
        throw new InvalidDataException("Native fixture exceeded instruction budget.");
    }

    private static uint Add(uint a, uint b)
    {
        long value = (long)(int)a + (int)b;
        if (value < int.MinValue || value > int.MaxValue) throw new InvalidDataException("Native signed ADD overflow.");
        return unchecked((uint)value);
    }

    private static uint Subtract(uint a, uint b)
    {
        long value = (long)(int)a - (int)b;
        if (value < int.MinValue || value > int.MaxValue) throw new InvalidDataException("Native signed SUB overflow.");
        return unchecked((uint)value);
    }

    internal static void SelfTest(Action<bool, string> check, Action<Action, string> reject)
    {
        var machine = new FreshSceneMachine();
        uint[] words = [0x8C220000, 0x24430000, 0x24440000, 0x10000001, 0x8C250000, 0x24A60000, 0x24A70000, 0x03E00008, 0];
        byte[] code = new byte[words.Length * 4];
        for (int i = 0; i < words.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(code.AsSpan(i * 4), words[i]);
        machine.Code(0x80000000, code);
        machine.Map(0x1000, new byte[] { 0x78, 0x56, 0x34, 0x12 }, false);
        machine.Registers[1] = 0x1000;
        machine.Registers[2] = 0xAAAA;
        machine.Registers[5] = 0xBBBB;
        machine.Registers[31] = ReturnPc;
        machine.Execute(0x80000000, ReturnPc);
        check(machine.Registers[3] == 0xAAAA && machine.Registers[4] == 0x12345678 &&
            machine.Registers[6] == 0xBBBB && machine.Registers[7] == 0x12345678,
            "fresh-scene oracle honors load delay and load in a taken branch delay slot");
        reject(() => machine.ReadWord(0x1004), "fresh-scene oracle rejects unmapped reads");
    }
}
