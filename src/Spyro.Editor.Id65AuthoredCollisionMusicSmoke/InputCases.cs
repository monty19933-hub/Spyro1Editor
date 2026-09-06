using System.Text;
using System.Text.Json.Nodes;
using Spyro.Editor.Core.Exporting;

internal static class InputCases
{
    public static int Run()
    {
        int checks = 0;
        var doc = Id65AuthoringEditorDocument.CreateRemotePadMobyChoiceTerrainSetBlankCycloramaExact(
            Id65RemotePadMobyKind.GrassT107, Id65RemotePadGrassT107Catalog.CenterPositionId, [Id65AuthoringTerrainSetCatalog.Tile1Id]);
        byte[] original = doc.SerializeUtf8();
        Require(doc.CanonicalSha256 == "1978bc3afde796349990e4a7f8c9d76babc5ed9d813026d47c272249921c0678", "unchanged schema6 golden");
        var music = Id65AuthoringMusicBuildInput.Create(doc, new string('a', 64), 2);
        byte[] oldInput = music.SerializeUtf8();
        var input = Id65AuthoredCollisionMusicInput.Create(music);
        var roundtrip = Id65AuthoredCollisionMusicInput.Parse(input.SerializeUtf8());
        Require(roundtrip.SerializeUtf8().SequenceEqual(input.SerializeUtf8()) && roundtrip.CanonicalSha256 == input.CanonicalSha256,
            "canonical combined input exact reload");
        Require(music.SerializeUtf8().SequenceEqual(oldInput) && doc.SerializeUtf8().SequenceEqual(original), "old input and schema6 bytes preserved");
        Require(Id65AuthoredCollisionMusicInput.Create(music, Id65AuthoredCollisionMusicSubstrate.ExactCollapsedSky).CanonicalSha256 != input.CanonicalSha256,
            "compiled profiles have distinct identities");
        foreach (var (field, value) in new[] { ("format", "unknown"), ("profileId", "unknown"), ("substrate", "free-form"), ("contentSha256", new string('0', 64)) })
            RejectJson(root => root[field] = value);
        RejectJson(root => root["sourcePath"] = "/tmp/unsafe.bin");
        RejectJson(root => root["runtimeProven"] = true);
        RejectJson(root => root["schemaVersion"] = 7);
        RejectJson(root => root["substrate"] = 1);
        RejectJson(root => root["musicBuildInput"]!["music"]!["trackId"] = 26);
        RejectJson(root => root.Remove("substrate"));
        Reject(() => Id65AuthoredCollisionMusicInput.Parse(Encoding.UTF8.GetBytes(" " + Encoding.UTF8.GetString(input.SerializeUtf8()))));
        Reject(() => Id65AuthoredCollisionMusicInput.Parse(Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(input.SerializeUtf8())
            .Replace("\"schemaVersion\":1,\"profileId\"", "\"schemaVersion\":1,\"schemaVersion\":1,\"profileId\"", StringComparison.Ordinal))));
        Reject(() => Id65AuthoredCollisionMusicInput.Create(music, (Id65AuthoredCollisionMusicSubstrate)0));
        Reject(() => Id65AuthoredCollisionMusicInput.Parse([]));
        byte[] escaped = input.SerializeUtf8(); escaped[0] ^= 1;
        Require(input.SerializeUtf8()[0] == (byte)'{', "immutable input bytes");
        return checks;

        void Require(bool condition, string label) { if (!condition) throw new InvalidOperationException(label); checks++; }
        void Reject(Action action)
        {
            try { action(); }
            catch (InvalidDataException) { checks++; return; }
            throw new InvalidOperationException("Combined input accepted malformed or unauthorized input.");
        }
        void RejectJson(Action<JsonObject> mutate)
        {
            JsonObject root = JsonNode.Parse(input.SerializeUtf8())!.AsObject(); mutate(root);
            Reject(() => Id65AuthoredCollisionMusicInput.Parse(Encoding.UTF8.GetBytes(root.ToJsonString())));
        }
    }
}
