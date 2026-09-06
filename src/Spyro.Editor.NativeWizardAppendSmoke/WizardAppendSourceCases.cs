using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;

internal static class WizardAppendSourceCases
{
    internal static void Run(string image, string workspace, Action<bool, string> check, Action<Action, string> reject)
    {
        var catalog = LevelCatalog.Load(workspace);
        var source = NativeDarkHollowWizardAppendSource.Capture(image, catalog);
        var empty = source.EmptyOwners();
        byte[] emptyBefore = empty.Serialize();
        var first = source.CompileActive(empty, [new("z")]);
        check(empty.Serialize().SequenceEqual(emptyBefore) && empty.HighWater == 128,
            "Producing a proposed scene/ledger must not mutate the accepted empty state.");
        check(first.ProposedOwners.SourceCertified && first.ProposedOwners.HighWater == 129 &&
              first.Families.Single().FamilyId == "z" && first.Families.Single().NativeIndex == 128,
            "First source-issued Wizard must reserve persistent native owner128.");
        byte[] firstLedger = first.ProposedOwners.Serialize(), firstBytes = first.CopyScene();
        check(first.FinalSceneSha256 == "9ac99fdd150133a75aac8298da4b738dcbcdd12a1ab649088f53c300bd1a37c4",
            "New single-role producer must preserve the independently verified original one-Wizard scene bytes.");

        // This is a real re-capture with fresh source/address objects, not an
        // incremental call retaining the previous in-memory allocator.
        var reopenedSource = NativeDarkHollowWizardAppendSource.Capture(image, catalog);
        var reopenedOwners = reopenedSource.RestoreOwners(firstLedger);
        var second = reopenedSource.CompileActive(reopenedOwners, [new("a"), new("z")]);
        check(second.ProposedOwners.HighWater == 130 &&
              second.ProposedOwners.Entries.Single(e => e.FamilyId == "z").NativeIndex == 128 &&
              second.ProposedOwners.Entries.Single(e => e.FamilyId == "a").NativeIndex == 129,
            "Temporal z128 -> reopen -> lexically earlier a must preserve z128 and append a129.");
        check(second.Families.Select(f => (f.FamilyId, f.NativeIndex)).SequenceEqual(new[] { ("z", 128), ("a", 129) }),
            "Native materialized family owners must follow ledger ordinals, not fresh batch lexical order.");
        check(second.FinalSceneSha256 == "0766f7e16adc2a58ccbc2797d6aba0d2ca4cd5ef5d788ee99599f27e9bd78fb7",
            "Two-Wizard scene must match the separate raw-sector native splice oracle.");
        var reversed = reopenedSource.CompileActive(reopenedOwners, [new("z"), new("a")]);
        check(reversed.CopyScene().SequenceEqual(second.CopyScene()) &&
              reversed.ProposedOwners.Serialize().SequenceEqual(second.ProposedOwners.Serialize()) &&
              reversed.GraphFingerprint == second.GraphFingerprint,
            "Active view/request order cannot change native scene, persistent mapping or dependency graph.");
        var twiceReopened = source.RestoreOwners(second.ProposedOwners.Serialize());
        var repeated = source.CompileActive(twiceReopened, [new("a"), new("z")]);
        check(repeated.CopyScene().SequenceEqual(second.CopyScene()) &&
              repeated.ProposedOwners.Serialize().SequenceEqual(second.ProposedOwners.Serialize()),
            "Reopened complete active state must be idempotent without new allocations.");
        check(reopenedOwners.Serialize().SequenceEqual(firstLedger) && first.CopyScene().SequenceEqual(firstBytes),
            "Later compilation mutated the accepted source/first scene/ledger.");
        check(!second.PlacementApproved && !second.BehaviorClosureProven && !second.RuntimeFitProven &&
              !second.RuntimeVerified && !second.PublicationApproved && !second.ProjectTransactionIntegrated &&
              !second.AcceptedExportHistoryProven,
            "Actual scene bytes and identity bookkeeping must not promote placement, behavior, App/history or publication.");
        check(second.PlacedCountBefore == 128 && second.PlacedCountAfter == 130 && second.FixupCountAfter == 151 &&
              second.DynamicCapacityBefore == 91 && second.DynamicCapacityAfter == 89 && second.SceneByteLength == 0x11800,
            "Actual native row/fixup and shared dynamic-arena debit differs from source parsing.");

        var inventory = NativeMobySourceInventory.Capture(image, catalog);
        var trueDonor = inventory.Levels.Single(l => l.LevelId == 33).Rows.Single(r => r.TrueIndex == 6);
        byte[] originalExe = ReadLogical(image, 53875, 0, 0x66000);
        check(Hash(originalExe) == "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998" &&
              Word(originalExe, 0x6002C) == 0x80010168 && Word(originalExe, 0x60030) == 0x8001015C &&
              originalExe.AsSpan(0x968, 12).SequenceEqual("WIZARD PEAK\0"u8) &&
              originalExe.AsSpan(0x95C, 9).SequenceEqual("BLOWHARD\0"u8),
            "Original native title slots15/16 distinguish Wizard Peak33 from Blowhard34; display labels cannot replace source identity.");
        check(inventory.Levels.Single(l => l.LevelId == 33) is { DataWadEntry: 40, LevelKey: "wizardpeak", DisplayName: "Wizard Peak" } &&
              inventory.Levels.Single(l => l.LevelId == 34) is { DataWadEntry: 42, LevelKey: "blowhard", DisplayName: "Blowhard" },
            "Captured catalog naming agrees with the exact original native titles and WAD owners.");
        check(second.Families.All(f => f.RoleId == "wizard" && f.DonorLevelId == 33 && f.DonorTrueIndex == 6 &&
                    f.SourceVariantId == trueDonor.SourceVariantId) &&
              second.ProposedOwners.Entries.All(e => e.RoleId == "wizard" && e.SourceVariantId == trueDonor.SourceVariantId),
            "Native scene must bind exact actual Wizard Peak33/T6, not the ledger smoke's distinct Blowhard34/T6.");
        byte[] before = second.CopyOriginalScene(), after = second.CopyScene();
        byte[] donorData = ReadWad(image, 0x2F6B000, 0x28A000);
        check(Hash(donorData) == "9973493f865b5e86bc17b32c5af9e3d7596e25615618c007ed28e0379161f2d8",
            "Independent raw donor read must retain exact full native data ownership.");
        byte[] donor = donorData.AsSpan(0x1CE800, 0x11800).ToArray();
        byte[] oracle = Splice(before, donor, 2);
        check(after.SequenceEqual(oracle), "Every final scene byte must equal the independent native splice, not only its summary hash.");
        foreach (var family in second.Families)
        {
            int n = family.NativeIndex - 128, row = 0xB838 + n * 0x58, props = 0xEED4 + n * 0x50;
            check(family.RowOffset == row && family.PropertiesOffset == props &&
                  Word(after, row) == props && Word(after, props) == props + 0x28,
                "Family row/private self references must bind distinct exact final owners.");
            check(after.AsSpan(row + 4, 0x54).SequenceEqual(donor.AsSpan(0x638C, 0x54)) &&
                  after.AsSpan(props + 4, 0x4C).SequenceEqual(donor.AsSpan(0xC354, 0x4C)),
                "Donor row/routes must remain byte-exact except the two declared relocations; no placement claim.");
        }
        check(after.AsSpan(0x8C38, 128 * 0x58).SequenceEqual(before.AsSpan(0x8C38, 128 * 0x58)) &&
              after.AsSpan(0xE03C, 0xEED4 - 0xE03C).SequenceEqual(before.AsSpan(0xE03C, 0xEED4 - 0xE03C)),
            "All original rows and complete original private properties must remain unchanged.");
        check(after.AsSpan(0xEF74, 0x10EF0 - 0xEED4).SequenceEqual(before.AsSpan(0xEED4, 0x10EF0 - 0xEED4)),
            "Whole native pod/collision suffix must move once by the two complete private extents.");
        check(Word(after, 0x10F90) == 151 && after.AsSpan(0x10F94, 147 * 4).SequenceEqual(before.AsSpan(0x10EF4, 147 * 4)) &&
              Enumerable.Range(0, 4).Select(i => Word(after, 0x111E0 + i * 4)).SequenceEqual(new uint[] { 0xB838, 0xEED4, 0xB890, 0xEF24 }) &&
              !after.AsSpan(0x111F0).ContainsAnyExcept((byte)0),
            "All147 old fixups stay exact, four new fields retain ledger order, and final zero tail stays owned.");
        second.VerifyOriginalAndFinal(before, after);
        byte[] corrupt = after.ToArray(); corrupt[0xEF28] ^= 1;
        reject(() => second.VerifyOriginalAndFinal(before, corrupt), "late private complement corruption");
        byte[] stale = before.ToArray(); stale[0x8C70] ^= 1;
        reject(() => second.VerifyOriginalAndFinal(stale, after), "original native row changed");
        byte[] detached = second.CopyScene(); detached[0] ^= 1;
        byte[] detachedLedger = second.ProposedOwners.Serialize(); detachedLedger[0] ^= 1;
        check(second.CopyScene().SequenceEqual(after) && second.ProposedOwners.Serialize().SequenceEqual(twiceReopened.Serialize()),
            "Returned arrays cannot mutate scene or ledger owners.");

        reject(() => reopenedSource.CompileActive(reopenedOwners, [new("a")]), "missing prior active family must not compact z");
        reject(() => reopenedSource.CompileActive(reopenedOwners, []), "empty active set must not silently delete prior family");
        reject(() => reopenedSource.CompileActive(reopenedOwners, [new("z"), new("z")]), "duplicate family request");
        reject(() => reopenedSource.CompileActive(reopenedOwners, [new("z"), null!]), "late null request must be atomic");
        reject(() => reopenedSource.CompileActive(reopenedOwners, [new("z"), new("bad/id")]), "invalid stable family ID");
        var retired = reopenedOwners.Retire(["z"]);
        reject(() => reopenedSource.CompileActive(retired, [new("a")]), "retired reserved native row has no proved tombstone");
        reject(() => reopenedSource.CompileActive(retired, [new("z")]), "active request cannot silently reactivate retired owner");
        var rawOwnerSource = NativeMobyPersistentOwnerSource.Capture(image, catalog, 12);
        foreach (var selections in new NativeMobyPersistentOwnerRoleSelection[][]
        {
            [new("wizard", 34, 6)], [new("changed-role", 33, 6)], [new("wizard", 33, 7)],
            [new("a", 33, 6), new("wizard", 33, 6)]
        })
        {
            var wrong = rawOwnerSource.Empty().Append([new("z", rawOwnerSource.BindFamily(selections))]);
            reject(() => source.CompileActive(wrong, [new("z")]), "real source-issued row grouping does not prove the required exact one-role private schema");
        }
        var town = NativeMobyPersistentOwnerSource.Capture(image, catalog, 13);
        reject(() => source.CompileActive(town.Empty(), [new("z")]), "foreign target scene/save owner");
        var forged = JsonNode.Parse(firstLedger)!.AsObject();
        forged["entries"]![0]!["sourceVariantId"] = "caller-forged-schema";
        reject(() => source.RestoreOwners(Encoding.UTF8.GetBytes(forged.ToJsonString())), "caller JSON is not native source authority");
        var forgedCatalog = new LevelCatalog(catalog.Levels.Select(l => l.LevelId == 33 ? l with { SourceRecordCount = 1 } : l));
        reject(() => NativeDarkHollowWizardAppendSource.Capture(image, forgedCatalog), "stale donor locator metadata");

        int advances = 0;
        IEnumerable<NativeDarkHollowWizardAppendInstance> Guarded()
        {
            for (int i = 0; i <= 256; i++) { advances++; yield return new("guard-" + i); }
            throw new InvalidOperationException("Unbounded enumeration advanced to max+2.");
        }
        reject(() => source.CompileActive(empty, Guarded()), "finite max+1 request bound before native materialization");
        check(advances <= 257, "Request materialization crossed its finite input bound.");
        // Exact zero-tail limit is floor((11800-11140)/(50+8))=19;
        // these requests still fit the row arena and native256 identity limit.
        var boundaryRequests = Enumerable.Range(0, 19).Select(i => new NativeDarkHollowWizardAppendInstance("boundary-" + i.ToString("D2"))).ToArray();
        var boundary = source.CompileActive(empty, boundaryRequests);
        check(boundary.PlacedCountAfter == 147 && boundary.FixupCountAfter == 185 &&
              boundary.DynamicCapacityAfter == (0xE038 - 0x8C38 - 147 * 0x58) / 0x70 &&
              boundary.CopyScene().SequenceEqual(Splice(before, donor, 19)) &&
              !boundary.CopyScene().AsSpan(0x117C8, 0x38).ContainsAnyExcept((byte)0),
            "Exact19-family tail success must retain native independent splice, count, pool debit and final38 zero bytes.");
        reject(() => source.CompileActive(empty, [..boundaryRequests, new("boundary-19")]),
            "The20th family exhausts native scene tail by20 bytes despite remaining row/owner capacity.");
        // This distinct negative reaches native row capacity before tail. Every
        // requested identity itself still fits the ledger's<=256 bound.
        var tooMany = Enumerable.Range(0, 128).Select(i => new NativeDarkHollowWizardAppendInstance("capacity-" + i)).ToArray();
        reject(() => source.CompileActive(empty, tooMany), "actual native row-arena capacity failure");
        check(empty.Serialize().SequenceEqual(emptyBefore) && reopenedOwners.Serialize().SequenceEqual(firstLedger),
            "A failed family/bijection/schema/capacity batch mutated accepted owner state.");
        check(source.CompileActive(empty, [new("z")]).CopyScene().SequenceEqual(firstBytes),
            "Retry after rejected batches must still reserve the same first owner and exact bytes.");
        var legacy = NativeDarkHollowWizardScenePlan.Capture(image);
        check(legacy.CopyScene().SequenceEqual(firstBytes) && legacy.FinalLevelSha256 == "6fd296b8f56f70f1d993596fe262076e3b2674865c24a1070543979334e56e38",
            "Existing fixed compiler API and original one-candidate scene/level goldens must remain unchanged.");
    }

    private static byte[] Splice(byte[] source, byte[] donor, int extra)
    {
        byte[] result = source.ToArray();
        int delta = extra * 0x50;
        source.AsSpan(0xEED4, 0x10EF0 - 0xEED4).CopyTo(result.AsSpan(0xEED4 + delta));
        Put(result, 0x8C34, (uint)(128 + extra)); Put(result, 0xE038, Word(source, 0xE038) + (uint)delta);
        source.AsSpan(0x10EF4, 147 * 4).CopyTo(result.AsSpan(0x10EF4 + delta));
        Put(result, 0x10EF0 + delta, (uint)(147 + 2 * extra));
        for (int i = 0; i < extra; i++)
        {
            int row = 0xB838 + i * 0x58, props = 0xEED4 + i * 0x50;
            donor.AsSpan(0x6388, 0x58).CopyTo(result.AsSpan(row));
            donor.AsSpan(0xC350, 0x50).CopyTo(result.AsSpan(props));
            Put(result, row, (uint)props); Put(result, props, (uint)(props + 0x28));
            Put(result, 0x11140 + delta + i * 8, (uint)row); Put(result, 0x11144 + delta + i * 8, (uint)props);
        }
        return result;
    }

    private static byte[] ReadWad(string path, int offset, int length) => ReadLogical(path, 37, offset, length);

    private static byte[] ReadLogical(string path, int baseLba, int offset, int length)
    {
        using var image = File.OpenRead(path);
        byte[] bytes = new byte[length];
        for (int at = 0; at < length;)
        {
            int n = Math.Min(length - at, 2048 - offset % 2048);
            image.Position = ((long)baseLba + offset / 2048) * 2352 + 24 + offset % 2048;
            image.ReadExactly(bytes.AsSpan(at, n));
            at += n; offset += n;
        }
        return bytes;
    }
    private static uint Word(ReadOnlySpan<byte> bytes, int at) => BinaryPrimitives.ReadUInt32LittleEndian(bytes[at..]);
    private static void Put(Span<byte> bytes, int at, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes[at..], value);
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
