using Spyro.Editor.Core.Exporting;

/// <summary>Virtual read-only attacks; the original BIN is never opened for writes.</summary>
internal static class CaptureCases
{
    internal static async Task Source(string path,NativeId65FreshWizardResearchExecutablePlan exe,Action<bool,string> check)
    {
        // Arm ONLY after the genuine old index/source capture. One changed
        // read disappears before the final full hash: final SHA alone is not
        // the discriminating guard. Entire-sector auth must reject before parse.
        foreach(long at in new[]{16L*2352+24+80,22L*2352+24+1900,18L*2352+24+100,19L*2352+24+100,
            20L*2352+24+100,21L*2352+24+100,37L*2352+24+2000,53875L*2352+24+100,
            55125L*2352+2348,55161L*2352+2351,265759L*2352+24+1900})
        {
            using var file=File.OpenRead(path);using var stream=new Probe(file);bool refused=false;
            try{await NativeId65FreshWizardResearchDiscSource.CaptureForSmokeAsync(stream,exe,()=>stream.MutateAt=at,CancellationToken.None);}
            catch(InvalidDataException e)when(e.Message.Contains("private authenticated source-LBA digest",StringComparison.Ordinal)){refused=true;}
            check(refused&&stream.Mutations==1&&stream.EofReads>=3,
                "New parser/preimage authenticates whole physical LBA before parsing a one-read mutation restored before final full SHA.");
        }
        // Same original index, but the newly read source position lies. Unlike
        // a final-only position check this must stop before the parser proceeds.
        using(var file=File.OpenRead(path))using(var stream=new Probe(file))
        {
            bool refused=false;
            try{await NativeId65FreshWizardResearchDiscSource.CaptureForSmokeAsync(stream,exe,()=>stream.PositionFaultAfterNextRead=true,CancellationToken.None);}
            catch(InvalidDataException e)when(e.Message.Contains("Authenticated physical read position",StringComparison.Ordinal)){refused=true;}
            check(refused&&stream.PositionFaults==1,"New authenticated reader checks actual source position after a raw sector read.");
        }
        foreach(bool readable in new[]{true,false})
        {
            using var file=File.OpenRead(path);using var stream=new Probe(file){Readable=readable,Seekable=!readable};
            await Refuse<InvalidDataException>(()=>NativeId65FreshWizardResearchDiscSource.CaptureForSmokeAsync(stream,exe,null,CancellationToken.None));
            check(stream.Reads==0,"Actual issuer refuses unavailable read/seek before any source bytes or index allocation.");
        }
        foreach(long length in new[]{0L,661547039L,661547041L,long.MaxValue})
        {
            using var file=File.OpenRead(path);using var stream=new Probe(file){DeclaredLength=length};
            await Refuse<InvalidDataException>(()=>NativeId65FreshWizardResearchDiscSource.CaptureForSmokeAsync(stream,exe,null,CancellationToken.None));
            check(stream.Reads==0,"Declared wrong/oversized source length refuses before read/index allocation.");
        }
        using(var file=File.OpenRead(path))using(var stream=new Probe(file){ExtraEof=true})
        {
            await Refuse<InvalidDataException>(()=>NativeId65FreshWizardResearchDiscSource.CaptureForSmokeAsync(stream,exe,null,CancellationToken.None));
            check(stream.ExtraReads==1,"Actual source EOF byte is checked despite declared exact Length.");
        }
        foreach(string phase in new[]{"pre","index","after-original","new-gap","last-preimage","final-identity"})
        {
            using var file=File.OpenRead(path);using var stream=new Probe(file);using var cancel=new CancellationTokenSource();
            bool armed=false,triggered=false;int lastReads=0;
            if(phase=="pre"){cancel.Cancel();triggered=true;}
            stream.AfterRead=(start,n)=>
            {
                bool index=phase=="index"&&stream.EofReads==1&&n>2352;
                bool gap=armed&&phase=="new-gap"&&start==54079L*2352&&n==2352;
                if(armed&&start==55161L*2352&&n==2352)lastReads++;
                bool last=armed&&phase=="last-preimage"&&lastReads==2;
                bool final=armed&&phase=="final-identity"&&start==0&&n>2352;
                if(!triggered&&(index||gap||last||final)){triggered=true;cancel.Cancel();}
            };
            Action hook=()=>{armed=true;if(phase=="after-original"){triggered=true;cancel.Cancel();}};
            await Refuse<OperationCanceledException>(()=>NativeId65FreshWizardResearchDiscSource.CaptureForSmokeAsync(stream,exe,hook,cancel.Token));
            check(triggered&&(phase!="pre"||stream.Reads==0),"Cancellation at exact "+phase+" returns no partial source/plan.");
            if(phase is "index" or "after-original" or "new-gap" or "last-preimage" or "final-identity")
                check(stream.EofReads>=2,"Real noncancellable original finally completed after "+phase+" cancellation.");
        }
        // New-reader length faults are transient and removed before final hash.
        using(var file=File.OpenRead(path))using(var stream=new Probe(file))
        {
            bool refused=false;
            try{await NativeId65FreshWizardResearchDiscSource.CaptureForSmokeAsync(stream,exe,()=>stream.LengthFaultAfterNextRead=true,CancellationToken.None);}
            catch(InvalidDataException e)when(e.Message.Contains("Authenticated physical source length",StringComparison.Ordinal)){refused=true;}
            check(refused&&stream.LengthFaults==1,"New parser cannot consume a raw read whose Length changed transiently.");
        }
        Console.WriteLine("FRESH_WIZARD_DISC_CAPTURE_OK transientMetadata=11 cancellationPhases=6 outputsWritten=0");
    }
    private static async Task Refuse<T>(Func<Task> action)where T:Exception
    {try{await action();}catch(T){return;}throw new Exception("Expected actual "+typeof(T).Name+" source refusal.");}
    private sealed class Probe(Stream inner):Stream
    {
        internal bool Readable{get;init;}=true;
        internal bool Seekable{get;init;}=true;
        internal long? DeclaredLength{get;init;}
        internal long? MutateAt{get;set;}
        internal bool ExtraEof{get;init;}
        internal bool PositionFaultAfterNextRead{get;set;}
        internal bool LengthFaultAfterNextRead{get;set;}
        internal Action<long,int>? AfterRead{get;set;}
        internal int Reads,Mutations,EofReads,ExtraReads,PositionFaults,LengthFaults;
        private bool _extra,_positionFault,_lengthFault;
        public override bool CanRead=>Readable;
        public override bool CanSeek=>Seekable;
        public override bool CanWrite=>false;
        public override long Length
        {
            get
            {
                if(_lengthFault){_lengthFault=false;LengthFaults++;return inner.Length+1;}
                return DeclaredLength??inner.Length;
            }
        }
        public override long Position
        {
            get
            {
                if(_positionFault){_positionFault=false;PositionFaults++;return inner.Position+1;}
                return inner.Position+(_extra?1:0);
            }
            set{inner.Position=value;_extra=false;_positionFault=false;}
        }
        public override int Read(byte[] b,int at,int n)=>Read(b.AsSpan(at,n));
        public override int Read(Span<byte> b)
        {
            long start=inner.Position;Reads++;
            if(b.Length>0&&start==inner.Length)
            {
                EofReads++;
                if(ExtraEof&&!_extra){b[0]=1;_extra=true;ExtraReads++;return 1;}
            }
            int n=inner.Read(b);
            if(MutateAt is long changed&&changed>=start&&changed<start+n)
            {b[(int)(changed-start)]^=1;MutateAt=null;Mutations++;}
            if(PositionFaultAfterNextRead&&n>0){PositionFaultAfterNextRead=false;_positionFault=true;}
            if(LengthFaultAfterNextRead&&n>0){LengthFaultAfterNextRead=false;_lengthFault=true;}
            AfterRead?.Invoke(start,n);return n;
        }
        public override ValueTask<int> ReadAsync(Memory<byte> b,CancellationToken cancellationToken=default)
        {cancellationToken.ThrowIfCancellationRequested();return ValueTask.FromResult(Read(b.Span));}
        public override Task<int> ReadAsync(byte[] b,int at,int n,CancellationToken cancellationToken)
        {cancellationToken.ThrowIfCancellationRequested();return Task.FromResult(Read(b,at,n));}
        public override long Seek(long offset,SeekOrigin origin)=>inner.Seek(offset,origin);
        public override void Flush()=>throw new NotSupportedException();
        public override void SetLength(long n)=>throw new NotSupportedException();
        public override void Write(byte[] b,int at,int n)=>throw new NotSupportedException();
    }
}
