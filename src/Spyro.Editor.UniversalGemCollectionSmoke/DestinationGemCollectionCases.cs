using System.Buffers.Binary;
using System.Text.Json;
using Spyro.Editor.Core.Universal;

static class DestinationGemCollectionCases
{
    static UniversalOverlay Overlay(params uint[] words)
    {
        byte[] bytes = new byte[0x400];
        for (int i = 0; i < words.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x100 + i * 4), words[i]);
        return new(bytes, new[] { UniversalExeBindings.UpdateGlobal, UniversalExeBindings.InitGlobal, UniversalExeBindings.CopyBufferGlobal }
            .ToDictionary(g => g, g => new UniversalExeBinding(g, 0, 0, UniversalOverlay.Base + 0x100)));
    }
    internal static void Free(Action<bool, string> check)
    {
        uint start = UniversalOverlay.Base + 0x100;
        uint[] words = [UniversalMips.EncodeI(35, 19, 17, 0), 0, UniversalMips.EncodeI(35, 17, 4, 0),
            UniversalMips.EncodeJ(3, UniversalGemCollectionAdapter.CollectItem), 0, UniversalMips.JrRa, 0];
        SortedSet<uint> code = [.. Enumerable.Range(0, words.Length).Select(i => start + (uint)i * 4)];
        bool Refuses(Action a) { try { a(); return false; } catch (InvalidDataException) { return true; } }
        var owner = Overlay(words);
        var calls = UniversalDestinationGemCollectionAdapter.ProveCalls(owner, code, [], 19);
        check(calls.Count == 1 && calls[0].Role == "native-actor-property-linked-actor", "source-owned linked actor proven through property load");
        foreach (int i in new[] { 0, 1, 2, 4 })
        {
            var changed = words.ToArray();
            changed[i] = i switch
            {
                0 => UniversalMips.EncodeI(35, 19, 17, 4), // another property field
                1 => UniversalMips.EncodeI(9, 0, 17, 0), // clobbered pointer
                2 => UniversalMips.EncodeI(35, 17, 4, 4), // another linked field
                _ => UniversalMips.EncodeI(9, 0, 4, 0) // delay replaces actor
            };
            check(Refuses(() => UniversalDestinationGemCollectionAdapter.ProveCalls(Overlay(changed), code, [], 19)), "wrong linked actor provenance refused " + i);
        }
        var delayed = words.ToArray(); delayed[1] = words[0]; delayed[0] = 0;
        check(Refuses(() => UniversalDestinationGemCollectionAdapter.ProveCalls(Overlay(delayed), code, [], 19)), "uncommitted linked-property load refused");
        uint[] entered = [UniversalMips.EncodeI(4, 8, 0, 3), 0, (19u << 21) | (4u << 11) | 0x21,
            (19u << 21) | (4u << 11) | 0x21, UniversalMips.EncodeJ(3, UniversalGemCollectionAdapter.CollectItem), 0, UniversalMips.JrRa, 0];
        var enteredCode = Enumerable.Range(0, entered.Length).Select(i => start + (uint)i * 4).ToHashSet();
        check(Refuses(() => UniversalDestinationGemCollectionAdapter.ProveCalls(Overlay(entered), enteredCode, [], 19)), "direct branch entry skipping actor setup refused");
        uint[] direct = [UniversalMips.EncodeJ(3, UniversalGemCollectionAdapter.CollectItem), (19u << 21) | (4u << 11) | 0x21, UniversalMips.JrRa, 0];
        var directCode = Enumerable.Range(0, direct.Length).Select(i => start + (uint)i * 4).ToHashSet();
        check(UniversalDestinationGemCollectionAdapter.ProveCalls(Overlay(direct), directCode, [], 19).Single().Role == "current-native-actor", "direct current actor delay accepted");
        var bindings = new[] { new UniversalDestinationGemCall(start + 12, 0, calls[0].Role, 0x80090000) };
        byte[] patched = owner.Bytes.ToArray(); UniversalDestinationGemCollectionAdapter.Apply(patched, bindings);
        check(BinaryPrimitives.ReadUInt32LittleEndian(patched.AsSpan(0x10C)) == UniversalMips.EncodeJ(3, 0x80090000) &&
            BinaryPrimitives.ReadUInt32LittleEndian(patched.AsSpan(0x110)) == 0, "only owned call rewritten, delay exact");
        byte[] bad = owner.Bytes.ToArray(); bad[0x110] = 1; byte[] before = bad.ToArray();
        check(Refuses(() => UniversalDestinationGemCollectionAdapter.Apply(bad, bindings)) && bad.SequenceEqual(before), "changed delay refused atomically");
        var changedOwner = words.ToArray(); changedOwner[1] = 1;
        check(Refuses(() => UniversalDestinationGemCollectionAdapter.RequireCurrentOwner(Overlay(changedOwner), owner, calls)), "whole update source-owner mutation refused");
        check(UniversalGemCollectionAdapter.BuildMappings([new(0x53, 0x1FD), new(0x53, 0x1FC)], "two_sources").Items.Count > 0,
            "distinct source packages with same canonical value retain separate actual IDs");
        check(Refuses(() => UniversalGemCollectionAdapter.BuildMappings([new(0x53, 0x1FD), new(0x54, 0x1FD)], "ambiguous")),
            "ambiguous actual class owner refused");
    }
    internal static void Source(UniversalSourceLibrary library, Spyro.Editor.Core.Levels.LevelCatalog catalog, string output, Action<bool, string> check)
    {
        List<object> evidence = [];
        foreach (var level in catalog.Levels.Where(l => l.SourceWadEntry > 0))
        {
            var source = library.Get(level.Key).Overlay;
            var calls = UniversalDestinationGemCollectionAdapter.Analyze(source);
            UniversalDestinationGemCollectionAdapter.RequireCurrentOwner(source, source, calls);
            check(true, "complete native destination call/linked-actor ownership " + level.Key);
            foreach (var call in calls)
            {
                foreach (uint pc in new[] { call.Pc, call.Pc + 4 })
                {
                    var changed = source.Bytes.ToArray(); changed[(int)(pc - UniversalOverlay.Base)] ^= 1;
                    bool refused = false;
                    try { UniversalDestinationGemCollectionAdapter.RequireCurrentOwner(new(changed, source.Bindings), source, calls); }
                    catch (InvalidDataException) { refused = true; }
                    check(refused, "native call/delay source mutation refused " + level.Key);
                }
                if (call.Role == "native-actor-property-linked-actor")
                {
                    var changed = source.Bytes.ToArray(); changed[(int)(call.Pc - 4 - UniversalOverlay.Base)] ^= 4;
                    bool refused = false;
                    try { UniversalDestinationGemCollectionAdapter.Analyze(new(changed, source.Bindings)); }
                    catch (InvalidDataException) { refused = true; }
                    check(refused, "wrong native linked-actor field refused " + level.Key);
                }
            }
            var (_, tables) = UniversalDispatchAnalysis.Closure(source, source.Update, [source.Update], new HashSet<uint>());
            foreach (var table in tables)
            {
                var changed = source.Bytes.ToArray(); changed[(int)(table.TableAddress - UniversalOverlay.Base)] ^= 4;
                bool refused = false;
                try { UniversalDestinationGemCollectionAdapter.RequireCurrentOwner(new(changed, source.Bindings), source, calls); }
                catch (InvalidDataException) { refused = true; }
                check(refused, "native dispatch data mutation refused " + level.Key);
            }
            evidence.Add(new { level.Key, Calls = calls });
        }
        File.WriteAllText(output, JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }));
    }
}
