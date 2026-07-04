using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Analysis;

public static class TerrainBehaviorRamPairComparer
{
    private const int MainRamSize = 0x200000;
    private const uint PsxRamBase = 0x80000000;
    private const int LevelMobyPointerOffset = 0x75828;
    private const int RuntimeRecordStride = 0x58;
    private const int ControlRecordBytes = 0x58;
    private const int SpecialWindowBytes = 128;
    private const int SpyroStateOffset = 0x78A58;
    private const int SpyroStateWindowBytes = 0x220;
    private const string SpyroFieldSource = "c0mposer Spyro 1 reverse-engineering symbol map/labeled globals";

    public static TerrainBehaviorRamPairReport Compare(byte[] beforeBytes, byte[] afterBytes, TerrainBehaviorRamPairOptions options)
    {
        RamWindow before = FindRamWindow(beforeBytes);
        RamWindow after = FindRamWindow(afterBytes);
        TerrainBehaviorGlobals beforeGlobals = ReadGlobals(before.Ram);
        TerrainBehaviorGlobals afterGlobals = ReadGlobals(after.Ram);
        SpyroPlayerSnapshot beforeSpyro = ReadSpyroPlayerSnapshot(before.Ram);
        SpyroPlayerSnapshot afterSpyro = ReadSpyroPlayerSnapshot(after.Ram);
        IReadOnlyList<TerrainBehaviorFieldChange> globalChanges = CompareGlobals(beforeGlobals, afterGlobals);
        IReadOnlyList<TerrainBehaviorFieldChange> spyroChanges = CompareSpyroPlayerSnapshots(beforeSpyro, afterSpyro);
        List<ExcludedRange> excludedRanges =
        [
            new(0x75600, 0x500, "known globals neighborhood"),
            new(SpyroStateOffset, SpyroStateWindowBytes, "Spyro player state")
        ];

        List<TerrainBehaviorFocusControlDiff> focusDiffs = new();
        foreach (TerrainBehaviorFocusControl focus in options.FocusControls.OrderBy(item => item.TrueIndex))
        {
            MobySnapshot beforeRecord = ReadMobySnapshot(before.Ram, focus.TrueIndex);
            MobySnapshot afterRecord = ReadMobySnapshot(after.Ram, focus.TrueIndex);
            if (beforeRecord.Found && beforeRecord.RamOffset >= 0)
                excludedRanges.Add(new ExcludedRange(beforeRecord.RamOffset, ControlRecordBytes, $"focus control T{focus.TrueIndex}"));

            ByteRangeDiff mainDiff = CompareRange(before.Ram, after.Ram, beforeRecord.RamOffset, ControlRecordBytes, $"focus control T{focus.TrueIndex}");
            IReadOnlyList<TerrainBehaviorFieldChange> fieldChanges = CompareMobySnapshots(beforeRecord, afterRecord);
            int specialOffset = PointerToOffset(beforeRecord.SpecialDataPointerValue);
            if (specialOffset >= 0)
                excludedRanges.Add(new ExcludedRange(specialOffset, SpecialWindowBytes, $"focus special T{focus.TrueIndex}"));

            focusDiffs.Add(new TerrainBehaviorFocusControlDiff(
                Focus: focus,
                BeforeRecord: beforeRecord,
                AfterRecord: afterRecord,
                FieldChanges: fieldChanges,
                MainRecordDiff: mainDiff,
                SpecialDataDiff: CompareRange(before.Ram, after.Ram, specialOffset, SpecialWindowBytes, $"special data T{focus.TrueIndex}")));
        }

        IReadOnlyList<ByteRangeDiff> watchedRanges =
        [
            CompareRange(before.Ram, after.Ram, SpyroStateOffset, SpyroStateWindowBytes, "Spyro player state"),
            CompareRange(before.Ram, after.Ram, 0x75600, 0x500, "known globals neighborhood"),
            CompareRange(before.Ram, after.Ram, 0x77900, 1231, "collectable flags"),
            CompareRange(before.Ram, after.Ram, 0x70000, 0x8000, "loaded executable/state neighborhood")
        ];

        IReadOnlyList<ChangedCluster> clusters = BuildChangedClusters(before.Ram, after.Ram, excludedRanges, options.MaxChangedClusters);
        string interpretation = Interpret(globalChanges, beforeSpyro, afterSpyro, spyroChanges, focusDiffs);
        return new TerrainBehaviorRamPairReport(
            GeneratedAt: DateTimeOffset.Now.ToString("s"),
            LevelKey: options.LevelKey,
            TerrainEvent: options.TerrainEvent,
            SpyroFieldSource: SpyroFieldSource,
            Before: new TerrainBehaviorRamSummary(before.BaseOffset, before.MobyCount, beforeGlobals, beforeSpyro),
            After: new TerrainBehaviorRamSummary(after.BaseOffset, after.MobyCount, afterGlobals, afterSpyro),
            GlobalChanges: globalChanges,
            SpyroChanges: spyroChanges,
            WatchedRanges: watchedRanges,
            FocusControls: focusDiffs,
            ChangedClusters: clusters,
            Interpretation: interpretation,
            NextRepeatabilityRule: "Promote a terrain behavior only after the same byte/record pattern repeats for that terrain type and does not appear in a solid-ground control capture.");
    }

    private static RamWindow FindRamWindow(byte[] bytes)
    {
        if (bytes.Length == MainRamSize)
        {
            uint pointer = ReadUInt32(bytes, LevelMobyPointerOffset);
            int count = CountPlausibleMobys(bytes, pointer);
            if (!IsPsxPointer(pointer) || count == 0)
                throw new InvalidDataException($"This 2 MB file does not look like live Spyro RAM. ptr=0x{pointer:X8}, mobys={count}.");

            return new RamWindow(bytes, 0, pointer, count);
        }

        if (bytes.Length < MainRamSize)
            throw new InvalidDataException($"RAM dump is smaller than 2 MB: {bytes.Length} bytes.");

        RamWindow? best = null;
        for (int offset = 0; offset <= bytes.Length - MainRamSize; offset += 0x1000)
        {
            uint pointer = ReadUInt32(bytes, offset + LevelMobyPointerOffset);
            int count = CountPlausibleMobys(bytes.AsSpan(offset, MainRamSize), pointer);
            if (best == null || count > best.MobyCount)
            {
                byte[] ram = new byte[MainRamSize];
                Buffer.BlockCopy(bytes, offset, ram, 0, MainRamSize);
                best = new RamWindow(ram, offset, pointer, count);
            }
        }

        return best is { MobyCount: > 0 }
            ? best
            : throw new InvalidDataException("Could not find a plausible Spyro main RAM window in the dump.");
    }

    private static int CountPlausibleMobys(ReadOnlySpan<byte> ram, uint pointer)
    {
        int start = PointerToOffset(pointer);
        if (start < 0 || start + RuntimeRecordStride > ram.Length)
            return 0;

        int count = 0;
        int badRun = 0;
        for (int i = 0; i < 512; i++)
        {
            int offset = start + (i * RuntimeRecordStride);
            if (offset + RuntimeRecordStride > ram.Length)
                break;

            if (IsPlausibleMoby(ram, offset))
            {
                count++;
                badRun = 0;
            }
            else
            {
                badRun++;
                if (count > 8 && badRun >= 24)
                    break;
            }
        }

        return count;
    }

    private static bool IsPlausibleMoby(ReadOnlySpan<byte> ram, int offset)
    {
        if (offset < 0 || offset + RuntimeRecordStride > ram.Length)
            return false;

        int type = ram[offset + 0x50];
        int state = ram[offset + 0x51];
        if (type <= 0 || type > 0x7F || state > 0x7F)
            return false;

        uint special = ReadUInt32(ram, offset + 0x08);
        if (special != 0 && !IsPsxPointer(special))
            return false;

        int x = ReadInt32(ram, offset + 0x0C);
        int y = ReadInt32(ram, offset + 0x10);
        int z = ReadInt32(ram, offset + 0x14);
        if (Math.Abs((long)x) > 4_000_000 || Math.Abs((long)y) > 4_000_000 || Math.Abs((long)z) > 4_000_000)
            return false;

        return Math.Abs((long)x) >= 16 || Math.Abs((long)y) >= 16 || Math.Abs((long)z) >= 16;
    }

    private static TerrainBehaviorGlobals ReadGlobals(byte[] ram)
    {
        return new TerrainBehaviorGlobals(
            PtrDynamicLevelMobys: FormatHex(ReadUInt32(ram, 0x7573C)),
            GlobalDragonCount: ReadInt32(ram, 0x75750),
            GlobalEggCount: ReadInt32(ram, 0x75810),
            PtrLevelMobys: FormatHex(ReadUInt32(ram, LevelMobyPointerOffset)),
            GlobalGemCount: ReadUInt16(ram, 0x75860),
            LevelId: FormatHex(ReadUInt32(ram, 0x758B4)),
            PtrLevelMobySpecialData: FormatHex(ReadUInt32(ram, 0x75930)));
    }

    private static IReadOnlyList<TerrainBehaviorFieldChange> CompareGlobals(TerrainBehaviorGlobals before, TerrainBehaviorGlobals after)
    {
        List<TerrainBehaviorFieldChange> changes = new();
        AddIfChanged(changes, "ptrDynamicLevelMobys", before.PtrDynamicLevelMobys, after.PtrDynamicLevelMobys);
        AddIfChanged(changes, "globalDragonCount", before.GlobalDragonCount, after.GlobalDragonCount);
        AddIfChanged(changes, "globalEggCount", before.GlobalEggCount, after.GlobalEggCount);
        AddIfChanged(changes, "ptrLevelMobys", before.PtrLevelMobys, after.PtrLevelMobys);
        AddIfChanged(changes, "globalGemCount", before.GlobalGemCount, after.GlobalGemCount);
        AddIfChanged(changes, "levelId", before.LevelId, after.LevelId);
        AddIfChanged(changes, "ptrLevelMobySpecialData", before.PtrLevelMobySpecialData, after.PtrLevelMobySpecialData);
        return changes;
    }

    private static SpyroPlayerSnapshot ReadSpyroPlayerSnapshot(byte[] ram)
    {
        int x = ReadInt32(ram, 0x78A58);
        int y = ReadInt32(ram, 0x78A5C);
        int z = ReadInt32(ram, 0x78A60);
        return new SpyroPlayerSnapshot(
            PositionAddress: FormatRuntimeAddress(0x78A58),
            PositionRawX: x,
            PositionRawY: y,
            PositionRawZ: z,
            Position: new Vector3f(x / 16f, y / 16f, z / 16f),
            StateHex: FormatHex(ReadUInt32(ram, 0x78AD0)),
            StateSubHex: FormatHex(ReadUInt32(ram, 0x78AD4)),
            StateFrames: ReadInt32(ram, 0x78AD8),
            Grounded: ReadInt32(ram, 0x78BB4),
            IFrames: ReadInt32(ram, 0x78BB8),
            Health: ReadInt32(ram, 0x78BBC),
            IsGliding: ReadInt32(ram, 0x78BAC));
    }

    private static IReadOnlyList<TerrainBehaviorFieldChange> CompareSpyroPlayerSnapshots(SpyroPlayerSnapshot before, SpyroPlayerSnapshot after)
    {
        List<TerrainBehaviorFieldChange> changes = new();
        AddIfChanged(changes, "positionRawX", before.PositionRawX, after.PositionRawX);
        AddIfChanged(changes, "positionRawY", before.PositionRawY, after.PositionRawY);
        AddIfChanged(changes, "positionRawZ", before.PositionRawZ, after.PositionRawZ);
        AddIfChanged(changes, "stateHex", before.StateHex, after.StateHex);
        AddIfChanged(changes, "stateSubHex", before.StateSubHex, after.StateSubHex);
        AddIfChanged(changes, "stateFrames", before.StateFrames, after.StateFrames);
        AddIfChanged(changes, "grounded", before.Grounded, after.Grounded);
        AddIfChanged(changes, "iFrames", before.IFrames, after.IFrames);
        AddIfChanged(changes, "health", before.Health, after.Health);
        AddIfChanged(changes, "isGliding", before.IsGliding, after.IsGliding);
        return changes;
    }

    private static MobySnapshot ReadMobySnapshot(byte[] ram, int trueIndex)
    {
        uint pointer = ReadUInt32(ram, LevelMobyPointerOffset);
        int start = PointerToOffset(pointer);
        if (start < 0)
            return MobySnapshot.Missing(trueIndex, "invalid level moby pointer");

        int offset = start + (trueIndex * RuntimeRecordStride);
        if (offset < 0 || offset + RuntimeRecordStride > ram.Length)
            return MobySnapshot.Missing(trueIndex, "record outside RAM");

        uint special = ReadUInt32(ram, offset + 0x08);
        return new MobySnapshot(
            TrueIndex: trueIndex,
            Found: true,
            Reason: "",
            RamOffset: offset,
            RuntimeAddress: FormatRuntimeAddress(offset),
            SpecialDataPointer: FormatHex(special),
            SpecialDataPointerValue: special,
            Position: new Vector3f(
                ReadInt32(ram, offset + 0x0C) / 16f,
                ReadInt32(ram, offset + 0x10) / 16f,
                ReadInt32(ram, offset + 0x14) / 16f),
            TypeHex: FormatHexByte(ram[offset + 0x50]),
            StateHex: FormatHexByte(ram[offset + 0x51]),
            Flag4AHex: FormatHexByte(ram[offset + 0x52]),
            Flag4BHex: FormatHexByte(ram[offset + 0x53]));
    }

    private static IReadOnlyList<TerrainBehaviorFieldChange> CompareMobySnapshots(MobySnapshot before, MobySnapshot after)
    {
        List<TerrainBehaviorFieldChange> changes = new();
        AddIfChanged(changes, "found", before.Found, after.Found);
        AddIfChanged(changes, "specialDataPointer", before.SpecialDataPointer, after.SpecialDataPointer);
        AddIfChanged(changes, "x", before.Position.X, after.Position.X);
        AddIfChanged(changes, "y", before.Position.Y, after.Position.Y);
        AddIfChanged(changes, "z", before.Position.Z, after.Position.Z);
        AddIfChanged(changes, "typeHex", before.TypeHex, after.TypeHex);
        AddIfChanged(changes, "stateHex", before.StateHex, after.StateHex);
        AddIfChanged(changes, "flag4AHex", before.Flag4AHex, after.Flag4AHex);
        AddIfChanged(changes, "flag4BHex", before.Flag4BHex, after.Flag4BHex);
        return changes;
    }

    private static ByteRangeDiff CompareRange(byte[] before, byte[] after, int offset, int length, string label)
    {
        if (offset < 0 || offset >= before.Length || offset >= after.Length)
            return new ByteRangeDiff(label, false, "", "", 0, 0, [], "offset outside RAM");

        int byteCount = Math.Min(length, Math.Min(before.Length - offset, after.Length - offset));
        int changed = 0;
        List<ChangedByte> samples = new();
        for (int i = 0; i < byteCount; i++)
        {
            byte oldValue = before[offset + i];
            byte newValue = after[offset + i];
            if (oldValue == newValue)
                continue;

            changed++;
            if (samples.Count < 32)
            {
                samples.Add(new ChangedByte(
                    Offset: $"+0x{i:X}",
                    RamOffset: FormatOffset(offset + i),
                    RuntimeAddress: FormatRuntimeAddress(offset + i),
                    BeforeHex: FormatHexByte(oldValue),
                    AfterHex: FormatHexByte(newValue)));
            }
        }

        return new ByteRangeDiff(label, true, FormatOffset(offset), FormatRuntimeAddress(offset), byteCount, changed, samples, "");
    }

    private static IReadOnlyList<ChangedCluster> BuildChangedClusters(byte[] before, byte[] after, IReadOnlyList<ExcludedRange> excludedRanges, int maxClusters)
    {
        const int blockSize = 256;
        int limit = Math.Min(before.Length, after.Length);
        List<ChangedCluster> clusters = new();
        for (int offset = 0; offset < limit; offset += blockSize)
        {
            int byteCount = Math.Min(blockSize, limit - offset);
            if (excludedRanges.Any(range => offset < range.Start + range.Length && offset + byteCount > range.Start))
                continue;

            int changed = 0;
            List<string> firstChanges = new();
            for (int i = 0; i < byteCount; i++)
            {
                byte oldValue = before[offset + i];
                byte newValue = after[offset + i];
                if (oldValue == newValue)
                    continue;

                changed++;
                if (firstChanges.Count < 8)
                    firstChanges.Add($"+0x{i:X}:{oldValue:X2}->{newValue:X2}");
            }

            if (changed > 0)
                clusters.Add(new ChangedCluster(FormatOffset(offset), FormatRuntimeAddress(offset), byteCount, changed, firstChanges));
        }

        return clusters
            .OrderByDescending(cluster => cluster.ChangedByteCount)
            .ThenBy(cluster => cluster.RamOffset, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(1, maxClusters))
            .ToList();
    }

    private static string Interpret(
        IReadOnlyList<TerrainBehaviorFieldChange> globalChanges,
        SpyroPlayerSnapshot beforeSpyro,
        SpyroPlayerSnapshot afterSpyro,
        IReadOnlyList<TerrainBehaviorFieldChange> spyroChanges,
        IReadOnlyList<TerrainBehaviorFocusControlDiff> focusDiffs)
    {
        if (globalChanges.Any(change => string.Equals(change.Field, "levelId", StringComparison.OrdinalIgnoreCase)))
            return "The level id changed between captures. Treat this as a load/transition sample, not a clean terrain response sample.";

        bool counterChanged = globalChanges.Any(change => change.Field is "globalGemCount" or "globalDragonCount" or "globalEggCount");
        bool healthDropped = afterSpyro.Health < beforeSpyro.Health;
        bool iFramesStarted = afterSpyro.IFrames > beforeSpyro.IFrames;
        bool stateChanged = spyroChanges.Any(change => change.Field is "stateHex" or "stateSubHex" or "grounded" or "isGliding");
        bool positionChanged = spyroChanges.Any(change => change.Field is "positionRawX" or "positionRawY" or "positionRawZ");
        bool controlChanged = focusDiffs.Any(diff =>
            diff.FieldChanges.Count > 0 ||
            diff.MainRecordDiff.ChangedByteCount > 0 ||
            diff.SpecialDataDiff.ChangedByteCount > 0);

        if (healthDropped || iFramesStarted)
            return "Spyro health or invulnerability frames changed across the capture. This is direct player-response evidence; repeat it against a solid-ground control and another sample of the same terrain type before promoting the behavior.";
        if (stateChanged)
            return "Spyro state fields changed across the capture without a level transition. This is useful direct player-response evidence, especially if it repeats for one terrain type and not solid ground.";
        if (controlChanged)
            return "At least one nearby hazard-control candidate changed. This is the strongest lead for control-record-driven terrain behavior.";
        if (positionChanged && !counterChanged)
            return "Spyro moved but no named damage/state field changed. This may be a movement-only sample; capture closer to the exact terrain contact frame.";
        if (counterChanged)
            return "Persistent counters changed. This sample includes a pickup/rescue side effect, so use a cleaner terrain-only capture for behavior proof.";
        return "No focused terrain-response signal was isolated. Use a closer before/after pair around the moment Spyro touches the terrain.";
    }

    private static void AddIfChanged(List<TerrainBehaviorFieldChange> changes, string field, object? before, object? after)
    {
        string beforeText = Convert.ToString(before, System.Globalization.CultureInfo.InvariantCulture) ?? "";
        string afterText = Convert.ToString(after, System.Globalization.CultureInfo.InvariantCulture) ?? "";
        if (!string.Equals(beforeText, afterText, StringComparison.Ordinal))
            changes.Add(new TerrainBehaviorFieldChange(field, beforeText, afterText));
    }

    private static int PointerToOffset(uint value)
    {
        return IsPsxPointer(value) ? (int)(value - PsxRamBase) : -1;
    }

    private static bool IsPsxPointer(uint value)
    {
        return value >= PsxRamBase && value < PsxRamBase + MainRamSize && (value & 0x03) == 0;
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> bytes, int offset)
    {
        return offset >= 0 && offset + 2 <= bytes.Length
            ? BitConverter.ToUInt16(bytes[offset..(offset + 2)])
            : (ushort)0;
    }

    private static uint ReadUInt32(ReadOnlySpan<byte> bytes, int offset)
    {
        return offset >= 0 && offset + 4 <= bytes.Length
            ? BitConverter.ToUInt32(bytes[offset..(offset + 4)])
            : 0;
    }

    private static int ReadInt32(ReadOnlySpan<byte> bytes, int offset)
    {
        return offset >= 0 && offset + 4 <= bytes.Length
            ? BitConverter.ToInt32(bytes[offset..(offset + 4)])
            : 0;
    }

    private static string FormatHex(uint value) => $"0x{value:X8}";
    private static string FormatHexByte(byte value) => $"0x{value:X2}";
    private static string FormatOffset(int offset) => offset < 0 ? "" : $"0x{offset:X}";
    private static string FormatRuntimeAddress(int offset) => offset < 0 ? "" : $"0x{PsxRamBase + (uint)offset:X8}";

    private sealed record RamWindow(byte[] Ram, int BaseOffset, uint Pointer, int MobyCount);
    private sealed record ExcludedRange(int Start, int Length, string Label);
}

public sealed record TerrainBehaviorRamPairOptions(
    string LevelKey,
    string TerrainEvent,
    IReadOnlyList<TerrainBehaviorFocusControl> FocusControls,
    int MaxChangedClusters = 24);

public sealed record TerrainBehaviorFocusControl(
    int TrueIndex,
    string Label,
    string HazardSurface,
    int HazardTextureId,
    string HazardRuntimeKey,
    double? HazardDistance,
    double? HazardZDelta);

public sealed record TerrainBehaviorRamPairReport(
    string GeneratedAt,
    string LevelKey,
    string TerrainEvent,
    string SpyroFieldSource,
    TerrainBehaviorRamSummary Before,
    TerrainBehaviorRamSummary After,
    IReadOnlyList<TerrainBehaviorFieldChange> GlobalChanges,
    IReadOnlyList<TerrainBehaviorFieldChange> SpyroChanges,
    IReadOnlyList<ByteRangeDiff> WatchedRanges,
    IReadOnlyList<TerrainBehaviorFocusControlDiff> FocusControls,
    IReadOnlyList<ChangedCluster> ChangedClusters,
    string Interpretation,
    string NextRepeatabilityRule);

public sealed record TerrainBehaviorRamSummary(
    int RamBaseOffset,
    int DecodedMobys,
    TerrainBehaviorGlobals Globals,
    SpyroPlayerSnapshot Spyro);

public sealed record TerrainBehaviorGlobals(
    string PtrDynamicLevelMobys,
    int GlobalDragonCount,
    int GlobalEggCount,
    string PtrLevelMobys,
    int GlobalGemCount,
    string LevelId,
    string PtrLevelMobySpecialData);

public sealed record TerrainBehaviorFieldChange(string Field, string Before, string After);

public sealed record SpyroPlayerSnapshot(
    string PositionAddress,
    int PositionRawX,
    int PositionRawY,
    int PositionRawZ,
    Vector3f Position,
    string StateHex,
    string StateSubHex,
    int StateFrames,
    int Grounded,
    int IFrames,
    int Health,
    int IsGliding);

public sealed record TerrainBehaviorFocusControlDiff(
    TerrainBehaviorFocusControl Focus,
    MobySnapshot BeforeRecord,
    MobySnapshot AfterRecord,
    IReadOnlyList<TerrainBehaviorFieldChange> FieldChanges,
    ByteRangeDiff MainRecordDiff,
    ByteRangeDiff SpecialDataDiff);

public sealed record MobySnapshot(
    int TrueIndex,
    bool Found,
    string Reason,
    int RamOffset,
    string RuntimeAddress,
    string SpecialDataPointer,
    uint SpecialDataPointerValue,
    Vector3f Position,
    string TypeHex,
    string StateHex,
    string Flag4AHex,
    string Flag4BHex)
{
    public static MobySnapshot Missing(int trueIndex, string reason)
    {
        return new MobySnapshot(trueIndex, false, reason, -1, "", "", 0, new Vector3f(), "", "", "", "");
    }
}

public sealed record ByteRangeDiff(
    string Label,
    bool Valid,
    string RamOffset,
    string RuntimeAddress,
    int ByteCount,
    int ChangedByteCount,
    IReadOnlyList<ChangedByte> ChangedBytes,
    string Reason);

public sealed record ChangedByte(
    string Offset,
    string RamOffset,
    string RuntimeAddress,
    string BeforeHex,
    string AfterHex);

public sealed record ChangedCluster(
    string RamOffset,
    string RuntimeAddress,
    int ByteCount,
    int ChangedByteCount,
    IReadOnlyList<string> FirstChanges);
