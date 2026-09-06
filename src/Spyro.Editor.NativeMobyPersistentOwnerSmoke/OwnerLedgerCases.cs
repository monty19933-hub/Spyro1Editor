using System.Collections;
using System.Text;
using System.Text.Json.Nodes;
using Spyro.Editor.Core.Scene;

internal static class OwnerLedgerCases
{
    internal static void Run(Action<bool, string> check, Action<Action, string> reject)
    {
        // Internal structural fixture identities are NOT production certificates.
        var seed = new NativeMobyOwnerSourceBinding(new string('1', 64), 12, 2, 128, new string('2', 64));
        NativeMobyOwnerRoleIdentity Role(string id, int donor = 6, ushort actor = 0x11B) =>
            new(id, 34, donor, actor, "fixture-source-row-" + donor + "-class-" + actor);
        NativeMobyPersistentOwnerFamily Family(params NativeMobyOwnerRoleIdentity[] roles) =>
            new("fixture-identity-only-v1", roles);
        var actor = Family(Role("actor"));
        NativeMobyPersistentOwnerLayout Layout(int original = 128,
            params NativeMobyPersistentOwnerFamily[] families) =>
            new(seed with { OriginalRowCount = original }, families.Length == 0 ? [actor] : families);
        NativeMobyPersistentOwnerRequest R(string id, NativeMobyPersistentOwnerFamily? template = null) => new(id, template ?? actor);
        int Index(NativeMobyPersistentOwnerLedger ledger, string family, string role = "actor") =>
            ledger.Entries.Single(e => e.FamilyId == family && e.RoleId == role).NativeIndex;

        // The primary temporal regression: determinism of a fresh sorted batch
        // is insufficient. Reopen source binding before adding an earlier ID.
        var layout = Layout();
        var empty = layout.Empty();
        var z = empty.Append([R("z")]);
        byte[] zBytes = z.Serialize();
        var reopened = Layout().Restore(zBytes);
        var za = reopened.Append([R("a")]);
        check(Index(za, "z") == 128 && Index(za, "a") == 129 && za.HighWater == 130,
            "Temporal reopen + lexically earlier ID reassigned an already published native owner.");
        check(z.HighWater == 129 && empty.HighWater == 128 && z.Serialize().SequenceEqual(zBytes),
            "Append mutated the prior immutable ledger.");
        check(za.Append([R("a"), R("z")]).Serialize().SequenceEqual(za.Serialize()) &&
              za.Append([R("z"), R("a")]).Serialize().SequenceEqual(za.Serialize()),
            "Display/input reorder changed native identity or serialized bytes.");
        check(Layout().Restore(za.Serialize()).Serialize().SequenceEqual(za.Serialize()), "Exact canonical round-trip drifted.");
        check(!za.SourceCertified && !za.NativeEmissionApproved && !za.RuntimeVerified,
            "A neutral ledger claimed native source, emission, or gameplay authority.");

        // Retirement reserves the ordinal, including a retired tail. No guessed
        // FF/FD/zero/native-row tombstone may be emitted by this identity layer.
        var retired = za.Retire(["z"]);
        var next = layout.Restore(retired.Serialize()).Append([R("b")]);
        check(Index(next, "z") == 128 && Index(next, "a") == 129 && Index(next, "b") == 130 &&
              next.HighWater == 131 && next.Entries.Single(e => e.FamilyId == "z").Retired,
            "Retirement compacted/reused a durable bit or was lost on reopen.");
        reject(() => next.RequireNoUnmaterializedRetirements(), "unproved retired native row encoding");
        reject(() => next.Append([R("z")]), "Append cannot silently reactivate a retired family");
        var restored = next.Reactivate([R("z")]);
        restored.RequireNoUnmaterializedRetirements();
        check(Index(restored, "z") == 128 && restored.HighWater == 131 && restored.Entries.All(e => !e.Retired),
            "Explicit same-identity undo failed to recover its reserved owner.");
        var tail = za.Retire(["a"]).Append([R("c")]);
        check(Index(tail, "a") == 129 && Index(tail, "c") == 130 && tail.HighWater == 131, "Retired tail was recycled.");
        reject(() => za.Retire(["unknown"]), "unknown family retirement");
        reject(() => za.Retire(["z", "z"]), "duplicate retirement request");
        reject(() => za.Reactivate([R("z")]), "reactivation requires a retired reservation");

        // Stable role IDs are independent of caller enumeration. Whole-family
        // first allocation is canonical, but existing role identity cannot morph.
        var trio = Family(Role("root"), Role("reward", 7, 0xD), Role("effect", 8, 0x26));
        var trioReordered = Family(Role("effect", 8, 0x26), Role("root"), Role("reward", 7, 0xD));
        var multiLayout = Layout(250, trio, trioReordered);
        var multi = multiLayout.Empty().Append([R("family", trio)]);
        check(multi.Entries.Select(e => e.RoleId).SequenceEqual(new[] { "effect", "reward", "root" }) &&
              multi.Entries.Select(e => e.NativeIndex).SequenceEqual(new[] { 250, 251, 252 }), "Whole-family stable role allocation drifted.");
        check(multi.Append([R("family", trioReordered)]).Serialize().SequenceEqual(multi.Serialize()),
            "Role enumeration was mistaken for a changed native role binding.");
        var retiredMulti = multi.Retire(["family"]);
        check(retiredMulti.Entries.Count == 3 && retiredMulti.Entries.All(e => e.Retired), "Partial family retirement.");
        var changedClass = Family(Role("actor", actor: 0x26));
        var changedDonor = Family(Role("actor", donor: 7));
        var changedRole = Family(Role("new-role"));
        foreach (var changed in new[] { changedClass, changedDonor, changedRole })
        {
            reject(() => za.Append([R("z", changed)]), "persistent role/class/source identity changed");
            reject(() => retired.Reactivate([R("z", changed)]), "changed-identity undo");
        }
        reject(() => Family(Role("duplicate"), Role("duplicate")), "duplicate stable roles");
        reject(() => Family(), "empty family");

        // Every placed ordinal participates in the loader's bit lookup. Class135
        // is a nonreward fixture; absence of a reward-byte flag cannot admit256.
        var scenery = Family(Role("scenery", 9, 0x135));
        var at255 = Layout(255, scenery).Empty().Append([R("last", scenery)]);
        check(at255.Entries.Single().NativeIndex == 255 && at255.HighWater == 256, "Last ordinary placed owner255 rejected.");
        reject(() => at255.Append([R("overflow", scenery)]), "nonreward owner256 aliases adjacent storage");
        reject(() => Layout(256, scenery).Empty().Append([R("overflow", scenery)]), "empty suffix beyond native bitmap");
        reject(() => Layout(254, trio).Empty().Append([R("partial", trio)]), "family crosses persistent capacity");
        foreach (int invalid in new[] { -1, 257, int.MaxValue }) reject(() => Layout(invalid), "invalid original row high-water");
        reject(() => new NativeMobyPersistentOwnerLayout(seed with { SaveSlot = 3 }, [actor]), "level/save identity alias");
        reject(() => new NativeMobyPersistentOwnerLayout(seed with { LevelId = 16 }, [actor]), "decimal ID alias");
        reject(() => new NativeMobyPersistentOwnerLayout(seed with { LevelId = 65, SaveSlot = 35 }, [actor]), "ID65 is not a proved independent native slot");

        // Validate complete batches before returning any reservations. A failed
        // last family must not consume slots or alter previous serialized bytes.
        var almostFull = Layout(254, actor, trio).Empty().Append([R("kept")]);
        byte[] before = almostFull.Serialize();
        reject(() => almostFull.Append([R("a-valid"), R("z-too-large", trio)]), "late capacity failure is atomic");
        check(almostFull.Serialize().SequenceEqual(before) && almostFull.Append([R("retry")]).HighWater == 256,
            "Failure consumed earlier reservations or changed persisted bytes.");
        reject(() => za.Append([R("new"), R("z", changedDonor)]), "late identity failure");
        byte[] rollback = za.Serialize();
        reject(() => za.Append([R("a-valid-new"), R("z", changedRole)]), "later family role change rolls back complete batch");
        check(za.Serialize().SequenceEqual(rollback) && za.Append([R("next")]).Entries.Single(e => e.FamilyId == "next").NativeIndex == 130,
            "Role-change batch failure consumed the earlier accepted-in-batch reservation.");
        check(za.Serialize().SequenceEqual(layout.Restore(za.Serialize()).Serialize()) && za.HighWater == 130,
            "Source/role failure changed accepted mapping.");
        reject(() => za.Append([R("dup"), R("dup")]), "duplicate family IDs");
        reject(() => za.Append([R("bad id")]), "bounded identity grammar");
        reject(() => za.Append([null!]), "null request");
        reject(() => za.Append((IEnumerable<NativeMobyPersistentOwnerRequest>)null!), "null batch");

        // Snapshot guards are finite: the adversary throws if a reader advances
        // beyond max+1. No infinite enumeration or memory-stress run is needed.
        var oversized = new BudgetEnumerable<NativeMobyPersistentOwnerRequest>(257, i => R("f" + i));
        reject(() => empty.Append(oversized), "bounded request materialization");
        check(oversized.MoveNextCalls <= 257, "Request enumeration continued beyond the rejection boundary.");
        byte[] detached = za.Serialize(); detached[0] ^= 1;
        check(za.Serialize()[0] != detached[0], "Caller mutated serialized ledger storage.");

        void BadDocument(Action<JsonObject> edit, string label)
        {
            var json = JsonNode.Parse(za.Serialize())!.AsObject(); edit(json);
            reject(() => layout.Restore(Encoding.UTF8.GetBytes(json.ToJsonString())), label);
        }
        BadDocument(j => j["schemaVersion"] = 999, "unknown schema version");
        BadDocument(j => j["sourceImageSha256"] = new string('3', 64), "foreign image");
        BadDocument(j => j["originalRowsSha256"] = new string('4', 64), "stale original rows");
        BadDocument(j => j["levelId"] = 13, "foreign native level");
        BadDocument(j => j["saveSlot"] = 3, "foreign save slot");
        BadDocument(j => j["originalRowCount"] = 127, "changed original count");
        BadDocument(j => j["highWater"] = 129, "high-water rollback");
        BadDocument(j => j["highWater"] = 131, "unexplained gap cannot mint a reservation");
        BadDocument(j => j["entries"]![1]!["nativeIndex"] = 128, "aliased serialized ordinal");
        BadDocument(j => j["entries"]![1]!["familyId"] = "z", "duplicate role identity");
        BadDocument(j => j["entries"]![0]!["sourceVariantId"] = "forged", "unbound serialized donor identity");
        BadDocument(j => j["unknownOwnerPolicy"] = true, "unknown root field cannot grant a policy");
        BadDocument(j => j["entries"]![0]!["unknownRetirementEncoding"] = "FD", "unknown entry field cannot grant an encoding");
        BadDocument(j => j["entries"]![0]!["roleId"] = new string('x', 4097), "oversize entry identity");
        string canonical = Encoding.UTF8.GetString(za.Serialize());
        reject(() => layout.Restore(Encoding.UTF8.GetBytes("{\"highWater\":130," + canonical[1..])),
            "duplicate JSON property even if its value agrees");
        reject(() => layout.Restore(Encoding.UTF8.GetBytes("{\"entries\":" + new string('[', 65) + "0" + new string(']', 65) + "}")),
            "bounded nested JSON parser");
        var partialRetirement = JsonNode.Parse(retiredMulti.Serialize())!.AsObject();
        partialRetirement["entries"]![0]!["retired"] = false;
        reject(() => multiLayout.Restore(Encoding.UTF8.GetBytes(partialRetirement.ToJsonString())), "partial family retirement on restore");
        reject(() => layout.Restore("{}"u8.ToArray()), "missing document fields");
        reject(() => layout.Restore(new byte[1024 * 1024 + 1]), "bounded document parser");
        check(z.Serialize().SequenceEqual(zBytes), "Hostile restore mutated an earlier ledger.");
    }

    private sealed class BudgetEnumerable<T>(int maximumCalls, Func<int, T> item) : IEnumerable<T>
    {
        internal int MoveNextCalls { get; private set; }
        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; ; i++)
            {
                if (++MoveNextCalls > maximumCalls) throw new InvalidOperationException("Enumerator was advanced beyond max+1.");
                yield return item(i);
            }
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
