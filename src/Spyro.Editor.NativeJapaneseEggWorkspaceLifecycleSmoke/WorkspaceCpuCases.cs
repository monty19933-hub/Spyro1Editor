using System.Buffers.Binary;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

// One RAM image contains actual admitted ledger, complete model/workspace and
// relocated scene. Native card service alone is mocked; no emulator or I/O.
internal static class WorkspaceCpuCases
{
    internal static void Run(NativeCollectibleSaveWorkspacePlan workspace,NativeJapaneseEggLifecycleOverlayPlan plan,Action<bool,string> check)
    {
        const uint live=0x801FF600,menu=0x80160000,stop=0x80010000,overlay=0x8007AA38;
        var ancestry=workspace.Ancestry;var authored=ancestry.Authored;var card=ancestry.Admission;
        var guard=plan.LiveGuard!;var cpu=new RenderProbeCpu{StrictSignedArithmetic=true};
        byte[] Bytes(uint at,int n)=>Enumerable.Range(0,n).Select(i=>(byte)cpu.Get(at+(uint)i,1)).ToArray();
        cpu.Copy(0x8000F800,ancestry.CopyExecutable());cpu.Copy(overlay,card.CopyOverlay());
        cpu.Copy(NativeCollectibleCardLoadPlan.ReadPc,AttemptFixtures.Words(0x3C088007,0x24090003,0xAD095B50,0xAD005B58,0x24020001,0x03E00008,0));
        cpu.Copy(NativeCollectibleCardLoadPlan.ShutdownPc,AttemptFixtures.Words(0x03E00008,0));
        byte[] file=new byte[0x2000];new Random(963).NextBytes(file.AsSpan(0,0x1400));file[0x7E]=0xCD;file[0x7F]=0xAB;
        DetachedCollectibleSaveCodec.Serialize(DetachedCollectibleSave.CreateNew(NativeCollectibleCardLoadResearchComposition.FixedManifest())
            .WithCollected(0,plan.Binding.Rows[0].InstanceId)).CopyTo(file,0x1400);
        cpu.Copy(menu,file[..0x1400]);
        foreach(var(a,v)in new(uint,uint)[]{(0x800755A4,0xA00),(0x80078D14,1),(0x80078D18,0),(0x80078D1C,0),(0x80078D20,0xABCD),
            (0x80078D78,2),(0x80078D7C,5),(0x80078D8C,0),(0x80078D94,1),(0x80078DA0,0),(0x80078DC4,menu),(0x800785E8,menu+0x2000)})cpu.Put(a,v);
        cpu.Regs[29]=0x801FFCA0;cpu.Regs[31]=stop;cpu.Run(card.Kernel.BeginPc,stop);
        check(cpu.Regs[2]==1,"Full-workspace CPU begins actual emitted admission, never a manufactured Ready image");
        cpu.Copy(card.Reader.ReadBufferAddress,file);cpu.Put(0x80075B50,0);cpu.Put(0x80075B58,1);cpu.Put(0x80075B94,3);cpu.Put(0x80075B98,0);
        cpu.Run(card.Kernel.GatePc,0x8007CC08);
        check(cpu.Get(live+0x16,1)==1&&cpu.Get(live+0x90,1)==1,"Actual native poll plus emitted validation/admission publishes one earned bit");
        byte[] data=workspace.CopyLevelData();uint W(int at)=>BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(at));
        cpu.Copy(overlay,plan.CopyOverlay());
        cpu.Copy(plan.Projection.ModelAddress,data.AsSpan((int)W(0x10),(int)W(0x14)).ToArray());
        foreach(var model in plan.Projection.Models)
        {
            foreach(var field in model.LoaderFields)cpu.Put(model.RootAddress+(uint)field.PackageFieldOffset,field.ProjectedLoadedWord);
            cpu.Put(0x80076378+model.AuthoredClass*4u,model.RootAddress);
        }
        uint first=plan.Binding.Rows[0].RowAddress,second=plan.Binding.Rows[1].RowAddress;
        uint rows=guard.ExpectedSceneGlobals.Single(g=>g.Address==0x80075828).Value;
        void Scene()
        {
            cpu.Copy(plan.Projection.SceneAddress,data.AsSpan((int)W(0x18),(int)W(0x1C)).ToArray());
            foreach(var(a,v)in guard.ExpectedSceneGlobals)cpu.Put(a,v);
            int fix=authored.Integration.OriginalSceneSource.FixupOffset+24;
            cpu.Regs[16]=plan.Projection.SceneAddress+(uint)fix;cpu.Regs[17]=0;cpu.Regs[18]=plan.Projection.SceneAddress;
            cpu.Run(0x80014040,0x80014084);
            foreach(var row in plan.Binding.Rows)
            {
                cpu.Regs[16]=0;cpu.Regs[4]=row.RowAddress;cpu.Run(0x8001417C,0x80052758);
                check(cpu.Get(row.RowAddress)==row.PrivateAddress&&cpu.Get(row.RowAddress+0x36,2)==row.AuthoredClass&&
                    cpu.Get(row.RowAddress+0x34,2)==0xFFFF&&cpu.Get(row.RowAddress+0x48,1)==0,"Actual source initializer retains exact final private/class/live row");
                cpu.Put(row.RowAddress+0x51,1,1);
            }
            // Restrict this native callback fixture to its two authored rows.
            for(uint at=rows;at<first;at+=0x58)cpu.Put(at+0x48,0xFD,1);
            cpu.Put(plan.Lifecycle.DynamicBase+0x48,0xFF,1);
            cpu.Put(0x80078D78,3);cpu.Put(0x80078D7C,0);cpu.Put(0x800756CC,1);cpu.Put(0x800756C4,1);cpu.Put(0x80075778,0);
            foreach(uint at in new uint[]{0x80078C4C,0x80078AD0,0x80078AD4,0x80078A58,0x80078A5C,0x80078A60})cpu.Put(at,0);
        }
        void Tick()
        {cpu.Regs[29]=0x801FFFB8;cpu.Regs[31]=stop;cpu.Run(0x8007D9C8,stop);}
        Scene();byte[] tail=Bytes(workspace.Geometry.TailAddress,workspace.GrowthBytes),code=Bytes(overlay,0x10000);
        byte[] models=Bytes(plan.Projection.ModelAddress,plan.Binding.ModelByteLength);
        byte[] other=Bytes(rows,(int)(first-rows)),ledger=Bytes(live,0x200);
        foreach(var old in workspace.RequiredGlobalRebindings)
        {
            cpu.Put(old.Address,old.OldValue);cpu.Regs[19]=second;cpu.Regs[29]=0x801FFB48;cpu.Writes.Clear();cpu.Calls.Clear();
            cpu.Run(guard.BeforeEntryPc,0x80088B14);
            check(cpu.Writes.Count==0&&cpu.Calls.Count==0,"New bound row with exact stale parent global refuses without writes/calls");cpu.Put(old.Address,old.NewValue);
        }
        Tick();check(cpu.Get(first+0x48,1)==0xFD&&cpu.Get(second+0x48,1)==0&&Bytes(live,0x200).SequenceEqual(ledger),"Native callback suppresses seeded A and leaves distant B active");
        foreach(var(a,o)in new(uint,uint)[]{(0x80078A58,12),(0x80078A5C,16),(0x80078A60,20)})cpu.Put(a,cpu.Get(second+o));
        Tick();ledger[0x90]=3;ledger[0x7A]=2;ledger[0x19]=1;ledger[0x7C]=1;
        check(cpu.Get(second+0x48,1)==0xFD&&Bytes(live,0x200).SequenceEqual(ledger),"Same-image native callback pickup changes only bit/count/dirty/generation");
        Scene();Tick();check(cpu.Get(first+0x48,1)==0xFD&&cpu.Get(second+0x48,1)==0xFD&&Bytes(live,0x200).SequenceEqual(ledger),"Reloaded relocated scene suppresses both without re-earning");
        check(Bytes(workspace.Geometry.TailAddress,workspace.GrowthBytes).SequenceEqual(tail)&&Bytes(overlay,0x10000).SequenceEqual(code)&&
            Bytes(plan.Projection.ModelAddress,models.Length).SequenceEqual(models)&&Bytes(rows,other.Length).SequenceEqual(other),
            "All registered models/controller code/scratch/filename/padding, entire overlay and other native rows preserved through callbacks/reload");
        check(!cpu.Calls.Any(pc=>pc is NativeCollectibleSaveControllerKernel.ReadPc or NativeCollectibleSaveControllerKernel.WritePc),"No card request after admission in the detached lifecycle slice");
    }
}
