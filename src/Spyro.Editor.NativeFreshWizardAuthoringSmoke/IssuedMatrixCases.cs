// Explicit-source comparison only; no Candidate or writer invocation.
// The complete literal expectations were derived independently from original.
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Primitives;

internal static class IssuedMatrixCases
{
    private const long Length=661547040;
    private const int Sectors=281270,RawSize=2352;
    private const string Original="fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37";
    private const string Expectations="6172ebc01fa4f6a3cddd007bfdf5b89216da6de40fa61a8922c8802462407794";
    private const string EditorId="11111111111111111111111111111111";

    internal static async Task Source(string originalBin,Action<bool,string> check)
    {
        // Resource name/hook are promoted only with this exact reviewed fixture.
        using var expected=ReadExpected();
        string path=Path.GetFullPath(originalBin);FileStream? held=null;
        List<Exception> errors=[];int rows=0,admitted=0,tracks=0;double completedRowSeconds=0;
        try
        {
            held=Open(path);Authenticate(held);
            var family=await NativeWizardFamilyAssetPlan.CaptureAsync(path);
            foreach(var row in expected.RootElement.GetProperty("cases").EnumerateArray())
            {
                var clock=Stopwatch.StartNew();string name=row.GetProperty("name").GetString()!;
                JsonElement r=row.GetProperty("recipe");int I(string key)=>r.GetProperty(key).GetInt32();
                var floor=NativeId65FreshDocumentIntent.Create(I("floor_x"),I("floor_y"),I("floor_z"),
                    I("width"),I("depth"),I("spawn_x"),I("spawn_y"),I("heading"),
                    Rgb(I("floor_rgb")),Rgb(I("sky_rgb")),I("track"));
                var route=NativeFreshWizardRouteIntent.Create(EditorId,I("origin_x"),I("origin_y"),
                    I("origin_z"),I("end_x"),I("end_y"));
                var document=NativeFreshLevelDocumentIntent.Create(floor,[route]);
                var compilation=await NativeId65FreshDocumentCompilation.CaptureAsync(path,floor);
                bool historical=name=="historical-A-P1";
                var resource=historical
                    ?await NativeId65FreshWizardResearchResourcePlan.CaptureAsync(path,compilation,family)
                    :await NativeId65FreshWizardResearchResourcePlan.CaptureAuthoredAsync(path,compilation,family);
                var scene=historical
                    ?await NativeId65FreshWizardResearchScenePlan.CaptureAsync(path,compilation,family,resource,
                        new(route.OriginX,route.OriginY,route.OriginZ,route.EndX,route.EndY))
                    :await NativeId65FreshWizardResearchScenePlan.CaptureAuthoredAsync(path,document,compilation,family,resource);
                var clearance=await NativeFreshInitialSpawnClearancePlan.CaptureWizardAsync(path,document,scene);
                bool policy=row.GetProperty("assessment").GetProperty("admitted").GetBoolean();
                if(policy){_=clearance.RequireInitialFloorFootprint(document,scene);admitted++;}
                else Refuse(()=>clearance.RequireInitialFloorFootprint(document,scene));
                var equal=NativeFreshLevelDocumentIntent.Create(floor,[route]);
                Refuse(()=>clearance.RequireInitialFloorFootprint(equal,scene),"another actual document/scene");
                var fallback=await NativeId65FreshWizardResearchFallbackResourcePlan.CaptureAsync(path,resource);
                var host=await NativeId65FreshWizardResearchHostPlan.CaptureAsync(path,scene,fallback);
                var body=await NativeId65FreshWizardResearchBodyPlan.CaptureAsync(path,host);
                // Track is deliberately NOT passed here. It comes from this
                // actual row's Body.Host.Scene.Compilation.Intent chain.
                var exe=await NativeId65FreshWizardResearchExecutablePlan.CaptureAsync(path,body);
                var disc=await NativeId65FreshWizardResearchDiscPlan.CaptureAsync(path,exe);
                resource.RequireAncestry(compilation,family);scene.RequireAncestry(compilation,family,resource);
                exe.RequireAncestry(body);disc.RequireAncestry(exe);
                CheckHash(resource.CopyDataComponents().AsSpan(8,0x144),row,"surfaceSha256");
                CheckHash(resource.CopyDataComponents(),row,"dataSha256");
                CheckHash(scene.CopyScene(),row,"sceneSha256");
                byte[] actualBody=disc.CopyBody();CheckHash(actualBody,row,"bodySha256");
                CheckHash(actualBody.AsSpan(0,0x800),row,"headerSha256");
                CheckHash(disc.CopyExecutable(),row,"executableSha256");
                CheckHash(disc.CopyOverlay(),"ad6d952cbf70ab8b1eab53f7637ffd0235271b7b20696956c9406f36f714fa7e");
                check(exe.SelectedBuiltInTrack==I("track"),name+": actual ancestry-selected music.");
                // These are actual Disc outputs, not expected Python bytes.
                // Full independent expected afterimage SHA checks every actual
                // raw byte cryptographically. Native execution is NOT repeated.
                var measured=MeasureActual(held,disc);
                check(measured.Sha==row.GetProperty("imageAfterimageSha256").GetString() &&
                    measured.Changed==row.GetProperty("originalChangedRawSectors").GetInt32(),
                    name+": full actual raw afterimage against independent original-only golden.");
                check(!disc.RuntimeVerified && !disc.PublicationApproved && !disc.NormalCreateBinEnabled &&
                    !clearance.FirstQueryProven && !clearance.TrajectoryProven,
                    name+": source compiler comparison never implies runtime or Candidate issuance.");
                rows++;if(name.StartsWith("music-",StringComparison.Ordinal))tracks++;
                completedRowSeconds+=clock.Elapsed.TotalSeconds;
                Console.WriteLine($"ACTUAL_WIZARD_MATRIX_ROW name={name} elapsedSeconds={clock.Elapsed.TotalSeconds:F3} rawSha={measured.Sha} admitted={policy} candidateIssued=false");
                if(rows==2)Console.WriteLine($"ACTUAL_WIZARD_MATRIX_TWO_ROW_TIMING elapsedSeconds={completedRowSeconds:F3} estimatedRemainingSeconds={completedRowSeconds/2*70:F3} measuredRows=2 remainingRows=70 projectionOnly=true");
            }
            check(rows==72&&tracks==46&&admitted==67,"Complete literal field/track/policy matrix.");
        }
        catch(Exception error){errors.Add(error);}
        finally
        {
            try{if(held!=null)Authenticate(held);}catch(Exception error){errors.Add(error);}
            // Attempt a current-name check even after partial initial failure;
            // assign before Authenticate so a hash failure still closes owner.
            FileStream? current=null;
            try{current=Open(path);Authenticate(current);}catch(Exception error){errors.Add(error);}
            finally{try{current?.Dispose();}catch(Exception error){errors.Add(error);}}
            try{held?.Dispose();}catch(Exception error){errors.Add(error);}
        }
        if(errors.Count==1)ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if(errors.Count>1)throw new IOException("Actual matrix primary and independent original finals/close failures.",new AggregateException(errors));
        Console.WriteLine($"ACTUAL_WIZARD_MATRIX_SOURCE_OK rows={rows} music={tracks} admitted={admitted} sourceFinalAndClosed=true outputWritten=false candidateIssued=false nativeExecutionRepeated=false");
    }

    private static (string Sha,int Changed) MeasureActual(Stream source,NativeId65FreshWizardResearchDiscPlan disc)
    {
        using var original=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var output=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        source.Position=0;byte[] before=new byte[RawSize];int changed=0;
        for(int lba=0;lba<Sectors;lba++)
        {
            source.ReadExactly(before);
            Require(source.Length==Length&&source.Position==(lba+1L)*RawSize,"Exact per-sector source position/length.");
            byte[] actual=disc.CreateExpectedRawSector(lba,before);
            Require(actual.Length==RawSize,"One complete actual forward sector.");
            // Actual production inverse here is a round trip, not claimed an
            // independent algorithm. The full expected SHA is independent.
            Require(disc.CreateOriginalRawSector(lba,actual).AsSpan().SequenceEqual(before),"Actual forward/inverse round trip.");
            original.AppendData(before);output.AppendData(actual);
            if(!actual.AsSpan().SequenceEqual(before))changed++;
            if(lba is 0 or 22)
            {
                byte[] corrupt=before.ToArray();corrupt[24]^=1;
                Refuse(()=>disc.CreateExpectedRawSector(lba,corrupt));
                byte[] wrong=actual.ToArray();wrong[^1]^=1;
                Refuse(()=>disc.CreateOriginalRawSector(lba,wrong));
            }
        }
        Require(source.Position==Length&&source.Length==Length&&source.ReadByte()==-1&&source.Position==Length&&source.Length==Length,
            "Full original stream true EOF after all actual sectors.");
        Require(Convert.ToHexStringLower(original.GetHashAndReset())==Original,"Full actual original hash.");
        return(Convert.ToHexStringLower(output.GetHashAndReset()),changed);
    }

    private static JsonDocument ReadExpected()
    {
        using var stream=typeof(IssuedMatrixCases).Assembly.GetManifestResourceStream("WizardMatrixExpectations")
            ??throw new InvalidDataException("Missing exact embedded independent expectation fixture.");
        Require(stream.Length==149352&&stream.Length<=256*1024,"Bounded literal fixture length.");
        byte[] bytes=new byte[149352];stream.ReadExactly(bytes);
        Require(stream.Position==bytes.Length&&stream.ReadByte()==-1&&stream.Position==bytes.Length,
            "Complete literal fixture EOF.");
        CheckHash(bytes,Expectations);var result=JsonDocument.Parse(bytes);
        Require(result.RootElement.GetProperty("cases").GetArrayLength()==72 &&
            result.RootElement.GetProperty("sourceImageSha256").GetString()==Original &&
            !result.RootElement.GetProperty("actualCSharpIssuerCompared").GetBoolean(),"Frozen original-only expectation metadata.");
        return result;
    }
    private static FileStream Open(string path)
    {
        Require(new FileInfo(path).LinkTarget==null&&!Directory.Exists(path),"Regular nonlinked source path.");
        return new(path,FileMode.Open,FileAccess.Read,FileShare.Read);
    }
    private static void Authenticate(Stream stream)
    {
        Require(stream.CanRead&&stream.CanSeek&&stream.Length==Length,"Whole original length.");stream.Position=0;
        string sha=Convert.ToHexStringLower(SHA256.HashData(stream));
        Require(sha==Original&&stream.Position==Length&&stream.Length==Length&&stream.ReadByte()==-1&&
            stream.Position==Length&&stream.Length==Length,"Whole original SHA/position/true EOF.");
    }
    private static ColorRgba Rgb(int value)=>new((byte)value,(byte)(value>>8),(byte)(value>>16));
    private static void CheckHash(ReadOnlySpan<byte> bytes,JsonElement row,string property)=>CheckHash(bytes,row.GetProperty(property).GetString()!);
    private static void CheckHash(ReadOnlySpan<byte> bytes,string expected)=>Require(Convert.ToHexStringLower(SHA256.HashData(bytes))==expected,"Complete independent byte hash.");
    private static void Require(bool ok,string label){if(!ok)throw new InvalidDataException(label);}
    private static void Refuse(Action action,string? phrase=null)
    {
        try{action();}catch(InvalidDataException error){Require(phrase==null||error.Message.Contains(phrase,StringComparison.Ordinal),"Expected refusal identity.");return;}
        throw new InvalidDataException("Expected bounded refusal.");
    }
}
