using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Universal;

static class ApproachVerification
{
    public static async Task<string> Run(string input,string directory,LevelCatalog catalog,Action<bool,string> check)
    {
        input=Path.GetFullPath(input);var level=catalog.FindByKey("artisans")!;
        var landing=FlyInLandingLocator.Locate(input,level);var marker=FlyInLandingEditorControl.Create(level,landing);
        int[] spawn=[99328,51948,9257];marker.Position=new Vector3f(spawn[0]/16f,spawn[1]/16f,spawn[2]/16f);
        string edits=Path.Combine(directory,"artisans-native-edits.json");
        check(await MobyEditStore.SaveAsync(edits,[marker],level.DisplayName)==1,"one ordinary source spawn edit saved");
        var reopened=FlyInLandingEditorControl.Create(level,landing);
        check(MobyEditStore.Load(edits,new List<Moby>{reopened})==1&&reopened.Position==marker.Position,"source spawn edit survives reopen");
        var result=await MobySourcePatchExporter.ExportAsync(new(input,Path.ChangeExtension(input,".cue"),Path.Combine(directory,"near-stone-portal"),level,edits,true));
        check(result.WroteImage&&result.Plan.SkippedEdits.Count==0&&result.Plan.PatchCount==3,"normal production exporter changes exactly three source spawn axes");
        var before=UniversalDiscImage.Read(input);var after=UniversalDiscImage.Read(result.OutputImagePath);
        check(before.Exe.SequenceEqual(after.Exe),"convenience fixture retains the entire routed executable");
        for(int entry=0;entry<256;entry++)
        {
            byte[] a=before.EntryBytes(entry),b=after.EntryBytes(entry);
            if(entry!=level.SourceWadEntry)check(a.SequenceEqual(b),"convenience fixture preserves every other WAD entry");
            else
            {
                int scene=new UniversalLevelData(a).Subfiles()[3].Offset;
                check(a.Length==b.Length&&Enumerable.Range(0,a.Length).All(p=>p>=scene&&p<scene+12||a[p]==b[p]),"convenience fixture changes only the owned 12 spawn bytes");
                check(Enumerable.Range(0,3).All(i=>BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(scene+i*4))==spawn[i]),"exact requested native approach node readback");
            }
        }
        var actual=FlyInLandingLocator.Locate(result.OutputImagePath,level);
        check(actual.RawX==spawn[0]&&actual.RawY==spawn[1]&&actual.RawZ==spawn[2]&&actual.YawByte==landing.YawByte,"source spawn and original heading independently decoded");
        var portalsBefore=PortalSourceDataLocator.Locate(input,level);var portalsAfter=PortalSourceDataLocator.Locate(result.OutputImagePath,level);
        var oldStone=portalsBefore.Portals.Single(p=>p.SourcePathMobyTrueIndex==38);var newStone=portalsAfter.Portals.Single(p=>p.SourcePathMobyTrueIndex==38);
        check(JsonSerializer.Serialize(oldStone)==JsonSerializer.Serialize(newStone),"moved Stone plane and native arrival path retained exactly");
        var floor=PortalSourceDataLocator.LocateTerrainSurfaces(input,level).CollisionSurfaceTriangles.Single(t=>t.TriangleIndex==958);
        double x=spawn[0]/16.0,y=spawn[1]/16.0;
        double den=(floor.P2.Y-floor.P3.Y)*(floor.P1.X-floor.P3.X)+(floor.P3.X-floor.P2.X)*(floor.P1.Y-floor.P3.Y);
        double aWeight=((floor.P2.Y-floor.P3.Y)*(x-floor.P3.X)+(floor.P3.X-floor.P2.X)*(y-floor.P3.Y))/den;
        double bWeight=((floor.P3.Y-floor.P1.Y)*(x-floor.P3.X)+(floor.P1.X-floor.P3.X)*(y-floor.P3.Y))/den;
        double floorRaw=(floor.P1.Z*aWeight+floor.P2.Z*bWeight+floor.P3.Z*(1-aWeight-bWeight))*16;
        check(aWeight>=0&&bWeight>=0&&1-aWeight-bWeight>=0&&floor.FlagByte==255&&spawn[2]>floorRaw,"requested approach node has ordinary floor below it");
        string Digest(string path){using var stream=File.OpenRead(path);return Convert.ToHexStringLower(SHA256.HashData(stream));}
        var receipt=new{Scope="normal source spawn edit only; routed portal handlers retained",InputImage=input,InputImageSha256=Digest(input),
            OutputImage=result.OutputImagePath,OutputCue=result.OutputCuePath,OutputImageSha256=Digest(result.OutputImagePath),OutputCueSha256=Digest(result.OutputCuePath),
            CoreModuleVersionId=typeof(MobySourcePatchExporter).Module.ModuleVersionId,CoreModuleHash=Digest(typeof(MobySourcePatchExporter).Assembly.Location),
            OriginalSpawn=new[]{landing.RawX,landing.RawY,landing.RawZ},ChangedSpawn=spawn,OriginalHeading=landing.YawByte,ChangedHeading=actual.YawByte,
            SourceSpawnWadOffset=landing.WadOffset,Floor=floor,InterpolatedFloorRawZ=floorRaw,InitialHeightAboveFloorWorld=(spawn[2]-floorRaw)/16,
            Stone=newStone,PortalDestination=portalsAfter.SpecialSurfaces.Single(s=>s.Type==6&&s.PortalIndex==newStone.Index).DestinationLevelId,
            RuntimeTested=false};
        File.WriteAllText(Path.Combine(directory,"approach-fixture.json"),JsonSerializer.Serialize(receipt,new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("PORTAL_APPROACH_FIXTURE_READY "+result.OutputCuePath);
        return result.OutputImagePath;
    }
}
