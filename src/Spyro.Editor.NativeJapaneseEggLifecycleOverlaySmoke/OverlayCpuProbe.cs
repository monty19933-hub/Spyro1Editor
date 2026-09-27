using System.Buffers.Binary;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;
using F = AttemptFixtures;

// Bounded detached MIPS-I probes share the existing branch/load-delay interpreter.
// They do not emulate a native scene load, install a ledger residence, or boot a disc.
internal static class OverlayCpuProbe
{
    private const uint OverlayBase = 0x8007AA38, ExeBase = 0x8000F800, Visual = 0x80088A38,
        Restore = 0x80088B14, PostEdge = 0x80088AE4, Continue = 0x80085780;
    private const uint DispatchSp = 0x801FFFF8 - 0x20 - 0x20 - 0x1C0, FrameSp = DispatchSp - 0x2B0;
    private static readonly byte[] Retirement = F.Words(0x84850034,0x3C018007,0x24215778,0x04A0000C,0x8C210000,0x00052880,0x00A12820,
        0x20A2FFFC,0x20450004,0x8CA20000,0,0x1444FFFC,0,0x8C820004,0,0xACA20000,0x3C018007,
        0x24215890,0x8C210000,0,0x00240822,0x18200003,0x2002FFFD,0x03E00008,0xA0820048);
    private static byte[] Snapshot(RenderProbeCpu cpu, uint at, int bytes) => Enumerable.Range(0, bytes).Select(i => (byte)cpu.Get(at + (uint)i, 1)).ToArray();
    private static uint Word(byte[] bytes, int at) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at));

    private static RenderProbeCpu Load(NativeJapaneseEggLifecycleOverlayPlan plan, NativeJapaneseEggAuthoredClassesPlan authored,
        DetachedCollectibleSave save, int slot, bool near = true)
    {
        var cpu = new RenderProbeCpu { StrictSignedArithmetic = true };
        cpu.Copy(ExeBase, plan.CopyExecutable()); cpu.Copy(OverlayBase, plan.CopyOverlay());
        cpu.Copy(plan.Projection.SceneAddress, authored.CopyScene());
        foreach (var model in plan.Projection.Models)
        {
            var old = authored.Bindings.Single(b => b.RowId == model.RowId);
            cpu.Copy(model.RootAddress, old.Edit.CopyPackage());
            foreach (var field in model.LoaderFields) cpu.Put(model.RootAddress + (uint)field.PackageFieldOffset, field.ProjectedLoadedWord);
            cpu.Put(0x80076378 + model.AuthoredClass * 4u, model.RootAddress);
            cpu.Put(model.RowAddress, model.PrivateAddress);
            // Exact placed-row initializer outcome. Native loader execution is
            // separately exercised in the source callback probe below.
            cpu.Put(model.RowAddress + 0x34, 0xFFFF, 2);
            cpu.Put(model.RowAddress + 0x51, 1, 1);
        }
        cpu.Put(0x80075890, plan.Lifecycle.DynamicBase); cpu.Put(0x80075778, 0);
        var row = plan.Binding.Rows[0];
        foreach (var (global, offset) in new (uint, uint)[] { (0x80078A58, 0xC), (0x80078A5C, 0x10), (0x80078A60, 0x14) })
            cpu.Put(global, near ? cpu.Get(row.RowAddress + offset) : 0);
        cpu.Put(0x800756CC, 1); cpu.Put(0x800756C4, 1);
        foreach (uint address in new uint[] { 0x80078C4C, 0x80078AD0, 0x80078AD4 }) cpu.Put(address, 0);
        cpu.Copy(plan.Collector.CallerLedgerAddress, F.Ledger(save, slot));
        if (!plan.SourceCertified) cpu.Copy(0x80052568, Retirement);
        cpu.Copy(FrameSp - 0x70, Enumerable.Repeat((byte)0xCC, 0x80).ToArray());
        for (int i = 1; i < 32; i++) cpu.Regs[i] = 0xBA000000u + (uint)i;
        cpu.Regs[19] = row.RowAddress; cpu.Regs[29] = DispatchSp; cpu.Regs[31] = 0x80010000;
        cpu.Hi = 0x12345678; cpu.Lo = 0x87654321;
        cpu.Reads.Clear(); cpu.Writes.Clear(); cpu.Calls.Clear(); cpu.Visited.Clear();
        return cpu;
    }

    internal static void Verify(NativeJapaneseEggLifecycleOverlayPlan plan, NativeJapaneseEggAuthoredClassesPlan authored,
        DetachedCollectibleManifest manifest, Action<bool, string> check)
    {
        var empty = DetachedCollectibleSave.CreateNew(manifest);
        uint entry = authored.Integration.VisualAncestor.EntryPc, ledger = plan.Collector.CallerLedgerAddress;
        var row = plan.Binding.Rows[0];
        void ReturnStub(RenderProbeCpu cpu, uint pc, uint result) => cpu.Copy(pc,
            F.Words(0x3C020000u | result >> 16, 0x34420000u | (result & 0xFFFF), 0x03E00008, 0));
        byte[] Begin(RenderProbeCpu cpu)
        {
            cpu.Run(entry, plan.PreBridgePc);
            check(cpu.Regs[29] == FrameSp, "Existing dispatcher allocates its unchanged 2B0 frame before the pre bridge.");
            return Snapshot(cpu, FrameSp + 0x230, 0x80);
        }
        void RestoreFrame(RenderProbeCpu cpu, byte[] saved)
        {
            check(Snapshot(cpu, FrameSp + 0x230, 0x80).SequenceEqual(saved), "Bridge and nested lifecycle calls preserve the complete dispatcher saved frame.");
            cpu.Run(Restore, Continue);
            int[] registers = Enumerable.Range(1, 31).Where(r => r != 29).ToArray();
            check(registers.Select((r, i) => cpu.Regs[r] == Word(saved, i * 4)).All(v => v) && cpu.Regs[29] == DispatchSp &&
                cpu.Hi == Word(saved, 0x78) && cpu.Lo == Word(saved, 0x7C), "Unchanged restore returns every GPR, HI/LO and SP to the original dispatch continuation.");
        }

        // Independent result stubs force every defined refusal and unknown
        // result through the actual emitted bridge decision. Available10 and
        // UnboundRow6 preserve visual dispatch; Suppressed11 and other refusals
        // take the restore edge. The inherited guard also admits canonical F5.
        foreach (uint result in Enumerable.Range(0, 12).Select(i => (uint)i).Concat(new uint[] { 12, 0x1000A, uint.MaxValue }))
        {
            var cpu = Load(plan, authored, empty, 1); ReturnStub(cpu, plan.Lifecycle.BeforeVisualEntryPc, result);
            byte[] saved = Begin(cpu); cpu.Writes.Clear(); cpu.Calls.Clear();
            bool visual = result is 10 or 6;
            cpu.Run(plan.PreBridgePc, visual ? Visual : Restore);
            check(cpu.Calls.SequenceEqual(new[] { plan.Lifecycle.BeforeVisualEntryPc }) && cpu.Regs[4] == row.RowAddress && cpu.Regs[5] == ledger &&
                cpu.Regs[29] == FrameSp && !cpu.Visited.Contains(Visual), "Pre bridge supplies the exact row/ledger and makes one delayed lifecycle call for result " + result);
            check(!cpu.Writes.Any(a => a >= row.RowAddress && a < row.RowAddress + 0x58), "A bridge decision alone never modifies native rows.");
            if (visual)
            {
                ReturnStub(cpu, plan.Lifecycle.AfterVisualEntryPc, 9); cpu.Calls.Clear();
                cpu.Run(PostEdge, Restore);
                check(cpu.Calls.SequenceEqual(new[] { plan.Lifecycle.AfterVisualEntryPc }) && cpu.Visited.Contains(plan.PostBridgePc),
                    "Postvisual owned edge calls the after lifecycle then enters the unchanged restore owner.");
            }
            RestoreFrame(cpu, saved);
        }
        foreach (uint result in Enumerable.Range(0, 12).Select(i => (uint)i).Append(uint.MaxValue))
        {
            var cpu = Load(plan, authored, empty, 1); ReturnStub(cpu, plan.Lifecycle.AfterVisualEntryPc, result);
            byte[] saved = Begin(cpu); cpu.Calls.Clear(); cpu.Run(PostEdge, Restore);
            check(cpu.Calls.SequenceEqual(new[] { plan.Lifecycle.AfterVisualEntryPc }), "Every post lifecycle result restores through the existing owner.");
            RestoreFrame(cpu, saved);
        }

        var available = Load(plan, authored, empty, 1);
        byte[] availableSaved = Begin(available); available.Calls.Clear(); available.Run(plan.PreBridgePc, Visual);
        check(available.Regs[2] == 10 && available.Calls.Contains(plan.Query.EntryPc) && !available.Calls.Contains(plan.Predicate.EntryPc) &&
            !available.Calls.Contains(plan.Collector.EntryPc), "Real pre lifecycle queries Available10 before any proximity or collection call.");
        available.Run(Visual, Restore);
        check(available.Calls.Contains(plan.Lifecycle.AfterVisualEntryPc) && available.Calls.Contains(plan.Attempt.EntryPc) &&
            available.Calls.Contains(plan.Predicate.EntryPc) && available.Calls.Contains(plan.Collector.EntryPc) && available.Calls.Contains(0x80052568),
            "Real visual executes, then post lifecycle attempts pickup and calls exact placed-row retirement.");
        check(available.Get(row.RowAddress + 0x48, 1) == 0xFD && available.Get(row.RowAddress + 0x49, 1) != 0 &&
            (available.Get(ledger + 0x90 + row.BitIndex / 8, 1) & (1u << ((int)row.BitIndex % 8))) != 0 && available.Get(ledger + 0x7A, 2) == 1,
            "Final-address row is initialized, collected once using its full-width bit index, then retired.");
        check(available.Get(FrameSp - 0x64) == 0xCCCCCCCC && available.Regs[29] == FrameSp,
            "Real nested lifecycle/predicate stack stays within the bounded 60-byte footprint below the adapter frame.");
        RestoreFrame(available, availableSaved);

        var earned = empty.WithCollected(1, row.InstanceId);
        var suppressed = Load(plan, authored, earned, 1, near: false);
        byte[] privateBefore = Snapshot(suppressed, row.PrivateAddress, 12), ledgerBefore = Snapshot(suppressed, ledger, 0x200);
        byte[] suppressedSaved = Begin(suppressed); suppressed.Calls.Clear(); suppressed.Run(plan.PreBridgePc, Restore);
        check(suppressed.Regs[2] == 11 && suppressed.Calls.Contains(plan.Query.EntryPc) && suppressed.Calls.Contains(0x80052568) &&
            !suppressed.Visited.Contains(Visual) && !suppressed.Calls.Contains(plan.Attempt.EntryPc) && !suppressed.Calls.Contains(plan.Predicate.EntryPc),
            "Real earned query yields Suppressed11 and skips visuals and proximity even when the player is far away.");
        check(suppressed.Get(row.RowAddress + 0x48, 1) == 0xFD && suppressed.Get(row.RowAddress + 0x49, 1) == 0 &&
            privateBefore.SequenceEqual(Snapshot(suppressed, row.PrivateAddress, 12)) && ledgerBefore.SequenceEqual(Snapshot(suppressed, ledger, 0x200)),
            "Suppression changes only native retirement state, with no private initialization or ledger mutation.");
        RestoreFrame(suppressed, suppressedSaved);

        // A formerly valid row address cannot pass the newly projected
        // lifecycle binding. Unbound rows retain existing visual dispatch.
        var stale = Load(plan, authored, empty, 1);
        stale.Regs[4] = authored.Bindings[0].ProjectedRowAddress; stale.Regs[5] = ledger; stale.Regs[31] = 0x80010000;
        stale.Run(plan.Lifecycle.BeforeVisualEntryPc, 0x80010000);
        check(stale.Regs[2] == (uint)NativeJapaneseEggLifecycleResult.UnboundRow && stale.Calls.Count == 0,
            "Old row addresses cannot accidentally enter a query through the newly projected lifecycle binding.");

        const uint UnboundRow = 0x801F0000, UnboundPrivate = UnboundRow + 0x58;
        var canonical = Load(plan, authored, empty, 1);
        canonical.Copy(UnboundRow, Snapshot(canonical, row.RowAddress, 0x58));
        canonical.Copy(UnboundPrivate, new byte[12]); canonical.Put(UnboundRow, UnboundPrivate); canonical.Put(UnboundRow + 0x36, 0xF5, 2);
        canonical.Regs[3] = 0xF5; canonical.Regs[19] = UnboundRow;
        byte[] canonicalLedger = Snapshot(canonical, ledger, 0x200);
        canonical.Run(authored.GuardPc, plan.PreBridgePc);
        byte[] canonicalSaved = Snapshot(canonical, FrameSp + 0x230, 0x80);
        canonical.Calls.Clear(); canonical.Run(plan.PreBridgePc, Restore);
        check(canonical.Visited.Contains(Visual) && canonical.Visited.Contains(plan.PostBridgePc) && canonical.Get(UnboundRow + 0x49, 1) != 0 &&
            canonical.Get(UnboundRow + 0x48, 1) == 0 && canonical.Regs[2] == (uint)NativeJapaneseEggLifecycleResult.UnboundRow,
            "Canonical unbound F5 retains its original visual behavior through inherited guard and both bridges.");
        check(!canonical.Calls.Contains(plan.Query.EntryPc) && !canonical.Calls.Contains(plan.Predicate.EntryPc) && !canonical.Calls.Contains(plan.Collector.EntryPc) &&
            !canonical.Calls.Contains(0x80052568) && canonicalLedger.SequenceEqual(Snapshot(canonical, ledger, 0x200)),
            "Unbound canonical F5 performs no collectible query, proximity, collection, retirement, or ledger mutation.");
        RestoreFrame(canonical, canonicalSaved);
        if (plan.SourceCertified) VerifySourceCallback(plan, authored, empty, check);
    }

    internal static void VerifyLedgerAncestry(NativeJapaneseEggLifecycleOverlayPlan plan, NativeJapaneseEggLifecycleOverlayPlan changedManifest,
        NativeJapaneseEggLifecycleOverlayPlan changedSlot, NativeJapaneseEggAuthoredClassesPlan authored,
        DetachedCollectibleManifest manifest, Action<bool, string> check)
    {
        var empty = DetachedCollectibleSave.CreateNew(manifest);
        foreach (var current in new[] { changedManifest, changedSlot })
        {
            var cpu = Load(current, authored, empty, 1); uint ledger = current.Collector.CallerLedgerAddress;
            byte[] before = Snapshot(cpu, ledger, 0x200);
            cpu.Run(authored.Integration.VisualAncestor.EntryPc, Restore);
            check(cpu.Regs[2] == (uint)NativeJapaneseEggLifecycleResult.InvalidLedger && !cpu.Visited.Contains(Visual) &&
                Snapshot(cpu, ledger, 0x200).SequenceEqual(before), "Changed manifest/slot compilation refuses the old ledger before visuals and leaves it unchanged.");
        }
    }

    private static void VerifySourceCallback(NativeJapaneseEggLifecycleOverlayPlan plan, NativeJapaneseEggAuthoredClassesPlan authored,
        DetachedCollectibleSave empty, Action<bool, string> check)
    {
        const uint Stop = 0x80010000, UpdateList = 0x800700F4, DrawList = 0x80071EF4;
        var cpu = Load(plan, authored, empty, 1); var row = plan.Binding.Rows[0]; var other = plan.Binding.Rows[1];
        foreach (var binding in plan.Binding.Rows)
        {
            cpu.Put(binding.RowAddress + 0x34, 0, 2); cpu.Regs[4] = binding.RowAddress; cpu.Run(0x800526A8, 0x80052758);
            check(cpu.Get(binding.RowAddress + 0x34, 2) == 0xFFFF, "Actual native animated initializer accepts the final projected authored row.");
            cpu.Put(binding.RowAddress + 0x51, 1, 1);
        }
        cpu.Put(0x80075828, row.RowAddress); cpu.Put(plan.Lifecycle.DynamicBase + 0x48, 0xFF, 1);
        cpu.Regs[29] = DispatchSp + 0x1C0; cpu.Regs[31] = Stop; cpu.Calls.Clear(); cpu.Visited.Clear();
        cpu.Run(0x8007D9C8, Stop);
        check(cpu.Calls.Contains(0x80051FEC) && cpu.Calls.Contains(0x800522C0) && cpu.Visited.Contains(plan.PreBridgePc) && cpu.Visited.Contains(plan.PostBridgePc) &&
            cpu.Get(row.RowAddress + 0x48, 1) == 0xFD && cpu.Get(other.RowAddress + 0x48, 1) == 0,
            "Actual source callback dispatches through both appended bridges and retires only the eligible final row.");
        cpu.Regs[31] = Stop; cpu.Run(0x800521C0, Stop);
        check(cpu.Get(DrawList) == other.RowAddress && cpu.Get(DrawList + 4) == 0, "Same-frame native render-list rebuild excludes the newly collected row.");
        cpu.Regs[29] = DispatchSp + 0x1C0; cpu.Regs[31] = Stop; cpu.Run(0x8007D9C8, Stop);
        check(cpu.Get(UpdateList) == other.RowAddress && cpu.Get(UpdateList + 4) == 0 && cpu.Get(row.RowAddress + 0x48, 1) == 0xFD,
            "Next native callback rebuild excludes the retired row before another visual dispatch.");
    }
}
