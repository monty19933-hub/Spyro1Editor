using System.Text.Json;
using System.Buffers.Binary;
using Spyro.Editor.Core.Universal;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;

internal static class GlobalFreeFlightCases
{
    internal static int Run()
    {
        int checks = 0;
        var windows = NativeFreeFlightPatchBuilder.BuildGlobalNormal();
        Check(windows.Count == 12, "same bounded instruction ownership");
        Check(windows.All(w => w.Before.Length == w.After.Length), "no global code growth");
        foreach (int level in Enumerable.Range(10, 56))
        foreach (int classification in new[] { 0, 1, -1, int.MinValue, int.MaxValue })
        {
            var memory = Memory(level, classification);
            foreach (var window in windows.Where(w => w.Name.StartsWith("enter-", StringComparison.Ordinal)))
            {
                var before = Machine.Run(window.Address, window.Before, memory, Registers());
                var after = Machine.Run(window.Address, window.After, memory, Registers());
                Check(after.Exit == (window.Name == "enter-cross" ? 0x80041FACu : 0x80043FC0u),
                    "every normal entry grants native flight state");
                if (window.Name != "enter-cross") Check(after.Registers[4] == 32, "native flight animation state");
                Check(after.Writes.Count == 0, "entry no writes");
                Same(before, after, [2, 3, 4]);
                if (classification != 0 || level == 64)
                    Check(before.Exit == after.Exit && (window.Name == "enter-cross" || before.Registers[4] == after.Registers[4]),
                        "native flight and64 entry preserved");
            }
            foreach (int ground in new[] { 0, 1, -1, int.MinValue, int.MaxValue })
            {
                memory[0x80078AF4] = ground;
                var permission = windows.Single(w => w.Name == "permission");
                var before = Machine.Run(permission.Address, permission.Before, memory, Registers());
                var after = Machine.Run(permission.Address, permission.After, memory, Registers());
                Check(after.Memory[0x80078CA4] == ((classification == 0 || level == 64) && ground == 0 ? 1 : 0),
                    "normal/native64 permission with both load delays");
                Same(before, after, [1, 2, 3]);
                Check(after.Writes.All(a => a == 0x80078CA4), "only native permission field");
            }
            foreach (var height in windows.Where(w => w.Name.StartsWith("height-", StringComparison.Ordinal)))
            foreach (int substate in new[] { 0, 1, -1 })
            foreach (int delta in new[] { int.MinValue, -513, 0, 512, 513, int.MaxValue })
            {
                memory[0x80078AD4] = substate; memory[0x80078A60] = delta;
                var before = Machine.Run(height.Address, height.Before, memory, Registers());
                var after = Machine.Run(height.Address, height.After, memory, Registers());
                Check(after.Exit == (classification == 0 && substate == 0 ? height.SuccessAddress : before.Exit),
                    "only normal classifier bypasses ceiling");
                Check(after.Writes.Count == 0, "height no writes");
                Same(before, after, [2, 3]);
            }
            var pitch = windows.Single(w => w.Name == "pitch-ceiling");
            foreach (int delta in new[] { -307, -306, 0, 307, int.MaxValue })
            foreach (int angle in new[] { -801, -800, -1, 0, 77, 800 })
            {
                memory[0x80078B78] = angle;
                int[] registers = Registers(); registers[18] = delta;
                registers[21] = unchecked((int)0x80078B70);
                var before = Machine.Run(pitch.Address, pitch.Before, memory, registers);
                var after = Machine.Run(pitch.Address, pitch.After, memory, registers);
                Check(after.Memory[0x80078B78] == (classification == 0 ? Math.Max(angle, -800) : before.Memory[0x80078B78]),
                    "course pitch preserved; normal lower clamp retained");
                Check(before.Registers[18] == after.Registers[18] && before.Registers[19] == after.Registers[19],
                    "native intermediate registers preserved");
                Check(after.Writes.All(a => a == 0x80078B78), "only native pitch field");
                Same(before, after, [1, 2, 3]);
            }
        }
        return checks;

        void Check(bool value, string label)
        { if (!value) throw new InvalidDataException("GLOBAL FLIGHT FAILED: " + label); checks++; }
        void Same(Machine.Result before, Machine.Result after, int[] allowed) => Check(
            Enumerable.Range(0, 32).All(r => allowed.Contains(r) || before.Registers[r] == after.Registers[r]),
            "unowned registers preserved");
    }

    internal static async Task<int> RunSourceAsync(string sourcePath, string? reportPath)
    {
        int checks = 0;
        var plan = await NativeFreeFlightCompositionPlan.CaptureAsync(sourcePath, true);
        var source = await NativePlayerAnimationSource.CaptureAsync(sourcePath);
        Check(plan.NormalLevelIds.Count == 30, "all thirty normal owners");
        byte[] originalExe = plan.ExecutablePatch.CopySourceExecutable();
        byte[] finalExe = plan.ApplyToExecutable(originalExe);
        Check(plan.ExecutablePatch.RevertExecutable(finalExe).SequenceEqual(originalExe), "global exact inverse");
        byte[] freshExe = originalExe.ToArray();
        Convert.FromHexString("005E023C").CopyTo(freshExe, 0x40C0);
        Check(plan.ExecutablePatch.RevertExecutable(plan.ApplyToExecutable(freshExe)).SequenceEqual(freshExe),
            "known fresh classifier delta preserved through global inverse");
        freshExe[0x40C4] ^= 1;
        try { _ = plan.ApplyToExecutable(freshExe); throw new Exception("Fresh classifier corruption accepted."); }
        catch (InvalidDataException) { checks++; }
        var off = await NativeFreeFlightCompositionPlan.CaptureAsync(sourcePath, false);
        Check(off.ApplyToExecutable(originalExe).SequenceEqual(originalExe), "global OFF exact original executable");
        using FileStream image = new(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        DiscLayout disc = DiscImage.DetectLayout(sourcePath);
        byte[] peteHeader = DiscImage.ReadFileBytes(image, disc, 37, 0x7B8800, 0x800);
        byte[] pete = DiscImage.ReadFileBytes(image, disc, 37, 0x7B9000, 0x39C48);
        byte[] residentEntry = DiscImage.ReadFileBytes(image, disc, 37, 0x7B8800, 0x3A000);
        plan.RequireResidentEntry8(residentEntry); checks++;
        residentEntry[0] ^= 1;
        try { plan.RequireResidentEntry8(residentEntry); throw new Exception("Resident allocator header mutation accepted."); }
        catch (InvalidDataException) { checks++; }
        List<object> rows = [];
        foreach (var level in source.Levels)
        {
            byte[] before = DiscImage.ReadFileBytes(image, disc, 37, level.DataWadOffset, level.DataByteLength);
            var merged = plan.MergeLevelData(level.LevelId, before);
            byte[] after = merged.CopyLevelData();
            Check(off.MergeLevelData(level.LevelId, before).CopyLevelData().SequenceEqual(before), "global OFF body exact no-op");
            Check(merged.StagedBeforeSha256 == level.DataSha256, "actual original owner");
            if (NativeFreeFlightCompositionPlan.IsNormalLevel(level.LevelId))
            {
                var ordinary = new UniversalLevelData(before);
                ordinary.GrowSubfile(1, 0x800);
                var scene = ordinary.Scene();
                if (scene.RowCount > 0)
                    BinaryPrimitives.WriteInt32LittleEndian(ordinary.Bytes.AsSpan(ordinary.Subfiles()[3].Offset + scene.RowsOffset + 0x0C), 0x123456);
                byte[] staged = ordinary.Bytes.ToArray();
                var joint = plan.MergeLevelData(level.LevelId, staged);
                byte[] jointlyMerged = joint.CopyLevelData();
                Check(jointlyMerged.SequenceEqual(staged), "ordinary staged bytes unchanged by resident reuse");
                Check(joint.Preservations.All(p => staged.AsSpan(p.OriginalOffset, p.ByteLength)
                    .SequenceEqual(jointlyMerged.AsSpan(p.FinalOffset, p.ByteLength))), "every staged ordinary edit retained");
                Check(joint.StagedBeforeSha256 != merged.StagedBeforeSha256, "ordinary body is not required to match retail hash");
                Check(after.SequenceEqual(before), "global resident reuse adds no bytes");
                Check(plan.MergeLevelData(level.LevelId, after).CopyLevelData().SequenceEqual(after), "equivalent stream reuse");
                var memory = NativeLevelRuntimeMemoryReport.CaptureOriginal(level, after, originalExe, peteHeader, pete);
                // This is a source-alone address report; combined edits must use
                // the final Universal composer report rather than reuse this gap.
                var baselineMemory = NativeLevelRuntimeMemoryReport.CaptureOriginal(level, before, originalExe, peteHeader, pete);
                var closure = plan.RequireFinalLevelClosure(level.LevelId, after, finalExe);
                Check(closure.RemainingBytes == baselineMemory.NormalReservedBufferGapBytes,
                    "global final closure uses unchanged actual original allocation");
                Check(closure.UnchangedSourceBaselineException == (level.LevelId == 62),
                    "only authenticated level62 source baseline exception");
                if (level.LevelId == 62)
                {
                    Check(closure.RemainingBytes == 700 && closure.LevelReadEnd == closure.OriginalSourceReadEnd,
                        "level62 retains its authenticated700-byte source gap");
                    var growing62 = new UniversalLevelData(before.ToArray());
                    growing62.GrowSubfile(2, 0x800);
                    try { _ = plan.RequireFinalLevelClosure(62, growing62.Bytes, finalExe);
                        throw new Exception("Growing baseline level62 accepted."); }
                    catch (InvalidDataException) { checks++; }
                }
                rows.Add(new { level.LevelId, merged.ExactInsertedByteLength, growth = after.Length - before.Length,
                    originalNormalReservedGapBytes = baselineMemory.NormalReservedBufferGapBytes,
                    memory.CopyBufferAddress, memory.SceneReadEnd, memory.NormalReservedBufferGapBytes,
                    memory.KnownPostLoadExtentsDisjoint, RuntimeVerified = false });
            }
            else
            {
                Check(after.SequenceEqual(before) && merged.NativeCourseUnchanged, "native course exact byte no-op");
                rows.Add(new { level.LevelId, unchangedNativeCourse = true, RuntimeVerified = false });
            }
        }
        if (reportPath != null)
            File.WriteAllText(reportPath, JsonSerializer.Serialize(new { plan.SourceImageSha256,
                globalPlan = plan.ExecutablePatch.PlanSha256, normalLevels = plan.NormalLevelIds,
                rows, checks, runtimeVerified = false }, new JsonSerializerOptions { WriteIndented = true }));
        return checks;
        void Check(bool value, string label)
        { if (!value) throw new InvalidDataException("GLOBAL FLIGHT SOURCE FAILED: " + label); checks++; }
    }

    private static Dictionary<uint, int> Memory(int level, int classifier) => new()
    {
        [0x8007596C] = level, [0x80075690] = classifier, [0x80078AF4] = 0, [0x80078CA4] = 0,
        [0x80078AD4] = 0, [0x80078A60] = 0, [0x80078BB0] = 0, [0x80078B78] = 0
    };
    private static int[] Registers() => Enumerable.Range(0, 32)
        .Select(i => i == 0 ? 0 : unchecked((int)(0x51A00000u + (uint)i * 0x1001))).ToArray();
}
