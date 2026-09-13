using Spyro.Editor.Core.Editing;

// One source-free authored fixture shared by saved Core and actual Window tests.
// This is an intent, not a captured asset, source certificate or runtime proof.
internal static class SavedFamilyDocumentFixture
{
    internal const string WizardId = "11111111111111111111111111111111";
    internal const string GrassId = "22222222222222222222222222222222";

    internal static NativeFreshLevelDocumentIntent Document(int entry = 10, int index = 121, int nativeClass = 501,
        int x = 9152, int y = 9152, int z = 8704, int yaw = 64)
    {
        var floor = Floor();
        var old = NativeFreshLevelDocumentIntent.Create(floor,
            [NativeFreshWizardRouteIntent.Create(WizardId, 9984, 9984, 8704, 9472, 9984)])
            .WithFloorMaterials(Pair(14, 1, 70, 1)).UpgradeForNativeMobys();
        return old.WithContent(old.Base, [old.Placements[0],
                NativeFreshMobyPlacementIntent.Create(GrassId,
                    NativeFreshRetailMobyReference.Create(NativeFreshRetailTerrainMaterialIntent.SupportedSourceVariant,
                        entry, index, nativeClass), x, y, z, yaw)]);
    }

    internal static IReadOnlyList<(string Name, NativeFreshLevelDocumentIntent Document)> Unsupported()
    {
        var valid = Document();
        var grass = (NativeFreshMobyPlacementIntent)valid.Placements[1];
        return [
            ("entry", Document(entry: 14)), ("index", Document(index: 122)), ("class", Document(nativeClass: 500)),
            ("x", Document(x: 9153)), ("y", Document(y: 9153)), ("z", Document(z: 8705)), ("yaw", Document(yaw: 65)),
            ("order", valid.WithContent(valid.Base, valid.Placements.Reverse())),
            ("wizard-only", valid.WithContent(valid.Base, [valid.Placements[0]])),
            ("grass-only", valid.WithContent(valid.Base, [grass])), ("empty", valid.WithContent(valid.Base, [])),
            ("second-grass", valid.WithContent(valid.Base, [.. valid.Placements,
                NativeFreshMobyPlacementIntent.Create(new string('3', 32), grass.Source, 9200, 9200, 8704, 64)])),
            // A catalogue reference to a Wizard is still native-moby, never a wizard-route.
            ("native-wizard", valid.WithContent(valid.Base, [NativeFreshMobyPlacementIntent.Create(WizardId,
                NativeFreshRetailMobyReference.Create(grass.Source.SourceVariant, 14, 6, 33), 9984, 9984, 8704, 64), grass])),
            ("pair-reversed", valid.WithFloorMaterials(Pair(70, 1, 14, 1))),
            ("pair-repeated", valid.WithFloorMaterials(Pair(14, 1, 14, 1))),
            ("pair-other", valid.WithFloorMaterials(Pair(70, 1, 70, 1))),
            ("first-t2", valid.WithFloorMaterials(Pair(14, 2, 70, 1))),
            ("second-t2", valid.WithFloorMaterials(Pair(14, 1, 70, 2))),
            ("first-color", valid.WithFloorMaterials(NativeFreshFloorMaterialsIntent.Create(
                NativeFreshFloorMaterialIntent.BaseFloorColor(), Pair(14, 1, 70, 1).Triangle1))),
            ("second-color", valid.WithFloorMaterials(NativeFreshFloorMaterialsIntent.Create(
                Pair(14, 1, 70, 1).Triangle0, NativeFreshFloorMaterialIntent.BaseFloorColor()))),
            ("no-materials", NativeFreshLevelDocumentIntent.Create(valid.Base, []).UpgradeForNativeMobys()
                .WithContent(valid.Base, valid.Placements)),
            ("floor-x", valid.WithContent(Floor(x: 8464), valid.Placements)),
            ("floor-y", valid.WithContent(Floor(y: 8464), valid.Placements)),
            ("floor-z", valid.WithContent(Floor(z: 8720), valid.Placements)),
            ("floor-width", valid.WithContent(Floor(width: 1920), valid.Placements)),
            ("floor-depth", valid.WithContent(Floor(depth: 1920), valid.Placements)),
            ("spawn-x", valid.WithContent(Floor(spawnX: 8976), valid.Placements)),
            ("spawn-y", valid.WithContent(Floor(spawnY: 8976), valid.Placements)),
            ("heading", valid.WithContent(Floor(heading: 65), valid.Placements)),
            ("music", valid.WithContent(Floor(music: 20), valid.Placements)),
            ("floor-color", valid.WithContent(Floor(floorRed: 81), valid.Placements)),
            ("sky-color", valid.WithContent(Floor(skyRed: 129), valid.Placements)),
            ("route-origin", valid.WithContent(valid.Base,
                [NativeFreshWizardRouteIntent.Create(WizardId, 10000, 9984, 8704, 9472, 9984), grass])),
            ("route-z", valid.WithContent(valid.Base,
                [NativeFreshWizardRouteIntent.Create(WizardId, 9984, 9984, 8705, 9472, 9984), grass])),
            ("route-end", valid.WithContent(valid.Base,
                [NativeFreshWizardRouteIntent.Create(WizardId, 9984, 9984, 8704, 9456, 9984), grass]))
        ];
    }

    private static NativeId65FreshDocumentIntent Floor(int x = 8448, int y = 8448, int z = 8704,
        int width = 2048, int depth = 2048, int spawnX = 8960, int spawnY = 8960, int heading = 64,
        int music = 19, byte floorRed = 80, byte skyRed = 128) => NativeId65FreshDocumentIntent.Create(
            x, y, z, width, depth, spawnX, spawnY, heading, new(floorRed, 128, 112), new(skyRed, 96, 64), music);

    private static NativeFreshFloorMaterialsIntent Pair(int firstEntry, int firstTexture, int secondEntry, int secondTexture) =>
        NativeFreshFloorMaterialsIntent.Create(
            NativeFreshFloorMaterialIntent.RetailTerrain(NativeFreshRetailTerrainMaterialIntent.SupportedSourceVariant, firstEntry, firstTexture),
            NativeFreshFloorMaterialIntent.RetailTerrain(NativeFreshRetailTerrainMaterialIntent.SupportedSourceVariant, secondEntry, secondTexture));
}
