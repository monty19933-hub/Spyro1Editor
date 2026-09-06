using System.Buffers.Binary;
using System.Security.Cryptography;

internal static class NativeFreshDiscScalarCases
{
    private const uint Base = NativeFreshDiscScalarMachine.ExecutableBase;
    private const uint Title = 0x80075564;
    private const string OriginalSha = "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998";
    private const string BootstrapSha = "0f6996babd64e815529df648fd7136f478c42f8187567d71439462df12b931ec";
    private sealed record Pin(string Name, uint Pc, string Sha, bool Code, string Hex)
    {
        internal byte[] Bytes { get; } = Convert.FromHexString(Hex);
    }

    // Independent original bytes/hashes from the reviewed Python audits. The
    // dispatch table and cycle slice are additionally pinned because bootstrap
    // executes them too. Data owners are never accepted for instruction fetch.
    private static readonly Pin[] Pins =
    [
        new("bootstrap-8001389C", 0x8001389C, "398098cc79b53e156f12866918d55207d3bfd172fc2c9802d9a0adfa84ccdec1", true,
            "0780023cd857428c0c001124060051106666023c092b010c212000000880013c488c31ac6666023c0780043c6c59848c6766423418008200c3170400fbff84241040000083180800231862008010030021104300401002002900441401000224"),
        new("bootstrap-800138FC", 0x800138FC, "bf56b83df3c1f8fc9881cc7dabc0c7f530952c528df83aab05cabbdf791a89a7", true,
            "0780033ce078638c0780013c905622ac0880013ca48c22ac0780013cc05823ac0880013c"),
        new("bootstrap-8001399C", 0x8001399C, "c3f8c8884a05b4a01cd1c48f4c6a2b824a7f250a5555b8d4875c54535e6e6d81", true,
            "0780023cd056428c0780013c905620ac0880013ca48c20ac"),
        new("bootstrap-8002D718", 0x8002D718, "f517cdf8f03a2369a5655f9ea09ad5471ba7bb36c3ebd5e87f3951fb38e956f9", true,
            "0780033cec58638c0000000034006010801003002110430040100200211044000780013cb45822acf6ff42243700422c0780013cf05824ac030040100600822807004014000000000780023c6c59428c0780013cb45822acfab5000800000000"),
        new("bootstrap-8002D7AC", 0x8002D7AC, "517c266ff1bd5d66bfbe8fba5a515ef8110da4ef620edb5b873cb7cec82a53e0", true,
            "0080033c12006334010002240780013cd85722ac0780013c645822ac0780013cd05620ac0780013cb05620ac0780013c9c5722ac0780013c906e23ac0780013c805820ac00b60008000000000780013c"),
        new("bootstrap-8002D810", 0x8002D810, "c273b09cfbd7304d1f9a65454781c9f68dae23d022aa93772b77cdb07dbfebf5", true,
            "e8ffbd2703000224150082101000bfaf040082280500401002000224080082100000000037b600080000000004000224240082106300022437b6000800000000"),
        new("bootstrap-8005A470", 0x8005A470, "7dba88624efe091d8b34a22b8874a2d12ab577858f7583a324d82f11d14d5c4f", true,
            "0780033c6c59638c000000006400622c97044010801003000180013c21082200a413228c000000000800400000000000"),
        new("bootstrap-8005A898", 0x8005A898, "fe4669229aedbbc8ff698d57a237327af36025aaeda0ff5cc2e1c806fd23e50b", true,
            "0980023ccc9e42240880013cd88522ac0880023ca47242240780013ccc5822ac0880023c54da42240780013c345722ac0980023c688f42240780013ce45822ac0880023c207e42240780013cbc5622ac0880023c2caf42240780013ca05722ac0880023c68b042240780013ca85822ac0880023cacb042240780013c4c5722ac0880023c4cd042240780013cd85822acb86d010800000000"),
        new("bootstrap-8005B6E0", 0x8005B6E0, "19566edf84d04523d6cc943feb5107817297413669d3838b10ef868db744151d", true,
            "0880023cd885428c0880013cdc8522ac0800e00300000000"),
        new("bootstrap-800155E4", 0x800155E4, "48c5db51e3fde2d17c86c91474b6d9ee9b733ba5ea92374a1eb031074bebce50", true,
            "0780023cb458428c0780013c6c5922ac1c69010c000000006666023c0780053c6c59a58c676642341800a2000780023c6459428c0780013c885622acc3170500104800008320090023208200ffff82244018020021186200401803000780013c5c5922ac8010040021104400401002002328a200211065000780013c645922ac"),
        new("bootstrap-80015664", 0x80015664, "24510d685f716e44f8462ba42f8cd4a5c27128f9e650198ce58675678a782501", true,
            "0f00001200000000001102000780043c906b848c0180053ca013a58c0880013c210822001ca7268c0880013c2108220018a7278c58020224a659000c1000a2af0780023c6458428ced58000801004224"),
        new("bootstrap-8005B95C", 0x8005B95C, "87aee6de9ebb5229353f2dc7d635e2c86348d8ccac653a746a872c085ee8e9df", true,
            "07801c3c64529c27"),
        new("entering-confronting-title-argument", 0x800197B4, "c6dd511143766c328719b3a750dea2ba2d33abc0dcb51be4431e432e382f5670", true,
            "3c00a228030040100400022404008210801003003f0002240800a214801003000780013c21082200f0f7268c0180053ce00aa524026600083800a4273800a4270780013c21082200f0f7268c0180053cf40aa524f58b010c00000000"),
        new("inventory-title-argument", 0x8001BF80, "30226d62c601cf4be2eb97a815c563d2694b3eac9173c8bfd4851c4f2a0f5caa", true,
            "40100300211043004010020021105300801002000780013c21082200f0f7248c0780103c1057108e6b60000c12000724"),
        new("world-text-title-argument", 0x8003C390, "7fb2744a30d86f68aced79bc557c9fd37b09b9b58e810763f85027243e35ce1d", true,
            "0000828e000000000000428c46009792801002000780013c21082200f0f7328cfcf00008ff00f032"),
        new("title-string-length", 0x8006276C, "67eba1fda7d9cdea44c7e0341d218d166b95f8baf32341890895a84f25308943", true,
            "0400801421180000e589010821100000010063240000829000000000fcff401401008424211060000800e00300000000"),
        new("glyph-first-character-null-test", 0x800181AC, "034ae948304e0579db72674888dfd220723e51f4fa1917d93589ce5bda36c866", true,
            "d0ffbd271400b1af218880001000b0af2180a0001c00b3af2198c0002000b4af21a0e0001800b2af2800bfaf2400b5af000023924000b58f8d00601001001224ff0063302000022479006210"),
        new("glyph-next-character-null-test", 0x80018408, "0329e8ccea54ce3b5cd299cbcf7dfa45bc02cf612f04cf8ca10280668443ea74", true,
            "01003126000023920000000075ff6014000000000780023c"),
        new("world-text-uppercase-glyph-selector", 0x8003C538, "db5ddecc9b7b73e0c1981a83569763a3491cdca6fe5588d4f0dd0fcab86b94ce", true,
            "5d00c01b4400a7af21b040020000c39220000224ff0064304e008210bfff62241a00422c03004010000000005cf10008690184244c0004240780023ccc58428c0000000009f84000"),
        new("neighbor-count-indexed-consumer", 0x8001BC10, "e96a04140d4cd3a377a559954d892af8bd1b7054143290c350616e9cbb472f92", true,
            "0780023c4457428c0780053c5c55a5240780013c210822005c5522900780103c1057108e1a004018212000000780063c78ccc6240780023cb858428c00000000c010020021104400ff00423040100200211046000000438401007326401003002110430043120200460002a20780023c4457428c0c0084242110450000004290000000002a106202eaff401458001026"),
        new("inventory-world-index-initializer", 0x8002C74C, "103a43ace286dac2d6946578bb8ca477a70816d3848b2b7f737d8245dc7e6895", true,
            "aa2a023c0780033c6459638cabaa4234180062000780013ccc5720ac0780013cd45620ac03000224c31f03000780013cd85722ac10400000231803010780013c445723ac"),
        new("inventory-world-navigation-bounds", 0x8002EC04, "9d77ad1406169e08fff3f9124d15038a66ed45dbde47a1b33b0cfa5164bccb87", true,
            "0780033c4457638c000000001a0060184010030021104300401002000880013c21082200728e22900000000014004010050062280780023c8073428c00000000008042300c004010212800000780023cd461428c10000624380044909e56010c21380000400002240780013cd45622ac0780033c4457638c0000000005006228180040104010030021104300401002000880013c210822007e8e22900000000010004010000000000780023c8073428c00000000002042300a004010212800000780023cd461428c10000624380044909e56010c21380000c0ff02240780013cd45622ac"),
        new("inventory-full-motion-step", 0x8002ECE8, "1a2720a2e5e54b19de9db8a60960031dcd967dfa219c7f9f8bdaf7c03c7a0c76", true,
            "0780033cd456638c0000000027006010000000000780023ccc57428c00000000211843000780013ccc5723ac0500601440fe04240780013cd45620ac65bb0008000000000e006414c0010224c0ff02240780013cd45622ac0780023c4457428cc00103240780013ccc5723ac010042240780013c445722ac65bb0008000000000a006214400002240780033c4457638c0780013cd45622ac0780013ccc5724acffff63240780013c445723ac"),
        new("level-title-table", 0x8006F7F0, "9a14304e3998c23e1edddebf7f83b22c4086d5821a6185b3b5a196abf502996e", false,
            "78550780fc010180f0010180e401018070550780d401018078550780c8010180bc010180b0010180a001018090010180785507808001018074010180680101805c0101804c010180785507803c0101803001018024010180180101800c01018078550780fc000180ec000180dc00018068550780d0000180c0000180b4000180a4000180940001808400018064550780"),
        new("preceding-inventory-rotation-counts", 0x8007555C, "aa2df93dbb751d2e2d5a1678a343fc980b8ddce821dcabe99918ae9a34e357b8", false,
            "080c0d0b0c0a0c00"),
        new("aligned-placeholder-before-next-string", 0x80075564, "daaf62de76f04e09a55d751f98b97c440bd2dba3f4d9d55a6e93ce8446266f92", false,
            "41000000"),
        new("separately-referenced-Jacques", 0x80075568, "519110bfeb97a61de653a7146d508f029c4a01777d00e0d5450372f75e01172f", false,
            "4a41435155455300"),
        new("source-trig-read-owner", 0x8006CC78, "7167a6246b5738b9028a66103a5732873dcf069460d08285bd30d22ecce299da", false,
            "0010ff0ffb0ff50fec0fe10fd40fc40fb10f9c0f850f6c0f500f310f110fee0ec80ea10e770e4b0e1c0eec0db90d850d4e0d150dda0c9d0c5e0c1e0cdb0b970b500b080bbf0a730a260ad80988093709e4088f083a08e3078b073207d7067c061f06c20564050505a5044404e30381031f03bc025902f50191012d01c900650000009bff37ffd3fe6ffe0bfea7fd44fde1fc7ffc1dfcbcfb5bfbfbfa9cfa3efae1f984f929f9cef875f81df8c6f771f71cf7c9f678f628f6daf58df541f5f8f4b0f469f425f4e2f3a2f363f326f3ebf2b2f27bf247f214f2e4f1b5f189f15ff138f112f1eff0cff0b0f094f07bf064f04ff03cf02cf01ff014f00bf005f001f000f001f005f00bf014f01ff02cf03cf04ff064f07bf094f0b0f0cff0eff012f138f15ff189f1b5f1e4f114f247f27bf2b2f2ebf226f363f3a2f3e2f325f469f4b0f4f8f441f58df5daf528f678f6c9f61cf771f7c6f71df875f8cef829f984f9e1f93efa9cfafbfa5bfbbcfb1dfc7ffce1fc44fda7fd0bfe6ffed3fe37ff9bff00006500c9002d019101f5015902bc021f038103e3034404a50405056405c2051f067c06d70632078b07e3073a088f08e40837098809d809260a730abf0a080b500b970bdb0b1e0c5e0c9d0cda0c150d4e0d850db90dec0d1c0e4b0e770ea10ec80eee0e110f310f500f6c0f850f9c0fb10fc40fd40fe10fec0ff50ffb0fff0f"),
        new("native-100-entry-initializer-dispatch", 0x800113A4, "fe23676eb71d88e2e5c79ef87bbf196c3333fa9ff27a9acd5d38b7e21b81b03d", false,
            "a0a4058038a5058088a50580d8a5058028a60580e0b60580e0b6058078a60580e0b60580c8a6058018a70580b0a7058048a8058098a8058030a9058080a90580e0b60580e0b60580e0b60580e0b6058028aa0580c0aa058058ab0580f0ab058040ac058090ac0580e0b60580e0b60580e0b60580e0b6058038ad0580d0ad058020ae058070ae0580c0ae058010af0580e0b60580e0b60580e0b60580e0b60580b8af058050b00580a0b00580f0b0058040b1058090b10580e0b60580e0b60580e0b60580e0b6058038b20580d0b2058020b3058070b30580c0b3058010b40580e0b60580e0b60580e0b60580e0b60580b8b4058050b50580a0b50580f0b5058040b60580e0b60580e0b60580e0b60580e0b60580e0b60580e0b60580e0b60580e0b60580e0b60580e0b60580e0b60580e0b60580e0b60580e0b60580e0b60580e0b60580e0b60580e0b60580e0b60580e0b60580e0b60580e0b60580e0b60580e0b60580e0b60580e0b60580e0b60580e0b60580e0b60580e0b60580e0b60580e0b60580e0b60580e0b6058090b60580"),
        new("native-next-level-cycle-wrap", 0x8002D6B4, "3082af139dfeab1f7eb2de4c0697254fbd1dac506a54e59d50af89a0035bff43", true,
            "0780023cb458428c00000000f6ff42243700422c030040140a0002240780013cb45822ac"),
    ];
    private static readonly (uint Start, int Length)[] CodeOwners =
        Pins.Where(p => p.Code).Select(p => (p.Pc, p.Bytes.Length)).ToArray();
    private static readonly (int Offset, string Before, string After)[] BootstrapPatches =
    [
        (0x1CA8, "e0b60580", "98a80580"),
        (0x1DF44, "3700422c", "3800422c"),
        (0x1E034, "37b6000800000000", "1c0684af880680af"),
        (0x40C0, "6666023c", "005e023c")
    ];

    internal static void Run(Action<bool, string> check, Action<Action, string> reject)
    {
        var executable = Fixture();
        RequirePins(executable);
        check(Pins.Length == 30, "Thirty exact code/data pins, including bootstrap dispatch and cycle owners.");
        foreach (var pin in Pins)
        {
            var changed = executable.ToArray();
            changed[checked((int)(pin.Pc - Base))] ^= 1;
            reject(() => RequirePins(changed), "Changed scalar certificate: " + pin.Name);
        }
        MachineBoundaries(executable, check, reject);
        Bootstrap(executable, check);
        Titles(executable, check, reject);
        Console.WriteLine("NATIVE_ID65_FRESH_DISC_SCALAR_OK pins=30 retailClassificationPairs=175 " +
            "directSelectionPairs=60 titleConsumers=144 navigationCases=48 motionTicksPerCase=14 " +
            "nativeCpu=false helperBodies=false gpu=false runtime=false");
    }

    internal static void RequireOriginalExecutable(ReadOnlySpan<byte> executable, Action<bool, string> check)
    {
        check(executable.Length == 0x66000 && Sha(executable) == OriginalSha,
            "Scalar rebinding requires the exact complete original executable.");
        RequirePins(executable);
        check(true, "All thirty scalar code/data certificates rebound to original executable.");
        var after = PatchBootstrap(executable);
        check(Sha(after) == BootstrapSha, "Exact four-window bootstrap executable afterimage.");
        foreach (var patch in BootstrapPatches)
            Convert.FromHexString(patch.Before).CopyTo(after, patch.Offset);
        check(after.AsSpan().SequenceEqual(executable), "Bootstrap inverse preserves complete executable complement.");
        Console.WriteLine("NATIVE_ID65_FRESH_DISC_SCALAR_SOURCE_OK pins=30 originalExecutable=true runtime=false");
    }

    private static string Sha(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static ReadOnlySpan<byte> Slice(byte[] exe, uint address, int length) =>
        exe.AsSpan(checked((int)(address - Base)), length);
    private static uint U32(byte[] exe, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Slice(exe, address, 4));
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }

    private static byte[] Fixture()
    {
        var bytes = new byte[0x66000];
        var occupied = new bool[bytes.Length];
        foreach (var pin in Pins)
        {
            Require(pin.Pc >= Base && (ulong)pin.Pc + (uint)pin.Bytes.Length <= (ulong)Base + (uint)bytes.Length &&
                Sha(pin.Bytes) == pin.Sha, "Invalid embedded scalar certificate.");
            int offset = checked((int)(pin.Pc - Base));
            for (int i = 0; i < pin.Bytes.Length; i++)
            {
                Require(!occupied[offset + i] || bytes[offset + i] == pin.Bytes[i], "Conflicting scalar certificate overlap.");
                bytes[offset + i] = pin.Bytes[i];
                occupied[offset + i] = true;
            }
        }
        return bytes;
    }

    private static void RequirePins(ReadOnlySpan<byte> exe)
    {
        Require(exe.Length == 0x66000, "Wrong scalar executable size.");
        foreach (var pin in Pins)
        {
            var actual = exe.Slice(checked((int)(pin.Pc - Base)), pin.Bytes.Length);
            Require(actual.SequenceEqual(pin.Bytes) && Sha(actual) == pin.Sha, "Changed scalar certificate: " + pin.Name);
        }
    }

    private static byte[] PatchBootstrap(ReadOnlySpan<byte> exe)
    {
        var after = exe.ToArray();
        foreach (var patch in BootstrapPatches)
        {
            var before = Convert.FromHexString(patch.Before);
            var replacement = Convert.FromHexString(patch.After);
            Require(before.Length == replacement.Length && after.AsSpan(patch.Offset, before.Length).SequenceEqual(before),
                "Bootstrap patch preimage or width.");
            replacement.CopyTo(after, patch.Offset);
        }
        return after;
    }

    private static NativeFreshDiscScalarMachine New(byte[] exe) => new(exe, CodeOwners);

    private static NativeFreshDiscScalarMachine BootstrapMachine(byte[] exe)
    {
        var m = New(exe);
        uint[] globals =
        [
            0x800757D8, 0x8007596C, 0x80078C48, 0x800778E0, 0x80075690, 0x80078CA4, 0x800758C0,
            0x800756D0, 0x800758EC, 0x800758F0, 0x800758B4, 0x80075880, 0x801FFF10, 0x80075964,
            0x8007595C, 0x80075688, 0x80075864, 0x800756B0, 0x8007579C, 0x80076E90, 0x800785D8,
            0x800785DC, 0x800758CC, 0x80075734, 0x800758E4, 0x800756BC, 0x800757A0, 0x800758A8,
            0x8007574C, 0x800758D8
        ];
        foreach (uint address in globals) m.Word(address, 0xA5A5A5A5);
        m.Map(0x800113A4, Slice(exe, 0x800113A4, 400));
        m.R[29] = 0x801FFF18;
        m.R[31] = 0x800FFFF0;
        return m;
    }

    private static (bool Flight, bool ClassifierReached) Classify(byte[] exe, uint level, uint globalState,
        Action<bool, string> check)
    {
        var m = BootstrapMachine(exe);
        m.SeedWord(0x8007596C, level);
        m.SeedWord(0x800757D8, globalState); // Global transition state, NOT player state 80078AD0.
        m.Word(0x80078AD0, 12); // Deliberately independent: classifier must not read player state.
        uint stop = m.Run(0x8001389C, 0x8004AC24, 0x800138FC, 0x8001399C);
        if (stop == 0x8004AC24)
        {
            check(m.R[31] == 0x800138B8 && m.R[4] == 0, "Native reset JAL/delay entry arguments.");
            // Do not synthesize the reset helper. This is a SEPARATE returned-entry
            // witness: the actual next LUI must kill the poisoned v0.
            m.R[2] = 0xDEADBEEF;
            stop = m.Run(0x800138B8, 0x800138FC, 0x8001399C);
        }
        bool flight = stop == 0x800138FC;
        m.Run(stop, flight ? 0x80013914u : 0x800139B4u);
        check(m.Read(0x80075690) == (flight ? 1u : 0u) && m.Read(0x80078CA4) == (flight ? 1u : 0u) &&
            !m.Reads.Any(r => r.Address == 0x80078AD0), "Actual classifier flag stores; player state is not the guard.");
        return (flight, m.Trace.Contains(0x800138C0));
    }

    private static void Bootstrap(byte[] original, Action<bool, string> check)
    {
        var patched = PatchBootstrap(original);
        var ids = Enumerable.Range(10, 55).Where(i => U32(original, 0x800113A4 + (uint)i * 4) != 0x8005B6E0).ToArray();
        check(ids.SequenceEqual(Enumerable.Range(0, 35).Select(i => (i / 6 + 1) * 10 + i % 6)),
            "All 35 retail IDs derived from actual native 100-entry initializer dispatch.");
        foreach (int level in ids)
        {
            foreach (uint state in new uint[] { 0, 1, 7, 12, uint.MaxValue })
            {
                bool before = Classify(original, (uint)level, state, check).Flight;
                bool after = Classify(patched, (uint)level, state, check).Flight;
                check(before == after && after == (level is 15 or 25 or 35 or 45 or 55),
                    "Original/patched native classification agrees for every retail ID and transition-state witness.");
            }
            uint originalTarget = U32(original, 0x800113A4 + (uint)level * 4);
            foreach (var exe in new[] { original, patched })
            {
                var m = BootstrapMachine(exe);
                m.SeedWord(0x8007596C, (uint)level);
                check(m.Run(0x8005A470, originalTarget) == originalTarget, "Every original initializer target preserved, including JR delay.");
            }
        }
        check(Classify(original, 65, 1, check) == (true, true), "Original ID65 state1 takes flight classifier.");
        check(Classify(patched, 65, 1, check) == (false, true), "Patched ID65 normal transition enters normal classifier.");
        check(Classify(patched, 65, 12, check) == (true, false),
            "Global transition-state800757D8=12 still bypasses classifier: foreign entry explicitly excluded.");
        check(Slice(original, 0x8002D6C4, 4).SequenceEqual(Convert.FromHexString("3700422c")) &&
            Slice(patched, 0x8002D6C4, 4).SequenceEqual(Slice(original, 0x8002D6C4, 4)), "Separate next-level bound remains unchanged.");
        foreach (var exe in new[] { original, patched })
        {
            var m = BootstrapMachine(exe);
            m.SeedWord(0x800758B4, 65);
            m.Run(0x8002D6B4, 0x8002D6D8);
            check(m.Read(0x800758B4) == 10, "Native next-level cycle still wraps65 to10; it does not offer65.");
            m = BootstrapMachine(exe);
            m.SeedWord(0x8007596C, 65);
            uint target = ReferenceEquals(exe, original) ? 0x8005B6E0u : 0x8005A898u;
            check(m.Run(0x8005A470, target) == target, "ID65 exact original/patched initializer dispatch.");
        }
        var boot = BootstrapMachine(patched);
        boot.Run(0x8005B95C, 0x8005B964);
        check(boot.R[28] == 0x80075264, "Actual native boot GP setup.");
        boot.R[4] = 1;
        boot.Run(0x8002D810, 0x8002D8DC);
        check(boot.Read(0x80075880) == 1 && boot.Read(0x800758EC) == 0, "Native hidden cheat1 activation and owned delay stores.");
        for (uint world = 1; world <= 6; world++)
        for (uint digit = 0; digit < 10; digit++)
        {
            uint level = world * 10 + digit;
            foreach (var exe in new[] { original, patched })
            {
                var m = BootstrapMachine(exe);
                m.SeedWord(0x800758EC, world);
                m.SeedWord(0x8007596C, 13);
                m.R[4] = digit;
                bool accepted = m.Run(0x8002D718, 0x8002D778, 0x8002D7E8) == 0x8002D778;
                check(accepted == (ids.Contains((int)level) || (!ReferenceEquals(exe, original) && level == 65)),
                    "Native direct tens/units selection changes only ID65.");
            }
        }
        var incoming = BootstrapMachine(patched);
        incoming.Run(0x8002D7AC, 0x8002D800);
        check(incoming.Read(0x800757D8) == 1 && incoming.Read(0x80075864) == 1 && incoming.Read(0x800756D0) == 0,
            "Native post-helper transition setters; preceding helper bodies not executed.");
        incoming = BootstrapMachine(patched);
        incoming.SeedWord(0x800758B4, 65);
        incoming.Run(0x800155E4, 0x800155FC);
        check(incoming.Read(0x8007596C) == 65 && incoming.Read(0x800785D8) == 0x80089ECC &&
            incoming.Read(0x800785DC) == 0x80089ECC, "Actual incoming ID65/default copy-buffer setters.");
        (uint Address, uint Value)[] callbacks =
        [
            (0x800758CC, 0x800872A4), (0x80075734, 0x8007DA54), (0x800758E4, 0x80088F68),
            (0x800756BC, 0x80087E20), (0x800757A0, 0x8007AF2C), (0x800758A8, 0x8007B068),
            (0x8007574C, 0x8007B0AC), (0x800758D8, 0x8007D04C)
        ];
        check(callbacks.All(p => incoming.Read(p.Address) == p.Value), "All eight source Town callback assignments, not callback execution.");
        incoming.Run(0x800155FC, 0x80015664);
        check(incoming.Read(0x8007595C) == 5 && incoming.Read(0x80075964) == 35, "Native ID65 world5/continuous35 identity arithmetic.");
    }

    private static void Titles(byte[] exe, Action<bool, string> check, Action<Action, string> reject)
    {
        var table = Slice(exe, 0x8006F7F0, 0x90).ToArray();
        for (uint index = 0; index < 36; index++)
        {
            uint expected = BinaryPrimitives.ReadUInt32LittleEndian(table.AsSpan((int)index * 4, 4));
            foreach (var witness in new[] { (65u, 5u, 0x80010AF4u), (63u, 3u, 0x80010AE0u) })
            {
                var m = New(exe);
                m.Map(0x8006F7F0, table);
                m.R[3] = index; m.R[4] = witness.Item2; m.R[5] = witness.Item1; m.R[29] = 0x80100000;
                m.Run(0x800197B4, 0x80019808); // BEFORE formatter JAL.
                check(m.R[6] == expected && m.R[5] == witness.Item3, "Actual entering/confronting title/format arguments.");
                check(m.Reads.SequenceEqual(new[] { (0x8006F7F0 + index * 4, 4) }), "Only the bounded native title-table row is read.");
            }
            var inventory = New(exe);
            inventory.Map(0x8006F7F0, table);
            inventory.Word(0x80075710, 0x80110000);
            inventory.R[3] = index / 6; inventory.R[19] = index % 6;
            inventory.Run(0x8001BF80, 0x800181AC); // Actual JAL + delay; stop BEFORE glyph body.
            check(inventory.R[4] == expected && inventory.R[7] == 0x12 && inventory.R[16] == 0x80110000 &&
                inventory.R[31] == 0x8001BFB0, "Actual inventory table pointer and load/JAL delay arguments.");
            var worldText = New(exe);
            worldText.Map(0x8006F7F0, table);
            var moby = new byte[0x58];
            BinaryPrimitives.WriteUInt32LittleEndian(moby, 0x80120000);
            moby[0x46] = 0xA5;
            worldText.Map(0x80110000, moby);
            worldText.Word(0x80120000, index);
            worldText.R[20] = 0x80110000;
            worldText.Run(0x8003C390, 0x8003C3F0); // Actual J/delay, no subsequent body.
            check(worldText.R[18] == expected && worldText.R[16] == 0xA5, "Native world-text props pointer and delay-slot state.");
        }
        check(U32(exe, 0x8006F7F0 + 35 * 4) == Title, "Exact original ID65 title table points at four-byte owner.");
        var foreign = New(exe);
        foreign.Map(0x8006F7F0, table);
        foreign.R[3] = 36; foreign.R[4] = 5; foreign.R[5] = 65;
        RefusesAt(() => foreign.Run(0x800197B4, 0x80019808), "Unowned scalar access 8006F880/4.", reject,
            "Foreign title index36 cannot borrow the next owner.");

        (byte[] Bytes, byte[] Characters)[] strings =
        [
            ([65, 0, 0, 0], [65]), ([76, 65, 66, 0], [76, 65, 66]),
            ([0, 65, 66, 0], []), ([76, 0, 66, 0], [76])
        ];
        foreach (var row in strings)
        {
            var m = New(exe);
            m.Map(Title, row.Bytes); m.R[4] = Title; m.R[31] = 0x800FFFF0;
            m.Run(0x8006276C, 0x800FFFF0);
            check(m.R[2] == row.Characters.Length, "Native strlen returns owned character count.");
            var expectedReads = Enumerable.Range(0, row.Characters.Length + 1).Select(i => (Title + (uint)i, 1)).ToArray();
            check(m.Reads.SequenceEqual(expectedReads), "Native strlen reads exactly through first NUL.");
            m = New(exe);
            m.Map(Title, row.Bytes);
            m.Map(0x80100000, new byte[0x100], true);
            m.R[4] = Title; m.R[29] = 0x80100080;
            uint at = m.Run(0x800181AC, 0x800181EC, 0x8001841C);
            List<byte> characters = [];
            while (at == 0x800181EC)
            {
                Require(characters.Count < 4, "Finite glyph-control fixture limit.");
                characters.Add(checked((byte)m.R[3]));
                // Separate native next-character entry, NOT an invented return
                // from omitted glyph allocation, matrix or GPU/helper bodies.
                at = m.Run(0x80018408, 0x800181EC, 0x8001841C);
            }
            check(characters.SequenceEqual(row.Characters), "Native first/next control slices choose exact title characters.");
            check(m.Reads.Where(p => p.Address >= Title && p.Address < Title + 4).SequenceEqual(expectedReads),
                "Glyph control terminates within four-byte owner, before Jacques.");
        }
        var nullString = New(exe);
        nullString.R[31] = 0x800FFFF0;
        nullString.Run(0x8006276C, 0x800FFFF0);
        check(nullString.R[2] == 0 && nullString.Reads.Count == 0, "Actual native null-pointer strlen path.");
        var unterminated = New(exe);
        unterminated.Map(Title, "LAB"u8); unterminated.R[4] = Title; unterminated.R[31] = 0x800FFFF0;
        RefusesAt(() => unterminated.Run(0x8006276C, 0x800FFFF0), "Unowned scalar access 80075567/1.", reject,
            "Unterminated title cannot read neighboring owner.");
        foreach (byte character in "LAB"u8)
        {
            var m = New(exe); m.Map(Title, new[] { character }); m.R[22] = Title;
            m.Run(0x8003C544, 0x8003C570); // BEFORE callback load/JALR.
            check(m.R[4] == character + 0x169, "Actual uppercase world-text glyph selector.");
        }
        for (uint index = 0; index < 36; index++)
        {
            var m = New(exe);
            m.Word(0x80075964, index);
            foreach (uint address in new uint[] { 0x800757CC, 0x800756D4, 0x800757D8, 0x80075744 })
                m.Word(address, 0xDEADBEEF);
            m.Run(0x8002C74C, 0x8002C790);
            check(m.Read(0x80075744) == index / 6, "Actual signed multiply/divide6 initializer keeps normal world0..5.");
        }
        Navigation(exe, check);
        Neighbors(exe, check, reject);
        var after = exe.ToArray();
        "LAB\0"u8.CopyTo(after.AsSpan(0x65D64, 4));
        check(after.AsSpan(0x65D5C, 8).SequenceEqual(exe.AsSpan(0x65D5C, 8)) &&
            after.AsSpan(0x65D68, 8).SequenceEqual("JACQUES\0"u8), "Detached four-byte title leaves both neighboring owners exact.");
        exe.AsSpan(0x65D64, 4).CopyTo(after.AsSpan(0x65D64, 4));
        check(after.AsSpan().SequenceEqual(exe), "Title inverse preserves the entire detached fixture executable.");
        reject(() => Require("BLANK\0"u8.Length <= 4, "Title crosses Jacques owner."), "Long title cannot borrow Jacques.");
    }

    private static void Navigation(byte[] exe, Action<bool, string> check)
    {
        for (uint world = 0; world < 6; world++)
        foreach (uint pressed in new uint[] { 0, 0x8000, 0x2000, 0xA000 })
        foreach (byte visited in new byte[] { 0, 1 })
        {
            NativeFreshDiscScalarMachine Entry()
            {
                var m = New(exe);
                m.Word(0x80075744, world); m.Word(0x80077380, pressed);
                m.Word(0x800757CC, 0); m.Word(0x800756D4, 0);
                m.Map(0x80078E70, Enumerable.Repeat(visited, 48).ToArray());
                return m;
            }
            var m = Entry();
            bool left = m.Run(0x8002EC04, 0x8002EC50, 0x8002EC7C, 0x8002EC84) == 0x8002EC50;
            check(left == (world > 0 && (pressed & 0x8000) != 0 && visited != 0), "Native left-world guard before audio call.");
            m = Entry(); m.R[3] = world; // Explicit native-reloaded v1 entry witness.
            bool right = m.Run(0x8002EC7C, 0x8002ECC4, 0x8002ECE8) == 0x8002ECC4;
            check(right == (world < 5 && (pressed & 0x2000) != 0 && visited != 0), "Native right-world guard before audio call.");
            m = Entry();
            // Exact post-audio scalar slices only. No audio helper execution or
            // fabricated call return; poisoned v0 exposes accidental dependency.
            if (left) { m.R[2] = 0xDEADBEEF; m.Run(0x8002EC68, 0x8002EC80); }
            if (right) { m.R[2] = 0xBAD0C0DE; m.Run(0x8002ECDC, 0x8002ECE8); }
            for (int tick = 0; tick < 14; tick++)
            {
                m.Run(0x8002ECE8, 0x8002ED94);
                check(m.Read(0x80075744) < 6, "Every source-native scalar motion tick stays in normal world0..5.");
            }
            int expected = (int)world + (right ? 1 : left ? -1 : 0);
            check(m.Read(0x80075744) == expected && m.Read(0x800757CC) == 0 && m.Read(0x800756D4) == 0,
                "Native14-tick motion settles once; right overrides left when both input bits qualify.");
        }
    }

    private static void Neighbors(byte[] exe, Action<bool, string> check, Action<Action, string> reject)
    {
        for (uint world = 0; world < 6; world++)
        {
            var m = New(exe);
            m.Map(0x8007555C, Slice(exe, 0x8007555C, 8));
            m.Map(0x8006CC78, Slice(exe, 0x8006CC78, 0x200));
            m.Map(0x80110000, new byte[13 * 0x58], true);
            m.Word(0x80075744, world); m.Word(0x80075710, 0x80110000); m.Word(0x800758B8, 127);
            m.R[19] = 0; // Explicit first counter; caller and complete glyph helper are not executed.
            m.Run(0x8001BC10, 0x8001BCA0);
            var reads = m.Reads.Where(p => p.Address >= 0x8007555C && p.Address < Title).ToArray();
            check(reads.Length > 0 && reads.All(p => p.Address == 0x8007555C + world && p.Width == 1),
                "Both native neighbor count reads use only the correct owned byte.");
            check(m.Writes.Count == exe[checked((int)(0x8007555C + world - Base))],
                "Actual native counter loop has exactly the retained count of row stores.");
        }
        var foreign = New(exe);
        foreign.Map(0x8007555C, Slice(exe, 0x8007555C, 8));
        foreign.Word(0x80075744, 10); foreign.Word(0x80075710, 0x80110000);
        RefusesAt(() => foreign.Run(0x8001BC10, 0x8001BCA0), "Unowned scalar access 80075566/1.", reject,
            "Foreign world10 refuses before borrowing title bytes; no all-state title safety claim.");
    }

    private static void RefusesAt(Action action, string exactMessage, Action<Action, string> reject, string label) =>
        reject(() =>
        {
            try { action(); }
            catch (InvalidDataException error)
            {
                // A different refusal is a fixture failure, not an expected rejection.
                if (error.Message != exactMessage) throw new Exception(label + ": wrong refusal: " + error.Message, error);
                throw;
            }
        }, label);

    private static void MachineBoundaries(byte[] exe, Action<bool, string> check, Action<Action, string> reject)
    {
        var pending = New(exe);
        pending.Map(Title, "LAB\0"u8); pending.R[4] = Title; pending.R[31] = 0x800FFFF0;
        RefusesAt(() => pending.Run(0x80062780, 0x80062784), "Pending delay at scalar stop.", reject,
            "Stopping immediately after native LBU cannot discard the load delay.");
        var unowned = New(exe);
        reject(() => unowned.Run(0x80018100, 0x80018104), "Even in-image zero bytes outside code pins cannot execute.");
        var dataAsCode = New(exe);
        reject(() => dataAsCode.Run(Title, Title + 4), "Pinned title data is not executable code.");
        var bounded = New(exe);
        bounded.R[31] = 0x800FFFF0;
        reject(() => bounded.RunBounded(0x8006276C, new uint[] { 0x800FFFF0 }, 1), "Finite scalar instruction budget is enforced.");
        var protectedOwner = New(exe);
        protectedOwner.Map(Title, "LAB\0"u8);
        reject(() => protectedOwner.SeedWord(Title, 0), "Readonly title owner cannot be seeded as writable.");
        reject(() => protectedOwner.Map(Title + 1, new byte[1]), "Overlapping scalar ownership refused.");
        reject(() => protectedOwner.Read(Title + 1), "Misaligned scalar LW refused.");
        check(protectedOwner.Writes.Count == 0, "Fixture seeds/refused writes are not native-store evidence.");
    }
}
