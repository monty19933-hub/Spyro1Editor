using System.Text;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using static Cases;

internal static class FamilyCodecCases
{
    // Independent canonical literals. Neither codec output nor native source bytes define these expectations.
    internal const string FamilyId = "00000000000000000000000000000002";
    internal const string SourceJson = "{\"sourceVariant\":\"spyro1-usa-retail-mode2-2352-v1\",\"definitionId\":\"peace-keepers-locked-chest-v1\"}";
    internal const string KeyJson = "{\"x\":8705,\"y\":8706,\"z\":0}";
    internal const string ChestJson = "{\"x\":9000,\"y\":9001,\"z\":8704}";
    internal const string FamilyJson = "{\"id\":\"" + FamilyId + "\",\"tag\":\"locked-chest-family\",\"source\":" + SourceJson +
        ",\"key\":" + KeyJson + ",\"chest\":" + ChestJson + ",\"rewardDestinations\":null}";
    internal const string LegacyContentJson = "{\"tag\":\"legacy-floor\",\"base\":" + BaseJson + ",\"floorMaterials\":null}";
    internal const string TiledContentJson = "{\"tag\":\"ordered-flat-tiles-v1\",\"tiles\":[" + TiledCodecCases.TileJson +
        "],\"spawn\":" + TiledCodecCases.SpawnJson + ",\"skyRgb\":[128,96,64],\"musicTrackId\":19}";
    internal const string Golden = "{\"kind\":\"spyro-native-fresh-level\",\"schema\":5,\"content\":" + LegacyContentJson +
        ",\"placements\":[" + FamilyJson + "]}";
    internal const string TiledGolden = "{\"kind\":\"spyro-native-fresh-level\",\"schema\":5,\"content\":" + TiledContentJson +
        ",\"placements\":[" + FamilyJson + "]}";
    internal const string Reward2Json = "{\"role\":\"reward.2\",\"x\":0,\"y\":262128,\"z\":17}";
    internal const string Reward4Json = "{\"role\":\"reward.4\",\"x\":8590,\"y\":9616,\"z\":9216}";
    internal const string ExplicitFamilyJson = "{\"id\":\"" + FamilyId + "\",\"tag\":\"locked-chest-family\",\"source\":" + SourceJson +
        ",\"key\":" + KeyJson + ",\"chest\":" + ChestJson + ",\"rewardDestinations\":[" + Reward2Json + "," + Reward4Json + "]}";
    internal const string ExplicitGolden = "{\"kind\":\"spyro-native-fresh-level\",\"schema\":5,\"content\":" + LegacyContentJson +
        ",\"placements\":[" + ExplicitFamilyJson + "]}";

    internal static NativeFreshLockedChestFamilyPlacementIntent Family(string id = FamilyId,
        NativeFreshChestRewardDestinationOverrides? destinations = null) => NativeFreshLockedChestFamilyPlacementIntent.Create(id,
            NativeFreshLockedChestFamilyReference.Create(MaterialIntentCases.Variant, "peace-keepers-locked-chest-v1"),
            8705, 8706, 0, 9000, 9001, 8704, destinations);

    internal static NativeFreshLevelDocumentIntent Value(bool tiled = false) =>
        (tiled ? TiledCodecCases.Value() : Document()).UpgradeForFamilies().WithPlacements([Family()]);

    internal static void Run()
    {
        Check(Serialize(Value()) == Golden && Serialize(Value(true)) == TiledGolden,
            "schema5 literal root/content/family order for both actual content variants without source projection");
        foreach (string text in new[] { Golden, TiledGolden, ExplicitGolden })
        {
            var parsed = Parse(text);
            var family = (NativeFreshLockedChestFamilyPlacementIntent)parsed.Placements.Single();
            Check(parsed.SchemaVersion == 5 && family.Source.SourceVariant == MaterialIntentCases.Variant &&
                family.Source.DefinitionId == "peace-keepers-locked-chest-v1" && family.KeyX == 8705 && family.KeyY == 8706 &&
                family.KeyZ == 0 && family.ChestX == 9000 && family.ChestY == 9001 && family.ChestZ == 8704 &&
                Serialize(parsed) == text, "literal schema5 parse retains neutral raw anchors, source name and exact canonical bytes");
            Check(parsed.WithPlacements([]).SchemaVersion == 5, "removing final family cannot downgrade parsed schema5");
        }
        Check(Parse(Golden).LegacyContent != null && Parse(Golden).TiledContent == null &&
            Parse(TiledGolden).TiledContent != null && Parse(TiledGolden).LegacyContent == null,
            "schema5 selects exactly one actual content authority");
        Check(Serialize(Parse(Golden.Replace("\"floorMaterials\":null", "\"floorMaterials\":" + MaterialCodecCases.PairJson))) ==
            Golden.Replace("\"floorMaterials\":null", "\"floorMaterials\":" + MaterialCodecCases.PairJson),
            "legacy schema5 retains existing material pair grammar and explicit nullable distinction");
        string mixed = Golden.Replace(FamilyJson, PlacementJson + "," + FamilyJson + "," + MobyCodecCases.MobyJson);
        Check(Serialize(Value().WithPlacements([Wizard(), Family(), MobyIntentCases.Moby()])) == mixed &&
            Serialize(Parse(mixed)) == mixed, "all three placement variants share one ordered schema5 list");

        string reordered = " {\"placements\":[" + FamilyJson + "],\"content\":" + LegacyContentJson +
            ",\"schema\":5,\"kind\":\"spyro-native-fresh-level\"}\r\n";
        Check(Serialize(Parse(reordered)) == Golden && Serialize(Parse(Golden.Replace("\"definitionId\":", "\"definition\\u0049d\":"))) == Golden,
            "schema5 whitespace/property order/valid escaped spelling canonicalize without implicit migration");
        string reverseRewards = ExplicitGolden.Replace(Reward2Json + "," + Reward4Json, Reward4Json + "," + Reward2Json);
        Check(Serialize(Parse(reverseRewards)) == ExplicitGolden,
            "reward destinations canonicalize by immutable semantic role, not incoming array order or coordinate order");
        var explicitValues = NativeFreshChestRewardDestinationOverrides.Create([
            new(NativeFreshChestRewardRole.Reward4, 8590, 9616, 9216), new(NativeFreshChestRewardRole.Reward2, 0, 262128, 17)]);
        Check(Serialize(Value().WithPlacements([Family(destinations: explicitValues)])) == ExplicitGolden,
            "typed out-of-floor WORLD destinations remain neutral authorship, not filtered by surface or source eligibility");
        var noOverrides = (NativeFreshLockedChestFamilyPlacementIntent)Parse(Golden).Placements[0];
        var explicitOverrides = (NativeFreshLockedChestFamilyPlacementIntent)Parse(ExplicitGolden).Placements[0];
        Check(noOverrides.RewardDestinations == null && explicitOverrides.RewardDestinations!.Values.Count == 2 &&
            explicitOverrides.RewardDestinations.Values[1] is { Role: NativeFreshChestRewardRole.Reward4, X: 8590, Y: 9616, Z: 9216 },
            "absence stays null; explicit source-relative-default-looking values stay explicit without native default inference");
        string five = string.Join(',', Enumerable.Range(1, 5).Select(i =>
            "{\"role\":\"reward." + i + "\",\"x\":0,\"y\":262128,\"z\":17}"));
        string fiveText = Golden.Replace("\"rewardDestinations\":null", "\"rewardDestinations\":[" + five + "]");
        Check(Serialize(Parse(fiveText)) == fiveText &&
            ((NativeFreshLockedChestFamilyPlacementIntent)Parse(fiveText).Placements[0]).RewardDestinations!.Values.Select(v => v.Role)
                .SequenceEqual(Enum.GetValues<NativeFreshChestRewardRole>()), "all five named roles are literal and ordered, never numeric selectors");

        foreach (string old in new[] { Canonical, MaterialCodecCases.Golden, MobyCodecCases.Golden,
            MobyCodecCases.MaterialGolden, TiledCodecCases.Golden })
        {
            Check(Serialize(Parse(old)) == old, "every independent schema1..4 golden remains exact without implicit upgrade");
            Reject(() => Parse(old.Replace(PlacementJson, FamilyJson)), "old schemas refuse family even with otherwise valid legacy fields");
        }
        foreach (string oldLayout in new[] { Canonical, MaterialCodecCases.Golden, MobyCodecCases.Golden, TiledCodecCases.Golden })
            Reject(() => Parse(oldLayout.Replace("\"schema\":" + Parse(oldLayout).SchemaVersion, "\"schema\":5")),
                "schema5 refuses old root shapes instead of silently migrating their authority");
        foreach (int version in new[] { 0, 1, 2, 3, 4, 6 })
            Reject(() => Parse(Golden.Replace("\"schema\":5", "\"schema\":" + version)), "exact explicit schema5 root only");

        foreach (var fixture in new[] { (Golden, LegacyContentJson), (TiledGolden, TiledContentJson), (ExplicitGolden, Reward2Json) })
            foreach (string owner in new[] { fixture.Item1, fixture.Item2, FamilyJson, SourceJson, KeyJson, ChestJson })
            {
                if (!fixture.Item1.Contains(owner, StringComparison.Ordinal)) continue;
                int comma = owner.IndexOf(','); string first = owner[1..comma]; string key = first[..first.IndexOf(':')];
                foreach (string changed in new[] { owner[..^1] + ",\"unknown\":0}", "{" + owner[(comma + 1)..],
                    "{" + first + "," + owner[1..], owner.Replace(key + ":", key.ToUpperInvariant() + ":") })
                    Reject(() => Parse(fixture.Item1.Replace(owner, changed)), "each family/content owner independently refuses unknown/missing/duplicate/case fields");
            }
        foreach (string owner in new[] { LegacyContentJson, TiledContentJson, FamilyJson, SourceJson, KeyJson, ChestJson, Reward2Json })
        {
            string baseline = owner == TiledContentJson ? TiledGolden : owner == Reward2Json ? ExplicitGolden : Golden;
            string field = owner[2..owner.IndexOf('"', 2)];
            string needle = "\"" + field + "\":";
            string escaped = "\"\\u" + ((int)field[0]).ToString("x4") + field[1..] + "\":";
            Reject(() => Parse(baseline.Replace(owner, owner.Replace(needle, needle + "null," + escaped))),
                "escaped duplicate is rejected within each new object, not hidden by another invalid owner");
        }
        foreach (string field in new[] { "sourceBin", "sourceHandle", "sha256", "nativeOwner", "rawRows", "privateBytes", "parentIndex",
            "nativeYawByte", "rewardSelector", "runtimeVerified", "publicationApproved" })
            foreach (string owner in new[] { FamilyJson, SourceJson })
                Reject(() => Parse(Golden.Replace(owner, owner[..^1] + ",\"" + field + "\":0}")),
                    "family/reference permit neutral intent only, never authority or raw-row/private overlays");
        foreach (string field in new[] { "base", "surface", "spawn", "floorMaterials", "skyRgb", "musicTrackId" })
            Reject(() => Parse(Golden[..^1] + ",\"" + field + "\":null}"), "schema5 root has no second content authority");
        Reject(() => Parse(TiledGolden.Replace(TiledContentJson, TiledContentJson[..^1] + ",\"base\":" + BaseJson + "}")),
            "tiled content cannot also contain a legacy Base");

        foreach (string bad in new[] { Golden.Replace(FamilyJson, "null"), Golden.Replace(SourceJson, "[]"), Golden.Replace(KeyJson, "null"),
            Golden.Replace(ChestJson, "[]"), Golden.Replace("locked-chest-family", "Locked-chest-family"),
            Golden.Replace("peace-keepers-locked-chest-v1", "peace-keepers-locked-chest-v2"),
            Golden.Replace(MaterialIntentCases.Variant, "unknown"), Golden.Replace(LegacyContentJson, "null"),
            Golden.Replace("legacy-floor", "unknown"), TiledGolden.Replace("ordered-flat-tiles-v1", "ordered-flat-tiles-v2"),
            Golden.Replace(",\"floorMaterials\":null", ""), Golden.Replace("\"floorMaterials\":null", "\"floorMaterials\":[]"),
            Golden.Replace(FamilyId, new string('0', 32)), Golden.Replace(FamilyJson, FamilyJson + "," + FamilyJson),
            mixed.Replace(FamilyId, Id), Golden.Replace("\"rewardDestinations\":null", "\"rewardDestinations\":[]"),
            Golden.Replace("\"rewardDestinations\":null", "\"rewardDestinations\":{}"),
            ExplicitGolden.Replace(Reward2Json, "null"), ExplicitGolden.Replace("reward.2", "Reward.2"),
            ExplicitGolden.Replace("reward.2", "reward.0"), ExplicitGolden.Replace("reward.2", "reward.6"),
            ExplicitGolden.Replace("reward.2", "reward.4"), ExplicitGolden.Replace("\"reward.2\"", "1") })
            Reject(() => Parse(bad), "closed content/family/source/role/ID/nullable grammar");
        foreach (string owner in new[] { KeyJson, ChestJson, Reward2Json })
        {
            string baseline = owner == Reward2Json ? ExplicitGolden : Golden;
            foreach (string axis in new[] { "x", "y", "z" })
            {
                int start = owner.IndexOf("\"" + axis + "\":", StringComparison.Ordinal) + 4;
                int end = owner.IndexOfAny([',', '}'], start); string number = owner[start..end];
                foreach (string invalid in new[] { "null", "true", "\"1\"", "1.0", "1e0", "-1", "262129", "2147483648", "-2147483649" })
                    Reject(() => Parse(baseline.Replace(owner, owner.Remove(start, number.Length).Insert(start, invalid))),
                        "each anchor/destination scalar independently requires integer WORLD0..262128");
            }
        }
        foreach (string owner in new[] { KeyJson, ChestJson })
        {
            string edge = owner == KeyJson ? KeyJson.Replace("8705", "8448") : ChestJson.Replace("9000", "9472");
            Reject(() => Parse(Golden.Replace(owner, edge)), "key and chest XY each require strict actual-content interior");
            string maxZ = owner == KeyJson ? KeyJson.Replace("\"z\":0", "\"z\":262128") : ChestJson.Replace("8704", "262128");
            Check(Serialize(Parse(Golden.Replace(owner, maxZ))) == Golden.Replace(owner, maxZ),
                "neutral family Z accepts WORLD262128 without ushort/grid/source-profile truncation");
        }

        byte[] input = Encoding.UTF8.GetBytes(ExplicitGolden); var retained = NativeFreshLevelDocumentCodec.Parse(input);
        Array.Fill(input, (byte)0); byte[] output = NativeFreshLevelDocumentCodec.SerializeCanonical(retained); Array.Fill(output, (byte)0);
        Check(Serialize(retained) == ExplicitGolden, "input/output buffers cannot mutate retained source/anchor/override ownership");
        Check(Serialize(Parse(Golden + new string(' ', 65536 - Encoding.UTF8.GetByteCount(Golden)))) == Golden,
            "schema5 keeps inclusive64KiB bound");
        Reject(() => Parse(Golden + new string(' ', 65537 - Encoding.UTF8.GetByteCount(Golden))), "schema5 refuses64KiB plus one");
        string baseAtCap = BaseJson[..^1] + new string(' ', 16384 - Encoding.UTF8.GetByteCount(BaseJson)) + "}";
        Check(Serialize(Parse(Golden.Replace(BaseJson, baseAtCap))) == Golden, "embedded legacy Base still permits exact16KiB");
        Reject(() => Parse(Golden.Replace(BaseJson, baseAtCap[..^1] + " }")), "embedded Base cap remains independent inside content");
        Reject(() => NativeFreshLevelDocumentCodec.Parse(new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes(Golden)).ToArray()), "schema5 no BOM");
        Reject(() => NativeFreshLevelDocumentCodec.Parse(Encoding.UTF8.GetBytes(Golden).Concat(new byte[] { 0xFF }).ToArray()), "schema5 strict UTF8");
        Reject(() => Parse("/*comment*/" + Golden), "schema5 no comments");
        Reject(() => Parse(Golden[..^1] + ",}"), "schema5 no trailing comma");
        Reject(() => Parse(Golden.Replace("\"rewardDestinations\":null", "\"rewardDestinations\":" + new string('[', 10) + "0" + new string(']', 10))),
            "unchanged maximum JSON depth10 even for nested new fields");
        foreach (string escaped in new[] { "\\ud800", "\\udc00" })
        {
            Reject(() => Parse(Golden.Replace("definitionId", escaped)), "malformed UTF16 property name in family reference");
            Reject(() => Parse(Golden.Replace("peace-keepers-locked-chest-v1", escaped)), "malformed UTF16 family definition string");
        }
        var many = Value().WithPlacements(Enumerable.Range(1, 128).Select(i => Family(i.ToString("x32"))));
        Check(Parse(Serialize(many)).Placements.Count == 128, "128 family authoring intents fit separately from native row count/capacity");
        string excessive = Golden.Replace(FamilyJson, string.Join(',', Enumerable.Repeat("null", 129)));
        try { Parse(excessive); throw new InvalidOperationException("Expected early129-placement refusal."); }
        catch (InvalidDataException error)
        { Check(error.Message.Contains("128", StringComparison.Ordinal), "parser bounds authoring count before constructing any family element"); Refusals++; }
        string excessiveRoles = Golden.Replace("\"rewardDestinations\":null", "\"rewardDestinations\":[null,null,null,null,null,null]");
        Reject(() => Parse(excessiveRoles), "six destination entries refused before any role coercion");
        var allRoles = NativeFreshChestRewardDestinationOverrides.Create(Enum.GetValues<NativeFreshChestRewardRole>()
            .Select(role => new NativeFreshChestRewardDestinationOverride(role, 262128, 262128, 262128)).ToArray());
        var byteLimited = Value().WithPlacements(Enumerable.Range(1, 128).Select(i => Family(i.ToString("x32"), allRoles)));
        Check(byteLimited.Placements.Count == 128, "large neutral intent remains valid independently of serialization budget");
        Reject(() => NativeFreshLevelDocumentCodec.SerializeCanonical(byteLimited), "64KiB independently bounds128 valid families with explicit child destinations");
    }
}
