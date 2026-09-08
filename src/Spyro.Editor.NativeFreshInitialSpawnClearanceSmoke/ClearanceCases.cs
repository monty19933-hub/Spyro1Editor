using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;

internal static class ClearanceCases
{
    internal static int Checks,Refusals;
    internal const string Id="11111111111111111111111111111111";
    internal static NativeId65FreshDocumentIntent A()=>NativeId65FreshDocumentIntent.Create(
        8448,8448,8704,1024,1024,8960,8960,64,new(80,128,112),new(128,96,64),19);
    internal static NativeWizardPlacementIntent Placement=>new(8704,8960,8704,9216,8960);
    internal static NativeFreshLevelDocumentIntent Document(NativeId65FreshDocumentIntent floor,NativeWizardPlacementIntent p)=>
        NativeFreshLevelDocumentIntent.Create(floor,[NativeFreshWizardRouteIntent.Create(Id,p.OriginX,p.OriginY,p.OriginZ,p.EndX,p.EndY)]);
    internal static async Task Run()
    {
        Type t=typeof(NativeFreshInitialSpawnClearancePlan);
        Check(t.IsSealed&&t.GetConstructors().Length==0&&t.GetProperties().All(p=>p.SetMethod==null),"Closed immutable issued assessment.");
        var methods=t.GetMethods(BindingFlags.Public|BindingFlags.Static|BindingFlags.DeclaredOnly);
        Check(methods.Length==1&&methods[0].Name=="CaptureWizardAsync"&&methods[0].GetParameters().Select(p=>p.ParameterType).SequenceEqual(new[]{
            typeof(string),typeof(NativeFreshLevelDocumentIntent),typeof(NativeId65FreshWizardResearchScenePlan),typeof(CancellationToken)}),"Only actual document+scene source factory.");
        using(var cancel=new CancellationTokenSource())
        {cancel.Cancel();await Reject<OperationCanceledException>(()=>NativeFreshInitialSpawnClearancePlan.CaptureWizardAsync(null!,null!,null!,cancel.Token));}
        await Reject<ArgumentNullException>(()=>NativeFreshInitialSpawnClearancePlan.CaptureWizardAsync("not-opened",null!,null!));
        // These synthetic bounded bytes test grammar/math only, never source authority.
        byte[] package=Package(),row=Row(Placement);var spawn=new NativeSpawnClearancePoint(8960,8960,9216);
        var f=NativeFreshInitialSpawnClearanceSource.EvaluateWizard(package,row,spawn,Id);
        Check(f.Status==NativeInitialSpawnClearanceStatus.Overlap&&f.DistanceSquared==243828&&f.CombinedRadiusSquared==571536,
            "Independent source/recorded unsafe P1 squared-distance witness.");
        Check(f.Centre==new NativeSpawnClearancePoint(8548,8950,8944)&&f.Vertex==new NativeSpawnClearancePoint(-156,10,-240),"Exact signed/scale/axis transform.");
        Check(f.IncomingDamageFlags==0&&f.ResponsePermitted==true&&f.PlayerRadius==356&&f.ColliderRadius==400,"Zero damage does not disable positional response.");
        foreach(var point in new[]{new NativeSpawnClearancePoint(8548,8970,8464),new(8626,8955,8824),new(8704,8960,8704)})
            Check(NativeInitialSpawnClearanceMath.SquaredDistance(point,spawn)!=f.DistanceSquared,"Wrong sign/scale/row-centre cannot reproduce source witness.");
        var zero=new NativeSpawnClearancePoint(0,0,0);
        Check(NativeInitialSpawnClearanceMath.Classify(zero,400,new(756,0,0),356)==NativeInitialSpawnClearanceStatus.Touching,"Conservative exact tangency is a separate policy refusal.");
        Check(NativeInitialSpawnClearanceMath.Classify(zero,400,new(755,0,0),356)==NativeInitialSpawnClearanceStatus.Overlap,"Inside by one.");
        Check(NativeInitialSpawnClearanceMath.Classify(zero,400,new(757,0,0),356)==NativeInitialSpawnClearanceStatus.InitialSeparation,"Outside by one.");
        Check(NativeInitialSpawnClearanceMath.SquaredDistance(new(int.MinValue,0,0),new(int.MaxValue,0,0)).ToString()=="18446744065119617025","No subtract/square overflow at signed limits.");
        foreach(int radius in new[]{-1,int.MinValue})Reject<InvalidDataException>(()=>NativeInitialSpawnClearanceMath.Classify(zero,radius,zero,356));
        foreach(var change in new[]{(0x3C,(byte)1),(0x3E,(byte)1),(0x44,(byte)1)})
        {byte[] r=row.ToArray();r[change.Item1]=change.Item2;var unknown=NativeFreshInitialSpawnClearanceSource.EvaluateWizard(package,r,spawn,Id);Check(unknown.Status==NativeInitialSpawnClearanceStatus.Unknown,"Unsupported current pose/rotation is Unknown.");Reject<InvalidDataException>(()=>NativeInitialSpawnClearanceMath.RequireInitialSeparation(unknown));}
        Reject<InvalidDataException>(()=>NativeInitialSpawnClearanceMath.RequireInitialSeparation(f));
        Reject<InvalidDataException>(()=>NativeInitialSpawnClearanceMath.RequireInitialSeparation(new(Id,NativeInitialSpawnClearanceStatus.Touching,NativeInitialSpawnUnknownReason.None,spawn)));
        byte[] nullShape=package.ToArray();Put(nullShape,0x68,0xFF200D78);
        Check(NativeFreshInitialSpawnClearanceSource.EvaluateWizard(nullShape,row,spawn,Id).UnknownReason==NativeInitialSpawnUnknownReason.NullCollider,
            "Null selected collider is unknown, not an active zero-radius proof.");
        byte[] unsupported=package.ToArray();unsupported[0x49]=2;
        Check(NativeFreshInitialSpawnClearanceSource.EvaluateWizard(unsupported,row,spawn,Id).UnknownReason==NativeInitialSpawnUnknownReason.UnsupportedAnimationEncoding,"Valid bounded unsupported scale distinguished from malformed owner.");
        foreach(int at in new[]{0x14,0x34,0x38,0x35AC})
        {byte[] b=package.ToArray();Put(b,at,uint.MaxValue);Reject<InvalidDataException>(()=>NativeFreshInitialSpawnClearanceSource.EvaluateWizard(b,row,spawn,Id));}
        foreach(var pair in new[]{(package[..^1],row),(package,row[..^1])})
            Reject<InvalidDataException>(()=>NativeFreshInitialSpawnClearanceSource.EvaluateWizard(pair.Item1,pair.Item2,spawn,Id));
        byte[] negativeY=package.ToArray();Put(negativeY,0xFB0,0xF67FDFC4);
        var packed=NativeFreshInitialSpawnClearanceSource.EvaluateWizard(negativeY,row,spawn,Id);
        Check(packed.Vertex?.Y==-10&&packed.Centre?.Y==8970&&packed.Centre?.Z==8945,"Native signed ADD into packed V borrows high half for negative y; not an OR.");
        Check(NativeFreshInitialSpawnClearanceSource.EvaluateWizard(package,row,new(int.MaxValue,0,0),Id).UnknownReason==NativeInitialSpawnUnknownReason.UnsupportedTransformRange,"Unbounded native IR distance cannot pass through exact integer math.");
        byte[] badCommand=package.ToArray();Put(badCommand,0x35A8,0x01900303);
        Check(NativeFreshInitialSpawnClearanceSource.EvaluateWizard(badCommand,row,spawn,Id).UnknownReason==NativeInitialSpawnUnknownReason.UnsupportedResponseSemantics,"No source flag bypass to separation.");
        var corner=NativeFreshInitialSpawnClearanceSource.EvaluateWizard(package,Row(new(8464,8464,8704,9456,9456)),spawn,Id);
        Check(corner.Status==NativeInitialSpawnClearanceStatus.InitialSeparation&&corner.DistanceSquared==755124,"Initial sphere separation alone is not floor/route safety.");
        var intent=A();byte[] canonical=NativeId65FreshDocumentCodec.SerializeCanonical(intent);
        var aggregate=Document(intent,Placement);byte[] aggregateBytes=NativeFreshLevelDocumentCodec.SerializeCanonical(aggregate);
        Check(aggregate.Placements.Count==1&&aggregateBytes.Length>canonical.Length,"Unsafe neutral authoring remains serializable without source clearance.");
        var elevated=Document(intent,Placement with{OriginZ=8909});
        Check(((NativeFreshWizardRouteIntent)elevated.Placements[0]).OriginZ==8909,"Z has no generic16-grid rejection.");
        foreach(var p in new[]{Placement with{OriginX=8705},Placement with{EndX=9472},Placement with{EndX=8704},Placement with{OriginZ=-1}})
            Reject<InvalidDataException>(()=>Document(intent,p));
        Check(NativeId65FreshDocumentCodec.SerializeCanonical(intent).SequenceEqual(canonical),"No old v1 byte change after clearance/neutral failures.");
        OwnerCountBounds();
        await SnapshotFinally();
        Console.WriteLine($"INITIAL_SPAWN_CLEARANCE_FREE_OK checks={Checks} refusals={Refusals} sourceIssued=false outputsWritten=0");
    }
    private static void OwnerCountBounds()
    {
        // Neutral ownership-checker fixtures only: these cannot issue a Plan.
        // A real strided upload can legitimately exceed 1024 owner spans.
        byte[] package=Package(),models=new byte[0x17800];package.CopyTo(models,0x13B80);
        NativeFreshWizardModelRoot[] roots=[new(0,0,0x10A4C,0x10A4C),new(0x78,0x10A4C,0x2BD0,0x2BD0),
            new(0x26,0x1361C,0x564,0x564),new(0x11B,0x13B80,0x35B4,0x3C80)];
        var owners=new List<NativeFreshWizardResourceOwner>();
        string zeroHash=Convert.ToHexStringLower(SHA256.HashData(new byte[1]));
        for(int i=0;i<1024;i++)owners.Add(new($"upload:{i}",NativeFreshWizardResourceDomain.Upload,i,1,zeroHash,"neutral-strided-upload",null,null));
        owners.Add(new("upload:tail",NativeFreshWizardResourceDomain.Upload,1024,0xE5800-1024,
            Convert.ToHexStringLower(SHA256.HashData(new byte[0xE5800-1024])),"neutral-strided-upload",null,null));
        owners.Add(new("data",NativeFreshWizardResourceDomain.Data,0,0x8FC,
            Convert.ToHexStringLower(SHA256.HashData(new byte[0x8FC])),"neutral-data",null,null));
        foreach(var span in new[]{(At:0,N:0x13B80),(At:0x13B80,N:0x35B4),(At:0x17134,N:0x6CC)})
            owners.Add(new($"models:{span.At}",NativeFreshWizardResourceDomain.Models,span.At,span.N,
                Convert.ToHexStringLower(SHA256.HashData(models.AsSpan(span.At,span.N))),
                span.At==0x17134?"generated-model-padding":"neutral-model",null,null));
        NativeFreshInitialSpawnClearanceSource.RequireInstalledOwners(package,models,roots,owners);
        Check(owners.Count>1024,"Legitimate-shaped complete strided extent count is not an arbitrary 1024-cap failure.");
        foreach(var replacement in new[]{owners[0] with{Offset=-1},owners[0] with{ByteLength=0},
            owners[0] with{ByteLength=-1},owners[0] with{Offset=0xE5800},owners[0] with{ByteLength=int.MaxValue},
            owners[0] with{Domain=(NativeFreshWizardResourceDomain)3},
            owners[1025] with{ByteLength=0x8FD},owners[^1] with{ByteLength=0x6CD}})
        {
            var bad=owners.ToArray();bad[0]=replacement;
            Reject<InvalidDataException>(()=>NativeFreshInitialSpawnClearanceSource.RequireInstalledOwners(package,models,roots,bad));
        }
        foreach(int count in new[]{0,-1,0xE5800+0x8FC+0x17800+1,int.MaxValue})
        {
            var impossible=new CountOnlyOwners(count);
            Reject<InvalidDataException>(()=>NativeFreshInitialSpawnClearanceSource.RequireInstalledOwners(package,models,roots,impossible));
            Check(impossible.Enumerations==0,"Impossible positive-extent count fails before enumeration or allocation.");
        }
    }
    private sealed class CountOnlyOwners(int count):IReadOnlyList<NativeFreshWizardResourceOwner>
    {
        internal int Enumerations;
        public int Count=>count;
        public NativeFreshWizardResourceOwner this[int index]=>throw new InvalidOperationException("Must reject count first.");
        public IEnumerator<NativeFreshWizardResourceOwner> GetEnumerator()
        {Enumerations++;throw new InvalidOperationException("Must reject count first.");}
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()=>GetEnumerator();
    }
    private static async Task SnapshotFinally()
    {
        byte[] bytes=Enumerable.Range(0,32).Select(i=>(byte)i).ToArray();string hash=Convert.ToHexStringLower(SHA256.HashData(bytes));
        foreach(string mode in new[]{"success","cancel","fault","final-drift","combined","fault-cancel","final-drift-cancel"})
        {
            using var cancel=new CancellationTokenSource();using var stream=new SnapshotStream(bytes);bool invoked=false;
            async Task<int> Run()=>await NativeFreshInitialSpawnClearanceSource.WithSnapshotAsync(stream,32,hash,()=>{
                invoked=true;if(mode is "cancel" or "combined" or "fault-cancel" or "final-drift-cancel")cancel.Cancel();
                if(mode is "final-drift" or "combined" or "final-drift-cancel")stream.Corrupt=true;
                if(mode is "fault" or "combined" or "fault-cancel")throw new IOException("exact-primary");return 42;
            },cancel.Token);
            if(mode=="success")Check(await Run()==42,"Actual wrapper result only after final full read.");
            else if(mode=="cancel")await Reject<OperationCanceledException>(async()=>{_=await Run();});
            else if(mode is "final-drift" or "final-drift-cancel")await Reject<InvalidDataException>(async()=>{_=await Run();});
            else if(mode=="combined")
            {try{_=await Run();throw new InvalidDataException("Missing combined failure.");}catch(AggregateException e){Check(e.InnerExceptions.Count==2&&e.InnerExceptions[0] is IOException first&&first.Message=="exact-primary"&&e.InnerExceptions[1] is InvalidDataException,"Primary and final failure preserved in order; late token cannot replace either.");}}
            else
            {try{_=await Run();throw new InvalidDataException("Missing primary failure.");}catch(IOException e){Check(e.Message=="exact-primary","Unchanged primary when finally succeeds.");}}
            Check(invoked&&stream.EofReads==2&&stream.BytesRead==64&&stream.FinalUsedNone,"Cancellation/error still completes exact None final SHA/EOF.");
        }
        using(var cancel=new CancellationTokenSource())
        {cancel.Cancel();using var stream=new SnapshotStream(bytes);await Reject<OperationCanceledException>(()=>NativeFreshInitialSpawnClearanceSource.WithSnapshotAsync(stream,32,hash,()=>1,cancel.Token));Check(stream.BytesRead==0&&stream.EofReads==0,"Early token wins before source read.");}
        using(var stream=new SnapshotStream(bytes){Corrupt=true})
        {bool ran=false;await Reject<InvalidDataException>(()=>NativeFreshInitialSpawnClearanceSource.WithSnapshotAsync(stream,32,hash,()=>{ran=true;return 1;},CancellationToken.None));Check(!ran&&stream.EofReads==1&&stream.BytesRead==32,"Failed first authentication: no action, no claimed authenticated-final read.");}
    }
    private sealed class SnapshotStream:Stream
    {
        private readonly MemoryStream inner;internal SnapshotStream(byte[] b){inner=new(b.ToArray(),false);}
        internal bool Corrupt;internal int EofReads,BytesRead;internal bool FinalUsedNone=true;
        public override bool CanRead=>true;public override bool CanSeek=>true;public override bool CanWrite=>false;
        public override long Length=>inner.Length;public override long Position{get=>inner.Position;set=>inner.Position=value;}
        public override int Read(byte[] b,int o,int n)=>Read(b.AsSpan(o,n));
        public override int Read(Span<byte> b){int n=inner.Read(b);BytesRead+=n;if(Corrupt&&n>0)b[0]^=1;return n;}
        public override ValueTask<int> ReadAsync(Memory<byte> b,CancellationToken t=default)
        {if(EofReads>0)FinalUsedNone&=t==CancellationToken.None;t.ThrowIfCancellationRequested();return ValueTask.FromResult(Read(b.Span));}
        public override int ReadByte(){EofReads++;return inner.ReadByte();}
        public override long Seek(long o,SeekOrigin s)=>inner.Seek(o,s);public override void Flush()=>throw new NotSupportedException();
        public override void SetLength(long n)=>throw new NotSupportedException();public override void Write(byte[] b,int o,int n)=>throw new NotSupportedException();
        protected override void Dispose(bool disposing){if(disposing)inner.Dispose();base.Dispose(disposing);}
    }
    internal static byte[] Package()
    {
        byte[] p=new byte[0x35B4];Put(p,0,3);Put(p,0x14,0x800035A4);Put(p,0x34,0x238);Put(p,0x38,0x44);Put(p,0x3C,0x100);Put(p,0x40,0x19C);
        BinaryPrimitives.WriteInt16LittleEndian(p.AsSpan(0x44),19);p[0x49]=1;Put(p,0x68,0xFF000D78);Put(p,0xFB0,0xF6402FC4);
        Put(p,0x35A4,2011);Put(p,0x35A8,0x01900301);return p;
    }
    internal static byte[] Row(NativeWizardPlacementIntent p)
    {byte[] r=new byte[0x58];Put(r,12,(uint)p.OriginX);Put(r,16,(uint)p.OriginY);Put(r,20,(uint)p.OriginZ);BinaryPrimitives.WriteUInt16LittleEndian(r.AsSpan(0x36),0x11B);Put(r,0x44,0x04000000);return r;}
    internal static void Put(Span<byte> b,int at,uint v)=>BinaryPrimitives.WriteUInt32LittleEndian(b[at..],v);
    internal static void Check(bool ok,string why){if(!ok)throw new InvalidDataException(why);Checks++;}
    internal static void Reject<T>(Action action)where T:Exception{try{action();}catch(T){Checks++;Refusals++;return;}throw new InvalidDataException("Expected "+typeof(T).Name);}
    internal static async Task Reject<T>(Func<Task> action)where T:Exception{try{await action();}catch(T){Checks++;Refusals++;return;}throw new InvalidDataException("Expected "+typeof(T).Name);}
}
