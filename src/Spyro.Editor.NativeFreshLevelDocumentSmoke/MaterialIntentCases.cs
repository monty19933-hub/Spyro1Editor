using System.Reflection;
using Spyro.Editor.Core.Editing;
using static Cases;

// Neutral material authoring only; no source or native eligibility.
internal static class MaterialIntentCases
{
    internal const string Variant = "spyro1-usa-retail-mode2-2352-v1";
    internal static NativeFreshFloorMaterialIntent Color() => NativeFreshFloorMaterialIntent.BaseFloorColor();
    internal static NativeFreshRetailTerrainMaterialIntent Retail(int entry = 14, int id = 0) =>
        NativeFreshFloorMaterialIntent.RetailTerrain(Variant, entry, id);
    internal static NativeFreshFloorMaterialsIntent Pair() => NativeFreshFloorMaterialsIntent.Create(Retail(), Retail(70));

    internal static void Run()
    {
        var old = Document(Wizard());
        Check(old.SchemaVersion == 1 && old.FloorMaterials == null && Serialize(old) == Canonical,
            "existing Create remains exact schema1");
        var color = Color(); var first = Retail(); var second = Retail(70);
        Check(color.Tag == "base-floor-color" && first.Tag == "retail-terrain-record" &&
            first.SourceVariant == Variant && first.SourceWadEntry == 14 && first.TextureId == 0,
            "closed neutral source identity, no actual source eligibility");
        foreach (Type type in new[] { color.GetType(), first.GetType(), typeof(NativeFreshFloorMaterialsIntent) })
            Check(type.IsSealed && type.GetConstructors().Length == 0 &&
                type.GetProperties().All(p => p.SetMethod == null), "sealed immutable material values");
        Check(typeof(NativeFreshFloorMaterialIntent).IsAbstract &&
            typeof(NativeFreshFloorMaterialIntent).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
                .All(c => c.IsFamilyAndAssembly), "external subclasses cannot issue a new material tag");
        var pair = NativeFreshFloorMaterialsIntent.Create(first, second);
        var modern = old.WithFloorMaterials(pair);
        Check(modern.SchemaVersion == 2 && ReferenceEquals(modern.FloorMaterials, pair) &&
            ReferenceEquals(pair.Triangle0, first) && ReferenceEquals(pair.Triangle1, second) &&
            ReferenceEquals(modern.Base, old.Base) && ReferenceEquals(modern.Placements[0], old.Placements[0]),
            "explicit upgrade retains actual immutable content and ordered slots");
        var reversed = modern.WithFloorMaterials(NativeFreshFloorMaterialsIntent.Create(second, first));
        Check(Serialize(reversed) != Serialize(modern), "material slot order is semantic, never sorted");
        var same = old.WithFloorMaterials(NativeFreshFloorMaterialsIntent.Create(first, first));
        Check(ReferenceEquals(same.FloorMaterials!.Triangle0, same.FloorMaterials.Triangle1), "same selection may occupy both slots");
        var clear = modern.WithFloorMaterials(NativeFreshFloorMaterialsIntent.Create(color, color));
        Check(clear.SchemaVersion == 2 && clear.FloorMaterials != null, "explicit both-color clear never downgrades");
        Check(old.SchemaVersion == 1 && old.FloorMaterials == null && Serialize(old) == Canonical,
            "material edit never mutates the prior v1 object or bytes");
        var rows = new List<NativeFreshPlacementIntent> { Wizard(Id2), Wizard() };
        var edited = modern.WithContent(Base(20), rows);
        rows.Clear();
        Check(edited.SchemaVersion == 2 && ReferenceEquals(edited.FloorMaterials, pair) &&
            edited.Base.MusicTrackId == 20 && edited.Placements.Select(p => p.EditorId).SequenceEqual(new[] { Id2, Id }) &&
            edited.Placements.Cast<NativeFreshWizardRouteIntent>().All(p => p.OriginZ == 8909),
            "WithContent copies rows, preserves material identity, IDs/order/Z and edited Base");
        Check(old.WithContent(old.Base, []).SchemaVersion == 1 &&
            modern.WithContent(modern.Base, []).FloorMaterials == pair, "empty content preserves each version");
        var all = modern.WithContent(modern.Base, Enumerable.Range(1, 128).Select(i => Wizard(i.ToString("x32"))));
        Check(all.Placements.Count == 128 && all.FloorMaterials == pair, "128 placements independent from two material slots");
        int visited = 0; bool disposed = false;
        IEnumerable<NativeFreshPlacementIntent> TooMany()
        {
            try { for (int i = 1; i <= 130; i++) { visited++; yield return Wizard(i.ToString("x32")); } }
            finally { disposed = true; }
        }
        Reject(() => modern.WithContent(modern.Base, TooMany()), "WithContent bounded enumeration");
        Check(visited == 129 && disposed, "refuse/dispose on129th without unbounded material copy path");
        Reject(() => modern.WithContent(modern.Base, [Wizard(), Wizard()]), "duplicate ID still refused");
        Reject(() => modern.WithContent(modern.Base, [Wizard(x: 8448)]), "strict interior still required");
        Reject(() => modern.WithContent(null!, []), "null Base");
        Reject(() => modern.WithContent(modern.Base, null!), "null placements");
        Reject(() => modern.WithFloorMaterials(null!), "null materials cannot request downgrade");
        Reject(() => NativeFreshFloorMaterialsIntent.Create(null!, color), "missing triangle0");
        Reject(() => NativeFreshFloorMaterialsIntent.Create(color, null!), "missing triangle1");
        foreach (string? variant in new string?[] { null, "", "Spyro1-usa-retail-mode2-2352-v1", "spyro1-usa-retail-mode2-2352-v2", Variant + " " })
            Reject(() => NativeFreshFloorMaterialIntent.RetailTerrain(variant!, 14, 0), "closed exact variant");
        foreach (int entry in new[] { int.MinValue, -2, 0, 8, 11, 77, 80, int.MaxValue })
            Reject(() => Retail(entry), "neutral donor integer domain");
        foreach (int id in new[] { int.MinValue, -1, 128, int.MaxValue })
            Reject(() => Retail(14, id), "neutral texture ID domain");
        foreach (int entry in Enumerable.Range(0, 35).Select(i => 10 + i * 2))
            foreach (int id in new[] { 0, 127 })
                Check(Retail(entry, id).SourceWadEntry == entry && Retail(entry, id).TextureId == id,
                    "neutral descriptor may be unresolved/nonstatic; no ROM or catalog consulted");
    }
}
