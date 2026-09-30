using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Universal;

internal static class GlobalFreeFlightOutput
{
    internal static async Task VerifyAsync(string original, string candidate, string report)
    {
        var plan = await NativeFreeFlightCompositionPlan.CaptureAsync(original, true);
        var source = await NativePlayerAnimationSource.CaptureAsync(original);
        var disc = UniversalDiscImage.Read(candidate);
        plan.RequireResidentEntry8(disc.EntryBytes(8));
        _ = plan.ExecutablePatch.RevertExecutable(disc.Exe);
        List<NativeFreeFlightMemoryEvidence> memory = [];
        foreach (var level in source.Levels.Where(l => NativeFreeFlightCompositionPlan.IsNormalLevel(l.LevelId)))
            memory.Add(plan.RequireFinalLevelClosure(level.LevelId, disc.EntryBytes(level.DataWadEntry), disc.Exe));
        bool fresh = UniversalExeBindings.Word(disc.Exe, UniversalExeBindings.LevelInitJumpTable + 65 * 4) !=
            UniversalExeBindings.Word(plan.ExecutablePatch.CopySourceExecutable(), UniversalExeBindings.LevelInitJumpTable + 65 * 4);
        if (fresh) memory.Add(plan.RequireFinalLevelClosure(65, disc.EntryBytes(80), disc.Exe));
        using FileStream file = File.OpenRead(candidate);
        File.WriteAllText(report, JsonSerializer.Serialize(new { candidate = Path.GetFullPath(candidate),
            candidateSha256 = Convert.ToHexStringLower(await SHA256.HashDataAsync(file)),
            planSha256 = plan.ExecutablePatch.PlanSha256, memory, fresh65 = fresh, runtimeVerified = false },
            new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("NATIVE_GLOBAL_FLIGHT_CANDIDATE_VERIFIED owners=" + memory.Count);
    }

    internal static async Task ApplyAsync(string original, string staged, string output)
    {
        original = Path.GetFullPath(original); staged = Path.GetFullPath(staged); output = Path.GetFullPath(output);
        if (output == original || output == staged || File.Exists(output))
            throw new InvalidDataException("Flight verification output must be a new distinct file; source/staged fixtures are read-only.");
        var plan = await NativeFreeFlightCompositionPlan.CaptureAsync(original, true);
        var source = await NativePlayerAnimationSource.CaptureAsync(original);
        var disc = UniversalDiscImage.Read(staged);
        plan.RequireResidentEntry8(disc.EntryBytes(8));
        byte[] exe = plan.ApplyToExecutable(disc.Exe);
        List<NativeFreeFlightMemoryEvidence> memory = [];
        foreach (var level in source.Levels)
        {
            byte[] body = disc.EntryBytes(level.DataWadEntry);
            if (!plan.MergeLevelData(level.LevelId, body).CopyLevelData().AsSpan().SequenceEqual(body))
                throw new InvalidDataException("Resident global flight unexpectedly changed a staged level body.");
            if (NativeFreeFlightCompositionPlan.IsNormalLevel(level.LevelId))
                memory.Add(plan.RequireFinalLevelClosure(level.LevelId, body, exe));
        }
        bool fresh = UniversalExeBindings.Word(disc.Exe, UniversalExeBindings.LevelInitJumpTable + 65 * 4) !=
            UniversalExeBindings.Word(plan.ExecutablePatch.CopySourceExecutable(), UniversalExeBindings.LevelInitJumpTable + 65 * 4);
        if (fresh) memory.Add(plan.RequireFinalLevelClosure(65, disc.EntryBytes(80), exe));
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        string temporary = output + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            disc.WriteImage(temporary, disc.Wad, exe);
            var readback = UniversalDiscImage.Read(temporary);
            if (!readback.Exe.AsSpan().SequenceEqual(exe) || !readback.Wad.AsSpan().SequenceEqual(disc.Wad))
                throw new InvalidDataException("Flight verification candidate readback changed staged WAD or final executable.");
            File.Move(temporary, output);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        string cue = Path.ChangeExtension(output, ".cue");
        File.WriteAllLines(cue, ["FILE \"" + Path.GetFileName(output) + "\" BINARY", "  TRACK 01 MODE2/2352", "    INDEX 01 00:00:00"]);
        using FileStream final = File.OpenRead(output);
        File.WriteAllText(Path.ChangeExtension(output, ".flight.json"), JsonSerializer.Serialize(new
        {
            original, staged, output, cue, sourceSha256 = plan.SourceImageSha256,
            candidateSha256 = Convert.ToHexStringLower(await SHA256.HashDataAsync(final)),
            planSha256 = plan.ExecutablePatch.PlanSha256, stagedWadUnchanged = true, addedLevelBytes = 0,
            fresh65 = fresh, memory, runtimeVerified = false
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("NATIVE_GLOBAL_FLIGHT_CANDIDATE_OK " + cue);
    }
}
