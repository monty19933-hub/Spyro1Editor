using System.Buffers.Binary;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;

internal static class NativeLoaderCases
{
    internal static void Run(NativeCollectibleSaveWorkspacePlan plan,Action<bool,string> check)
    {
        var cpu=new RenderProbeCpu();var ancestry=plan.Ancestry;
        byte[] data=plan.CopyLevelData(),exe=ancestry.CopyExecutable();
        uint W(int at)=>BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(at));
        byte[] Bytes(uint at,int length)=>Enumerable.Range(0,length).Select(i=>(byte)cpu.Get(at+(uint)i,1)).ToArray();
        cpu.Copy(0x8000F800,exe);cpu.Copy(0x80076C00,data.AsSpan(0,0x800).ToArray());
        cpu.Put(0x800785E0,plan.Memory.ModelStart);cpu.Regs[29]=0x801FFB00;
        // Stop before the real CD service. Zero entry base and handle17 are
        // prescribed test inputs: this proves native argument construction,
        // not actual CD completion, physical WAD relocation or loader lifetime.
        cpu.Put(0x80076B90,17);cpu.Put(0x80075964,0);cpu.Put(0x8007A720,0);
        cpu.Run(0x800159FC,0x80016698);
        check(cpu.Regs[4]==17&&cpu.Regs[5]==plan.Memory.ModelStart&&cpu.Regs[6]==plan.Memory.FinalModelBytes&&cpu.Regs[7]==W(0x10)&&
            cpu.Get(cpu.Regs[29]+0x10)==600,"Original model CD setup requests the entire measured descriptor, including explicit new owners");
        // Model the completed transfer with the exact complete descriptor body,
        // then execute the unchanged actual registration/helper instructions.
        cpu.Copy(plan.Memory.ModelStart,data.AsSpan((int)W(0x10),(int)W(0x14)).ToArray());
        byte[] tail=Bytes(plan.Geometry.TailAddress,plan.GrowthBytes);
        int slot=ancestry.Authored.Bindings[0].RegistrationSlot;
        cpu.Regs[16]=(uint)slot;cpu.Regs[17]=0x80076C50+(uint)slot*4;cpu.Regs[18]=0x80076D50+(uint)slot*2;
        cpu.Regs[19]=0x80076C50;cpu.Regs[20]=0x80076378;cpu.Writes.Clear();cpu.Run(0x80015AE8,0x80015B34);
        foreach(var model in ancestry.Lifecycle.Projection.Models)
        {
            check(cpu.Get(0x80076378+model.AuthoredClass*4u)==model.RootAddress,"Actual native registration retains existing root "+model.RowId);
            foreach(var field in model.LoaderFields)
                check(cpu.Get(model.RootAddress+(uint)field.PackageFieldOffset)==field.ProjectedLoadedWord,"Unchanged native package relocation "+model.RowId);
        }
        check(Bytes(plan.Geometry.TailAddress,plan.GrowthBytes).SequenceEqual(tail)&&
            cpu.Writes.All(a=>a<plan.Geometry.TailAddress||a>=plan.Geometry.EndAddress),"Registration never treats nonregistered tail as another model or writes it");
        cpu.Run(0x80015B34,0x800163BC);
        check(cpu.Get(0x800785E4)==plan.Memory.FinalSceneStart&&cpu.Get(0x80078600)==W(0x18)&&cpu.Get(0x80078604)==W(0x1C),
            "Actual full-model-length scene placement, not a guessed root highwater");
        cpu.Run(0x80015B80,0x80016698);
        check(cpu.Regs[5]==plan.Memory.FinalSceneStart&&cpu.Regs[6]==W(0x1C)&&cpu.Regs[7]==W(0x18),"Actual scene CD request begins after every new workspace byte");
        uint sceneBase=plan.Memory.FinalSceneStart;byte[] scene=data.AsSpan((int)W(0x18),(int)W(0x1C)).ToArray();
        cpu.Copy(sceneBase,scene);int moby=ancestry.Authored.Integration.OriginalSceneSource.RowTableOffset-8;
        cpu.Regs[16]=sceneBase+(uint)moby;cpu.Regs[5]=cpu.Regs[16];cpu.Writes.Clear();cpu.Run(0x80013F24,0x80013FC4);
        check(cpu.Get(0x80075828)==plan.Memory.FinalMobyTable&&cpu.Get(0x80075890)==plan.RequiredRowRebindings[^1].NewRowAddress+0x58,
            "Native row arena and terminator follow the shifted scene");
        int fix=ancestry.Authored.Integration.OriginalSceneSource.FixupOffset+24;
        cpu.Regs[16]=sceneBase+(uint)fix;cpu.Regs[17]=0;cpu.Regs[18]=sceneBase;cpu.Run(0x80014040,0x80014084);
        foreach(var row in plan.RequiredRowRebindings)
        {
            check(cpu.Get(row.NewRowAddress)==row.NewPrivateAddress,"Native relative private pointer relocates to required new binding");
            cpu.Regs[16]=0;cpu.Regs[4]=row.NewRowAddress;cpu.Run(0x8001417C,0x80052758);
            check(cpu.Get(row.NewRowAddress+0x34,2)==0xFFFF,"Native animated row initializer still succeeds after explicit insertion");
        }
        check(Bytes(plan.Geometry.TailAddress,plan.GrowthBytes).SequenceEqual(tail)&&
            cpu.Writes.All(a=>a<plan.Geometry.TailAddress||a>=plan.Geometry.EndAddress),"Scene fixups/row initialization preserve every code, scratch and padding byte");
        // No ledger readiness is invented. Executing the relocated preparation
        // in this unadmitted fixture must refuse without touching any owner.
        cpu.Regs[4]=plan.Preparation.OldTailAddress;cpu.Regs[5]=0x100;cpu.Regs[6]=0x801FF600;cpu.Regs[7]=0;
        cpu.Regs[29]=0x801FFB00;cpu.Regs[31]=0x80010000;cpu.Writes.Clear();cpu.Run(plan.Preparation.EntryPc,0x80010000);
        check(cpu.Regs[2]!=0&&cpu.Writes.Count==0,"Relocated emitted code remains fail-closed with an unadmitted ledger; no card call");
        if(plan.Controller is{} controller)
        {
            // The actual materialized Begin entry is also executable after the
            // original loader windows. A zero ledger never grants admission.
            // Framed refusal may change only controller state and its stack.
            cpu.Put(0x80078D14,1);cpu.Put(0x80078D18,0);cpu.Put(0x80078D1C,0);cpu.Put(0x80078D20,0);
            cpu.Regs[4]=controller.OrdinaryInputAddress;cpu.Regs[5]=0x600;cpu.Regs[6]=0x801FF600;cpu.Regs[7]=0;
            cpu.Regs[29]=0x801FFB00;cpu.Regs[31]=0x80010000;cpu.Hi=0x11223344;cpu.Lo=0x55667788;
            cpu.Writes.Clear();cpu.Calls.Clear();cpu.Run(controller.BeginPc,0x80010000);
            check(cpu.Regs[2]==(uint)NativeCollectibleSaveControllerStage.Failed&&
                cpu.Get(controller.StateAddress)==(uint)NativeCollectibleSaveControllerStage.Failed,
                "Actual loaded controller refuses unadmitted ledger with terminal failure");
            check(!cpu.Calls.Any(p=>p is NativeCollectibleSaveControllerKernel.ReadPc or NativeCollectibleSaveControllerKernel.WritePc or NativeCollectibleSaveControllerKernel.PollPc)&&
                cpu.Writes.All(a=>a>=controller.StateAddress&&a<controller.StateAddress+NativeCollectibleSaveControllerKernel.StateBytes||a>=0x801FFAC0&&a<0x801FFB00),
                "Loaded refusal reaches no native card entry and writes only owned state/current frame");
            byte[] refused=Bytes(plan.Geometry.TailAddress,plan.GrowthBytes);
            check(Enumerable.Range(0,refused.Length).All(i=>plan.Geometry.TailAddress+i>=controller.StateAddress&&
                plan.Geometry.TailAddress+i<controller.StateAddress+NativeCollectibleSaveControllerKernel.StateBytes||refused[i]==tail[i]),
                "Loaded refusal preserves all code, filename, buffers and padding outside terminal state");
            check(cpu.Regs[29]==0x801FFB00&&cpu.Regs[31]==0x80010000&&cpu.Hi==0x11223344&&cpu.Lo==0x55667788,
                "Loaded controller refusal restores caller SP/RA and HI/LO");
        }
    }
}
