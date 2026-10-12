using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Sky;

namespace Spyro.Editor.App.Views;

/// <summary>Import Sky: a 360° panorama picture (PNG, JPEG, WebP, BMP) or a sky model (OBJ, FBX) becomes the game's own
/// kind of sky — coloured triangles around the player — after a preview of exactly what the game will draw, with a
/// detail and a turn setting. Used for the game's levels (as a .sky file in the project) and for levels you build (kept
/// in the level).</summary>
public sealed partial class MainWindow
{
    internal static readonly string[] SkyPictureExtensions = [".png", ".jpg", ".jpeg", ".webp", ".bmp"];
    internal static readonly string[] SkyModelExtensions = [".obj", ".fbx"];

    /// <summary>Answers the Import Sky dialog in tests (null: cancel).</summary>
    internal Func<SkyBuildOptions?>? SkyImportChoiceForTesting { get; set; }

    internal static FilePickerFileType SkyFileType(bool withNativeSky) => new(withNativeSky ? "Skies: 360° pictures, OBJ, FBX or .sky" : "Skies: 360° pictures, OBJ or FBX")
    {
        Patterns = [.. SkyPictureExtensions.Concat(SkyModelExtensions).Concat(withNativeSky ? [".sky"] : []).Select(e => "*" + e)]
    };

    /// <summary>Decodes a picture with the app's image support, at most <paramref name="maxWidth"/> pixels wide.</summary>
    internal static SkyImage DecodeSkyPicture(byte[] bytes, int maxWidth)
    {
        Bitmap bitmap;
        try
        {
            using (var stream = new MemoryStream(bytes)) bitmap = new Bitmap(stream);
            if (bitmap.PixelSize.Width > maxWidth)
            {
                bitmap.Dispose();
                using var again = new MemoryStream(bytes);
                bitmap = Bitmap.DecodeToWidth(again, maxWidth, BitmapInterpolationMode.HighQuality);
            }
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            throw new InvalidDataException("The picture could not be read (use PNG, JPEG, WebP or BMP).", e);
        }
        using (bitmap)
        {
            int width = bitmap.PixelSize.Width, height = bitmap.PixelSize.Height, stride = width * 4;
            byte[] raw = new byte[stride * height];
            IntPtr buffer = Marshal.AllocHGlobal(raw.Length);
            try
            {
                bitmap.CopyPixels(new PixelRect(bitmap.PixelSize), buffer, raw.Length, stride);
                Marshal.Copy(buffer, raw, 0, raw.Length);
            }
            finally { Marshal.FreeHGlobal(buffer); }
            bool bgra = bitmap.Format is not { } format || format == PixelFormats.Bgra8888;
            bool premultiplied = bitmap.AlphaFormat is not AlphaFormat.Unpremul;
            var pixels = new Rgba32[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                byte c0 = raw[i * 4], c1 = raw[i * 4 + 1], c2 = raw[i * 4 + 2], a = raw[i * 4 + 3];
                byte r = bgra ? c2 : c0, g = c1, b = bgra ? c0 : c2;
                if (premultiplied && a is > 0 and < 255)
                {
                    r = (byte)Math.Min(255, r * 255 / a); g = (byte)Math.Min(255, g * 255 / a); b = (byte)Math.Min(255, b * 255 / a);
                }
                pixels[i] = new(r, g, b, a);
            }
            return new SkyImage(width, height, pixels);
        }
    }

    /// <summary>Reads a sky source into a panorama (pictures as they are; models as seen from their centre).</summary>
    internal static SkyPanorama LoadSkyPanorama(string path)
    {
        string extension = Path.GetExtension(path).ToLowerInvariant();
        return extension switch
        {
            ".obj" => SkyPanorama.FromModel(ObjSkyReader.Read(path, DecodeSkyPicture)),
            ".fbx" => SkyPanorama.FromModel(FbxSkyReader.Read(path, DecodeSkyPicture)),
            _ when SkyPictureExtensions.Contains(extension) => SkyPanorama.FromImage(DecodeSkyPicture(File.ReadAllBytes(path), 4096)),
            _ => throw new InvalidDataException("Choose a 360° panorama picture (PNG, JPEG, WebP, BMP) or a sky model (OBJ, FBX).")
        };
    }

    /// <summary>Turns a picture or model into a native sky block, after the Import Sky dialog; null when it was canceled
    /// or could not be read (the status line says why).</summary>
    internal async Task<SkyBuildResult?> ImportSkyFromFileAsync(string path)
    {
        _statusText.Text = $"Reading {Path.GetFileName(path)}…";
        SkyPanorama panorama;
        try { panorama = await Task.Run(() => LoadSkyPanorama(path)); }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            _statusText.Text = $"Could not read {Path.GetFileName(path)} as a sky: {e.Message}";
            return null;
        }
        var choice = SkyImportChoiceForTesting != null ? SkyImportChoiceForTesting() : await ShowSkyImportDialogAsync(Path.GetFileName(path), panorama);
        if (choice is not { } chosen) { _statusText.Text = "Sky import canceled."; return null; }
        try { return await Task.Run(() => NativeSkyBuilder.Build(panorama, chosen)); }
        catch (InvalidDataException e) { _statusText.Text = "Could not make a sky from it: " + e.Message; return null; }
    }

    private async Task<SkyBuildOptions?> ShowSkyImportDialogAsync(string fileName, SkyPanorama panorama)
    {
        var preview = new Image { Name = "SkyImportPreview", Width = 640, Height = 320, Stretch = Stretch.Fill };
        var facing = new TextBlock { Text = "▲ faces the level's start direction (heading 0)", FontSize = 11, Foreground = new SolidColorBrush(ModernMutedInk), HorizontalAlignment = HorizontalAlignment.Center };
        var summary = new TextBlock { Name = "SkyImportSummary", TextWrapping = TextWrapping.Wrap };
        var detail = new Slider { Name = "SkyImportDetail", Minimum = SkyBuildOptions.MinTriangles, Maximum = SkyBuildOptions.MaxTriangles, Value = SkyBuildOptions.DefaultTriangles, TickFrequency = 100, IsSnapToTickEnabled = true };
        var turn = new Slider { Name = "SkyImportTurn", Minimum = 0, Maximum = 355, Value = 0, TickFrequency = 5, IsSnapToTickEnabled = true };
        var detailLabel = new TextBlock(); var turnLabel = new TextBlock();
        var mirror = new CheckBox { Name = "SkyImportMirror", Content = "Flip left and right (for a model made to be seen from outside, whose pictures read backwards)" };
        var notes = new TextBlock { Name = "SkyImportNotes", TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.FromRgb(168, 98, 0)), Text = string.Join("\n", panorama.Notes), IsVisible = panorama.Notes.Count > 0 };
        Button cancel = NewButton("Cancel"), use = NewButton("Use This Sky");
        cancel.Name = "SkyImportCancel"; use.Name = "SkyImportUse";
        StyleModernPrimaryButton(use, ModernBlue, 132);
        var content = new StackPanel { Margin = new Thickness(20), Spacing = 10 };
        content.Children.Add(new TextBlock { Text = "Import Sky", FontSize = 20, FontWeight = FontWeight.SemiBold });
        content.Children.Add(new TextBlock
        {
            TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(ModernMutedInk),
            Text = $"{fileName}, as the game will draw it all around the player. Spyro skies are made of coloured triangles, so small details " +
                   "(single stars, writing) soften; more detail keeps more of them but makes the level bigger."
        });
        content.Children.Add(new Border { Child = preview, BorderBrush = new SolidColorBrush(ModernLine), BorderThickness = new Thickness(1), HorizontalAlignment = HorizontalAlignment.Center });
        content.Children.Add(facing);
        content.Children.Add(notes);
        content.Children.Add(detailLabel); content.Children.Add(detail);
        content.Children.Add(turnLabel); content.Children.Add(turn);
        content.Children.Add(mirror);
        content.Children.Add(summary);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right };
        actions.Children.Add(cancel); actions.Children.Add(use);
        content.Children.Add(actions);
        var dialog = new Window
        {
            Name = "SkyImportDialog", Title = "Import Sky", Width = 700, SizeToContent = SizeToContent.Height, CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = new ScrollViewer { Content = content }
        };
        int revision = 0;
        WriteableBitmap? shown = null;
        async Task Refresh()
        {
            int request = ++revision;
            var options = new SkyBuildOptions((int)detail.Value, turn.Value, mirror.IsChecked == true);
            int triangles = options.Triangles; double degrees = options.TurnDegrees;
            detailLabel.Text = $"Detail: {triangles:N0} triangles";
            turnLabel.Text = $"Turn: {degrees:0}° (turns the sky around the player)";
            summary.Text = "Building the preview…";
            use.IsEnabled = false;
            try
            {
                var (result, picture) = await Task.Run(() =>
                {
                    var built = NativeSkyBuilder.Build(panorama, options);
                    return (built, NativeSkyPreview.Render(built.Block, 1024));
                });
                if (request != revision) return;
                var bitmap = ToBitmap(picture);
                preview.Source = bitmap; shown?.Dispose(); shown = bitmap;
                summary.Text = $"{result.Triangles:N0} triangles in {result.Parts} parts: {result.Block.Length / 1024.0:0} KB of the level " +
                               "(the game's own skies use 4-55 KB).";
                use.IsEnabled = true;
            }
            catch (InvalidDataException e)
            {
                if (request == revision) summary.Text = "This setting can't make a sky the game can draw: " + e.Message;
            }
        }
        detail.PropertyChanged += async (_, e) => { if (e.Property == RangeBase.ValueProperty) await Refresh(); };
        turn.PropertyChanged += async (_, e) => { if (e.Property == RangeBase.ValueProperty) await Refresh(); };
        mirror.IsCheckedChanged += async (_, _) => await Refresh();
        cancel.Click += (_, _) => dialog.Close(false);
        use.Click += (_, _) => dialog.Close(true);
        dialog.Closed += (_, _) => { revision++; shown?.Dispose(); };
        _ = Refresh();
        if (!await dialog.ShowDialog<bool>(this)) return null;
        return new SkyBuildOptions((int)detail.Value, turn.Value, mirror.IsChecked == true);
    }

    private static WriteableBitmap ToBitmap(SkyImage image)
    {
        var bitmap = new WriteableBitmap(new PixelSize(image.Width, image.Height), new Vector(96, 96), PixelFormats.Rgba8888, AlphaFormat.Unpremul);
        using var locked = bitmap.Lock();
        byte[] row = new byte[image.Width * 4];
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                Rgba32 p = image.Pixels[y * image.Width + x];
                row[x * 4] = p.R; row[x * 4 + 1] = p.G; row[x * 4 + 2] = p.B; row[x * 4 + 3] = 255;
            }
            Marshal.Copy(row, 0, locked.Address + y * locked.RowBytes, row.Length);
        }
        return bitmap;
    }

    /// <summary>Environment › Import a sky… in a level you build: the sky is kept in the level (Save Level), in place of a
    /// borrowed level's sky; the editor view takes its horizon colour.</summary>
    private async Task ImportFreshSkyAsync()
    {
        if (_freshSceneDocument == null) return;
        var owner = _freshSceneDocument;
        string? path = CustomSkyFilePickerOverrideForTesting != null ? await CustomSkyFilePickerOverrideForTesting() : await PickSkyFileAsync(withNativeSky: true);
        if (string.IsNullOrWhiteSpace(path) || _freshSceneDocument?.Id != owner.Id) return;
        byte[] block; string what;
        if (Path.GetExtension(path).Equals(".sky", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                block = Spyro.Editor.Core.Exporting.Spyro1SkyBlockAnalyzer.NormalizeStandaloneSkyFile(await File.ReadAllBytesAsync(path));
            }
            catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
            { _statusText.Text = "Could not use that .sky file: " + e.Message; return; }
            what = Path.GetFileName(path);
        }
        else
        {
            if (await ImportSkyFromFileAsync(path) is not { } built || _freshSceneDocument?.Id != owner.Id) return;
            block = built.Block;
            what = $"{Path.GetFileName(path)} ({built.Triangles:N0} triangles)";
        }
        SkyPanorama shownSky = SkyPanorama.FromImage(NativeSkyPreview.Render(block, 256));
        var (r, g, b) = shownSky.AverageBetween(0.3, 0.5);
        try { (_freshSceneDocument with { CustomSky = block }).Validate(); }
        catch (InvalidDataException e) { _statusText.Text = "Could not use that sky: " + e.Message; return; }
        _freshSceneDocument = _freshSceneDocument with { CustomSky = block, SkySourceWadEntry = null, SkyColor = new(r, g, b) };
        _viewport.SetAuthoredSkyColor(_freshSceneDocument.SkyColor);
        RefreshFreshDocumentPanels();
        _statusText.Text = $"Your level will use the sky from {what} in the game. Save Level keeps it.";
    }

    private async Task<string?> PickSkyFileAsync(bool withNativeSky)
    {
        if (StorageProvider is not { } storage) { _statusText.Text = "File picker is not available in this environment."; return null; }
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose a sky: a 360° panorama picture or a sky model (OBJ, FBX)" + (withNativeSky ? ", or a .sky file" : ""),
            AllowMultiple = false,
            FileTypeFilter = [SkyFileType(withNativeSky), FilePickerFileTypes.All]
        });
        return files.FirstOrDefault()?.Path.LocalPath;
    }
}
