using System.Buffers.Binary;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;

// The final composed executable is executed, not a freshly recompiled bootstrap
// or a hand-written copy-anchor surrogate. BIOS/CD, heap allocation and native
// selected-save admission remain outside this bounded instruction test.
internal static class StartupBootChecks
{
    internal static void Verify(NativeJapaneseEggLifecycleStartupPlan plan, Action<bool, string> check)
    {
        var boot = plan.BootstrapAncestor;
        var overlay = plan.OverlayAncestor;
        byte[] exe = plan.CopyExecutable();
        uint W(int at) => BinaryPrimitives.ReadUInt32LittleEndian(exe.AsSpan(at));
        byte[] Bytes(RenderProbeCpu cpu, uint at, int count) => Enumerable.Range(0, count).Select(i => (byte)cpu.Get(at + (uint)i, 1)).ToArray();
        foreach (uint salt in new[] { 0u, 0xFFFFFFFFu })
        {
            var cpu = new RenderProbeCpu { StrictSignedArithmetic = true };
            cpu.Copy(boot.EntryPc, Enumerable.Repeat((byte)0xA7, 0x53F8).ToArray());
            cpu.Copy(W(0x18), exe.AsSpan(0x800, checked((int)W(0x1C))).ToArray());
            cpu.Copy(boot.LedgerStart - 4, Enumerable.Repeat((byte)0xB9, 0x208).ToArray());
            for (int i = 1; i < 32; i++) cpu.Regs[i] = salt ^ (0xA1230000u + (uint)i * 0x10203);
            cpu.Hi = salt ^ 0x12345678; cpu.Lo = salt ^ 0x9ABCDEF0;
            uint[] registers = cpu.Regs.ToArray(); uint hi = cpu.Hi, lo = cpu.Lo;
            cpu.Run(W(0x10), boot.OriginalEntryPc);
            check(cpu.Writes.Count == 0x200 && cpu.Writes.Distinct().Count() == 0x200 &&
                cpu.Writes.Min() == boot.LedgerStart && cpu.Writes.Max() == boot.LedgerEnd - 1 &&
                Bytes(cpu, boot.LedgerStart, 0x200).All(b => b == 0) &&
                cpu.Get(boot.LedgerStart - 4) == 0xB9B9B9B9 && cpu.Get(boot.LedgerEnd) == 0xB9B9B9B9,
                "Combined EXE entry clears exactly the entire reservation while preserving both adjacent canaries.");
            check(cpu.Reads.Count == 0 && cpu.Calls.Count == 0 && cpu.Hi == hi && cpu.Lo == lo &&
                Enumerable.Range(0, 32).Where(i => i is not (1 or 2 or 3)).All(i => cpu.Regs[i] == registers[i]),
                "Combined startup retains all incoming state except its documented at/v0/v1 temporaries.");
            cpu.Writes.Clear();
            cpu.Run(boot.OriginalEntryPc, 0x8005B904);
            check(cpu.Writes.Count == 0x53F8 && cpu.Writes.Distinct().Count() == 0x53F8 &&
                cpu.Writes.Min() == boot.EntryPc && cpu.Writes.Max() == 0x8007AA37 &&
                Bytes(cpu, boot.EntryPc, 0x53F8).All(b => b == 0),
                "The combined executable's original BSS loop erases the ephemeral code and only its exact original extent.");
            cpu.Run(0x8005B904, 0x8005DB14);
            check(cpu.Regs[29] == 0x801FFFF8 && cpu.Regs[28] == 0x80075264 && cpu.Regs[4] == 0x8007AA3C &&
                cpu.Regs[5] == boot.Reservation.ProposedGeometry.HeapByteLength &&
                cpu.Get(0x800730C4) == boot.Reservation.ProposedGeometry.HeapByteLength &&
                cpu.Get(boot.EntryPc) == registers[31] && Bytes(cpu, boot.LedgerStart, 0x200).All(b => b == 0),
                "Real startup reaches the heap-service call with A00 geometry, unchanged SP/GP/RA-save and NOT-LOADED state.");

            // Explicit phase seam: supply the exact final overlay in test RAM.
            // This is not CD loading, a loader lifetime proof or game startup.
            cpu.Copy(0x8007AA38, plan.CopyOverlay());
            cpu.Writes.Clear(); cpu.Reads.Clear(); cpu.Calls.Clear();
            cpu.Run(0x8005A718, 0x8005A728);
            check(cpu.Get(0x800785D8) == overlay.Projection.CopyAnchor &&
                cpu.Writes.SequenceEqual(new uint[] { 0x800785D8, 0x800785D9, 0x800785DA, 0x800785DB }) &&
                cpu.Reads.Count == 0 && cpu.Calls.Count == 0,
                "Final original initializer executes the actual signed-low copy pair and publishes precisely the new anchor global.");
            foreach (uint entry in new[] { overlay.Query.EntryPc, overlay.Collector.EntryPc })
            {
                cpu.Regs[4] = boot.LedgerStart; cpu.Regs[5] = overlay.Binding.Rows[0].BitIndex; cpu.Regs[31] = 0x80010000;
                cpu.Writes.Clear(); cpu.Calls.Clear();
                cpu.Run(entry, 0x80010000);
                check(cpu.Regs[2] == (uint)NativeCollectibleQueryResult.InvalidLedger && cpu.Writes.Count == 0 && cpu.Calls.Count == 0 &&
                    Bytes(cpu, boot.LedgerStart, 0x200).All(b => b == 0),
                    "Actually linked query and collector reject cold NOT-LOADED state without inventing progress or modifying RAM.");
            }
        }
        check(plan.CopyExecutable().SequenceEqual(exe), "Bounded instruction execution never mutates the retained complete afterimage.");
    }
}
