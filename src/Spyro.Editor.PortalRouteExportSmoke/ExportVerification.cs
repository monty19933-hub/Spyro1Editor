using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Cache;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Universal;

static class ExportVerification
{
    static int I(byte[] b,int p)=>BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(p));
    static uint U(byte[] b,int p)=>BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(p));
    static string Hash(byte[] b)=>Convert.ToHexStringLower(SHA256.HashData(b));
    public static async Task Run(string original,string directory,LevelCatalog catalog,UniversalSourceLibrary library,Action<bool,string> check,bool writeFixtures=true)
    {
        var stone=new PortalEndpoint("artisans",38);var town=new PortalEndpoint("artisans",39);
        var reports=new List<object>();
        (byte[] exe,Dictionary<int,byte[]> changes) Compose(PortalRouteDocument route)
        {
            byte[] exe=library.Disc.Exe.ToArray();Dictionary<int,byte[]> changes=[];
            NativePortalRouteComposer.Apply(library,catalog,exe,changes,library.Disc.EntryBytes,route);
            return(exe,changes);
        }
        void VerifyDestination(Dictionary<int,byte[]> changes,PortalEndpoint endpoint,int destination)
        {
            var owner=library.Get(endpoint.HomeworldKey);var source=PortalSourceDataLocator.Locate(original,owner.Level);
            var portal=source.Portals.Single(p=>p.SourcePathMobyTrueIndex==endpoint.PathTrueIndex);
            var surface=source.SpecialSurfaces.Single(p=>p.Type==6&&p.PortalIndex==portal.Index);
            var data=new UniversalLevelData(changes[owner.Level.SourceWadEntry]);
            int surfaceOffset=checked((int)(surface.WadOffset-(source.LevelDataWadOffset-data.Subfiles()[1].Offset)));
            check(I(data.Bytes,surfaceOffset+4)==destination,"entry descriptor destination changed");
            check(I(data.Bytes,checked((int)(portal.WadOffset-(source.LevelDataWadOffset-data.Subfiles()[1].Offset)))+0x1C)==endpoint.Resolve().DestinationLevelId,"unique arrival token retained");
            check(I(data.CaptureRow(owner.Level.Key,endpoint.Resolve().LetteringTrueIndex-source.SourceMobyIndexBias).Properties,0)==(destination/10-1)*6+destination%10,"visible lettering selector changed");
        }
        int[] targets=[10,20,30,40,50,60,15,25,35,45,55,11,14,21,33];
        var all=PortalRouteDocument.Empty;
        for(int n=0;n<HomeworldPortalControlCatalog.All.Count;n++)
        {
            var def=HomeworldPortalControlCatalog.All[n];all=all.WithDestination(new(def.HomeworldKey,def.PathTrueIndex),targets[n%targets.Length],false);
        }
        var matrix=Compose(all);check(matrix.exe.SequenceEqual(library.Disc.Exe),"entry-only edits preserve executable");
        check(matrix.changes.Count==6,"all29 portal edits affect exactly six homeworld data entries");
        foreach(var edit in all.Destinations)VerifyDestination(matrix.changes,edit.Portal,edit.DestinationLevelId);
        foreach(var (entry,bytes) in matrix.changes)
        {
            var level=catalog.Levels.Single(l=>l.SourceWadEntry==entry);var source=PortalSourceDataLocator.Locate(original,level);
            var data=new UniversalLevelData(library.Disc.EntryBytes(entry));HashSet<int> owned=[];
            foreach(var edit in all.Destinations.Where(d=>d.Portal.HomeworldKey==level.Key))
            {
                var portal=source.Portals.Single(p=>p.SourcePathMobyTrueIndex==edit.Portal.PathTrueIndex);
                var surface=source.SpecialSurfaces.Single(p=>p.Type==6&&p.PortalIndex==portal.Index);
                int descriptor=checked((int)(surface.WadOffset-source.LevelDataWadOffset))+data.Subfiles()[1].Offset+4;
                int physical=edit.Portal.Resolve().LetteringTrueIndex-source.SourceMobyIndexBias;
                int props=data.U32(data.Subfiles()[3].Offset+data.Scene().RowsOffset+physical*UniversalLevelData.RowStride)+data.Subfiles()[3].Offset;
                for(int i=0;i<4;i++){owned.Add(descriptor+i);owned.Add(props+i);}
            }
            check(bytes.Length==data.Bytes.Length&&Enumerable.Range(0,bytes.Length).All(p=>owned.Contains(p)||bytes[p]==data.Bytes[p]),"all29 entries change only descriptor and lettering bytes");
        }
        foreach(int id in Enumerable.Range(10,55).Where(PortalRouteDocument.IsLevelId))
        {
            var result=Compose(PortalRouteDocument.Empty.WithDestination(stone,id,false));
            VerifyDestination(result.changes,stone,id);
        }
        foreach(var level in catalog.Levels.Where(l=>PortalRouteDocument.IsLevelId(l.LevelId)&&l.LevelId!=65))
        {
            var route=PortalRouteDocument.Empty.WithReturn(level.LevelId,stone);
            (byte[] exe,Dictionary<int,byte[]> changes) result;
            try{result=Compose(route);}catch(InvalidDataException ex) when(level.Key=="twilightharbor"&&ex.Message.Contains("needs 400 more bytes"))
            {
                var native=library.Get(level.Key);var wrapped=NativePortalRouteRuntime.AppendReturnWrapper(native.Overlay.Bytes.ToArray(),native.Overlay.Update,native.Overlay.CopyBuffer,10,11);
                check(NativePortalRouteRuntime.PatchReturnHomeCalls(library,wrapped.Overlay,wrapped.ReturnStubPointer)==1,"TwilightHarbor class9 packet hook certified despite export memory refusal");
                reports.Add(new{level.Key,level.LevelId,ExportRefused=ex.Message,StaticHookCount=1});continue;
            }
            var bindings=UniversalExeBindings.Read(result.exe,level.LevelId);byte[] overlay=result.changes[level.SourceWadEntry-1];
            uint update=bindings[UniversalExeBindings.UpdateGlobal].Value;int offset=checked((int)(update-UniversalOverlay.Base));
            check(U(overlay,offset+16)==NativePortalRouteRuntime.Tag,"edited return callback tag");
            check(I(overlay,offset+20)==10&&I(overlay,offset+24)==11,"selected home and unique portal token");
            uint stub=update+28;int patched=0;byte[] source=library.Disc.EntryBytes(level.SourceWadEntry-1);
            for(int p=0;p+4<=source.Length;p+=4)
                if(U(source,p)==UniversalMips.EncodeJ(3,NativePortalRouteRuntime.TransitionAddress)&&U(overlay,p)==UniversalMips.EncodeJ(3,stub))patched++;
            bool ordinary=level.LevelId%10 is >=1 and <=4&&level.LevelId!=63;
            HashSet<int> hookBytes=[];
            for(int p=0;p+4<=source.Length;p+=4)if(U(source,p)==UniversalMips.EncodeJ(3,NativePortalRouteRuntime.TransitionAddress)&&U(overlay,p)==UniversalMips.EncodeJ(3,stub))for(int i=0;i<4;i++)hookBytes.Add(p+i);
            var oldBindings=UniversalExeBindings.Read(library.Disc.Exe,level.LevelId);
            int originalUsed=checked((int)(oldBindings[UniversalExeBindings.CopyBufferGlobal].Value-UniversalOverlay.Base));
            check(Enumerable.Range(0,originalUsed).All(p=>hookBytes.Contains(p)||source[p]==overlay[p]),"original bound overlay bytes preserved except exact ReturnHome JAL owner");
            check(bindings.Where(b=>b.Key!=UniversalExeBindings.UpdateGlobal&&b.Key!=UniversalExeBindings.CopyBufferGlobal).All(b=>b.Value.Value==oldBindings[b.Key].Value),"unrelated native callbacks preserved");
            check(patched==(ordinary?1:0),"exact native class9 hook count, including all23 variants");
            reports.Add(new{level.Key,level.LevelId,update,stub,patched,SourceOverlayHash=Hash(source),FinalOverlayHash=Hash(overlay),CopyBuffer=bindings[UniversalExeBindings.CopyBufferGlobal].Value});
        }
        void Refuse(PortalRouteDocument route,string label)
        {
            byte[] exe=library.Disc.Exe.ToArray();Dictionary<int,byte[]> changes=[];bool refused=false;
            try{NativePortalRouteComposer.Apply(library,catalog,exe,changes,library.Disc.EntryBytes,route);}catch(InvalidDataException){refused=true;}
            check(refused,label);check(changes.Count==0&&exe.SequenceEqual(library.Disc.Exe),"failed composition leaves private inputs unchanged");
        }
        Refuse(PortalRouteDocument.Empty.WithDestination(stone,65,true),"missing authored65 refuses");
        File.WriteAllText(Path.Combine(directory,"export-matrix.json"),JsonSerializer.Serialize(new{Entries=all.Destinations,Returns=reports,RuntimeTested=false},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("PORTAL_ROUTE_EXPORT_MATRIX_OK");
        if(!writeFixtures)return;

        async Task<string> Move(string input,string key,int[] rows,int dx,int dy,int dz,string label)
        {
            var level=catalog.FindByKey(key)!;string cache=Path.Combine(directory,label+"-cache.json"),edits=Path.Combine(directory,key+"-native-edits.json");
            await SourceMobyCacheBuilder.BuildAsync(original,level,cache);
            var mobys=MobyLoader.LoadCached(cache).ToList();
            foreach(int index in rows){var m=mobys.Single(m=>m.TrueIndex==index);m.Position=new(m.Position.X+dx/16f,m.Position.Y+dy/16f,m.Position.Z+dz/16f);}
            check(await MobyEditStore.SaveAsync(edits,mobys,level.DisplayName)==rows.Length,"normal saved movement row count");
            var reopened=MobyLoader.LoadCached(cache).ToList();check(MobyEditStore.Load(edits,reopened)==rows.Length,"normal movement survives reopen");
            var result=await MobySourcePatchExporter.ExportAsync(new(input,Path.ChangeExtension(input,".cue"),Path.Combine(directory,label),level,edits,true));
            check(result.WroteImage&&result.Plan.SkippedEdits.Count==0,"normal production movement exported without skipped edits");
            return result.OutputImagePath;
        }
        string movedArtisans=await Move(original,"artisans",stone.Resolve().TrueIndexes.ToArray(),256,0,0,"moved-stone");
        var stoneSource=PortalSourceDataLocator.Locate(original,catalog.FindByKey("artisans")!);
        var stoneMoved=PortalSourceDataLocator.Locate(movedArtisans,catalog.FindByKey("artisans")!);
        var a=stoneSource.Portals.Single(p=>p.SourcePathMobyTrueIndex==38);var b=stoneMoved.Portals.Single(p=>p.SourcePathMobyTrueIndex==38);
        check(b.CenterX==a.CenterX+256&&b.CenterY==a.CenterY&&b.CenterZ==a.CenterZ,"normal export moved actual doorway center");
        check(b.Path.Nodes.Zip(a.Path.Nodes).All(n=>n.First.X==n.Second.X+256&&n.First.Y==n.Second.Y&&n.First.Z==n.Second.Z),"normal export moved both return fly-out nodes");
        string moved=await Move(movedArtisans,"toasty",[52,56],23572,-2939,-3584,"moved-toasty-return");
        var toastyFloor=PortalSourceDataLocator.LocateTerrainSurfaces(original,catalog.FindByKey("toasty")!).CollisionSurfaceTriangles.Single(t=>t.TriangleIndex==12125);
        double px=129239/16.0,py=114145/16.0;
        double den=(toastyFloor.P2.Y-toastyFloor.P3.Y)*(toastyFloor.P1.X-toastyFloor.P3.X)+(toastyFloor.P3.X-toastyFloor.P2.X)*(toastyFloor.P1.Y-toastyFloor.P3.Y);
        double baryA=((toastyFloor.P2.Y-toastyFloor.P3.Y)*(px-toastyFloor.P3.X)+(toastyFloor.P3.X-toastyFloor.P2.X)*(py-toastyFloor.P3.Y))/den;
        double baryB=((toastyFloor.P3.Y-toastyFloor.P1.Y)*(px-toastyFloor.P3.X)+(toastyFloor.P1.X-toastyFloor.P3.X)*(py-toastyFloor.P3.Y))/den;
        check(baryA>=0&&baryB>=0&&1-baryA-baryB>=0&&15837-990*16==-3,"moved ReturnHome is inside proven floor with original -3raw offset");
        check(toastyFloor.FlagByte==255&&toastyFloor.P1.Z==990&&toastyFloor.P2.Z==990&&toastyFloor.P3.Z==990,"ordinary native Toasty floor proof");
        var fixtures=new List<object>();
        foreach(var (name,route) in new[]{("stone-to-toasty",PortalRouteDocument.Empty.WithDestination(stone,14,true)),("duplicate-toasty-return-stone",PortalRouteDocument.Empty.WithDestination(stone,14,true).WithDestination(town,14,false)),("stone-to-wizard",PortalRouteDocument.Empty.WithDestination(stone,33,true)),("stone-to-sunny-flight",PortalRouteDocument.Empty.WithDestination(stone,15,true))})
        {
            string output=Path.Combine(directory,name+".bin");NativePortalRouteComposer.ApplyImage(library,catalog,moved,output,route);
            File.WriteAllText(Path.ChangeExtension(output,".cue"),"FILE \""+Path.GetFileName(output)+"\" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n");
            var read=UniversalDiscImage.Read(output);var baseDisc=UniversalDiscImage.Read(moved);var composedExe=baseDisc.Exe.ToArray();Dictionary<int,byte[]> expected=[];
            NativePortalRouteComposer.Apply(library,catalog,composedExe,expected,baseDisc.EntryBytes,route);
            check(read.Exe.SequenceEqual(composedExe),"independent fixture executable readback");
            for(int entry=0;entry<256;entry++)check(read.EntryBytes(entry).SequenceEqual(expected.GetValueOrDefault(entry)??baseDisc.EntryBytes(entry)),"all256 fixture entries match staged input or exact routing result");
            var rt=PortalSourceDataLocator.Locate(output,catalog.FindByKey("artisans")!);var doorway=rt.Portals.Single(p=>p.SourcePathMobyTrueIndex==38);
            check(doorway.DestinationLevelId==11&&rt.SpecialSurfaces.Single(s=>s.Type==6&&s.PortalIndex==doorway.Index).DestinationLevelId==route.DestinationFor(stone),"moved fixture retains endpoint token with changed entry destination");
            var td=new UniversalLevelData(read.EntryBytes(catalog.FindByKey("toasty")!.SourceWadEntry));var row=td.Row(52);
            check(I(row,12)==129239&&I(row,16)==114145&&I(row,20)==15837,"grounded Toasty return row readback");
            using var stream=File.OpenRead(output);fixtures.Add(new{Name=name,ImagePath=output,CuePath=Path.ChangeExtension(output,".cue"),ImageSha256=Convert.ToHexStringLower(SHA256.HashData(stream)),Routes=route,
                MovedStoneCenter=new[]{doorway.CenterX,doorway.CenterY,doorway.CenterZ},MovedStonePath=doorway.Path.Nodes,ToastyReturn=new[]{129239,114145,15837},Floor=toastyFloor,RuntimeTested=false});
            Console.WriteLine("PORTAL_ROUTE_FIXTURE_READY "+output);
        }
        File.WriteAllText(Path.Combine(directory,"fixtures.json"),JsonSerializer.Serialize(new{Source=original,SourceHash=UniversalSourceLibrary.OriginalUsaSha256,
            CoreModuleVersionId=typeof(NativePortalRouteComposer).Module.ModuleVersionId,CoreModuleHash=Hash(File.ReadAllBytes(typeof(NativePortalRouteComposer).Assembly.Location)),Fixtures=fixtures,
            Limitation="Toasty class011E ambient companion private three-node path is retained by the ordinary exporter. Its use as Spyro outgoing flight is not inferred. Homeworld return fly-out nodes move through the standard portal movement exporter.",RuntimeTested=false},new JsonSerializerOptions{WriteIndented=true}));
    }
}
