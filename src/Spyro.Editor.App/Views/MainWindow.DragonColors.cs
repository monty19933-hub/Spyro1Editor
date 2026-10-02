using Avalonia.Controls;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.App.Views;

public sealed partial class MainWindow
{
    private sealed record DragonColorEditor(ColorPalettePicker Picker, CheckBox Custom, Control Control)
    {
        public bool Validate(out string message)
        {
            message = "";
            return Custom.IsChecked != true || Picker.TryApplyTypedHex(out message);
        }
        public ColorRgba? Value => Custom.IsChecked == true ? Picker.SelectedColor : null;
    }

    private static DragonColorEditor? BuildDragonColorEditor(Moby moby)
    {
        if (!DragonStatueColor.IsStatue(moby)) return null;
        ColorPalettePicker picker = new([
            new("Current colour", moby.DragonColor ?? ColorRgba.FromRgb(48, 208, 112)),
            new("Ruby", ColorRgba.FromRgb(255, 48, 72)),
            new("Gold", ColorRgba.FromRgb(255, 196, 32)),
            new("Emerald", ColorRgba.FromRgb(32, 224, 96)),
            new("Sapphire", ColorRgba.FromRgb(48, 112, 255)),
            new("Amethyst", ColorRgba.FromRgb(192, 64, 255)),
            new("Pearl", ColorRgba.FromRgb(255, 255, 255))]);
        picker.Control.Name = "DragonColorPicker";
        CheckBox custom = new() { Name = "DragonCustomColor", Content = "Customize dragon color", IsChecked = moby.DragonColor != null };
        picker.Control.IsEnabled = custom.IsChecked == true;
        custom.IsCheckedChanged += (_, _) => picker.Control.IsEnabled = custom.IsChecked == true;
        Button original = NewButton("Use original color");
        original.Click += (_, _) => custom.IsChecked = false;
        StackPanel panel = new() { Spacing = 8, Children = { custom, picker.Control, original } };
        return new(picker, custom, panel);
    }
}
