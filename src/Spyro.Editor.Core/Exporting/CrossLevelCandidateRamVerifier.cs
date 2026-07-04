using System.Text.Json;

namespace Spyro.Editor.Core.Exporting;

public static class CrossLevelCandidateRamVerifier
{
    private const int PointerLevelMobysRamOffset = 0x75828;
    private const int PointerDynamicMobysRamOffset = 0x7573C;

    private static readonly int[] FullRecordOffsetsToCheck =
    [
        0x0C, 0x10, 0x14,
        0x36, 0x37, 0x4F,
        0x50, 0x51, 0x52, 0x53
    ];

    public static CrossLevelCandidateRamVerification VerifyPlanFile(string planPath, string ramPath)
    {
        using FileStream stream = File.OpenRead(planPath);
        MobySourcePatchPlan? plan = JsonSerializer.Deserialize<MobySourcePatchPlan>(stream);
        if (plan == null)
            throw new InvalidDataException($"Could not read candidate patch plan {planPath}.");
        return Verify(plan, ramPath);
    }

    public static CrossLevelCandidateRamVerification Verify(MobySourcePatchPlan plan, string ramPath)
    {
        byte[] ram = File.ReadAllBytes(ramPath);
        if (ram.Length < PointerLevelMobysRamOffset + 4)
        {
            return new CrossLevelCandidateRamVerification(
                RamPath: ramPath,
                RamReadable: false,
                BootedLevel: false,
                ObjectRecordsMatch: false,
                ActorRootsMatch: false,
                RuntimeMobyPointer: "",
                RuntimeMobyCount: 0,
                ExpectedRecordCount: ExpectedMinimumRecordCount(plan),
                Checks: [],
                RootChecks: [],
                Summary: "RAM dump is too small to contain Spyro's level moby pointer.");
        }

        uint pointer = BitConverter.ToUInt32(ram, PointerLevelMobysRamOffset);
        int tableOffset = (int)(pointer & 0x001FFFFF);
        int expectedRecordCount = ExpectedMinimumRecordCount(plan);
        bool bootedLevel = pointer >= 0x80000000 &&
            tableOffset >= 0 &&
            tableOffset + Math.Max(plan.SourceRecordCount, expectedRecordCount) * plan.RecordStride <= ram.Length;
        int runtimeCount = bootedLevel
            ? DetermineRuntimeCount(ram, tableOffset, plan.RecordStride, expectedRecordCount)
            : 0;

        List<CrossLevelCandidateRamRecordCheck> checks = [];
        List<CrossLevelCandidateRamRootCheck> rootChecks = [];
        if (bootedLevel)
        {
            foreach (IGrouping<int, MobySourcePatch> group in plan.Patches
                .Where(patch => patch.TrueIndex >= 0)
                .GroupBy(patch => patch.TrueIndex)
                .OrderBy(group => group.Key))
            {
                checks.Add(VerifyRecordPatchGroup(plan, ram, tableOffset, runtimeCount, group.Key, group.ToList()));
            }
            rootChecks.AddRange(VerifyActorRoots(plan, ram));
        }

        bool objectRecordsMatch = bootedLevel &&
            checks.Count > 0 &&
            checks.Where(check => check.ExpectedBytes > 0).All(check => check.Matched);
        bool actorRootsMatch = bootedLevel &&
            (rootChecks.Count == 0 || rootChecks.All(check => check.Matched));
        string summary = BuildSummary(bootedLevel, objectRecordsMatch, actorRootsMatch, runtimeCount, expectedRecordCount, checks, rootChecks);

        return new CrossLevelCandidateRamVerification(
            RamPath: ramPath,
            RamReadable: true,
            BootedLevel: bootedLevel,
            ObjectRecordsMatch: objectRecordsMatch,
            ActorRootsMatch: actorRootsMatch,
            RuntimeMobyPointer: $"0x{pointer:X8}",
            RuntimeMobyCount: runtimeCount,
            ExpectedRecordCount: expectedRecordCount,
            Checks: checks,
            RootChecks: rootChecks,
            Summary: summary);
    }

    private static IEnumerable<CrossLevelCandidateRamRootCheck> VerifyActorRoots(MobySourcePatchPlan plan, byte[] ram)
    {
        foreach (MobyActorPackageRootPreview root in plan.PackageImportPreviews.SelectMany(preview => preview.RootEntries))
        {
            uint targetRoot = ParseHexUInt32(root.TargetRoot);
            ushort actorId = (ushort)ParseHexUInt32(root.TargetActorId);
            int rootSlot = checked((int)ParseHexUInt32(root.TargetRootSlot));
            int rootIndex = root.RootIndex >= 0 ? root.RootIndex : (rootSlot - 0x50) / 4;
            int actorIdDelta = 0x100 - (rootIndex * 2);
            int foundOffset = FindRootActorPair(ram, targetRoot, actorId, actorIdDelta);
            yield return new CrossLevelCandidateRamRootCheck(
                Slot: root.TargetRootSlot,
                ActorId: root.TargetActorId,
                TargetRoot: root.TargetRoot,
                Matched: foundOffset >= 0,
                RamOffset: foundOffset >= 0 ? $"0x{foundOffset:X}" : "",
                Details: foundOffset >= 0
                    ? $"found root/actor-id pair at RAM offset 0x{foundOffset:X}"
                    : $"could not find root 0x{targetRoot:X8} with actor ID 0x{actorId:X4} at root-table actor-id delta 0x{actorIdDelta:X}");
        }
    }

    private static int FindRootActorPair(byte[] ram, uint targetRoot, ushort actorId, int actorIdDelta)
    {
        if (actorIdDelta < 0)
            return -1;

        byte[] rootBytes = BitConverter.GetBytes(targetRoot);
        byte[] actorBytes = BitConverter.GetBytes(actorId);
        for (int offset = 0; offset + 4 < ram.Length && offset + actorIdDelta + 2 <= ram.Length; offset++)
        {
            if (ram[offset] == rootBytes[0] &&
                ram[offset + 1] == rootBytes[1] &&
                ram[offset + 2] == rootBytes[2] &&
                ram[offset + 3] == rootBytes[3] &&
                ram[offset + actorIdDelta] == actorBytes[0] &&
                ram[offset + actorIdDelta + 1] == actorBytes[1])
            {
                return offset;
            }
        }

        return -1;
    }

    private static CrossLevelCandidateRamRecordCheck VerifyRecordPatchGroup(
        MobySourcePatchPlan plan,
        byte[] ram,
        int tableOffset,
        int runtimeCount,
        int trueIndex,
        IReadOnlyList<MobySourcePatch> patches)
    {
        if (trueIndex >= runtimeCount)
        {
            return new CrossLevelCandidateRamRecordCheck(
                TrueIndex: trueIndex,
                Label: patches.FirstOrDefault()?.MobyLabel ?? $"T{trueIndex}",
                ExpectedBytes: 0,
                MatchedBytes: 0,
                Matched: false,
                Details: $"T{trueIndex} is beyond the runtime table count {runtimeCount}.");
        }

        int recordOffset = tableOffset + trueIndex * plan.RecordStride;
        List<(int Offset, byte Expected)> expected = [];
        foreach (MobySourcePatch patch in patches)
        {
            byte[] after = ParseHexPreview(patch.AfterHexPreview);
            if (after.Length == 0)
                continue;

            if (IsFullRecordPatch(patch) && after.Length >= plan.RecordStride)
            {
                foreach (int offset in FullRecordOffsetsToCheck)
                    expected.Add((offset, after[offset]));
            }
            else if (TryParseRecordOffset(patch.RecordOffset, out int byteOffset) && byteOffset >= 0 && byteOffset < plan.RecordStride)
            {
                for (int i = 0; i < after.Length && byteOffset + i < plan.RecordStride; i++)
                    expected.Add((byteOffset + i, after[i]));
            }
        }

        List<string> mismatches = [];
        int matched = 0;
        foreach ((int offset, byte expectedByte) in expected.Distinct())
        {
            byte actual = ram[recordOffset + offset];
            if (actual == expectedByte)
            {
                matched++;
            }
            else
            {
                mismatches.Add($"+0x{offset:X2} expected 0x{expectedByte:X2}, got 0x{actual:X2}");
            }
        }

        string label = patches.FirstOrDefault(patch => !string.IsNullOrWhiteSpace(patch.MobyLabel))?.MobyLabel ?? $"T{trueIndex}";
        return new CrossLevelCandidateRamRecordCheck(
            TrueIndex: trueIndex,
            Label: label,
            ExpectedBytes: expected.Distinct().Count(),
            MatchedBytes: matched,
            Matched: mismatches.Count == 0 && expected.Count > 0,
            Details: mismatches.Count == 0 ? "runtime record matches expected candidate bytes" : string.Join("; ", mismatches.Take(6)));
    }

    private static int DetermineRuntimeCount(byte[] ram, int tableOffset, int stride, int expectedRecordCount)
    {
        if (ram.Length >= PointerDynamicMobysRamOffset + 4)
        {
            uint dynamicPointer = BitConverter.ToUInt32(ram, PointerDynamicMobysRamOffset);
            int dynamicOffset = (int)(dynamicPointer & 0x001FFFFF);
            if (dynamicOffset > tableOffset && dynamicOffset <= ram.Length && ((dynamicOffset - tableOffset) % stride) == 0)
                return Math.Max(expectedRecordCount, Math.Min(512, (dynamicOffset - tableOffset) / stride));
        }

        return Math.Min(512, (ram.Length - tableOffset) / stride);
    }

    private static int ExpectedMinimumRecordCount(MobySourcePatchPlan plan)
    {
        int maxPatchedRecord = plan.Patches
            .Where(patch => patch.TrueIndex >= 0)
            .Select(patch => patch.TrueIndex + 1)
            .DefaultIfEmpty(plan.SourceRecordCount)
            .Max();
        return Math.Max(plan.SourceRecordCount, maxPatchedRecord);
    }

    private static bool IsFullRecordPatch(MobySourcePatch patch)
    {
        return string.Equals(patch.Kind, "moby-record-append", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(patch.Kind, "spring-chest-reward-row-append", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseRecordOffset(string value, out int offset)
    {
        offset = 0;
        if (string.IsNullOrWhiteSpace(value) || !value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return false;
        return int.TryParse(value[2..], System.Globalization.NumberStyles.HexNumber, null, out offset);
    }

    private static byte[] ParseHexPreview(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return [];

        List<byte> bytes = [];
        foreach (string part in hex.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (part == "...")
                break;
            if (byte.TryParse(part, System.Globalization.NumberStyles.HexNumber, null, out byte value))
                bytes.Add(value);
        }
        return bytes.ToArray();
    }

    private static string BuildSummary(
        bool bootedLevel,
        bool objectRecordsMatch,
        bool actorRootsMatch,
        int runtimeCount,
        int expectedRecordCount,
        IReadOnlyList<CrossLevelCandidateRamRecordCheck> checks,
        IReadOnlyList<CrossLevelCandidateRamRootCheck> rootChecks)
    {
        if (!bootedLevel)
            return "RAM dump does not look like the candidate level is booted.";
        if (checks.Count == 0)
            return $"Level table is readable with {runtimeCount} record(s), but this candidate has no source-record bytes to verify.";
        if (objectRecordsMatch && actorRootsMatch)
        {
            string rootText = rootChecks.Count == 0 ? "no actor-root import checks were needed" : $"{rootChecks.Count} actor-root import(s) match";
            return $"Candidate level table is readable with {runtimeCount} record(s); all {checks.Count} checked moby record(s) match expected bytes and {rootText}.";
        }

        int matched = checks.Count(check => check.Matched);
        int rootMatched = rootChecks.Count(check => check.Matched);
        return $"Candidate level table is readable with {runtimeCount}/{expectedRecordCount} expected record(s); {matched}/{checks.Count} checked moby record(s) and {rootMatched}/{rootChecks.Count} actor-root import(s) match expected bytes.";
    }

    private static uint ParseHexUInt32(string value)
    {
        string text = value.Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            text = text[2..];
        return uint.Parse(text, System.Globalization.NumberStyles.HexNumber, null);
    }
}

public sealed record CrossLevelCandidateRamVerification(
    string RamPath,
    bool RamReadable,
    bool BootedLevel,
    bool ObjectRecordsMatch,
    bool ActorRootsMatch,
    string RuntimeMobyPointer,
    int RuntimeMobyCount,
    int ExpectedRecordCount,
    IReadOnlyList<CrossLevelCandidateRamRecordCheck> Checks,
    IReadOnlyList<CrossLevelCandidateRamRootCheck> RootChecks,
    string Summary);

public sealed record CrossLevelCandidateRamRecordCheck(
    int TrueIndex,
    string Label,
    int ExpectedBytes,
    int MatchedBytes,
    bool Matched,
    string Details);

public sealed record CrossLevelCandidateRamRootCheck(
    string Slot,
    string ActorId,
    string TargetRoot,
    bool Matched,
    string RamOffset,
    string Details);
