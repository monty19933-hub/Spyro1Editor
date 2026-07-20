using System.Buffers.Binary;
using System.Text.Json.Nodes;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Workspace;

EditorWorkspace workspace = EditorWorkspace.Find(args.FirstOrDefault());
LevelCatalog catalog = LevelCatalog.Load(workspace.RootPath);
string sourceImagePath = DiscImageLocator.FindImage(workspace);
if (!File.Exists(sourceImagePath))
    throw new FileNotFoundException("Native movement export smoke needs the configured clean Spyro BIN.", sourceImagePath);

string sourceCuePath = DiscImageLocator.FindCueForImage(sourceImagePath);
string tempDirectory = Path.Combine(workspace.RootPath, "_local", $"native-movement-export-smoke-{Guid.NewGuid():N}");
Directory.CreateDirectory(tempDirectory);
try
{
    VerifyNativePathExport(
        sourceImagePath,
        sourceCuePath,
        catalog.FindByKey("townsquare") ?? throw new InvalidOperationException("Missing Town Square catalog row."),
        tempDirectory);
    VerifyDragonRunToExport(
        sourceImagePath,
        sourceCuePath,
        catalog.FindByKey("artisans") ?? throw new InvalidOperationException("Missing Artisans catalog row."),
        tempDirectory);
}
finally
{
    Directory.Delete(tempDirectory, recursive: true);
}

Console.WriteLine("PASS: MobySourcePatchExporter consumed saved egg-thief PathData edits as one exact 12-byte XYZ patch.");
Console.WriteLine("PASS: stale native path identity/preimage was rejected before a patch plan could be produced.");
Console.WriteLine("PASS: synthetic dragon endpoint edits produced only packed +0x28/+0x2C writes and preserved +0x30.");
Console.WriteLine("PASS: stale synthetic dragon preimages were skipped with an explicit rejection reason and no writes.");
Console.WriteLine("PASS: long/no-terrain dragon destinations export with targeted Build Safety Review findings.");
Console.WriteLine("PASS: zero, overflow, invalid, copied/new, and stale dragon controls are targeted Blocked findings with no writes.");

static void VerifyNativePathExport(
    string sourceImagePath,
    string sourceCuePath,
    LevelDefinition level,
    string tempDirectory)
{
    string nativeManifestPath = Path.Combine(tempDirectory, "townsquare-native-edits.json");
    string pathManifestPath = Path.Combine(tempDirectory, NativeMobyPathEditStore.DefaultFileName(level.Key));
    WriteEmptyMobyManifest(nativeManifestPath, level.DisplayName);

    IReadOnlyList<NativeMobyPath> paths = EggThiefPathLocator.Locate(sourceImagePath, level);
    NativeMobyPath path = paths.Single(nativePath => nativePath.OwnerTrueIndex == 88);
    NativePathNode node = path.Nodes[0];
    node.SetRawPosition(
        checked(node.OriginalRawX + 16),
        checked(node.OriginalRawY - 32),
        checked(node.OriginalRawZ + 48));
    Assert(
        NativeMobyPathEditStore.SaveAsync(pathManifestPath, level.DisplayName, paths).GetAwaiter().GetResult() == 1,
        "Native path store did not save exactly one route.");

    MobySourcePatchPlan plan = BuildPlan(
        sourceImagePath,
        sourceCuePath,
        level,
        nativeManifestPath,
        pathManifestPath,
        tempDirectory,
        "townsquare-path");
    Assert(plan.SkippedEdits.Count == 0, "Valid native path edit was skipped.");
    Assert(plan.Patches.Count == 1, $"One native path node unexpectedly produced {plan.Patches.Count} source patches.");
    MobySourcePatch patch = plan.Patches.Single();
    Assert(patch.Kind == "native-moby-path-node-xyz", "Native path patch has the wrong kind.");
    Assert(patch.TrueIndex == path.OwnerTrueIndex, "Native path patch lost owner identity.");
    Assert(patch.ByteLength == 12, "Native path patch is not exactly three 32-bit coordinate words.");
    Assert(ParseOffset(patch.WadRelativeOffset) == node.CoordinateWadOffset, "Native path patch targets the wrong PathData node.");
    byte[] before = ParseHex(patch.BeforeHexPreview);
    byte[] after = ParseHex(patch.AfterHexPreview);
    Assert(before.Length == 12 && after.Length == 12, "Native path previews are not exactly 12 bytes.");
    Assert(BinaryPrimitives.ReadInt32LittleEndian(after.AsSpan(0, 4)) == node.RawX, "Edited path X was not exported.");
    Assert(BinaryPrimitives.ReadInt32LittleEndian(after.AsSpan(4, 4)) == node.RawY, "Edited path Y was not exported.");
    Assert(BinaryPrimitives.ReadInt32LittleEndian(after.AsSpan(8, 4)) == node.RawZ, "Edited path Z was not exported.");

    JsonObject staleRoot = JsonNode.Parse(File.ReadAllText(pathManifestPath))!.AsObject();
    JsonObject staleEdit = staleRoot["edits"]!.AsArray()[0]!.AsObject();
    staleEdit["originalPathSha256"] = new string('0', 64);
    string stalePathManifest = Path.Combine(tempDirectory, "townsquare-native-moby-path-edits-stale.json");
    File.WriteAllText(stalePathManifest, staleRoot.ToJsonString());
    MobySourcePatchPlan stalePlan = BuildPlan(
        sourceImagePath,
        sourceCuePath,
        level,
        nativeManifestPath,
        stalePathManifest,
        tempDirectory,
        "townsquare-path-stale");
    Assert(stalePlan.Patches.Count == 0, "A stale native path fingerprint produced source writes.");
    Assert(
        stalePlan.SkippedEdits.Any(reason => reason.Contains("original path fingerprint no longer matches", StringComparison.OrdinalIgnoreCase)),
        "A stale native path fingerprint did not produce an explicit guarded rejection.");
}

static void VerifyDragonRunToExport(
    string sourceImagePath,
    string sourceCuePath,
    LevelDefinition level,
    string tempDirectory)
{
    string nativeManifestPath = Path.Combine(tempDirectory, "artisans-native-edits.json");
    WriteEmptyMobyManifest(nativeManifestPath, level.DisplayName);

    DragonRescueCameraData scene = DragonRescueCameraLocator.Locate(sourceImagePath, level)
        .Values
        .OrderBy(data => data.DragonTrueIndex)
        .First();
    NativeDragonRunToEdit edit = new(level.Key, scene);
    int editedAngle = DragonRescueRunTo.NormalizeAngle(scene.RunToAngle + 0x123);
    int editedRadius = checked(scene.RunToRadius + 17);
    DragonRunToEndpoint requestedEndpoint = DragonRescueRunTo.DecodeEndpoint(
        scene.DragonRawX,
        scene.DragonRawY,
        editedAngle,
        editedRadius);
    edit.SetRawEndpoint(requestedEndpoint.RawX, requestedEndpoint.RawY);
    Assert(
        DragonRunToEditStore.MergeIntoMobyManifestAsync(nativeManifestPath, [edit]).GetAwaiter().GetResult() == 1,
        "Dragon run-to store did not save exactly one synthetic control.");

    DragonRunToEditLoadResult persisted = DragonRunToEditStore.LoadFromMobyManifest(
        nativeManifestPath,
        level.Key,
        new Dictionary<int, DragonRescueCameraData> { [scene.DragonTrueIndex] = scene });
    Assert(
        persisted.AppliedCount == 1 && persisted.BlockedReasons.Count == 0 &&
        persisted.Edits.Single().RawX == requestedEndpoint.RawX &&
        persisted.Edits.Single().RawY == requestedEndpoint.RawY,
        "Synthetic dragon endpoint did not round-trip through the ordinary native Moby manifest.");

    MobySourcePatchPlan plan = BuildPlan(
        sourceImagePath,
        sourceCuePath,
        level,
        nativeManifestPath,
        "",
        tempDirectory,
        "artisans-dragon-run-to");
    Assert(plan.SkippedEdits.Count == 0, "Valid dragon endpoint edit was skipped.");
    Assert(plan.Patches.Count == 2, $"Exact dragon endpoint edit unexpectedly produced {plan.Patches.Count} patches.");
    HashSet<string> expectedKinds =
    [
        "dragon-rescue-run-to-angle",
        "dragon-rescue-run-to-radius"
    ];
    Assert(plan.Patches.Select(patch => patch.Kind).ToHashSet().SetEquals(expectedKinds), "Dragon endpoint export wrote fields beyond angle/radius.");
    foreach (MobySourcePatch patch in plan.Patches)
    {
        Assert(patch.TrueIndex == scene.DragonTrueIndex, "Dragon endpoint patch lost owner identity.");
        Assert(patch.ByteLength == sizeof(int), "Dragon endpoint field patch is not one 32-bit word.");
        long relative = ParseOffset(patch.WadRelativeOffset) - scene.CameraDataWadOffset;
        Assert(
            relative is DragonRescueRunTo.AngleFieldOffset or DragonRescueRunTo.RadiusFieldOffset,
            $"Dragon endpoint export touched forbidden packed-properties offset +0x{relative:X}.");
        Assert(relative != DragonRescueRunTo.AuxiliaryFieldOffset, "Dragon endpoint export changed +0x30 auxiliary data.");
    }

    JsonObject staleRoot = JsonNode.Parse(File.ReadAllText(nativeManifestPath))!.AsObject();
    JsonObject staleRow = staleRoot["edits"]!.AsArray()[0]!.AsObject();
    staleRow["originalAngle"] = scene.RunToAngle ^ 1;
    string staleManifest = Path.Combine(tempDirectory, "artisans-native-edits-stale.json");
    File.WriteAllText(staleManifest, staleRoot.ToJsonString());

    DragonRunToEditLoadResult staleLoad = DragonRunToEditStore.LoadFromMobyManifest(
        staleManifest,
        level.Key,
        new Dictionary<int, DragonRescueCameraData> { [scene.DragonTrueIndex] = scene });
    Assert(staleLoad.AppliedCount == 0, "Stale dragon run-to row was applied during persistence load.");
    Assert(
        staleLoad.BlockedReasons.Any(reason => reason.Contains("preimage", StringComparison.OrdinalIgnoreCase)),
        "Stale dragon run-to persistence row did not report a preimage rejection.");

    MobySourcePatchPlan stalePlan = BuildPlan(
        sourceImagePath,
        sourceCuePath,
        level,
        staleManifest,
        "",
        tempDirectory,
        "artisans-dragon-run-to-stale");
    Assert(stalePlan.Patches.Count == 0, "Stale dragon run-to row produced source writes.");
    Assert(
        stalePlan.SkippedEdits.Any(reason => reason.Contains("preimage", StringComparison.OrdinalIgnoreCase)),
        "Exporter did not surface the stale dragon run-to preimage rejection.");

    JsonObject staleAddressRoot = JsonNode.Parse(File.ReadAllText(nativeManifestPath))!.AsObject();
    JsonObject staleAddressRow = staleAddressRoot["edits"]!.AsArray()[0]!.AsObject();
    staleAddressRow["cameraDataWadOffset"] = $"0x{scene.CameraDataWadOffset + 4:X}";
    string staleAddressManifest = Path.Combine(tempDirectory, "artisans-native-edits-stale-address.json");
    File.WriteAllText(staleAddressManifest, staleAddressRoot.ToJsonString());
    MobySourcePatchPlan staleAddressPlan = BuildPlan(
        sourceImagePath,
        sourceCuePath,
        level,
        staleAddressManifest,
        "",
        tempDirectory,
        "artisans-dragon-run-to-stale-address");
    Assert(staleAddressPlan.Patches.Count == 0, "Stale dragon run-to scene address produced source writes.");
    Assert(
        staleAddressPlan.SkippedEdits.Any(reason => reason.Contains("scene address", StringComparison.OrdinalIgnoreCase)),
        "Exporter silently reinterpreted a stale dragon run-to scene address.");

    VerifyDragonBuildSafetyGuards(
        sourceImagePath,
        sourceCuePath,
        level,
        nativeManifestPath,
        scene,
        tempDirectory);
}

static void VerifyDragonBuildSafetyGuards(
    string sourceImagePath,
    string sourceCuePath,
    LevelDefinition level,
    string validManifestPath,
    DragonRescueCameraData scene,
    string tempDirectory)
{
    MobySourcePatchPlan longPlan = BuildMutatedDragonPlan(
        validManifestPath,
        Path.Combine(tempDirectory, "artisans-native-edits-long.json"),
        root =>
        {
            JsonObject edited = FirstDragonRow(root)["rawEdited"]!.AsObject();
            edited["x"] = checked(scene.DragonRawX + 100_000);
            edited["y"] = scene.DragonRawY;
        },
        sourceImagePath,
        sourceCuePath,
        level,
        tempDirectory,
        "artisans-dragon-run-to-long");
    Assert(longPlan.Patches.Count > 0, "A long but safe dragon destination was incorrectly blocked.");
    Assert(longPlan.SkippedEdits.Count == 0, "A long but safe dragon destination was skipped.");
    MobySourceEditOutcome longOutcome = longPlan.EditOutcomes!
        .Single(outcome => outcome.EditorTrueIndex == scene.DragonTrueIndex);
    Assert(
        (longOutcome.SafetyFindings ?? []).Any(finding =>
            finding.Code == "dragon-run-to-outside-retail-radius" &&
            finding.Status == MobyBuildSafetyStatus.Review),
        "Long dragon destination did not retain its structured retail-radius Review finding.");
    Assert(
        (longOutcome.SafetyFindings ?? []).Any(finding =>
            finding.Code == "dragon-run-to-no-terrain-hit" &&
            finding.Status == MobyBuildSafetyStatus.Review),
        "Dragon destination without a source-derived terrain hit did not retain its structured Review finding.");
    MobyBuildSafetyLevelReport longSafety = MobyBuildSafetyInspector.InspectLevel(sourceImagePath, level, longPlan);
    Assert(longSafety.Status == MobyBuildSafetyStatus.Review, "Long dragon destination did not promote the level to Review.");
    AssertTargetedIssue(longSafety, scene.DragonTrueIndex, "dragon-run-to-outside-retail-radius", MobyBuildSafetyStatus.Review);
    AssertTargetedIssue(longSafety, scene.DragonTrueIndex, "dragon-run-to-no-terrain-hit", MobyBuildSafetyStatus.Review);

    MobySourcePatchPlan zeroPlan = BuildMutatedDragonPlan(
        validManifestPath,
        Path.Combine(tempDirectory, "artisans-native-edits-zero.json"),
        root =>
        {
            JsonObject edited = FirstDragonRow(root)["rawEdited"]!.AsObject();
            edited["x"] = scene.DragonRawX;
            edited["y"] = scene.DragonRawY;
        },
        sourceImagePath,
        sourceCuePath,
        level,
        tempDirectory,
        "artisans-dragon-run-to-zero");
    AssertBlockedDragonPlan(sourceImagePath, level, zeroPlan, scene.DragonTrueIndex, "dragon-run-to-zero-radius");

    MobySourcePatchPlan overflowPlan = BuildMutatedDragonPlan(
        validManifestPath,
        Path.Combine(tempDirectory, "artisans-native-edits-overflow.json"),
        root =>
        {
            JsonObject edited = FirstDragonRow(root)["rawEdited"]!.AsObject();
            edited["x"] = checked(scene.DragonRawX + DragonRescueRunTo.MaximumSafeRadiusRaw + 1);
            edited["y"] = scene.DragonRawY;
        },
        sourceImagePath,
        sourceCuePath,
        level,
        tempDirectory,
        "artisans-dragon-run-to-overflow");
    AssertBlockedDragonPlan(sourceImagePath, level, overflowPlan, scene.DragonTrueIndex, "dragon-run-to-radius-overflow");

    MobySourcePatchPlan invalidPlan = BuildMutatedDragonPlan(
        validManifestPath,
        Path.Combine(tempDirectory, "artisans-native-edits-invalid.json"),
        root => FirstDragonRow(root)["rawEdited"]!.AsObject()["x"] = "not-an-integer",
        sourceImagePath,
        sourceCuePath,
        level,
        tempDirectory,
        "artisans-dragon-run-to-invalid");
    AssertBlockedDragonPlan(sourceImagePath, level, invalidPlan, scene.DragonTrueIndex, "invalid dragon run-to endpoint");

    MobySourcePatchPlan copiedPlan = BuildMutatedDragonPlan(
        validManifestPath,
        Path.Combine(tempDirectory, "artisans-native-edits-copied.json"),
        root =>
        {
            JsonObject row = FirstDragonRow(root);
            row["editKind"] = "add";
            row["added"] = true;
        },
        sourceImagePath,
        sourceCuePath,
        level,
        tempDirectory,
        "artisans-dragon-run-to-copied");
    AssertBlockedDragonPlan(sourceImagePath, level, copiedPlan, scene.DragonTrueIndex, "invalid copied/new dragon run-to control");

    MobySourcePatchPlan stalePlan = BuildMutatedDragonPlan(
        validManifestPath,
        Path.Combine(tempDirectory, "artisans-native-edits-stale-safety.json"),
        root => FirstDragonRow(root)["originalAngle"] = scene.RunToAngle ^ 1,
        sourceImagePath,
        sourceCuePath,
        level,
        tempDirectory,
        "artisans-dragon-run-to-stale-safety");
    AssertBlockedDragonPlan(sourceImagePath, level, stalePlan, scene.DragonTrueIndex, "preimage");
}

static MobySourcePatchPlan BuildMutatedDragonPlan(
    string validManifestPath,
    string mutatedManifestPath,
    Action<JsonObject> mutate,
    string sourceImagePath,
    string sourceCuePath,
    LevelDefinition level,
    string tempDirectory,
    string outputName)
{
    JsonObject root = JsonNode.Parse(File.ReadAllText(validManifestPath))!.AsObject();
    mutate(root);
    File.WriteAllText(mutatedManifestPath, root.ToJsonString());
    return BuildPlan(
        sourceImagePath,
        sourceCuePath,
        level,
        mutatedManifestPath,
        "",
        tempDirectory,
        outputName);
}

static JsonObject FirstDragonRow(JsonObject root) =>
    root["edits"]!.AsArray()[0]!.AsObject();

static void AssertBlockedDragonPlan(
    string sourceImagePath,
    LevelDefinition level,
    MobySourcePatchPlan plan,
    int ownerTrueIndex,
    string expectedReason)
{
    Assert(plan.Patches.Count == 0, $"Blocked dragon control '{expectedReason}' produced source writes.");
    Assert(
        plan.SkippedEdits.Any(reason => reason.Contains(expectedReason, StringComparison.OrdinalIgnoreCase)),
        $"Blocked dragon control did not report '{expectedReason}'.");
    MobyBuildSafetyLevelReport safety = MobyBuildSafetyInspector.InspectLevel(sourceImagePath, level, plan);
    Assert(safety.Status == MobyBuildSafetyStatus.Blocked, $"'{expectedReason}' did not promote the level to Blocked.");
    MobyBuildSafetyIssue issue = safety.Issues.Single(issue =>
        issue.Status == MobyBuildSafetyStatus.Blocked &&
        issue.EditorTrueIndex == ownerTrueIndex &&
        issue.Message.Contains(expectedReason, StringComparison.OrdinalIgnoreCase));
    Assert(issue.CanNavigate, $"'{expectedReason}' Build Safety issue cannot navigate to the dragon owner.");
}

static void AssertTargetedIssue(
    MobyBuildSafetyLevelReport safety,
    int ownerTrueIndex,
    string code,
    MobyBuildSafetyStatus status)
{
    MobyBuildSafetyIssue issue = safety.Issues.Single(issue =>
        issue.Code == code &&
        issue.Status == status &&
        issue.EditorTrueIndex == ownerTrueIndex);
    Assert(issue.CanNavigate, $"Build Safety issue '{code}' cannot navigate to dragon T{ownerTrueIndex}.");
}

static MobySourcePatchPlan BuildPlan(
    string sourceImagePath,
    string sourceCuePath,
    LevelDefinition level,
    string nativeManifestPath,
    string pathManifestPath,
    string tempDirectory,
    string outputName) =>
    MobySourcePatchExporter.BuildPlan(
        sourceImagePath,
        sourceCuePath,
        Path.Combine(tempDirectory, $"{outputName}.bin"),
        Path.Combine(tempDirectory, $"{outputName}.cue"),
        level,
        nativeManifestPath,
        nativeMobyPathEditsPath: pathManifestPath);

static void WriteEmptyMobyManifest(string path, string levelName)
{
    JsonObject root = new()
    {
        ["generatedAt"] = DateTimeOffset.UtcNow.ToString("O"),
        ["editor"] = "Spyro.Editor.NativeMovementExportSmoke",
        ["levelName"] = levelName,
        ["editCount"] = 0,
        ["edits"] = new JsonArray()
    };
    File.WriteAllText(path, root.ToJsonString());
}

static long ParseOffset(string text) =>
    Convert.ToInt64(text[2..], 16);

static byte[] ParseHex(string text) =>
    text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(part => Convert.ToByte(part, 16))
        .ToArray();

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
