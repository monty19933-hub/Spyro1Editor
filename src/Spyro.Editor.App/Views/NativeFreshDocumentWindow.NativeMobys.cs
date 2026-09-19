using System.Globalization;
using Avalonia.Controls;
using Avalonia.Media;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.App.Views;

internal sealed partial class NativeFreshDocumentWindow
{
    private string NativeMobyCapabilityText => CurrentAggregateIntent?.TiledContent != null ? TiledCapabilityText
        : NativeFreshSavedLevelResearchEligibility.IsSupportedFamilyProfile(CurrentAggregateIntent)
        ? "Research supports the exact saved Wizard + Grass profile after explicit Apply and Save, subject to actual original BIN/CUE validation. Normal Create BIN is unavailable. This bounded profile does not certify other native imports or general gameplay."
        : "Schema-3 native Moby authoring is authored-only; native dependencies are unsupported by the current research action. Normal Create BIN and research export are unavailable. Source references and XY markers do not certify imported models, collision or gameplay.";
    private readonly StackPanel _nativeMobyEditor = new() { Name = "FreshNativeMobyFields", Spacing = 8, IsVisible = false };
    private readonly TextBlock _nativeMobySourceIdentity = new()
    {
        Name = "FreshNativeMobySourceIdentity", TextWrapping = TextWrapping.Wrap, FontSize = 11
    };
    private readonly Dictionary<string, TextBox> _nativeMobyFields = new(StringComparer.Ordinal);
    private readonly record struct NativeMobyFields(string? X, string? Y, string? Z, string? NativeYaw);
    private NativeMobyFields _appliedNativeMobyFields;

    private Control BuildNativeMobyFields()
    {
        _nativeMobyEditor.Children.Add(Section("Native Moby placement",
            "Authored raw native coordinates. XYZ are not display units; XY is shown at floor height in the schematic. Native yaw is not the player heading. No model or dependency is imported by editing these fields."));
        _nativeMobyEditor.Children.Add(_nativeMobySourceIdentity);
        _nativeMobyEditor.Children.Add(Row(NativeMobyField("FreshNativeMobyX", "X"), NativeMobyField("FreshNativeMobyY", "Y"), NativeMobyField("FreshNativeMobyZ", "Z 0–65535")));
        _nativeMobyEditor.Children.Add(NativeMobyField("FreshNativeMobyYaw", "Native yaw byte 0–255"));
        _nativeMobyEditor.Children.Add(_cancelNativeMobyAdd);
        return _nativeMobyEditor;
    }

    private Control NativeMobyField(string name, string label)
    {
        var box = new TextBox { Name = name, MaxLength = 16, MinHeight = 32 };
        _nativeMobyFields.Add(name, box);
        box.TextChanged += (_, _) => FieldsChanged();
        return new StackPanel { Spacing = 4, Children = { new TextBlock { Text = label, FontSize = 12 }, box } };
    }

    private void WriteNativeMobyFields()
    {
        var selected = _addingWizard || _pendingNativeMobyAdd != null ? null : _placements.SelectedItem as NativeFreshMobyPlacementIntent;
        SetNativeMoby("FreshNativeMobyX", selected?.X); SetNativeMoby("FreshNativeMobyY", selected?.Y);
        SetNativeMoby("FreshNativeMobyZ", selected?.Z); SetNativeMoby("FreshNativeMobyYaw", selected?.NativeYawByte);
        var source = _pendingNativeMobyAdd?.Source ?? selected?.Source;
        _nativeMobySourceIdentity.Text = source == null ? "No native Moby selected." :
            $"Read-only source intent · {source.SourceVariant} · WAD {source.SourceWadEntry} · true index {source.SourceTrueIndex} · expected class {source.ExpectedNativeClass}";
        _appliedNativeMobyFields = NativeMobyFieldSignature();
    }

    private NativeFreshPlacementIntent ReadSelectedPlacement(NativeFreshPlacementIntent selected)
    {
        switch (selected)
        {
            case NativeFreshWizardRouteIntent wizard:
                RequireNativeMobyFieldsUnchanged();
                return NativeFreshWizardRouteIntent.Create(wizard.EditorId,
                    RouteNumber("FreshWizardOriginX"), RouteNumber("FreshWizardOriginY"), RouteNumber("FreshWizardOriginZ"),
                    RouteNumber("FreshWizardEndX"), RouteNumber("FreshWizardEndY"));
            case NativeFreshMobyPlacementIntent moby:
                if (RouteFieldSignature() != _appliedRouteFields)
                    throw new InvalidDataException("Inactive Wizard fields changed. Undo those fields before editing the native Moby; placement types are never converted.");
                // Retain the actual immutable source tuple. The form does not
                // edit source identity or substitute a Wizard/retail instance.
                return NativeFreshMobyPlacementIntent.Create(moby.EditorId, moby.Source,
                    NativeMobyNumber("FreshNativeMobyX"), NativeMobyNumber("FreshNativeMobyY"),
                    NativeMobyNumber("FreshNativeMobyZ"), NativeMobyNumber("FreshNativeMobyYaw"));
            default:
                throw new InvalidDataException("Unsupported selected placement type; no conversion or partial Apply was performed.");
        }
    }

    private void RequireNativeMobyFieldsUnchanged()
    {
        if (NativeMobyFieldSignature() != _appliedNativeMobyFields)
            throw new InvalidDataException("Inactive native Moby fields changed. Undo those fields before editing another placement; no raw fields were discarded.");
    }

    private void RefreshNativeMobyChrome(bool canEditRows, NativeFreshPlacementIntent? selected)
    {
        bool native = !_addingWizard && (_pendingNativeMobyAdd != null || selected is NativeFreshMobyPlacementIntent);
        _nativeMobyEditor.IsVisible = _aggregateMode && native;
        foreach (TextBox field in _nativeMobyFields.Values) field.IsEnabled = canEditRows && native;
    }

    private NativeMobyFields NativeMobyFieldSignature() => new(_nativeMobyFields["FreshNativeMobyX"].Text,
        _nativeMobyFields["FreshNativeMobyY"].Text, _nativeMobyFields["FreshNativeMobyZ"].Text, _nativeMobyFields["FreshNativeMobyYaw"].Text);
    private int NativeMobyNumber(string name) => int.TryParse(_nativeMobyFields[name].Text,
        NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value :
        throw new InvalidDataException($"{name} requires an integer; no snapping or wrapping was applied.");
    private void SetNativeMoby(string name, int? value) => _nativeMobyFields[name].Text = value?.ToString(CultureInfo.InvariantCulture) ?? "";
}
