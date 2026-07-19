using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Spyro.Editor.Core.Cache;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Workspace;

string workspaceRoot = args.Length > 0 ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();
string sourceImagePath = DiscImageLocator.FindImage(new EditorWorkspace(workspaceRoot));
Assert(File.Exists(sourceImagePath), $"Missing configured retail source BIN: {sourceImagePath}");
string outputRoot = Path.Combine(workspaceRoot, "_local", "research", "native-terrain-texture-runtime-control");
Directory.CreateDirectory(outputRoot);

LevelCatalog catalog = LevelCatalog.Load(workspaceRoot);
List<AuditRow> rows = [];
int animationAffectedHpFaces = 0;
int scrollingAffectedHpFaces = 0;
int faceReferencedTextureCount = 0;
int faceReferencedAnimationSourceIds = 0;
int animationMutationCount = 0;
int scrollingMutationCount = 0;
int initializedCacheTextureCount = 0;
int initializedCacheFrameCount = 0;
int initializedCacheLqTextureCount = 0;
int initializedCacheNativeUnreferencedTextureCount = 0;
List<(string LevelKey, int TextureId)> controlledWithoutHpFace = [];
foreach (LevelDefinition level in LevelRealmCatalog.OrderLevels(catalog.Levels.Where(level => level.SourceWadEntry >= 0)))
{
    NativeTerrainTextureRuntimeControlAudit audit = NativeTerrainTextureRuntimeControlScanner.Inspect(sourceImagePath, level);
    Assert(audit.Complete, $"{level.DisplayName}: runtime-control inspection was incomplete: {string.Join("; ", audit.SafetyBlockers)}");
    Assert(audit.TextureCount > 0, $"{level.DisplayName}: decoded no terrain texture records.");
    Assert(audit.ControlledTextureIds.All(textureId => textureId >= 0 && textureId < audit.TextureCount),
        $"{level.DisplayName}: a runtime control targets an invalid texture id.");
    Assert(audit.ControlledTextureIds.All(textureId => !audit.IsRuntimePersistentTarget(textureId)),
        $"{level.DisplayName}: a controlled texture was incorrectly marked runtime-persistent.");

    int stableCount = Enumerable.Range(0, audit.TextureCount).Count(audit.IsRuntimePersistentTarget);
    Assert(stableCount + audit.ControlledTextureIds.Count == audit.TextureCount,
        $"{level.DisplayName}: runtime-persistence partition does not cover every texture record exactly once.");

    Assert(audit.AnimationControls.All(control =>
            control.Frames.Count > 0 &&
            control.InitialCurrentFrame >= 0 && control.InitialCurrentFrame < control.Frames.Count &&
            control.InitialSourceTextureId == control.Frames[control.InitialCurrentFrame].SourceTextureId &&
            control.Frames.All(frame => frame.ForwardNextFrame >= 0 && frame.ForwardNextFrame < control.Frames.Count &&
                frame.ReverseNextFrame >= 0 && frame.ReverseNextFrame < control.Frames.Count &&
                frame.SourceTextureId >= 0 && frame.SourceTextureId < audit.TextureCount)),
        $"{level.DisplayName}: an animation program failed exact frame/source validation.");
    Assert(audit.ScrollingControls.All(control =>
            control.Frames.Count > 0 && control.InitialPhase is >= 0 and <= 0x7F &&
            control.InitialCurrentFrame >= 0 && control.InitialCurrentFrame < control.Frames.Count &&
            control.Frames.All(frame => frame.ForwardNextFrame >= 0 && frame.ForwardNextFrame < control.Frames.Count &&
                frame.ReverseNextFrame >= 0 && frame.ReverseNextFrame < control.Frames.Count)),
        $"{level.DisplayName}: a scrolling program failed exact frame/phase validation.");

    NativeTerrainTextureInitialStateResult initialState =
        TerrainPatchExporter.InspectTerrainTextureInitialState(sourceImagePath, level, audit);
    Assert(initialState.Complete,
        $"{level.DisplayName}: native load-state initialization failed: {string.Join("; ", initialState.SafetyBlockers)}");
    Assert(initialState.TextureCount == audit.TextureCount,
        $"{level.DisplayName}: initialized texture count does not match the scene control audit.");
    Assert(initialState.Mutations.Count == audit.AnimationControls.Count + audit.ScrollingControls.Count,
        $"{level.DisplayName}: native initialization did not apply every control exactly once.");
    Assert(initialState.Mutations.Take(audit.AnimationControls.Count).All(mutation =>
            mutation.Kind == NativeTerrainTextureRuntimeControlKind.FullRecordAnimation),
        $"{level.DisplayName}: animation mutations were not applied before scrolling mutations.");
    Assert(initialState.Mutations.Skip(audit.AnimationControls.Count).All(mutation =>
            mutation.Kind == NativeTerrainTextureRuntimeControlKind.ScrollingDescriptor),
        $"{level.DisplayName}: scrolling mutations were not applied after animation mutations.");
    Assert(initialState.Mutations.Where(mutation => mutation.Kind == NativeTerrainTextureRuntimeControlKind.FullRecordAnimation)
            .All(mutation => mutation.LqChangedByteCount == 0 && mutation.HqChangedByteCount == 0),
        $"{level.DisplayName}: a retail animation destination no longer matches its load-selected source record on disc.");
    Assert(initialState.Mutations.Where(mutation => mutation.Kind == NativeTerrainTextureRuntimeControlKind.ScrollingDescriptor)
            .All(mutation => mutation.LqChangedByteCount + mutation.HqChangedByteCount > 0),
        $"{level.DisplayName}: a retail scrolling destination unexpectedly already matches its initialized coordinates.");
    foreach (NativeTerrainScrollingTextureControl control in audit.ScrollingControls)
        AssertInitializedScrollingCoordinates(level, initialState, control);
    animationMutationCount += initialState.Mutations.Count(mutation => mutation.Kind == NativeTerrainTextureRuntimeControlKind.FullRecordAnimation);
    scrollingMutationCount += initialState.Mutations.Count(mutation => mutation.Kind == NativeTerrainTextureRuntimeControlKind.ScrollingDescriptor);

    string overlayPath = Path.Combine(workspaceRoot, "editor-cache", $"{level.Key}-runtime-scene-editor-overlay.json");
    Assert(File.Exists(overlayPath), $"{level.DisplayName}: missing source-derived overlay fixture {overlayPath}.");
    GeometryCandidate geometry = GeometryOverlayLoader.LoadFirstCandidate(overlayPath);
    HashSet<int> animatedIds = audit.AnimatedTextureIds.ToHashSet();
    HashSet<int> scrollingIds = audit.ScrollingTextureIds.ToHashSet();
    HashSet<int> usedTextureIds = geometry.Polygons.Select(polygon => polygon.TextureId).Where(id => id >= 0).ToHashSet();
    faceReferencedTextureCount += usedTextureIds.Count(textureId => textureId < audit.TextureCount);
    controlledWithoutHpFace.AddRange(audit.ControlledTextureIds
        .Where(textureId => !usedTextureIds.Contains(textureId))
        .Select(textureId => (level.Key, textureId)));
    animationAffectedHpFaces += geometry.Polygons.Count(polygon => animatedIds.Contains(polygon.TextureId));
    scrollingAffectedHpFaces += geometry.Polygons.Count(polygon => scrollingIds.Contains(polygon.TextureId));
    faceReferencedAnimationSourceIds += audit.AnimationSourceTextureIds.Count(usedTextureIds.Contains);
    HashSet<int> nativeFaceReferencedTextureIds = usedTextureIds
        .Where(textureId => textureId < audit.TextureCount)
        .ToHashSet();
    (int cachedTextures, int cachedFrames, int cachedLqTextures, int cachedNativeUnreferencedTextures) =
        AssertInitialStateManifest(workspaceRoot, level, audit, nativeFaceReferencedTextureIds);
    initializedCacheTextureCount += cachedTextures;
    initializedCacheFrameCount += cachedFrames;
    initializedCacheLqTextureCount += cachedLqTextures;
    initializedCacheNativeUnreferencedTextureCount += cachedNativeUnreferencedTextures;

    rows.Add(new AuditRow(
        LevelKey: level.Key,
        LevelName: level.DisplayName,
        Realm: LevelRealmCatalog.TryGetPosition(level, out LevelRealmPosition position) ? position.Realm.DisplayName : "Unknown",
        WadEntry: level.SourceWadEntry,
        TextureCount: audit.TextureCount,
        RuntimePersistentTargetCount: stableCount,
        AnimatedTextureIds: audit.AnimatedTextureIds,
        ScrollingTextureIds: audit.ScrollingTextureIds,
        AnimationSourceTextureIds: audit.AnimationSourceTextureIds,
        SceneByteLength: audit.SceneByteLength,
        SceneSha256: audit.SceneSha256));
}

Assert(rows.Count == 35, $"Expected all 35 retail levels, got {rows.Count}.");
AuditRow artisans = rows.Single(row => row.LevelKey == "artisans");
Assert(artisans.AnimatedTextureIds.Count == 0 && artisans.ScrollingTextureIds.SequenceEqual([23]),
    "Artisans should identify only scrolling texture 23.");
AuditRow magicCrafters = rows.Single(row => row.LevelKey == "magiccrafters");
Assert(magicCrafters.AnimatedTextureIds.SequenceEqual([61, 62, 63, 65]) && magicCrafters.ScrollingTextureIds.SequenceEqual([51]),
    "Magic Crafters runtime-control ids changed unexpectedly.");
AuditRow gnastysWorld = rows.Single(row => row.LevelKey == "gnastysworld");
Assert(gnastysWorld.AnimatedTextureIds.Count == 0 && gnastysWorld.ScrollingTextureIds.Count == 0,
    "Gnasty's World should not have texture-animation or scrolling controls.");
Assert(rows.Sum(row => row.AnimatedTextureIds.Count) == 44, "Expected 44 animation-controlled texture records across retail levels.");
Assert(rows.Sum(row => row.ScrollingTextureIds.Count) == 26, "Expected 26 scrolling-controlled texture records across retail levels.");
Assert(rows.Sum(row => row.AnimatedTextureIds.Count + row.ScrollingTextureIds.Count) == 70,
    "Expected 70 runtime-controlled texture destinations across retail levels.");
Assert(animationMutationCount == 44 && scrollingMutationCount == 26,
    $"Expected 44 animation and 26 scrolling initialization mutations, got {animationMutationCount}/{scrollingMutationCount}.");
Assert(rows.Sum(row => row.AnimationSourceTextureIds.Count) == 22,
    "Expected 22 per-level animation source texture records required by diagnostics/default-timeline support.");
Assert(faceReferencedAnimationSourceIds == 0,
    $"Expected animation source records to be absent from the HP face request set, but found {faceReferencedAnimationSourceIds} references.");
Assert(faceReferencedTextureCount == 2025,
    $"Expected 2,025 distinct per-level HP face-referenced texture records, got {faceReferencedTextureCount}.");
Assert(animationAffectedHpFaces == 217,
    $"Expected 217 HP faces affected by full-record animation, got {animationAffectedHpFaces}.");
Assert(scrollingAffectedHpFaces == 1794,
    $"Expected 1,794 HP faces affected by scrolling controls, got {scrollingAffectedHpFaces}.");
Assert(initializedCacheTextureCount == 2070 && initializedCacheFrameCount == 4140 && initializedCacheLqTextureCount == 2070,
    $"Expected format-9 cache coverage of 2,070 exact LQ textures / 2,070 HQ textures / 4,140 dual-tier HQ frames, got {initializedCacheLqTextureCount}/{initializedCacheTextureCount}/{initializedCacheFrameCount}.");
Assert(initializedCacheNativeUnreferencedTextureCount == 19,
    $"Expected 19 native records outside face/control/source roles, got {initializedCacheNativeUnreferencedTextureCount}.");
AssertPublicLoaderFailsClosed(workspaceRoot, "artisans");
Assert(controlledWithoutHpFace.Count == 4 && controlledWithoutHpFace.ToHashSet().SetEquals(new[]
       {
           ("beastmakers", 52),
           ("gnastysloot", 12),
           ("stonehill", 0),
           ("toasty", 18)
       }),
    $"Expected the four runtime-controlled records absent from HP faces to remain force-exported; got {string.Join(", ", controlledWithoutHpFace.Select(item => $"{item.LevelKey}:{item.TextureId}"))}.");

NativeTerrainTextureRuntimeControlAudit artisansAudit =
    NativeTerrainTextureRuntimeControlScanner.Inspect(sourceImagePath, catalog.Levels.Single(level => level.Key == "artisans"));
NativeTerrainScrollingTextureControl artisansScroll = artisansAudit.ScrollingControls.Single();
Assert(artisansScroll.SceneRelativeStructureOffset == 0x9C && artisansScroll.ControlId == 1 &&
       artisansScroll.DestinationTextureId == 23 && artisansScroll.InitialPhase == 0 &&
       artisansScroll.Frames.Count == 64 && artisansScroll.Frames[0].PhaseDelta == -2,
    "Artisans scrolling-control fixture changed unexpectedly.");
Assert(artisansScroll.RawBytesHex.StartsWith("0100000117000000040100FE040200FE", StringComparison.Ordinal) &&
       artisansScroll.RawBytesHex.EndsWith("043F00FE040000FE", StringComparison.Ordinal),
    "Artisans scrolling-control raw fixture bytes changed unexpectedly.");

NativeTerrainTextureRuntimeControlAudit magicAudit =
    NativeTerrainTextureRuntimeControlScanner.Inspect(sourceImagePath, catalog.Levels.Single(level => level.Key == "magiccrafters"));
Assert(magicAudit.AnimationControls.Select(control => control.SceneRelativeStructureOffset).SequenceEqual([0xA0, 0xB0, 0xC4, 0xD8]),
    "Magic Crafters animation structure offsets changed unexpectedly.");
NativeTerrainTextureAnimationControl magicFirst = magicAudit.AnimationControls[0];
Assert(magicFirst.RawBytesHex == "0900001E3D0000007801004028000042" &&
       magicFirst.ControlId == 9 && magicFirst.DestinationTextureId == 61 &&
       magicFirst.InitialSourceTextureId == 64,
    "Magic Crafters first animation-control fixture bytes changed unexpectedly.");

string jsonPath = Path.Combine(outputRoot, "terrain-texture-runtime-control.json");
await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(new
{
    generatedUtc = DateTime.UtcNow,
    sourceImagePath,
    levelCount = rows.Count,
    animationControlledTextureCount = rows.Sum(row => row.AnimatedTextureIds.Count),
    scrollingControlledTextureCount = rows.Sum(row => row.ScrollingTextureIds.Count),
    animationSourceTextureCount = rows.Sum(row => row.AnimationSourceTextureIds.Count),
    faceReferencedTextureCount,
    animationAffectedHpFaces,
    scrollingAffectedHpFaces,
    controlledWithoutHpFace = controlledWithoutHpFace.Select(item => new { item.LevelKey, item.TextureId }).ToArray(),
    initializedCacheTextureCount,
    initializedCacheFrameCount,
    initializedCacheLqTextureCount,
    initializedCacheNativeUnreferencedTextureCount,
    rows
}, new JsonSerializerOptions { WriteIndented = true }));

StringBuilder markdown = new();
markdown.AppendLine("# Native terrain texture runtime-control audit");
markdown.AppendLine();
markdown.AppendLine("This source-bound scan identifies local texture records which the retail game rewrites after level load. Cross-level descriptor relocation must block these records until their animation/scroll control is composed with the transplant.");
markdown.AppendLine();
markdown.AppendLine($"- Levels inspected: {rows.Count}/35");
markdown.AppendLine($"- Full-record animation destinations: {rows.Sum(row => row.AnimatedTextureIds.Count)}");
markdown.AppendLine($"- Scrolling-descriptor destinations: {rows.Sum(row => row.ScrollingTextureIds.Count)}");
markdown.AppendLine($"- Otherwise-unreferenced per-level animation source records: {rows.Sum(row => row.AnimationSourceTextureIds.Count)}");
markdown.AppendLine($"- Distinct per-level HP face-referenced records: {faceReferencedTextureCount}");
markdown.AppendLine($"- HP faces receiving an initialized animation record: {animationAffectedHpFaces}");
markdown.AppendLine($"- HP faces receiving initialized scrolling coordinates: {scrollingAffectedHpFaces}");
markdown.AppendLine($"- Controlled destinations absent from HP faces but force-exported: {string.Join(", ", controlledWithoutHpFace.Select(item => $"{item.LevelKey} T{item.TextureId}"))}");
markdown.AppendLine($"- Format-9 initialized cache coverage: {initializedCacheLqTextureCount} exact 32x32 indexed LQ textures / {initializedCacheTextureCount} HQ textures / {initializedCacheFrameCount} dual-tier HQ frames (all 2,070 native records)");
markdown.AppendLine($"- Role partition: 2,025 face-referenced; 70 controlled destinations (66 face-referenced + 4 without faces); 22 otherwise-unreferenced animation sources; {initializedCacheNativeUnreferencedTextureCount} native-unreferenced static records");
markdown.AppendLine();
markdown.AppendLine("| Realm | Level | Records | Persistent targets | Full-record animation ids | Scrolling ids |");
markdown.AppendLine("|---|---|---:|---:|---|---|");
foreach (AuditRow row in rows)
{
    markdown.AppendLine($"| {Escape(row.Realm)} | {Escape(row.LevelName)} | {row.TextureCount} | {row.RuntimePersistentTargetCount} | {FormatIds(row.AnimatedTextureIds)} | {FormatIds(row.ScrollingTextureIds)} |");
}

string markdownPath = Path.Combine(outputRoot, "terrain-texture-runtime-control.md");
await File.WriteAllTextAsync(markdownPath, markdown.ToString());
Console.WriteLine($"Native terrain texture runtime-control smoke passed: 35 levels, 44 animations / 26 scrolling controls, 217 / 1,794 affected HP faces, 2,070 exact LQ textures / 2,070 HQ textures / 4,140 HQ frames, 19 native-unreferenced records.");
Console.WriteLine(markdownPath);
Console.WriteLine(jsonPath);

static string FormatIds(IReadOnlyList<int> ids) => ids.Count == 0 ? "none" : string.Join(", ", ids);
static string Escape(string value) => value.Replace("|", "\\|", StringComparison.Ordinal);
static void AssertPublicLoaderFailsClosed(string workspaceRoot, string levelKey)
{
    string temporaryRoot = Path.Combine(Path.GetTempPath(), $"spyro-lq-public-loader-{Guid.NewGuid():N}");
    string sourceDirectory = Path.Combine(workspaceRoot, "editor-cache", "terrain-textures", levelKey);
    string temporaryDirectory = Path.Combine(temporaryRoot, "editor-cache", "terrain-textures", levelKey);
    try
    {
        Directory.CreateDirectory(temporaryDirectory);
        string manifestPath = Path.Combine(temporaryDirectory, "manifest.json");
        File.Copy(Path.Combine(sourceDirectory, "manifest.json"), manifestPath);
        string payloadPath = Path.Combine(temporaryDirectory, NativeTerrainLqTextureCacheCodec.PayloadFileName);
        File.Copy(Path.Combine(sourceDirectory, NativeTerrainLqTextureCacheCodec.PayloadFileName), payloadPath);
        string materialPayloadPath = Path.Combine(temporaryDirectory, NativeTerrainHqMaterialCacheCodec.PayloadFileName);
        File.Copy(Path.Combine(sourceDirectory, NativeTerrainHqMaterialCacheCodec.PayloadFileName), materialPayloadPath);
        using (JsonDocument sourceManifest = JsonDocument.Parse(File.ReadAllText(manifestPath)))
        {
            foreach (JsonElement frame in sourceManifest.RootElement.GetProperty("textures").EnumerateArray())
            {
                string file = frame.GetProperty("file").GetString()
                    ?? throw new InvalidOperationException("HQ preview fixture contains an empty frame filename.");
                File.Copy(Path.Combine(sourceDirectory, file), Path.Combine(temporaryDirectory, file));
            }
        }
        Assert(PortableEditorCacheBuilder.TryLoadNativeTerrainLqTextureCache(temporaryRoot, levelKey, out NativeTerrainLqTextureSet? loaded) &&
               loaded != null && loaded.TextureCount > 0,
            "Public LQ cache loader rejected an intact copied cache fixture.");
        Assert(PortableEditorCacheBuilder.TryLoadNativeTerrainHqMaterialCache(temporaryRoot, levelKey, out NativeTerrainHqMaterialSet? loadedMaterials) &&
               loadedMaterials != null && loadedMaterials.TextureCount > 0,
            "Public HQ material cache loader rejected an intact copied cache fixture.");
        Assert(PortableEditorCacheBuilder.IsCurrentTerrainTexturePreviewCache(temporaryRoot, levelKey),
            "The complete HQ/LQ cache validator rejected an intact copied cache fixture.");

        string validManifest = File.ReadAllText(manifestPath);
        AssertManifestMutationRejected("wrong state semantics", root => root["stateSemantics"] = "rawDiscState");
        AssertManifestMutationRejected("incomplete runtime initialization", root => root["runtimeControlInitialStateComplete"] = false);
        AssertManifestMutationRejected("missing control-scene hash", root => root.Remove("runtimeControlSceneSha256"));
        AssertManifestMutationRejected("wrong runtime program count", root =>
            root["runtimeControlProgramCount"] = (root["runtimeControlProgramCount"]?.GetValue<int>() ?? 0) + 1);
        AssertManifestMutationRejected("mutation/control mismatch", root =>
        {
            JsonObject mutation = root["initializationMutations"]?.AsArray().FirstOrDefault()?.AsObject()
                ?? throw new InvalidOperationException("Artisans negative fixture has no initialization mutation.");
            mutation["ControlId"] = (mutation["ControlId"]?.GetValue<int>() ?? 0) + 1;
        });
        AssertManifestMutationRejected("missing controlled LQ initialization proof", root =>
        {
            JsonObject controlled = root["lqTextures"]?.AsArray()
                .Select(node => node?.AsObject())
                .FirstOrDefault(node => node?["runtimeControlled"]?.GetValue<bool>() == true)
                ?? throw new InvalidOperationException("Artisans negative fixture has no controlled LQ row.");
            controlled.Remove("runtimeInitializationApplied");
        });
        AssertManifestMutationRejected("wrong controlled LQ role", root =>
        {
            JsonObject controlled = root["lqTextures"]?.AsArray()
                .Select(node => node?.AsObject())
                .FirstOrDefault(node => node?["runtimeControlled"]?.GetValue<bool>() == true)
                ?? throw new InvalidOperationException("Artisans negative fixture has no controlled LQ row.");
            controlled["runtimeRole"] = "faceReferencedStatic";
        });

        AssertHqManifestMutationRejected("a missing HQ frame SHA-256", root =>
        {
            JsonObject frame = root["textures"]?.AsArray().FirstOrDefault()?.AsObject()
                ?? throw new InvalidOperationException("Artisans negative fixture has no HQ frame row.");
            frame.Remove("fileSha256");
        });
        AssertHqManifestMutationRejected("an HQ frame SHA-256 mismatch", root =>
        {
            JsonObject frame = root["textures"]?.AsArray().FirstOrDefault()?.AsObject()
                ?? throw new InvalidOperationException("Artisans negative fixture has no HQ frame row.");
            frame["fileSha256"] = new string('0', 64);
        });
        AssertHqManifestMutationRejected("an HQ frame dimension mismatch", root =>
        {
            JsonObject frame = root["textures"]?.AsArray().FirstOrDefault()?.AsObject()
                ?? throw new InvalidOperationException("Artisans negative fixture has no HQ frame row.");
            int width = frame["width"]?.GetValue<int>() ?? 0;
            int height = frame["height"]?.GetValue<int>() ?? 0;
            frame["width"] = width + 1;
            frame["pixelCount"] = (width + 1) * height;
        });
        AssertHqManifestMutationRejected("duplicate HQ keys with one LQ texture missing from HQ", root =>
        {
            JsonArray frames = root["textures"]?.AsArray()
                ?? throw new InvalidOperationException("Artisans negative fixture has no HQ frame rows.");
            int firstClose = frames.Select((node, index) => (node, index))
                .First(pair => pair.node?["descriptorTier"]?.GetValue<string>() == "hqDataClose").index;
            int secondClose = frames.Select((node, index) => (node, index))
                .SkipWhile(pair => pair.index <= firstClose)
                .First(pair => pair.node?["descriptorTier"]?.GetValue<string>() == "hqDataClose").index;
            frames[secondClose] = frames[firstClose]?.DeepClone();
        });

        JsonObject intactRoot = JsonNode.Parse(validManifest)?.AsObject()
            ?? throw new InvalidOperationException("Could not parse the intact HQ preview fixture.");
        string firstFrameFile = intactRoot["textures"]?.AsArray().FirstOrDefault()?["file"]?.GetValue<string>()
            ?? throw new InvalidOperationException("Artisans HQ preview fixture has no frame file.");
        string firstFramePath = Path.Combine(temporaryDirectory, firstFrameFile);
        byte[] validFrameBytes = File.ReadAllBytes(firstFramePath);
        byte[] corruptedFrameBytes = validFrameBytes.ToArray();
        corruptedFrameBytes[^1] ^= 0x01;
        File.WriteAllBytes(firstFramePath, corruptedFrameBytes);
        Assert(!PortableEditorCacheBuilder.IsCurrentTerrainTexturePreviewCache(temporaryRoot, levelKey),
            "Complete HQ/LQ cache validation accepted an HQ PNG whose bytes no longer match its manifest SHA-256.");
        Assert(PortableEditorCacheBuilder.TryLoadNativeTerrainLqTextureCache(temporaryRoot, levelKey, out NativeTerrainLqTextureSet? lqAfterHqCorruption) &&
               lqAfterHqCorruption != null,
            "An HQ-only PNG corruption incorrectly invalidated the independently proved LQ sidecar.");
        File.WriteAllBytes(firstFramePath, validFrameBytes);
        Assert(PortableEditorCacheBuilder.IsCurrentTerrainTexturePreviewCache(temporaryRoot, levelKey),
            "The complete HQ/LQ cache validator did not recover after restoring an intact HQ PNG.");

        byte[] corrupted = File.ReadAllBytes(payloadPath);
        corrupted[^1] ^= 0x01;
        File.WriteAllBytes(payloadPath, corrupted);
        Assert(!PortableEditorCacheBuilder.TryLoadNativeTerrainLqTextureCache(temporaryRoot, levelKey, out NativeTerrainLqTextureSet? rejected) &&
               rejected == null,
            "Public LQ cache loader accepted a sidecar whose payload no longer matches its manifest SHA-256.");

        void AssertManifestMutationRejected(string label, Action<JsonObject> mutate)
        {
            JsonObject root = JsonNode.Parse(validManifest)?.AsObject()
                ?? throw new InvalidOperationException("Could not parse the valid public-loader manifest fixture.");
            mutate(root);
            File.WriteAllText(manifestPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            Assert(!PortableEditorCacheBuilder.TryLoadNativeTerrainLqTextureCache(
                       temporaryRoot,
                       levelKey,
                       out NativeTerrainLqTextureSet? rejectedManifest) && rejectedManifest == null,
                $"Public LQ cache loader accepted {label}.");
            File.WriteAllText(manifestPath, validManifest);
        }

        void AssertHqManifestMutationRejected(string label, Action<JsonObject> mutate)
        {
            JsonObject root = JsonNode.Parse(validManifest)?.AsObject()
                ?? throw new InvalidOperationException("Could not parse the valid HQ preview manifest fixture.");
            mutate(root);
            File.WriteAllText(manifestPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            Assert(!PortableEditorCacheBuilder.IsCurrentTerrainTexturePreviewCache(temporaryRoot, levelKey),
                $"Complete HQ/LQ cache validation accepted {label}.");
            Assert(PortableEditorCacheBuilder.TryLoadNativeTerrainLqTextureCache(
                       temporaryRoot,
                       levelKey,
                       out NativeTerrainLqTextureSet? stillValidLq) && stillValidLq != null,
                $"HQ-only manifest tamper case '{label}' incorrectly invalidated the independently proved LQ sidecar.");
            File.WriteAllText(manifestPath, validManifest);
        }
    }
    finally
    {
        if (Directory.Exists(temporaryRoot))
            Directory.Delete(temporaryRoot, recursive: true);
    }
}

static (int TextureCount, int FrameCount, int LqTextureCount, int NativeUnreferencedTextureCount) AssertInitialStateManifest(
    string workspaceRoot,
    LevelDefinition level,
    NativeTerrainTextureRuntimeControlAudit audit,
    IReadOnlySet<int> expectedFaceReferencedTextureIds)
{
    string manifestPath = Path.Combine(
        workspaceRoot,
        "editor-cache",
        "terrain-textures",
        level.Key,
        "manifest.json");
    Assert(File.Exists(manifestPath), $"{level.DisplayName}: missing initialized terrain texture cache manifest.");
    using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifestPath));
    JsonElement root = document.RootElement;
    Assert(root.GetProperty("cacheFormatVersion").GetInt32() == PortableEditorCacheBuilder.TerrainTexturePreviewCacheFormatVersion &&
           root.GetProperty("decoder").GetString() == PortableEditorCacheBuilder.TerrainTexturePreviewDecoder &&
           root.GetProperty("stateSemantics").GetString() == PortableEditorCacheBuilder.TerrainTexturePreviewStateSemantics &&
           root.GetProperty("runtimeControlComplete").GetBoolean() &&
           root.GetProperty("runtimeControlInitialStateComplete").GetBoolean() &&
           !root.GetProperty("runtimePlaybackIncluded").GetBoolean() &&
           !root.GetProperty("currentGameplayState").GetBoolean(),
        $"{level.DisplayName}: cache manifest does not describe a complete native load/default state.");
    Assert(root.GetProperty("runtimeControlSceneSha256").GetString() == audit.SceneSha256 &&
           root.GetProperty("runtimeControlProgramCount").GetInt32() == audit.AnimationControls.Count + audit.ScrollingControls.Count,
        $"{level.DisplayName}: cache manifest is not bound to the exact parsed runtime-control scene/program count.");

    HashSet<int> controlled = root.GetProperty("runtimeControlledTextureIds").EnumerateArray().Select(item => item.GetInt32()).ToHashSet();
    HashSet<int> initialized = root.GetProperty("runtimeInitializedTextureIds").EnumerateArray().Select(item => item.GetInt32()).ToHashSet();
    HashSet<int> sources = root.GetProperty("animationSourceTextureIds").EnumerateArray().Select(item => item.GetInt32()).ToHashSet();
    HashSet<int> faceReferenced = root.GetProperty("faceReferencedTextureIds").EnumerateArray().Select(item => item.GetInt32()).ToHashSet();
    HashSet<int> controlledWithoutFace = root.GetProperty("controlledWithoutFaceTextureIds").EnumerateArray().Select(item => item.GetInt32()).ToHashSet();
    HashSet<int> nativeUnreferenced = root.GetProperty("nativeUnreferencedTextureIds").EnumerateArray().Select(item => item.GetInt32()).ToHashSet();
    HashSet<int> expectedNativeUnreferenced = Enumerable.Range(0, audit.TextureCount)
        .Where(textureId =>
            !expectedFaceReferencedTextureIds.Contains(textureId) &&
            !controlled.Contains(textureId) &&
            !sources.Contains(textureId))
        .ToHashSet();
    Assert(controlled.SetEquals(audit.ControlledTextureIds) && initialized.SetEquals(controlled) &&
           sources.SetEquals(audit.AnimationSourceTextureIds) &&
           faceReferenced.SetEquals(expectedFaceReferencedTextureIds) &&
           controlledWithoutFace.SetEquals(controlled.Where(textureId => !faceReferenced.Contains(textureId))) &&
           nativeUnreferenced.SetEquals(expectedNativeUnreferenced) &&
           root.GetProperty("nativeTextureCount").GetInt32() == audit.TextureCount &&
           root.GetProperty("faceReferencedTextureCount").GetInt32() == faceReferenced.Count &&
           root.GetProperty("runtimeControlledTextureCount").GetInt32() == controlled.Count &&
           root.GetProperty("controlledWithoutFaceTextureCount").GetInt32() == controlledWithoutFace.Count &&
           root.GetProperty("animationSourceDiagnosticTextureCount").GetInt32() == sources.Count &&
           root.GetProperty("nativeUnreferencedTextureCount").GetInt32() == nativeUnreferenced.Count,
        $"{level.DisplayName}: cache manifest role sets do not match the native face/control/source partition.");

    int requested = root.GetProperty("requestedTextureCount").GetInt32();
    int decoded = root.GetProperty("decodedTextureCount").GetInt32();
    int completeDualTier = root.GetProperty("completeDualTierTextureCount").GetInt32();
    JsonElement textures = root.GetProperty("textures");
    Assert(requested == audit.TextureCount && decoded == requested && completeDualTier == requested && textures.GetArrayLength() == requested * 2,
        $"{level.DisplayName}: cache manifest is not complete for both exact HQ tiers.");

    string lqPayloadFile = root.GetProperty("lqPayloadFile").GetString() ?? "";
    int lqRequested = root.GetProperty("lqRequestedTextureCount").GetInt32();
    int lqDecoded = root.GetProperty("lqDecodedTextureCount").GetInt32();
    int completeLq = root.GetProperty("completeLqTextureCount").GetInt32();
    JsonElement lqTextures = root.GetProperty("lqTextures");
    Assert(root.GetProperty("lqStateSemantics").GetString() == PortableEditorCacheBuilder.TerrainTexturePreviewStateSemantics &&
           lqPayloadFile == NativeTerrainLqTextureCacheCodec.PayloadFileName && Path.GetFileName(lqPayloadFile) == lqPayloadFile &&
           root.GetProperty("lqPayloadFormatVersion").GetInt32() == NativeTerrainLqTextureCacheCodec.PayloadFormatVersion &&
           root.GetProperty("lqPayloadRecordSize").GetInt32() == NativeTerrainLqTextureCacheCodec.RecordSize &&
           root.GetProperty("lqDescriptorCountPerTexture").GetInt32() == NativeTerrainLqTextureCacheCodec.DescriptorCount &&
           root.GetProperty("lqTextureSide").GetInt32() == NativeTerrainLqTextureCacheCodec.TextureSide &&
           root.GetProperty("lqBitsPerPixel").GetInt32() == NativeTerrainLqTextureCacheCodec.BitsPerPixel &&
           root.GetProperty("lqPackedIndexByteCount").GetInt32() == NativeTerrainLqTextureCacheCodec.PackedIndexByteCount &&
           root.GetProperty("lqPaletteRowCount").GetInt32() == NativeTerrainLqTextureCacheCodec.PaletteRowCount &&
           root.GetProperty("lqPaletteColorCount").GetInt32() == NativeTerrainLqTextureCacheCodec.PaletteColorCount &&
           lqRequested == requested && lqDecoded == requested && completeLq == requested &&
           root.GetProperty("lqNativeTextureCount").GetInt32() == audit.TextureCount &&
           root.GetProperty("lqRetailAliasCount").GetInt32() == audit.TextureCount &&
           root.GetProperty("lqRetailAliasComplete").GetBoolean() &&
           lqTextures.GetArrayLength() == requested,
        $"{level.DisplayName}: cache manifest is not complete for the exact indexed LQ payload.");

    NativeTerrainLqTextureCacheProof lqProof = new(
        root.GetProperty("lqPayloadFormatVersion").GetInt32(),
        root.GetProperty("lqPayloadRecordSize").GetInt32(),
        root.GetProperty("lqPayloadByteLength").GetInt64(),
        root.GetProperty("lqPayloadSha256").GetString() ?? "",
        root.GetProperty("lqNativeTextureCount").GetInt32(),
        lqDecoded,
        root.GetProperty("lqTexturePagesByteLength").GetInt32(),
        root.GetProperty("lqTexturePagesSha256").GetString() ?? "",
        root.GetProperty("lqOriginalTextureComponentSha256").GetString() ?? "",
        root.GetProperty("lqInitializedTableSha256").GetString() ?? "",
        root.GetProperty("lqRetailAliasCount").GetInt32());
    string lqPayloadPath = Path.Combine(Path.GetDirectoryName(manifestPath)!, lqPayloadFile);
    NativeTerrainLqTextureCacheReadResult lqRead = NativeTerrainLqTextureCacheCodec.ReadValidated(lqPayloadPath, lqProof);
    Assert(lqRead.Complete && lqRead.TextureSet != null,
        $"{level.DisplayName}: exact LQ sidecar failed validation: {string.Join("; ", lqRead.SafetyBlockers)}");
    NativeTerrainLqTextureSet lqSet = lqRead.TextureSet!;
    Assert(lqSet.TextureCount == requested && lqSet.NativeTextureCount == audit.TextureCount && lqSet.RetailAliasComplete,
        $"{level.DisplayName}: validated LQ sidecar does not preserve the full native load-state proof.");
    Assert(PortableEditorCacheBuilder.TryLoadNativeTerrainLqTextureCache(workspaceRoot, level.Key, out NativeTerrainLqTextureSet? publicLoadedSet) &&
           publicLoadedSet != null && publicLoadedSet.TextureCount == lqSet.TextureCount &&
           publicLoadedSet.Textures.Select(texture => texture.TextureId).SequenceEqual(lqSet.Textures.Select(texture => texture.TextureId)),
        $"{level.DisplayName}: public fail-closed LQ cache loader rejected or changed the validated sidecar.");

    HashSet<int> hqTextureIds = textures.EnumerateArray()
        .Select(entry => entry.GetProperty("textureId").GetInt32())
        .ToHashSet();
    HashSet<int> normalHqTextureIds = [];
    HashSet<int> closeHqTextureIds = [];
    HashSet<(int TextureId, string DescriptorTier)> hqFrameKeys = [];
    string frameDirectory = Path.GetDirectoryName(manifestPath)!;
    HashSet<int> lqManifestIds = [];
    foreach (JsonElement lqEntry in lqTextures.EnumerateArray())
    {
        int textureId = lqEntry.GetProperty("textureId").GetInt32();
        bool hasRecord = lqSet.TryGetTexture(textureId, out NativeTerrainLqTextureRecordPayload lqRecord);
        Assert(lqManifestIds.Add(textureId) && hasRecord,
            $"{level.DisplayName}: duplicate or missing LQ texture {textureId}.");
        Assert(lqEntry.GetProperty("descriptorCount").GetInt32() == NativeTerrainLqTextureCacheCodec.DescriptorCount &&
               lqEntry.GetProperty("side").GetInt32() == NativeTerrainLqTextureCacheCodec.TextureSide &&
               lqEntry.GetProperty("bitsPerPixel").GetInt32() == NativeTerrainLqTextureCacheCodec.BitsPerPixel &&
               lqEntry.GetProperty("paletteRowCount").GetInt32() == NativeTerrainLqTextureCacheCodec.PaletteRowCount &&
               lqEntry.GetProperty("paletteColorCount").GetInt32() == NativeTerrainLqTextureCacheCodec.PaletteColorCount,
            $"{level.DisplayName}: LQ texture {textureId} has a malformed indexed/palette shape.");
        bool expectedControlled = controlled.Contains(textureId);
        bool expectedSource = sources.Contains(textureId);
        bool expectedFace = faceReferenced.Contains(textureId);
        bool expectedUnreferenced = nativeUnreferenced.Contains(textureId);
        string expectedState = expectedControlled
            ? PortableEditorCacheBuilder.TerrainTexturePreviewStateSemantics
            : expectedSource
                ? "animationSourceDiagnostic"
                : "staticRecord";
        string expectedRole = ExpectedRuntimeRole(expectedControlled, expectedSource, expectedFace);
        Assert(lqEntry.GetProperty("runtimeControlled").GetBoolean() == expectedControlled &&
               lqEntry.GetProperty("runtimeInitializationApplied").GetBoolean() == expectedControlled &&
               lqEntry.GetProperty("animationSourceDiagnostic").GetBoolean() == expectedSource &&
               lqEntry.GetProperty("faceReferenced").GetBoolean() == expectedFace &&
               lqEntry.GetProperty("nativeUnreferenced").GetBoolean() == expectedUnreferenced &&
               lqEntry.GetProperty("previewState").GetString() == expectedState &&
               lqEntry.GetProperty("runtimeRole").GetString() == expectedRole,
            $"{level.DisplayName}: LQ texture {textureId} does not preserve its exact native role/provenance.");

        JsonElement descriptors = lqEntry.GetProperty("descriptors");
        Assert(descriptors.GetArrayLength() == NativeTerrainLqTextureCacheCodec.DescriptorCount,
            $"{level.DisplayName}: LQ texture {textureId} does not retain both native descriptors.");
        foreach (JsonElement descriptorEntry in descriptors.EnumerateArray())
        {
            int descriptorIndex = descriptorEntry.GetProperty("descriptorIndex").GetInt32();
            Assert(descriptorIndex is >= 0 and < NativeTerrainLqTextureCacheCodec.DescriptorCount,
                $"{level.DisplayName}: LQ texture {textureId} has invalid descriptor index {descriptorIndex}.");
            NativeTerrainLqTextureDescriptorPayload descriptor = lqRecord.GetDescriptor(descriptorIndex);
            Assert(descriptorEntry.GetProperty("rawDescriptorHex").GetString() == Convert.ToHexString(descriptor.RawDescriptor.Span) &&
                   descriptorEntry.GetProperty("RawDescriptorSha256").GetString() == descriptor.RawDescriptorSha256 &&
                   descriptorEntry.GetProperty("PackedIndicesSha256").GetString() == descriptor.PackedIndicesSha256 &&
                   descriptorEntry.GetProperty("PaletteWordsSha256").GetString() == descriptor.PaletteWordsSha256 &&
                   descriptorEntry.GetProperty("Orientation").GetInt32() == descriptor.Orientation &&
                   descriptorEntry.GetProperty("Abr").GetInt32() == descriptor.Abr,
                $"{level.DisplayName}: LQ texture {textureId} descriptor {descriptorIndex} disagrees with the validated sidecar.");
        }
    }
    Assert(lqManifestIds.SetEquals(hqTextureIds) && lqManifestIds.SetEquals(lqSet.Textures.Select(texture => texture.TextureId)),
        $"{level.DisplayName}: LQ and HQ cache texture-id coverage differs.");

    foreach (JsonElement entry in textures.EnumerateArray())
    {
        int textureId = entry.GetProperty("textureId").GetInt32();
        string descriptorTier = entry.GetProperty("descriptorTier").GetString() ?? "";
        string previewTier = entry.GetProperty("previewTier").GetString() ?? "";
        int width = entry.GetProperty("width").GetInt32();
        int height = entry.GetProperty("height").GetInt32();
        int descriptorCount = entry.GetProperty("descriptorCount").GetInt32();
        int pixelCount = entry.GetProperty("pixelCount").GetInt32();
        long fileByteLength = entry.GetProperty("fileByteLength").GetInt64();
        string fileSha256 = entry.GetProperty("fileSha256").GetString() ?? "";
        string file = entry.GetProperty("file").GetString() ?? "";
        string expectedSuffix = descriptorTier switch
        {
            "hqData" => "normal",
            "hqDataClose" => "close",
            _ => ""
        };
        string expectedFile = $"{level.Key}-texture-{textureId:000}-{expectedSuffix}.png";
        Assert(!string.IsNullOrEmpty(expectedSuffix) && previewTier == expectedSuffix &&
               hqFrameKeys.Add((textureId, descriptorTier)) &&
               width > 0 && height > 0 && pixelCount == checked(width * height) &&
               descriptorCount == (descriptorTier == "hqData" ? 4 : 16) &&
               file == expectedFile && Path.GetFileName(file) == file &&
               fileByteLength > 0 && fileSha256.Length == 64 && fileSha256.All(Uri.IsHexDigit) &&
               PortableEditorCacheBuilder.IsNativeTerrainTexturePreviewFrameValid(
                   Path.Combine(frameDirectory, file),
                   width,
                   height,
                   fileByteLength,
                   fileSha256),
            $"{level.DisplayName}: HQ texture {textureId} {descriptorTier} does not match its exact PNG dimensions/length/SHA-256 proof.");
        (descriptorTier == "hqData" ? normalHqTextureIds : closeHqTextureIds).Add(textureId);
        bool expectedControlled = controlled.Contains(textureId);
        bool expectedSource = sources.Contains(textureId);
        bool expectedFace = faceReferenced.Contains(textureId);
        bool expectedUnreferenced = nativeUnreferenced.Contains(textureId);
        string expectedState = expectedControlled
            ? PortableEditorCacheBuilder.TerrainTexturePreviewStateSemantics
            : expectedSource
                ? "animationSourceDiagnostic"
                : "staticRecord";
        Assert(entry.GetProperty("runtimeControlled").GetBoolean() == expectedControlled &&
               entry.GetProperty("runtimeInitializationApplied").GetBoolean() == expectedControlled &&
               entry.GetProperty("animationSourceDiagnostic").GetBoolean() == expectedSource &&
               entry.GetProperty("faceReferenced").GetBoolean() == expectedFace &&
               entry.GetProperty("nativeUnreferenced").GetBoolean() == expectedUnreferenced &&
               entry.GetProperty("previewState").GetString() == expectedState &&
               entry.GetProperty("runtimeRole").GetString() == ExpectedRuntimeRole(expectedControlled, expectedSource, expectedFace),
            $"{level.DisplayName}: HQ texture {textureId} does not preserve its exact native role/provenance.");
    }
    Assert(normalHqTextureIds.SetEquals(lqManifestIds) &&
           closeHqTextureIds.SetEquals(lqManifestIds) &&
           hqFrameKeys.Count == requested * 2 &&
           PortableEditorCacheBuilder.IsCurrentTerrainTexturePreviewCache(workspaceRoot, level.Key),
        $"{level.DisplayName}: normal HQ, close HQ, and independently validated LQ key sets are not identical/current.");

    foreach (int textureId in controlled.Concat(sources).Distinct())
    {
        JsonElement[] entries = textures.EnumerateArray()
            .Where(entry => entry.GetProperty("textureId").GetInt32() == textureId)
            .ToArray();
        JsonElement[] lqEntries = lqTextures.EnumerateArray()
            .Where(entry => entry.GetProperty("textureId").GetInt32() == textureId)
            .ToArray();
        Assert(entries.Length == 2 &&
               entries.Select(entry => entry.GetProperty("descriptorTier").GetString()).ToHashSet().SetEquals(["hqData", "hqDataClose"]),
            $"{level.DisplayName}: runtime texture {textureId} is missing an exact normal or close preview frame.");
        Assert(lqEntries.Length == 1,
            $"{level.DisplayName}: runtime texture {textureId} is missing its exact indexed LQ record.");
        if (controlled.Contains(textureId))
        {
            Assert(entries.All(entry => entry.GetProperty("runtimeControlled").GetBoolean() &&
                    entry.GetProperty("runtimeInitializationApplied").GetBoolean() &&
                    entry.GetProperty("previewState").GetString() == PortableEditorCacheBuilder.TerrainTexturePreviewStateSemantics),
                $"{level.DisplayName}: controlled texture {textureId} lacks per-entry load-initialization proof.");
            Assert(lqEntries.All(entry => entry.GetProperty("runtimeControlled").GetBoolean() &&
                    entry.GetProperty("runtimeInitializationApplied").GetBoolean() &&
                    entry.GetProperty("previewState").GetString() == PortableEditorCacheBuilder.TerrainTexturePreviewStateSemantics),
                $"{level.DisplayName}: controlled texture {textureId} lacks exact LQ load-initialization proof.");
        }
        if (sources.Contains(textureId))
        {
            Assert(entries.All(entry => entry.GetProperty("animationSourceDiagnostic").GetBoolean()),
                $"{level.DisplayName}: animation source texture {textureId} is not labeled as a diagnostic/default-timeline source.");
            Assert(lqEntries.All(entry => entry.GetProperty("animationSourceDiagnostic").GetBoolean()),
                $"{level.DisplayName}: animation source texture {textureId} is not labeled as an LQ diagnostic/default-timeline source.");
        }
    }

    return (requested, textures.GetArrayLength(), lqSet.TextureCount, nativeUnreferenced.Count);
}

static string ExpectedRuntimeRole(bool controlled, bool animationSource, bool faceReferenced) =>
    controlled
        ? faceReferenced
            ? "controlledFaceDestination"
            : "controlledDestinationWithoutFace"
        : animationSource
            ? "animationSourceDiagnostic"
            : faceReferenced
                ? "faceReferencedStatic"
                : "nativeUnreferencedStatic";

static void AssertInitializedScrollingCoordinates(
    LevelDefinition level,
    NativeTerrainTextureInitialStateResult initialState,
    NativeTerrainScrollingTextureControl control)
{
    int lqOffset = initialState.LowDetailTableOffset + (control.DestinationTextureId * 16);
    int hqOffset = initialState.HighDetailTableOffset + (control.DestinationTextureId * 168);
    byte[] lq = initialState.InitializedTextureData.AsSpan(lqOffset, 16).ToArray();
    byte[] hq = initialState.InitializedTextureData.AsSpan(hqOffset, 168).ToArray();
    byte quarter = (byte)(control.InitialPhase >> 2);
    byte half = (byte)(control.InitialPhase >> 1);
    Assert(new[] { 1, 5, 9, 13 }.All(offset => lq[offset] == quarter),
        $"{level.DisplayName}: initialized LQ scrolling coordinates do not match native phase {control.InitialPhase}.");
    Assert(new[] { 1, 5 }.All(offset => hq[offset] == quarter) &&
           new[] { 9, 13, 17, 21 }.All(offset => hq[offset] == half) &&
           new[] { 25, 29, 33, 37 }.All(offset => hq[offset] == ((half + 32) & 63)) &&
           new[] { 41, 45, 49, 53, 57, 61, 65, 69 }.All(offset => hq[offset] == half) &&
           new[] { 73, 77, 81, 85, 89, 93, 97, 101 }.All(offset => hq[offset] == ((half + 16) & 63)) &&
           new[] { 105, 109, 113, 117, 121, 125, 129, 133 }.All(offset => hq[offset] == ((half + 32) & 63)) &&
           new[] { 137, 141, 145, 149, 153, 157, 161, 165 }.All(offset => hq[offset] == ((half + 48) & 63)),
        $"{level.DisplayName}: initialized HQ scrolling coordinates do not match native phase {control.InitialPhase}.");
}
static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

internal sealed record AuditRow(
    string LevelKey,
    string LevelName,
    string Realm,
    int WadEntry,
    int TextureCount,
    int RuntimePersistentTargetCount,
    IReadOnlyList<int> AnimatedTextureIds,
    IReadOnlyList<int> ScrollingTextureIds,
    IReadOnlyList<int> AnimationSourceTextureIds,
    int SceneByteLength,
    string SceneSha256);
