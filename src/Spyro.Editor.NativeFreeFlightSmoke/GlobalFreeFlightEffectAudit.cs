using System.Text.Json;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Universal;

internal static class GlobalFreeFlightEffectAudit
{
    internal static async Task RunAsync(string original, string report, string? freshCandidate = null)
    {
        var source = await NativePlayerAnimationSource.CaptureAsync(original);
        var disc = UniversalDiscImage.Read(original);
        var fresh = freshCandidate == null ? null : UniversalDiscImage.Read(freshCandidate);
        var owners = source.Levels.Select(l => (l.LevelId, l.DataWadEntry)).ToList();
        if (fresh != null) owners.Add((65, 80));
        List<object> rows = [];
        foreach (var level in owners)
        {
            if (!NativeFreeFlightCompositionPlan.IsNormalLevel(level.LevelId) && level.LevelId != 65) continue;
            var ownerDisc = level.LevelId == 65 ? fresh! : disc;
            var overlay = new UniversalOverlay(ownerDisc.EntryBytes(level.DataWadEntry - 1), UniversalExeBindings.Read(ownerDisc.Exe, level.LevelId));
            uint spawn = overlay.Bindings[UniversalExeBindings.ParticleSpawnGlobal].Value;
            List<UniversalJumpTable> tables = [];
            for (uint pc = spawn; pc < Math.Min(spawn + 0x180, overlay.End); pc += 4)
                if (UniversalMips.IsJr(overlay.Word(pc)) && UniversalMips.Rs(overlay.Word(pc)) != 31)
                    if (UniversalDispatchAnalysis.ResolveJumpTable(overlay, pc) is { } table) tables.Add(table);
            rows.Add(new { level.LevelId, spawn = $"{spawn:X8}",
                callbackHex = Convert.ToHexStringLower(overlay.Bytes.Slice((int)(spawn - UniversalOverlay.Base),
                    (int)(overlay.FunctionEnd(spawn) - spawn))), tables = tables.Select(t => new
            {
                table = $"{t.TableAddress:X8}", dispatch = $"{t.JumpRegisterPc:X8}", t.Targets.Count,
                allTargets = t.Targets,
                targets = new[] { 0x1A, 0x1B }.Where(i => i < t.Targets.Count).Select(i => new
                {
                    selector = i, target = $"{t.Targets[i]:X8}",
                    words = Enumerable.Range(0, 12).TakeWhile(j => overlay.Contains(t.Targets[i] + (uint)j * 4))
                        .Select(j => $"{overlay.Word(t.Targets[i] + (uint)j * 4):X8}").ToArray()
                }).ToArray()
            }).ToArray(), runtimeVerified = false });
        }
        File.WriteAllText(report, JsonSerializer.Serialize(new { source.SourceImageSha256, rows,
            freshCandidate,
            interpretation = "Source dispatch target extraction only; selector-index and no-op behavior require instruction review.",
            runtimeVerified = false }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("GLOBAL_FLIGHT_EFFECT_SOURCE_AUDIT_OK rows=" + rows.Count);
    }
}
