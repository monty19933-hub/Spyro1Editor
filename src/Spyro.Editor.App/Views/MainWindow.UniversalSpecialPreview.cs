using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.App.Views;

public sealed partial class MainWindow
{
    private static Control BuildEggColourChoice(string id, string name)
    {
        int index = UniversalDragonflyEggs.VariantIndex(id);
        var rgb = index >= 0 ? UniversalDragonflyEggs.IconTint(index) : (R: 128, G: 128, B: 128);
        StackPanel row = new() { Orientation = Orientation.Horizontal, Spacing = 9 };
        row.Children.Add(new Border { Width = 20, Height = 20, CornerRadius = new CornerRadius(10),
            Background = new SolidColorBrush(Color.FromRgb((byte)rgb.R, (byte)rgb.G, (byte)rgb.B)),
            BorderBrush = new SolidColorBrush(Color.Parse("#8995A3")), BorderThickness = new Thickness(1) });
        row.Children.Add(new TextBlock { Text = EggColourName(name), VerticalAlignment = VerticalAlignment.Center });
        return row;
    }

    private Control BuildUniversalEggEditPreview(ComboBox colours)
    {
        Border host = new() { Name = "UniversalEggEditPreviewHost", MaxWidth = 280, HorizontalAlignment = HorizontalAlignment.Left };
        void Refresh()
        {
            if (colours.SelectedItem is not UniversalEggColourChoice choice) return;
            host.Child = UniversalMobyModelPreview.Build(async () =>
            {
                try
                {
                    var (_, library, _) = await EnsureUniversalCatalogAsync();
                    var variant = UniversalSpecialCatalog.Entries(true).SelectMany(e => e.Variants)
                        .Single(v => UniversalSpecialCatalog.SpecialIdForClass(v.ClassId) == choice.Id);
                    var japanese = await Task.Run(ResolveUniversalJapaneseSource);
                    return await UniversalMobyModelPreview.LoadAsync(library, variant, japanese);
                }
                catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException)
                { return new(null, error.Message); }
            }, detailed: true);
        }
        colours.SelectionChanged += (_, _) => Refresh();
        Refresh();
        return host;
    }

    private static Control BuildUniversalSignPreview(TextBox input)
    {
        TextBlock text = new() { Name = "UniversalSignLivePreview", Foreground = Brushes.White, FontWeight = FontWeight.Bold,
            FontSize = 16, LineHeight = 24, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.NoWrap };
        TextBlock constraints = new() { Name = "UniversalSignConstraintStatus", TextWrapping = TextWrapping.Wrap, FontSize = 11 };
        void Refresh()
        {
            string entered = (input.Text ?? "").Replace("\r", "");
            string normalized = string.Join('\n', UniversalPlacementReader.SplitSignText(entered).Select(UniversalSpecialBehaviors.NormalizeSignLine)).TrimEnd('\n');
            text.Text = normalized.Length == 0 ? "(EMPTY MESSAGE)" : normalized;
            int lines = entered.Split('\n').Length;
            bool changed = normalized != entered.ToUpperInvariant().TrimEnd('\n');
            constraints.Text = changed
                ? "The game will show the preview above. Extra lines, characters beyond the limit and unsupported symbols are removed when you apply."
                : $"{Math.Min(lines, UniversalSpecialBehaviors.SignLines)} / {UniversalSpecialBehaviors.SignLines} lines · {UniversalSpecialBehaviors.SignLineMaxChars} characters per line · shown in capitals";
            constraints.Foreground = new SolidColorBrush(changed ? Color.Parse("#986700") : ModernMutedInk);
        }
        input.TextChanged += (_, _) => Refresh(); Refresh();
        return new StackPanel { Spacing = 6, Children =
        {
            new TextBlock { Text = "In-game message preview", FontSize = 11, Foreground = new SolidColorBrush(ModernMutedInk) },
            new Border { Background = new SolidColorBrush(Color.Parse("#202938")), CornerRadius = new CornerRadius(8), Padding = new Thickness(12), Child = text }, constraints
        } };
    }
}
