using Spyro.Editor.Core.Editing;

// Neutral authored intent shared by saved/source/physical/UI tests. Neither
// metadata nor a fixture hash grants original-source or publication authority.
internal static class SavedTiledMaterialDocumentFixture
{
    private const string Variant = "spyro1-usa-retail-mode2-2352-v1";
    internal static NativeFreshLevelDocumentIntent Document(int[]? bindings = null, string? firstId = null)
    {
        bindings ??= [0, 1, 0, 1, 0, 1];
        if (bindings.Length != 6) throw new ArgumentException("The reviewed fixture owns all six face bindings.", nameof(bindings));
        return Make(Enumerable.Range(0, 3).Select(i => Tile(i, 8448 + i * 2048, 8448, 8704, 2048, 2048,
            bindings[2 * i], bindings[2 * i + 1], i == 0 ? firstId : null)), 8960, 8960, 9216);
    }

    internal static NativeFreshLevelDocumentIntent BoundaryDocument(int zCell) => Make(
        Enumerable.Range(0, 8).Select(i => Tile(i, i * 16, 0, zCell * 4096, 16, 16, 0, 1)),
        8, 8, zCell * 4096 + 512);

    internal static IReadOnlyList<(string Name, NativeFreshLevelDocumentIntent Document)> Unsupported()
    {
        var document = Document();
        return new (string, NativeFreshLevelDocumentIntent)[]
        {
            ("one-descriptor", Document([0, 255, 0, 255, 0, 255])),
            ("third-descriptor-last-face", Document([0, 1, 0, 1, 0, 2])),
            ("wizard", document.WithPlacements([NativeFreshWizardRouteIntent.Create(
                new string('a', 32), 13056, 8960, 8704, 13568, 8960)])),
            ("native-moby", document.WithPlacements([NativeFreshMobyPlacementIntent.Create(
                new string('b', 32), NativeFreshRetailMobyReference.Create(Variant, 10, 0, 0x78), 13056, 8960, 8704, 64)])),
            ("actual-data1004", BoundaryDocument(60))
        };
    }

    private static NativeFreshFlatTileIntent Tile(int index, int x, int y, int z, int width, int depth,
        int first, int second, string? id = null) => NativeFreshFlatTileIntent.Create(id ?? (index + 1).ToString("x32"),
            x, y, z, width, depth, new(80, 128, 112), NativeFreshFloorMaterialsIntent.Create(Material(first), Material(second)));
    private static NativeFreshFloorMaterialIntent Material(int slot) => slot == 255 ? NativeFreshFloorMaterialIntent.BaseFloorColor() :
        NativeFreshFloorMaterialIntent.RetailTerrain(Variant, slot switch { 0 => 14, 1 => 70, 2 => 10,
            _ => throw new ArgumentOutOfRangeException(nameof(slot)) }, 0);
    private static NativeFreshLevelDocumentIntent Make(IEnumerable<NativeFreshFlatTileIntent> tiles, int x, int y, int z) =>
        NativeFreshLevelDocumentIntent.CreateTiled(NativeFreshTiledLevelContent.Create(tiles, x, y, z, 64, new(128, 96, 64), 19), []);
}
