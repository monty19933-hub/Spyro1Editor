using System.Reflection;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

// Production composition contracts, not another publisher. Free cases cannot
// issue a source plan. The explicit source mode owns only its new directory.
internal static class ProductionStageCases
{
    internal static async Task<int> RunFreeAsync()
    {
        var cases = new Cases();
        string root = Directory.CreateTempSubdirectory("spyro-flight-production-free-").FullName;
        try
        {
            string workspace = Directory.CreateDirectory(Path.Combine(root, "project")).FullName;
            string image = Path.Combine(root, "generated-source.bin");
            string cue = Path.Combine(root, "generated-source.cue");
            byte[] original = [1, 2, 3, 4];
            File.WriteAllBytes(image, original);
            WriteCue(cue, image);
            byte[] cueBefore = File.ReadAllBytes(cue);
            string output = Directory.CreateDirectory(Path.Combine(root, "output")).FullName;

            Type result = typeof(NativeFreeFlightExportStageResult);
            cases.Check(result.IsSealed && result.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance).All(c => c.IsPrivate), "result is a closed source-issued value");
            cases.Check(result.GetProperties(BindingFlags.Public | BindingFlags.Instance).All(p => p.SetMethod == null),
                "result evidence has no public or private property setters");
            cases.Check(typeof(NativeFreeFlightExportStage).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .All(m => m.GetParameters().All(p => p.ParameterType != typeof(bool) &&
                    !p.Name!.Contains("selector", StringComparison.OrdinalIgnoreCase))),
                "production factory has no diagnostic selector or boolean authority override");
            cases.Check(!result.GetProperties(BindingFlags.Public | BindingFlags.Instance).Any(p =>
                p.Name is "RuntimeVerified" or "NormalCreateBinApproved" or "PublicationAuthorized"),
                "stage evidence is not a native or final-publication capability");

            var absent = NativeFreeFlightProjectExportIntent.Capture(workspace);
            await cases.RejectAsync(() => NativeFreeFlightExportStage.CreateAsync(absent, workspace,
                image, cue, Path.Combine(output, "absent")), "absent intent does not authorize a stage");
            cases.Empty(output, "absent intent creates no output");

            var saved = NativeFreeFlightSettingsStore.Save(NativeFreeFlightSettingsStore.Capture(workspace), 14, false);
            var off = NativeFreeFlightProjectExportIntent.Capture(workspace);
            cases.Check(off.RequiresRebuild && off.EnabledLevelId == null, "explicit OFF is a real rebuild request");
            await cases.RejectAsync(() => NativeFreeFlightExportStage.CreateAsync(off, workspace,
                image, cue, Path.Combine(output, "off")), "OFF cannot promote tiny source into an original image");
            cases.Empty(output, "failed OFF source capture creates no output");
            saved = NativeFreeFlightSettingsStore.Save(saved, 14, true);
            var on = NativeFreeFlightProjectExportIntent.Capture(workspace);
            cases.Check(on.EnabledLevelId == 14, "ON intent retains the admitted target");
            await cases.RejectAsync(() => NativeFreeFlightExportStage.CreateAsync(on, workspace,
                image, cue, Path.Combine(output, "on")), "ON cannot promote tiny source into an original image");
            cases.Empty(output, "failed ON source capture creates no output");
            await cases.RejectAsync(() => NativeFreeFlightExportStage.CreateAsync(on, workspace,
                image, cue, Path.Combine(output, "canceled"), new CancellationToken(true)), "canceled stage");
            cases.Empty(output, "cancellation creates no output");
            await cases.RejectAsync(() => NativeFreeFlightExportStage.CreateAsync(off, workspace,
                image, cue, Path.Combine(output, "stale")), "stale OFF cannot override current ON");

            string foreign = Directory.CreateDirectory(Path.Combine(root, "foreign-project")).FullName;
            NativeFreeFlightSettingsStore.Save(NativeFreeFlightSettingsStore.Capture(foreign), 14, true);
            await cases.RejectAsync(() => NativeFreeFlightExportStage.CreateAsync(on, foreign,
                image, cue, Path.Combine(output, "foreign")), "equal settings in another project do not own this request");
            cases.Empty(output, "stale and foreign intent refusals create no output");

            foreach (string suffix in new[] { ".bin", ".cue" })
            {
                string prefix = Path.Combine(output, "occupied");
                string occupied = prefix + suffix;
                byte[] owned = [8, 7, 6, 5];
                File.WriteAllBytes(occupied, owned);
                await cases.RejectAsync(() => NativeFreeFlightExportStage.CreateAsync(on, workspace,
                    image, cue, prefix), "occupied stage leaf is never replaced");
                cases.Check(File.ReadAllBytes(occupied).SequenceEqual(owned) &&
                    Directory.EnumerateFileSystemEntries(output).Count() == 1, "occupied data and complement retained");
                File.Delete(occupied);
            }
            string directoryPrefix = Path.Combine(output, "directory");
            Directory.CreateDirectory(directoryPrefix + ".cue");
            await cases.RejectAsync(() => NativeFreeFlightExportStage.CreateAsync(on, workspace,
                image, cue, directoryPrefix), "output companion cannot be a directory");
            cases.Check(Directory.EnumerateFileSystemEntries(output).Count() == 1, "directory refusal creates no other leaf");
            Directory.Delete(directoryPrefix + ".cue");

            await cases.RejectAsync(() => NativeFreeFlightExportStage.CreateAsync(on, workspace,
                image, cue, Path.Combine(root, "generated-source")), "output cannot replace source BIN or CUE");
            if (!OperatingSystem.IsWindows())
            {
                string prefix = Path.Combine(output, "linked");
                File.CreateSymbolicLink(prefix + ".bin", image);
                await cases.RejectAsync(() => NativeFreeFlightExportStage.CreateAsync(on, workspace,
                    image, cue, prefix), "linked output cannot replace its source target");
                cases.Check(new FileInfo(prefix + ".bin").LinkTarget != null, "refusal preserves caller-owned symbolic link");
                File.Delete(prefix + ".bin");
            }
            cases.Check(File.ReadAllBytes(image).SequenceEqual(original) && File.ReadAllBytes(cue).SequenceEqual(cueBefore),
                "every failed stage preserves its complete generated source pair");
            cases.Empty(output, "all failed attempts retain no owned staging, analysis or launcher files");
            return cases.Count;
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    internal static async Task<int> RunSourceAsync(string originalImage, string originalCue,
        NativeFreeFlightResearchCandidatePlan unchangedResearchOracle, string newDirectory)
    {
        var cases = new Cases();
        string root = Path.GetFullPath(newDirectory);
        if (File.Exists(root) || Directory.Exists(root))
            throw new IOException("Production-stage source regression requires a new exact directory.");
        Directory.CreateDirectory(root);
        string workspace = Directory.CreateDirectory(Path.Combine(root, "private-project")).FullName;
        string output = Directory.CreateDirectory(Path.Combine(root, "disposable-stages")).FullName;
        string sourceForStage = Path.GetFullPath(originalImage);
        if (!OperatingSystem.IsWindows())
        {
            sourceForStage = Path.Combine(root, "private-original.bin");
            File.CreateSymbolicLink(sourceForStage, Path.GetFullPath(originalImage));
        }
        string privateCue = Path.Combine(root, "private-source.cue");
        WriteCue(privateCue, sourceForStage);
        byte[] privateCueBefore = File.ReadAllBytes(privateCue);
        byte[] protectedCueBefore = File.ReadAllBytes(originalCue);
        string researchPlanBefore = unchangedResearchOracle.PlanSha256;
        cases.Check(!unchangedResearchOracle.TestLevelSelectorEnabled, "oracle is the unchanged no-selector research recipe");
        var saved = NativeFreeFlightSettingsStore.Save(NativeFreeFlightSettingsStore.Capture(workspace), 14, true);
        byte[] settingsOn = File.ReadAllBytes(saved.Path);
        var onIntent = NativeFreeFlightProjectExportIntent.Capture(workspace);
        string onPrefix = Path.Combine(output, "production-on");
        var on = await NativeFreeFlightExportStage.CreateAsync(onIntent, workspace, sourceForStage, privateCue, onPrefix);
        cases.Check(on.OutputImagePath == onPrefix + ".bin" && on.OutputCuePath == onPrefix + ".cue" &&
            on.EnabledLevelId == 14 && on.SourceImageSha256 == NativeFreeFlightPatchPlan.SourceImageSha256,
            "actual ON result binds exact source, admitted target and caller-owned new leaves");
        var oracle = unchangedResearchOracle.VerifyImage(on.OutputImagePath);
        cases.Check(on.OutputImageSha256 == oracle.Hash && on.ChangedRawSectors == oracle.Changed && oracle.Changed > 0,
            "production ON whole image equals unchanged independent no-selector research oracle");
        cases.Check(await HashFileAsync(on.OutputImagePath) == on.OutputImageSha256 &&
            DiscCueSheet.ReferencesDataImage(on.OutputCuePath, on.OutputImagePath), "complete output hash and live launcher binding");
        cases.Check(Directory.EnumerateFileSystemEntries(output).Select(Path.GetFileName).Order(StringComparer.Ordinal)
            .SequenceEqual(new[] { "production-on.bin", "production-on.cue" }),
            "production stage emits no research receipt, publication lock or public-output container");
        await on.VerifyAsync();
        await on.RequireCurrentAsync(workspace);
        cases.Check(true, "actual ON stage and source/intent can be independently reverified");

        foreach (long offset in new[] { 30L * 2352 + 24, new FileInfo(on.OutputImagePath).Length - 1 })
        {
            byte before = ReadByte(on.OutputImagePath, offset);
            try
            {
                WriteByte(on.OutputImagePath, offset, (byte)(before ^ 1));
                await cases.RejectAsync(() => on.VerifyAsync(), "whole-image readback refuses unrelated raw complement corruption");
            }
            finally { WriteByte(on.OutputImagePath, offset, before); }
        }
        byte[] outputCueBefore = File.ReadAllBytes(on.OutputCuePath);
        try
        {
            File.AppendAllText(on.OutputCuePath, "\n", DiscCueSheet.TextEncoding);
            await cases.RejectAsync(() => on.VerifyAsync(), "semantically equivalent output CUE changes exact companion bytes");
        }
        finally { File.WriteAllBytes(on.OutputCuePath, outputCueBefore); }
        try
        {
            File.AppendAllText(privateCue, "\n", DiscCueSheet.TextEncoding);
            await cases.RejectAsync(() => on.RequireCurrentAsync(workspace), "semantically equivalent private source CUE is a stale binding");
        }
        finally { File.WriteAllBytes(privateCue, privateCueBefore); }
        if (!OperatingSystem.IsWindows())
        {
            string fake = Path.Combine(root, "generated-unowned.bin");
            File.WriteAllBytes(fake, [9, 8, 7, 6]);
            try
            {
                // Only replace the test-owned link, never write through it or
                // change either the real original BIN or its existing aliases.
                File.Delete(sourceForStage);
                File.CreateSymbolicLink(sourceForStage, fake);
                await cases.RejectAsync(() => on.RequireCurrentAsync(workspace),
                    "retained original image alias cannot be replaced with another source owner");
            }
            finally
            {
                File.Delete(sourceForStage);
                File.CreateSymbolicLink(sourceForStage, Path.GetFullPath(originalImage));
            }
        }
        try
        {
            File.AppendAllText(saved.Path, "\n");
            await cases.RejectAsync(() => on.RequireCurrentAsync(workspace), "same semantic settings with new raw bytes are stale");
        }
        finally { File.WriteAllBytes(saved.Path, settingsOn); }
        string foreign = Directory.CreateDirectory(Path.Combine(root, "foreign-project")).FullName;
        NativeFreeFlightSettingsStore.Save(NativeFreeFlightSettingsStore.Capture(foreign), 14, true);
        await cases.RejectAsync(() => on.RequireCurrentAsync(foreign), "equal settings cannot replace retained project identity");
        await cases.RejectAsync(() => NativeFreeFlightExportStage.CreateAsync(onIntent, workspace,
            sourceForStage, privateCue, onPrefix), "successful disposable outputs cannot be silently reused or replaced");
        await on.VerifyAsync();
        await on.RequireCurrentAsync(workspace);
        cases.Check(await HashFileAsync(on.OutputImagePath) == oracle.Hash, "all restored source/output mutation fixtures recover exact ON image");

        saved = NativeFreeFlightSettingsStore.Save(saved, 14, false);
        var offIntent = NativeFreeFlightProjectExportIntent.Capture(workspace);
        await cases.RejectAsync(() => on.RequireCurrentAsync(workspace), "old ON result cannot pass precommit after saved OFF");
        string offPrefix = Path.Combine(output, "production-off");
        var off = await NativeFreeFlightExportStage.CreateAsync(offIntent, workspace, sourceForStage, privateCue, offPrefix);
        cases.Check(off.EnabledLevelId == null && off.ChangedRawSectors == 0 &&
            off.SourceImageSha256 == NativeFreeFlightPatchPlan.SourceImageSha256 &&
            off.OutputImageSha256 == NativeFreeFlightPatchPlan.SourceImageSha256 &&
            await HashFileAsync(off.OutputImagePath) == NativeFreeFlightPatchPlan.SourceImageSha256,
            "OFF rebuilds complete exact original image including original WAD/ISO layout and absent animation growth");
        cases.Check(off.OutputImagePath == offPrefix + ".bin" && off.OutputCuePath == offPrefix + ".cue" &&
            DiscCueSheet.ReferencesDataImage(off.OutputCuePath, off.OutputImagePath), "OFF owns its own exact new output pair");
        await off.VerifyAsync();
        await off.RequireCurrentAsync(workspace);
        cases.Check(await HashFileAsync(on.OutputImagePath) == oracle.Hash, "OFF does not mutate the preceding ON image");

        // Simulate the existing ordinary exporter consuming its private OFF
        // input. Freshness is about intent/source, distinct from output Verify.
        string consumed = Path.Combine(root, "consumed-off-stage.bin");
        File.Move(off.OutputImagePath, consumed);
        try
        {
            await off.RequireCurrentAsync(workspace);
            cases.Check(true, "final source/intent guard survives legitimate downstream stage consumption");
            await cases.RejectAsync(() => off.VerifyAsync(), "missing private output cannot pass output verification");
        }
        finally { File.Move(consumed, off.OutputImagePath); }
        await off.VerifyAsync();
        await unchangedResearchOracle.RequireFreshAsync();
        cases.Check(unchangedResearchOracle.PlanSha256 == researchPlanBefore &&
            File.ReadAllBytes(originalCue).SequenceEqual(protectedCueBefore) &&
            File.ReadAllBytes(privateCue).SequenceEqual(privateCueBefore),
            "research identity and complete real source pair remain unchanged; only owned fixture bytes were mutated");
        Console.WriteLine($"NATIVE_FREE_FLIGHT_PRODUCTION_STAGE_SOURCE_OK checks={cases.Count} on={on.OutputImageSha256} off={off.OutputImageSha256} researchOracleUnchanged=true publication=false");
        return cases.Count;
    }

    private static void WriteCue(string path, string image) => File.WriteAllText(path,
        $"FILE \"{Path.GetFullPath(image)}\" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n", DiscCueSheet.TextEncoding);

    private static async Task<string> HashFileAsync(string path)
    {
        using FileStream input = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(input));
    }

    private static byte ReadByte(string path, long offset)
    {
        using FileStream input = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        input.Position = offset;
        int value = input.ReadByte();
        return value < 0 ? throw new InvalidDataException("Owned output fixture offset exceeds its image.") : (byte)value;
    }

    private static void WriteByte(string path, long offset, byte value)
    {
        using FileStream output = new(path, FileMode.Open, FileAccess.Write, FileShare.None);
        output.Position = offset;
        output.WriteByte(value);
        output.Flush(flushToDisk: true);
    }

    private sealed class Cases
    {
        internal int Count { get; private set; }
        internal void Check(bool value, string label)
        {
            if (!value) throw new InvalidDataException("Production flight stage assertion failed: " + label);
            Count++;
        }
        internal void Empty(string path, string label) => Check(!Directory.EnumerateFileSystemEntries(path).Any(), label);
        internal async Task RejectAsync(Func<Task> action, string label)
        {
            try { await action(); }
            catch (Exception error) when (error is IOException or InvalidDataException or InvalidOperationException or
                ArgumentException or OperationCanceledException)
            { Count++; return; }
            throw new InvalidDataException("Production flight stage accepted: " + label);
        }
    }
}
