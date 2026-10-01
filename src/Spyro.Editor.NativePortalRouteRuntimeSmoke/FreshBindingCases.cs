using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Universal;

internal static class FreshBindingCases
{
    internal static async Task Run(string sourcePath,byte[] original,string workspace,UniversalSourceLibrary donors,Action<bool,string> check,Action<Action,string> refuse)
    {
        string folder=Path.Combine(workspace,"work","universal-gameplay-20261001","dependencies","portal-fresh-binding-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        var issued=await NativeFreshMainCompilation.CaptureAsync(sourcePath,SceneDocument.New("Portal Bind Proof"),Path.Combine(folder,"fresh-baseline.bin"));
        byte[] fresh=issued.CopyExecutable();var originalTown=UniversalExeBindings.Read(original,13);var legacy=UniversalExeBindings.Read(fresh,65);
        uint[] changed=[UniversalExeBindings.CopyBufferGlobal,UniversalExeBindings.InitGlobal,UniversalExeBindings.UpdateGlobal];
        check(legacy.Count==9&&originalTown.Count==9,"captured fresh and original Town have all nine callbacks");
        check(originalTown.Where(p=>!changed.Contains(p.Key)).All(p=>legacy[p.Key].Value==p.Value.Value),"all six other fresh callbacks match authenticated Town literals");
        check(UniversalExeBindings.Word(original,UniversalExeBindings.LevelInitJumpTable+65*4)==NativePortalRouteFreshBinding.CommonTail&&UniversalExeBindings.Word(fresh,UniversalExeBindings.LevelInitJumpTable+65*4)==NativePortalRouteFreshBinding.Address,"retail ID65 is tail-only; actual fresh bootstrap alone introduces Town alias");
        var route=NativePortalRouteRuntime.AppendReturnWrapper(issued.CopyOverlay(),legacy[UniversalExeBindings.UpdateGlobal].Value,legacy[UniversalExeBindings.CopyBufferGlobal].Value,10,11);
        byte[] split=fresh.ToArray();NativePortalRouteFreshBinding.Rebind(original,split,route.UpdatePointer,route.CopyBuffer);
        var after=UniversalExeBindings.Read(split,65);var town=UniversalExeBindings.Read(split,13);
        check(town.Count==9&&originalTown.All(p=>town[p.Key].Value==p.Value.Value),"Town readback retains all nine retail callbacks");
        check(after.Count==9&&after[UniversalExeBindings.UpdateGlobal].Value==route.UpdatePointer&&after[UniversalExeBindings.CopyBufferGlobal].Value==route.CopyBuffer&&after[UniversalExeBindings.InitGlobal].Value==legacy[UniversalExeBindings.InitGlobal].Value,"ID65 readback has private route pointers and original captured fresh init");
        check(after.Where(p=>!changed.Contains(p.Key)).All(p=>p.Value.Value==originalTown[p.Key].Value),"ID65 retains all six other captured callback values");
        check(Enumerable.Range(0,split.Length).Where(i=>fresh[i]!=split[i]).All(i=>i>=NativePortalRouteFreshBinding.Address-UniversalExeBindings.ExeBase&&i<NativePortalRouteFreshBinding.Address-UniversalExeBindings.ExeBase+NativePortalRouteFreshBinding.Length),"fresh split changes only authenticated152-byte Town owner");
        uint[] globals=[..originalTown.Keys,0x800785DC];
        foreach(int id in new[]{13,65})
        {
            var baseline=new ScalarMips();baseline.Segment(UniversalExeBindings.ExeBase,id==13?original:fresh);baseline.Memory[0x8007596C]=(uint)id;
            var actual=new ScalarMips();actual.Segment(UniversalExeBindings.ExeBase,split);actual.Memory[0x8007596C]=(uint)id;
            uint[] registers=actual.R.ToArray();baseline.Run(NativePortalRouteFreshBinding.Address,baseline.R[31]);actual.Run(NativePortalRouteFreshBinding.Address,actual.R[31]);
            check(globals.All(g=>actual.Memory[g]==(id==65&&g==UniversalExeBindings.UpdateGlobal?route.UpdatePointer:id==65&&(g==UniversalExeBindings.CopyBufferGlobal||g==0x800785DC)?route.CopyBuffer:baseline.Memory[g])),"actual delayed native initializer execution preserves correct globals "+id);
            check(Enumerable.Range(4,4).Concat(Enumerable.Range(16,8)).Concat(new[]{29,31}).All(r=>actual.R[r]==registers[r])&&actual.PendingLoad==null,"compact initializer preserves argument/saved/frame registers "+id);
            check(actual.Writes.All(w=>globals.Contains(w.Address)),"compact initializer owns only exact callback globals "+id);
        }
        // Every retail level's nine-or-fewer callback values remain exactly retail after removing the bootstrap alias.
        foreach(int id in Enumerable.Range(10,55).Where(i=>i%10<=5))
        {
            var retail=UniversalExeBindings.Read(original,id);var final=UniversalExeBindings.Read(split,id);
            check(final.Count==retail.Count&&retail.All(p=>final.TryGetValue(p.Key,out var v)&&v.Value==p.Value.Value),"all retail per-level callbacks preserve original values "+id);
        }
        byte[] before=split.ToArray();NativePortalRouteFreshBinding.Rebind(original,split,route.UpdatePointer,route.CopyBuffer);check(split.SequenceEqual(before),"compact fresh rebind is idempotent");
        refuse(()=>UniversalExeBindings.Patch(split,after[UniversalExeBindings.UpdateGlobal],route.UpdatePointer+4),"virtual compact binding cannot patch shared literal instruction");check(split.SequenceEqual(before),"raw compact patch refusal leaves source intact");
        foreach(uint address in new[]{NativePortalRouteFreshBinding.Address,NativePortalRouteFreshBinding.Address+148,NativePortalRouteFreshBinding.CommonTail,0x8005A470u,0x800155F4u,UniversalExeBindings.LevelInitJumpTable+65*4})
        {
            byte[] bad=fresh.ToArray();bad[(int)(address-UniversalExeBindings.ExeBase)]^=1;byte[] capture=bad.ToArray();
            refuse(()=>NativePortalRouteFreshBinding.Rebind(original,bad,route.UpdatePointer,route.CopyBuffer),"changed staged fresh owner refused "+address.ToString("X8"));check(bad.SequenceEqual(capture),"fresh owner refusal leaves all bytes intact");
        }
        foreach(uint address in new[]{NativePortalRouteFreshBinding.Address,NativePortalRouteFreshBinding.CommonTail,0x8005A470u,0x800155F4u,UniversalExeBindings.LevelInitJumpTable+13*4,UniversalExeBindings.LevelInitJumpTable+65*4})
        {
            byte[] bad=original.ToArray();bad[(int)(address-UniversalExeBindings.ExeBase)]^=1;
            refuse(()=>NativePortalRouteFreshBinding.Rebind(bad,fresh.ToArray(),route.UpdatePointer,route.CopyBuffer),"changed retail fresh source owner refused "+address.ToString("X8"));
        }
        byte[] unexpected=fresh.ToArray();var particle=legacy[UniversalExeBindings.ParticleSpawnGlobal];UniversalExeBindings.Patch(unexpected,particle,particle.Value+4);
        refuse(()=>NativePortalRouteFreshBinding.Rebind(original,unexpected,route.UpdatePointer,route.CopyBuffer),"unsupported fourth fresh callback override refused");
        byte[] malformed=split.ToArray();malformed[(int)(NativePortalRouteFreshBinding.Address-UniversalExeBindings.ExeBase)+12]^=1;
        refuse(()=>UniversalExeBindings.Read(malformed,65),"modified compact initializer readback refused");
        foreach(uint pointer in new[]{UniversalOverlay.Base-4,route.UpdatePointer+1,0x80200000u})
            refuse(()=>NativePortalRouteFreshBinding.Rebind(original,split.ToArray(),pointer,route.CopyBuffer),"invalid fresh resident callback refused");
        refuse(()=>NativePortalRouteFreshBinding.RebindTown(original,fresh.ToArray(),route.UpdatePointer,route.CopyBuffer),"Town rebind refuses an unsplit legacy alias");
        foreach(uint copy in new[]{0x8007AA38u,0x80080000u,0x8008FFFCu,0x80090000u,0x800A0010u,0x80180000u})
        foreach(uint update in new[]{0x8007AA38u,0x80080000u,0x8008FFFCu,0x80090000u,0x800A0010u,0x80180000u})
        {
            byte[] both=split.ToArray();NativePortalRouteFreshBinding.RebindTown(original,both,update,copy);
            var bothTown=UniversalExeBindings.Read(both,13);var bothFresh=UniversalExeBindings.Read(both,65);
            check(bothTown.Count==9&&originalTown.All(p=>bothTown[p.Key].Value==(p.Key==UniversalExeBindings.CopyBufferGlobal?copy:p.Key==UniversalExeBindings.UpdateGlobal?update:p.Value.Value)),"independent Town compact pointer readback "+copy.ToString("X8")+":"+update.ToString("X8"));
            check(after.All(p=>bothFresh[p.Key].Value==p.Value.Value),"Town route never changes any fresh callback");
            foreach(int id in new[]{13,65})
            {
                var m=new ScalarMips();m.Segment(UniversalExeBindings.ExeBase,both);m.Memory[0x8007596C]=(uint)id;uint[] registers=m.R.ToArray();m.Run(NativePortalRouteFreshBinding.Address,m.R[31]);
                var expected=id==13?bothTown:bothFresh;
                check(expected.All(p=>m.Memory[p.Key]==p.Value.Value)&&m.Memory[0x800785DC]==expected[UniversalExeBindings.CopyBufferGlobal].Value,"actual scalar simultaneous Town/fresh split "+id+":"+copy.ToString("X8")+":"+update.ToString("X8"));
                check(Enumerable.Range(4,4).Concat(Enumerable.Range(16,8)).Concat(new[]{29,31}).All(r=>m.R[r]==registers[r])&&m.PendingLoad==null,"simultaneous split preserves argument/saved/frame registers "+id);
            }
            byte[] unchanged=both.ToArray();NativePortalRouteFreshBinding.Rebind(original,both,route.UpdatePointer,route.CopyBuffer);check(both.SequenceEqual(unchanged),"fresh rebind preserves current independent Town route");
            NativePortalRouteFreshBinding.RebindTown(original,both,update,copy);check(both.SequenceEqual(unchanged),"Town route rebind is idempotent");
        }
        for(int word=0;word<NativePortalRouteFreshBinding.Length;word+=4)
        {
            byte[] bad=split.ToArray();bad[(int)(NativePortalRouteFreshBinding.Address-UniversalExeBindings.ExeBase)+word+3]^=0x80;
            byte[] snapshot=bad.ToArray();refuse(()=>NativePortalRouteFreshBinding.RebindTown(original,bad,route.UpdatePointer,route.CopyBuffer),"changed compact instruction/padding refused "+word);check(snapshot.SequenceEqual(bad),"compact negative test is atomic");
        }
        foreach(uint pointer in new[]{UniversalOverlay.Base-4,route.UpdatePointer+1,0x80200000u})
            refuse(()=>NativePortalRouteFreshBinding.RebindTown(original,split.ToArray(),pointer,route.CopyBuffer),"invalid Town resident callback refused");
        var source=donors.Get("stonehill");int returnRow=Enumerable.Range(0,source.Data.Scene().RowCount)
            .Single(row=>BinaryPrimitives.ReadUInt16LittleEndian(source.Data.Row(row).AsSpan(0x36))==9);
        var placed=Enumerable.Range(0,8).Select(slot=>new UniversalMobyPlacement("stonehill",returnRow,8960,9600,8704,
            EditorId:"live-return-"+slot,PersistentSlot:slot,Retired:slot!=7)).ToArray();
        var imported=issued.Compose(donors,placed);var importedBindings=UniversalExeBindings.Read(imported.Executable,65);
        var contexts=NativePortalReturnClass.Capture(imported.Classes);
        check(contexts.Count==1&&contexts[0].SourceLevelKey=="stonehill"&&contexts[0].DestinationClassId==0x1FD,"actual composition captures source009 as live mapped0x1FD provenance");
        var routed=NativePortalRouteRuntime.AppendReturnWrapper(imported.Overlay,importedBindings[UniversalExeBindings.UpdateGlobal].Value,
            importedBindings[UniversalExeBindings.CopyBufferGlobal].Value,10,11);
        var copied=routed.Overlay.ToArray();int liveCalls=NativePortalRouteRuntime.PatchReturnHomeCalls(donors,routed,importedBindings,
            new UniversalLevelData(imported.LevelData),issued.Target.Key,contexts);
        check(liveCalls==2,"live owner proof accepts actual SMAP wrapped import while preserving dormant native certificate patching");
        byte[] fixedCalls=routed.Overlay.ToArray();check(NativePortalRouteRuntime.PatchReturnHomeCalls(donors,routed,importedBindings,
            new UniversalLevelData(imported.LevelData),issued.Target.Key,contexts)==2&&routed.Overlay.SequenceEqual(fixedCalls),"live-owner validation supports canonical already patched packets");
        int nativePacket=checked((int)(0x8007E07C-UniversalOverlay.Base));
        var patchedCalls=Enumerable.Range(0,routed.Overlay.Length/4).Select(i=>i*4).Where(at=>
            BinaryPrimitives.ReadUInt32LittleEndian(routed.Overlay.AsSpan(at))==UniversalMips.EncodeJ(3,routed.ReturnStubPointer)).ToArray();
        int importedPacket=patchedCalls.Single(at=>at!=nativePacket);
        foreach(int at in new[]{importedPacket,importedPacket-40,importedPacket+4})
        {
            byte[] bad=copied.ToArray();bad[at]^=1;var liveBefore=bad.ToArray();var badRoute=routed with{Overlay=bad};
            refuse(()=>NativePortalRouteRuntime.PatchReturnHomeCalls(donors,badRoute,importedBindings,new UniversalLevelData(imported.LevelData),issued.Target.Key,contexts),"damaged live mapped packet refuses despite intact dormant native9 "+at);
            check(bad.SequenceEqual(liveBefore)&&bad.AsSpan(nativePacket-44,52).SequenceEqual(copied.AsSpan(nativePacket-44,52)),"live-owner refusal changes no byte including dormant native packet");
        }
        byte[] missingCall=copied.ToArray();BinaryPrimitives.WriteUInt32LittleEndian(missingCall.AsSpan(importedPacket),0);byte[] missingSnapshot=missingCall.ToArray();
        refuse(()=>NativePortalRouteRuntime.PatchReturnHomeCalls(donors,routed with{Overlay=missingCall},importedBindings,
            new UniversalLevelData(imported.LevelData),issued.Target.Key,contexts),"removed live mapped transition refuses even when dormant native9 still matches");
        check(missingCall.SequenceEqual(missingSnapshot),"removed mapped transition refusal is atomic");
        var futureData=new UniversalLevelData(issued.CopyLevelData());check(futureData.Scene().RowCount==0,"dependency validation fixture starts with no placed actor rows");
        refuse(()=>NativePortalRouteRuntime.PatchReturnHomeCalls(donors,routed with{Overlay=missingCall.ToArray()},importedBindings,
            futureData,issued.Target.Key,contexts),"a declared future-spawn ReturnHome owner cannot bypass validation because its initial row is absent");
        check(NativePortalRouteRuntime.PatchReturnHomeCalls(donors,routed with{Overlay=copied.ToArray()},importedBindings,
            futureData,issued.Target.Key,contexts)==2,"valid declared future-spawn ReturnHome owner remains supported");
        var falseContexts=new[]{new NativePortalReturnClass("hauntedtowers",0x1FD)};
        refuse(()=>NativePortalRouteRuntime.PatchReturnHomeCalls(donors,routed with{Overlay=copied.ToArray()},importedBindings,
            new UniversalLevelData(imported.LevelData),issued.Target.Key,falseContexts),"mapped behavior with different source packet certificate refuses");
        var townSource=donors.Get("townsquare");int townReturnRow=Enumerable.Range(0,townSource.Data.Scene().RowCount)
            .Single(row=>BinaryPrimitives.ReadUInt16LittleEndian(townSource.Data.Row(row).AsSpan(0x36))==9);
        var reused=UniversalMobyComposer.Compose(townSource.Level,townSource.Data.Bytes,townSource.Overlay.Bytes.ToArray(),original,donors.Get,donors.SharedClasses,
            UniversalMobyExport.DefaultProtectedVram(sourcePath,townSource.Level),[new UniversalMobyPlacement("townsquare",townReturnRow,8960,9600,8704,EditorId:"reused-town-return")],
            sharedModelPackage:donors.SharedModelPackage,playerAnimationSource:donors.PlayerAnimations);
        var reusedContexts=NativePortalReturnClass.Capture(reused.Classes);var reusedClass=reused.Classes.Single(c=>c.ClassId==9);
        check(reusedContexts.Count==1&&reusedContexts[0].SourceLevelKey=="townsquare"&&reusedContexts[0].DestinationClassId==reusedClass.DestinationClassId,"actual Town return composition always records source009 including native reuse");
        check(reusedClass.DestinationClassId==9&&!reusedClass.BehaviorImported,"actual ordinary Town ReturnHome reuses native class009 behavior");
        var reusedBindings=UniversalExeBindings.Read(reused.Executable,13);var reusedRoute=NativePortalRouteRuntime.AppendReturnWrapper(reused.Overlay,
            reusedBindings[UniversalExeBindings.UpdateGlobal].Value,reusedBindings[UniversalExeBindings.CopyBufferGlobal].Value,10,13);
        check(NativePortalRouteRuntime.PatchReturnHomeCalls(donors,reusedRoute,reusedBindings,new UniversalLevelData(reused.LevelData),townSource.Level.Key,reusedContexts)==1,"native class009 reuse validates its actual Town engine certificate");
        var reusedBad=reusedRoute.Overlay.ToArray();reusedBad[nativePacket]^=1;byte[] reusedSnapshot=reusedBad.ToArray();
        refuse(()=>NativePortalRouteRuntime.PatchReturnHomeCalls(donors,reusedRoute with{Overlay=reusedBad},reusedBindings,
            new UniversalLevelData(reused.LevelData),townSource.Level.Key,reusedContexts),"changed live reused class009 packet refuses");
        check(reusedBad.SequenceEqual(reusedSnapshot),"reused class009 packet rejection is atomic");
        File.WriteAllBytes(Path.Combine(folder,"fresh-exe-before.bin"),fresh);File.WriteAllBytes(Path.Combine(folder,"fresh-exe-after.bin"),split);
        File.WriteAllText(Path.Combine(folder,"receipt.json"),JsonSerializer.Serialize(new{ownerAddress=NativePortalRouteFreshBinding.Address,ownerLength=NativePortalRouteFreshBinding.Length,sourceOwnerSha256=NativePortalRouteFreshBinding.SourceSha256,freshInit=after[UniversalExeBindings.InitGlobal].Value,update=route.UpdatePointer,copy=route.CopyBuffer,town=town.ToDictionary(p=>p.Key,p=>p.Value.Value),runtime="not-tested"},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"NATIVE_PORTAL_ROUTE_FRESH_BINDING_OK ownerBytes=152 emittedBytes=144..152 simultaneousTownPairs=36 privateCapture={folder} runtime=not-tested");
    }
}
