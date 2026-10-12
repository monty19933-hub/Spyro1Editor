using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.App.Views;

/// <summary>Terrain › Textures › Choose Texture… in a level you build: every texture of an original level as a picture.
/// Click one to put it on the selected tiles; then every tile clicked in the view takes it, until Esc. Only the look
/// changes: what Spyro finds on a tile stays (the Ground card and Tile Type… set that).</summary>
public sealed partial class MainWindow
{
    private AuthoredMaterial? _activeBuiltTexture;
    private string _activeBuiltTextureName = "";

    internal AuthoredMaterial? ActiveBuiltTextureForTesting => _activeBuiltTexture;
    /// <summary>Answers the texture grid in tests: (level key, texture id), or null to cancel.</summary>
    internal Func<(string LevelKey, int TextureId)?>? BuiltTextureChoiceForTesting { get; set; }

    private async Task ChooseBuiltTextureAsync()
    {
        if (_freshSceneDocument == null || _currentGeometry == null) return;
        var owner = _freshSceneDocument;
        (string LevelKey, int TextureId)? choice = BuiltTextureChoiceForTesting != null ? BuiltTextureChoiceForTesting() : await ShowBuiltTextureGridAsync();
        if (choice is not { } picked || _freshSceneDocument?.Id != owner.Id) return;
        var level = _retailCatalog.FindByKey(picked.LevelKey);
        if (level == null) return;
        string disc = ResolveUniversalSourceDisc();
        try
        {
            // Textures that move in their own level (animated water, lava…) are refused here: a level you build copies a still.
            await Task.Run(() => NativeStaticTerrainTextureSource.Capture(disc, level.SourceWadEntry, picked.TextureId));
        }
        catch (Exception e) when (e is InvalidDataException or ArgumentException or IOException)
        {
            _statusText.Text = $"{level.DisplayName}'s texture {picked.TextureId + 1} moves in its level, so it can't be copied into a level you build. Choose another one.";
            return;
        }
        StartBuiltTexturePainting(new(level.SourceWadEntry, picked.TextureId), $"{level.DisplayName} texture {picked.TextureId + 1}");
        List<TerrainPolygon> selected = SelectedTilesForTypeChange();
        if (selected.Count > 0)
        {
            await ApplyBuiltTextureAsync(selected, _activeBuiltTexture!);
            ClearGroundSelection();
            _statusText.Text = $"{Count(selected.Count, "tile shows", "tiles show")} {_activeBuiltTextureName} now. Click more tiles to paint them; Esc stops.";
        }
        else
            _statusText.Text = $"Painting {_activeBuiltTextureName}: click tiles in the view. Esc stops.";
    }

    private void StartBuiltTexturePainting(AuthoredMaterial material, string name)
    {
        StopGroundPainting(announce: false);
        StopTerrainTexturePaintMode(announce: false);
        _activeBuiltTexture = material;
        _activeBuiltTextureName = name;
        _viewport.TerrainTexturePaintMode = true;
        _viewport.TerrainTexturePaintHint = name;
    }

    /// <summary>Paints a clicked face while a texture from the grid is chosen; false when none is.</summary>
    private async Task<bool> PaintBuiltTextureFromViewportAsync(TerrainPolygon face)
    {
        if (_activeBuiltTexture is not { } material) return false;
        if (_freshSceneDocument == null || face.IsTerrainRemoved || face.IsTerrainAddClone) return true;
        await ApplyBuiltTextureAsync([face], material);
        _statusText.Text = $"Painted {_activeBuiltTextureName}. Keep clicking tiles; Esc stops.";
        return true;
    }

    /// <summary>Gives faces a texture and keeps what Spyro finds on them (a new texture clears a staged kind, which is put
    /// back), then saves the level.</summary>
    internal async Task ApplyBuiltTextureAsync(IReadOnlyList<TerrainPolygon> faces, AuthoredMaterial material)
    {
        if (_freshSceneDocument == null) return;
        var surfaces = _freshSceneDocument.Surfaces.ToArray();
        foreach (TerrainPolygon face in faces)
        {
            if (face.SectorIndex < 0 || face.SectorIndex >= surfaces.Length) continue;
            surfaces[face.SectorIndex] = surfaces[face.SectorIndex] with { Material = material };
            var kind = face.SurfaceBehaviorEdit;
            face.ApplyTextureOverride(material.TextureId);
            if (kind != null) face.ApplySurfaceBehaviorEdit(kind);
        }
        _freshSceneDocument = _freshSceneDocument with { Surfaces = surfaces };
        _viewport.NotifyTerrainPresentationDataChanged();
        _viewport.InvalidateVisual();
        _ = RefreshFreshMaterialPreviewAsync();
        await SaveFreshSceneAsync();
    }

    private bool StopBuiltTexturePainting(bool announce)
    {
        if (_activeBuiltTexture == null) return false;
        _activeBuiltTexture = null;
        _viewport.TerrainTexturePaintMode = false;
        _viewport.TerrainTexturePaintHint = "";
        if (announce) _statusText.Text = "Stopped painting. Clicks select tiles again.";
        return true;
    }

    private async Task<(string LevelKey, int TextureId)?> ShowBuiltTextureGridAsync(Window? owner = null)
    {
        var levels = _retailCatalog.Levels.Where(l => l.SourceWadEntry is >= 10 and <= 78).ToArray();
        var current = _freshSceneDocument?.Surfaces.Select(s => s.Material).OfType<AuthoredMaterial>().GroupBy(m => m.SourceWadEntry)
            .OrderByDescending(g => g.Count()).FirstOrDefault()?.Key;
        ComboBox level = new()
        {
            Name = "BuiltTextureLevel", ItemsSource = levels, MinWidth = 220,
            SelectedItem = levels.FirstOrDefault(l => l.SourceWadEntry == current) ?? levels[0]
        };
        TextBlock note = new() { Name = "BuiltTextureNote", TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(ModernMutedInk) };
        WrapPanel grid = new() { Name = "BuiltTextureGrid", Orientation = Orientation.Horizontal, ItemSpacing = 6, LineSpacing = 6 };
        ScrollViewer scroll = new() { Content = grid, Height = 420, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        Window dialog = new()
        {
            Name = "BuiltTextureDialog", Title = "Choose a texture", Width = 640, SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, CanResize = true
        };
        (string, int)? result = null;
        List<Bitmap> bitmaps = [];
        int revision = 0;
        async Task Fill()
        {
            int request = ++revision;
            grid.Children.Clear();
            foreach (var b in bitmaps) b.Dispose();
            bitmaps.Clear();
            if (level.SelectedItem is not LevelDefinition source) return;
            note.Text = $"Loading {source.DisplayName}'s textures…";
            string folder = Path.Combine(_workspace.RootPath, "_local", "texture-grid", source.Key);
            string disc = ResolveUniversalSourceDisc();
            IReadOnlyList<TerrainTextureImageExport> textures;
            try
            {
                Directory.CreateDirectory(folder);
                textures = await TerrainPatchExporter.ExportTerrainTextureImagesAsync(disc, source, Enumerable.Range(0, 128), folder);
            }
            catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
            { if (request == revision) note.Text = "The textures could not be read from your disc: " + e.Message; return; }
            if (request != revision) return;
            foreach (var texture in textures.OrderBy(t => t.TextureId))
            {
                string path = Path.Combine(folder, $"{source.Key}-texture-{texture.TextureId:000}.png");
                if (!File.Exists(path)) continue;
                var bitmap = new Bitmap(path); bitmaps.Add(bitmap);
                int id = texture.TextureId;
                StackPanel face = new() { Spacing = 2 };
                face.Children.Add(new Image { Source = bitmap, Width = 64, Height = 64, Stretch = Stretch.UniformToFill });
                face.Children.Add(new TextBlock { Text = (id + 1).ToString(), FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center });
                Button pick = new()
                {
                    Name = $"BuiltTexture{id}", Content = face, Padding = new Thickness(3), Background = Brushes.Transparent,
                    BorderBrush = new SolidColorBrush(ModernLine), BorderThickness = new Thickness(1)
                };
                ToolTip.SetTip(pick, $"{source.DisplayName}, texture {id + 1}");
                pick.Click += (_, _) => { result = (source.Key, id); dialog.Close(); };
                grid.Children.Add(pick);
            }
            note.Text = $"{source.DisplayName}: {Count(grid.Children.Count, "texture", "textures")}. Click one to paint the selected tiles with it, then click more tiles in the view. " +
                "Only the look changes; the Ground card and Tile Type… set what Spyro finds on a tile.";
        }
        level.SelectionChanged += async (_, _) => await Fill();
        Button cancel = NewButton("Cancel", () => dialog.Close());
        cancel.Name = "BuiltTextureCancel";
        dialog.Content = new StackPanel
        {
            Spacing = 10, Margin = new Thickness(16),
            Children =
            {
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { new TextBlock { Text = "Textures of", VerticalAlignment = VerticalAlignment.Center }, level } },
                note, scroll,
                new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Children = { cancel } }
            }
        };
        dialog.Closed += (_, _) => { revision++; foreach (var b in bitmaps) b.Dispose(); };
        _ = Fill();
        await dialog.ShowDialog(owner ?? this);
        return result;
    }
}
