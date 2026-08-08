using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Spyro.Editor.Core.Diagnostics;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;
using System.Text.Json;

namespace Spyro.Editor.App.Views;

public sealed partial class MainWindow
{
    private const string AppendedPrivateTerrainBuildSafetyCodePrefix =
        "terrain-appended-private-";

    private readonly TextBlock _buildSafetySummaryText = new()
    {
        Text = "Not inspected yet",
        TextWrapping = TextWrapping.Wrap,
        FontSize = 12,
        LineHeight = 17
    };
    private Button? _inspectBuildSafetyButton;
    private MobyBuildSafetyInspectionResult? _lastBuildSafetyInspection;

    private Control BuildModernBuildSafetyPanel()
    {
        StackPanel panel = new() { Spacing = 8 };
        panel.Children.Add(ModernSectionHeading("Build safety"));

        _buildSafetySummaryText.Foreground = new SolidColorBrush(ModernMutedInk);
        panel.Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(244, 247, 249)),
            BorderBrush = new SolidColorBrush(ModernLine),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(9, 7),
            Child = _buildSafetySummaryText
        });

        _inspectBuildSafetyButton = NewAsyncButton("Inspect Build Safety", ShowBuildSafetyInspectorAsync);
        StyleModernPrimaryButton(_inspectBuildSafetyButton, ModernTeal);
        ToolTip.SetTip(
            _inspectBuildSafetyButton,
            "Check saved object, chest, and terrain edits before Create BIN.");
        panel.Children.Add(_inspectBuildSafetyButton);
        if (!_releaseMode)
            panel.Children.Add(BuildSpecialChestAvailabilityControl());
        return panel;
    }

    private async Task ShowBuildSafetyInspectorAsync()
    {
        string sourceImage = FirstExistingDiscImagePath(
            _discImagePathBox.Text,
            _skyboxDiscImagePathBox.Text,
            DiscImageLocator.FindImage(_workspace));
        if (!File.Exists(sourceImage))
        {
            _statusText.Text = "Choose the original Spyro BIN/CUE before inspecting build safety.";
            return;
        }

        await SaveCurrentEditsAsync();
        IReadOnlyList<EditedLevelExportTarget> targets = FindEditedLevelExportTargets();
        MobyBuildSafetyInspectionResult inspection = await InspectSavedObjectBuildSafetyAsync(sourceImage, targets);
        await ShowBuildSafetyDialogAsync(inspection, allowCreateAnyway: false);
    }

    private async Task<MobyBuildSafetyInspectionResult> InspectSavedObjectBuildSafetyAsync(
        string sourceImage,
        IReadOnlyList<EditedLevelExportTarget> targets)
    {
        string outputDirectory = EnsureUserOutputDirectory();
        string sourceCue = DiscImageLocator.FindCueForImage(sourceImage);
        List<MobyBuildSafetyInput> inputs = [];
        foreach (EditedLevelExportTarget target in targets.Where(target => target.HasObjectEdits))
        {
            string editsPath = Path.Combine(_workspace.RootPath, $"{target.Level.Key}-native-edits.json");
            string planPrefix = Path.Combine(outputDirectory, "_combined-build", $"safety-{target.Level.Key}");
            try
            {
                MobySourcePatchPlan plan = await Task.Run(() => MobySourcePatchExporter.BuildPlan(
                    sourceImage,
                    sourceCue,
                    $"{planPrefix}.bin",
                    $"{planPrefix}.cue",
                    target.Level,
                    editsPath,
                    nativeMobyPathEditsPath: NativeMobyPathEditPath(target.Level.Key)));
                inputs.Add(new MobyBuildSafetyInput(target.Level, plan));
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or OverflowException)
            {
                inputs.Add(new MobyBuildSafetyInput(
                    target.Level,
                    CreateEmptyBuildSafetyPlan(sourceImage, sourceCue, target.Level, editsPath, planPrefix),
                    ex.Message));
            }
        }

        MobyBuildSafetyReport report = await Task.Run(() => MobyBuildSafetyInspector.Inspect(sourceImage, inputs));
        report = await AddSpecialChestBuildSafetyAsync(report, targets, sourceImage);
        report = await AddAppendedPrivateTerrainTextureBuildSafetyAsync(
            report,
            targets,
            sourceImage);
        string reportPrefix = Path.Combine(outputDirectory, "Spyro Editor - All Saved Edits.build-safety");
        MobyBuildSafetyWriteResult written = await MobyBuildSafetyReportWriter.WriteAsync(
            report,
            $"{reportPrefix}.json",
            $"{reportPrefix}.md");
        MobyBuildSafetyInspectionResult result = new(report, written);
        _lastBuildSafetyInspection = result;
        RefreshBuildSafetySummary(result);
        EditorDiagnostics.RecordAction(
            "Build safety inspected",
            $"Status: {report.StatusLabel}; levels: {report.Levels.Count}; true appends: {report.TrueAppendCount}; runtime slots consumed: {report.RuntimeSlotsConsumed}; skipped edits: {report.SkippedEditCount}; report: {written.JsonPath}");
        return result;
    }

    private async Task<MobyBuildSafetyReport> AddAppendedPrivateTerrainTextureBuildSafetyAsync(
        MobyBuildSafetyReport report,
        IReadOnlyList<EditedLevelExportTarget> targets,
        string sourceImagePath)
    {
        List<MobyBuildSafetyLevelReport> levels = report.Levels.ToList();
        foreach (EditedLevelExportTarget target in targets.Where(candidate =>
                     candidate.HasTerrainEdits ||
                     candidate.HasNativeTerrainTextureRelocations ||
                     File.Exists(NativeTerrainTextureRelocationEditStore.ManifestPath(
                         _workspace.RootPath,
                         candidate.Level.Key))))
        {
            string manifestPath = NativeTerrainTextureRelocationEditStore.ManifestPath(
                _workspace.RootPath,
                target.Level.Key);
            string manifestError = InspectNativeTerrainTextureManifestEnvelope(
                manifestPath,
                target.Level.Key,
                out int declaredRowCount);
            IReadOnlyList<NativeTerrainTextureRelocationEdit> edits =
                Array.Empty<NativeTerrainTextureRelocationEdit>();
            if (string.IsNullOrWhiteSpace(manifestError))
            {
                try
                {
                    edits = NativeTerrainTextureRelocationEditStore.Load(
                        _workspace.RootPath,
                        target.Level.Key);
                    if (declaredRowCount != edits.Count)
                    {
                        manifestError =
                            $"The relocation manifest declares {declaredRowCount} row(s), but only " +
                            $"{edits.Count} unique valid row(s) loaded.";
                    }
                }
                catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
                {
                    manifestError = ex.Message;
                }
            }

            bool hasAppendedRows = edits.Any(edit => edit.UsesAppendedPrivateRecord);

            List<TerrainTextureBuildSafetyFaceSnapshot> faces = [];
            string geometryError = "";
            try
            {
                TerrainGeometryLoadData loaded =
                    _currentLevel != null &&
                    _currentGeometry != null &&
                    string.Equals(
                        LevelCatalog.NormalizeKey(_currentLevel.Key),
                        LevelCatalog.NormalizeKey(target.Level.Key),
                        StringComparison.OrdinalIgnoreCase)
                        ? new TerrainGeometryLoadData(
                            _currentGeometry,
                            _loadedTerrainEdits,
                            _terrainCacheHealthMessage)
                        : LoadGeometryData(target.Level.Key);
                if (loaded.Geometry == null)
                {
                    geometryError =
                        $"{target.Level.DisplayName}'s source-derived terrain did not load.";
                }
                else
                {
                    faces = loaded.Geometry.Polygons
                        .Select((face, index) => new TerrainTextureBuildSafetyFaceSnapshot(
                            index,
                            face.RuntimeKey,
                            face.TextureId,
                            face.OriginalTextureId,
                            face.IsTerrainRemoved))
                        .ToList();
                }
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
            {
                geometryError = ex.Message;
            }

            NativeTerrainTextureRecordAppendSourceBinding? currentBinding = null;
            string bindingError = "";
            try
            {
                currentBinding = await Task.Run(() =>
                    NativeTerrainTextureRecordAppendBuilder.InspectSourceBinding(
                        sourceImagePath,
                        target.Level));
            }
            catch (Exception ex) when (
                ex is InvalidDataException or IOException or UnauthorizedAccessException or OverflowException)
            {
                bindingError = ex.Message;
            }

            bool gateAllowed = false;
            string gateReason;
            if (hasAppendedRows &&
                string.IsNullOrWhiteSpace(manifestError) &&
                string.IsNullOrWhiteSpace(geometryError) &&
                string.IsNullOrWhiteSpace(bindingError) &&
                currentBinding != null)
            {
                (gateAllowed, gateReason) =
                    await ResolveExactAppendedPrivateTerrainTextureGateAsync(
                        target,
                        sourceImagePath,
                        edits,
                        currentBinding);
            }
            else
            {
                gateReason =
                    "Build Safety could not select an exact source-bound structural writer for this saved private-texture batch. " +
                    "Pending or unknown writer state remains blocked; a level-key allow list is not release authorization.";
            }
            IReadOnlyList<MobyBuildSafetyIssue> terrainIssues =
                InspectAppendedPrivateTerrainTextureState(
                    target.Level,
                    edits,
                    faces,
                    currentBinding,
                    manifestError,
                    geometryError,
                    bindingError,
                    gateAllowed,
                    gateReason);
            if (terrainIssues.Count == 0)
                continue;

            int existingIndex = levels.FindIndex(level =>
                string.Equals(
                    LevelCatalog.NormalizeKey(level.LevelKey),
                    LevelCatalog.NormalizeKey(target.Level.Key),
                    StringComparison.OrdinalIgnoreCase));
            MobyBuildSafetyLevelReport existing = existingIndex >= 0
                ? levels[existingIndex]
                : CreateTerrainTextureBuildSafetyLevelReport(target.Level);
            List<string> findings = existing.Findings.ToList();
            List<string> recommendations = existing.Recommendations.ToList();
            int appendedCount = edits.Count(edit => edit.UsesAppendedPrivateRecord);
            findings.Add(
                appendedCount == 0
                    ? "A saved native terrain-texture manifest is invalid and cannot be trusted by Create BIN."
                    : $"{appendedCount} appended-private native terrain texture row(s) were checked against the gate, live face assignments, contiguous IDs, and source preimage.");
            recommendations.Add(
                "Double-click an affected terrain section, then Undo or remove its guarded private texture row. Do not enable normal Create BIN until the exact destination writer has DuckStation runtime evidence.");
            MobyBuildSafetyStatus status = (MobyBuildSafetyStatus)Math.Max(
                (int)existing.Status,
                (int)terrainIssues.Max(issue => issue.Status));
            MobyBuildSafetyLevelReport updated = existing with
            {
                Status = status,
                Issues = existing.Issues.Concat(terrainIssues).ToArray(),
                Findings = findings,
                Recommendations = recommendations
            };
            if (existingIndex >= 0)
                levels[existingIndex] = updated;
            else
                levels.Add(updated);
        }

        MobyBuildSafetyStatus overall = levels.Count == 0
            ? report.Status
            : levels.Max(level => level.Status);
        return report with { Status = overall, Levels = levels };
    }

    private async Task<(bool Allowed, string Reason)>
        ResolveExactAppendedPrivateTerrainTextureGateAsync(
            EditedLevelExportTarget target,
            string sourceImagePath,
            IReadOnlyList<NativeTerrainTextureRelocationEdit> savedEdits,
            NativeTerrainTextureRecordAppendSourceBinding sourceBinding)
    {
        try
        {
            string sourceCue = DiscImageLocator.FindCueForImage(sourceImagePath);
            string outputDirectory = EnsureUserOutputDirectory();
            string planDirectory = Path.Combine(
                outputDirectory,
                "_combined-build",
                "build-safety-private-terrain");
            Directory.CreateDirectory(planDirectory);
            TerrainPatchResult? ordinaryResult = await TryPlanTerrainPatchAsync(
                target.Level,
                sourceImagePath,
                sourceCue,
                planDirectory,
                $"{SafeOutputPart(target.Level.DisplayName)}-private-build-safety",
                writeImage: false,
                includeNativeTextureRelocations: false);
            if (ordinaryResult == null)
            {
                return (
                    false,
                    "Build Safety could not form the saved face-assignment preflight needed to select one exact private-texture structural writer.");
            }

            IReadOnlyList<NativeTerrainTextureRecordExistingPatch> ordinaryPatches =
                NativeTerrainTexturePrivateRecordBatchCompiler.ConvertOrdinaryTerrainPlan(
                    ordinaryResult.Plan);
            string wadAnalysisPath = await EnsureSkyboxWadAnalysisAsync(sourceImagePath);
            NativeTerrainTexturePrivateRecordBatchRequest request = new(
                SourceImagePath: sourceImagePath,
                SourceCuePath: sourceCue,
                OutputPrefix: Path.Combine(
                    planDirectory,
                    $"{SafeOutputPart(target.Level.DisplayName)}-private-not-exported"),
                WadAnalysisPath: wadAnalysisPath,
                TargetLevel: target.Level,
                SavedEdits: savedEdits,
                OrdinaryLevelDataPatches: ordinaryPatches,
                HasCustomImageTexturePatches:
                    ordinaryResult.Plan.CustomTextureBytePatchCount > 0,
                StructuralGrowthPolicy:
                    AppendedPrivateTerrainTextureResearchGate
                        .ResolveStructuralGrowthPolicy(
                            sourceBinding,
                            savedEdits.Count(edit =>
                                edit.UsesAppendedPrivateRecord)));
            NativeTerrainTexturePrivateRecordBatchPlan plan =
                await Task.Run(() =>
                    NativeTerrainTexturePrivateRecordBatchCompiler.BuildPlan(
                        request));
            CachePrivateTerrainBuildPlan(request, plan);
            bool allowed =
                AppendedPrivateTerrainTextureResearchGate.TryAllow(
                plan.SourceBinding,
                plan.WriterKind,
                plan.AppendedPrivateEdits.Count,
                plan.StructuralGrowthPolicy,
                out string reason);
            return (
                allowed,
                $"Exact writer: {plan.WriterKind}. {reason}");
        }
        catch (Exception ex) when (
            ex is InvalidDataException or InvalidOperationException or IOException or
                UnauthorizedAccessException or OverflowException)
        {
            return (
                false,
                $"Build Safety could not select an exact source-bound structural writer: {ex.Message}");
        }
    }

    private static MobyBuildSafetyLevelReport CreateTerrainTextureBuildSafetyLevelReport(
        LevelDefinition level)
    {
        return new MobyBuildSafetyLevelReport(
            LevelKey: level.Key,
            LevelName: level.DisplayName,
            Status: MobyBuildSafetyStatus.Stable,
            CatalogSourceRecordCount: level.SourceRecordCount,
            SourceRuntimeRecordCount: 0,
            SourceCatalogRecordCount: 0,
            PlannedRuntimeRecordCount: 0,
            PlannedCatalogRecordCount: 0,
            CatalogPrefixRows: 0,
            TrueAppendCount: 0,
            SourceSlotReuseCount: 0,
            SkippedEditCount: 0,
            ComponentWadOffset: 0,
            ComponentByteLength: 0,
            SourceDynamicArenaBytes: 0,
            ProjectedDynamicArenaBytes: 0,
            SourceDynamicCapacity: 0,
            ProjectedDynamicCapacity: 0,
            RuntimeSlotsConsumed: 0,
            PersistentIndexHeadroom: 0,
            RuntimeRowAlignmentDeltaBytes: 0,
            ComponentRepacked: false,
            Issues: [],
            Findings: [],
            Recommendations: []);
    }

    private static string InspectNativeTerrainTextureManifestEnvelope(
        string manifestPath,
        string expectedLevelKey,
        out int declaredRowCount)
    {
        declaredRowCount = 0;
        if (!File.Exists(manifestPath))
            return "";

        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return "The native terrain-texture relocation manifest root is not an object.";
            if (!root.TryGetProperty("version", out JsonElement versionElement) ||
                !versionElement.TryGetInt32(out int version) ||
                version is not (1 or 2 or 3))
            {
                return "The native terrain-texture relocation manifest has an unsupported or missing version.";
            }

            string savedLevelKey = root.TryGetProperty(
                    "destinationLevelKey",
                    out JsonElement levelElement) &&
                levelElement.ValueKind == JsonValueKind.String
                    ? LevelCatalog.NormalizeKey(levelElement.GetString() ?? "")
                    : "";
            if (!string.Equals(
                    savedLevelKey,
                    LevelCatalog.NormalizeKey(expectedLevelKey),
                    StringComparison.OrdinalIgnoreCase))
            {
                return
                    $"The relocation manifest belongs to '{savedLevelKey}', not " +
                    $"'{LevelCatalog.NormalizeKey(expectedLevelKey)}'.";
            }

            if (!root.TryGetProperty("relocations", out JsonElement relocations) ||
                relocations.ValueKind != JsonValueKind.Array)
            {
                return "The native terrain-texture relocation manifest is missing its relocations array.";
            }
            declaredRowCount = relocations.GetArrayLength();
            return "";
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return $"The native terrain-texture relocation manifest could not be read: {ex.Message}";
        }
    }

    internal static IReadOnlyList<MobyBuildSafetyIssue>
        InspectAppendedPrivateTerrainTextureState(
            LevelDefinition level,
            IReadOnlyList<NativeTerrainTextureRelocationEdit> edits,
            IReadOnlyList<TerrainTextureBuildSafetyFaceSnapshot> faces,
            NativeTerrainTextureRecordAppendSourceBinding? currentBinding,
            string manifestError,
            string geometryError,
            string bindingError,
            bool gateAllowed,
            string gateReason)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(edits);
        ArgumentNullException.ThrowIfNull(faces);
        List<MobyBuildSafetyIssue> issues = [];
        TerrainTextureBuildSafetyFaceSnapshot? firstVisible = faces.FirstOrDefault(face => !face.IsRemoved);
        if (!string.IsNullOrWhiteSpace(manifestError))
        {
            TerrainTextureBuildSafetyFaceSnapshot? target = faces.FirstOrDefault(face =>
                !face.IsRemoved && face.TextureId != face.OriginalTextureId) ?? firstVisible;
            issues.Add(NewTerrainTextureBuildSafetyIssue(
                "manifest-invalid",
                $"The saved appended-private terrain-texture manifest is invalid: {manifestError}",
                level,
                target));
        }

        NativeTerrainTextureRelocationEdit[] appended = edits
            .Where(edit => edit.UsesAppendedPrivateRecord)
            .OrderBy(edit => edit.TargetTextureId)
            .ToArray();
        if (!string.IsNullOrWhiteSpace(geometryError) && appended.Length > 0)
        {
            issues.Add(NewTerrainTextureBuildSafetyIssue(
                "terrain-unavailable",
                $"Live face ownership could not be checked: {geometryError}",
                level,
                target: null));
        }
        if ((!string.IsNullOrWhiteSpace(bindingError) || currentBinding == null) &&
            appended.Length > 0)
        {
            issues.Add(NewTerrainTextureBuildSafetyIssue(
                "source-binding-unavailable",
                $"The retail texture-table preimage could not be checked: " +
                $"{(string.IsNullOrWhiteSpace(bindingError) ? "no binding was resolved" : bindingError)}",
                level,
                SelectTerrainTextureIssueFace(faces, appended[0], sourceTextureCount: -1)));
        }

        if (appended.Length > 0 && !gateAllowed)
        {
            foreach (NativeTerrainTextureRelocationEdit edit in appended)
            {
                issues.Add(NewTerrainTextureBuildSafetyIssue(
                    "gate-disallowed-row",
                    $"Private texture T{edit.TargetTextureId} is not allowed by normal Create BIN. {gateReason}",
                    level,
                    SelectTerrainTextureIssueFace(
                        faces,
                        edit,
                        currentBinding?.ExpectedSourceTextureCount ?? edit.SourceTextureRecordCount)));
            }
        }

        try
        {
            NativeTerrainTextureAppendedPrivateCompaction compacted =
                NativeTerrainTextureRelocationEditStore.CompactAppendedPrivateRecords(edits);
            if (compacted.Changed)
            {
                issues.Add(NewTerrainTextureBuildSafetyIssue(
                    "manifest-needs-compaction",
                    "Appended-private texture IDs are gapped, duplicated by donor/material contract, or no longer match their canonical contiguous table order.",
                    level,
                    SelectTerrainTextureIssueFace(
                        faces,
                        appended.FirstOrDefault(),
                        currentBinding?.ExpectedSourceTextureCount ?? compacted.SourceTextureRecordCount)));
            }
        }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or OverflowException)
        {
            issues.Add(NewTerrainTextureBuildSafetyIssue(
                "manifest-binding-conflict",
                $"Appended-private rows do not form one valid table: {ex.Message}",
                level,
                SelectTerrainTextureIssueFace(
                    faces,
                    appended.FirstOrDefault(),
                    currentBinding?.ExpectedSourceTextureCount ?? -1)));
        }

        int sourceTextureCount = currentBinding?.ExpectedSourceTextureCount ??
            appended.Select(edit => edit.SourceTextureRecordCount).FirstOrDefault(count => count >= 0, -1);
        foreach (NativeTerrainTextureRelocationEdit edit in appended)
        {
            TerrainTextureBuildSafetyFaceSnapshot? target = SelectTerrainTextureIssueFace(
                faces,
                edit,
                sourceTextureCount);
            bool invalidStableIdentity;
            try
            {
                invalidStableIdentity = !string.Equals(
                    edit.PrivateRecordEditId,
                    NativeTerrainTextureRelocationEditStore.BuildAppendedPrivateRecordId(
                        edit.TargetTextureId),
                    StringComparison.Ordinal);
            }
            catch (ArgumentOutOfRangeException)
            {
                invalidStableIdentity = true;
            }

            bool invalidRow =
                edit.TargetTextureId is < 0 or > 127 ||
                sourceTextureCount >= 0 && edit.TargetTextureId < sourceTextureCount ||
                edit.ApplyMode != NativeTerrainTextureRelocationApplyMode.ArtOnlyPreserveTarget ||
                edit.DescriptorTier != NativeTerrainTextureRelocationEditStore.CompleteDescriptorTier ||
                edit.MaterialTemplateTextureId < 0 ||
                sourceTextureCount >= 0 && edit.MaterialTemplateTextureId >= sourceTextureCount ||
                edit.DonorWadEntry < 0 ||
                edit.DonorTextureId < 0 ||
                string.IsNullOrWhiteSpace(LevelCatalog.NormalizeKey(edit.DonorLevelKey)) ||
                string.IsNullOrWhiteSpace(edit.DonorProvenanceKey) ||
                invalidStableIdentity;
            if (invalidRow)
            {
                issues.Add(NewTerrainTextureBuildSafetyIssue(
                    "row-invalid",
                    $"Private texture T{edit.TargetTextureId} has invalid ID, art-only mode, descriptor tier, material template, donor provenance, or stable manifest identity.",
                    level,
                    target));
            }

            if (currentBinding != null &&
                (edit.TargetWadEntry != currentBinding.TargetWadEntry ||
                 edit.SourceTextureRecordCount != currentBinding.ExpectedSourceTextureCount ||
                 !string.Equals(
                     edit.SourceImageSha256,
                     currentBinding.SourceImageSha256,
                     StringComparison.OrdinalIgnoreCase) ||
                 !string.Equals(
                     edit.SourceTextureComponentSha256,
                     currentBinding.ExpectedTextureComponentSha256,
                     StringComparison.OrdinalIgnoreCase) ||
                 !string.Equals(
                     edit.SourceLevelDataSha256,
                     currentBinding.ExpectedLevelDataSha256,
                     StringComparison.OrdinalIgnoreCase)))
            {
                issues.Add(NewTerrainTextureBuildSafetyIssue(
                    "stale-source-binding",
                    $"Private texture T{edit.TargetTextureId} is bound to a different disc, WAD entry, retail texture count, texture component, or level-data preimage.",
                    level,
                    target));
            }

            if (!faces.Any(face => !face.IsRemoved && face.TextureId == edit.TargetTextureId))
            {
                issues.Add(NewTerrainTextureBuildSafetyIssue(
                    "orphan-row",
                    $"Private texture T{edit.TargetTextureId} has a manifest row but no live terrain section uses it.",
                    level,
                    target));
            }
        }

        if (sourceTextureCount >= 0)
        {
            HashSet<int> appendedIds = appended
                .Select(edit => edit.TargetTextureId)
                .ToHashSet();
            foreach (IGrouping<int, TerrainTextureBuildSafetyFaceSnapshot> missing in faces
                         .Where(face =>
                             !face.IsRemoved &&
                             face.TextureId >= sourceTextureCount &&
                             !appendedIds.Contains(face.TextureId))
                         .GroupBy(face => face.TextureId)
                         .OrderBy(group => group.Key))
            {
                issues.Add(NewTerrainTextureBuildSafetyIssue(
                    "missing-manifest-row",
                    $"Terrain texture T{missing.Key} is assigned to {missing.Count()} live section(s), but no matching appended-private manifest row exists.",
                    level,
                    missing.OrderBy(face => face.TerrainIndex).First()));
            }
        }

        return issues
            .DistinctBy(issue => (
                issue.Code,
                issue.EditorTrueIndex,
                issue.MobyLabel,
                issue.Message))
            .OrderBy(issue => issue.EditorTrueIndex ?? int.MaxValue)
            .ThenBy(issue => issue.Code, StringComparer.Ordinal)
            .ToArray();
    }

    private static TerrainTextureBuildSafetyFaceSnapshot? SelectTerrainTextureIssueFace(
        IReadOnlyList<TerrainTextureBuildSafetyFaceSnapshot> faces,
        NativeTerrainTextureRelocationEdit? edit,
        int sourceTextureCount)
    {
        if (edit != null)
        {
            TerrainTextureBuildSafetyFaceSnapshot? consumer = faces.FirstOrDefault(face =>
                !face.IsRemoved && face.TextureId == edit.TargetTextureId);
            if (consumer != null)
                return consumer;
            TerrainTextureBuildSafetyFaceSnapshot? template = faces.FirstOrDefault(face =>
                !face.IsRemoved &&
                face.OriginalTextureId == edit.MaterialTemplateTextureId);
            if (template != null)
                return template;
        }

        return faces.FirstOrDefault(face =>
                   !face.IsRemoved &&
                   sourceTextureCount >= 0 &&
                   face.TextureId >= sourceTextureCount) ??
            faces.FirstOrDefault(face => !face.IsRemoved);
    }

    private static MobyBuildSafetyIssue NewTerrainTextureBuildSafetyIssue(
        string suffix,
        string message,
        LevelDefinition level,
        TerrainTextureBuildSafetyFaceSnapshot? target)
    {
        return new MobyBuildSafetyIssue(
            Code: AppendedPrivateTerrainBuildSafetyCodePrefix + suffix,
            Status: MobyBuildSafetyStatus.Blocked,
            Message: message,
            LevelKey: level.Key,
            LevelName: level.DisplayName,
            EditorTrueIndex: target?.TerrainIndex,
            MobyLabel: target?.RuntimeKey ?? "");
    }

    private static bool IsTerrainTextureBuildSafetyIssue(MobyBuildSafetyIssue issue) =>
        issue.Code.StartsWith(
            AppendedPrivateTerrainBuildSafetyCodePrefix,
            StringComparison.Ordinal);

    private string BuildSafetyIssueTargetLabel(MobyBuildSafetyIssue issue) =>
        IsTerrainTextureBuildSafetyIssue(issue)
            ? issue.CanNavigate
                ? _releaseMode ? "Terrain section" : $"Terrain section {issue.MobyLabel}"
                : _releaseMode ? "Terrain texture edit" : "Terrain texture manifest"
            : issue.TargetLabel;

    private string BuildSafetyIssueMessage(MobyBuildSafetyIssue issue)
    {
        if (!_releaseMode)
            return issue.Message;

        if (IsTerrainTextureBuildSafetyIssue(issue))
        {
            return issue.CanNavigate
                ? "This terrain texture edit no longer matches the loaded disc. Double-click it, then undo or repaint that section."
                : "One or more terrain texture edits no longer match the loaded disc. Undo the affected edit or restore the original terrain before creating a BIN.";
        }

        if (issue.Code.StartsWith("special-chest-", StringComparison.Ordinal))
            return "This special chest is incomplete or cannot be safely combined with the current level edits. Double-click it, then remove and add the complete chest group again.";

        return issue.Code switch
        {
            "skipped-edit" or "skipped-edit-unresolved" =>
                "This object edit cannot be safely exported yet. Double-click the object, then undo or replace the edit.",
            "actor-package-blocked" or "actor-package-blocked-unresolved" or
            "actor-package-test-only" or "actor-package-test-only-unresolved" =>
                "This cross-level object is not available for normal Create BIN in this level yet.",
            "source-row-append" or "source-row-append-blocked" =>
                "This added object exceeds the level's currently supported object capacity.",
            "locked-chest-runtime-bundle" =>
                "The complete Key + Locked Chest group is included and will be created together.",
            "level-wide-safety-limit" =>
                "Some edits in this level cannot be safely combined. Review the items below before creating a BIN.",
            "native-component-unresolved" =>
                "The editor could not read the required level data from the selected disc. Reopen the original BIN/CUE and inspect again.",
            _ => issue.Message
        };
    }

    internal sealed record TerrainTextureBuildSafetyFaceSnapshot(
        int TerrainIndex,
        string RuntimeKey,
        int TextureId,
        int OriginalTextureId,
        bool IsRemoved);

    private static MobySourcePatchPlan CreateEmptyBuildSafetyPlan(
        string sourceImage,
        string sourceCue,
        Spyro.Editor.Core.Levels.LevelDefinition level,
        string editsPath,
        string outputPrefix)
    {
        return new MobySourcePatchPlan(
            GeneratedAt: DateTimeOffset.Now,
            SourceImagePath: sourceImage,
            OutputImagePath: $"{outputPrefix}.bin",
            OutputCuePath: $"{outputPrefix}.cue",
            NativeEditsPath: editsPath,
            LevelKey: level.Key,
            LevelName: level.DisplayName,
            SourceTableWadOffset: level.SourceTableWadOffset,
            SourceRecordCount: level.SourceRecordCount,
            RecordStride: MobyBuildSafetyInspector.MobyRecordStride,
            PatchCount: 0,
            TotalPatchedBytes: 0,
            Patches: [],
            PackageImportPreviews: [],
            SkippedEdits: [],
            Notes: [$"Could not create the normal object patch plan. Source CUE: {Path.GetFileName(sourceCue)}"]);
    }

    private void RefreshBuildSafetySummary(MobyBuildSafetyInspectionResult inspection)
    {
        MobyBuildSafetyReport report = inspection.Report;
        _buildSafetySummaryText.Text = _releaseMode
            ? report.Levels.Count == 0
                ? "Ready: no saved edits need attention"
                : report.Status == MobyBuildSafetyStatus.Stable
                    ? $"Ready: {report.Levels.Count} edited level(s) checked"
                    : $"{report.StatusLabel}: review {report.Levels.Count} edited level(s)"
            : report.Levels.Count == 0
                ? $"{report.StatusLabel}: no saved edits requiring structural inspection"
                : $"{report.StatusLabel}: {report.Levels.Count} level(s), {report.TrueAppendCount} true append(s), {report.RuntimeSlotsConsumed} runtime slot(s) consumed, {report.SkippedEditCount} skipped edit(s)";
        _buildSafetySummaryText.Foreground = BuildSafetyBrush(report.Status);
    }

    private void InvalidateBuildSafetySummary()
    {
        if (_lastBuildSafetyInspection == null)
            return;

        _lastBuildSafetyInspection = null;
        _buildSafetySummaryText.Text = "Saved edits changed; inspect again before Create BIN";
        _buildSafetySummaryText.Foreground = new SolidColorBrush(ModernMutedInk);
    }

    private async Task<BuildSafetyDecision> ConfirmBuildSafetyAsync(MobyBuildSafetyInspectionResult inspection)
    {
        if (inspection.Report.Status == MobyBuildSafetyStatus.Stable)
            return BuildSafetyDecision.Create;
        return await ShowBuildSafetyDialogAsync(inspection, allowCreateAnyway: true);
    }

    private async Task<BuildSafetyDecision> ShowBuildSafetyDialogAsync(
        MobyBuildSafetyInspectionResult inspection,
        bool allowCreateAnyway)
    {
        Window dialog = new()
        {
            Title = "Build Safety",
            Width = 760,
            Height = 660,
            MinWidth = 620,
            MinHeight = 500,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true
        };
        dialog.Opened += (_, _) =>
        {
            dialog.Activate();
            dialog.Focus();
        };
        dialog.Content = BuildBuildSafetyDialogContent(dialog, inspection, allowCreateAnyway);
        return await dialog.ShowDialog<BuildSafetyDecision>(this);
    }

    private Control BuildBuildSafetyDialogContent(
        Window dialog,
        MobyBuildSafetyInspectionResult inspection,
        bool allowCreateAnyway)
    {
        MobyBuildSafetyReport report = inspection.Report;
        StackPanel body = new() { Spacing = 12, Margin = new Thickness(18) };
        body.Children.Add(new TextBlock
        {
            Text = $"Build Safety: {report.StatusLabel}",
            FontSize = 20,
            FontWeight = FontWeight.SemiBold,
            Foreground = BuildSafetyBrush(report.Status)
        });
        body.Children.Add(new TextBlock
        {
            Text = _releaseMode
                ? report.Levels.Count == 0
                    ? "No saved edits need attention."
                    : report.Status == MobyBuildSafetyStatus.Stable
                        ? $"{report.Levels.Count} edited level(s) checked. No problems were found."
                        : "Review the highlighted items below before creating a BIN."
                : report.Levels.Count == 0
                    ? "No saved edits require a structural Build Safety inspection."
                    : $"{report.Levels.Count} edited level(s); {report.TrueAppendCount} true source-row append(s); {report.RuntimeSlotsConsumed} projected runtime slot(s) consumed; {report.SkippedEditCount} skipped edit(s).",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(ModernMutedInk)
        });

        foreach (MobyBuildSafetyLevelReport level in report.Levels)
            body.Children.Add(BuildBuildSafetyLevelRow(dialog, level));

        if (!_releaseMode)
        {
            body.Children.Add(new TextBlock
            {
                Text = $"JSON: {inspection.Written.JsonPath}{Environment.NewLine}Markdown: {inspection.Written.MarkdownPath}",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 11,
                Foreground = new SolidColorBrush(ModernMutedInk)
            });
        }

        WrapPanel actions = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Button openFolder = NewButton("Open Report Folder", () =>
        {
            string? folder = Path.GetDirectoryName(inspection.Written.JsonPath);
            if (!string.IsNullOrWhiteSpace(folder))
                OpenPath(folder);
        });
        actions.Children.Add(openFolder);

        Button close = NewButton(report.Status == MobyBuildSafetyStatus.Blocked ? "Return to Editor" : "Close");
        close.Click += (_, _) => dialog.Close(BuildSafetyDecision.Cancel);
        actions.Children.Add(close);
        if (allowCreateAnyway && report.Status == MobyBuildSafetyStatus.Review)
        {
            Button create = NewButton("Create BIN Anyway");
            create.Background = new SolidColorBrush(ModernBlue);
            create.Foreground = Brushes.White;
            create.Click += (_, _) => dialog.Close(BuildSafetyDecision.Create);
            actions.Children.Add(create);
        }
        body.Children.Add(actions);

        return new ScrollViewer
        {
            Content = body,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
    }

    private Control BuildBuildSafetyLevelRow(Window dialog, MobyBuildSafetyLevelReport level)
    {
        StackPanel content = new() { Spacing = 6 };
        content.Children.Add(new TextBlock
        {
            Text = $"{level.LevelName}  |  {level.StatusLabel}",
            FontWeight = FontWeight.SemiBold,
            FontSize = 14,
            Foreground = BuildSafetyBrush(level.Status)
        });
        if (!_releaseMode)
        {
            content.Children.Add(new TextBlock
            {
                Text = level.ComponentByteLength > 0
                    ? $"Static rows {level.SourceRuntimeRecordCount} -> {level.PlannedRuntimeRecordCount}    Runtime capacity {level.SourceDynamicCapacity} -> {level.ProjectedDynamicCapacity}    Persistent headroom {level.PersistentIndexHeadroom}"
                    : level.Issues.Any(IsTerrainTextureBuildSafetyIssue)
                        ? "Guarded native terrain-texture structural inspection"
                        : "Native Level Moby component unresolved",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                Foreground = new SolidColorBrush(ModernInk)
            });
            content.Children.Add(new TextBlock
            {
                Text = $"True appends {level.TrueAppendCount}    Existing-slot reuse {level.SourceSlotReuseCount}    Skipped edits {level.SkippedEditCount}    Row alignment {level.RuntimeRowAlignmentDeltaBytes:+#;-#;0} bytes",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 11,
                Foreground = new SolidColorBrush(ModernMutedInk)
            });
        }
        else
        {
            content.Children.Add(new TextBlock
            {
                Text = level.Issues.Count == 0
                    ? "All saved edits in this level passed the available safety checks."
                    : "Review the items below before creating a BIN.",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                Foreground = new SolidColorBrush(ModernInk)
            });
        }
        if (level.Issues.Count > 0)
        {
            content.Children.Add(new TextBlock
            {
                Text = level.Issues.Any(issue =>
                        issue.CanNavigate && IsTerrainTextureBuildSafetyIssue(issue))
                    ? "Issues — double-click an object or terrain section to locate it"
                    : level.Issues.Any(issue => issue.CanNavigate)
                        ? "Issues — double-click an object to locate it"
                    : "Issues",
                Margin = new Thickness(0, 5, 0, 1),
                FontWeight = FontWeight.SemiBold,
                FontSize = 12,
                Foreground = new SolidColorBrush(ModernInk)
            });
            foreach (MobyBuildSafetyIssue issue in level.Issues)
                content.Children.Add(BuildBuildSafetyIssueRow(dialog, issue));
        }

        if (!_releaseMode)
        {
            content.Children.Add(new TextBlock
            {
                Text = "Technical findings",
                Margin = new Thickness(0, 5, 0, 0),
                FontWeight = FontWeight.SemiBold,
                FontSize = 11,
                Foreground = new SolidColorBrush(ModernMutedInk)
            });
            foreach (string finding in level.Findings)
            {
                content.Children.Add(new TextBlock
                {
                    Text = $"- {finding}",
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 11,
                    Foreground = new SolidColorBrush(ModernMutedInk)
                });
            }
        }

        return new Border
        {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(ModernLine),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(11),
            Child = content
        };
    }

    private Control BuildBuildSafetyIssueRow(Window dialog, MobyBuildSafetyIssue issue)
    {
        TextBlock target = new()
        {
            Text = BuildSafetyIssueTargetLabel(issue),
            FontWeight = FontWeight.SemiBold,
            FontSize = 12,
            Foreground = BuildSafetyBrush(issue.Status),
            TextWrapping = TextWrapping.Wrap
        };
        TextBlock message = new()
        {
            Text = BuildSafetyIssueMessage(issue),
            FontSize = 11,
            Foreground = new SolidColorBrush(ModernInk),
            TextWrapping = TextWrapping.Wrap
        };
        StackPanel text = new() { Spacing = 2 };
        text.Children.Add(target);
        text.Children.Add(message);
        if (issue.CanNavigate)
        {
            text.Children.Add(new TextBlock
            {
                Text = IsTerrainTextureBuildSafetyIssue(issue)
                    ? "Double-click to select and center this terrain section"
                    : "Double-click to select and center this object",
                FontSize = 10,
                Foreground = new SolidColorBrush(ModernTeal)
            });
        }

        Border row = new()
        {
            DataContext = issue,
            Background = new SolidColorBrush(issue.Status == MobyBuildSafetyStatus.Blocked
                ? Color.FromRgb(255, 244, 244)
                : Color.FromRgb(255, 249, 238)),
            BorderBrush = new SolidColorBrush(issue.Status == MobyBuildSafetyStatus.Blocked
                ? Color.FromRgb(231, 184, 184)
                : Color.FromRgb(232, 207, 164)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 6),
            Cursor = issue.CanNavigate ? new Cursor(StandardCursorType.Hand) : null,
            Child = text
        };
        row.Classes.Add("build-safety-issue");
        if (issue.CanNavigate)
        {
            row.DoubleTapped += (_, args) =>
            {
                args.Handled = true;
                ActivateBuildSafetyIssue(dialog, issue);
            };
        }

        return row;
    }

    private void ActivateBuildSafetyIssue(Window dialog, MobyBuildSafetyIssue issue)
    {
        dialog.Close(BuildSafetyDecision.Cancel);
        Dispatcher.UIThread.Post(() => _ = NavigateToBuildSafetyIssueAsync(issue));
    }

    private async Task NavigateToBuildSafetyIssueAsync(MobyBuildSafetyIssue issue)
    {
        if (!issue.CanNavigate || issue.EditorTrueIndex is not int trueIndex)
            return;

        try
        {
            LevelDefinition? level = _catalog.FindByKey(issue.LevelKey);
            if (level == null)
            {
                _statusText.Text = $"Could not find level {issue.LevelKey} for {issue.TargetLabel}.";
                return;
            }

            if (!string.Equals(_currentLevel?.Key, level.Key, StringComparison.OrdinalIgnoreCase))
            {
                await SelectLevelFromPickerAsync(level);
                if (!string.Equals(_currentLevel?.Key, level.Key, StringComparison.OrdinalIgnoreCase))
                {
                    _statusText.Text = $"Build Safety navigation to {level.DisplayName} was canceled.";
                    return;
                }
            }

            if (IsTerrainTextureBuildSafetyIssue(issue))
            {
                if (_currentGeometry == null)
                {
                    _statusText.Text =
                        $"Loaded {level.DisplayName}, but its terrain was unavailable for {BuildSafetyIssueTargetLabel(issue)}.";
                    return;
                }

                int terrainIndex = _currentGeometry.Polygons.FindIndex(face =>
                    string.Equals(
                        face.RuntimeKey,
                        issue.MobyLabel,
                        StringComparison.OrdinalIgnoreCase));
                if (terrainIndex < 0 &&
                    trueIndex >= 0 &&
                    trueIndex < _currentGeometry.Polygons.Count)
                {
                    terrainIndex = trueIndex;
                }
                if (terrainIndex < 0 || terrainIndex >= _currentGeometry.Polygons.Count)
                {
                    _statusText.Text =
                        $"Loaded {level.DisplayName}, but terrain section {issue.MobyLabel} was not found.";
                    return;
                }

                TerrainPolygon terrain = _currentGeometry.Polygons[terrainIndex];
                if (terrain.IsTerrainRemoved)
                {
                    _statusText.Text =
                        $"Loaded {level.DisplayName}, but terrain section {terrain.RuntimeKey} is removed and cannot be centered.";
                    return;
                }

                ActivateEditorShellWorkspace(
                    EditorShellWorkspace.LevelBuildingEditor,
                    _modernTerrainWorkspaceTab,
                    announce: false);
                _viewport.FocusTerrain(terrainIndex);
                _statusText.Text =
                    $"Build Safety: selected and centered {level.DisplayName} terrain section {terrain.RuntimeKey}.";
                return;
            }

            Moby? moby = _currentMobys.FirstOrDefault(candidate =>
                candidate.TrueIndex == trueIndex && !candidate.IsEditorControl);
            if (moby == null)
            {
                _statusText.Text = $"Loaded {level.DisplayName}, but {issue.TargetLabel} was not found.";
                return;
            }
            if (moby.IsRemoved)
            {
                _statusText.Text = $"Loaded {level.DisplayName}, but {issue.TargetLabel} is currently removed and cannot be centered.";
                return;
            }

            ActivateEditorShellWorkspace(
                EditorShellWorkspace.ObjectManager,
                _modernObjectWorkspaceTab,
                announce: false);
            _mobySearchBox.Text = "";
            SelectMobyCategory("All objects");
            RefreshMobyList(moby);
            _viewport.SelectMoby(moby, true);
            _mobyList.ScrollIntoView(moby);
            _mobyList.Focus();
            _statusText.Text = $"Build Safety: selected and centered {level.DisplayName} T{moby.TrueIndex} {moby.DisplayLabel}.";
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException)
        {
            EditorDiagnostics.RecordException("navigating from Build Safety", ex);
            _statusText.Text = $"Could not locate {issue.TargetLabel}: {ex.Message}";
        }
    }

    private static IBrush BuildSafetyBrush(MobyBuildSafetyStatus status) => status switch
    {
        MobyBuildSafetyStatus.Stable => new SolidColorBrush(ModernGreen),
        MobyBuildSafetyStatus.Review => new SolidColorBrush(Color.FromRgb(173, 99, 15)),
        MobyBuildSafetyStatus.Blocked => new SolidColorBrush(ModernRed),
        _ => new SolidColorBrush(ModernInk)
    };

    private sealed record MobyBuildSafetyInspectionResult(
        MobyBuildSafetyReport Report,
        MobyBuildSafetyWriteResult Written);

    private enum BuildSafetyDecision
    {
        Cancel,
        Create
    }
}
