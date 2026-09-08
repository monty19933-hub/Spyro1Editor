using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Editing;

// Actual App worker controls; no Window/native UI, fake Source/result, Store or output image.
internal static class NativeFreshMaterialInspectionWorkerCases
{
    private const string Variant = "spyro1-usa-retail-mode2-2352-v1";
    private const string Original = "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37";
    private const string CueSha = "05142502c75d3a0b6374003a8fe41893d6faf0ad55863ff0b62a265a1b152766";
    private const string FreeMode = "--fresh-material-inspection-worker-only";
    private const string SourceMode = "--fresh-material-inspection-worker-source";
    private static int checks;

    internal static bool Handles(string[] args) => args.Any(a=>a.StartsWith("--fresh-material-inspection-worker",StringComparison.Ordinal));
    internal static async Task Run(string[] args)
    {
        bool source=RequireArguments(args);
        if(source)await Source(args[1],args[2]);else await Free();
    }
    private static bool RequireArguments(string[] args)
    {
        if(args.Length==1 && args[0]==FreeMode)return false;
        if(args.Length==3 && args[0]==SourceMode && args.Skip(1).All(a=>!string.IsNullOrWhiteSpace(a) &&
            !a.Contains('\0') && !a.StartsWith('-') && Path.IsPathFullyQualified(a)))return true;
        throw new ArgumentException("Usage: UiSmoke --fresh-material-inspection-worker-only OR --fresh-material-inspection-worker-source absolute-original.bin absolute-original.cue");
    }

    internal static async Task Free()
    {
        checks = 0;
        string root = Directory.CreateTempSubdirectory("spyro-material-inspection-worker-").FullName;
        string tiny = Path.Combine(root, "tiny.bin"), missing = Path.Combine(root, "missing.bin");
        byte[] tinyBytes = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray();
        File.WriteAllBytes(tiny, tinyBytes);
        List<Exception> failures = [];
        try
        {
            Check(!RequireArguments([FreeMode]) && RequireArguments([SourceMode,"/not-opened.bin","/not-opened.cue"]),"exact new modes parse without IO");
            foreach(var args in new[]{Array.Empty<string>(),new[]{FreeMode,"extra"},new[]{FreeMode,FreeMode},
                new[]{SourceMode},new[]{SourceMode,"/bin"},new[]{SourceMode,"/bin","/cue","extra"},
                new[]{SourceMode,FreeMode,"/cue"},new[]{SourceMode,"--unknown","/cue"},
                new[]{SourceMode,"/bin",SourceMode},new[]{SourceMode,"relative.bin","/cue"},
                new[]{SourceMode,"/bin"," "},new[]{SourceMode,"/bin\0","/cue"},new[]{"--fresh-material-inspection-worker-unknown"}})
                Reject(()=>RequireArguments(args),"malformed mode refuses before source/project/UI work");
            var document = Document(); var pair = Pair(14,0,70,1);
            var request = NativeFreshMaterialInspectionRequest.Create(document, pair, missing);
            Check(ReferenceEquals(request.Document, document) && ReferenceEquals(request.Materials,pair) &&
                request.OriginalImagePath == missing && document.SchemaVersion == 1,
                "actual neutral schema1 doc and different raw pair retained without Apply/upgrade/source eligibility");
            foreach (Type type in new[] { typeof(NativeFreshMaterialInspectionRequest), typeof(NativeFreshMaterialInspectionWork) })
                Check(type.IsSealed && type.GetConstructors(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).All(c=>c.IsPrivate) &&
                    type.GetProperties(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).All(p=>p.SetMethod==null),
                    "closed request/completed result, no caller success constructor or mutable state");
            Check(typeof(NativeFreshMaterialInspectionWork).GetMethods(BindingFlags.Static|BindingFlags.NonPublic)
                .Where(m=>m.Name is "CaptureAsync" or "CaptureForSmokeAsync").All(m=>m.GetParameters()[0].ParameterType==typeof(NativeFreshMaterialInspectionRequest)),
                "capture accepts only the concrete neutral request, not bytes or an injected source/result");
            Reject(()=>NativeFreshMaterialInspectionRequest.Create(null!,pair,missing),"null document");
            Reject(()=>NativeFreshMaterialInspectionRequest.Create(document,null!,missing),"null pair");
            foreach(string path in new[]{"", " ", "\0"})
                Reject(()=>NativeFreshMaterialInspectionRequest.Create(document,pair,path),"invalid path");
            Reject(()=>NativeFreshMaterialInspectionRequest.Create(document,
                NativeFreshFloorMaterialsIntent.Create(NativeFreshFloorMaterialIntent.BaseFloorColor(),NativeFreshFloorMaterialIntent.BaseFloorColor()),missing),
                "all color has no source job");
            Check((await Error(()=>NativeFreshMaterialInspectionWork.CaptureAsync(null!))) is ArgumentNullException,
                "null request specifically rejected");
            var phases = new ConcurrentQueue<string>();
            using(var canceled=new CancellationTokenSource())
            {
                canceled.Cancel();
                Exception error=await Error(()=>NativeFreshMaterialInspectionWork.CaptureForSmokeAsync(request,phases.Enqueue,canceled.Token));
                Check(error is OperationCanceledException canceledError && canceledError.CancellationToken==canceled.Token && phases.IsEmpty,
                    "pre-cancel before missing-path IO and any callback");
            }
            Exception missingError=await Error(()=>NativeFreshMaterialInspectionWork.CaptureForSmokeAsync(request,phases.Enqueue));
            Check(Leaves(missingError).Count==2 && Leaves(missingError).All(e=>e is FileNotFoundException) &&
                phases.Contains("before-final-current") && phases.Last()=="after-held-close",
                "actual failed acquisition and independent named-final open both reported, no acquired held-owner claim");

            var tinyRequest=NativeFreshMaterialInspectionRequest.Create(document,pair,tiny);
            phases.Clear();
            Exception tinyError=await Error(()=>NativeFreshMaterialInspectionWork.CaptureForSmokeAsync(tinyRequest,phases.Enqueue));
            Check(Leaves(tinyError).Count==3 && Leaves(tinyError).All(e=>e.GetType()==typeof(InvalidDataException)),
                "actual initial/held/current complete-original guards each reject the genuine tiny file");
            RequireClosed(tiny);

            IOException primary=new IOException("primary observer"), heldHook=new IOException("held hook"),
                currentHook=new IOException("current hook"), closeHook=new IOException("close hook");
            phases.Clear();
            Exception grouped=await Error(()=>NativeFreshMaterialInspectionWork.CaptureForSmokeAsync(tinyRequest,phase=>
            {
                phases.Enqueue(phase);
                if(phase=="source-opened")throw primary;
                if(phase=="before-final-held")throw heldHook;
                if(phase=="before-final-current")throw currentHook;
                if(phase=="before-held-close")throw closeHook;
            }));
            var leaves=Leaves(grouped);
            Check(grouped is IOException && leaves.Count==6 && ReferenceEquals(leaves[0],primary) &&
                ReferenceEquals(leaves[1],heldHook) && leaves[2].GetType()==typeof(InvalidDataException) &&
                ReferenceEquals(leaves[3],currentHook) && leaves[4].GetType()==typeof(InvalidDataException) &&
                ReferenceEquals(leaves[5],closeHook) && phases.Last()=="after-held-close",
                "every final observation/close attempted after throwing observers, exact primary and ordered original failures");
            RequireClosed(tiny);
            await TinyCancellationDrain(tinyRequest);
        }
        catch(Exception error){failures.Add(error);}
        finally
        {
            try
            {
                Check(File.ReadAllBytes(tiny).SequenceEqual(tinyBytes) && !File.Exists(missing) &&
                    Directory.EnumerateFileSystemEntries(root).Select(Path.GetFileName).SequenceEqual(new[]{"tiny.bin"}),
                    "all tiny input bytes/tree retained; no cache, lease, Store or output file");
            }
            catch(Exception error){failures.Add(error);}
            Console.WriteLine("MATERIAL_INSPECTION_WORKER_FIXTURES_RETAINED path="+root);
        }
        Throw(failures);
        Console.WriteLine($"FRESH_MATERIAL_INSPECTION_WORKER_FREE_OK checks={checks} nativeWindow=false sourceOriginal=false publication=false");
    }

    private static async Task TinyCancellationDrain(NativeFreshMaterialInspectionRequest request)
    {
        using var cancellation=new CancellationTokenSource();
        using var release=new ManualResetEventSlim(false);
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var phases=new ConcurrentQueue<string>();
        List<Exception> failures=[];
        Exception? actual=null;
        Task<NativeFreshMaterialInspectionWork>? task=null;
        try
        {
            task=NativeFreshMaterialInspectionWork.CaptureForSmokeAsync(request,phase=>
            {
                phases.Enqueue(phase);
                if(phase=="source-opened"){entered.TrySetResult();release.Wait();}
            },cancellation.Token);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            cancellation.Cancel();
            Check(!task.IsCompleted,"actual worker retains its opened owner while blocked even after cancellation");
        }
        catch(Exception error){failures.Add(error);}
        finally
        {
            release.Set(); // Always release BEFORE draining, even on a failed assertion/timeout.
            if(task!=null)try{await task;}catch(Exception error){actual=error;}
            try
            {
                Check(actual!=null && Leaves(actual).Count==3 && Leaves(actual)[0] is OperationCanceledException canceled &&
                    canceled.CancellationToken==cancellation.Token && Leaves(actual).Skip(1).All(e=>e.GetType()==typeof(InvalidDataException)) &&
                    !phases.Any(p=>p.StartsWith("before-material",StringComparison.Ordinal)) && phases.Last()=="after-held-close",
                    "actual cancel plus both real final guards retained; task settles only after every owner close");
            }
            catch(Exception assertion)
            {
                if(actual!=null)failures.Add(actual);
                failures.Add(assertion);
            }
            try{RequireClosed(request.OriginalImagePath);}
            catch(Exception error){failures.Add(error);}
        }
        Throw(failures);
    }

    internal static async Task Source(string bin, string cue)
    {
        checks=0;bin=Path.GetFullPath(bin);cue=Path.GetFullPath(cue);
        FileStream? held=null,heldCue=null;List<Exception> failures=[];
        try
        {
            held=File.OpenRead(bin);Authenticate(held,661547040,Original);
            heldCue=File.OpenRead(cue);Authenticate(heldCue,88,CueSha);
            var document=Document();byte[] canonical=NativeFreshLevelDocumentCodec.SerializeCanonical(document);
            var first=NativeFreshFloorMaterialIntent.RetailTerrain(Variant,14,0);
            var second=NativeFreshFloorMaterialIntent.RetailTerrain(Variant,70,1);
            foreach(var pair in new[]{NativeFreshFloorMaterialsIntent.Create(first,second),
                NativeFreshFloorMaterialsIntent.Create(second,first),NativeFreshFloorMaterialsIntent.Create(first,first),
                NativeFreshFloorMaterialsIntent.Create(NativeFreshFloorMaterialIntent.BaseFloorColor(),second)})
            {
                var request=NativeFreshMaterialInspectionRequest.Create(document,pair,bin);
                var phases=new ConcurrentQueue<string>();
                var result=await NativeFreshMaterialInspectionWork.CaptureForSmokeAsync(request,phases.Enqueue);
                Check(ReferenceEquals(result.Request,request) && ReferenceEquals(request.Document,document) &&
                    ReferenceEquals(request.Materials,pair) && phases.Last()=="after-held-close",
                    "actual successful request/result identity and completed final ownership");
                int count=0;
                for(int slot=0;slot<2;slot++)
                {
                    var material=slot==0?pair.Triangle0:pair.Triangle1;var preview=result.GetPreview(slot);
                    if(material is not NativeFreshRetailTerrainMaterialIntent retail)
                    {Check(preview==null,"color slot receives no source or placeholder thumbnail");continue;}
                    count++;
                    Check(preview!=null && preview.Source.SourceWadEntry==retail.SourceWadEntry &&
                        preview.Source.SourceTextureId==retail.TextureId && Hash(Rgba(preview.CopyRgba()))==Golden(retail.SourceWadEntry,retail.TextureId),
                        "actual source row0 matches independent original-only RGBA literal");
                    var pixels=preview!.CopyRgba();pixels[0]=new(1,2,3,4);
                    Check(Hash(Rgba(preview.CopyRgba()))==Golden(retail.SourceWadEntry,retail.TextureId),"worker retains actual defensive wrapper");
                }
                Check(result.CapturedSlotCount==count && count is >=1 and <=2 &&
                    phases.Count(p=>p.StartsWith("before-material",StringComparison.Ordinal))==count,
                    "exact one/two actual captures, no scan or silent third source");
                if(ReferenceEquals(pair.Triangle0,pair.Triangle1))
                    Check(!ReferenceEquals(result.FirstPreview!.Source,result.SecondPreview!.Source) &&
                        result.FirstPreview.Source.Handle==result.SecondPreview.Source.Handle,
                        "same descriptor twice uses genuine independent captures without losing slot identity");
                result.RequireRequest(request);
                Reject(()=>result.RequireRequest(NativeFreshMaterialInspectionRequest.Create(document,pair,bin)),"foreign equal-field request");
                var equal=NativeFreshLevelDocumentCodec.Parse(canonical);
                Reject(()=>result.RequireRequest(NativeFreshMaterialInspectionRequest.Create(equal,pair,bin)),"genuine equal-byte separately parsed document request");
                Reject(()=>result.GetPreview(-1),"negative slot");Reject(()=>result.GetPreview(2),"third slot");
                Check(!result.DocumentSaveBindingClaimed && !result.DestinationAllocationProven && !result.SchematicRenderingProven &&
                    !result.RuntimeVerified && !result.PublicationApproved && NativeFreshLevelDocumentCodec.SerializeCanonical(document).SequenceEqual(canonical),
                    "neutral document remains unchanged; no saved/native/renderer/publication claim");
            }
            const string runtimeRefusal="Selected native record has unclosed animation/scroll/source-frame semantics or stale scene evidence.";
            const string layoutRefusal="Native texture depth/shape/page/orientation is outside the canonical static subset.";
            foreach(var bad in new[]{(Wad:16,Id:1,Message:runtimeRefusal),(Wad:40,Id:70,Message:runtimeRefusal),
                (Wad:70,Id:12,Message:layoutRefusal)})
            {
                var phases=new ConcurrentQueue<string>();
                var request=NativeFreshMaterialInspectionRequest.Create(document,Pair(14,0,bad.Wad,bad.Id),bin);
                Exception error=await Error(()=>NativeFreshMaterialInspectionWork.CaptureForSmokeAsync(request,phases.Enqueue));
                Check(error.GetType()==typeof(InvalidDataException) && error.Message==bad.Message &&
                    phases.Contains("material0-captured") && phases.Contains("before-material1") &&
                    !phases.Contains("material1-captured") && phases.Last()=="after-held-close",
                    "actual record reaches its exact runtime-control or canonical-layout refusal; no partial result or slot replacement");
            }
            await SourceCancellationDrain(NativeFreshMaterialInspectionRequest.Create(document,Pair(14,0,70,1),bin));
            foreach(string phase in new[]{"before-final-held","after-held-close"})
            {
                using var cts=new CancellationTokenSource();var phases=new ConcurrentQueue<string>();
                Exception error=await Error(()=>NativeFreshMaterialInspectionWork.CaptureForSmokeAsync(
                    NativeFreshMaterialInspectionRequest.Create(document,Pair(14,0,70,1),bin),p=>{phases.Enqueue(p);if(p==phase)cts.Cancel();},cts.Token));
                Check(error is OperationCanceledException canceled && canceled.CancellationToken==cts.Token && phases.Last()=="after-held-close",
                    "late cancellation suppresses successful delivery only after all real final checks/owners settle");
            }
        }
        catch(Exception error){failures.Add(error);}
        finally
        {
            Observe(()=>{if(held!=null)Authenticate(held,661547040,Original);});
            Observe(()=>AuthenticatePath(bin,661547040,Original));
            Observe(()=>{if(heldCue!=null)Authenticate(heldCue,88,CueSha);});
            Observe(()=>AuthenticatePath(cue,88,CueSha));
            Observe(()=>held?.Dispose());Observe(()=>heldCue?.Dispose());
        }
        Throw(failures);
        Console.WriteLine($"FRESH_MATERIAL_INSPECTION_WORKER_SOURCE_OK checks={checks} heldAndNamedOriginalCueFinalAndClosed=true nativeWindow=false publication=false");
        void Observe(Action action){try{action();}catch(Exception error){failures.Add(error);}}
    }

    private static async Task SourceCancellationDrain(NativeFreshMaterialInspectionRequest request)
    {
        using var cts=new CancellationTokenSource();using var release=new ManualResetEventSlim(false);
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var phases=new ConcurrentQueue<string>();List<Exception> failures=[];
        Task<NativeFreshMaterialInspectionWork>? task=null;Exception? actual=null;
        try
        {
            task=NativeFreshMaterialInspectionWork.CaptureForSmokeAsync(request,p=>
            {phases.Enqueue(p);if(p=="material0-captured"){entered.TrySetResult();release.Wait();}},cts.Token);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(30));cts.Cancel();
            Check(!task.IsCompleted,"actual original and first issued source remain owned while the real worker is held");
        }
        catch(Exception error){failures.Add(error);}
        finally
        {
            release.Set();if(task!=null)try{await task;}catch(Exception error){actual=error;}
            try{Check(actual is OperationCanceledException canceled && canceled.CancellationToken==cts.Token &&
                !phases.Contains("before-material1") && phases.Last()=="after-held-close",
                "actual issued first source canceled before slot1; all final original checks and closes precede Task settlement");}
            catch(Exception assertion)
            {
                if(actual!=null)failures.Add(actual);
                failures.Add(assertion);
            }
        }
        Throw(failures);
    }

    private static string Golden(int wad,int id)=>(wad,id) switch
    {
        (14,0)=>"32b6ed11158f07efcd99e58b2ad25d548785dc2246c2204077c8e5a134c7a0e8",
        (70,1)=>"912676823721b6edcb03f1d6b9c1e8c8794e71ef867a7ac66e840c88beb79283",
        _=>throw new InvalidDataException("No independent worker fixture for this source.")
    };
    private static NativeFreshFloorMaterialsIntent Pair(int a,int x,int b,int y)=>NativeFreshFloorMaterialsIntent.Create(
        NativeFreshFloorMaterialIntent.RetailTerrain(Variant,a,x),NativeFreshFloorMaterialIntent.RetailTerrain(Variant,b,y));
    private static NativeFreshLevelDocumentIntent Document()=>NativeFreshLevelDocumentIntent.Create(
        NativeId65FreshDocumentIntent.Create(8448,8448,8704,2048,2048,8960,8960,64,new(80,128,112),new(128,96,64),19),
        Enumerable.Range(1,128).Select(i=>NativeFreshWizardRouteIntent.Create(i.ToString("x32"),9984,9984,8909,9472,9984)));
    private static byte[] Rgba(Rgba32[] pixels)=>pixels.SelectMany(p=>new[]{p.R,p.G,p.B,p.A}).ToArray();
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Authenticate(Stream stream,long length,string sha)
    {
        if(stream.Length!=length)throw new InvalidDataException("Source fixture full length changed.");
        stream.Position=0;if(Convert.ToHexStringLower(SHA256.HashData(stream))!=sha || stream.Length!=length ||
            stream.Position!=length || stream.ReadByte()!=-1 || stream.Position!=length || stream.Length!=length)
            throw new InvalidDataException("Source fixture whole SHA/trueEOF changed.");
    }
    private static void AuthenticatePath(string path,long length,string sha)
    {
        FileStream? stream=null;List<Exception> failures=[];
        try{stream=File.OpenRead(path);Authenticate(stream,length,sha);}catch(Exception error){failures.Add(error);}
        finally{try{stream?.Dispose();}catch(Exception error){failures.Add(error);}}
        Throw(failures);
    }
    private static void RequireClosed(string path)
    {using var exclusive=new FileStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None);Check(exclusive.Length==32,"actual tiny owner handles all closed before returned failure");}
    private static async Task<Exception> Error(Func<Task> action)
    {try{await action();}catch(Exception error){return error;}throw new InvalidDataException("Expected actual operation refusal.");}
    private static List<Exception> Leaves(Exception error)=>error is AggregateException group
        ? group.InnerExceptions.SelectMany(Leaves).ToList():error.InnerException!=null?Leaves(error.InnerException):[error];
    private static void Reject(Action action,string message)
    {try{action();}catch(Exception error)when(error is ArgumentException or InvalidDataException){checks++;return;}throw new InvalidDataException("Expected refusal: "+message);}
    private static void Check(bool condition,string message){if(!condition)throw new InvalidDataException(message);checks++;}
    private static void Throw(List<Exception> failures)
    {if(failures.Count==1)ExceptionDispatchInfo.Capture(failures[0]).Throw();if(failures.Count>1)throw new IOException("Worker test and independent final failures.",new AggregateException(failures));}
}
