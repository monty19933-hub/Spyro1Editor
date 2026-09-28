using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;

namespace Spyro.Editor.App.Views;

// A level is selected through the selection pattern, never inferred from an AX string.
internal sealed class LevelPickerComboBox : ComboBox
{
    protected override Type StyleKeyOverride => typeof(ComboBox);

    protected override AutomationPeer OnCreateAutomationPeer() => new LevelPickerAutomationPeer(this);

    private sealed class LevelPickerAutomationPeer(ComboBox owner) : ComboBoxAutomationPeer(owner), IValueProvider
    {
        bool IValueProvider.IsReadOnly => true;

        string? IValueProvider.Value
        {
            get
            {
                var selected = GetSelection();
                return selected.Count == 1 ? selected[0].GetName() : null;
            }
        }

        void IValueProvider.SetValue(string? value)
        {
            // Avalonia 12.0.3's read-only peer throws here. A native AX caller can
            // still invoke this entry, so reject it without throwing across the
            // native callback boundary. Do not resolve even a matching label.
        }
    }
}
