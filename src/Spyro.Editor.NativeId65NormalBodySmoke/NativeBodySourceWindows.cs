using Spyro.Editor.Core.Exporting;

/// <summary>
/// Actual source words are pinned independently below and re-compared against
/// the complete original EXE in source mode. Source-free execution establishes
/// only scalar fixture behavior; it cannot privately issue a source plan.
/// </summary>
internal static class NativeBodySourceWindows
{
    private const uint CopyAnchor = 0x80089ECC;
    private const uint DataBase = 0x123000; // Symbolic detached fixture base, never a published WAD assignment.
    private const int FixtureContinuousIndex = 3;
    private static readonly (uint Slot, uint Target)[] Callbacks =
    [
        (0x800758CC, 0x800872A4), (0x80075734, 0x8007DA54),
        (0x800758E4, 0x80088F68), (0x800756BC, 0x80087E20),
        (0x800757A0, 0x8007AF2C), (0x800758A8, 0x8007B068),
        (0x8007574C, 0x8007B0AC), (0x800758D8, 0x8007D04C)
    ];

    internal static void Run(Action<bool, string> check, Action<Action, string> reject)
    {
        NativeBodyMachine.SelfTest(check, reject);
        foreach (var pin in Pins)
            check(NativeBodyCases.Hash(Convert.FromHexString(pin.Hex)) == pin.Sha256,
                "source-free instruction fixture retains its independently recorded exact-source digest");
        Initializers(check, reject);
        EntryReset(check);
        CompanionAndAuxiliaryReset(check, reject);
        ColdBoot(check);
        byte[] header = new byte[0x800];
        foreach (var (at, value) in new[] { (0, 0x800), (4, 0xDE000), (8, 0xDE800), (12, 0x1000),
            (16, 0xDF800), (20, 0xF800), (24, 0xEF000), (28, 0x3000) })
            NativeBodyCases.Put(header, at, value);
        Requests(header, check);
    }

    internal static void RequireOriginalExecutable(byte[] executable, Action<bool, string> check)
    {
        check(executable.Length == 0x66000 && NativeBodyCases.Hash(executable) ==
            "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998",
            "pinned scalar windows are rebound to the complete exact original executable");
        foreach (var pin in Pins)
            check(executable.AsSpan(checked((int)(pin.Address - 0x8000F800)), pin.Hex.Length / 2)
                .SequenceEqual(Convert.FromHexString(pin.Hex)), "every scalar byte and delay slot equals actual original source");
    }

    internal static void ReadRequests(byte[] executable, byte[] body, NativeId65NormalBodyPlan plan,
        Action<bool, string> check, Action<Action, string> reject)
    {
        RequireOriginalExecutable(executable, check);
        Requests(body.AsSpan(0, 0x800).ToArray(), check);
        byte[] ram = Enumerable.Repeat((byte)0xA5, 0x81000).ToArray();
        byte[] overlay = plan.CopyEngineOverlay();
        // Explicit abstract completed writes below are fixture byte algebra.
        // They do not execute CD, prove async completion, or install a runtime.
        byte[] retainedCode = overlay.AsSpan(0, 0xF494).ToArray();
        byte[] installed = new byte[0xF494 + ram.Length];
        overlay.CopyTo(installed, 0);
        body.AsSpan(0, 0x800).CopyTo(installed.AsSpan(0xF494));
        check(installed.AsSpan(0, 0xF494).SequenceEqual(retainedCode) &&
            !installed.AsSpan(0xF494, 0x36C).SequenceEqual(overlay.AsSpan(0xF494)),
            "abstract header read overwrites the originally overwritten overlay tail, preserving every callback byte without indirect-lifetime approval");
        body.AsSpan(0x800, 0x80000).CopyTo(ram);
        body.AsSpan(0x80800, 0x5E000).CopyTo(ram);
        byte[] transfer = body.AsSpan(0x80800, 0x5E000).ToArray()
            .Concat(body.AsSpan(0x5E800, 0x21000).ToArray()).ToArray();
        check(ram.AsSpan(0, 0x7F000).SequenceEqual(transfer),
            "abstract second CD read preserves the actual prior VRAM residue through the DMA-only tail");
        byte[] beforeData = ram.AsSpan(0, 0x7F000).ToArray();
        body.AsSpan(0xDE800, 0x1000).CopyTo(ram);
        byte[] used = ram.AsSpan(0, 0x8C8).ToArray();
        body.AsSpan(0xDF800, 0xF800).CopyTo(ram.AsSpan(0x8C8));
        check(ram.AsSpan(0, 0x8C8).SequenceEqual(used) &&
            ram.AsSpan(0x8C8, 0xF800).SequenceEqual(body.AsSpan(0xDF800, 0xF800)),
            "model write preserves used data but intentionally replaces its discarded sector padding");
        check(!ram.AsSpan(0, 0x7F000).SequenceEqual(beforeData) && !plan.TransferTimingProven,
            "later data write invalidates earlier transfer source; no fixture grants permission to discard it before hardware completion");
        byte[] scene = body.AsSpan(NativeBodyCases.Get(body, 24), NativeBodyCases.Get(body, 28)).ToArray();
        scene.CopyTo(ram, 0x8C8 + 0xF800);
        check(ram.AsSpan(0x8C8 + 0xF800, scene.Length).SequenceEqual(scene),
            "scene follows the complete padded model allocation, not an assumed retail scene anchor");
        byte[] corruptOverlay = overlay.ToArray();
        corruptOverlay[0xF490] ^= 1;
        reject(() => plan.RequireExactPayloads(body, corruptOverlay),
            "an earlier overwrite of callback epilogue bytes cannot acquire source-phase authority");
    }

    private static void Initializers(Action<bool, string> check, Action<Action, string> reject)
    {
        var incoming = Machine();
        incoming.SeedWord(0x800758B4, 13);
        incoming.Execute(0x800155E4, 0x8005A470);
        check(incoming.ReadWord(0x8007596C) == 13 && incoming.Registers[31] == 0x800155FC,
            "incoming level is stored and native JAL/delay execute before resident initializer dispatch");
        incoming.Map(0x800113A4 + 13 * 4, NativeBodyMachine.Words(0x8005A898), false);
        incoming.Execute(0x8005A470, 0x800155FC);
        check(incoming.ReadWord(0x800785D8) == CopyAnchor && incoming.ReadWord(0x800785DC) == CopyAnchor,
            "source resident initializer installs the actual copy anchor before any incoming overlay is bound");
        foreach (var (slot, target) in Callbacks)
            check(incoming.ReadWord(slot) == target, "every poisoned callback is replaced by its exact source target");
        reject(() => incoming.Execute(0x800872A4, NativeBodyMachine.ReturnPc),
            "a stored callback pointer is not executable before the overlay code owner is present");

        foreach (uint id in new uint[] { 65, 100, uint.MaxValue })
        {
            var stale = Machine();
            stale.SeedWord(0x8007596C, id);
            stale.SeedWord(0x800785D8, 0x80123450);
            if (id == 65)
                stale.Map(0x800113A4 + 65 * 4, NativeBodyMachine.Words(0x8005B6E0), false);
            stale.Registers[31] = NativeBodyMachine.ReturnPc;
            stale.Execute(0x8005A470, NativeBodyMachine.ReturnPc);
            check(stale.ReadWord(0x800785DC) == 0x80123450 &&
                Callbacks.All(c => stale.ReadWord(c.Slot) == 0xA5A5A5A5),
                "retail ID65/default tail copies stale buffer state and does not issue any callback installation");
        }
        var missing = Machine();
        missing.SeedWord(0x8007596C, 13);
        reject(() => missing.Execute(0x8005A470, NativeBodyMachine.ReturnPc),
            "missing actual dispatch-table word cannot be replaced by a caller-provided profile label");
    }

    private static void EntryReset(Action<bool, string> check)
    {
        foreach (uint state in new uint[] { 0, 8, 11, 12, uint.MaxValue })
        {
            var machine = Machine();
            machine.SeedWord(0x800757D8, state);
            machine.Execute(0x80013B34, 0x80013B7C);
            check(machine.ReadWord(0x800757D8) == (state == 12 ? 12u : 0u) &&
                machine.ReadWord(0x8007579C) == (state == 12 ? 0xA5A5A5A5u : 1u),
                "native state12 preservation and other state resets execute both actual branch paths/delays");
            for (uint at = 0x800786C8; at < 0x80078800; at += 4)
                check(machine.ReadWord(at) == 0, "native call/helper zeroes each presentation word including superflame flag/timer");
            check(machine.ReadWord(0x800786C4) == 0xA5A5A5A5 && machine.ReadWord(0x80078800) == 0xA5A5A5A5,
                "normal presentation reset is exact, not authority to erase adjacent runtime owners");
            check(machine.ReadWord(0x800758CC) == 0xA5A5A5A5,
                "scene state reset does not silently supply absent engine callback initialization");
        }
    }

    private static void CompanionAndAuxiliaryReset(Action<bool, string> check, Action<Action, string> reject)
    {
        foreach (var (loadArgument, classifier) in new[] { (1u, 0u), (0u, 0u), (1u, 1u) })
        {
            var machine = Machine();
            const uint pool = 0x801C3BB8;
            machine.Map(pool, Enumerable.Repeat((byte)0xA5, 0x2000).ToArray(), true);
            machine.Map(pool + 0x2000, Enumerable.Repeat((byte)0xA5, 0x24).ToArray(), true);
            machine.SeedWord(0x800785F8, pool);
            machine.SeedWord(0x80075690, classifier);
            machine.SeedWord(0x800758CC, 0x800872A4);
            machine.Registers[21] = loadArgument;
            machine.Execute(0x8001441C, 0x800872A4, 0x80014490);
            bool spawn = loadArgument != 0 && classifier == 0;
            check(machine.LastStop == (spawn ? 0x800872A4u : 0x80014490u),
                "native companion spawn condition is the actual load argument and normal classifier");
            check(machine.ReadWord(0x80075824) == pool && machine.ReadWord(0x80075738) == pool &&
                machine.ReadByte(pool + 1) == 0xFF && machine.ReadWord(pool + 0x2000) == uint.MaxValue,
                "native pool pointers and terminal first-PETE-word sentinel are initialized before spawn");
            check(machine.ReadWord(pool + 0x2020) == 0xA5A5A5A5,
                "this reset does not invent ownership or initialize the renderer's later terminal lookahead value");
            if (spawn)
                check(machine.Registers[4] == 0x78 && machine.Registers[5] == 0 &&
                    machine.Registers[31] == 0x80014480,
                    "null-source class78 input and JALR delay are native, not fabricated placed data");
            for (int i = 0; i < 16; i++)
                check(machine.ReadWord(0x80078800 + (uint)(i * 0x24)) == 0,
                    "all shared companion auxiliary free markers are reset before mandatory allocation");
            for (int i = 0; i < 8; i++)
                check(machine.ReadByte(0x80077114 + (uint)(i * 0x18)) == 0,
                    "separate byte-marker reset remains distinct from rescue subfile resources");
        }
        var poolMachine = Machine();
        poolMachine.Registers[31] = NativeBodyMachine.ReturnPc;
        poolMachine.Execute(0x80058B68, NativeBodyMachine.ReturnPc);
        for (int i = 0; i < 16; i++)
        {
            poolMachine.Execute(0x80058AE8, NativeBodyMachine.ReturnPc);
            uint row = 0x80078800 + (uint)(i * 0x24);
            check(poolMachine.Registers[2] == row && poolMachine.ReadWord(row) == 0x11 &&
                poolMachine.ReadWord(row + 4) == 0x8006E308 && poolMachine.ReadWord(row + 0x20) == 0 &&
                poolMachine.ReadWord(row + 8) == 0xA5A5A5A5,
                "actual bounded auxiliary allocation initializes selected fields, not a fabricated full-record memset");
        }
        poolMachine.Execute(0x80058AE8, NativeBodyMachine.ReturnPc);
        check(poolMachine.Registers[2] == 0,
            "shared auxiliary allocation17 returns null; unlike scene Moby N+1 this has a native guard");
        reject(() => poolMachine.Execute(0x800524C4, NativeBodyMachine.ReturnPc),
            "auxiliary-pool proof is not silently promoted to unbound Moby allocator behavior");
    }

    private static void ColdBoot(Action<bool, string> check)
    {
        var machine = Machine();
        machine.Execute(0x8005B8E0, 0x8005B904);
        check(machine.WordStores == 0x53F8 / 4 && Callbacks.All(c => machine.ReadWord(c.Slot) == 0),
            "actual cold boot zeroes the complete exact BSS interval, not callback code or adjacent storage");
        check(machine.ReadWord(0x8007563C) == 0xA5A5A5A5 && machine.ReadWord(0x8007AA38) == 0xA5A5A5A5,
            "cold-boot zero loop owns neither preceding data nor incoming overlay first word");
    }

    private static void Requests(byte[] header, Action<bool, string> check)
    {
        foreach (uint loadOverlay in new uint[] { 0, 1 })
        {
            var machine = RequestMachine(header);
            machine.Registers[2] = FixtureContinuousIndex;
            machine.Registers[16] = loadOverlay;
            machine.Execute(0x80015664, 0x80016698, 0x800156A4);
            check(machine.LastStop == (loadOverlay == 0 ? 0x800156A4u : 0x80016698u),
                "actual zero mode skips the overlay request rather than loading an asserted engine profile");
            if (loadOverlay != 0)
                check(machine.Registers[5] == 0x8007AA38 && machine.Registers[6] == 0xF800 &&
                    machine.Registers[7] == 0x118E800 && machine.ReadWord(machine.Registers[29] + 0x10) == 600,
                    "native overlay request uses fixed code address, exact descriptor and call-delay timeout");
        }
        var headerRead = RequestMachine(header);
        headerRead.Execute(0x800156B4, 0x80016698);
        check(headerRead.Registers[5] == CopyAnchor && headerRead.Registers[6] == 0x800 &&
            headerRead.Registers[7] == DataBase, "actual initial header request reuses the overlay tail at copy anchor");
        var headerCopy = RequestMachine(header);
        headerCopy.Execute(0x800156FC, 0x80016958);
        check(headerCopy.Registers[4] == 0x80076C00 && headerCopy.Registers[5] == CopyAnchor &&
            headerCopy.Registers[6] == 0x1D0, "native header copy receives all descriptor/root/class words in its delay-slot length");
        foreach (var (pc, destination, length, offset) in new[]
        {
            (0x80015888u, CopyAnchor, 0x1000u, 0xDE800u),
            (0x800159FCu, CopyAnchor + 0x8C8, 0xF800u, 0xDF800u),
            (0x80015B80u, CopyAnchor + 0x8C8 + 0xF800, (uint)NativeBodyCases.Get(header, 28), 0xEF000u)
        })
        {
            var machine = RequestMachine(header);
            machine.Execute(pc, 0x80016698);
            check(machine.Registers[5] == destination && machine.Registers[6] == length &&
                machine.Registers[7] == DataBase + offset,
                "actual data/model/scene CD caller uses typed header owners and correct runtime-domain base");
        }
        var scene = RequestMachine(header);
        scene.Registers[19] = 0x80076C50;
        scene.Execute(0x80015B34, 0x800163BC);
        check(scene.ReadWord(0x800785E4) == CopyAnchor + 0x8C8 + 0xF800 &&
            scene.ReadWord(0x80078600) == 0xEF000 &&
            scene.ReadWord(0x80078604) == (uint)NativeBodyCases.Get(header, 28),
            "native scene anchor is model runtime plus model length, with separate entry-relative scene descriptor");
    }

    private static NativeBodyMachine RequestMachine(byte[] header)
    {
        var machine = Machine();
        machine.Map(0x800113A0, NativeBodyMachine.Words(0x8007AA38), false);
        machine.SeedWord(0x80076B90, 0x1234);
        machine.SeedWord(0x80075964, FixtureContinuousIndex);
        machine.SeedWord(0x800785DC, CopyAnchor);
        machine.SeedWord(0x800785E0, CopyAnchor + 0x8C8);
        machine.SeedWord(0x800785E4, CopyAnchor + 0x8C8 + 0xF800);
        machine.SeedWord(0x80078600, 0xEF000);
        machine.SeedWord(0x80078604, (uint)NativeBodyCases.Get(header, 28));
        uint table = 0x8007A718 + FixtureContinuousIndex * 16;
        machine.SeedWord(table, 0x118E800);
        machine.SeedWord(table + 4, 0xF800);
        machine.SeedWord(table + 8, DataBase);
        for (int i = 0; i < 0x1D0; i += 4)
            machine.SeedWord(0x80076C00 + (uint)i, unchecked((uint)NativeBodyCases.Get(header, i)));
        return machine;
    }

    private static NativeBodyMachine Machine()
    {
        var machine = new NativeBodyMachine();
        foreach (var pin in Pins)
            machine.Code(pin.Address, Convert.FromHexString(pin.Hex));
        machine.Map(0x8007563C, Enumerable.Repeat((byte)0xA5, 0x5400).ToArray(), true);
        machine.Map(0x801FF000, new byte[0x1000], true);
        machine.Registers[29] = 0x801FFF00;
        return machine;
    }

    private sealed record Pin(string Id, uint Address, string Hex, string Sha256);
    private static readonly Pin[] Pins =
    [
        new("entry", 0x800155E4, "0780023cb458428c0780013c6c5922ac1c69010c00000000", "c53e59924d4cb0ce0816bad8028aa1737121c1a3ce4d193965177301fedca31a"),
        new("dispatch", 0x8005A470, "0780033c6c59638c000000006400622c97044010801003000180013c21082200a413228c000000000800400000000000", "7dba88624efe091d8b34a22b8874a2d12ab577858f7583a324d82f11d14d5c4f"),
        new("initializer", 0x8005A898, "0980023ccc9e42240880013cd88522ac0880023ca47242240780013ccc5822ac0880023c54da42240780013c345722ac0980023c688f42240780013ce45822ac0880023c207e42240780013cbc5622ac0880023c2caf42240780013ca05722ac0880023c68b042240780013ca85822ac0880023cacb042240780013c4c5722ac0880023c4cd042240780013cd85822acb86d010800000000", "fe4669229aedbbc8ff698d57a237327af36025aaeda0ff5cc2e1c806fd23e50b"),
        new("tail", 0x8005B6E0, "0880023cd885428c0880013cdc8522ac0800e00300000000", "19566edf84d04523d6cc943feb5107817297413669d3838b10ef868db744151d"),
        new("overlay-read", 0x80015664, "0f00001200000000001102000780043c906b848c0180053ca013a58c0880013c210822001ca7268c0880013c2108220018a7278c58020224a659000c1000a2af", "37bc1454a17eee2024537b2a6ba6419b6b1ac141e68ea0d65522699e337673d7"),
        new("header-read", 0x800156B4, "000806240780043c906b848c0780023c6459428c0880053cdc85a58c001102000880013c2108220020a7278c58020224a659000c1000a2af", "338906189f294a358926aeaad67898f6ae26971e469ee7818d50e3f0fe942f6c"),
        new("header-copy", 0x800156FC, "0780113c006c3126212020020880103cdc8510260000058e565a000cd0010624", "9167b1789ce4e25ffe5a9ac38f0d5d7dd9ad716c7ba541998c8a207957ca1fcc"),
        new("data-read", 0x80015888, "0780043c906b848c0880053cdc85a58c0780023c6459428c0780063c0c6cc68c0780033c086c638c001102000880013c2108220020a7278c580202241000a2afa659000c21386700", "fb4abee00e8416f774fc8716428a4de64cb3a3e677a83c1f8573db7289e69a88"),
        new("model-read", 0x800159FC, "0780043c906b848c0880053ce085a58c0780023c6459428c0780063c146cc68c0780033c106c638c001102000880013c2108220020a7278c580202241000a2afa659000c21386700", "83ea25ec1c8183cfc9102d106ed5edad86807ce7c2f6e3606345bb367e7c7690"),
        new("scene-anchor", 0x80015B34, "0880023ce085428cc4ff648eccff658ec8ff668e0780033c6458638c21104400010063240880013ce48522ac0880013c048625ac0880013c008626ac0780013c645823acef58000800000000", "556eae60eabc2d6d06e0c7bf20086d0d60cdf371a7668041a58af7362e1b2760"),
        new("scene-read", 0x80015B80, "0780043c906b848c0880053ce485a58c0780023c6459428c0880063c0486c68c0880033c0086638c001102000880013c2108220020a7278c580202241000a2afa659000c21386700", "ea95a06cd941d702185504e4ed17ba17d927bbacfa7e8ffe9afcaeaf5aa2fa32"),
        new("reset", 0x80013B34, "0780033cd857638c0c00022409006210010002240780013cd85720ac0780013c9c5722ac0780013c0c5720ac0780013cc05620ac0880043cc886842421280000455a000c38010624", "1613a0f9202bade4e3568a1824b1daf3b1608cdd1a5bb3b95f111a424cceed27"),
        new("zero", 0x80016914, "2030c400fcffc620000085acfeff8614040084200800e00300000000", "572bd776ba509d54a3848293f5cf78731da439e3b203d100b9cb6fb2ec60decc"),
        new("pools", 0x8001441C, "0880023cf885428cff0003240780013c245822ac0780013c385722ac010043a00780033c2458638cffff0224da62010c002062ac0f00a012000000000780023c9056428c000000000a004014780004240780023ccc58428c0000000009f84000212800000780013c985822ac8051010c01000424", "0b54dc9bc7189e4b54bb874da4b2def42e3a3a4f26090f573b782dbe81c98881"),
        new("auxiliary", 0x80058AE8, "21300000110007240780083c08e308250880053c0088a5242118a000212000000000628c000000000a0040142110a000000067ac0880013c21082400048828ac0880013c21082400208820acd6620108000000002400a524240063240100c6241000c228eeff401424008424211000000800e003000000000800e003000080ac1c0202240880013c21082200008820acdcff4224fbff410400000000a80002240780013c21082200147120a0e8ff4224fbff4104000000000800e00300000000", "cc396c20757c749bebcf0f448e0654b3fed04131e4f51baa76ceefd6baa11879"),
        new("cold-boot", 0x8005B8E0, "0780023c405642240880033c38aa6324000040ac040042242b084300fcff201400000000", "7896dd616818db767dc8a988879948e6011c1b4c1600aff3762ec7dc645c1f48")
    ];
}
