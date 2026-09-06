using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;

internal static class OwnerLedgerSourceCases
{
    internal static void Run(string sourcePath, string workspaceRoot, Action<bool, string> check, Action<Action, string> reject)
    {
        var catalog = LevelCatalog.Load(workspaceRoot);
        var source = NativeMobyPersistentOwnerSource.Capture(sourcePath, catalog, 12);
        var actual = NativeMobySourceInventory.Capture(sourcePath, catalog);
        var exactDonor = actual.Levels.Single(l => l.LevelId == 34).Rows.Single(r => r.TrueIndex == 6);
        check(source.LevelId == 12 && source.SaveSlot == 2 && source.OriginalRowCount == 128 &&
              source.SourceImageSha256 == "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37",
            "Exact original Dark row/save identity was not source-issued.");
        // Row selection is intent, not a caller certificate. The factory reads
        // the complete native source and resolves the actual donor variant.
        var donor = source.BindFamily([new NativeMobyPersistentOwnerRoleSelection("actor", 34, 6)]);
        var first = source.Empty().Append([new("z", donor)]);
        var again = NativeMobyPersistentOwnerSource.Capture(sourcePath, catalog, 12);
        var reopen = again.Restore(first.Serialize());
        var freshDonor = again.BindFamily([new NativeMobyPersistentOwnerRoleSelection("actor", 34, 6)]);
        var result = reopen.Append([new("a", freshDonor)]);
        check(result.Entries.Single(e => e.FamilyId == "z").NativeIndex == 128 &&
              result.Entries.Single(e => e.FamilyId == "a").NativeIndex == 129 && result.HighWater == 130,
            "Source recapture/rebind renumbered a prior persistent owner.");
        check(result.SourceCertified && !result.NativeEmissionApproved && !result.RuntimeVerified,
            "Source identity was not separated from native materialization/runtime authority.");
        check(result.Entries.All(e => e.NativeClass == 0x11B && e.SourceVariantId == exactDonor.SourceVariantId),
            "Source-selected roles did not bind the real Wizard row variant.");
        foreach (Func<LevelDefinition, LevelDefinition> mutate in new Func<LevelDefinition, LevelDefinition>[]
        {
            l => l with { SourceRecordCount = l.SourceRecordCount - 1 },
            l => l with { SourceWadEntry = l.SourceWadEntry + 2 },
            l => l with { SourceTableWadOffset = "0x00000000" },
            l => l with { SourceTableRelativeOffset = "0x00000000" }
        })
        {
            var forged = new LevelCatalog(catalog.Levels.Select(l => l.LevelId == 12 ? mutate(l) : l));
            reject(() => NativeMobyPersistentOwnerSource.Capture(sourcePath, forged, 12),
                "caller catalog locator/count metadata is not source authority");
        }
        var donorForged = new LevelCatalog(catalog.Levels.Select(l => l.LevelId == 34 ? l with { SourceRecordCount = 1 } : l));
        reject(() => NativeMobyPersistentOwnerSource.Capture(sourcePath, donorForged, 12), "donor inventory count cannot mint a variant");
        reject(() => NativeMobyPersistentOwnerSource.Capture(sourcePath, new LevelCatalog(catalog.Levels.Where(l => l.LevelId != 34)), 12),
            "incomplete catalog cannot omit donor/source ownership");
        var town = NativeMobyPersistentOwnerSource.Capture(sourcePath, catalog, 13);
        reject(() => town.Restore(first.Serialize()), "cross-level restore must not reuse the same bit ordinals");
        reject(() => source.BindFamily([new("actor", 34, 9999)]), "nonexistent donor row");
        reject(() => source.BindFamily([new("actor", 65, 0)]), "unissued donor level");
        reject(() => source.BindFamily([new("actor", 34, 6), new("actor", 34, 7)]), "duplicate stable source roles");
        reject(() => source.BindFamily([]), "empty source family");
        var changed = source.BindFamily([new("actor", 34, 7)]);
        reject(() => first.Append([new("z", changed)]), "source role replacement under prior identity");
        var json = JsonNode.Parse(first.Serialize())!.AsObject();
        json["entries"]![0]!["sourceVariantId"] = "caller-forged-source-certificate";
        reject(() => again.Restore(Encoding.UTF8.GetBytes(json.ToJsonString())), "serialized hashes cannot issue source authority");
        foreach (Type type in new[] { typeof(NativeMobyPersistentOwnerSource), typeof(NativeMobyPersistentOwnerFamily), typeof(NativeMobyPersistentOwnerLedger) })
            check(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance).Length == 0, "Public metadata constructor bypasses source issuer: " + type.Name);
        string tiny = Path.Combine(Path.GetTempPath(), "spyro-owner-source-" + Guid.NewGuid().ToString("N") + ".bin");
        try
        {
            File.WriteAllBytes(tiny, [0]);
            reject(() => NativeMobyPersistentOwnerSource.Capture(tiny, catalog, 12), "wrong physical image length");
        }
        finally { if (File.Exists(tiny)) File.Delete(tiny); }
    }
}
