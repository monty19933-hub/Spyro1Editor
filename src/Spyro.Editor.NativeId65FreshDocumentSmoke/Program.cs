using System.Reflection;
using System.Text;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Music;
using Spyro.Editor.Core.Primitives;

if (args.Length != 0)
    throw new ArgumentException("Usage: NativeId65FreshDocumentSmoke (source-free only)");

int checks = 0;
const string canonical = "{\"kind\":\"spyro-native-fresh-intent\",\"schema\":1,\"floor\":{\"x\":8448,\"y\":8448,\"z\":8704,\"width\":1024,\"depth\":1024},\"spawn\":{\"x\":8960,\"y\":8960,\"heading\":64},\"display\":{\"floorRgb\":[80,128,112],\"skyRgb\":[128,96,64]},\"musicTrackId\":19}";
byte[] expected = Encoding.UTF8.GetBytes(canonical);
var first = Make();
Check(typeof(NativeId65FreshDocumentIntent).IsSealed &&
    typeof(NativeId65FreshDocumentIntent).GetConstructors().Length == 0,
    "intent is sealed and has no public constructor authority");
Check(typeof(NativeId65FreshDocumentIntent).GetProperties(BindingFlags.Public | BindingFlags.Instance)
    .All(p => p.SetMethod == null), "all public intent properties are read-only");
Check(typeof(NativeId65FreshDocumentIntent).GetProperties(BindingFlags.Public | BindingFlags.Instance)
    .Select(p => p.Name).Order().SequenceEqual(new[]
    { "FloorColor", "FloorDepth", "FloorWidth", "FloorX", "FloorY", "FloorZ", "Heading", "MusicTrackId", "SkyColor", "SpawnX", "SpawnY", "SpawnZ" }),
    "intent exposes only semantic scalars/colors, no native payloads or approval flags");
Check(typeof(NativeId65FreshDocumentCodec).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
    .Select(m => m.Name).Order().SequenceEqual(new[] { "Parse", "SerializeCanonical" }),
    "codec has no filesystem, hash-authority, migration or compilation API");
Check(NativeId65FreshDocumentCodec.SerializeCanonical(first).SequenceEqual(expected), "exact canonical UTF8/order/no-BOM/no-newline golden");
Check(first.SpawnZ == 9216 && NativeId65FreshDocumentIntent.SpawnHeightAboveFloor == 512 &&
    NativeId65FreshDocumentIntent.NominalDynamicCapacity == 16 && NativeId65FreshDocumentIntent.PlacedMobyCount == 0,
    "spawn height, nominal slots and zero placed rows remain code-owned and absent from JSON");
Check(first.FloorColor == new ColorRgba(80, 128, 112) && first.SkyColor == new ColorRgba(128, 96, 64),
    "display RGB channels remain semantic channels, not packed native words");
Check(Serialize(NativeId65FreshDocumentCodec.Parse(expected)) == canonical, "canonical round trip");
const string reordered = " { \"musicTrackId\":19, \"display\": {\"skyRgb\":[128,96,64],\"floorRgb\":[80,128,112]}, \"spawn\":{\"heading\":64,\"y\":8960,\"x\":8960}, \"floor\":{\"depth\":1024,\"width\":1024,\"z\":8704,\"y\":8448,\"x\":8448}, \"schema\":1, \"kind\":\"spyro-native-fresh-intent\" } \r\n";
Check(Serialize(Parse(reordered)) == canonical, "property order and whitespace normalize deterministically");
byte[] mutableInput = expected.ToArray();
var detached = NativeId65FreshDocumentCodec.Parse(mutableInput);
Array.Fill(mutableInput, (byte)0);
Check(Serialize(detached) == canonical, "parsed intent retains no mutable input-array alias");
byte[] mutableOutput = NativeId65FreshDocumentCodec.SerializeCanonical(first);
Array.Fill(mutableOutput, (byte)0);
Check(Serialize(first) == canonical, "serialized byte arrays cannot mutate immutable intent");
Reject(() => NativeId65FreshDocumentCodec.SerializeCanonical(null!), "null intent");

foreach (var valid in new[]
{
    Make(width: 16, depth: 16, sx: 8449, sy: 8449, heading: 0),
    Make(width: 16, depth: 16, sx: 8463, sy: 8463, heading: 255),
    Make(x: 8192, y: 8192, width: 2048, depth: 2048),
    Make(z: 261616),
    Make(fc: new ColorRgba(255, 0, 0), sc: new ColorRgba(0, 0, 255)),
    Make(fc: new ColorRgba(0, 0, 0), sc: new ColorRgba(255, 255, 255))
})
    Check(Serialize(NativeId65FreshDocumentCodec.Parse(NativeId65FreshDocumentCodec.SerializeCanonical(valid))) == Serialize(valid),
        "bounded geometry/heading/color endpoint survives round trip");

foreach (Action invalid in new Action[]
{
    () => Make(x: -16), () => Make(x: 262144), () => Make(x: 8449),
    () => Make(y: -16), () => Make(y: 262144), () => Make(y: 8449),
    () => Make(z: -16), () => Make(z: 8705), () => Make(z: 261632), () => Make(z: int.MaxValue),
    () => Make(width: 0), () => Make(width: 15), () => Make(width: 17), () => Make(width: 2064),
    () => Make(depth: 0), () => Make(depth: 17), () => Make(depth: 2064),
    () => Make(x: 11264, sx: 11500), () => Make(y: 11264, sy: 11500),
    () => Make(sx: 8448), () => Make(sx: 9472), () => Make(sx: 8447), () => Make(sx: 9473),
    () => Make(sy: 8448), () => Make(sy: 9472), () => Make(sy: 8447), () => Make(sy: 9473),
    () => Make(sx: int.MaxValue), () => Make(sy: int.MinValue),
    () => Make(heading: -1), () => Make(heading: 256),
    () => Make(fc: new ColorRgba(1, 2, 3, 0)), () => Make(sc: new ColorRgba(1, 2, 3, 254))
}) Reject(invalid, "invalid geometry, strict interior point, height, heading or alpha");

int selectable = 0;
for (int track = -1; track <= 48; track++)
{
    int id = track;
    if (MusicTrackCatalog.Find(id)?.IsSelectable == true)
    {
        var selected = Make(track: id);
        Check(Parse(Serialize(selected)).MusicTrackId == id, "every actual selectable music ID round trips");
        selectable++;
    }
    else Reject(() => Make(track: id), "reserved/unknown music ID refuses");
}
Check(selectable == 46 && Make(track: 46).MusicTrackId == 46 && Make(track: 47).MusicTrackId == 47,
    "selectable IDs are0..47 excluding30/35, not an incorrect0..45 interval");
Reject(() => Make(track: int.MaxValue), "oversized track ID");

foreach (string malformed in new[]
{
    "", "null", "[]", "{}", canonical + "{}", canonical + " trailing",
    canonical.Replace("\"schema\":1", "\"schema\":2"),
    canonical.Replace("\"schema\":1", "\"schema\":1,\"schema\":1"),
    canonical.Replace("\"schema\":1", "\"schema\":1,\"\\u0073chema\":1"),
    canonical.Replace("\"schema\":1", "\"Schema\":1"),
    canonical.Replace("\"schema\":1,", ""),
    canonical.Replace("spyro-native-fresh-intent", "legacy-id65-authoring-document"),
    canonical.Replace("\"x\":8448", "\"x\":8448,\"x\":8448"),
    canonical.Replace("\"x\":8448", "\"X\":8448"),
    canonical.Replace("\"x\":8448", "\"x\":8448.0"),
    canonical.Replace("\"x\":8448", "\"x\":8.448e3"),
    canonical.Replace("\"x\":8448", "\"x\":2147483648"),
    canonical.Replace("\"x\":8448", "\"x\":\"8448\""),
    canonical.Replace("\"x\":8448", "\"x\":null"),
    canonical.Replace("\"heading\":64", "\"heading\":64,\"z\":9216"),
    canonical.Replace("\"floorRgb\":[80,128,112]", "\"floorRgb\":[80,128,112],\"floorRgb\":[80,128,112]"),
    canonical.Replace("[80,128,112]", "[80,128]"),
    canonical.Replace("[80,128,112]", "[80,128,112,255]"),
    canonical.Replace("[80,128,112]", "[-1,128,112]"),
    canonical.Replace("[80,128,112]", "[256,128,112]"),
    canonical.Replace("[80,128,112]", "[80.0,128,112]"),
    canonical.Replace("[80,128,112]", "[8e1,128,112]"),
    canonical.Replace("[80,128,112]", "[\"80\",128,112]"),
    canonical.Replace("[80,128,112]", "null"),
    canonical.Replace("\"musicTrackId\":19", "\"musicTrackId\":30"),
    canonical.Replace("\"musicTrackId\":19", "\"musicTrackId\":35"),
    canonical.Replace("\"musicTrackId\":19", "\"musicTrackId\":19,"),
    canonical.Replace("\"schema\":1", "/*comment*/\"schema\":1"),
    canonical.Replace("\"schema\":1", "\"schema\":1//comment\n"),
    "{\"schemaVersion\":6,\"blankCyclorama\":{\"presetId\":\"blank-slate-v1\"}}",
    canonical.Replace("\"x\":8448", "\"x\":" + new string('[', 9) + "0" + new string(']', 9))
}) Reject(() => Parse(malformed), "strict JSON/schema/duplicate/type/legacy refusal");
foreach (string foreignField in new[] { "path", "sourceBin", "ramAddress", "payload", "runtimeVerified", "nominalCapacity", "placedMobys" })
    Reject(() => Parse(canonical[..^1] + ",\"" + foreignField + "\":0}"), "unknown authority/storage field");
// ASCII JSON escapes pass raw UTF8 validation; malformed UTF16 must still reach
// the codec's data-refusal boundary, not leak a decoding InvalidOperationException.
foreach (string escaped in new[] { "\\ud800", "\\udc00", "\\ud83d\\ude00" })
{
    Reject(() => Parse(canonical.Replace("spyro-native-fresh-intent", escaped)),
        "lone surrogate or valid nonmatching astral kind is a normalized data refusal");
    Reject(() => Parse(canonical.Replace("\"kind\":", "\"" + escaped + "\":")),
        "lone surrogate or valid unknown astral property is a normalized data refusal");
}

foreach (byte[] malformed in new[]
{
    new byte[] { 0xEF, 0xBB, 0xBF }.Concat(expected).ToArray(),
    Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(canonical)).ToArray(),
    Encoding.UTF32.GetPreamble().Concat(Encoding.UTF32.GetBytes(canonical)).ToArray(),
    expected.Concat(new byte[] { 0xFF }).ToArray(),
    Encoding.UTF8.GetBytes("{\"kind\":\"").Concat(new byte[] { 0xC0, 0xAF }).Concat("\"}"u8.ToArray()).ToArray(),
    new byte[] { 0xE2, 0x82 }
}) Reject(() => NativeId65FreshDocumentCodec.Parse(malformed), "BOM/invalid UTF8 refusal");
byte[] atCap = Enumerable.Repeat((byte)' ', 16 * 1024).ToArray();
expected.CopyTo(atCap, 0);
Check(Serialize(NativeId65FreshDocumentCodec.Parse(atCap)) == canonical, "exact16KiB bound permits valid trailing whitespace");
Reject(() => NativeId65FreshDocumentCodec.Parse(atCap.Concat(new byte[] { 32 }).ToArray()), "over16KiB input refuses");

Console.WriteLine($"NATIVE_ID65_FRESH_DOCUMENT_SMOKE_OK checks={checks}");

NativeId65FreshDocumentIntent Make(int x = 8448, int y = 8448, int z = 8704, int width = 1024, int depth = 1024,
    int sx = 8960, int sy = 8960, int heading = 64, ColorRgba? fc = null, ColorRgba? sc = null, int track = 19) =>
    NativeId65FreshDocumentIntent.Create(x, y, z, width, depth, sx, sy, heading,
        fc ?? new ColorRgba(80, 128, 112), sc ?? new ColorRgba(128, 96, 64), track);
NativeId65FreshDocumentIntent Parse(string text) => NativeId65FreshDocumentCodec.Parse(Encoding.UTF8.GetBytes(text));
string Serialize(NativeId65FreshDocumentIntent value) => Encoding.UTF8.GetString(NativeId65FreshDocumentCodec.SerializeCanonical(value));
void Check(bool value, string label) { if (!value) throw new InvalidDataException(label); checks++; }
void Reject(Action action, string label)
{
    try { action(); }
    catch (Exception error) when (error is InvalidDataException or ArgumentException) { checks++; return; }
    throw new InvalidDataException("Expected refusal: " + label);
}
