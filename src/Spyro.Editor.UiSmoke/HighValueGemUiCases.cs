using System.Reflection;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Universal;

internal static class HighValueGemUiCases
{
    internal static void Run()
    {
        int checks = 0;
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
        string root = Directory.CreateTempSubdirectory("spyro-high-gem-ui-").FullName;
        Moby Gem(string id, GemValue value, int index) => new()
        {
            Index = index, TrueIndex = 174 + index, Type = 0x18, State = 0, IsAdded = true,
            SourceByte36 = value.IdByte & 255, SourceByte37 = value.IdByte >> 8,
            Flag4A = 0x40, Flag4B = 0xFF, Position = new(560 + index * 50, 560, 560),
            Color = value.Color, Label = value.DisplayName,
            CrossLevelTemplateId = UniversalSpecialCatalog.TemplatePrefix + id,
            CrossLevelRequiredExporterFeature = UniversalMobyExport.RequiredExporterFeature,
            CrossLevelSourceLevelKey = UniversalHighValueGems.SourceKey,
            CrossLevelSourceTrueIndex = -1, CrossLevelFamily = "universal"
        };
        var teal = Gem(UniversalHighValueGems.TealId, GemValue.Teal, 0);
        var rainbow = Gem(UniversalHighValueGems.RainbowId, GemValue.Rainbow, 1);
        var duplicate = Gem(UniversalHighValueGems.TealId, GemValue.Teal, 2);
        List<Moby> mobys = [teal, rainbow, duplicate];
        Check(mobys.Sum(m => m.TreasureValue) == 200, "editor totals include exact high values and duplicates");
        Check(mobys.All(m => m.VisualKind == MobyVisualKind.Gem && m.IsVisibleGem), "both new objects use gem selection and placement behavior");
        Check(GemValue.Known.Select(g => g.Value).SequenceEqual(new[] {1,2,5,10,25}), "retail reward selectors remain unchanged");
        Check(mobys.Select(m => m.UniversalPlacementId).Distinct().Count() == 3, "duplicates receive unique collected identities");
        var entries = UniversalSpecialCatalog.Entries(false).Where(e => e.Default.SourceLevelKey == UniversalHighValueGems.SourceKey).ToArray();
        Check(entries.Length == 2 && entries.All(e => e.RestrictionReason == null && e.SubCategory == "Gems"), "both placeable catalog entries work without Japanese disc");
        var editable = typeof(MainWindow).GetMethod("HasEditableGemValue", BindingFlags.Static | BindingFlags.NonPublic)!;
        Check(mobys.All(m => !(bool)editable.Invoke(null, [m])!), "retail reward editor cannot corrupt special gem identity");
        bool refused = false;
        try { teal.ApplyGem(GemValue.Red); } catch (InvalidOperationException) { refused = true; }
        Check(refused && teal.TreasureValue == 50, "direct retail recolor preserves special value");
        string path = Path.Combine(root, "artisans-native-edits.json");
        MobyEditStore.SaveAsync(path, mobys, "Artisans").GetAwaiter().GetResult();
        List<Moby> reopened = [];
        MobyEditStore.Load(path, reopened);
        Check(reopened.Sum(m => m.TreasureValue) == 200 && reopened.All(m => m.IsHighValueGem), "saved retail project reopens both values and colors");
        var before = UniversalPlacementReader.Read(path);
        Check(before.All(p => p.SourceLevelKey == UniversalHighValueGems.SourceKey && UniversalHighValueGems.IsId(p.SpecialId)), "export reader retains USA custom source and special identity");
        reopened.Single(m => m.CrossLevelTemplateId.EndsWith(UniversalHighValueGems.RainbowId)).IsRemoved = true;
        reopened.Reverse();
        MobyEditStore.SaveAsync(path, reopened, "Artisans").GetAwaiter().GetResult();
        var after = UniversalPlacementReader.Read(path);
        Check(after.Count == 3 && after.Count(p => p.Retired) == 1, "removal retains durable tombstone");
        Check(after.All(p => before.Single(b => b.EditorId == p.EditorId).PersistentSlot == p.PersistentSlot), "reorder and remove retain logical save rows");
        Check(reopened.Where(m => !m.IsRemoved).Sum(m => m.TreasureValue) == 100, "removed gem leaves editor available total");
        var fresh = SceneDocument.New("Gem Test");
        fresh = fresh.Capture(fresh.ProjectGeometry(), mobys);
        string scene = Path.Combine(root, SceneDocument.FileName);
        File.WriteAllBytes(scene, fresh.Serialize());
        var loaded = SceneDocument.Read(scene);
        Check(loaded.ProjectObjects().Sum(m => m.TreasureValue) == 200, "fresh document preserves higher values");
        Check(loaded.Ledger.Entries.Select(e => e.Id).SequenceEqual(fresh.Ledger.Entries.Select(e => e.Id)), "fresh reload preserves collection identities");
        Check(MobyGemIconCatalog.HitRadius(12, GemValue.Rainbow) >= 18 && MobyGemIconCatalog.HitRadius(12, GemValue.Teal) >= 18, "new viewport markers keep usable hit regions");
        Console.WriteLine($"HIGH_VALUE_GEM_UI_OK checks={checks}");
    }
}
