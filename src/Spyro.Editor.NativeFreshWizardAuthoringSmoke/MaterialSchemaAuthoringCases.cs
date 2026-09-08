using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;

// Called ONLY inside the existing --source original loop,
// immediately after actual document/compilation/family/resource issuance and before Scene capture.
internal static class MaterialSchemaAuthoringCases
{
    internal static async Task Source(NativeFreshLevelDocumentIntent schema1,
        NativeId65FreshDocumentCompilation compilation, NativeWizardFamilyAssetPlan family,
        NativeId65FreshWizardResearchResourcePlan resource, Action<bool,string> check)
    {
        var expected = NativeFreshWizardAuthoringShape.RequireDocument(schema1, compilation);
        check(schema1.SchemaVersion == 1 && schema1.FloorMaterials == null &&
            expected.OriginZ == ((NativeFreshWizardRouteIntent)schema1.Placements[0]).OriginZ,
            "actual current schema1 ancestry remains admitted");
        var color = NativeFreshFloorMaterialIntent.BaseFloorColor();
        var terrain = NativeFreshFloorMaterialIntent.RetailTerrain("spyro1-usa-retail-mode2-2352-v1", 14, 0);
        foreach (var materials in new[]
        {
            NativeFreshFloorMaterialsIntent.Create(terrain, terrain),
            NativeFreshFloorMaterialsIntent.Create(terrain, color),
            NativeFreshFloorMaterialsIntent.Create(color, color)
        })
        {
            var schema2 = schema1.WithFloorMaterials(materials);
            check(ReferenceEquals(schema2.Base, compilation.Intent) && schema2.Placements.Count == 1,
                "rejection fixture has genuine matching issued Base/route, not unrelated ancestry");
            try
            {
                await NativeId65FreshWizardResearchScenePlan.CaptureAuthoredAsync(
                    "must-not-open-schema2-source", schema2, compilation, family, resource);
            }
            catch (InvalidDataException error) when (error.Message.Contains("schema 1", StringComparison.Ordinal))
            {
                check(true, "existing Wizard explicitly refuses schema2 before source IO, including both-color clear");
                continue;
            }
            throw new InvalidDataException("Existing Wizard must refuse schema2 with an explicit schema 1 eligibility message.");
        }
    }
}
