using System.Text;
using Spyro.Editor.Core.Editing;
using static Cases;

internal static class CodecCases
{
    internal static void Run()
    {
        Check(Serialize(Document(Wizard())) == Canonical, "independent full canonical literal/order/noBOM/noLF");
        Check(Encoding.UTF8.GetString(NativeId65FreshDocumentCodec.SerializeCanonical(Base())) == BaseJson, "old v1 literal unchanged");
        Check(Serialize(Parse(Canonical)) == Canonical, "canonical roundtrip");
        string reordered = " { \"placements\": [ { \"routeEnd\":{\"y\":8960,\"x\":9216}, \"origin\":{\"z\":8909,\"y\":8960,\"x\":8704},\"tag\":\"wizard-route\",\"id\":\"" + Id + "\" } ], \"base\":" + BaseJson + ",\"schema\":1,\"kind\":\"spyro-native-fresh-level\"}\r\n";
        Check(Serialize(Parse(reordered)) == Canonical, "field order and whitespace normalized without placement reorder");
        Check(Serialize(Parse(Canonical.Replace("\"id\":", "\"\\u0069d\":"))) == Canonical, "valid property escape normalized, not regenerated");
        byte[] input = Encoding.UTF8.GetBytes(Canonical); var retained = NativeFreshLevelDocumentCodec.Parse(input);
        Array.Fill(input, (byte)0); Check(Serialize(retained) == Canonical, "no caller parse-buffer alias");
        byte[] output = NativeFreshLevelDocumentCodec.SerializeCanonical(retained); Array.Fill(output, (byte)0);
        Check(Serialize(retained) == Canonical, "no returned canonical-buffer alias");
        Reject(() => NativeFreshLevelDocumentCodec.SerializeCanonical(null!), "null serialization");
        Reject(() => NativeId65FreshDocumentCodec.Parse(Encoding.UTF8.GetBytes(Canonical)), "new aggregate never old v1");
        Reject(() => Parse(BaseJson), "old v1 never silently migrated");
        foreach (string bad in new[] { "", "null", "[]", "{}", Canonical + "{}", Canonical + "x", Canonical[..^1] + ",}",
                     Canonical.Replace("\"schema\":1", "\"schema\":2"), Canonical.Replace("spyro-native-fresh-level", "spyro-native-fresh-wizard-intent"),
                     Canonical.Replace("\"placements\":[" + PlacementJson + "]", "\"placements\":null"),
                     Canonical.Replace("\"placements\":[" + PlacementJson + "]", "\"placements\":{}"),
                     Canonical.Replace(PlacementJson, "null"), Canonical.Replace(PlacementJson, "[]"),
                     Canonical.Replace("wizard-route", "unknown"), Canonical.Replace("wizard-route", "Wizard-route"),
                     Canonical.Replace("\"base\":" + BaseJson, "\"base\":null"),
                     Canonical.Replace("\"kind\":", "/*comment*/\"kind\":"),
                     Canonical.Replace("\"z\":8909", "\"z\":" + new string('[', 12) + "0" + new string(']', 12)) })
            Reject(() => Parse(bad), "root/tag/shape/schema/comment/depth refusal");

        // Every object boundary independently refuses unknown/missing/duplicate/case keys.
        foreach (string original in new[] { Canonical, PlacementJson, "{\"x\":8704,\"y\":8960,\"z\":8909}", "{\"x\":9216,\"y\":8960}", BaseJson })
        {
            string key = original.StartsWith("{\"kind\"") ? "kind" : original.StartsWith("{\"id\"") ? "id" : "x";
            int end = original.IndexOf(',', StringComparison.Ordinal);
            string first = original[1..end];
            foreach (string changed in new[] { original[..^1] + ",\"unrecognized\":0}", "{" + original[(end + 1)..],
                         "{" + first + "," + original[1..], original.Replace("\"" + key + "\":", "\"" + key.ToUpperInvariant() + "\":") })
                Reject(() => Parse(original == Canonical ? changed : Canonical.Replace(original, changed)), "strict object field set");
        }
        foreach (string field in new[] { "id", "tag", "origin", "routeEnd", "schema", "placements", "base" })
        {
            string needle = "\"" + field + "\":";
            string escaped = "\"\\u" + ((int)field[0]).ToString("x4") + field[1..] + "\":";
            string doubled = field is "id" or "tag" ? needle + "\"x\"," : needle + "null,";
            Reject(() => Parse(Canonical.Replace(needle, doubled + escaped)), "escaped duplicate field");
        }
        foreach (string target in new[] { "\"x\":8704", "\"y\":8960", "\"z\":8909", "\"x\":9216", "\"schema\":1" })
        {
            string key = target[..(target.IndexOf(':') + 1)];
            foreach (string invalid in new[] { "null", "true", "\"1\"", "1.0", "1e0", "2147483648", "-2147483649", "[]", "{}" })
                Reject(() => Parse(Canonical.Replace(target, key + invalid)), "integer type/fraction/exponent/overflow refusal");
        }
        foreach (string target in new[] { "\"id\":\"" + Id + "\"", "\"tag\":\"wizard-route\"", "\"kind\":\"spyro-native-fresh-level\"" })
        {
            string key = target[..(target.IndexOf(':') + 1)];
            foreach (string invalid in new[] { "null", "true", "1", "[]", "{}" })
                Reject(() => Parse(Canonical.Replace(target, key + invalid)), "string types refuse");
            foreach (string escaped in new[] { "\\ud800", "\\udc00", "\\ud83d\\ude00" })
                Reject(() => Parse(Canonical.Replace(target, key + "\"" + escaped + "\"")), "malformed/foreign escaped UTF16 value");
        }
        // Isolate repeated y fields so an invalid base spawn cannot mask a missing route check.
        foreach (string owner in new[] { "{\"x\":8704,\"y\":8960,\"z\":8909}", "{\"x\":9216,\"y\":8960}" })
            foreach (string invalid in new[] { "null", "true", "\"8960\"", "8960.0", "8.960e3", "2147483648", "-2147483649" })
                Reject(() => Parse(Canonical.Replace(owner, owner.Replace("\"y\":8960", "\"y\":" + invalid))),
                    "each repeated route y independently rejects malformed integer while base remains valid");
        foreach (string key in new[] { "kind", "id", "origin", "x" })
            foreach (string escaped in new[] { "\\ud800", "\\udc00", "\\ud83d\\ude00" })
                Reject(() => Parse(Canonical.Replace("\"" + key + "\":", "\"" + escaped + "\":")), "malformed/foreign escaped UTF16 name");
        foreach (string field in new[] { "sourceBin", "path", "nativeOwner", "classId", "ordinal", "dynamicCapacity", "payload", "runtimeVerified" })
            Reject(() => Parse(Canonical.Replace(PlacementJson, PlacementJson[..^1] + ",\"" + field + "\":0}")), "authoring does not accept authority/native fields");
        Reject(() => Parse(Canonical.Replace(Id, Id.ToUpperInvariant())), "uppercase ID not normalized");
        Reject(() => Parse(Canonical.Replace(Id, new string('0', 32))), "zero ID refusal");
        Reject(() => Parse(Canonical.Replace(PlacementJson, PlacementJson + "," + PlacementJson)), "duplicate parsed ID");
        string many = string.Join(',', Enumerable.Range(1, 129).Select(i => PlacementJson.Replace(Id, i.ToString("x32"))));
        Reject(() => Parse(Canonical.Replace(PlacementJson, many)), "129 parsed placements independently under byte cap");
        byte[] expected = Encoding.UTF8.GetBytes(Canonical);
        foreach (byte[] bad in new[] { new byte[] { 0xef, 0xbb, 0xbf }.Concat(expected).ToArray(),
                     Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(Canonical)).ToArray(),
                     Encoding.UTF32.GetPreamble().Concat(Encoding.UTF32.GetBytes(Canonical)).ToArray(),
                     expected.Concat(new byte[] { 0xff }).ToArray(), new byte[] { 0xc0, 0xaf }, new byte[] { 0xe2, 0x82 },
                     "{\"kind\":\""u8.ToArray().Concat(new byte[] { 0xed, 0xa0, 0x80 }).Concat("\"}"u8.ToArray()).ToArray() })
            Reject(() => NativeFreshLevelDocumentCodec.Parse(bad), "BOM/UTF8 refusal");
        byte[] atCap = Enumerable.Repeat((byte)' ', 65536).ToArray(); expected.CopyTo(atCap, 0);
        Check(Serialize(NativeFreshLevelDocumentCodec.Parse(atCap)) == Canonical, "exact64KiB accepted including whitespace");
        Reject(() => NativeFreshLevelDocumentCodec.Parse(atCap.Concat(new byte[] { 32 }).ToArray()), "over64KiB refused before parse");
        string bloatedBase = BaseJson[..^1] + new string(' ', 16384) + "}";
        Reject(() => Parse(Canonical.Replace(BaseJson, bloatedBase)), "embedded old base retains independent16KiB limit");
        Check(Serialize(Parse(Canonical)) == Canonical, "all refusals leave prior immutable content intact");
    }
}
