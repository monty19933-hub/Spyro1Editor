using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.App.Views;

public sealed partial class MainWindow
{
    private readonly TextBlock _terrainPrivateTextureCapacityText = new()
    {
        Name = "TerrainPrivateTextureCapacityText"
    };
    private string _terrainPrivateTextureCapacityProofLevelKey = "";
    private int _terrainPrivateTextureCapacityProofSourceRows = -1;
    private int _terrainPrivateTextureCapacityProofProposedRows = -1;
    private int? _terrainPrivateTextureCapacityProofFreeBytes;
    private NativeTerrainTexturePrivateImageWriterKind?
        _terrainPrivateTextureCapacityProofWriter;
    private bool? _terrainPrivateTextureCapacityProofPassed;
    private string _terrainPrivateTextureCapacityIndeterminateReason = "";

    private Control BuildTerrainPrivateTextureCapacityPanel()
    {
        Border border = new()
        {
            Background = new SolidColorBrush(Color.FromRgb(255, 250, 236)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(225, 199, 126)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10)
        };
        StackPanel content = new() { Spacing = 5 };
        content.Children.Add(new TextBlock
        {
            Text = _releaseMode ? "Single-Tile Texture Slots" : "Private Single-Texture Capacity",
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(90, 66, 18))
        });
        _terrainPrivateTextureCapacityText.TextWrapping = TextWrapping.Wrap;
        _terrainPrivateTextureCapacityText.FontSize = 12;
        _terrainPrivateTextureCapacityText.LineHeight = 17;
        _terrainPrivateTextureCapacityText.Foreground =
            new SolidColorBrush(Color.FromRgb(77, 65, 39));
        content.Children.Add(_terrainPrivateTextureCapacityText);
        border.Child = content;
        RefreshTerrainPrivateTextureCapacityUi();
        return border;
    }

    private void RecordTerrainPrivateTextureCapacityPass(
        NativeTerrainTextureRecordAppendSourceBinding sourceBinding,
        NativeTerrainTexturePrivateImageWriterKind writerKind,
        int proposedRows,
        int? freeBytes = null)
    {
        ArgumentNullException.ThrowIfNull(sourceBinding);
        _terrainPrivateTextureCapacityProofLevelKey =
            LevelCatalog.NormalizeKey(sourceBinding.TargetLevelKey);
        _terrainPrivateTextureCapacityProofSourceRows =
            sourceBinding.ExpectedSourceTextureCount;
        _terrainPrivateTextureCapacityProofProposedRows =
            Math.Max(0, proposedRows);
        _terrainPrivateTextureCapacityProofWriter = writerKind;
        _terrainPrivateTextureCapacityProofFreeBytes = freeBytes;
        _terrainPrivateTextureCapacityProofPassed = true;
        _terrainPrivateTextureCapacityIndeterminateReason = "";
        RefreshTerrainPrivateTextureCapacityUi();
    }

    private void RecordTerrainPrivateTextureCapacityRejection(
        string levelKey,
        int proposedRows)
    {
        _terrainPrivateTextureCapacityProofLevelKey =
            LevelCatalog.NormalizeKey(levelKey);
        _terrainPrivateTextureCapacityProofProposedRows =
            Math.Max(0, proposedRows);
        _terrainPrivateTextureCapacityProofFreeBytes = null;
        _terrainPrivateTextureCapacityProofWriter = null;
        _terrainPrivateTextureCapacityProofPassed = false;
        _terrainPrivateTextureCapacityIndeterminateReason = "";
        RefreshTerrainPrivateTextureCapacityUi();
    }

    private void RecordTerrainPrivateTextureCapacityIndeterminate(
        string levelKey,
        int proposedRows,
        string reason)
    {
        _terrainPrivateTextureCapacityProofLevelKey =
            LevelCatalog.NormalizeKey(levelKey);
        _terrainPrivateTextureCapacityProofSourceRows = -1;
        _terrainPrivateTextureCapacityProofProposedRows =
            Math.Max(0, proposedRows);
        _terrainPrivateTextureCapacityProofFreeBytes = null;
        _terrainPrivateTextureCapacityProofWriter = null;
        _terrainPrivateTextureCapacityProofPassed = null;
        _terrainPrivateTextureCapacityIndeterminateReason =
            string.IsNullOrWhiteSpace(reason)
                ? "The latest exact pack check did not finish."
                : reason.Trim();
        RefreshTerrainPrivateTextureCapacityUi();
    }

    private void ClearTerrainPrivateTextureCapacityProof()
    {
        _terrainPrivateTextureCapacityProofLevelKey = "";
        _terrainPrivateTextureCapacityProofSourceRows = -1;
        _terrainPrivateTextureCapacityProofProposedRows = -1;
        _terrainPrivateTextureCapacityProofFreeBytes = null;
        _terrainPrivateTextureCapacityProofWriter = null;
        _terrainPrivateTextureCapacityProofPassed = null;
        _terrainPrivateTextureCapacityIndeterminateReason = "";
        RefreshTerrainPrivateTextureCapacityUi();
    }

    private void RefreshTerrainPrivateTextureCapacityUi()
    {
        string currentLevelKey = LevelCatalog.NormalizeKey(_currentLevel?.Key ?? "");
        int stagedRows = _nativeTerrainTextureRelocations
            .Where(edit => edit.UsesAppendedPrivateRecord)
            .Select(edit => edit.TargetTextureId)
            .Distinct()
            .Count();
        NativePrivateTextureSlotCapacity nativeSlots =
            InspectNativePrivateTextureSlotCapacity();
        if (_releaseMode && !AppendedPrivateTerrainTextureResearchGate.IsEnabled)
        {
            AppendedPrivateTerrainTexturePromotionProfile? releaseProfile =
                AppendedPrivateTerrainTexturePromotionProfileRegistry.Profiles
                    .FirstOrDefault(profile =>
                        profile.RuntimeProven &&
                        string.Equals(
                            profile.NormalizedTargetLevelKey,
                            currentLevelKey,
                            StringComparison.OrdinalIgnoreCase));
            int appendedCapacity =
                releaseProfile?.RuntimeProvenMaxAppendedRecords ?? 0;
            int appendedRemaining =
                Math.Max(0, appendedCapacity - stagedRows);
            int capacity = nativeSlots.Total + appendedCapacity;
            int remaining = nativeSlots.Remaining + appendedRemaining;
            string latestCheck = "";
            bool checkTargetsCurrentLevel = string.Equals(
                currentLevelKey,
                _terrainPrivateTextureCapacityProofLevelKey,
                StringComparison.OrdinalIgnoreCase);
            if (checkTargetsCurrentLevel && _terrainPrivateTextureCapacityProofPassed == false)
                latestCheck = "\nThe last attempted tile could not be added safely, so no change was made.";
            else if (checkTargetsCurrentLevel &&
                     !string.IsNullOrWhiteSpace(_terrainPrivateTextureCapacityIndeterminateReason))
                latestCheck = "\nThe last tile check did not finish. Try it again before creating a BIN.";

            string capacityBreakdown =
                $"{nativeSlots.Remaining:N0} of {nativeSlots.Total:N0} proven native slot(s) remain" +
                (appendedCapacity > 0
                    ? $"; {appendedRemaining:N0} of {appendedCapacity:N0} runtime-proven appended slot(s) remain"
                    : "");
            string nativeProofNote = nativeSlots.ProofAvailable
                ? ""
                : $"\nNative-slot count is unavailable: {nativeSlots.Note}";

            _terrainPrivateTextureCapacityText.Text = capacity == 0
                ? "Single-tile cross-level painting is not available for this level yet. " +
                  "You can still replace the matching linked sections together when the editor offers that choice. " +
                  capacityBreakdown + "." +
                  nativeProofNote + latestCheck
                : $"{remaining:N0} of {capacity:N0} single-tile cross-level slot(s) remain in this level. " +
                  $"{capacityBreakdown}. " +
                  "Reusing a texture you already added does not consume another slot." +
                  nativeProofNote + latestCheck;
            return;
        }

        int[] savedSourceCounts = _nativeTerrainTextureRelocations
            .Where(edit =>
                edit.UsesAppendedPrivateRecord &&
                edit.SourceTextureRecordCount is > 0 and <= 128)
            .Select(edit => edit.SourceTextureRecordCount)
            .Distinct()
            .ToArray();
        int sourceRows = savedSourceCounts.Length == 1
            ? savedSourceCounts[0]
            : string.Equals(
                currentLevelKey,
                _terrainPrivateTextureCapacityProofLevelKey,
                StringComparison.OrdinalIgnoreCase)
                ? _terrainPrivateTextureCapacityProofSourceRows
                : -1;
        string idHeadroom = sourceRows > 0
            ? $"{Math.Max(0, NativeTerrainTextureRecordAppendBuilder.MaximumTextureRecordCount - sourceRows - stagedRows):N0} seven-bit texture ID(s) remain after the staged rows."
            : "Native ID headroom will be shown after the first exact source-bound check.";

        string exactPackStatus = "Exact page-pack fit has not been checked for this staged set yet.";
        bool proofTargetsCurrentLevel = string.Equals(
            currentLevelKey,
            _terrainPrivateTextureCapacityProofLevelKey,
            StringComparison.OrdinalIgnoreCase);
        if (proofTargetsCurrentLevel &&
            _terrainPrivateTextureCapacityProofPassed.HasValue)
        {
            int proposedRows = Math.Max(0, _terrainPrivateTextureCapacityProofProposedRows);
            string setName = proposedRows == stagedRows
                ? "staged set"
                : $"proposed {proposedRows:N0}-row set";
            if (_terrainPrivateTextureCapacityProofPassed.Value)
            {
                string writer = _terrainPrivateTextureCapacityProofWriter?.ToString() ?? "structural";
                string free = _terrainPrivateTextureCapacityProofFreeBytes.HasValue
                    ? $"; {_terrainPrivateTextureCapacityProofFreeBytes.Value:N0} native texture-page byte(s) remain"
                    : "";
                exactPackStatus = $"Exact pack: PASS for the {setName} with {writer}{free}.";
            }
            else
            {
                exactPackStatus =
                    $"Exact pack: REJECTED for the {setName}; no edit was staged. See the status message for the specific ownership/capacity conflict.";
            }
        }
        else if (proofTargetsCurrentLevel &&
            !string.IsNullOrWhiteSpace(_terrainPrivateTextureCapacityIndeterminateReason))
        {
            int proposedRows = Math.Max(0, _terrainPrivateTextureCapacityProofProposedRows);
            string setName = proposedRows == stagedRows
                ? "staged set"
                : $"proposed {proposedRows:N0}-row set";
            exactPackStatus =
                $"Exact pack: INDETERMINATE for the {setName}; no edit was staged. " +
                $"{_terrainPrivateTextureCapacityIndeterminateReason}";
        }

        if (AppendedPrivateTerrainTextureResearchGate.IsEnabled)
        {
            _terrainPrivateTextureCapacityText.Text =
                $"Staged unique private rows: {stagedRows:N0}. Research maximum: " +
                $"{AppendedPrivateTerrainTextureResearchGate.ResearchMaximumAppendedRecords:N0}. " +
                $"Proven native slots: {nativeSlots.Remaining:N0} of {nativeSlots.Total:N0} remain. " +
                $"{idHeadroom}\n{exactPackStatus}\n" +
                "The research maximum is not guaranteed physical capacity or normal-release authorization. " +
                "A compatible donor/material row can still be reused at the maximum without adding a row, " +
                "and Create BIN must pass the exact PS1 page packer and readback.";
            return;
        }

        AppendedPrivateTerrainTexturePromotionProfile? normalProfile =
            AppendedPrivateTerrainTexturePromotionProfileRegistry.Profiles
                .FirstOrDefault(profile =>
                    profile.RuntimeProven &&
                    string.Equals(
                        profile.NormalizedTargetLevelKey,
                        currentLevelKey,
                        StringComparison.OrdinalIgnoreCase));
        int provenCapacity =
            normalProfile?.RuntimeProvenMaxAppendedRecords ?? 0;
        string availability = normalProfile == null
            ? "No appended private row is runtime-proven for normal Create BIN in this level yet."
            : $"Runtime-proven normal-release capacity: {provenCapacity:N0} private row(s) for this exact retail profile.";
        _terrainPrivateTextureCapacityText.Text =
            $"Staged unique appended rows: {stagedRows:N0}. " +
            $"Proven native slots: {nativeSlots.Remaining:N0} of {nativeSlots.Total:N0} remain. " +
            $"{availability} " +
            $"{idHeadroom}\n{exactPackStatus}\n" +
            "Create BIN rechecks the exact disc/profile match, PS1 page packer, " +
            "and final readback. Research-only capacity is not advertised in the public editor.";
    }

    private NativePrivateTextureSlotCapacity InspectNativePrivateTextureSlotCapacity()
    {
        if (_currentLevel == null || _currentGeometry == null)
        {
            return new NativePrivateTextureSlotCapacity(
                0,
                0,
                false,
                "No level terrain is loaded yet.");
        }

        TerrainTextureCatalog catalog = BuildNativeTerrainTextureCatalog(
            _currentLevel,
            _currentGeometry,
            out string catalogError);
        int[] provenNativeTextureIds = catalog.Entries
            .Where(entry =>
                entry.Readiness.IsNativeUnreferencedStatic &&
                entry.Readiness.SupportsBothDescriptorTiers &&
                entry.Readiness.TargetRuntime.CanPersist)
            .Select(entry => entry.TextureId)
            .Distinct()
            .OrderBy(textureId => textureId)
            .ToArray();

        HashSet<int> occupiedTextureIds = _currentGeometry.Polygons
            .Where(face => !face.IsTerrainRemoved && face.TextureId >= 0)
            .Select(face => face.TextureId)
            .ToHashSet();
        foreach (CustomTerrainTextureImport import in _customTerrainTextures)
            occupiedTextureIds.Add(import.TextureId);
        foreach (NativeTerrainTextureRelocationEdit relocation in _nativeTerrainTextureRelocations)
            occupiedTextureIds.Add(relocation.TargetTextureId);

        int remaining = provenNativeTextureIds.Count(textureId =>
            !occupiedTextureIds.Contains(textureId));
        bool proofAvailable = string.IsNullOrWhiteSpace(catalogError) ||
            provenNativeTextureIds.Length > 0;
        return new NativePrivateTextureSlotCapacity(
            provenNativeTextureIds.Length,
            remaining,
            proofAvailable,
            proofAvailable
                ? "The source-bound native texture catalog and runtime-control audit completed."
                : catalogError);
    }

    private readonly record struct NativePrivateTextureSlotCapacity(
        int Total,
        int Remaining,
        bool ProofAvailable,
        string Note);
}
