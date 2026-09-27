using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;

// Extends the already initialized exact-source CPU in NativeCases. No emitted
// collector/hook is changed, and these calls do not run in an exported game.
internal static class NativeRetirementCases
{
    internal static void Run(RenderProbeCpu cpu, NativeJapaneseEggAuthoredClassesPlan plan, Action<bool,string> check)
    {
        const uint delete = 0x80052568, stop = 0x80010000, list = 0x80071EF4;
        byte[] exe = plan.Integration.CopyOriginalExecutable();
        check(plan.SourceCertified && Convert.ToHexStringLower(SHA256.HashData(exe.AsSpan(0x42D68,0x64))) ==
            "602fc31b1351212e5d5c72c39ac59cc86a9afd0226a9a0549192855588498505",
            "Exact original placed-row deletion witness, reused from the native chest contract.");
        cpu.StrictSignedArithmetic = true;
        uint dynamicBase = cpu.Get(0x80075890);
        List<uint> ReadList()
        {
            List<uint> result=[];
            for(uint at=list;cpu.Get(at)!=0;at+=4)
            {
                result.Add(cpu.Get(at));
                if(result.Count>256)throw new Exception("Unterminated native animated list.");
            }
            return result;
        }
        List<uint> Rebuild()
        {
            cpu.Regs[31]=stop;cpu.Run(0x800521C0,stop);
            return ReadList();
        }
        var originalList=Rebuild();
        HashSet<uint> retired=[];
        foreach(var row in plan.Bindings)
        {
            uint address=row.ProjectedRowAddress;
            check(address<dynamicBase && cpu.Get(address+0x34,2)==0xFFFF && cpu.Get(address+0x48,1)==0,
                "Actual animated initialization supplies the non-grid placed-row preconditions.");
            byte[] before=Enumerable.Range(0,0x58).Select(i=>(byte)cpu.Get(address+(uint)i,1)).ToArray();
            var priorList=ReadList();
            cpu.Regs[4]=address;cpu.Regs[31]=stop;cpu.Reads.Clear();cpu.Writes.Clear();cpu.Calls.Clear();cpu.Visited.Clear();
            cpu.Run(delete,stop);
            check(cpu.Get(address+0x48,1)==0xFD && cpu.Writes.SequenceEqual(new[]{address+0x48}),
                "Native placed-row delete writes exactly FD, never the FF terminator.");
            check(cpu.Reads.SequenceEqual(new[]{address+0x34,0x80075778u,0x80075890u}),
                "Negative grid index avoids pointer chasing; deletion never reads the high class ID.");
            check(cpu.Calls.Count==0 && cpu.Visited.All(pc=>pc>=delete&&pc<0x800525CC),
                "No generic kill/effect/callback, dynamic free or private-pool traversal.");
            check(Enumerable.Range(0,0x58).Where(i=>i!=0x48).All(i=>cpu.Get(address+(uint)i,1)==before[i]),
                "Every other animated row field is retained.");
            check(ReadList().SequenceEqual(priorList),
                "Deletion alone does not rebuild a previously cached render list.");
            retired.Add(address);
            var rebuilt=Rebuild();
            check(rebuilt.SequenceEqual(originalList.Where(a=>!retired.Contains(a))),
                "Actual render-list rebuild removes only the retired authored egg; all others retain order.");
            // This leaf's placed/no-grid branch is idempotent; it does not grant
            // repeat collection or save persistence and never changes counters.
            cpu.Regs[4]=address;cpu.Regs[31]=stop;cpu.Writes.Clear();cpu.Run(delete,stop);
            check(cpu.Writes.SequenceEqual(new[]{address+0x48}) && cpu.Get(address+0x48,1)==0xFD,
                "Repeated native deletion remains one FD store with no ledger/counter writes.");
        }
        check(cpu.Get(dynamicBase+0x48,1)==0xFF,
            "The actual row-table terminator remains intact after deleting both high-class eggs.");
    }
}
