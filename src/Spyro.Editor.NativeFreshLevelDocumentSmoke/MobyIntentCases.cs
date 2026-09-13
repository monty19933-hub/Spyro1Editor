using System.Reflection;
using Spyro.Editor.Core.Editing;
using static Cases;

internal static class MobyIntentCases
{
    internal static NativeFreshRetailMobyReference Source(int entry=10,int index=121,int nativeClass=0x01F5)=>
        NativeFreshRetailMobyReference.Create(MaterialIntentCases.Variant,entry,index,nativeClass);
    internal static NativeFreshMobyPlacementIntent Moby(string id=Id2,int x=8705,int y=8961,int z=65535,int yaw=64,
        NativeFreshRetailMobyReference? source=null)=>NativeFreshMobyPlacementIntent.Create(id,source??Source(),x,y,z,yaw);
    internal static NativeFreshLevelDocumentIntent Value(bool materials=false)
    {
        var old=materials?MaterialCodecCases.Value():Document(Wizard());
        var upgraded=old.UpgradeForNativeMobys();
        return upgraded.WithContent(upgraded.Base,[old.Placements[0],Moby()]);
    }

    internal static void Run()
    {
        foreach(Type type in new[]{typeof(NativeFreshRetailMobyReference),typeof(NativeFreshMobyPlacementIntent)})
        {
            Check(type.IsSealed&&type.GetConstructors().Length==0&&type.GetProperties().All(p=>p.SetMethod==null),
                "source reference and native placement are closed immutable neutral values");
            Check(type.GetMethods(BindingFlags.Public|BindingFlags.Static|BindingFlags.DeclaredOnly).All(m=>m.Name=="Create"),
                "neutral Moby factories do not capture source or issue native approval");
        }
        Check(typeof(NativeFreshRetailMobyReference).GetProperties().Select(p=>p.Name).Order().SequenceEqual(
            new[]{"ExpectedNativeClass","SourceTrueIndex","SourceVariant","SourceWadEntry"}),
            "reference has only intended retail row identity, no paths/hashes/payloads/capability flags");
        Check(typeof(NativeFreshMobyPlacementIntent).GetProperties().Select(p=>p.Name).Order().SequenceEqual(
            new[]{"EditorId","NativeYawByte","Source","Tag","X","Y","Z"}),"generic placement contains only identity/source/transform");
        var source=Source();var moby=Moby(source:source);
        Check(moby.Tag=="native-moby"&&ReferenceEquals(moby.Source,source)&&moby.X==8705&&moby.Y==8961&&
            moby.Z==65535&&moby.NativeYawByte==64,"actual neutral reference retained with raw non-grid position and explicit native yaw");
        foreach(int entry in Enumerable.Range(0,35).Select(i=>10+i*2))
            foreach(var row in new[]{(0,0),(4095,65535)})
            {
                var generic=Source(entry,row.Item1,row.Item2);
                Check(generic.SourceWadEntry==entry&&generic.SourceTrueIndex==row.Item1&&generic.ExpectedNativeClass==row.Item2,
                    "neutral ingestion domain is not restricted to Grass or actual inventory availability");
            }
        foreach(string? variant in new string?[]{null,"","unknown",MaterialIntentCases.Variant+" ","SPYRO1-USA-RETAIL-MODE2-2352-V1"})
            Reject(()=>NativeFreshRetailMobyReference.Create(variant!,10,121,501),"exact supported original variant");
        foreach(int entry in new[]{int.MinValue,-2,0,8,11,77,80,int.MaxValue})Reject(()=>Source(entry),"even10..78 source entry");
        foreach(int index in new[]{int.MinValue,-1,4096,int.MaxValue})Reject(()=>Source(index:index),"0..4095 ingestion index, not capacity");
        foreach(int nativeClass in new[]{int.MinValue,-1,65536,int.MaxValue})Reject(()=>Source(nativeClass:nativeClass),"unsigned16 expected native class");
        Reject(()=>NativeFreshMobyPlacementIntent.Create(Id2,null!,8705,8961,0,0),"missing source reference");
        foreach(int z in new[]{int.MinValue,-1,65536,int.MaxValue})Reject(()=>Moby(z:z),"neutral authored Z0..FFFF");
        foreach(int yaw in new[]{int.MinValue,-1,256,int.MaxValue})Reject(()=>Moby(yaw:yaw),"yaw byte is checked, never clamped/wrapped");
        foreach(string? id in new string?[]{null,"",Id2[..31],new string('0',32),Id.ToUpperInvariant()})Reject(()=>Moby(id!),"shared exact stable editor ID grammar");
        var one=Document(Wizard());var two=MaterialCodecCases.Value();
        foreach(var old in new[]{one,two})
        {
            string before=Serialize(old);var upgraded=old.UpgradeForNativeMobys();
            Check(upgraded.SchemaVersion==3&&ReferenceEquals(upgraded.Base,old.Base)&&ReferenceEquals(upgraded.FloorMaterials,old.FloorMaterials)&&
                upgraded.Placements.SequenceEqual(old.Placements),"explicit schema3 upgrade preserves floor/material/ordered placement intent");
            Check(upgraded.UpgradeForNativeMobys().SchemaVersion==3&&Serialize(old)==before,"upgrade is idempotent and never changes old document bytes");
            Reject(()=>old.WithContent(old.Base,[moby]),"WithContent never silently upgrades schema1/2");
            var mixed=upgraded.WithContent(upgraded.Base,[moby,Wizard()]);
            Check(mixed.SchemaVersion==3&&mixed.Placements.Select(p=>p.EditorId).SequenceEqual(new[]{Id2,Id}),"mixed union retains caller order");
            var removed=mixed.WithContent(mixed.Base,[Wizard()]);
            Check(removed.SchemaVersion==3&&ReferenceEquals(removed.FloorMaterials,old.FloorMaterials)&&
                removed.WithContent(removed.Base,[]).SchemaVersion==3,"removing final native/all placements never downgrades schema3");
            Check(mixed.WithFloorMaterials(MaterialIntentCases.Pair()).SchemaVersion==3&&
                mixed.WithFloorMaterials(NativeFreshFloorMaterialsIntent.Create(MaterialIntentCases.Color(),MaterialIntentCases.Color())).SchemaVersion==3,
                "material edits and explicit all-color clear retain schema3");
        }
        Reject(()=>NativeFreshLevelDocumentIntent.Create(Base(),[moby]),"old Create remains schema1 only");
        var value=Value();var mutable=new List<NativeFreshPlacementIntent>{moby,Wizard()};
        var copy=value.WithContent(value.Base,mutable);mutable.Clear();
        Check(copy.Placements.Count==2&&copy.Placements is not NativeFreshPlacementIntent[],"mixed collection does not alias caller array/list");
        if(copy.Placements is IList<NativeFreshPlacementIntent> list)
        {bool refused=false;try{list[0]=Wizard();}catch(NotSupportedException){refused=true;}Check(refused,"mixed placement collection cannot be mutated through cast");}
        foreach(var point in new[]{(8448,8961),(9472,8961),(8705,8448),(8705,9472),(int.MinValue,8961),(8705,int.MaxValue)})
            Reject(()=>value.WithContent(value.Base,[Moby(x:point.Item1,y:point.Item2)]),"native XY strictly inside actual current floor");
        foreach(var point in new[]{(8449,8449),(9471,9471)})
            Check(value.WithContent(value.Base,[Moby(x:point.Item1,y:point.Item2,z:0,yaw:255)]).Placements.Count==1,
                "nearest integer interior raw coordinates need no16-unit grid or Wizard route");
        Reject(()=>value.WithContent(value.Base,[Moby(id:Id),Wizard()]),"IDs unique across placement variants");
        Reject(()=>value.WithContent(value.Base,[null!]),"null still not a placement variant");
        var many=value.WithContent(value.Base,Enumerable.Range(1,128).Select(i=>
            i%2==0?(NativeFreshPlacementIntent)Moby(i.ToString("x32")):Wizard(i.ToString("x32"))));
        Check(many.Placements.Count==128,"shared128 authoring cap includes both variants");
        int visited=0;bool disposed=false;
        IEnumerable<NativeFreshPlacementIntent> TooMany()
        {try{for(int i=1;i<=130;i++){visited++;yield return Moby(i.ToString("x32"));}}finally{disposed=true;}}
        Reject(()=>value.WithContent(value.Base,TooMany()),"bounded generic placement enumeration");
        Check(visited==129&&disposed,"generic enumeration stops/disposes on129th without full consumption");
        Check(Serialize(one)==Canonical&&Serialize(two)==MaterialCodecCases.Golden,"both old schemas remain literal byte-exact");
    }
}
