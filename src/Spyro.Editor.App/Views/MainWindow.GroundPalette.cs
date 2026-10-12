using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.App.Views;

/// <summary>Terrain tab › Ground, for a level you build: one button per ground (grass, dirt, stone, sand, snow, wood,
/// water, lava, goo). A ground sets how a tile looks (an original level's texture) and what Spyro finds on it (water,
/// lava and goo cost a life) at once. Clicking a ground repaints the selected tiles and then paints every tile clicked in
/// the view until Esc or Stop.</summary>
public sealed partial class MainWindow
{
    private Border? _groundCard;
    private TextBlock? _groundNote;
    private Button? _groundStopButton;
    private GroundType? _activeGround;
    private readonly Dictionary<GroundType, Button> _groundButtons = [];
    private readonly Dictionary<GroundType, Image> _groundImages = [];
    private bool _groundImagesLoading;

    /// <summary>Answers for tests: paints these faces with a ground as a click on its button does.</summary>
    internal Task ChooseGroundForTestingAsync(string name) => ChooseGroundAsync(GroundTypes.Find(name) ?? throw new ArgumentException(name));
    internal GroundType? ActiveGroundForTesting => _activeGround;

    private Control BuildTerrainGroundCard()
    {
        StackPanel content = new();
        _groundNote = TerrainNote();
        _groundNote.Name = "GroundNote";
        content.Children.Add(_groundNote);
        WrapPanel swatches = new() { Name = "GroundSwatches", Orientation = Orientation.Horizontal, ItemSpacing = 6, LineSpacing = 6 };
        foreach (GroundType ground in GroundTypes.All)
        {
            Image image = new() { Width = 56, Height = 56, Stretch = Stretch.UniformToFill };
            Border frame = new()
            {
                Width = 56, Height = 56, CornerRadius = new CornerRadius(4), ClipToBounds = true,
                Background = new SolidColorBrush(Color.FromRgb(ground.Shade.R, ground.Shade.G, ground.Shade.B)), Child = image
            };
            StackPanel face = new() { Spacing = 3, HorizontalAlignment = HorizontalAlignment.Center };
            face.Children.Add(frame);
            face.Children.Add(new TextBlock { Text = ground.Name, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center });
            Button button = new()
            {
                Name = "Ground" + ground.Name, Content = face, Padding = new Thickness(5), Width = 74,
                Background = Brushes.Transparent, BorderBrush = new SolidColorBrush(ModernLine), BorderThickness = new Thickness(1)
            };
            ToolTip.SetTip(button, ground.Kind == null ? $"{ground.Name}: solid ground ({ground.Note})" : $"{ground.Name}: {ground.Note}");
            GroundType chosen = ground;
            button.Click += async (_, _) => await ChooseGroundAsync(chosen);
            _groundButtons[ground] = button;
            _groundImages[ground] = image;
            swatches.Children.Add(button);
        }
        content.Children.Add(swatches);
        _groundStopButton = NewButton("Stop Painting", () => StopGroundPainting(announce: true));
        _groundStopButton.Name = "GroundStop";
        _groundStopButton.HorizontalAlignment = HorizontalAlignment.Left;
        ToolTip.SetTip(_groundStopButton, "Go back to selecting tiles. Esc does the same.");
        content.Children.Add(_groundStopButton);
        _groundCard = TerrainCard("Ground", content);
        _groundCard.Name = "TerrainGroundCard";
        RefreshGroundCard();
        return _groundCard;
    }

    /// <summary>Shows the card in a level you build, loads the ground pictures from the user's disc once, and says what a
    /// click does now.</summary>
    private void RefreshGroundCard()
    {
        if (_groundCard == null || _groundNote == null)
            return;
        bool built = _freshSceneDocument != null && _currentGeometry != null;
        _groundCard.IsVisible = built;
        if (!built)
        {
            if (_activeGround != null) StopGroundPainting(announce: false);
            return;
        }
        int selected = SelectedTilesForTypeChange().Count;
        _groundNote.Text = _activeGround != null
            ? $"Painting {_activeGround.Name.ToLowerInvariant()}: click tiles in the view. Esc or Stop Painting goes back to selecting."
            : selected > 0
                ? $"Click a ground to make the {(selected == 1 ? "selected tile" : $"{selected} selected tiles")} that ground, then keep clicking tiles to paint more."
                : "Click a ground, then click tiles in the view to paint them. Water, lava and goo cost Spyro a life, as in the game.";
        foreach ((GroundType ground, Button button) in _groundButtons)
        {
            bool active = ground == _activeGround;
            button.BorderBrush = new SolidColorBrush(active ? ModernBlue : ModernLine);
            button.BorderThickness = new Thickness(active ? 3 : 1);
        }
        if (_groundStopButton != null)
            _groundStopButton.IsVisible = _activeGround != null;
        if (!_groundImagesLoading && _groundImages.Values.Any(i => i.Source == null))
            _ = LoadGroundImagesAsync();
    }

    private async Task LoadGroundImagesAsync()
    {
        _groundImagesLoading = true;
        try
        {
            string disc = ResolveUniversalSourceDisc();
            foreach (GroundType ground in GroundTypes.All)
            {
                if (_groundImages[ground].Source != null)
                    continue;
                var level = _retailCatalog.FindByKey(ground.LevelKey);
                if (level == null)
                    continue;
                string path = Path.Combine(_workspace.RootPath, "_local", "authored-materials", $"wad-{level.SourceWadEntry}-t-{ground.TextureId}.png");
                if (!File.Exists(path))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    await TerrainPatchExporter.TryExportTerrainTextureImageTierAsync(disc, level, ground.TextureId, path, "hqData");
                }
                if (!File.Exists(path))
                    continue;
                if (ground.Shade != new Spyro.Editor.Core.Primitives.ColorRgba(128, 128, 128))
                {
                    // Drawn as the game draws it: the texture times the ground's shade (128 = unchanged).
                    string tinted = Path.ChangeExtension(path, null) + $"-shade-{ground.Shade.R}-{ground.Shade.G}-{ground.Shade.B}.png";
                    if (!File.Exists(tinted))
                    {
                        var pixels = PngRgbaImage.ReadRgba(path, out int w, out int h);
                        static byte Mix(byte c, byte s) => (byte)Math.Min(255, c * s / 128);
                        await TerrainTexturePngWriter.WriteRgbaAsync(tinted, w, h,
                            pixels.Select(p => new Rgba32(Mix(p.R, ground.Shade.R), Mix(p.G, ground.Shade.G), Mix(p.B, ground.Shade.B), p.A)).ToArray());
                    }
                    path = tinted;
                }
                _groundImages[ground].Source = new Bitmap(path);
            }
        }
        catch (Exception e) when (e is IOException or InvalidDataException or ArgumentException or UnauthorizedAccessException)
        {
            // The pictures are only previews; the buttons keep their colour and name.
            Spyro.Editor.Core.Diagnostics.EditorDiagnostics.RecordWarning("Ground pictures", e.Message);
        }
        finally { _groundImagesLoading = false; }
    }

    private async Task ChooseGroundAsync(GroundType ground)
    {
        if (_freshSceneDocument == null || _currentGeometry == null)
        {
            _statusText.Text = "Ground painting works in a level you build: File ▾ › New Level or Start Blank.";
            return;
        }
        // While painting, another ground only changes the paint (the last tile clicked is selected, and must keep the
        // ground it was given); otherwise the ground goes on the selected tiles first.
        bool painting = _activeGround != null;
        StopTerrainTexturePaintMode(announce: false);
        List<TerrainPolygon> selected = painting ? [] : SelectedTilesForTypeChange();
        _activeGround = ground;
        _viewport.TerrainTexturePaintMode = true;
        _viewport.TerrainTexturePaintHint = ground.Name.ToLowerInvariant();
        if (selected.Count > 0)
        {
            await PaintGroundAsync(selected, ground);
            ClearGroundSelection();
            if (_activeGround == ground)
                _statusText.Text = $"{Count(selected.Count, "tile is", "tiles are")} {ground.Name.ToLowerInvariant()} now. Click more tiles to paint them; Esc stops.";
        }
        else
            _statusText.Text = $"Painting {ground.Name.ToLowerInvariant()}: click tiles in the view. Esc stops.";
        RefreshGroundCard();
    }

    /// <summary>Paints the face clicked in the view while a ground is chosen; false when no ground is (texture painting
    /// of an original level handles the click then).</summary>
    private async Task<bool> PaintGroundFromViewportAsync(TerrainPolygon face)
    {
        if (_activeGround is not { } ground)
            return false;
        if (_freshSceneDocument == null || face.IsTerrainRemoved || face.IsTerrainAddClone)
            return true;
        await PaintGroundAsync([face], ground);
        _statusText.Text = $"Painted {ground.Name.ToLowerInvariant()}. Keep clicking tiles; Esc stops.";
        return true;
    }

    /// <summary>Gives faces a ground's texture, shade and kind (texture first: a new texture clears a staged kind), then
    /// saves the level as Tile Type does.</summary>
    internal async Task PaintGroundAsync(IReadOnlyList<TerrainPolygon> faces, GroundType ground)
    {
        if (_freshSceneDocument == null)
            return;
        var material = ground.Material(_retailCatalog);
        var surfaces = _freshSceneDocument.Surfaces.ToArray();
        foreach (TerrainPolygon face in faces)
        {
            if (face.SectorIndex < 0 || face.SectorIndex >= surfaces.Length)
                continue;
            surfaces[face.SectorIndex] = surfaces[face.SectorIndex] with { Material = material, Color = ground.Shade };
            face.ApplyTextureOverride(material.TextureId);
            face.ApplyShade(ground.Shade);
        }
        _freshSceneDocument = _freshSceneDocument with { Surfaces = surfaces };
        var kind = ground.Kind is { } k ? new NativeTerrainSurfaceSignature(k.Type, k.Param1, k.Param2) : NativeTerrainSurfaceSignature.Ordinary;
        ApplyBuiltTileType(faces, kind);
        _viewport.InvalidateVisual();
        _ = RefreshFreshMaterialPreviewAsync();
        await SaveFreshSceneAsync();
    }

    /// <summary>Unselects the tiles a ground was just put on, so the next ground does not change them again.</summary>
    private void ClearGroundSelection()
    {
        _viewport.ClearTerrainMultiSelection();
        _selectedTerrain = null;
        _selectedTerrainIndex = -1;
        _selectedTerrainPointIndex = -1;
        _viewport.InvalidateVisual();
    }

    private void StopGroundPainting(bool announce)
    {
        bool was = _activeGround != null;
        _activeGround = null;
        if (was)
        {
            _viewport.TerrainTexturePaintMode = false;
            _viewport.TerrainTexturePaintHint = "";
            if (announce)
                _statusText.Text = "Stopped painting. Clicks select tiles again.";
        }
        RefreshGroundCard();
    }
}
