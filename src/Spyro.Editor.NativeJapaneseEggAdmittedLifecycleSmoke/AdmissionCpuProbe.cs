using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;
using F=AttemptFixtures;
internal static class AdmissionCpuProbe
{
    private const uint Base=0x8007AA38,Live=0x801FF600,Stop=0x80010000,Menu=0x80160000,Record=0x80100000;
    private const uint DispatchSp=0x801FFDF8,FrameSp=DispatchSp-0x2B0,Restore=0x80088B14,Continue=0x80085780;
    private static byte[] Bytes(RenderProbeCpu c,uint at,int n)=>Enumerable.Range(0,n).Select(i=>(byte)c.Get(at+(uint)i,1)).ToArray();
    private static readonly byte[] Poll=Convert.FromHexString("0780023c505b42240000428c0000000006004014000000000780023c585b428c0000000030004010ffff02240780083c505b08250000078d0780033c545b638c0c008014000000000780023c585b428c0000000013004014080003250000628c00000000fdff401000000000b39d0108000000000780023c585b428c0000000008004014000000000200c010000000000000c3ac1400a01021100000c49d01080000a7ac0600c010000000000780023c985b42240000428c000000000000c2ac0600a010000000000780023c945b42240000428c000000000000a2ac0780013c585b20ac010002240800e00300000000");
    private static readonly byte[] Retirement=F.Words(0x84850034,0x3C018007,0x24215778,0x04A0000C,0x8C210000,0x00052880,0x00A12820,
        0x20A2FFFC,0x20450004,0x8CA20000,0,0x1444FFFC,0,0x8C820004,0,0xACA20000,0x3C018007,
        0x24215890,0x8C210000,0,0x00240822,0x18200003,0x2002FFFD,0x03E00008,0xA0820048);
    internal static void Verify(NativeCollectibleCardAdmissionPlan card,NativeJapaneseEggAuthoredClassesPlan authored,
        NativeJapaneseEggLifecycleOverlayPlan lifecycle,byte[]? executable,Action<bool,string> check)
    {
        var guard=lifecycle.LiveGuard??throw new Exception("Guard required");var first=lifecycle.Binding.Rows[0];var second=lifecycle.Binding.Rows[1];
        RenderProbeCpu Admitted()
        {
            var c=new RenderProbeCpu{StrictSignedArithmetic=true};c.Copy(0x8000F800,executable??card.CopyExecutable());c.Copy(Base,card.CopyOverlay());
            c.Copy(NativeCollectibleCardLoadPlan.PollPc,Poll);
            c.Copy(NativeCollectibleCardLoadPlan.ReadPc,F.Words(0x3C088007,0x24090003,0xAD095B50,0xAD005B58,0x24020001,0x03E00008,0));
            c.Copy(NativeCollectibleCardLoadPlan.ShutdownPc,F.Words(0x03E00008,0));
            byte[] prefix=new byte[0x1400];new Random(928).NextBytes(prefix);prefix[0x7E]=0xCD;prefix[0x7F]=0xAB;
            c.Copy(Menu,prefix);c.Copy(Live,Enumerable.Repeat((byte)0xEC,512).ToArray());c.Put(Live+0x16,0,1);
            foreach(var (a,v) in new(uint,uint)[]{(0x800755A4,0xA00),(0x80078D14,1),(0x80078D18,0),(0x80078D1C,0),(0x80078D20,0xABCD),
                (0x80078D78,2),(0x80078D7C,5),(0x80078D8C,0),(0x80078D94,1),(0x80078DA0,0),(0x80078DC4,Menu),(0x800785E8,Menu+0x2000)})c.Put(a,v);
            c.Regs[29]=0x801FFCA0;c.Regs[31]=Stop;c.Run(card.Kernel.BeginPc,Stop);
            check(c.Regs[2]==1&&c.Get(card.Kernel.StateAddress)==1,"Actual Begin accepted and armed before gameplay.");
            byte[] block=new byte[0x2000];prefix.CopyTo(block,0);
            var manifest=NativeCollectibleCardLoadResearchComposition.FixedManifest();
            byte[] envelope=DetachedCollectibleSaveCodec.Serialize(DetachedCollectibleSave.CreateNew(manifest).WithCollected(0,first.InstanceId));
            envelope.CopyTo(block,0x1400);c.Copy(card.Reader.ReadBufferAddress,block);
            c.Put(0x80075B50,0);c.Put(0x80075B58,1);c.Put(0x80075B94,3);c.Put(0x80075B98,0);
            c.Run(card.Kernel.GatePc,0x8007CC08);
            check(c.Get(Live+0x16,1)==1&&c.Get(Live+0x90,1)==1&&c.Get(Live+0x7A,2)==1&&c.Get(Live+0x19,1)==0,
                "Actual native poll/SHA/envelope/admission chain publishes seeded one-earned Ready; never manually seeded Ready.");
            return c;
        }
        void Scene(RenderProbeCpu c)
        {
            // CD/model phase boundary is supplied, not represented as real I/O.
            if(executable is null){c.Copy(0x8000F800,lifecycle.CopyExecutable());c.Put(0x800755A4,0xA00);}
            c.Copy(Base,lifecycle.CopyOverlay());c.Copy(lifecycle.Projection.SceneAddress,authored.CopyScene());
            foreach(var model in lifecycle.Projection.Models)
            {
                var old=authored.Bindings.Single(b=>b.RowId==model.RowId);c.Copy(model.RootAddress,old.Edit.CopyPackage());
                foreach(var field in model.LoaderFields)c.Put(model.RootAddress+(uint)field.PackageFieldOffset,field.ProjectedLoadedWord);
                c.Put(0x80076378+model.AuthoredClass*4u,model.RootAddress);c.Put(model.RowAddress,model.PrivateAddress);
                c.Put(model.RowAddress+0x34,0xFFFF,2);c.Put(model.RowAddress+0x51,1,1);
            }
            foreach(var (a,v) in guard.ExpectedSceneGlobals)c.Put(a,v);
            c.Put(0x80078D78,3);c.Put(0x80078D7C,0);c.Put(0x800756CC,1);c.Put(0x800756C4,1);c.Put(0x80075778,0);
            foreach(uint a in new uint[]{0x80078C4C,0x80078AD0,0x80078AD4,0x80078A58,0x80078A5C,0x80078A60})c.Put(a,0);
            if(!lifecycle.SourceCertified)c.Copy(0x80052568,Retirement);
            c.Regs[29]=DispatchSp;c.Regs[31]=Stop;c.Hi=0x12345678;c.Lo=0x87654321;
            c.Reads.Clear();c.Writes.Clear();c.Calls.Clear();c.Visited.Clear();
        }
        void Near(RenderProbeCpu c,NativeJapaneseEggCollectibleBinding.Row row)
        {foreach(var(a,o) in new(uint,uint)[]{(0x80078A58,12),(0x80078A5C,16),(0x80078A60,20)})c.Put(a,c.Get(row.RowAddress+o));}
        void Tick(RenderProbeCpu c,NativeJapaneseEggCollectibleBinding.Row row)
        {
            c.Regs[19]=row.RowAddress;c.Regs[29]=DispatchSp;c.Regs[31]=Stop;var regs=c.Regs.ToArray();uint hi=c.Hi,lo=c.Lo;
            c.Run(authored.Integration.VisualAncestor.EntryPc,Continue);
            check(c.Regs.SequenceEqual(regs)&&c.Hi==hi&&c.Lo==lo,"Full adapter restores every GPR/HI/LO after guarded lifecycle.");
        }
        var cpu=Admitted();Scene(cpu);byte[] ledger=Bytes(cpu,Live,512);Tick(cpu,first);
        check(cpu.Get(first.RowAddress+0x48,1)==0xFD&&Bytes(cpu,Live,512).SequenceEqual(ledger)&&!cpu.Calls.Contains(lifecycle.Predicate.EntryPc),
            "Seeded first identity suppresses outside proximity with no progress mutation.");
        Near(cpu,second);Tick(cpu,second);
        check(cpu.Get(second.RowAddress+0x48,1)==0xFD&&cpu.Get(Live+0x90,1)==3&&cpu.Get(Live+0x7A,2)==2&&
            cpu.Get(Live+0x19,1)==1&&cpu.Get(Live+0x7C)==1,$"Second pickup changes01->03/count1->2/dirty1/generation1 then retires its own row. row={cpu.Get(second.RowAddress+0x48,1):X} bits={cpu.Get(Live+0x90,1):X} count={cpu.Get(Live+0x7A,2)} dirty={cpu.Get(Live+0x19,1)} gen={cpu.Get(Live+0x7C)} calls={string.Join(',',cpu.Calls.Select(x=>x.ToString("X8")))}");
        ledger[0x90]=3;ledger[0x7A]=2;ledger[0x19]=1;ledger[0x7C]=1;
        check(Bytes(cpu,Live,512).SequenceEqual(ledger),"Every guard/metadata/padding/other identity byte outside exact collection write set preserved.");
        byte[] earned=Bytes(cpu,Live,512);Scene(cpu);Tick(cpu,first);Tick(cpu,second);
        check(cpu.Get(first.RowAddress+0x48,1)==0xFD&&cpu.Get(second.RowAddress+0x48,1)==0xFD&&Bytes(cpu,Live,512).SequenceEqual(earned),
            "Restored scene suppresses both earned instances far from player; ledger survives reload boundary.");
        foreach(var (a,v) in guard.ExpectedSceneGlobals.Select(x=>(x.Address,x.Value^1u)).Concat(new(uint,uint)[]{
            (0x800755A4,0x800),(0x80078D14,0),(0x80078D18,16),(0x80078D1C,1),(0x80078D20,0xABCE)}))
        {
            cpu=Admitted();Scene(cpu);cpu.Put(a,v);ledger=Bytes(cpu,Live,512);byte[] row=Bytes(cpu,second.RowAddress,0x58);Near(cpu,second);Tick(cpu,second);
            check(Bytes(cpu,Live,512).SequenceEqual(ledger)&&Bytes(cpu,second.RowAddress,0x58).SequenceEqual(row)&&
                !cpu.Calls.Contains(lifecycle.Lifecycle.BeforeVisualEntryPc),"Current source scene/selection/reserve refusal enters no lifecycle and mutates neither row nor ledger.");
        }
        foreach(uint sp in new uint[]{0,Live,Live+0x200,0x801FF858,0x801FF85F,0x801FFB58,0x80200000})
        {
            cpu=Admitted();Scene(cpu);cpu.Regs[19]=second.RowAddress;cpu.Regs[29]=sp;cpu.Writes.Clear();cpu.Calls.Clear();
            cpu.Run(guard.BeforeEntryPc,Restore);check(cpu.Writes.Count==0&&cpu.Calls.Count==0,"Unsafe full nested/caller-stack placement refuses without new frame or native calls.");
        }
        foreach(uint sp in new uint[]{0x801FF860,FrameSp,0x801FFB50})
        {
            cpu=Admitted();Scene(cpu);cpu.Regs[19]=second.RowAddress;cpu.Regs[29]=sp;cpu.Writes.Clear();cpu.Calls.Clear();
            cpu.Run(guard.BeforeEntryPc,lifecycle.PreBridgePc);
            check(cpu.Writes.Count==0&&cpu.Calls.Count==0&&cpu.Regs[29]==sp,"Exact lower/upper stack boundaries and ordinary adapterSP pass leaf guard without hidden pushes.");
        }
        foreach(var(a,v)in new(uint,uint)[]{(Live+0x8C,0),(0x80078D1C,1),(0x800785E4,0),(0x8007596C,11)})
        {
            cpu=Admitted();Scene(cpu);cpu.Regs[19]=second.RowAddress;cpu.Regs[29]=FrameSp;cpu.Put(a,v);
            ledger=Bytes(cpu,Live,512);byte[] row=Bytes(cpu,second.RowAddress,0x58);cpu.Writes.Clear();cpu.Calls.Clear();
            cpu.Run(guard.AfterEntryPc,Restore);
            check(cpu.Writes.Count==0&&cpu.Calls.Count==0&&Bytes(cpu,Live,512).SequenceEqual(ledger)&&
                Bytes(cpu,second.RowAddress,0x58).SequenceEqual(row),"Postvisual guard independently rejects changed epoch/selection/scene before any lifecycle call or added write.");
        }
        foreach(var(a,v,width)in new(uint,uint,int)[]{(Live+0x8C,0,4),(Live+0x16,0,1),(Live+0x17,1,1),
            (Live+0x18,1,1),(Live+0x18,2,1),(Live+0x1A,0xABCE,2)})
        {
            cpu=Admitted();Scene(cpu);cpu.Put(a,v,width);ledger=Bytes(cpu,Live,512);byte[] row=Bytes(cpu,second.RowAddress,0x58);Near(cpu,second);Tick(cpu,second);
            check(Bytes(cpu,Live,512).SequenceEqual(ledger)&&Bytes(cpu,second.RowAddress,0x58).SequenceEqual(row)&&
                !cpu.Calls.Contains(lifecycle.Lifecycle.BeforeVisualEntryPc),"Zero epoch/phase and wrong ledger-selected tuple refuse before lifecycle calls or progress writes.");
        }
        // Canonical F5 is outside the fixed collectible binding. It bypasses
        // all custom ownership/Ready checks and retains the native visual path.
        cpu=Admitted();Scene(cpu);const uint canonical=0x801F0000;
        cpu.Copy(canonical,Bytes(cpu,second.RowAddress,0x58));cpu.Put(canonical,canonical+0x58);cpu.Copy(canonical+0x58,new byte[12]);
        cpu.Put(canonical+0x36,0xF5,2);cpu.Put(0x800755A4,0x800);cpu.Put(Live+0x16,0,1);ledger=Bytes(cpu,Live,512);
        cpu.Regs[3]=0xF5;cpu.Regs[19]=canonical;cpu.Regs[29]=DispatchSp;cpu.Regs[31]=Stop;cpu.Reads.Clear();cpu.Writes.Clear();cpu.Calls.Clear();
        cpu.Run(authored.GuardPc,Continue);
        check(cpu.Get(canonical+0x49,1)!=0&&cpu.Get(canonical+0x48,1)==0&&Bytes(cpu,Live,512).SequenceEqual(ledger)&&
            !cpu.Reads.Any(a=>a>=Live&&a<Live+0x200)&&!cpu.Calls.Contains(lifecycle.Query.EntryPc)&&!cpu.Calls.Contains(lifecycle.Collector.EntryPc),
            "Canonical unboundF5 preserves native visuals with wrong reserve/NOT-LOADED and never reads collectible RAM.");
        cpu=Admitted();Scene(cpu);cpu.Put(Live+0x16,0,1);Near(cpu,second);Tick(cpu,second);
        check(cpu.Get(second.RowAddress+0x48,1)==0&&cpu.Get(Live+0x90,1)==1,"NOT-LOADED/new-selection phase refuses collection without fabricated empty progress.");
        cpu=Admitted();Scene(cpu);cpu.Copy(Base,card.CopyOverlay());cpu.Put(0x80078D78,0);cpu.Put(0x80078D7C,0);
        cpu.Run(card.Kernel.EntryPc,0x8007ABB4);Scene(cpu);Near(cpu,second);Tick(cpu,second);
        check(cpu.Get(Live+0x16,1)==0&&cpu.Get(Live+0x90,1)==1&&cpu.Get(second.RowAddress+0x48,1)==0,
            "Actual card re-entry invalidation prevents stale pickup after a following scene boundary.");
        if(lifecycle.SourceCertified)
        {
            cpu=Admitted();Scene(cpu);cpu.Regs[29]=0x801FFCA0;cpu.Regs[31]=Stop;cpu.Run(0x80012604,Stop);
            Scene(cpu);Near(cpu,second);Tick(cpu,second);check(cpu.Get(Live+0x16,1)==0&&cpu.Get(second.RowAddress+0x48,1)==0,
                "Actual composed resident reset clearsReady before subsequent callback; earned bytes stay uncollected.");
            cpu=Admitted();Scene(cpu);
            uint rows=guard.ExpectedSceneGlobals.Single(g=>g.Address==0x80075828).Value;
            for(uint row=rows;row<first.RowAddress;row+=0x58)cpu.Put(row+0x48,0xFD,1);
            foreach(var row in lifecycle.Binding.Rows){cpu.Put(row.RowAddress+0x34,0,2);cpu.Regs[4]=row.RowAddress;cpu.Run(0x800526A8,0x80052758);cpu.Put(row.RowAddress+0x51,1,1);}
            cpu.Put(lifecycle.Lifecycle.DynamicBase+0x48,0xFF,1);Near(cpu,second);byte[] originalRows=Bytes(cpu,rows,checked((int)(first.RowAddress-rows)));
            cpu.Regs[29]=DispatchSp+0x1C0;cpu.Regs[31]=Stop;cpu.Run(0x8007D9C8,Stop);
            check(cpu.Get(Live+0x90,1)==3&&cpu.Get(first.RowAddress+0x48,1)==0xFD&&cpu.Get(second.RowAddress+0x48,1)==0xFD,
                "Actual original Artisans callback consumes real admitted state through both guarded hooks.");
            check(Bytes(cpu,rows,originalRows.Length).SequenceEqual(originalRows),"Complete other native rows unchanged by bounded callback fixture.");
            cpu.Regs[31]=Stop;cpu.Run(0x800521C0,Stop);check(cpu.Get(0x80071EF4)==0,"Same-frame native draw-list rebuild excludes both retired rows.");
        }
    }
}
