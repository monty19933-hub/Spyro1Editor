using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

internal static class PrivateAppearanceCases
{
    private const string CompanionKind = "native-terrain-texture-lp-companion";
    private const int WadStart = 37 * 2048;
    private const int FirstSector = 0x14000;
    private const int SecondSector = 0x14200;
    private const int UnrelatedSector = 0x14400;
    private const int LpOnlySector = 0x14600;
    private const int ColorOffset = 44;
    private const int HpTextureOffset = 92;

    public static void Run(Func<int, byte, byte[]> makeSector)
    {
        string directory = Directory.CreateTempSubdirectory("spyro-private-lp-").FullName;
        try
        {
            string source = Path.Combine(directory, "source.bin");
            string search = Path.Combine(directory, "source-search.json");
            byte[] first = makeSector(5, 0x30);
            byte[] second = makeSector(4, 0);
            byte[] unrelated = makeSector(3, 0x30);
            byte[] lpOnly = makeSector(3, 0x30);
            lpOnly[20] = lpOnly[22] = 0;
            Array.Resize(ref lpOnly, 68);
            Id65EnvironmentSectorHeaderContract.Stamp(lpOnly);
            byte[] original = BuildDisc(first, second, unrelated, lpOnly);
            File.WriteAllBytes(source, original);
            File.WriteAllText(search, JsonSerializer.Serialize(new
            {
                purpose = "source-derived private texture appearance regression",
                results = new[] { FirstSector, SecondSector, UnrelatedSector }.Select(offset => new
                {
                    edit = $"sector-{offset:X}",
                    sectorOffset = $"0x{offset:X}",
                    sectorSizeBytes = first.Length,
                    fullSectorHits = new[] { new { wadOffset = offset } }
                })
            }));
            LevelDefinition level = new() { Key = "fixture", DisplayName = "Fixture", SourceWadEntry = 0 };
            TerrainPatch assignment = Patch(FirstSector + HpTextureOffset,
                first.AsSpan(HpTextureOffset, 4).ToArray(), [6, 0, 0, 0], "texture-id-word3");
            // A prior LP tint must be composed, not replaced with retail grey.
            byte[] tint = first.AsSpan(ColorOffset, 16).ToArray();
            tint[0] = 120;
            TerrainPatch tintPatch = Patch(FirstSector + ColorOffset,
                first.AsSpan(ColorOffset, 16).ToArray(), tint, "lp-color");
            TerrainPatchPlan ordinary = Plan(source, search, [assignment, tintPatch]);
            NativeTerrainTextureRelocationEdit[] edits =
            [
                Edit(6, 5, NativeTerrainTextureTargetRecordKind.AppendedPrivate),
                Edit(4, -1, NativeTerrainTextureTargetRecordKind.ExistingNative)
            ];
            TerrainPatchPlan completed = TerrainPatchExporter.AddPrivateTextureAppearanceDependencies(
                ordinary, level, edits);
            TerrainPatch[] companions = completed.Patches.Where(p => p.Kind == CompanionKind).ToArray();
            Require(companions.Length == 2,
                $"Private batch omitted mixed appended/existing far-LOD appearance: expected two companions, got {companions.Length}.");
            Require(completed.NativeTextureRelocationCount == 0 && completed.NativeTextureRelocations.Count == 0,
                "Appearance dependency compilation invoked a second texture allocator.");
            Require(completed.Patches.All(p => !p.Kind.StartsWith("native-terrain-texture-", StringComparison.Ordinal) || p.Kind == CompanionKind),
                "Appearance dependency compilation emitted a descriptor/page patch.");
            IReadOnlyList<NativeTerrainTextureRecordExistingPatch> converted =
                NativeTerrainTexturePrivateRecordBatchCompiler.ConvertOrdinaryTerrainPlan(completed);
            Require(converted.Count == 3, "Private bridge failed to consume the composed LP tables and HP assignment exactly once.");
            byte[] output = original.ToArray();
            foreach (NativeTerrainTextureRecordExistingPatch patch in converted)
            {
                int offset = checked(WadStart + (int)patch.WadOffset);
                Require(output.AsSpan(offset, patch.Before.Length).SequenceEqual(patch.Before), "LP bridge lost its original preimage.");
                patch.After.CopyTo(output, offset);
            }
            for (int i = 0; i < original.Length; i++)
            {
                int relative = i - WadStart;
                bool approved = relative >= FirstSector + HpTextureOffset && relative < FirstSector + HpTextureOffset + 4 ||
                    IsRgb(relative, FirstSector) || IsRgb(relative, SecondSector);
                Require(approved || original[i] == output[i], $"Private LP plan changed protected byte 0x{i:X}.");
            }
            Require(output.AsSpan(WadStart + UnrelatedSector, unrelated.Length).SequenceEqual(unrelated), "Unrelated far sector changed.");
            Require(output.AsSpan(WadStart + LpOnlySector, lpOnly.Length).SequenceEqual(lpOnly), "Directory-only LP sector changed.");
            Require(File.ReadAllBytes(source).SequenceEqual(original), "Appearance planning mutated the source disc.");
            Require(output[WadStart + FirstSector + ColorOffset] > output[WadStart + FirstSector + ColorOffset + 4],
                "Pre-existing LP tint was discarded during appearance composition.");
            Require(output.AsSpan(WadStart + FirstSector + ColorOffset, 3).SequenceEqual(new byte[] { 214, 25, 100 }) &&
                output.AsSpan(WadStart + SecondSector + ColorOffset, 3).SequenceEqual(new byte[] { 255, 37, 163 }),
                "Private LP output did not match the independent native-palette/template/tint colour oracle.");

            foreach (string kind in new[] { "native-terrain-texture-page", "native-terrain-texture-descriptor", CompanionKind + "-spoof" })
            {
                ExpectRejected(() => NativeTerrainTexturePrivateRecordBatchCompiler.ConvertOrdinaryTerrainPlan(
                    completed with { Patches = [companions[0] with { Kind = kind }] }), "Private bridge admitted " + kind);
            }
            byte[] badCommands = Convert.FromHexString(companions[0].AfterHexPreview.Replace(" ", ""));
            badCommands[3] ^= 1;
            ExpectRejected(() => NativeTerrainTexturePrivateRecordBatchCompiler.ConvertOrdinaryTerrainPlan(
                completed with { Patches = [companions[0] with { AfterHexPreview = Convert.ToHexString(badCommands) }], NativeTextureRelocationBytePatchCount = 1 }),
                "Private bridge admitted modified native LP command bytes.");
            ExpectRejected(() => TerrainPatchExporter.AddPrivateTextureAppearanceDependencies(
                ordinary with { SourceSearchPath = "" }, level, edits), "Private appearance silently omitted missing source-sector proof.");
            ExpectRejected(() => TerrainPatchExporter.AddPrivateTextureAppearanceDependencies(completed, level, edits),
                "Private appearance dependencies were applied twice.");
            List<string> missedRejections = [];
            CheckRejected(() => TerrainPatchExporter.AddPrivateTextureAppearanceDependencies(
                ordinary, level, [edits[0] with { MaterialTemplateTextureId = 4 }, edits[1]]), "mismatched source material");
            TerrainPatch changedTopology = Patch(FirstSector + HpTextureOffset - 8,
                first.AsSpan(HpTextureOffset - 8, 4).ToArray(), [0, 2, 1, 3], "hp-face-topology");
            CheckRejected(() => TerrainPatchExporter.AddPrivateTextureAppearanceDependencies(
                ordinary with { Patches = ordinary.Patches.Append(changedTopology).ToArray() }, level, edits), "unproved changed face topology");
            string completeSearch = File.ReadAllText(search);
            CheckMap(root => root["results"]!.AsArray().RemoveAt(1), "missing whole sector row");
            CheckMap(root => root["results"]![1]!["fullSectorHits"] = new JsonArray(), "missing source hit");
            CheckMap(root => root["results"]![1]!["fullSectorHits"]!.AsArray().Add(
                root["results"]![1]!["fullSectorHits"]![0]!.DeepClone()), "ambiguous source hits");
            CheckMap(root => root["results"]!.AsArray().Add(root["results"]![0]!.DeepClone()), "duplicate source-map identity");
            CheckMap(root => root["results"]![1]!["sectorOffset"] = root["results"]![0]!["sectorOffset"]!.DeepClone(), "contradictory runtime/source identity");
            Require(missedRejections.Count == 0, "Private appearance accepted: " + string.Join(", ", missedRejections));
            Console.WriteLine("PRIVATE_TEXTURE_LP_APPEARANCE_SMOKE_OK mixedImports=2 initializedDonor=1 soleAllocator=1 stagedTintComposed=1 protectedComplement=1 nativeCommandsPreserved=1 sourceMaterialProved=1 completeUniqueSectorMap=1 lpOnlyDirectoryCoverage=1 invalidInputsRejected=13");

            void CheckRejected(Action action, string label)
            {
                try { action(); }
                catch (Exception exception) when (exception is InvalidOperationException or InvalidDataException or ArgumentException) { return; }
                missedRejections.Add(label);
            }
            void CheckMap(Action<JsonObject> mutate, string label)
            {
                JsonObject root = JsonNode.Parse(completeSearch)!.AsObject();
                mutate(root);
                File.WriteAllText(search, root.ToJsonString());
                try { CheckRejected(() => TerrainPatchExporter.AddPrivateTextureAppearanceDependencies(ordinary, level, edits), label); }
                finally { File.WriteAllText(search, completeSearch); }
            }
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static bool IsRgb(int offset, int sector) =>
        offset >= sector + ColorOffset && offset < sector + ColorOffset + 16 && (offset - sector - ColorOffset) % 4 != 3;

    private static NativeTerrainTextureRelocationEdit Edit(int target, int template, NativeTerrainTextureTargetRecordKind kind) =>
        new(target, "donor", "Donor", 1, 0, NativeTerrainTextureRelocationEditStore.BuildTextureRecordProvenanceKey("donor", 0), "both", "", "", "",
            ApplyMode: NativeTerrainTextureRelocationApplyMode.ArtOnlyPreserveTarget,
            TargetRecordKind: kind, TargetWadEntry: 0, SourceTextureRecordCount: 6,
            MaterialTemplateTextureId: template);

    private static TerrainPatch Patch(int offset, byte[] before, byte[] after, string kind) =>
        new(kind, kind, $"sector-{offset:X}", $"0x{offset:X}", "", before.Length,
            Hex(before), Hex(after), "Synthetic private appearance dependency.");

    private static string Hex(byte[] bytes) => string.Join(" ", bytes.Select(value => value.ToString("X2")));

    private static TerrainPatchPlan Plan(string source, string search, IReadOnlyList<TerrainPatch> patches) =>
        new(DateTimeOffset.UtcNow, source, "not-written.bin", "not-written.cue", "fixture", "Fixture", "", search, "", "", "", 0,
            patches.Count, patches.Sum(p => p.ByteLength), 0, 0, [], 0, 0, [], [], patches, [], []);

    private static byte[] BuildDisc(byte[] first, byte[] second, byte[] unrelated, byte[] lpOnly)
    {
        byte[] image = new byte[WadStart + 0x38000];
        Span<byte> pvd = image.AsSpan(16 * 2048, 2048);
        pvd[0] = 1; Encoding.ASCII.GetBytes("CD001").CopyTo(pvd[1..]); pvd[6] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(pvd[128..], 2048);
        BinaryPrimitives.WriteUInt16BigEndian(pvd[130..], 2048);
        Record(pvd[156..], "\0", 18, 2048);
        Record(image.AsSpan(18 * 2048), "WAD.WAD;1", 37, 0x38000);
        Span<byte> wad = image.AsSpan(WadStart);
        UInt(wad, 0, 0x1000); UInt(wad, 4, 0x15000);
        UInt(wad, 8, 0x20000); UInt(wad, 12, 0x15000);
        MakeAsset(wad[0x1000..], 6, 0x294A); // Native grey, 82 per channel.
        MakeAsset(wad[0x20000..], 2, 0x2814); // Red donor, RGB 165/0/82.
        // Raw donor T0 is incomplete; the native loader copies T1 into it.
        wad.Slice(0x20000 + 0x11000 + 8 + 2 * 16, 168).Clear();
        Span<byte> donorScene = wad[(0x20000 + 0x14000)..];
        UInt(donorScene, 136, 24); UInt(donorScene, 140, 1); UInt(donorScene, 144, 8);
        UInt(donorScene, 148, 0); UInt(donorScene, 152, 0);
        donorScene[156] = 4; donorScene[159] = 1;
        UInt(donorScene, 160, 8);
        first.CopyTo(wad[FirstSector..]); second.CopyTo(wad[SecondSector..]); unrelated.CopyTo(wad[UnrelatedSector..]); lpOnly.CopyTo(wad[LpOnlySector..]);
        const int targetModel = 0x1000 + 0x11000;
        const int environment = targetModel + 8 + 6 * 184;
        UInt(wad, environment, (uint)(LpOnlySector + lpOnly.Length - environment));
        UInt(wad, environment + 4, 4);
        UInt(wad, environment + 8, FirstSector - environment - 4);
        UInt(wad, environment + 12, SecondSector - environment - 4);
        UInt(wad, environment + 16, UnrelatedSector - environment - 4);
        UInt(wad, environment + 20, LpOnlySector - environment - 4);
        return image;
    }

    private static void MakeAsset(Span<byte> asset, int count, ushort color)
    {
        UInt(asset, 0, 0x1000); UInt(asset, 4, 0x10000);
        UInt(asset, 8, 0x11000); UInt(asset, 12, 0x3000);
        UInt(asset, 24, 0x14000); UInt(asset, 28, 0x1000);
        Span<byte> page = asset.Slice(0x1000, 0x10000); page[..0x8000].Fill(1);
        BinaryPrimitives.WriteUInt16LittleEndian(page[(0x8000 + 2)..], color);
        BinaryPrimitives.WriteUInt16LittleEndian(page[(0x8020 + 2)..], 0x14A5);
        Span<byte> model = asset.Slice(0x11000, 0x1000);
        UInt(model, 0, (uint)(8 + count * 184)); UInt(model, 4, (uint)count);
        for (int texture = 0; texture < count; texture++)
        {
            int row = 8 + count * 16 + texture * 168;
            for (int descriptor = 0; descriptor < 20; descriptor++)
            {
                int offset = row + 8 + descriptor * 8;
                // T4's source appearance differs from the private row's T5
                // template; mixing denominators must change the pinned oracle.
                BinaryPrimitives.WriteUInt16LittleEndian(model[(offset + 2)..], (ushort)(texture == 4 ? 2081 : 2080));
                model[offset + 4] = 15; model[offset + 5] = 15; model[offset + 6] = 0x88;
            }
        }
        UInt(asset, 0x14000 + 136, 8); UInt(asset, 0x14000 + 144, 8);
    }

    private static void UInt(Span<byte> bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes[offset..], value);
    private static void Record(Span<byte> record, string name, int lba, int length)
    {
        byte[] id = Encoding.ASCII.GetBytes(name);
        record[0] = (byte)(33 + id.Length + (id.Length % 2 == 0 ? 1 : 0));
        BinaryPrimitives.WriteInt32LittleEndian(record[2..], lba); BinaryPrimitives.WriteInt32BigEndian(record[6..], lba);
        BinaryPrimitives.WriteInt32LittleEndian(record[10..], length); BinaryPrimitives.WriteInt32BigEndian(record[14..], length);
        record[28] = record[31] = 1; record[32] = (byte)id.Length; id.CopyTo(record[33..]);
    }
    private static void ExpectRejected(Action action, string message)
    {
        try { action(); }
        catch (Exception exception) when (exception is InvalidOperationException or InvalidDataException or ArgumentException) { return; }
        throw new InvalidOperationException(message);
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
