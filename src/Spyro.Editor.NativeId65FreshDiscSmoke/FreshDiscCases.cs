using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;

internal static class FreshDiscCases
{
    internal static void SourceFree(Action<bool, string> check, Action<Action, string> reject)
    {
        foreach (var (length, wadLength, executableLba) in new[]
        {
            (0x1FB000, 0x6B31800, 54920),
            (0x1FB800, 0x6B32000, 54921)
        })
        {
            var geometry = NativeId65FreshDiscGeometry.Compile(length);
            check(!geometry.SourceCertified, "arithmetic cannot issue source or physical ownership");
            check(geometry.OverlayWadOffset == 0x6927000 && geometry.OverlayByteLength == 0xF800 &&
                geometry.BodyWadOffset == 0x6936800 && geometry.BodyByteLength == length,
                "complete appended overlay/body have separate native WAD extents");
            check(geometry.FinalWadByteLength == wadLength && geometry.FinalExecutableLba == executableLba &&
                geometry.FinalExecutableLba + 204 < 60000,
                "only the two exact body sizes determine the final live EXE LBA");
        }
        foreach (int length in new[] { -1, 0, 1, 0x1FAFFF, 0x1FB001, 0x1FB801, 0x200000, int.MaxValue })
            reject(() => NativeId65FreshDiscGeometry.Compile(length), "unsupported body grammar/overflow refuses");
        NativeId65FreshDiscPlan.RequireCurrentUntexturedShape(0x8C8, 0x1000, 0xDE000, 0xF800, 0x3000);
        check(NativeId65FreshDiscPlan.AcceptedBodyResourceProfile == "fresh-untextured-G3-two-triangle-data-8C8-v1",
            "disc issuer has an explicit narrow resource discriminator, not a total-size approval");
        reject(() => NativeId65FreshDiscPlan.RequireCurrentUntexturedShape(0xA38, 0x1000, 0xDE000, 0xF800, 0x3000),
            "future textured used end refuses despite identical data-sector and total body lengths");
        foreach (var shape in new[]
        {
            (0x8C8, 0x800, 0xDE000, 0xF800, 0x3000),
            (0x8C8, 0x1000, 0xDE001, 0xF800, 0x3000),
            (0x8C8, 0x1000, 0xDE000, 0xF000, 0x3000),
            (0x8C8, 0x1000, 0xDE000, 0xF800, 0x3800)
        })
            reject(() => NativeId65FreshDiscPlan.RequireCurrentUntexturedShape(
                shape.Item1, shape.Item2, shape.Item3, shape.Item4, shape.Item5), "foreign resource shape refuses");
        reject(() => NativeId65FreshDiscPlan.CaptureAsync("must-not-open.bin", null!, 19).GetAwaiter().GetResult(),
            "missing source-issued body refuses before file I/O, not IOException acceptance");
    }

    internal static async Task<NativeId65FreshDiscPlan> Source(string path, NativeId65NormalBodyPlan firstBody,
        NativeId65NormalBodyPlan secondBody, NativeId65NormalBodyPlan shortBody,
        Action<bool, string> check, Action<Action, string> reject)
    {
        check(firstBody.LevelDataSha256 == "b4abbdd09f794510d9b9341d7c7111ddd0b9068411bab30e70e7477b4f8c096c" &&
            secondBody.LevelDataSha256 == "1314b299759ad0f66eed054db853c18e8db6dabda7c56a28cabd3fd74ea39643",
            "the two preserved upstream body goldens remain untouched");
        var first = await NativeId65FreshDiscPlan.CaptureAsync(path, firstBody, 19);
        var repeat = await NativeId65FreshDiscPlan.CaptureAsync(path, firstBody, 19);
        var second = await NativeId65FreshDiscPlan.CaptureAsync(path, secondBody, 19);
        var shorter = await NativeId65FreshDiscPlan.CaptureAsync(path, shortBody, 19);
        check(shortBody.LevelDataSha256 == "6acc5144a313c3552406b43e0450aae6f9d81688381e25e4d922eb78072107ff" &&
            shortBody.CopyLevelData().Length == 0x1FB000 && shorter.FinalExecutableLba == 54920 &&
            shorter.FinalWadByteLength == 0x6B31800,
            "source-issued N=1 scene exercises the other actual body/terminal geometry");
        MusicMatrix(first.Executable, check, reject);
        foreach (int track in new[] { -1, 30, 35, 48, int.MaxValue })
            reject(() => NativeId65FreshDiscPlan.CaptureAsync(path, firstBody, track).GetAwaiter().GetResult(),
                "reserved/unknown tracks cannot bypass the unchanged built-in music selection guard");
        check(first.SourceCertified && first.BodyPlanSha256 == firstBody.PlanSha256 &&
            first.PlanSha256 == repeat.PlanSha256 && first.PlanSha256 != second.PlanSha256,
            "private exact upstream issuance preserves repeated and distinct authoring states");
        check(first.CopyBody().SequenceEqual(firstBody.CopyLevelData()) &&
            first.CopyOverlay().SequenceEqual(firstBody.CopyEngineOverlay()), "all issued body and overlay bytes retained");
        check(first.ExecutableBootstrapSha256 == "0f6996babd64e815529df648fd7136f478c42f8187567d71439462df12b931ec" &&
            first.ExistingMusicInputSha256 == "fa5fc7981188b78fa7d7b78facca64c1f79dadb107515e9146ad178ade39d442",
            "exact original bootstrap reaches the unchanged existing music source certificate");
        byte[] exe = first.CopyExecutable();
        check(exe.AsSpan(0x65D64, 4).SequenceEqual("LAB\0"u8) &&
            BinaryPrimitives.ReadUInt32LittleEndian(exe.AsSpan(0x6007C)) == 0x80075564,
            "fixed LAB title restores original pointer after the sequential intermediate stage");
        check(exe.AsSpan(0x65D68, 8).SequenceEqual("JACQUES\0"u8) &&
            exe.AsSpan(0x65D5C, 8).SequenceEqual(Convert.FromHexString("080C0D0B0C0A0C00")),
            "Jacques and the indexed preceding count table remain distinct exact owners");
        byte[] directory = first.CopyWadDirectory();
        check(BinaryPrimitives.ReadUInt32LittleEndian(directory.AsSpan(79 * 8)) == 0x6927000 &&
            BinaryPrimitives.ReadUInt32LittleEndian(directory.AsSpan(79 * 8 + 4)) == 0xF800 &&
            BinaryPrimitives.ReadUInt32LittleEndian(directory.AsSpan(80 * 8)) == 0x6936800 &&
            BinaryPrimitives.ReadUInt32LittleEndian(directory.AsSpan(80 * 8 + 4)) == 0x1FB800 &&
            directory.AsSpan(81 * 8, 8).SequenceEqual(Convert.FromHexString("0078C90500200000")),
            "only the exact reserved pair gains storage; occupied adjacent row81 survives");
        check(first.FinalExecutableLba == 54921 && first.FinalWadByteLength == 0x6B32000 &&
            !first.RuntimeVerified && !first.PublicationApproved && !first.CompleteNativeDependencyClosureProven &&
            !first.CheckpointOrForeignStateSupported && !first.NormalCreateBinEnabled,
            "actual physical byte production is not native, foreign-state or normal exporter approval");
        string immutableExe = Hash(exe);
        check(immutableExe == "3473dc92e087c87858434125d5fb1ec879450682e3be64c508de6805029ce7a2" &&
            Hash(directory) == "0a227cbf0d9f6b269ae7d41ce610129b0b48590519bbaaa2eac375e8818e23e0",
            "default full EXE and directory match independent raw afterimage oracle before C# implementation");
        exe[0] ^= 1;
        directory[0] ^= 1;
        check(Hash(first.CopyExecutable()) == immutableExe &&
            !first.CopyWadDirectory().SequenceEqual(directory), "exposed arrays cannot mutate captured authority");

        using var source = File.OpenRead(path);
        byte[] untouched = new byte[2352];
        source.ReadExactly(untouched);
        check(first.CreateExpectedRawSector(0, untouched).SequenceEqual(untouched) &&
            first.CreateOriginalRawSector(0, untouched).SequenceEqual(untouched),
            "even unchanged forward/inverse source sectors authenticate their private LBA index");
        byte[] corrupt = untouched.ToArray();
        corrupt[24] ^= 1;
        reject(() => first.CreateExpectedRawSector(0, corrupt), "off-scope caller bytes are never source authority");
        reject(() => first.CreateOriginalRawSector(0, corrupt), "inverse cannot hide off-scope corruption");
        reject(() => first.CreateExpectedRawSector(1, untouched), "authentic bytes supplied for the wrong LBA");
        foreach (int lba in new[] { -1, 281270, int.MaxValue })
            reject(() => first.CreateExpectedRawSector(lba, untouched), "invalid source sector index");
        reject(() => first.CreateExpectedRawSector(0, untouched[..^1]), "truncated sector refuses");
        reject(() => first.CreateExpectedRawSector(0, untouched.Concat(new byte[1]).ToArray()), "foreign sector suffix refuses");

        using var forwardHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var secondHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var shortHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var inverseHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var shortInverseHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        source.Position = 0;
        int changed = 0;
        int shortChanged = 0;
        for (int lba = 0; lba < 281270; lba++)
        {
            source.ReadExactly(untouched);
            byte[] after = first.CreateExpectedRawSector(lba, untouched);
            byte[] restored = first.CreateOriginalRawSector(lba, after);
            byte[] shortAfter = shorter.CreateExpectedRawSector(lba, untouched);
            byte[] shortRestored = shorter.CreateOriginalRawSector(lba, shortAfter);
            check(restored.SequenceEqual(untouched), "whole physical inverse restores exact original raw sector");
            check(shortRestored.SequenceEqual(untouched), "short-body inverse restores every raw sector including its excluded final gap sector");
            forwardHash.AppendData(after);
            secondHash.AppendData(second.CreateExpectedRawSector(lba, untouched));
            shortHash.AppendData(shortAfter);
            inverseHash.AppendData(restored);
            shortInverseHash.AppendData(shortRestored);
            if (!after.SequenceEqual(untouched))
                changed++;
            if (!shortAfter.SequenceEqual(untouched))
                shortChanged++;
            if (lba is 22 or 37 or 53874 or 53875 or 54078 or 54079 or 54919 or 54920 or 54921 or 55123 or 55124)
            {
                foreach (int offset in new[] { 12, 16, 18, 20, 24, 2072, 2076, 2351 })
                {
                    byte[] bad = after.ToArray();
                    bad[offset] ^= 1;
                    reject(() => first.CreateOriginalRawSector(lba, bad),
                        "inverse requires exact final payload/header/parity preimage before returning original Form1/Form2 bytes");
                    byte[] shortBad = shortAfter.ToArray();
                    shortBad[offset] ^= 1;
                    reject(() => shorter.CreateOriginalRawSector(lba, shortBad),
                        "short-body boundary and untouched final Form2 sector retain exact inverse guards");
                }
            }
        }
        check(source.ReadByte() == -1 && source.Position == 661547040 && source.Length == 661547040,
            "whole physical oracle requires actual final source EOF and guards");
        check(Convert.ToHexStringLower(inverseHash.GetHashAndReset()) ==
            "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37",
            "whole physical inverse restores old EXE and canonical Form2 gap, not only logical payloads");
        check(Convert.ToHexStringLower(shortInverseHash.GetHashAndReset()) ==
            "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37",
            "short body also completely restores the original full image");
        string firstHash = Convert.ToHexStringLower(forwardHash.GetHashAndReset());
        string otherHash = Convert.ToHexStringLower(secondHash.GetHashAndReset());
        string smallerHash = Convert.ToHexStringLower(shortHash.GetHashAndReset());
        check(changed == 1253 && firstHash == "1818fe50a132901b9ad670769d4967ccc0a5120897637efa2ad709b8f1d53188" &&
            otherHash == "40047bd8e40f23b522bfaa04dd72353013fbcfddbbd9ba6b1e357b87e8664343",
            "both complete raw afterimages match the independent oracle, including every unchanged sector");
        check(shortChanged == 1252 && smallerHash == "e6d5f2abd512814bac54f48b2d3bb3255d09dc4d5abfb6aef87ddf82ea43fc08" &&
            Hash(shorter.CopyIsoRootDirectory()) == "3af825567fb9a5425c5acc9addff76f69207b70c588352aeb492def548e944fb" &&
            Hash(shorter.CopyWadDirectory()) == "57c7f399a81b5748d8eb4c5562e8ba8fc6f1bf4b1df175831eb69d1ba7d83b95",
            "short-body full raw afterimage and both directories match independent source oracle");
        Console.WriteLine($"FRESH_DISC_AFTERIMAGE first={firstHash} second={otherHash} short={smallerHash} changed={changed}/{shortChanged}");
        return first;
    }

private static void MusicMatrix(NativeId65FreshExecutablePlan first,
        Action<bool, string> check, Action<Action, string> reject)
    {
        byte[] original = first.RevertExact(first.CopyExecutable());
        // Literal complete EXE goldens from the independent source-sector
        // Python oracle, captured before this producer compiled.
        (int Track, string Hash)[] expected =
        [
            (0, "8a0279771d10bf3e137aec25a6aea5caaadb3752771a8163ee461f6eacc3408c"),
            (1, "02221036d5a4437ac265d5fbc9d1a072c7f06092d6aa4cef55c78fb5a2f38bc2"),
            (2, "2c1fd87e9fa8ce5c5f16be53e738405cec90c0bfc10ac9cb8cf1ce979b2e5c57"),
            (3, "74a3587ab14dcf6b9b1dacc7bb229e1b8985156f90abdf1cc672a9af820b534c"),
            (4, "ee8dab608a9d1b07a2f13740b14845f03d595a5c082aa828802dc352fbfcaa76"),
            (5, "a1220510dd2b6fb3642570bea2d21c2b4787c37150ca3f52c045116c463cd6c2"),
            (6, "2f7eb449c154fa8cc213a982f7deb0930890fd540ad6c26fc84ba33cbce47bbf"),
            (7, "10945fe85c0c2556431898859a54e5bc6c68b63b8aec090d467ade18897dcac8"),
            (8, "c22c822a8c51bb88ecf5f6d069d03f21f6b69fef67efc427898cb055f308c800"),
            (9, "82cc350fca74f547bf16fa43477c162d22e4dd179214841be94e0e9e17abe462"),
            (10, "6e7db041c33a9c568571c2444a2c848e7bae6727bf1c131a54f1f56149a481cf"),
            (11, "aa8e66425c9b3bb0225514b4cc99ce9e47f1c6efe029162042bab26b6821a6df"),
            (12, "be2b3b6f175a7f58194899fbd530806fcb8197e2fac5ba2b48f381d5ab8998af"),
            (13, "89b04b01db0c369a2657c654598d148474818272850b32ab0f3a87691866362f"),
            (14, "1e26f28cc5f54e93ae5e7fc33350fe67e0ff01d15099e325d5ad0424166fd446"),
            (15, "ea71a336ede0d93684254262cc25f37b58106783ef0e5e891fab84d3860e2790"),
            (16, "dce4784f713fa2515f1a3f3f4f6c9185f5b4420ee2d600b37b33bcfacc5889de"),
            (17, "c1513e021ac23df638afd37629216d011d5cb36f714bfbbc53766eeb866e2f9e"),
            (18, "fb217efb3197f84f3e712515f563ce7f2c0598e985d01a8e578903c588d1542d"),
            (19, "3473dc92e087c87858434125d5fb1ec879450682e3be64c508de6805029ce7a2"),
            (20, "b08cb4e9ee2cacba2ed4d6be89165c9cd8e305fda85e24905f260ffa9695dadf"),
            (21, "6b764e64cfc38c5d4c95a12c908cf46850c72a5c019579dd23b1a4bc8ef22213"),
            (22, "a9e1e53ea3eecd83adc6bbe65edbf9e5536199d2915b3b6618877297cb16becf"),
            (23, "f3a37144de15abe5db9593744d265e00f0bb2e529a561d0108511bb3aa2835d0"),
            (24, "80b85462143420531af4fb5de1860ccb7d0b705688820016099c578a9657dd32"),
            (25, "adf7a8dd29e0795372b68b9f959813549a4a852bea16a93d6d75decc14e64b7c"),
            (26, "75e313eddb90a72fcee52b47799a95e4d9e386702661b4837d2d4e7cc8972adc"),
            (27, "a2ff7227dd190154aa8b60bd9d0095c36a8a771068657999655789ecd08da962"),
            (28, "9d0aee0cec7b171e7cf4c692ee1968a3cb49662e7ab7ee3cc428e8bc0116ed18"),
            (29, "daacf2d78ad8ddd7b9af040afe9c65bac4eb6f7ebb601efd6e6d0d3bbb2324d0"),
            (31, "687b84e448ec279c29204a872de6260ed65f864041006b3f6af888405efdbc6f"),
            (32, "422e2cc7fd1050e30bd5415c8631a8806511c5c17eb227bdda087723bbe6a152"),
            (33, "b6321fad5f765c02fefca01a5f575ae412afcf25a57c18603c803cf03aa895bf"),
            (34, "773fa23093538c094e5e9c0d94cf32b1ce411bb9a5d39aa104aef7d62bd89de8"),
            (36, "138afb9cbc43e75d9a91d0829b546bd1fe87517a750ff88bf0f6e0575ebae6d6"),
            (37, "9eb7335d66eca917789e640c5f2488431d72612ed618c47cccd71aa455673c10"),
            (38, "9c8fca3b8efc855467bd9721c026c9cd8ca2bf203f0839c7afee63f4a223162a"),
            (39, "923d564e45ae6b01d8b102686aaa654364c261050bc0682129870d04e7ed87e7"),
            (40, "cfb55ef10e552df3905da7c33d9fc03cb3e291bf44b4048576de660df0163019"),
            (41, "01bf47d3ff7feffa4991447d131fa3e751973f8a5d9408b0e552d1b0e3e619d7"),
            (42, "dc40e44006a56d5f1a22804abdbfa7c88edc903e9344c8567db5714ebfb6a433"),
            (43, "8c706d5c05c38e311f1933dd5cadb19d6744aa5260b2b3ac7bc093524970ceed"),
            (44, "fa9f78aa4b13ab61aa0f78516c2e2263fc0a11328cad638a053954f75e4494b2"),
            (45, "fdda6dbd3b3fe56f9aa0ab91a3f010cb2e4200ce9940ebe25af38730e4327e1b"),
            (46, "af6105d5caf972ce7efc83b8277bf2f2610eb2c665357cea166c5fd87b9ea92c"),
            (47, "7b677fe60f6636e88dd9e2ba146fcaff191fbe2bf5fe26592b39540df2d6f291")
        ];
        check(expected.Length == 46, "all and only native selectable tracks are represented");
        foreach (var (track, expectedHash) in expected)
        {
            var plan = NativeId65FreshExecutablePlan.Compile(original, track);
            byte[] output = plan.CopyExecutable();
            check(plan.FinalSha256 == expectedHash && Hash(output) == expectedHash,
                "every complete track EXE matches the independent sequential afterimage");
            check(plan.RevertExact(output).SequenceEqual(original),
                "every music/title output has a complete exact original inverse");
            check(plan.SequentialStages.Count == 12 && plan.SelectedBuiltInTrack == track &&
                !plan.RuntimeVerified && !plan.GlobalTransitionState12Supported &&
                !plan.GlyphAllocationOrRenderingProven && !plan.PublicationApproved,
                "sequential bridge retains bounded global-state and native runtime exclusions");
            byte[] folded = original.ToArray();
            int cursor = 0;
            foreach (var window in plan.FoldedWindows)
            {
                byte[] before = Convert.FromHexString(window.BeforeHex);
                byte[] after = Convert.FromHexString(window.AfterHex);
                check(window.FileOffset >= cursor && before.Length == after.Length &&
                    folded.AsSpan(window.FileOffset, before.Length).SequenceEqual(before),
                    "folded windows are disjoint exact original owners");
                after.CopyTo(folded, window.FileOffset);
                cursor = window.FileOffset + after.Length;
            }
            check(folded.SequenceEqual(output), "folded output retains every undeclared EXE byte");
            output[0x65D67] = 1;
            reject(() => plan.RevertExact(output), "lost LAB terminator cannot obtain original inverse authority");
        }
        foreach (int offset in new[] { 0, 0x1CA8, 0x40C0, 0x6007C, 0x65D5C, 0x65D64, 0x65D68 })
        {
            byte[] corrupt = original.ToArray();
            corrupt[offset] ^= 1;
            reject(() => NativeId65FreshExecutablePlan.Compile(corrupt, 19),
                "foreign source, bootstrap, neighbor, pointer or title byte refuses before composition");
        }
        reject(() => NativeId65FreshExecutablePlan.Compile(original[..^1], 19), "short complete EXE refuses");
        reject(() => NativeId65FreshExecutablePlan.Compile(original.Concat(new byte[1]).ToArray(), 19),
            "EXE suffix refuses");
        reject(() => NativeId65FreshExecutablePlan.Compile(first.CopyExecutable(), 19),
            "already-composed EXE cannot impersonate original source");
    }

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
