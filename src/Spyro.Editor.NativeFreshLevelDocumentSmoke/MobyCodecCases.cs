using System.Text;
using Spyro.Editor.Core.Editing;
using static Cases;

internal static class MobyCodecCases
{
    internal const string SourceJson="{\"sourceVariant\":\"spyro1-usa-retail-mode2-2352-v1\",\"sourceWadEntry\":10,\"sourceTrueIndex\":121,\"expectedNativeClass\":501}";
    internal const string PositionJson="{\"x\":8705,\"y\":8961,\"z\":65535}";
    internal const string MobyJson="{\"id\":\"00000000000000000000000000000001\",\"tag\":\"native-moby\",\"source\":"+SourceJson+",\"position\":"+PositionJson+",\"nativeYawByte\":64}";
    internal const string Golden="{\"kind\":\"spyro-native-fresh-level\",\"schema\":3,\"base\":"+BaseJson+",\"placements\":["+PlacementJson+","+MobyJson+"],\"floorMaterials\":null}";
    internal const string MaterialGolden="{\"kind\":\"spyro-native-fresh-level\",\"schema\":3,\"base\":"+BaseJson+",\"placements\":["+PlacementJson+","+MobyJson+"],\"floorMaterials\":"+MaterialCodecCases.PairJson+"}";
    internal static void Run()
    {
        Check(Serialize(MobyIntentCases.Value())==Golden&&Serialize(MobyIntentCases.Value(true))==MaterialGolden,
            "literal schema3 canonical order with explicit null/material pair; no codec-generated expected fixture");
        foreach(string text in new[]{Golden,MaterialGolden})
        {
            var parsed=Parse(text);var native=(NativeFreshMobyPlacementIntent)parsed.Placements[1];
            Check(parsed.SchemaVersion==3&&native.Source.SourceWadEntry==10&&native.Source.SourceTrueIndex==121&&
                native.Source.ExpectedNativeClass==501&&native.X==8705&&native.Y==8961&&native.Z==65535&&native.NativeYawByte==64&&
                Serialize(parsed)==text,"mixed semantic roundtrip without world-coordinate rounding, source resolution or yaw reinterpretation");
            Check(parsed.WithContent(parsed.Base,[parsed.Placements[0]]).SchemaVersion==3,"parsed retained version survives last-native removal");
        }
        Check(Serialize(Parse(Canonical))==Canonical&&Serialize(Parse(MaterialCodecCases.Golden))==MaterialCodecCases.Golden,
            "existing schema1 and schema2 canonical bytes exactly preserved");
        Check(Serialize(Parse(MaterialCodecCases.Golden.Replace("\"schema\":2","\"schema\":3")))==
            MaterialCodecCases.Golden.Replace("\"schema\":2","\"schema\":3"),"explicit schema3 may retain only Wizard routes and an existing pair");
        string reordered=" {\"floorMaterials\":null,\"placements\":["+PlacementJson+","+MobyJson+"] ,\"base\":"+BaseJson+",\"schema\":3,\"kind\":\"spyro-native-fresh-level\"}\r\n";
        Check(Serialize(Parse(reordered))==Golden,"canonical root reordering preserves mixed placement order");
        string general=Golden.Replace(SourceJson,"{\"sourceVariant\":\"spyro1-usa-retail-mode2-2352-v1\",\"sourceWadEntry\":78,\"sourceTrueIndex\":4095,\"expectedNativeClass\":65535}");
        Check(Serialize(Parse(general))==general,"neutral generic references retain unresolved/non-Grass families without inventory lookup");
        foreach(string bad in new[]{Golden.Replace("\"schema\":3","\"schema\":1"),MaterialGolden.Replace("\"schema\":3","\"schema\":2"),
            Golden.Replace("\"schema\":3","\"schema\":4"),Golden.Replace(",\"floorMaterials\":null",""),
            Golden.Replace("\"floorMaterials\":null","\"floorMaterials\":[]"),Golden.Replace("native-moby","Native-moby"),
            Golden.Replace("native-moby","unknown"),Golden.Replace(SourceJson,"null"),Golden.Replace(PositionJson,"[]"),
            Golden.Replace("\"sourceWadEntry\":10","\"sourceWadEntry\":11"),Golden.Replace("\"sourceTrueIndex\":121","\"sourceTrueIndex\":4096"),
            Golden.Replace("\"expectedNativeClass\":501","\"expectedNativeClass\":65536"),Golden.Replace("\"nativeYawByte\":64","\"nativeYawByte\":256"),
            Golden.Replace("\"z\":65535","\"z\":65536"),Golden.Replace("\"x\":8705","\"x\":8448"),Golden.Replace("\"y\":8961","\"y\":9472"),
            Golden.Replace(MaterialIntentCases.Variant,"unknown"),Golden.Replace(Id2,Id)})Reject(()=>Parse(bad),"closed version/tag/reference/transform/ID shape");
        foreach(string owner in new[]{MobyJson,SourceJson,PositionJson})
        {
            int comma=owner.IndexOf(',');string first=owner[1..comma];string key=first[..first.IndexOf(':')];
            foreach(string changed in new[]{owner[..^1]+",\"unknown\":0}","{"+owner[(comma+1)..],"{"+first+","+owner[1..],
                owner.Replace(key+":",key.ToUpperInvariant()+":")})
                Reject(()=>Parse(Golden.Replace(owner,changed)),"each new object independently rejects unknown/missing/duplicate/case fields");
        }
        foreach(string field in new[]{"source","position","nativeYawByte","sourceVariant","sourceWadEntry","sourceTrueIndex","expectedNativeClass"})
        {
            string needle="\""+field+"\":";string escaped="\"\\u"+((int)field[0]).ToString("x4")+field[1..]+"\":";
            Reject(()=>Parse(Golden.Replace(needle,needle+"null,"+escaped)),"escaped duplicate new field");
        }
        foreach(string target in new[]{"\"sourceWadEntry\":10","\"sourceTrueIndex\":121","\"expectedNativeClass\":501",
            "\"x\":8705","\"y\":8961","\"z\":65535","\"nativeYawByte\":64"})
            foreach(string invalid in new[]{"null","true","\"1\"","1.0","1e0","2147483648","-2147483649"})
                Reject(()=>Parse(Golden.Replace(target,target[..(target.IndexOf(':')+1)]+invalid)),"strict source/transform integer grammar");
        foreach(string owner in new[]{MobyJson,SourceJson})
            foreach(string field in new[]{"path","sha256","payload","sourceHandle","nativeOwner","importApproved","runtimeVerified"})
                Reject(()=>Parse(Golden.Replace(owner,owner[..^1]+",\""+field+"\":0}")),"neutral source and placement refuse injected authority");
        Reject(()=>Parse(Golden.Replace("\"nativeYawByte\":64","\"rotation\":64")),"no spawn heading/ambiguous rotation alias");
        byte[] input=Encoding.UTF8.GetBytes(Golden);var retained=NativeFreshLevelDocumentCodec.Parse(input);Array.Fill(input,(byte)0);
        byte[] output=NativeFreshLevelDocumentCodec.SerializeCanonical(retained);Array.Fill(output,(byte)0);
        Check(Serialize(retained)==Golden,"parse and serialize buffers do not alias new source/transform state");
        Check(Serialize(Parse(Golden+new string(' ',65536-Encoding.UTF8.GetByteCount(Golden))))==Golden,"schema3 keeps exact64KiB parser bound");
        Reject(()=>Parse(Golden+new string(' ',65537-Encoding.UTF8.GetByteCount(Golden))),"schema3 over-budget refusal");
        Reject(()=>NativeFreshLevelDocumentCodec.Parse(new byte[]{0xEF,0xBB,0xBF}.Concat(Encoding.UTF8.GetBytes(Golden)).ToArray()),"schema3 no BOM");
        var many=retained.WithContent(retained.Base,Enumerable.Range(1,128).Select(i=>MobyIntentCases.Moby(i.ToString("x32"))));
        Check(Parse(Serialize(many)).Placements.Count==128,"128 generic rows fit unchanged authoring byte/count boundaries");
    }
}
