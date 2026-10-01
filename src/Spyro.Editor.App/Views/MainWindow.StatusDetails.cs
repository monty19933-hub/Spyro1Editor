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
        dialog.Content = message;
        await dialog.ShowDialog(this);
    }
}
