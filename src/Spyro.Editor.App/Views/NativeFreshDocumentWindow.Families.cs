using System.Globalization;
using Avalonia.Controls;
using Avalonia.Media;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.App.Views;

internal sealed partial class NativeFreshDocumentWindow
{
    private const string FamilyDefaultMode = "Source-relative default";
    private const string FamilyExplicitMode = "Explicit WORLD XYZ";
    private readonly record struct FamilyPointFields(string? X, string? Y, string? Z);
    private readonly record struct FamilyRewardFields(string? Mode, string? X, string? Y, string? Z);
    private readonly record struct FamilyFields(FamilyPointFields Key, FamilyPointFields Chest,
        FamilyRewardFields Reward1, FamilyRewardFields Reward2, FamilyRewardFields Reward3,
        FamilyRewardFields Reward4, FamilyRewardFields Reward5);
    private FamilyFields _appliedFamilyFields;
    private bool _addingFamily;
    private bool HasPendingPlacementAdd => _addingWizard || _pendingNativeMobyAdd != null || _addingFamily;
    private readonly Dictionary<string, TextBox> _familyCoordinates = new(StringComparer.Ordinal);
    private readonly List<ComboBox> _familyRewardModes = [];
    private readonly StackPanel _familyEditor = new() { Name = "FreshFamilyFields", Spacing = 8, IsVisible = false };
    private readonly TextBlock _familyPreviewNotice = new() { Name = "FreshFamilyPreviewNotice", TextWrapping = TextWrapping.Wrap, FontSize = 11 };
    private Button _addFamily = null!;
    private Button _cancelAddFamily = null!;

    private string FamilyCapabilityText => NativeFreshSavedLevelResearchEligibility.IsSupportedLockedChestProfile(CurrentAggregateIntent)
        ? "Schema-5 family authoring. Research supports this exact saved Wizard + Grass + locked-chest R03 profile after Apply and Save and actual original BIN/CUE validation. Three authored entries produce nine native rows. Normal Create BIN is unavailable; arbitrary family layouts and persistent save ownership are not certified."
        : "Schema-5 family authoring is editable and saved, including tiled content. This layout is authored-only: research export supports only the exact legacy-floor Wizard + Grass + locked-chest R03 profile. Normal Create BIN is unavailable. Default rewards are resolved during source compilation; schematic markers do not prove native dependency, collision or gameplay support.";

    private Control BuildFamilyFields()
    {
        _addFamily = Button("FreshFamilyAdd", "Add locked-chest family", BeginFamilyAdd);
        _cancelAddFamily = Button("FreshFamilyCancelAdd", "Cancel new family", CancelFamilyAdd);
        _familyEditor.Children.Add(Section("Locked-chest family · seven members",
            "One selectable entry owns one key, one chest and five named reward members. WORLD XYZ 0–262128, no snapping. XY anchors must be inside the authored surface; explicit reward destinations may be outside it. Moving anchors never moves explicit WORLD destinations."));
        _familyEditor.Children.Add(new TextBlock
        {
            Name = "FreshFamilySourceIdentity", FontSize = 11, TextWrapping = TextWrapping.Wrap,
            Text = "Read-only definition: " + NativeFreshLockedChestFamilyReference.SupportedDefinitionId +
                " · " + RetailMaterialVariant + " · source availability not inferred"
        });
        _familyEditor.Children.Add(FamilyPointRow("Key", "Key"));
        _familyEditor.Children.Add(FamilyPointRow("Chest", "Chest"));
        for (int role = 1; role <= 5; role++)
        {
            int index = role - 1;
            var mode = new ReadOnlySelectionComboBox
            {
                Name = $"FreshFamilyReward{role}Mode", ItemsSource = new[] { FamilyDefaultMode, FamilyExplicitMode },
                SelectedItem = FamilyDefaultMode
            };
            _familyRewardModes.Add(mode);
            _familyEditor.Children.Add(Section($"reward.{role}", "Default: native default—resolved during source compilation. Explicit XYZ is a fixed WORLD destination, not an offset or a trajectory."));
            _familyEditor.Children.Add(mode);
            _familyEditor.Children.Add(FamilyPointRow("Reward" + role, "Destination"));
            mode.SelectionChanged += (_, _) =>
            {
                if (_refreshing) return;
                // Clearing is an explicit editable mode choice, never an async
                // refresh or a disabled-field mutation that could discard a draft.
                if (mode.IsEffectivelyEnabled && mode.SelectedItem is FamilyDefaultMode)
                {
                    bool previous = _refreshing; _refreshing = true;
                    try { SetFamilyPoint("Reward" + (index + 1), null, null, null); }
                    finally { _refreshing = previous; }
                }
                FieldsChanged();
            };
        }
        _familyEditor.Children.Add(_familyPreviewNotice);
        _familyEditor.Children.Add(_cancelAddFamily);
        return new StackPanel { Spacing = 8, Children = { _addFamily, _familyEditor } };
    }

    private Control FamilyPointRow(string member, string label) => Row(
        FamilyField(member + "X", label + " X"), FamilyField(member + "Y", "Y"), FamilyField(member + "Z", "Z"));
    private Control FamilyField(string key, string label)
    {
        var field = new TextBox { Name = "FreshFamily" + key, MaxLength = 16, MinHeight = 32 };
        _familyCoordinates.Add(key, field); field.TextChanging += (_, _) => FieldsChanged();
        return new StackPanel { Spacing = 4, Children = { new TextBlock { Text = label, FontSize = 12 }, field } };
    }

    private void BeginFamilyAdd()
    {
        if (!_aggregateMode || AggregateCommandsBlocked || CurrentAggregateIntent == null || HasUnappliedFields ||
            CurrentAggregateIntent.Placements.Count >= NativeFreshLevelDocumentIntent.MaximumPlacements) return;
        _addingFamily = true; WriteFields();
        _status.Text = "New family is pending. Enter independent key and chest WORLD anchors; choose defaults or explicit reward destinations. No ID, schema upgrade or supporting Moby is installed until valid Apply. Cancel restores the applied form.";
    }
    private void CancelFamilyAdd()
    {
        if (!_aggregateMode || AggregateCommandsBlocked || !_addingFamily) return;
        _addingFamily = false; WriteFields();
        _status.Text = "Canceled the pending family. No authored entry, editor ID or schema upgrade was installed.";
    }

    private void ApplyPendingFamily()
    {
        RequireNativeMobyFieldsUnchanged(); RequireWizardFieldsUnchanged();
        if (FieldSignature() != _appliedFields || MaterialFieldSignature() != _appliedMaterialFields)
            throw new InvalidDataException("Base and material fields are locked while adding a family. Cancel the new family before editing them.");
        var source = NativeFreshLockedChestFamilyReference.Create(RetailMaterialVariant,
            NativeFreshLockedChestFamilyReference.SupportedDefinitionId);
        _aggregateSession!.AddLockedChestFamily(source, FamilyNumber("KeyX"), FamilyNumber("KeyY"), FamilyNumber("KeyZ"),
            FamilyNumber("ChestX"), FamilyNumber("ChestY"), FamilyNumber("ChestZ"), ReadFamilyRewards());
        _addingFamily = false;
    }

    private NativeFreshLockedChestFamilyPlacementIntent ReadFamily(NativeFreshLockedChestFamilyPlacementIntent selected)
    {
        RequireNativeMobyFieldsUnchanged(); RequireWizardFieldsUnchanged();
        return NativeFreshLockedChestFamilyPlacementIntent.Create(selected.EditorId, selected.Source,
            FamilyNumber("KeyX"), FamilyNumber("KeyY"), FamilyNumber("KeyZ"),
            FamilyNumber("ChestX"), FamilyNumber("ChestY"), FamilyNumber("ChestZ"), ReadFamilyRewards());
    }

    private NativeFreshChestRewardDestinationOverrides? ReadFamilyRewards()
    {
        var values = new List<NativeFreshChestRewardDestinationOverride>();
        for (int i = 0; i < 5; i++)
        {
            string member = "Reward" + (i + 1);
            switch (_familyRewardModes[i].SelectedItem)
            {
                case FamilyDefaultMode:
                    var point = FamilyPointSignature(member);
                    if (!string.IsNullOrEmpty(point.X) || !string.IsNullOrEmpty(point.Y) || !string.IsNullOrEmpty(point.Z))
                        throw new InvalidDataException($"reward.{i + 1} uses a default but inactive raw coordinates changed. Undo those fields or explicitly choose WORLD XYZ; no raw values were discarded.");
                    break;
                case FamilyExplicitMode:
                    values.Add(new((NativeFreshChestRewardRole)i, FamilyNumber(member + "X"),
                        FamilyNumber(member + "Y"), FamilyNumber(member + "Z")));
                    break;
                default: throw new InvalidDataException($"reward.{i + 1} requires an explicit default/WORLD mode choice.");
            }
        }
        return values.Count == 0 ? null : NativeFreshChestRewardDestinationOverrides.Create(values);
    }

    private void WriteFamilyFields()
    {
        // Other pending placement types do not own or reset this inactive form.
        var selected = _addingFamily ? null : _placements.SelectedItem as NativeFreshLockedChestFamilyPlacementIntent;
        SetFamilyPoint("Key", selected?.KeyX, selected?.KeyY, selected?.KeyZ);
        SetFamilyPoint("Chest", selected?.ChestX, selected?.ChestY, selected?.ChestZ);
        for (int i = 0; i < 5; i++)
        {
            var value = selected?.RewardDestinations?.Values.SingleOrDefault(v => (int)v.Role == i);
            _familyRewardModes[i].SelectedItem = value == null ? FamilyDefaultMode : FamilyExplicitMode;
            SetFamilyPoint("Reward" + (i + 1), value?.X, value?.Y, value?.Z);
        }
        _appliedFamilyFields = FamilyFieldSignature();
    }
    private void RefreshFamilyChrome(bool canEditRows, NativeFreshPlacementIntent? selected)
    {
        bool active = !_addingWizard && _pendingNativeMobyAdd == null && (_addingFamily || selected is NativeFreshLockedChestFamilyPlacementIntent);
        _familyEditor.IsVisible = _aggregateMode && active;
        _addFamily.IsEnabled = canEditRows && !HasUnappliedFields && CurrentAggregateIntent!.Placements.Count < NativeFreshLevelDocumentIntent.MaximumPlacements;
        _cancelAddFamily.IsVisible = _addingFamily;
        _cancelAddFamily.IsEnabled = canEditRows && _addingFamily;
        foreach (var pair in _familyCoordinates)
        {
            bool explicitPoint = !pair.Key.StartsWith("Reward", StringComparison.Ordinal) ||
                _familyRewardModes[pair.Key[6] - '1'].SelectedItem is FamilyExplicitMode;
            pair.Value.IsEnabled = canEditRows && active && explicitPoint;
        }
        foreach (var mode in _familyRewardModes) mode.IsEnabled = canEditRows && active;
        _familyPreviewNotice.Text = "XY schematic only; exact Z remains numeric. Default reward locations are unknown until source compilation. Explicit WORLD points are never clamped to the floor.";
    }
    private void RequireFamilyFieldsUnchanged()
    {
        if (FamilyFieldSignature() != _appliedFamilyFields)
            throw new InvalidDataException("Inactive family fields changed. Undo those fields before editing another placement; no family raw fields were discarded.");
    }
    private void RequireWizardFieldsUnchanged()
    {
        if (RouteFieldSignature() != _appliedRouteFields)
            throw new InvalidDataException("Inactive Wizard fields changed. Undo those fields before editing another placement; types are never converted.");
    }
    private FamilyPointFields FamilyPointSignature(string name) => new(_familyCoordinates[name + "X"].Text,
        _familyCoordinates[name + "Y"].Text, _familyCoordinates[name + "Z"].Text);
    private FamilyRewardFields FamilyRewardSignature(int role)
    {
        var point = FamilyPointSignature("Reward" + role);
        return new(_familyRewardModes[role - 1].SelectedItem as string, point.X, point.Y, point.Z);
    }
    private FamilyFields FamilyFieldSignature() => new(FamilyPointSignature("Key"), FamilyPointSignature("Chest"),
        FamilyRewardSignature(1), FamilyRewardSignature(2), FamilyRewardSignature(3), FamilyRewardSignature(4), FamilyRewardSignature(5));
    private int FamilyNumber(string name) => int.TryParse(_familyCoordinates[name].Text, NumberStyles.Integer,
        CultureInfo.InvariantCulture, out int value) ? value : throw new InvalidDataException($"{name} requires an integer WORLD coordinate; no snapping was applied.");
    private void SetFamilyPoint(string name, int? x, int? y, int? z)
    {
        _familyCoordinates[name + "X"].Text = x?.ToString(CultureInfo.InvariantCulture) ?? "";
        _familyCoordinates[name + "Y"].Text = y?.ToString(CultureInfo.InvariantCulture) ?? "";
        _familyCoordinates[name + "Z"].Text = z?.ToString(CultureInfo.InvariantCulture) ?? "";
    }
}
