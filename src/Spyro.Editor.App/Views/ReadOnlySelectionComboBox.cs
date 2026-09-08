using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;

namespace Spyro.Editor.App.Views;

// Avalonia 12.0.3 exposes the non-editable ComboBox value on macOS, but its
// native bridge forwards SetValue without honoring IsReadOnly. The base setter
// throws across that native callback and terminates the application. Keep the
// live value/selection/menu providers; unsupported text writes are simply ignored.
internal sealed class ReadOnlySelectionComboBox : ComboBox
{
    protected override Type StyleKeyOverride => typeof(ComboBox);
    protected override AutomationPeer OnCreateAutomationPeer() => new Peer(this);

    private sealed class Peer(ComboBox owner) : ComboBoxAutomationPeer(owner)
    {
        private IValueProvider? _readOnlyValue;
        protected override object? GetProviderCore(Type providerType)
        {
            if (providerType != typeof(IValueProvider)) return base.GetProviderCore(providerType);
            // The base peer remains the real live read provider. Do not look up
            // through this override again or create another peer/subscription.
            return _readOnlyValue ??= new ReadOnlyValue((IValueProvider)base.GetProviderCore(providerType)!);
        }
    }

    private sealed class ReadOnlyValue(IValueProvider original) : IValueProvider
    {
        public bool IsReadOnly => true;
        public string? Value => original.Value;
        public void SetValue(string? value)
        {
            // This control is selected via its menu/selection provider, not edited
            // as free text. Never choose an item, dispatch a command or throw here.
        }
    }
}
