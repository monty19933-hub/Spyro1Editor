using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;

internal static class NestedGrowthDiscCases
{
    internal static void Run(Action<bool, string> check)
    {
        Assembly core = typeof(NativeTerrainTextureRecordAppendBuilder).Assembly;
        Type relocator = core.GetType("Spyro.Editor.Core.Exporting.NativeSkyWadRelocator", true)!;
        Type payloadType = core.GetType("Spyro.Editor.Core.Exporting.NativeSkyRelocationPayload", true)!;
        string directory = Directory.CreateTempSubdirectory("spyro-native-multi-growth-disc-").FullName;
        try
        {
            foreach (int sectorSize in new[] { 2048, 2336, 2352 })
            {
                byte[] source = GapFixture.Image(sectorSize);
                byte[] wad = new byte[0x3000];
                NestedGrowthCases.Word(wad, 0, 0x800); NestedGrowthCases.Word(wad, 4, 0x2800);
                NestedGrowthCases.Fixture().CopyTo(wad, 0x800);
                for (int i = 0; i < 6; i++)
                {
                    int sector = i;
                    GapFixture.EditSector(source, sectorSize, 37 + i, raw => wad.AsSpan(sector * 0x800, 0x800).CopyTo(raw.AsSpan(24)));
                }
                string sourcePath = Path.Combine(directory, $"source-{sectorSize}.bin");
                string outputPath = Path.Combine(directory, $"output-{sectorSize}.bin");
                string analysis = Path.Combine(directory, $"analysis-{sectorSize}.json");
                File.WriteAllBytes(sourcePath, source);
                File.WriteAllText(analysis, """{"wad":{"lba":37,"size":12288},"entries":[{"index":0,"offset":2048,"size":10240}]}""");
                NestedGrowthCases.Request[] requests = [NestedGrowthCases.Payload(1, 0x400, 0, 0x20) with { StorageWadEntry = 0 },
                    NestedGrowthCases.Payload(2, 0x800, 0, 0x28) with { StorageWadEntry = 0 },
                    NestedGrowthCases.Payload(3, 0x400, 0, 0x14) with { StorageWadEntry = 0 }];
                Array payloads = Payloads(requests);
                object plan = Call("BuildPlan", sourcePath, analysis, payloads)!;
                JsonElement fields = JsonSerializer.SerializeToElement(plan, plan.GetType());
                check(fields.GetProperty("WadGrowthBytes").GetInt32() == 0x1800 &&
                    fields.GetProperty("RelocatedExecutableLba").GetInt32() == 46, "Actual multi-subfile disc plan rounded or placed the executable incorrectly.");
                Dictionary<int, long> offsets = fields.GetProperty("RelocatedPatches").EnumerateArray()
                    .ToDictionary(p => p.GetProperty("PatchIndex").GetInt32(), p => p.GetProperty("WadOffset").GetInt64());
                check(offsets[1] == 0x1C00 && offsets[2] == 0x3000 && offsets[3] == 0x3C00,
                    "Actual multi-subfile plan lost preceding subfile growth in its patch addresses.");
                Call("WriteExpandedImage", sourcePath, outputPath, analysis, plan, payloads);
                byte[] output = File.ReadAllBytes(outputPath);
                byte[] expectedEntry = NestedGrowthCases.Expand(NestedGrowthCases.Fixture(), requests, 0x1800);
                check(Logical(output, 37, 0x800, expectedEntry.Length, sectorSize).SequenceEqual(expectedEntry),
                    "Actual multi-subfile disc writer did not emit its complete verified entry.");
                check(Logical(source, 43, 0, 0x1000, sectorSize).SequenceEqual(Logical(output, 46, 0, 0x1000, sectorSize)),
                    "Actual multi-subfile disc writer changed the relocated executable.");
                check(File.ReadAllBytes(sourcePath).SequenceEqual(source), "Multi-subfile disc writing changed the original image.");
                for (int lba = 0; lba < 64; lba++)
                    if (lba == 20 || lba is >= 37 and <= 47) GapFixture.CheckForm1(output, sectorSize, lba, check);
                    else check(source.AsSpan(lba * sectorSize, sectorSize).SequenceEqual(output.AsSpan(lba * sectorSize, sectorSize)),
                        "Multi-subfile disc writer changed a protected physical sector.");
                byte[] retained = output.ToArray();
                // A late second/third payload failure cannot publish an earlier
                // component or overwrite a preexisting successful output.
                var malformed = requests.ToArray(); malformed[2] = malformed[2] with { OriginalLength = 4, RequireLengthPrefix = true };
                object malformedPlan = Call("BuildPlan", sourcePath, analysis, Payloads(malformed))!;
                Reject(() => Call("WriteExpandedImage", sourcePath, outputPath, analysis, malformedPlan, Payloads(malformed)), "late payload prefix");
                malformed[2] = requests[2] with { OriginalWadOffset = requests[2].OriginalWadOffset + 4 };
                Reject(() => Call("BuildPlan", sourcePath, analysis, Payloads(malformed)), "stale source coordinate");
                check(File.ReadAllBytes(outputPath).SequenceEqual(retained) && File.ReadAllBytes(sourcePath).SequenceEqual(source) &&
                    !Directory.EnumerateFiles(directory, ".spyro-wad-growth-*").Any(), "Failed multi-subfile composition changed retained bytes or leaked a stage.");
            }
        }
        finally { Directory.Delete(directory, true); }

        void Reject(Action action, string label)
        {
            bool rejected = false;
            try { action(); } catch (InvalidDataException) { rejected = true; }
            check(rejected, $"The actual writer accepted {label}.");
        }
        Array Payloads(NestedGrowthCases.Request[] requests)
        {
            Array result = Array.CreateInstance(payloadType, requests.Length);
            for (int i = 0; i < requests.Length; i++) result.SetValue(JsonSerializer.Deserialize(JsonSerializer.Serialize(requests[i]), payloadType), i);
            return result;
        }
        object? Call(string method, params object[] parameters)
        {
            try { return relocator.GetMethod(method, BindingFlags.Static | BindingFlags.Public)!.Invoke(null, parameters); }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            { ExceptionDispatchInfo.Capture(exception.InnerException).Throw(); throw; }
        }
    }

    private static byte[] Logical(byte[] image, int lba, int offset, int length, int sectorSize)
    {
        byte[] result = new byte[length];
        for (int cursor = 0; cursor < length;)
        {
            int take = Math.Min(length - cursor, 2048 - offset % 2048);
            image.AsSpan((lba + offset / 2048) * sectorSize + (sectorSize == 2352 ? 24 : sectorSize == 2336 ? 8 : 0) + offset % 2048, take).CopyTo(result.AsSpan(cursor));
            cursor += take; offset += take;
        }
        return result;
    }
}
