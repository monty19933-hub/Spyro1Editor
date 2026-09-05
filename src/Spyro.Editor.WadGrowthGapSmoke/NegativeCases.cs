using System.Buffers.Binary;
using System.Reflection;

internal static class NegativeCases
{
    internal static void Run(string directory, Type payloadType, Func<string, object?[], object?> invoke, Action<bool, string> check)
    {
        foreach (int sectorSize in new[] { 2352, 2336, 2048 })
        {
            Run("late nonzero padding", image => GapFixture.EditSector(image, sectorSize, 47, raw => raw[24] = 1));
            Run("overlapping preceding extent", image => GapFixture.EditSector(image, sectorSize, 20,
                raw => GapFixture.Record(raw.AsSpan(24 + 202), "OVERLAP.STR;1", 36, 12 * 2048)));
            Run("unknown subdirectory", image => GapFixture.EditSector(image, sectorSize, 20,
                raw => GapFixture.Record(raw.AsSpan(24 + 202), "UNKNOWN", 55, 2048, directory: true)));
            Run("live nested file in allocation gap", image => GapFixture.AddDirectory(image, sectorSize, 45));
            Run("directory alias", image => GapFixture.EditSector(image, sectorSize, 20,
                raw => GapFixture.Record(raw.AsSpan(24 + 202), "LOOP", 20, 2048, directory: true)));
            Run("misaligned executable", image => GapFixture.EditSector(image, sectorSize, 20,
                raw => GapFixture.Both(raw.AsSpan(24 + 110), 10, 4095)));
            Run("late malformed directory", image => GapFixture.EditSector(image, sectorSize, 20, raw => raw[24 + 202] = 33));
            Run("late endian mismatch", image => GapFixture.EditSector(image, sectorSize, 20, raw => raw[24 + 156 + 6] ^= 1));
            Run("path table in allocation gap", image => GapFixture.EditSector(image, sectorSize, 16,
                raw => BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(24 + 140), 45)));
            Run("optional path table in allocation gap", image => GapFixture.EditSector(image, sectorSize, 16,
                raw => BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(24 + 144), 45)));
            Run("stale path table ownership", image => GapFixture.EditSector(image, sectorSize, 19, raw => raw[24 + 5] = 21));
            Run("unknown volume descriptor", image => GapFixture.EditSector(image, sectorSize, 17, raw => raw[24] = 2));
            Run("allocation beyond declared volume", image => GapFixture.EditSector(image, sectorSize, 16,
                raw => GapFixture.Both(raw.AsSpan(24), 80, 46)));
            Run("stale relocated executable", _ => { }, plan => Set(plan, "RelocatedExecutableLba", 45));
            Run("stale available capacity", _ => { }, plan => Set(plan, "AvailableGrowthBytes", 8192));
            Run("stale entry destination", _ => { }, plan =>
                ((IDictionary<int, long>)plan.GetType().GetProperty("RelocatedEntryOffsets")!.GetValue(plan)!)[1]++);
            Run("stale patch destination", _ => { }, plan =>
            {
                object patch = ((System.Collections.IEnumerable)plan.GetType().GetProperty("RelocatedPatches")!.GetValue(plan)!).Cast<object>().Single();
                Set(patch, "WadOffset", 0x1801L);
            });
            Run("stale length prefix", _ => { }, changePayload: payload => Set(payload, "RequireLengthPrefix", true));
            Run("wrong payload owner", _ => { }, changePayload: payload => Set(payload, "WadLba", 38));
            Run("wrong payload original address", _ => { }, changePayload: payload => Set(payload, "OriginalWadOffset", 0x1801L));
            Run("negative payload length", _ => { }, changePayload: payload => Set(payload, "OriginalLength", -1));
            if (sectorSize != 2048)
            {
                Run("noncanonical owner file", image => GapFixture.EditSector(image, sectorSize, 43, raw => raw[16] = raw[20] = 1));
                Run("noncanonical owner channel", image => GapFixture.EditSector(image, sectorSize, 40, raw => raw[17] = raw[21] = 1));
                Run("noncanonical owner coding", image => GapFixture.EditSector(image, sectorSize, 41, raw => raw[19] = raw[23] = 1));
                Run("unexpected internal record boundary", image => GapFixture.EditSector(image, sectorSize, 41, raw => raw[18] = raw[22] = 0x89));
                Run("late non-padding XA", image => GapFixture.EditSector(image, sectorSize, 47, raw => raw[16] = raw[20] = 1));
                Run("late malformed duplicate", image => GapFixture.EditSector(image, sectorSize, 47, raw => raw[22] ^= 8));
                Run("late invalid Form2 parity", image => image[48 * sectorSize - 1] ^= 1);
                if (sectorSize == 2352)
                {
                    Run("late invalid sync", image => image[47 * sectorSize + 1] = 0);
                    Run("late incorrect MSF", image => image[47 * sectorSize + 14] ^= 1);
                }
            }

            void Run(string name, Action<byte[]> changeImage, Action<object>? changePlan = null, Action<object>? changePayload = null)
            {
                string source = Path.Combine(directory, "negative-source.bin"), output = Path.Combine(directory, "negative-output.bin");
                string analysis = Path.Combine(directory, "synthetic-wad-analysis.json");
                byte[] before = GapFixture.Image(sectorSize);
                File.WriteAllBytes(source, before);
                Array payloads = Array.CreateInstance(payloadType, 1);
                object payload = Activator.CreateInstance(payloadType, [0, 37, 0x1800L, 0, 0x800, 0, new byte[6144], 0, false])!;
                payloads.SetValue(payload, 0);
                object plan = invoke("BuildPlan", [source, analysis, payloads])!;
                changeImage(before); File.WriteAllBytes(source, before);
                changePlan?.Invoke(plan); changePayload?.Invoke(payload);
                byte[] sentinel = [19, 81, 2, 44, 0, 255]; File.WriteAllBytes(output, sentinel);
                bool rejected = false;
                try { invoke("WriteExpandedImage", [source, output, analysis, plan, payloads]); }
                catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or ArgumentException or OverflowException) { rejected = true; }
                check(rejected, $"Unsafe {sectorSize} {name} was accepted.");
                check(File.ReadAllBytes(source).SequenceEqual(before), $"Rejected {name} changed its source.");
                check(File.ReadAllBytes(output).SequenceEqual(sentinel), $"Rejected {name} modified an existing output before complete preflight.");
                check(Directory.GetFiles(directory, ".spyro-wad-growth-*.tmp").Length == 0, "Rejected structural work left a staging image.");
            }
        }

        string aliasSource = Path.Combine(directory, "alias-source.bin"), aliasAnalysis = Path.Combine(directory, "synthetic-wad-analysis.json");
        byte[] aliasBefore = GapFixture.Image(2352); File.WriteAllBytes(aliasSource, aliasBefore);
        Array aliasPayloads = Array.CreateInstance(payloadType, 1);
        aliasPayloads.SetValue(Activator.CreateInstance(payloadType, [0, 37, 0x1800L, 0, 0x800, 0, new byte[2048], 0, false]), 0);
        object aliasPlan = invoke("BuildPlan", [aliasSource, aliasAnalysis, aliasPayloads])!;
        RejectAlias(aliasSource, aliasSource);
        RejectAlias(aliasSource, aliasAnalysis);
        if (!OperatingSystem.IsWindows())
        {
            string sourceLink = Path.Combine(directory, "source-link.bin"); File.CreateSymbolicLink(sourceLink, aliasSource);
            RejectAlias(sourceLink, aliasSource);
            RejectAlias(aliasSource, sourceLink);
            string analysisLink = Path.Combine(directory, "analysis-link.json"); File.CreateSymbolicLink(analysisLink, aliasAnalysis);
            RejectAlias(aliasSource, analysisLink);
            string directoryLink = Path.Combine(directory, "directory-link"); Directory.CreateSymbolicLink(directoryLink, directory);
            RejectAlias(aliasSource, Path.Combine(directoryLink, "alias-source.bin"));
            Directory.Delete(directoryLink);
        }
        string caseAlias = Path.Combine(directory, "ALIAS-SOURCE.BIN");
        if (File.Exists(caseAlias)) RejectAlias(aliasSource, caseAlias);
        void RejectAlias(string source, string output)
        {
            byte[] targetBefore = File.ReadAllBytes(output); bool rejected = false;
            try { invoke("WriteExpandedImage", [source, output, aliasAnalysis, aliasPlan, aliasPayloads]); }
            catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException) { rejected = true; }
            check(rejected, "A source-input output alias was accepted.");
            check(File.ReadAllBytes(aliasSource).SequenceEqual(aliasBefore), "An input alias changed the source image.");
            check(File.ReadAllBytes(output).SequenceEqual(targetBefore), "An input alias changed the selected input target.");
        }
    }

    private static void Set(object target, string property, object value) => target.GetType().GetProperty(property)!.SetValue(target, value);
}
