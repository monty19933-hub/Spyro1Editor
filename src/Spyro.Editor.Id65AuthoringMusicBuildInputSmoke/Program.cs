using System.Text;
using System.Text.Json.Nodes;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Music;

const string Envelope = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
Id65AuthoringEditorDocument[] documents =
[
    Id65AuthoringEditorDocument.CreateExact(),
    Id65AuthoringEditorDocument.CreateExact(mandatoryBaseFaceTextureId: 25),
    Id65AuthoringEditorDocument.CreateRemotePadGridExact(Id65RemotePadGrassT107Catalog.CenterPositionId),
    Id65AuthoringEditorDocument.CreateRemotePadMobyChoiceExact(Id65RemotePadMobyKind.GrassT107, Id65RemotePadGrassT107Catalog.CenterPositionId),
    Id65AuthoringEditorDocument.CreateRemotePadMobyChoiceTerrainSetExact(Id65RemotePadMobyKind.GrassT107, Id65RemotePadGrassT107Catalog.CenterPositionId, [Id65AuthoringTerrainSetCatalog.Tile1Id]),
    Id65AuthoringEditorDocument.CreateRemotePadMobyChoiceTerrainSetBlankCycloramaExact(Id65RemotePadMobyKind.GrassT107, Id65RemotePadGrassT107Catalog.CenterPositionId, [Id65AuthoringTerrainSetCatalog.Tile1Id])
];
// Captured from the unmodified pre-feature Core DLL, before the new input
// existed. These baseline document bytes must not migrate or gain music data.
string[] goldenHashes =
[
    "8f27bfc3f97238402c0b7dd59a081335ae19df1acfaefa9575d9ca7110dce808",
    "ef66ec698c9acccb57c31754985f7f8f42553dfd8cbca6fe3a4d94c11a5d6ea1",
    "4d17bd6de3ee6fa225b0633fe3129f8c249212083b3e32a3f3e198ed1ec09fad",
    "9aefd1070da214cdaa071749dc0dc12d34b1ffe0f85c638c3389cb630a7325ca",
    "f12e21807a9f29b0ddfa88d3f39a642791d730b09946c4ff5361655799c8764e",
    "1978bc3afde796349990e4a7f8c9d76babc5ed9d813026d47c272249921c0678"
];
int rejected = 0;
foreach ((Id65AuthoringEditorDocument document, string expected) in documents.Zip(goldenHashes))
{
    Require(document.CanonicalSha256 == expected, $"Schema-{document.SchemaVersion} baseline golden changed.");
    byte[] before = document.SerializeUtf8();
    Require(Id65AuthoringEditorDocument.Parse(before).SerializeUtf8().SequenceEqual(before), "A baseline document no longer reloads exactly.");
    Require(document.BaselineHandles.Music == "music.slot35", "The existing baseline music handle was reinterpreted.");
    if (document.SchemaVersion != 6)
        Reject(() => Id65AuthoringMusicBuildInput.Create(document, Envelope, 26), "Unsupported baseline schema");
}

Id65AuthoringEditorDocument baseline = documents[^1];
byte[] baselineBefore = baseline.SerializeUtf8();
HashSet<string> trackHashes = [];
foreach (MusicTrackEntry track in MusicTrackCatalog.SelectableTracks)
{
    Id65AuthoringMusicBuildInput input = Id65AuthoringMusicBuildInput.Create(baseline, Envelope, track.TrackId);
    byte[] bytes = input.SerializeUtf8();
    Id65AuthoringMusicBuildInput parsed = Id65AuthoringMusicBuildInput.Parse(bytes);
    Require(parsed.SelectedTrackId == track.TrackId && parsed.BaselineEnvelopeCanonicalSha256 == Envelope &&
        parsed.BaselineDocumentCanonicalSha256 == baseline.CanonicalSha256 && parsed.SerializeUtf8().SequenceEqual(bytes),
        "A selectable music build input did not reload exactly.");
    input.RequireExactBaseline(baseline, Envelope);
    Require(input.BaselineDocument.SerializeUtf8().SequenceEqual(baselineBefore) && baseline.SerializeUtf8().SequenceEqual(baselineBefore),
        "The opt-in input changed the baseline document.");
    Require(trackHashes.Add(input.CanonicalSha256), "Distinct selected tracks alias one authored build-input identity.");
    Require(Id65AuthoringMusicBuildInput.Create(baseline, Envelope, track.TrackId).SerializeUtf8().SequenceEqual(bytes),
        "Repeated authoring is nondeterministic.");
    bytes[0] ^= 1;
    Require(input.SerializeUtf8()[0] == (byte)'{', "Serialization exposed mutable backing storage.");
}
Require(trackHashes.Count == 46, "The input excludes a selectable built-in track.");

HashSet<string> stateHashes = [];
foreach (int topology in Enumerable.Range(0, 4))
foreach (Id65RemotePadMobyKind kind in Enum.GetValues<Id65RemotePadMobyKind>())
foreach (Id65RemotePadGrassT107Position position in Id65RemotePadGrassT107Catalog.Positions)
{
    List<string> tiles = [];
    if ((topology & 1) != 0) tiles.Add(Id65AuthoringTerrainSetCatalog.Tile1Id);
    if ((topology & 2) != 0) tiles.Add(Id65AuthoringTerrainSetCatalog.Tile2Id);
    Id65AuthoringEditorDocument document = Id65AuthoringEditorDocument.CreateRemotePadMobyChoiceTerrainSetBlankCycloramaExact(kind, position.PositionId, tiles);
    Id65AuthoringMusicBuildInput input = Id65AuthoringMusicBuildInput.Create(document, Envelope, 0);
    Id65AuthoringMusicBuildInput parsed = Id65AuthoringMusicBuildInput.Parse(input.SerializeUtf8());
    parsed.RequireExactBaseline(document, Envelope);
    Require(stateHashes.Add(input.CanonicalSha256), "Distinct schema-6 terrain/Moby states alias a music build input.");
}
Require(stateHashes.Count == 108, "The input does not preserve the complete existing schema-6 authoring state space.");

Id65AuthoringMusicBuildInput witness = Id65AuthoringMusicBuildInput.Create(baseline, Envelope, 26);
foreach (int track in new[] { int.MinValue, -1, 30, 35, 48, int.MaxValue })
    Reject(() => Id65AuthoringMusicBuildInput.Create(baseline, Envelope, track), "Unknown/reserved track");
foreach (string hash in new[] { "", "abc", new string('A', 64), new string('g', 64), new string('a', 65) })
    Reject(() => Id65AuthoringMusicBuildInput.Create(baseline, hash, 26), "Malformed envelope identity");
Reject(() => witness.RequireExactBaseline(baseline, new string('b', 64)), "Stale stored envelope");
Reject(() => witness.RequireExactBaseline(Id65AuthoringEditorDocument.CreateRemotePadMobyChoiceTerrainSetBlankCycloramaExact(
    Id65RemotePadMobyKind.None, Id65RemotePadGrassT107Catalog.CenterPositionId, [Id65AuthoringTerrainSetCatalog.Tile1Id]), Envelope), "Stale geometry/Moby baseline");
RejectJson(root => root["music"]!["trackId"] = 30, "Reserved serialized track");
RejectJson(root => root["music"]!["trackId"] = "26", "String track ID");
RejectJson(root => root["music"]!["trackId"] = 26.5, "Fractional track ID");
RejectJson(root => root["music"]!["kind"] = "raw-executable-patch", "Afterimage intent");
RejectJson(root => root["music"]!["fileOffset"] = 0x5F828, "Caller physical location");
RejectJson(root => root["sourcePath"] = "/tmp/anything.bin", "Caller path");
RejectJson(root => root["runtimeProven"] = true, "Caller authority");
RejectJson(root => root["schemaVersion"] = 2, "Future schema");
RejectJson(root => root["contentSha256"] = new string('0', 64), "Stale content hash");
RejectJson(root => root["baselineDocumentCanonicalSha256"] = new string('0', 64), "Stale baseline hash");
RejectJson(root => root["baselineDocument"]!["baselineHandles"]!["music"] = "music.authored", "Reinterpreted baseline music handle");
RejectJson(root => root.Remove("music"), "Missing explicit music intent");
Reject(() => Id65AuthoringMusicBuildInput.Parse([]), "Empty input");
Reject(() => Id65AuthoringMusicBuildInput.Parse(new byte[Id65AuthoringMusicBuildInput.MaximumSerializedByteCount + 1]), "Oversized input");
string canonical = Encoding.UTF8.GetString(witness.SerializeUtf8());
Reject(() => Id65AuthoringMusicBuildInput.Parse(Encoding.UTF8.GetBytes(canonical.Replace("\"trackId\":26", "\"trackId\":26,\"trackId\":26", StringComparison.Ordinal))), "Duplicate property");
Reject(() => Id65AuthoringMusicBuildInput.Parse(Encoding.UTF8.GetBytes(" " + canonical)), "Noncanonical serialization");
Require(baseline.SerializeUtf8().SequenceEqual(baselineBefore), "Negative cases mutated the existing baseline.");
Console.WriteLine($"ID65_AUTHORING_MUSIC_BUILD_INPUT_SMOKE_OK legacyGoldenSchemas=6 selectableTracks=46 schema6States=108 rejected={rejected} exactReload=1 staleBaselineRefused=1 noNativePayloadOrWriterAuthority=1");
if (args.Length != 0)
{
    if (args.Length != 2 || args[0] is not ("--source-aware" or "--candidate-writer"))
        throw new ArgumentException("Use no arguments for source-free input tests, --source-aware <repository-root> for disc-plan/readback, or --candidate-writer <repository-root> for retained candidate publication tests.");
    if (args[0] == "--source-aware") await ComposedPlanCases.RunAsync(Path.GetFullPath(args[1]));
    else await CandidateWriterCases.RunAsync(Path.GetFullPath(args[1]));
}

void Reject(Action action, string label)
{
    try { action(); }
    catch (InvalidDataException) { rejected++; return; }
    throw new InvalidOperationException($"Did not reject {label}.");
}
void RejectJson(Action<JsonObject> mutate, string label)
{
    JsonObject root = JsonNode.Parse(witness.SerializeUtf8())!.AsObject();
    mutate(root);
    Reject(() => Id65AuthoringMusicBuildInput.Parse(Encoding.UTF8.GetBytes(root.ToJsonString())), label);
}
static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
