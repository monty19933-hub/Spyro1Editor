using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Spyro.Editor.Core.Editing;

namespace Spyro.Editor.App.Views;

// Raw authoring fields only. No source acquisition, decoder, compiler or writer.
internal sealed partial class NativeFreshDocumentWindow
{
    private const string BaseColorMaterialLabel = "Base floor color";
    private const string RetailMaterialLabel = "Retail terrain record";
    private const string RetailMaterialVariant = "spyro1-usa-retail-mode2-2352-v1";
    private readonly StackPanel _materialFields = new() { Spacing = 8, IsVisible = false };
    private readonly MaterialSlotEditor[] _materialSlots = [new(0), new(1)];
    private Button _enableMaterials = null!;
    private bool _materialsEnabled;
    private MaterialFields _appliedMaterialFields;

    // Structured equality preserves arbitrary invalid raw text without delimiter
    // collisions. Unsupported selection objects remain distinguishable as well.
    private readonly record struct MaterialSlotFields(object? Kind, string? Variant, object? Level, string? TextureId);
    private readonly record struct MaterialFields(bool Enabled, MaterialSlotFields First, MaterialSlotFields Second);

    private sealed class MaterialSlotEditor
    {
        internal MaterialSlotEditor(int index)
        {
            string prefix = $"FreshMaterial{index}";
            Kind = new ReadOnlySelectionComboBox
            {
                Name = prefix + "Kind", ItemsSource = new[] { BaseColorMaterialLabel, RetailMaterialLabel },
                SelectedItem = BaseColorMaterialLabel, HorizontalAlignment = HorizontalAlignment.Stretch
            };
            SourceVariant = new TextBox { Name = prefix + "SourceVariant", MaxLength = 96, MinHeight = 32 };
            SourceLevel = new ReadOnlySelectionComboBox
            {
                Name = prefix + "SourceLevel", ItemsSource = Enumerable.Range(0, 35).Select(i => 10 + 2 * i).ToArray(),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                ItemTemplate = new FuncDataTemplate<int>((entry, _) => new TextBlock { Text = $"WAD {entry}" })
            };
            TextureId = new TextBox { Name = prefix + "TextureId", MaxLength = 16, MinHeight = 32 };
        }

        internal ComboBox Kind { get; }
        internal TextBox SourceVariant { get; }
        internal ComboBox SourceLevel { get; }
        internal TextBox TextureId { get; }
        internal MaterialSlotFields Capture() => new(Kind.SelectedItem, SourceVariant.Text, SourceLevel.SelectedItem, TextureId.Text);
    }

    private Control BuildMaterialFields()
    {
        _enableMaterials = Button("FreshMaterialsEnable", "Add material intent (schema 2)", EnableMaterialIntent);
        _materialFields.Children.Add(Section("Floor material intent",
            "Two ordered triangle slots. Base floor color follows the Base color above; retail coordinates identify source intent, not an issued texture."));
        _materialFields.Children.Add(_enableMaterials);
        for (int i = 0; i < _materialSlots.Length; i++)
        {
            MaterialSlotEditor slot = _materialSlots[i];
            slot.Kind.SelectionChanged += (_, _) => FieldsChanged();
            slot.SourceVariant.TextChanged += (_, _) => FieldsChanged();
            slot.SourceLevel.SelectionChanged += (_, _) => FieldsChanged();
            slot.TextureId.TextChanged += (_, _) => FieldsChanged();
            _materialFields.Children.Add(new TextBlock
            {
                Text = i == 0 ? "Triangle 0 · A/B/C" : "Triangle 1 · B/D/C",
                FontWeight = FontWeight.SemiBold
            });
            _materialFields.Children.Add(slot.Kind);
            _materialFields.Children.Add(MaterialLabel("Source variant", slot.SourceVariant));
            _materialFields.Children.Add(Row(MaterialLabel("Source level", slot.SourceLevel), MaterialLabel("Texture ID 0–127", slot.TextureId)));
        }
        _materialFields.Children.Add(new TextBlock
        {
            Name = "FreshMaterialAuthoringNotice",
            Text = "Material descriptors are authoring intent. Inspect sources explicitly for separate thumbnails. Clean saved two-retail/no-placement aggregates may use the research action; actual source eligibility is checked during preparation.",
            TextWrapping = TextWrapping.Wrap, FontSize = 11, Foreground = Brushes.DimGray
        });
        return _materialFields;
    }

    private static Control MaterialLabel(string label, Control control) => new StackPanel
    {
        Spacing = 4, Children = { new TextBlock { Text = label, FontSize = 12 }, control }
    };

    private void EnableMaterialIntent()
    {
        if (!_aggregateMode || CurrentAggregateIntent == null || AggregateCommandsBlocked ||
            _addingWizard || _materialsEnabled) return;

        // Explicit raw upgrade only. No applied history, source WAD or ID is
        // created until the complete form is validated and applied.
        bool previous = _refreshing;
        _refreshing = true;
        try
        {
            _materialsEnabled = true;
            foreach (MaterialSlotEditor slot in _materialSlots) WriteMaterialSlot(slot, NativeFreshFloorMaterialIntent.BaseFloorColor());
        }
        finally { _refreshing = previous; }
        FieldsChanged();
    }

    private void WriteMaterialFields()
    {
        NativeFreshFloorMaterialsIntent? pair = CurrentAggregateIntent?.FloorMaterials;
        _materialsEnabled = CurrentAggregateIntent?.SchemaVersion == 2;
        WriteMaterialSlot(_materialSlots[0], pair?.Triangle0 ?? NativeFreshFloorMaterialIntent.BaseFloorColor());
        WriteMaterialSlot(_materialSlots[1], pair?.Triangle1 ?? NativeFreshFloorMaterialIntent.BaseFloorColor());
        _appliedMaterialFields = MaterialFieldSignature();
    }

    private static void WriteMaterialSlot(MaterialSlotEditor slot, NativeFreshFloorMaterialIntent material)
    {
        if (material is NativeFreshRetailTerrainMaterialIntent retail)
        {
            slot.Kind.SelectedItem = RetailMaterialLabel;
            slot.SourceVariant.Text = retail.SourceVariant;
            slot.SourceLevel.SelectedItem = retail.SourceWadEntry;
            slot.TextureId.Text = retail.TextureId.ToString(CultureInfo.InvariantCulture);
            return;
        }
        if (material.Tag != "base-floor-color") throw new InvalidDataException("Unsupported material kind.");
        slot.Kind.SelectedItem = BaseColorMaterialLabel;
        // This label alone does not choose a retail material, WAD or texture ID.
        slot.SourceVariant.Text = RetailMaterialVariant;
        slot.SourceLevel.SelectedItem = null;
        slot.TextureId.Text = "";
    }

    private NativeFreshLevelDocumentIntent ApplyMaterialFieldsTo(NativeFreshLevelDocumentIntent candidate)
    {
        // WithContent already retains the actual validated immutable pair.
        // Only exact raw equality may skip reparsing; never normalize bad text.
        if (MaterialFieldSignature() == _appliedMaterialFields) return candidate;
        if (!_materialsEnabled)
            throw new InvalidDataException("Use the explicit material-intent action before editing material slots; no schema change was inferred.");
        NativeFreshFloorMaterialsIntent pair = NativeFreshFloorMaterialsIntent.Create(
            ReadMaterialSlot(_materialSlots[0]), ReadMaterialSlot(_materialSlots[1]));
        return candidate.WithFloorMaterials(pair);
    }

    private static NativeFreshFloorMaterialIntent ReadMaterialSlot(MaterialSlotEditor slot)
    {
        if (slot.Kind.SelectedItem is not string kind)
            throw new InvalidDataException("Select an explicit material kind.");
        if (kind == BaseColorMaterialLabel) return NativeFreshFloorMaterialIntent.BaseFloorColor();
        if (kind != RetailMaterialLabel) throw new InvalidDataException("Unsupported raw material kind.");
        if (slot.SourceLevel.SelectedItem is not int entry)
            throw new InvalidDataException("Select an explicit source WAD entry; no donor is inferred.");
        if (!int.TryParse(slot.TextureId.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
            throw new InvalidDataException("Material texture ID requires an integer.");
        // Model validation is the only neutral range/variant gate. A valid record
        // may still be absent, animated or otherwise unavailable to native output.
        return NativeFreshFloorMaterialIntent.RetailTerrain(slot.SourceVariant.Text ?? "", entry, id);
    }

    private MaterialFields MaterialFieldSignature() =>
        new(_materialsEnabled, _materialSlots[0].Capture(), _materialSlots[1].Capture());

    private void RefreshMaterialChrome(bool editable)
    {
        bool present = _aggregateMode && CurrentAggregateIntent != null;
        _materialFields.IsVisible = present;
        _enableMaterials.IsVisible = present && CurrentAggregateIntent!.SchemaVersion == 1;
        _enableMaterials.IsEnabled = editable && present && !_addingWizard && !_materialsEnabled;
        bool canEdit = editable && present && !_addingWizard && _materialsEnabled;
        foreach (MaterialSlotEditor slot in _materialSlots)
        {
            slot.Kind.IsEnabled = canEdit;
            bool retail = slot.Kind.SelectedItem is string kind && kind == RetailMaterialLabel;
            slot.SourceVariant.IsEnabled = slot.SourceLevel.IsEnabled = slot.TextureId.IsEnabled = canEdit && retail;
        }
    }
}
