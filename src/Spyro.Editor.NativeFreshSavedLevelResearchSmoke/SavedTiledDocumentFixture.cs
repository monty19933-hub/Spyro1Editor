using Spyro.Editor.Core.Editing;

// Authored intent only, shared by the source and explicit physical gates. No
// captured source asset, image hash or private compilation grants fixture identity.
internal static class SavedTiledDocumentFixture
{
    internal static NativeFreshLevelDocumentIntent Document(string? firstId = null) => Make(
        Enumerable.Range(0, 3).Select(i => Tile(i, 8448 + i * 2048, 8448, 8704, 2048, 2048,
            i == 0 ? firstId : null)), 8960, 8960, 9216);

    internal static NativeFreshLevelDocumentIntent BoundaryDocument(int zCell) => Make(
        Enumerable.Range(0, 10).Select(i => Tile(i, i * 16, 0, zCell * 4096, 16, 16)),
        8, 8, zCell * 4096 + 512);

    internal static IReadOnlyList<(string Name, NativeFreshLevelDocumentIntent Document)> Unsupported()
    {
        var document = Document();
        var content = document.TiledContent!;
        List<(string, NativeFreshLevelDocumentIntent)> result = [];
        foreach (int tileIndex in new[] { 0, 2 })
            foreach (int face in new[] { 0, 1 })
            {
                var tiles = content.Tiles.ToArray(); var tile = tiles[tileIndex];
                var color = NativeFreshFloorMaterialIntent.BaseFloorColor();
                var retail = NativeFreshFloorMaterialIntent.RetailTerrain("spyro1-usa-retail-mode2-2352-v1", 14, 1);
                tiles[tileIndex] = NativeFreshFlatTileIntent.Create(tile.EditorId, tile.X, tile.Y, tile.Z,
                    tile.Width, tile.Depth, tile.FloorColor,
                    NativeFreshFloorMaterialsIntent.Create(face == 0 ? retail : color, face == 1 ? retail : color));
                result.Add(($"tile{tileIndex}-face{face}-retail", document.WithTiledContent(content.WithTiles(tiles))));
            }
        result.Add(("wizard", document.WithPlacements([NativeFreshWizardRouteIntent.Create(
            new string('a', 32), 13056, 8960, 8704, 13568, 8960)])));
        result.Add(("native-moby", document.WithPlacements([NativeFreshMobyPlacementIntent.Create(
            new string('b', 32), NativeFreshRetailMobyReference.Create("spyro1-usa-retail-mode2-2352-v1", 10, 121, 501),
            13056, 8960, 8704, 64)])));
        result.Add(("actual-data1004", BoundaryDocument(48)));
        return result;
    }

    private static NativeFreshFlatTileIntent Tile(int index, int x, int y, int z, int width, int depth, string? id = null) =>
        NativeFreshFlatTileIntent.Create(id ?? (index + 1).ToString("x32"), x, y, z, width, depth, new(80, 128, 112),
            NativeFreshFloorMaterialsIntent.Create(NativeFreshFloorMaterialIntent.BaseFloorColor(), NativeFreshFloorMaterialIntent.BaseFloorColor()));

    private static NativeFreshLevelDocumentIntent Make(IEnumerable<NativeFreshFlatTileIntent> tiles, int x, int y, int z) =>
        NativeFreshLevelDocumentIntent.CreateTiled(NativeFreshTiledLevelContent.Create(tiles, x, y, z, 64, new(128, 96, 64), 19), []);
}
