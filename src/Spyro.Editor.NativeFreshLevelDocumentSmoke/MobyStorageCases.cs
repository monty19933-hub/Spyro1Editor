using System.Text;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Persistence;
using static Cases;

// Tiny neutral authoring owners only, through the existing Store/read context.
internal static class MobyStorageCases
{
    internal static void Run()
    {
        string root=Directory.CreateTempSubdirectory("spyro-moby-storage-").FullName;bool passed=false;
        try
        {
            foreach(bool materials in new[]{false,true})
            {
                string id=materials?"with-materials":"without-materials",path=Path.Combine(root,id);Directory.CreateDirectory(path);
                var project=new EditorProjectLayout(path,id);
                File.WriteAllText(project.ManifestPath,"{\"schemaVersion\":1,\"projectId\":\""+id+"\",\"unrelated\":\"preserve\"}\n");
                byte[] manifest=File.ReadAllBytes(project.ManifestPath);
                var legacy=NativeId65FreshDocumentStore.Save(NativeId65FreshDocumentStore.Capture(project),Base(20));
                byte[] legacyBytes=File.ReadAllBytes(legacy.Path);
                var old=materials?MaterialCodecCases.Value():Document(Wizard());
                var savedOld=NativeFreshLevelDocumentStore.Save(NativeFreshLevelDocumentStore.Capture(project),old);
                byte[] oldBytes=File.ReadAllBytes(savedOld.Path);
                var value=MobyIntentCases.Value(materials);
                byte[] expected=Encoding.UTF8.GetBytes(materials?MobyCodecCases.MaterialGolden:MobyCodecCases.Golden);
                var saved=NativeFreshLevelDocumentStore.Save(savedOld,value);
                Check(saved.Path==savedOld.Path&&File.ReadAllBytes(saved.Path).SequenceEqual(expected)&&saved.Intent!.SchemaVersion==3&&
                    !ReferenceEquals(saved.Intent,value)&&saved.Intent.Placements[1] is NativeFreshMobyPlacementIntent,
                    "schema3 uses unchanged filename/Store and actual independently parsed return");
                var context=NativeFreshSavedLevelDocumentReadContext.Capture(saved);context.RequireDocument(saved.Intent!);
                Check(ReferenceEquals(context.Document,saved.Intent),"existing neutral read context retains actual schema3 Snapshot intent");
                var guard=context.AcquireFinalReadGuard();
                try{Throws<IOException>(()=>NativeFreshLevelDocumentStore.Save(saved,value),"actual schema3 final guard excludes cooperating Save");}
                finally{guard.Complete();}
                DateTime stamp=new(2001,1,1,0,0,0,DateTimeKind.Utc);File.SetLastWriteTimeUtc(saved.Path,stamp);
                NativeFreshLevelDocumentStore.SaveForSmoke(saved,value,_=>throw new Exception("No-op must not stage"));
                Check(File.GetLastWriteTimeUtc(saved.Path)==stamp,"schema3 canonical no-op keeps existing Store timing");
                foreach(string external in new[]{Encoding.UTF8.GetString(expected)+" \n",
                    Encoding.UTF8.GetString(expected).Replace("\"sourceTrueIndex\":121","\"sourceTrueIndex\":4095"),
                    Encoding.UTF8.GetString(expected).Replace("\"expectedNativeClass\":501","\"expectedNativeClass\":65535"),
                    Encoding.UTF8.GetString(expected).Replace("\"x\":8705","\"x\":8706"),
                    Encoding.UTF8.GetString(expected).Replace("\"nativeYawByte\":64","\"nativeYawByte\":65")})
                {
                    File.WriteAllText(saved.Path,external);
                    Throws<InvalidDataException>(()=>NativeFreshLevelDocumentStore.Save(saved,value),"raw source/transform/whitespace conflict");
                    Check(File.ReadAllText(saved.Path)==external,"stale schema3 Save retains exact external bytes");
                    File.WriteAllBytes(saved.Path,expected);
                }
                File.WriteAllText(saved.Path,Encoding.UTF8.GetString(expected).Replace("\"nativeYawByte\":64","\"nativeYawByte\":256"));
                Throws<InvalidDataException>(()=>NativeFreshLevelDocumentStore.Capture(project),"invalid saved native transform cannot be adopted");
                File.WriteAllBytes(saved.Path,expected);
                var changed=value.WithContent(value.Base,[Wizard(),MobyIntentCases.Moby(yaw:255)]);
                using(var cancel=new CancellationTokenSource())
                {
                    Throws<OperationCanceledException>(()=>NativeFreshLevelDocumentStore.SaveForSmoke(saved,changed,
                        phase=>{if(phase=="staged")cancel.Cancel();},cancel.Token),"schema3 actual precommit cancellation");
                    Check(File.ReadAllBytes(saved.Path).SequenceEqual(expected),"precommit cancellation retains prior schema3 bytes");
                }
                using(var late=new CancellationTokenSource())
                {
                    saved=NativeFreshLevelDocumentStore.SaveForSmoke(saved,changed,phase=>{if(phase=="published")late.Cancel();},late.Token);
                    Check(late.IsCancellationRequested&&saved.Intent!.SchemaVersion==3&&
                        ((NativeFreshMobyPlacementIntent)saved.Intent.Placements[1]).NativeYawByte==255,
                        "committed schema3 save verifies actual transform despite late cancellation");
                }
                var removed=saved.Intent!.WithContent(saved.Intent.Base,[saved.Intent.Placements[0]]);
                saved=NativeFreshLevelDocumentStore.Save(saved,removed);
                Check(NativeFreshLevelDocumentStore.Capture(project).Intent!.SchemaVersion==3&&
                    (saved.Intent!.FloorMaterials!=null)==materials,"last native removal survives disk roundtrip without downgrade/material inference");
                Check(File.ReadAllBytes(legacy.Path).SequenceEqual(legacyBytes)&&File.ReadAllBytes(project.ManifestPath).SequenceEqual(manifest)&&
                    oldBytes.SequenceEqual(Encoding.UTF8.GetBytes(materials?MaterialCodecCases.Golden:Canonical)),
                    "old schema1/2 original fixture bytes and legacy/manifest owners preserved");
            }
            passed=true;
        }
        finally
        {
            if(passed)Directory.Delete(root,recursive:true); // Only this newly owned tiny fixture tree.
            else Console.WriteLine("MOBY_STORAGE_FAILURE_RETAINED path="+root);
        }
    }
    private static T Throws<T>(Action action,string label)where T:Exception
    {try{action();}catch(T error){Check(true,label);return error;}throw new InvalidDataException("Expected "+typeof(T).Name+": "+label);}
}
