using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Persistence;

internal static class Program
{
    private static int checks;
    private static readonly Guid Content = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
    private static Guid Id(int index) => Guid.Parse($"10000000-0000-0000-0000-{index:x12}");
    private static void Check(bool condition, string description)
    { checks++; if (!condition) throw new Exception(description); }
    private static void Refuses(Action action, string description)
    {
        try { action(); }
        catch (InvalidDataException) { Check(true, description); return; }
        catch (ArgumentException) { Check(true, description); return; }
        catch (NotSupportedException) { Check(true, description); return; }
        throw new Exception("Expected refusal: " + description);
    }
    private static byte[] Mutate(byte[] original, Action<byte[]> mutation, bool repairChecksum = true)
    {
        byte[] bytes = original.ToArray(); mutation(bytes);
        if (repairChecksum) SHA256.HashData(bytes.AsSpan(0, bytes.Length - 32)).CopyTo(bytes, bytes.Length - 32);
        return bytes;
    }
    private static void Put32(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
    private static void Put16(byte[] bytes, int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);

    private static void Main(string[] args)
    {
        if (args.Length != 0) throw new InvalidDataException("This smoke is source-free and accepts no paths or write options.");
        ManifestIdentity();
        SlotIsolationAndMigration();
        CapacityAndBoundaries();
        ManifestRefusals();
        SaveRefusalsAndPreservation();
        GoldenLayout();
        Console.WriteLine($"DETACHED_COLLECTIBLE_SAVE_SOURCE_FREE_OK checks={checks} sourceReads=0 candidateWrites=0 cardWrites=0 nativeOwnership=false");
    }

    private static void ManifestIdentity()
    {
        Guid[] ids = [Id(3), Id(1), Id(2)];
        var manifest = DetachedCollectibleManifest.CreateNew(Content, ids);
        var reordered = DetachedCollectibleManifest.CreateNew(Content, ids.Reverse());
        Check(manifest.Sha256 == reordered.Sha256, "placement enumeration cannot reassign initial identity bits");
        Check(manifest.SerializeCanonical().SequenceEqual(reordered.SerializeCanonical()), "canonical ledger independent of placement enumeration");
        Check(manifest.Revision == 1 && manifest.ParentSha256 == null && manifest.ActiveCount == 3, "explicit new ledger version");
        for (int index = 1; index <= 3; index++) Check(manifest.GetEntry(Id(index)).BitIndex == index - 1, "canonical GUID order");
        ids[0] = Id(999);
        Check(manifest.GetEntry(Id(3)).BitIndex == 2, "constructor does not retain mutable input");
        byte[] bytes = manifest.SerializeCanonical(); bytes[0] = 0;
        Check(manifest.SerializeCanonical()[0] == (byte)'S', "manifest serialization returns a copy");
        var parsed = DetachedCollectibleManifest.Parse(manifest.SerializeCanonical());
        Check(parsed.Sha256 == manifest.Sha256, "manifest serialization roundtrip");
        Check(parsed.Entries.SequenceEqual(manifest.Entries), "parsed identity ownership is exact");
        Refuses(() => ((IList<DetachedCollectibleManifest.Entry>)manifest.Entries)[0] = new(Id(88), 0, false), "read-only entry list");
        var successor = parsed.Advance([Id(9), Id(7), Id(8)], [Id(2)]);
        successor.RequireDirectSuccessorOf(manifest);
        Check(successor.Revision == 2 && successor.ParentSha256 == manifest.Sha256, "exact predecessor hash and next revision");
        Check(successor.LifetimeCount == 6 && successor.ActiveCount == 5, "retired identity retains allocated capacity");
        Check(successor.GetEntry(Id(2)).Retired && successor.GetEntry(Id(2)).BitIndex == 1, "retirement retains exact bit");
        for (int index = 1; index <= 3; index++) Check(successor.GetEntry(Id(index)).BitIndex == index - 1, "prior bits survive append");
        for (int index = 7; index <= 9; index++) Check(successor.GetEntry(Id(index)).BitIndex == index - 4, "new batch canonical order appends");
        var successorReordered = manifest.Advance([Id(8), Id(9), Id(7)], [Id(2)]);
        Check(successor.Sha256 == successorReordered.Sha256, "addition enumeration cannot alter successor bits");
        var lowerIdAppend = manifest.Advance([Id(0)], []);
        Check(lowerIdAppend.GetEntry(Id(0)).BitIndex == 3 && lowerIdAppend.GetEntry(Id(1)).BitIndex == 0,
            "later lower-sorting GUID must append without resorting historical ownership");
        var successorParsed = DetachedCollectibleManifest.Parse(successor.SerializeCanonical());
        successorParsed.RequireDirectSuccessorOf(parsed);
        Check(successorParsed.Sha256 == successor.Sha256, "history survives serialization");
        Refuses(() => successorParsed.Advance([Id(2)], []), "retired ID cannot be reused after reload");
        Refuses(() => successorParsed.Advance([], [Id(2)]), "retirement is explicit and not repeatable");
        Refuses(() => manifest.Advance([Id(1)], []), "active ID cannot be reused");
        Refuses(() => manifest.Advance([], [Id(99)]), "missing identity cannot be retired");
        Refuses(() => manifest.Advance([Id(99)], [Id(99)]), "new identity cannot be retired in the same update");
        Refuses(() => manifest.Advance([], []), "no untracked revision-only edits");
        Refuses(() => successor.RequireDirectSuccessorOf(successor), "self is not a successor");
    }

    private static void SlotIsolationAndMigration()
    {
        var first = DetachedCollectibleManifest.CreateNew(Content, Enumerable.Range(0, 7).Select(Id));
        var empty = DetachedCollectibleSave.CreateNew(first);
        var earned = empty.WithCollected(0, Id(0)).WithCollected(1, Id(1)).WithCollected(2, Id(6));
        for (int slot = 0; slot < 3; slot++)
            for (int index = 0; index < 7; index++)
            {
                Check(!empty.IsEarned(slot, Id(index)), "new state remains immutable");
                Check(earned.IsEarned(slot, Id(index)) == (index == (slot == 2 ? 6 : slot)), "three ordinary slots have independent bits");
            }
        Check(DetachedCollectibleSaveCodec.Serialize(earned).SequenceEqual(DetachedCollectibleSaveCodec.Serialize(earned.WithCollected(0, Id(0)))), "repeated pickup is idempotent");
        byte[] exposed = earned.CopySlotBits(0); exposed[0] = 0;
        Check(earned.IsEarned(0, Id(0)), "slot bitset returns a copy");
        var second = first.Advance([Id(7), Id(8), Id(9)], [Id(0), Id(6)]);
        var migrated = earned.MigrateTo(second);
        Check(earned.BytesPerSlot == 1 && migrated.BytesPerSlot == 2, "migration crosses byte boundary");
        for (int slot = 0; slot < 3; slot++)
        {
            for (int index = 0; index < 7; index++) Check(migrated.IsEarned(slot, Id(index)) == earned.IsEarned(slot, Id(index)), "earned history survives retirement and stride growth");
            for (int index = 7; index < 10; index++) Check(!migrated.IsEarned(slot, Id(index)), "new owner starts unearned without inheriting retired bits");
        }
        Check(migrated.EarnedActiveInstances(0).Count == 0 && migrated.IsEarned(0, Id(0)), "inventory omits retired owner but retains earned evidence");
        Check(migrated.EarnedActiveInstances(1).SequenceEqual(new[] { Id(1) }), "inventory lists only active earned identity");
        Check(migrated.EarnedActiveInstances(2).Count == 0 && migrated.IsEarned(2, Id(6)), "third slot retired history is independent");
        Refuses(() => migrated.WithCollected(1, Id(0)), "retired identity cannot earn a new bit");
        var retiredOnly = second.Advance([], [Id(1)]);
        var migratedTwice = migrated.MigrateTo(retiredOnly);
        Check(migratedTwice.IsEarned(1, Id(1)) && migratedTwice.EarnedActiveInstances(1).Count == 0, "retirement-only update preserves earned bit");
        Refuses(() => earned.MigrateTo(retiredOnly), "cannot skip intermediate manifests");
        Refuses(() => migrated.MigrateTo(first), "cannot downgrade and discard earned ownership");
        Refuses(() => earned.MigrateTo(DetachedCollectibleManifest.CreateNew(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"), [Id(0)])), "cannot migrate across content identities");
        var fork = first.Advance([Id(10)], []);
        Refuses(() => migrated.MigrateTo(fork), "sibling branch is not a successor");
        byte[] original = DetachedCollectibleSaveCodec.Serialize(earned);
        var mismatch = DetachedCollectibleSaveCodec.Inspect(original, second);
        Check(mismatch.Status == DetachedCollectibleReadStatus.ManifestMismatch && mismatch.State == null, "no implicit migration or fresh-state fallback");
        var loadedOld = DetachedCollectibleSaveCodec.Inspect(original, first).RequireCompatible();
        Check(loadedOld.MigrateTo(second).IsEarned(2, Id(6)), "explicit decode with exact old manifest then migration");
        var roundtrip = DetachedCollectibleSaveCodec.Inspect(DetachedCollectibleSaveCodec.Serialize(migratedTwice), retiredOnly).RequireCompatible();
        Check(roundtrip.IsEarned(0, Id(0)) && roundtrip.IsEarned(1, Id(1)) && roundtrip.IsEarned(2, Id(6)), "retired earned state survives save roundtrip");
        foreach (int slot in new[] { -1, 3, int.MinValue, int.MaxValue })
        {
            Refuses(() => earned.IsEarned(slot, Id(0)), "invalid ordinary slot read");
            Refuses(() => earned.WithCollected(slot, Id(0)), "invalid ordinary slot collection");
            Refuses(() => earned.CopySlotBits(slot), "invalid ordinary slot bitset");
            Refuses(() => earned.EarnedActiveInstances(slot), "invalid ordinary slot inventory");
        }
        Refuses(() => earned.WithCollected(0, Id(99)), "no implicit allocation during pickup");
        Refuses(() => earned.IsEarned(0, Guid.Empty), "empty identity is not an earned owner");
    }

    private static void CapacityAndBoundaries()
    {
        var emptyManifest = DetachedCollectibleManifest.CreateNew(Content, []);
        byte[] emptyBytes = DetachedCollectibleSaveCodec.Serialize(DetachedCollectibleSave.CreateNew(emptyManifest));
        Check(emptyBytes.Length == 128, "explicit empty manifest encodes without inventing collectibles");
        Check(DetachedCollectibleSaveCodec.Inspect(emptyBytes, emptyManifest).RequireCompatible().EarnedActiveInstances(0).Count == 0, "zero-added-content baseline has zero inventory");
        foreach (int count in new[] { 1, 7, 8, 9, 15, 16, 17, 4095, 4096 })
        {
            var manifest = DetachedCollectibleManifest.CreateNew(Content, Enumerable.Range(0, count).Select(Id).Reverse());
            var save = DetachedCollectibleSave.CreateNew(manifest);
            for (int slot = 0; slot < 3; slot++) save = save.WithCollected(slot, Id(count - 1));
            byte[] bytes = DetachedCollectibleSaveCodec.Serialize(save);
            Check(bytes.Length == 128 + 3 * ((count + 7) / 8), "exact bounded bitset length");
            var loaded = DetachedCollectibleSaveCodec.Inspect(bytes, manifest).RequireCompatible();
            for (int slot = 0; slot < 3; slot++)
            {
                Check(loaded.IsEarned(slot, Id(count - 1)), "final lifetime bit belongs to each independent slot");
                Check(loaded.EarnedActiveInstances(slot).Count == 1, "last bit does not bleed into adjacent slot");
            }
            if (count == 4096)
            {
                Check(bytes.Length == 1664 && bytes.Length == DetachedCollectibleSaveCodec.MaximumEncodedBytes, "maximum detached save byte count, not a native address claim");
                Check(manifest.SerializeCanonical().Length == 98416, "maximum bounded ledger byte count");
                var retired = manifest.Advance([], [Id(0)]);
                Refuses(() => retired.Advance([Id(5000)], []), "retirement does not recycle lifetime capacity");
                var allBits = Enumerable.Repeat((byte)255, 1536).ToArray();
                var allEarned = DetachedCollectibleSave.FromValidatedBits(manifest, allBits);
                allBits[0] = 0;
                var allLoaded = DetachedCollectibleSaveCodec.Inspect(DetachedCollectibleSaveCodec.Serialize(allEarned), manifest).RequireCompatible();
                for (int slot = 0; slot < 3; slot++)
                {
                    Check(allLoaded.EarnedActiveInstances(slot).Count == 4096, "full earned bitset roundtrips in every slot");
                    for (int id = 0; id < count; id++) Check(allEarned.IsEarned(slot, Id(id)), "all 12,288 distinct slot/instance cells survive immutable capture");
                }
            }
        }
        Refuses(() => DetachedCollectibleManifest.CreateNew(Content, Enumerable.Range(0, 4097).Select(Id)), "over-capacity input is refused");
        int yielded = 0;
        IEnumerable<Guid> BoundedProbe()
        {
            for (int index = 0; ; index++) { yielded++; if (yielded > 4097) throw new Exception("Unbounded enumeration"); yield return Id(index); }
        }
        Refuses(() => DetachedCollectibleManifest.CreateNew(Content, BoundedProbe()), "enumeration stops at policy capacity");
        Check(yielded == 4097, "does not enumerate an unbounded sequence beyond capacity detection");
        Refuses(() => DetachedCollectibleManifest.CreateNew(Guid.Empty, []), "empty content identity refused");
        Refuses(() => DetachedCollectibleManifest.CreateNew(Content, [Guid.Empty]), "empty instance identity refused");
        Refuses(() => DetachedCollectibleManifest.CreateNew(Content, [Id(0), Id(0)]), "duplicate initial identity refused");
        Refuses(() => emptyManifest.Advance([Id(0), Id(0)], []), "duplicate addition refused");
        var single = emptyManifest.Advance([Id(0)], []);
        Refuses(() => single.Advance([], [Id(0), Id(0)]), "duplicate retirement refused");
        Refuses(() => DetachedCollectibleSave.FromValidatedBits(single, [0x80, 0, 0]), "unassigned bits are never silently masked");
        Refuses(() => DetachedCollectibleSave.FromValidatedBits(single, [0, 0]), "slot bits cannot omit third ordinary slot");
        Refuses(() => DetachedCollectibleSave.FromValidatedBits(single, [0, 0, 0, 0]), "slot bits cannot add a hidden fourth slot");
    }

    private static void ManifestRefusals()
    {
        var first = DetachedCollectibleManifest.CreateNew(Content, [Id(0), Id(1), Id(2)]);
        byte[] canonical = first.SerializeCanonical();
        foreach (int size in new[] { 0, 7, 8, 79, 111, canonical.Length - 1 })
            Refuses(() => DetachedCollectibleManifest.Parse(canonical.AsSpan(0, size)), "truncated manifest");
        var oversized = new byte[DetachedCollectibleManifest.MaximumEncodedBytes + 1]; canonical.CopyTo(oversized, 0);
        Refuses(() => DetachedCollectibleManifest.Parse(oversized), "oversized ledger");
        foreach (Action<byte[]> change in new Action<byte[]>[]
        {
            b => b[0] ^= 1, b => Put16(b, 8, 2), b => Put16(b, 0xA, 79), b => Put32(b, 0xC, 1),
            b => Put32(b, 0x24, 4097), b => b[0x48] = 1, b => b.AsSpan(0x10, 16).Clear(), b => Put32(b, 0x20, 0),
            b => b[0x28] = 1, b => Put32(b, 0x20, 2), b => b.AsSpan(80, 16).Clear(),
            b => b.AsSpan(80, 16).CopyTo(b.AsSpan(104, 16)), b => Put32(b, 96, 1), b => b[100] = 2,
            b => b[101] = 1, b => b[100] = 1,
            b => { byte[] a = b.AsSpan(80, 16).ToArray(); b.AsSpan(104, 16).CopyTo(b.AsSpan(80, 16)); a.CopyTo(b, 104); }
        }) Refuses(() => DetachedCollectibleManifest.Parse(Mutate(canonical, change)), "structural manifest violation even with recomputed checksum");
        for (int offset = 0; offset < canonical.Length; offset++)
            Refuses(() => DetachedCollectibleManifest.Parse(Mutate(canonical, b => b[offset] ^= 1, false)), "every single-byte mutation of fixture is refused");
        var second = first.Advance([Id(3)], [Id(0)]);
        byte[] secondBytes = second.SerializeCanonical();
        var reassigned = DetachedCollectibleManifest.Parse(Mutate(secondBytes, b =>
        {
            byte[] a = b.AsSpan(80, 16).ToArray(); b.AsSpan(104, 16).CopyTo(b.AsSpan(80, 16)); a.CopyTo(b, 104);
        }));
        Refuses(() => reassigned.RequireDirectSuccessorOf(first), "well-formed successor cannot reassign two existing bit owners");
        var newRetired = DetachedCollectibleManifest.Parse(Mutate(secondBytes, b => b[80 + 3 * 24 + 20] = 1));
        Refuses(() => newRetired.RequireDirectSuccessorOf(first), "append cannot introduce a pre-retired identity");
        var badParent = DetachedCollectibleManifest.Parse(Mutate(secondBytes, b => b[0x28] ^= 1));
        Refuses(() => badParent.RequireDirectSuccessorOf(first), "predecessor hash binds exact ledger history");
        var third = second.Advance([Id(4)], []);
        var revived = DetachedCollectibleManifest.Parse(Mutate(third.SerializeCanonical(), b => b[100] = 0));
        Refuses(() => revived.RequireDirectSuccessorOf(second), "parsed successor cannot revive retired identity");
        var overflow = DetachedCollectibleManifest.Parse(Mutate(secondBytes, b => Put32(b, 0x20, uint.MaxValue)));
        Refuses(() => overflow.Advance([Id(4)], []), "revision cannot wrap to reused history");
        var unordered = first.Advance([Id(3), Id(4)], []);
        var swappedAppend = DetachedCollectibleManifest.Parse(Mutate(unordered.SerializeCanonical(), b =>
        {
            byte[] a = b.AsSpan(152, 16).ToArray(); b.AsSpan(176, 16).CopyTo(b.AsSpan(152, 16)); a.CopyTo(b, 176);
        }));
        Refuses(() => swappedAppend.RequireDirectSuccessorOf(first), "append order must be deterministic even after parse");
    }

    private static void SaveRefusalsAndPreservation()
    {
        var manifest = DetachedCollectibleManifest.CreateNew(Content, Enumerable.Range(0, 9).Select(Id));
        byte[] original = DetachedCollectibleSaveCodec.Serialize(DetachedCollectibleSave.CreateNew(manifest).WithCollected(2, Id(8)));
        void Rejected(byte[] bytes, DetachedCollectibleReadStatus? expected = null)
        {
            byte[] expectedBytes = bytes.ToArray();
            var result = DetachedCollectibleSaveCodec.Inspect(bytes, manifest);
            Check(result.State == null && result.Status != DetachedCollectibleReadStatus.Compatible, "invalid input never resets earned state");
            if (expected.HasValue) Check(result.Status == expected, "refusal category is explicit");
            Check(result.CopyOriginalBytes().SequenceEqual(expectedBytes), "refusal preserves exact opaque input");
            Check(result.OriginalSha256 == Convert.ToHexStringLower(SHA256.HashData(expectedBytes)), "refusal hashes exact opaque input");
            Refuses(() => result.RequireCompatible(), "refusal cannot be consumed as a new empty save");
            if (bytes.Length > 0)
            {
                bytes[0] ^= 1; byte[] exposed = result.CopyOriginalBytes(); exposed[0] ^= 2;
                Check(result.CopyOriginalBytes().SequenceEqual(expectedBytes), "opaque original has no mutable aliases");
            }
        }
        Rejected([], DetachedCollectibleReadStatus.UnknownOrLegacy);
        Rejected(new byte[3072], DetachedCollectibleReadStatus.UnknownOrLegacy);
        Rejected(Enumerable.Repeat((byte)0xFF, 3072).ToArray(), DetachedCollectibleReadStatus.UnknownOrLegacy);
        Rejected(Enumerable.Repeat((byte)0x69, 512).ToArray(), DetachedCollectibleReadStatus.UnknownOrLegacy);
        Rejected("unrecognized legacy payload"u8.ToArray(), DetachedCollectibleReadStatus.UnknownOrLegacy);
        for (int length = 0; length < original.Length; length++) Rejected(original.AsSpan(0, length).ToArray());
        for (int offset = 0; offset < original.Length; offset++) Rejected(Mutate(original, b => b[offset] ^= 1, false));
        Rejected(Mutate(original, b => Put16(b, 8, 2)), DetachedCollectibleReadStatus.UnsupportedVersion);
        foreach (Action<byte[]> change in new Action<byte[]>[]
        {
            b => Put16(b, 0xA, 95), b => Put32(b, 0xC, 1), b => Put32(b, 0x24, 4097),
            b => Put16(b, 0x28, 2), b => Put16(b, 0x28, 4), b => Put16(b, 0x2A, 1),
            b => b[0x2C] = 1, b => b[0x50] = 1,
            b => b[97] = 0x80, b => b[99] = 0x80, b => b[101] = 0x80
        }) Rejected(Mutate(original, change), DetachedCollectibleReadStatus.CorruptOrMalformed);
        foreach (Action<byte[]> change in new Action<byte[]>[]
        {
            b => b[0x10] ^= 1, b => Put32(b, 0x20, 2), b => b[0x30] ^= 1, b => Put32(b, 0x24, 10)
        }) Rejected(Mutate(original, change), DetachedCollectibleReadStatus.ManifestMismatch);
        byte[] trailing = new byte[original.Length + 1]; original.CopyTo(trailing, 0);
        Rejected(trailing, DetachedCollectibleReadStatus.CorruptOrMalformed);
        var otherManifest = DetachedCollectibleManifest.CreateNew(Content, Enumerable.Range(20, 9).Select(Id));
        var mismatch = DetachedCollectibleSaveCodec.Inspect(original, otherManifest);
        Check(mismatch.Status == DetachedCollectibleReadStatus.ManifestMismatch && mismatch.CopyOriginalBytes().SequenceEqual(original), "same content and count but different GUID owners cannot reinterpret earned bits");
        var valid = DetachedCollectibleSaveCodec.Inspect(original, manifest);
        Check(valid.Status == DetachedCollectibleReadStatus.Compatible, "valid exact payload accepted");
        Check(DetachedCollectibleSaveCodec.Serialize(valid.RequireCompatible()).SequenceEqual(original), "valid payload lossless canonical roundtrip");
        original[100] ^= 1;
        Check(valid.RequireCompatible().IsEarned(2, Id(8)), "decoded state is immutable against caller mutation");
    }

    private static void GoldenLayout()
    {
        var manifest = DetachedCollectibleManifest.CreateNew(Content, [Id(0), Id(1)]);
        byte[] ledger = manifest.SerializeCanonical();
        Check(Convert.ToHexStringLower(ledger.AsSpan(0x10, 16)) == "00112233445566778899aabbccddeeff", "content GUID is network-order bytes, not platform Guid memory order");
        Check(Convert.ToHexStringLower(ledger.AsSpan(80, 16)) == "10000000000000000000000000000000", "instance GUID wire order");
        Check(ledger.AsSpan(0, 8).SequenceEqual("SPYCMAN\0"u8), "manifest magic golden");
        Check(ledger.Length == 160 && BinaryPrimitives.ReadUInt16LittleEndian(ledger.AsSpan(8)) == 1 && ledger[0xA] == 80, "manifest version and header golden");
        Check(ledger.AsSpan(0x28, 32).ToArray().All(value => value == 0), "new manifest zero predecessor field");
        Check(SHA256.HashData(ledger.AsSpan(0, ledger.Length - 32)).SequenceEqual(ledger.AsSpan(ledger.Length - 32).ToArray()), "independent manifest checksum");
        // Independently encoded with Python struct/uuid/hashlib, not produced by this C# serializer.
        Check(manifest.Sha256 == "9755a8ce84d011bae172bfa92159c1b8fa8dae42dedf73ca71560f875dc8ee0d", "complete manifest independent wire-format golden");
        Check(Convert.ToHexStringLower(ledger.AsSpan(ledger.Length - 32)) == "9f615c818a9e0fd318ae815f01b1c8d742935f1aac701237a10d338fa1530964", "manifest checksum independent golden");
        byte[] save = DetachedCollectibleSaveCodec.Serialize(DetachedCollectibleSave.CreateNew(manifest).WithCollected(0, Id(1)).WithCollected(2, Id(0)));
        Check(save.Length == 131 && save.AsSpan(0, 8).SequenceEqual("SPYCSAV\0"u8), "save magic and length golden");
        Check(save[0x28] == 3 && save[0x2A] == 1 && save[96] == 2 && save[97] == 0 && save[98] == 1, "slot-major LSB-first bit ownership golden");
        Check(save.AsSpan(0x30, 32).SequenceEqual(SHA256.HashData(ledger)), "save binds complete manifest including ledger checksum");
        Check(SHA256.HashData(save.AsSpan(0, save.Length - 32)).SequenceEqual(save.AsSpan(save.Length - 32).ToArray()), "independent save checksum");
        Check(Convert.ToHexStringLower(SHA256.HashData(save)) == "e4dc34e56d4865c46f09c1594e43e0a88d42cc4fa7b7773ee466f0afc4e83baa", "complete save independent wire-format golden");
        Check(Convert.ToHexStringLower(save.AsSpan(save.Length - 32)) == "864d503ee829e38227c917740aab67b5ead32cc0ddbc4100a3c021e3790f2666", "save checksum independent golden");
    }
}
