using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Universal;

static class FreshVerification
{
    static uint U(byte[] bytes,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at));
    static string Hash(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    public static void Run(string input,string directory,LevelCatalog retail,UniversalSourceLibrary donors,Action<bool,string> check)
    {
        input=Path.GetFullPath(input);using var inputStream=File.OpenRead(input);string inputHash=Convert.ToHexStringLower(SHA256.HashData(inputStream));
        var basis=UniversalDiscImage.Read(input);var target=SceneDocument.NativeTarget("PORTAL TEST");
        var catalog=new LevelCatalog(retail.Levels.Where(l=>l.LevelId!=65).Append(target));
        var originalTown=UniversalExeBindings.Read(donors.Disc.Exe,13);
        check(new UniversalLevelData(basis.EntryBytes(80)).Scene().RowCount==0,"real fresh baseline has no donor actors");
        var body=new UniversalLevelData(basis.EntryBytes(80));var source=donors.Get("stonehill");
        int returnRow=Enumerable.Range(0,source.Data.Scene().RowCount).Single(r=>BinaryPrimitives.ReadUInt16LittleEndian(source.Data.Row(r).AsSpan(0x36))==9);
        var imported=UniversalMobyComposer.Compose(target,body.Bytes,basis.EntryBytes(79),basis.Exe,
            key=>key==target.Key?(target,body,new UniversalOverlay(basis.EntryBytes(79),UniversalExeBindings.Read(basis.Exe,65))):donors.Get(key),
            donors.SharedClasses,UniversalMobyExport.DefaultProtectedVram(input,target),Enumerable.Range(0,8).Select(n=>new UniversalMobyPlacement("stonehill",returnRow,8960,9600,8704,EditorId:"return-home-"+n,PersistentSlot:n,Retired:n!=7)).ToArray(),
            sharedModelPackage:donors.SharedModelPackage,playerAnimationSource:donors.PlayerAnimations);
        check(imported.NewRowIndices.Count(r=>r>=0)==1&&imported.LevelData.Length>0,"normal universal composer imports actual source ReturnHome into fresh65");
        List<byte[]> certificates=[];
        foreach(var level in retail.Levels.Where(l=>l.LevelId%10 is >=1 and <=4&&l.LevelId!=63))
        {
            var owner=donors.Get(level.Key).Overlay;var dispatch=UniversalDispatchAnalysis.FindUpdateSwitch(owner);
            var trace=UniversalDispatchAnalysis.Trace(owner,dispatch.TreeStart,9,dispatch.Seeds,dispatch.ClassRegister);
            var closure=UniversalDispatchAnalysis.Closure(owner,dispatch.Function,[trace.Stop],new HashSet<uint>{dispatch.TreeStart}).Instructions;
            foreach(uint pc in closure.Where(pc=>owner.Word(pc)==UniversalMips.EncodeJ(3,NativePortalRouteRuntime.TransitionAddress)))
            {
                check(Enumerable.Range(0,13).All(i=>closure.Contains(pc-44+(uint)i*4)),"complete original class9 instruction certificate");
                var bytes=owner.Bytes.Slice(checked((int)(pc-44-UniversalOverlay.Base)),52).ToArray();
                if(!certificates.Any(c=>c.SequenceEqual(bytes)))certificates.Add(bytes);
            }
        }
        check(certificates.Count==5,"all23 original ReturnHome packets produce five exact source signatures");
        var importedCalls=Enumerable.Range(0,Math.Max(0,(imported.Overlay.Length-52)/4+1)).Select(i=>i*4)
            .Where(p=>certificates.Any(c=>imported.Overlay.AsSpan(p,52).SequenceEqual(c))).Select(p=>p+44).ToArray();
        check(importedCalls.Length==2,"fresh import retains dormant nativeTown packet plus one actual importedStone packet");
        var stone=new PortalEndpoint("artisans",38);var town=new PortalEndpoint("artisans",39);
        var document=PortalRouteDocument.Empty.WithDestination(stone,65,true).WithReturn(13,town);
        var exe=imported.Executable.ToArray();Dictionary<int,byte[]> entries=new(){{79,imported.Overlay.ToArray()},{80,imported.LevelData.ToArray()}};
        NativePortalRouteComposer.Apply(donors,catalog,exe,entries,basis.EntryBytes,document,true);
        var freshBindings=UniversalExeBindings.Read(exe,65);var townBindings=UniversalExeBindings.Read(exe,13);
        check(townBindings.Count==originalTown.Count&&townBindings.Where(b=>b.Key!=UniversalExeBindings.UpdateGlobal&&b.Key!=UniversalExeBindings.CopyBufferGlobal).All(b=>b.Value.Value==originalTown[b.Key].Value),"ID13 preserves all seven unrelated retail callbacks with simultaneous custom ID65 route");
        check(freshBindings[UniversalExeBindings.UpdateGlobal].Value!=townBindings[UniversalExeBindings.UpdateGlobal].Value,"ID13 and65 have distinct selected ReturnHome callbacks");
        foreach(var (id,entry,home,token) in new[]{(65,79,10,11),(13,donors.Get("townsquare").Level.SourceWadEntry-1,10,13)})
        {
            var bindings=UniversalExeBindings.Read(exe,id);uint update=bindings[UniversalExeBindings.UpdateGlobal].Value;
            byte[] overlay=entries[entry];int off=checked((int)(update-UniversalOverlay.Base));
            check(U(overlay,off+16)==NativePortalRouteRuntime.Tag&&U(overlay,off+20)==home&&U(overlay,off+24)==token,"independent fresh and retail endpoint metadata");
            uint call=UniversalMips.EncodeJ(3,update+28);int count=Enumerable.Range(0,overlay.Length/4).Count(i=>U(overlay,i*4)==call);
            byte[] previousOverlay=id==65?imported.Overlay:donors.Get("townsquare").Overlay.Bytes.ToArray();
            byte[] previousExe=id==65?imported.Executable:donors.Disc.Exe;
            int[] calls=id==65?importedCalls:Enumerable.Range(0,(previousOverlay.Length-52)/4+1).Select(i=>i*4).Where(p=>certificates.Any(c=>previousOverlay.AsSpan(p,52).SequenceEqual(c))).Select(p=>p+44).ToArray();
            check(count==calls.Length&&calls.All(p=>U(overlay,p)==call),"every complete source imported/native packet changes to selected route stub");
            int used=checked((int)(UniversalExeBindings.Read(previousExe,id)[UniversalExeBindings.CopyBufferGlobal].Value-UniversalOverlay.Base));
            check(Enumerable.Range(0,used).All(p=>calls.Any(c=>p>=c&&p<c+4)||overlay[p]==previousOverlay[p]),"original composed resident bytes preserved outside complete native call certificates");
            uint previousUpdate=UniversalExeBindings.Read(previousExe,id)[UniversalExeBindings.UpdateGlobal].Value;
            int previous=checked((int)(previousUpdate-UniversalOverlay.Base));
            if(id==65)check(U(previousOverlay,previous+8)==NativeMobySaveMap.Tag,"normal fresh import emits a real sparse save-map wrapper");
            if(id==65)check(U(overlay,off+8)==U(previousOverlay,previous+8)&&U(overlay,off+12)==U(previousOverlay,previous+12),"portal wrapper preserves actual imported save-map metadata");
        }
        check(UniversalExeBindings.Word(exe,UniversalExeBindings.LevelInitJumpTable+13*4)==NativePortalRouteFreshBinding.Address&&UniversalExeBindings.Word(exe,UniversalExeBindings.LevelInitJumpTable+65*4)==NativePortalRouteFreshBinding.Address,"bounded shared initializer selects both IDs");
        check(U(exe,0x47910)==UniversalMips.EncodeI(10,2,2,66),"native entry limit includes65");
        var homeworld=donors.Get("artisans");var art=new UniversalLevelData(entries[homeworld.Level.SourceWadEntry]);
        int letters=stone.Resolve().LetteringTrueIndex;
        check(U(art.CaptureRow("artisans",letters).Properties,0)==35,"authored65 lettering selects actual native slot35");
        // A missing complete packet while a native class9 actor remains must refuse atomically.
        byte[] badExe=donors.Disc.Exe.ToArray(),badOverlay=donors.Get("toasty").Overlay.Bytes.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(badOverlay.AsSpan(checked((int)(0x8007B528-UniversalOverlay.Base))),0);
        Dictionary<int,byte[]> badEntries=new(){{donors.Get("toasty").Level.SourceWadEntry-1,badOverlay}};
        string initialExe=Hash(badExe),initialOverlay=Hash(badOverlay);bool refused=false;
        try{NativePortalRouteComposer.Apply(donors,retail,badExe,badEntries,donors.Disc.EntryBytes,PortalRouteDocument.Empty.WithReturn(14,stone));}
        catch(InvalidDataException){refused=true;}
        check(refused&&Hash(badExe)==initialExe&&badEntries.Count==1&&Hash(badEntries.Values.Single())==initialOverlay,"incomplete ReturnHome packet refuses without changing input owners");
        File.WriteAllText(Path.Combine(directory,"fresh-source-integration.json"),JsonSerializer.Serialize(new{Scope="real fresh65 baseline plus normal universal ReturnHome import and final portal composition",
            InputImage=input,InputImageSha256=inputHash,InputExeHash=Hash(basis.Exe),SourceHash=UniversalSourceLibrary.OriginalUsaSha256,CoreModuleVersionId=typeof(NativePortalRouteComposer).Module.ModuleVersionId,
            CoreModuleHash=Hash(File.ReadAllBytes(typeof(NativePortalRouteComposer).Assembly.Location)),SourceReturnRow=returnRow,CertificateSignatures=certificates.Select(Hash),ImportedPacketCount=importedCalls.Length,imported.Classes,imported.Ram,imported.PhysicalToLogicalSaveRows,
            FinalFreshRuntimeEnd=UniversalMobyComposer.RuntimeEnd(new UniversalLevelData(entries[80]),freshBindings[UniversalExeBindings.CopyBufferGlobal].Value),
            Fresh=freshBindings.Values,TownSquare=townBindings.Values,Route=document,FinalExeHash=Hash(exe),Entries=entries.Select(e=>new{e.Key,Hash=Hash(e.Value),Length=e.Value.Length}),RuntimeTested=false},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("PORTAL_ROUTE_FRESH_SOURCE_INTEGRATION_OK");
    }
}
