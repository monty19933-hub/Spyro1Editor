using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Spyro.Editor.App.Views;

public partial class MainWindow
{
    private async Task ShowStatusDetailsAsync()
    {
        Window dialog = new()
        {
            Title = "Status Details", Width = 720, Height = 320,
            MinWidth = 400, MinHeight = 220, CanResize = true,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        TextBox message = new()
        {
            Name = "StatusDetailsMessage", Text = _statusText.Text ?? "",
            IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(16), HorizontalAlignment = HorizontalAlignment.Stretch
        };
        // With a repair on offer the window has the status bar's Fix It For Me too; the message then says what it did.
        Button fix = NewButton("Fix It For Me");
        fix.Name = "StatusDetailsFixIt";
        fix.IsVisible = _pendingRepair != null;
        fix.HorizontalAlignment = HorizontalAlignment.Right;
        fix.Margin = new Thickness(16, 0, 16, 16);
        StyleModernPrimaryButton(fix, ModernBlue);
        fix.Click += async (_, _) =>
        {
            fix.IsEnabled = false;
            await ApplyPendingRepairAsync();
            message.Text = _statusText.Text ?? "";
            fix.IsVisible = _pendingRepair != null;
            fix.IsEnabled = true;
        };
        DockPanel content = new();
        DockPanel.SetDock(fix, Dock.Bottom);
        content.Children.Add(fix);
        content.Children.Add(message);
        dialog.Content = content;
        await dialog.ShowDialog(this);
    }
}
