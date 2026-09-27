using System.Buffers.Binary;
using Spyro.Editor.Core.Exporting;

internal static class NativeCases
{
    internal static void Run(NativeJapaneseEggAuthoredClassesPlan p,Action<bool,string> check)
    {
        const uint table=0x80076378,stop=0x80010000,pete=0x801C5BB8;
        var cpu=new RenderProbeCpu();cpu.Copy(0x8000F800,p.Integration.CopyOriginalExecutable());
        byte[] body=p.CopyLevelData(),scene=p.CopyScene();uint model=p.Projection.ProjectedModelRamStart,sceneBase=p.Projection.ProjectedSceneRamStart;
        uint W(byte[] b,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(at));
        // Execute both original 512-slot cleanup owners. Half the table is
        // deliberately local and half resident; absent entries remain absent.
        foreach(uint start in new uint[]{0x80015A80,0x80015284})
        {
            for(int i=0;i<512;i++)cpu.Put(table+(uint)i*4,i%3==0?0:i%3==1?0x80100000u+(uint)i*4:pete+(uint)i*4);
            // The alternate window enters with s0=785E0 and s1=0;
            // its own ADDIU advances s0 by1C to the PETE-base global.
            cpu.Put(0x800785FC,pete);cpu.Regs[16]=0x800785E0;cpu.Regs[17]=0;cpu.Writes.Clear();
            cpu.Run(start,start==0x80015A80?0x80015ACC:0x800152C8);
            check(Enumerable.Range(0,512).All(i=>cpu.Get(table+(uint)i*4)==(i%3==2?pete+(uint)i*4:0)),"Actual512-slot clear removes local roots and retains resident PETE roots at "+start.ToString("X"));
            check(cpu.Writes.All(a=>a>=table&&a<table+512*4),"Native cleanup writes only declared model-table slots.");
        }
        for(int generation=0;generation<2;generation++)
        {
            // Refill from immutable source-form bytes on each load. The native
            // registration loop alone both relocates and publishes the roots.
            cpu.Copy(model,body.AsSpan(p.Projection.OriginalModelOffset,(int)W(body,0x14)).ToArray());
            cpu.Copy(0x80076C00,body.AsSpan(0,0x800).ToArray());cpu.Put(0x800785E0,model);
            int slot=p.Bindings[0].RegistrationSlot;cpu.Regs[16]=(uint)slot;cpu.Regs[17]=0x80076C50+(uint)slot*4;
            cpu.Regs[18]=0x80076D50+(uint)slot*2;cpu.Regs[19]=0x80076C50;cpu.Regs[20]=table;
            cpu.Writes.Clear();cpu.Run(0x80015AE8,0x80015B34);
            foreach(var b in p.Bindings)
            {
                check(cpu.Get(table+b.AuthoredDestinationClass*4u)==b.ProjectedRoot,"Native registration publishes distinct authored root in load generation "+generation);
                foreach(var f in b.LoaderFields)check(cpu.Get(b.ProjectedRoot+(uint)f.PackageFieldOffset)==f.ProjectedLoadedWord,"Native loader matches exact per-root relocation.");
                byte[] package=b.Edit.CopyPackage();var pointerBytes=b.LoaderFields.SelectMany(f=>Enumerable.Range(f.PackageFieldOffset,4)).ToHashSet();
                check(Enumerable.Range(0,package.Length).Where(i=>!pointerBytes.Contains(i)).All(i=>cpu.Get(b.ProjectedRoot+(uint)i,1)==package[i]),"Complete RGB/geometry/CODE/frame complement survives native relocation.");
            }
            check(cpu.Writes.All(a=>p.Bindings.Any(b=>a>=b.ProjectedRoot&&a<b.ProjectedRoot+b.ByteLength||a>=table+b.AuthoredDestinationClass*4u&&a<table+b.AuthoredDestinationClass*4u+4)),"Registration only writes the two package and table owners.");
            if(generation==0)
            {
                cpu.Put(0x800785FC,pete);cpu.Run(0x80015A80,0x80015ACC);
                check(p.Bindings.All(b=>cpu.Get(table+b.AuthoredDestinationClass*4u)==0),"Next native load clears both authored roots before relocation/re-registration.");
            }
        }
        cpu.Copy(sceneBase,scene);int moby=p.Integration.OriginalSceneSource.RowTableOffset-8;
        cpu.Regs[16]=sceneBase+(uint)moby;cpu.Regs[5]=cpu.Regs[16];cpu.Run(0x80013F24,0x80013FC4);
        check(cpu.Get(0x80075890)==p.Bindings[^1].ProjectedRowAddress+0x58,"Native row sentinel includes both authored rows.");
        int fix=p.Integration.OriginalSceneSource.FixupOffset+24;
        cpu.Regs[16]=sceneBase+(uint)fix;cpu.Regs[17]=0;cpu.Regs[18]=sceneBase;cpu.Run(0x80014040,0x80014084);
        foreach(var b in p.Bindings)
        {
            check(cpu.Get(b.ProjectedRowAddress)==b.ProjectedPrivateAddress,"Native row+00 fixup retains independent private allocation.");
            cpu.Regs[16]=0;cpu.Regs[4]=b.ProjectedRowAddress;cpu.Calls.Clear();cpu.Run(0x8001417C,0x80052758);
            check(cpu.Calls.Contains(0x800526A8)&&!cpu.Calls.Contains(0x800529CC),"Authored ID follows the unmodified native animated initializer.");
        }
        const uint scratch=0x8006FCF4,listAddress=0x801E0000;
        cpu.Copy(0x800771C8,new byte[256]);cpu.Regs[31]=stop;cpu.Run(0x800521C0,stop);
        List<uint> admitted=[];for(uint at=scratch+0x2200;cpu.Get(at)!=0;at+=4){admitted.Add(cpu.Get(at));if(admitted.Count>256)throw new Exception("Unbounded render list");}
        check(p.Bindings.All(b=>admitted.Count(v=>v==b.ProjectedRowAddress)==1),"Both authored rows enter the actual animated render list exactly once.");
        for(int i=0;i<2;i++)cpu.Put(listAddress+(uint)i*4,p.Bindings[i].ProjectedRowAddress);cpu.Put(listAddress+8,0);
        for(int tick=0;tick<32;tick++)
        {
            cpu.Regs[4]=listAddress;cpu.Regs[5]=0;cpu.Regs[31]=stop;cpu.Calls.Clear();cpu.Run(0x800522C0,stop);
            foreach(var b in p.Bindings)
                check(cpu.Get(b.ProjectedRowAddress+0x3E,1)<2&&cpu.Get(b.ProjectedRowAddress+0x3F,1)<2&&cpu.Get(b.ProjectedRowAddress+0x40,1)==(uint)((tick+1)%2*32),"Each authored root stays in its owned two-frame animation.");
            check(!cpu.Calls.Contains(0x80055A78),"Native FF event tags do not dispatch effects/sounds.");
        }
        foreach(var b in p.Bindings)foreach(byte blend in new byte[]{0,32})
        {
            uint row=b.ProjectedRowAddress,animation=b.ProjectedRoot+0x3C;
            cpu.Put(row+0x40,blend,1);cpu.Regs[29]=row;cpu.Regs[28]=table;cpu.Run(0x80020A14,0x80020A38,true);
            check(cpu.Regs[15]==b.ProjectedRoot&&cpu.BoundaryLoad==(13,animation),"Unmodified renderer chooses this authored class root and animation.");
            cpu.Regs[13]=animation;cpu.Regs[30]=scratch+0x1600;cpu.Regs[12]=100;
            cpu.Regs[1]=cpu.Get(row+12);cpu.Regs[2]=cpu.Get(row+16);cpu.Regs[3]=cpu.Get(row+20);cpu.Regs[4]=0;
            cpu.Run(0x80020BB0,0x80020CAC);
            check(cpu.Get(scratch+0x1604)==animation&&cpu.Get(scratch+0x1634)==row,"Draw command retains selected model animation and row identity.");
            uint near=cpu.Get(animation+0x18),far=cpu.Get(animation+0x20);
            check(near==b.ProjectedRoot+(uint)b.Edit.Source.NearRange.PackageOffset&&far==b.ProjectedRoot+(uint)b.Edit.Source.FarRange.PackageOffset,"Selected animation owns its distinct near/far RGB arrays.");
            foreach(int color in Enumerable.Range(0,70))
            {
                cpu.Regs[1]=near+(uint)color*4;cpu.Regs[2]=far+(uint)color*4;cpu.Regs[31]=animation;cpu.Run(0x800215E4,0x80021610);
                uint expectedNear=W(b.Edit.CopyPackage(),b.Edit.Source.NearRange.PackageOffset+color*4),expectedFar=W(b.Edit.CopyPackage(),b.Edit.Source.FarRange.PackageOffset+color*4);
                check(cpu.Regs[5]==expectedNear&&cpu.Regs[6]==(expectedFar&255)*16&&cpu.Regs[7]==((expectedFar>>8)&255)*16&&cpu.Regs[8]==((expectedFar>>16)&255)*16,"Real RGB decode loads only this variant's authored near/far channels.");
            }
        }
        check(cpu.Get(p.Bindings[0].ProjectedRoot+(uint)p.Bindings[0].Edit.Source.NearRange.PackageOffset)!=cpu.Get(p.Bindings[1].ProjectedRoot+(uint)p.Bindings[1].Edit.Source.NearRange.PackageOffset),"Two actual native roots retain different RGB bytes without a global per-frame rewrite.");
        NativeRetirementCases.Run(cpu,p,check);
    }
}
