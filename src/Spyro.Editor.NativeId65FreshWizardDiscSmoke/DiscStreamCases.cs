using Spyro.Editor.Core.Exporting;

/// <summary>Actual issued-plan readback controls; virtual output is not an independent byte oracle.</summary>
internal static class DiscStreamCases
{
    private const long ImageLength=661547040;
    private const int SectorLength=2352, SectorCount=281270;

    internal static void Source(string path,NativeId65FreshWizardResearchDiscPlan plan,Action<bool,string> check)
    {
        // SourceCases separately checks every expected/inverse sector against
        // the pre-production raw oracle. This stream tests real VerifyImage
        // control paths without creating a physical candidate or fake Plan.
        Images((source,output)=>
        {
            source.Position=123;output.Position=456;
            NativeId65FreshDiscVerification result=plan.VerifyImage(source,output);
            check(result.OutputSha256=="c4a70f826b1a9a3d4ad8e36512be354b6205d27ea2c7a314776a51067f35b5a6"&&result.ChangedSectors==1290,
                "Actual whole verifier returns the independent full hash/count after resetting caller positions.");
            check(source.Position==ImageLength&&output.Position==ImageLength&&source.EofReads>0&&output.EofReads>0,
                "Successful verification consumed both complete streams and probed both actual EOFs.");
        });
        foreach(bool sourceSide in new[]{true,false})
        {
            foreach(long length in new[]{-1L,0L,ImageLength-1,ImageLength+1,long.MaxValue})
                Images((source,output)=>
                {
                    (sourceSide?source:output).ReportedLength=length;
                    Reject<InvalidDataException>(()=>plan.VerifyImage(source,output));
                    check(source.ReadCalls==0&&output.ReadCalls==0,"Initial invalid declared length refuses before any sector read.");
                });
            foreach(bool readable in new[]{true,false})
                Images((source,output)=>
                {
                    Probe selected=sourceSide?source:output;
                    if(readable)selected.Readable=false;else selected.Seekable=false;
                    Reject<InvalidDataException>(()=>plan.VerifyImage(source,output));
                    check(source.ReadCalls==0&&output.ReadCalls==0,"Missing readable/seekable capability refuses before any sector read.");
                });
            Images((source,output)=>
            {
                Probe selected=sourceSide?source:output;selected.ShortAfterBytes=SectorLength-1;
                Reject<EndOfStreamException>(()=>plan.VerifyImage(source,output));
                check(selected.BytesRead==SectorLength-1&&selected.EofReads>0,"Actual truncation cannot hide behind an exact reported image length.");
            });
            foreach(bool lengthField in new[]{true,false})
                Images((source,output)=>
                {
                    Probe selected=sourceSide?source:output;
                    selected.AfterRead=end=>
                    {
                        if(end!=SectorLength)return;
                        if(lengthField)selected.OnceLengthDelta=1;else selected.OncePositionDelta=1;
                    };
                    Reject<InvalidDataException>(()=>plan.VerifyImage(source,output));
                    check(selected.PerturbedPropertyReads==1&&source.BytesRead==SectorLength&&output.BytesRead==SectorLength,
                        "One-time source/output Length/Position change is rejected in its first raw pair, not only at final hash.");
                });
            Images((source,output)=>
            {
                Probe selected=sourceSide?source:output;selected.ExtraEofByte=true;
                Reject<InvalidDataException>(()=>plan.VerifyImage(source,output));
                check(selected.ExtraEofReads==1,"Actual source/output suffix refuses despite the exact declared Length.");
            });
            foreach(int sectors in new[]{1,SectorCount/2,SectorCount})
                Images((source,output)=>
                {
                    using var cancellation=new CancellationTokenSource();
                    (sourceSide?source:output).AfterRead=end=>{if(end>=SectorLength*(long)sectors)cancellation.Cancel();};
                    Reject<OperationCanceledException>(()=>plan.VerifyImage(source,output,cancellation.Token));
                    check(cancellation.IsCancellationRequested&&source.BytesRead==SectorLength*(long)sectors&&output.BytesRead==SectorLength*(long)sectors,
                        "First/middle/final source or output pair cancellation cannot return a partial verification success.");
                });
            Images((source,output)=>
            {
                using var cancellation=new CancellationTokenSource();
                Probe selected=sourceSide?source:output;selected.AfterEof=cancellation.Cancel;
                Reject<OperationCanceledException>(()=>plan.VerifyImage(source,output,cancellation.Token));
                check(selected.EofReads>0&&cancellation.IsCancellationRequested,"Cancellation during either actual EOF probe cannot return success.");
            });
        }
        Images((source,output)=>
        {
            using var cancellation=new CancellationTokenSource();cancellation.Cancel();
            Reject<OperationCanceledException>(()=>plan.VerifyImage(source,output,cancellation.Token));
            check(source.ReadCalls==0&&output.ReadCalls==0,"Already canceled verification performs no source/output reads.");
        });

        void Images(Action<Probe,Probe> action)
        {
            using var original=File.OpenRead(path);using var expectedOriginal=File.OpenRead(path);
            using var expected=new ExpectedImage(expectedOriginal,plan);
            using var source=new Probe(original);using var output=new Probe(expected);
            action(source,output);
            check(!source.WasDisposed&&!output.WasDisposed&&original.CanRead&&expectedOriginal.CanRead,
                "Verifier never disposes caller-owned source/output streams on success or refusal.");
        }
        void Reject<T>(Action action)where T:Exception
        {
            try{action();}catch(T){check(true,"Expected actual verifier refusal "+typeof(T).Name);return;}
            throw new InvalidDataException("Expected actual verifier refusal "+typeof(T).Name);
        }
    }

    // Generated only from the actual private plan and actual original raw
    // sectors. No independent claim: the literal full hash is from Python.
    internal sealed class ExpectedImage(Stream original,NativeId65FreshWizardResearchDiscPlan plan):Stream
    {
        private long _position;
        public override bool CanRead=>true;
        public override bool CanSeek=>true;
        public override bool CanWrite=>false;
        public override long Length=>ImageLength;
        public override long Position
        {
            get=>_position;
            set{if(value<0||value>Length)throw new ArgumentOutOfRangeException(nameof(value));_position=value;}
        }
        public override int Read(byte[] buffer,int offset,int count)=>Read(buffer.AsSpan(offset,count));
        public override int Read(Span<byte> destination)
        {
            int count=(int)Math.Min(destination.Length,Length-Position),copied=0;
            while(copied<count)
            {
                int lba=checked((int)(Position/SectorLength)),offset=(int)(Position%SectorLength);
                int part=Math.Min(SectorLength-offset,count-copied);byte[] before=new byte[SectorLength];
                original.Position=SectorLength*(long)lba;original.ReadExactly(before);
                byte[] after=plan.CreateExpectedRawSector(lba,before);
                after.AsSpan(offset,part).CopyTo(destination[copied..]);copied+=part;Position+=part;
            }
            return count;
        }
        public override long Seek(long offset,SeekOrigin origin)
        {Position=checked((origin switch{SeekOrigin.Begin=>0,SeekOrigin.Current=>Position,SeekOrigin.End=>Length,_=>throw new ArgumentOutOfRangeException(nameof(origin))})+offset);return Position;}
        public override void Flush()=>throw new NotSupportedException();
        public override void SetLength(long value)=>throw new NotSupportedException();
        public override void Write(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
    }

    private sealed class Probe(Stream inner):Stream
    {
        internal bool Readable{get;set;}=true;
        internal bool Seekable{get;set;}=true;
        internal long? ReportedLength{get;set;}
        internal long? ShortAfterBytes{get;set;}
        internal long? OnceLengthDelta{get;set;}
        internal long? OncePositionDelta{get;set;}
        internal int PerturbedPropertyReads{get;private set;}
        internal bool ExtraEofByte{get;set;}
        internal int ExtraEofReads{get;private set;}
        internal int EofReads{get;private set;}
        internal int ReadCalls{get;private set;}
        internal long BytesRead{get;private set;}
        internal bool WasDisposed{get;private set;}
        internal Action<long>? AfterRead{get;set;}
        internal Action? AfterEof{get;set;}
        private bool _pastEnd;
        public override bool CanRead=>Readable;
        public override bool CanSeek=>Seekable;
        public override bool CanWrite=>false;
        public override long Length
        {
            get
            {
                long actual=ReportedLength??inner.Length;
                if(OnceLengthDelta is not long delta)return actual;
                OnceLengthDelta=null;PerturbedPropertyReads++;return checked(actual+delta);
            }
        }
        public override long Position
        {
            get
            {
                long actual=inner.Position+(_pastEnd?1:0);
                if(OncePositionDelta is not long delta)return actual;
                OncePositionDelta=null;PerturbedPropertyReads++;return checked(actual+delta);
            }
            set{inner.Position=value;_pastEnd=false;}
        }
        public override int Read(byte[] buffer,int offset,int count)=>Read(buffer.AsSpan(offset,count));
        public override int Read(Span<byte> buffer)
        {
            ReadCalls++;
            if(buffer.IsEmpty)return 0;
            if(ExtraEofByte&&inner.Position==inner.Length&&!_pastEnd)
            {buffer[0]=0xA5;_pastEnd=true;ExtraEofReads++;return 1;}
            int allowed=buffer.Length;
            if(ShortAfterBytes is long limit)allowed=(int)Math.Min(allowed,Math.Max(0,limit-BytesRead));
            int n=_pastEnd||allowed==0?0:inner.Read(buffer[..allowed]);BytesRead+=n;
            if(n==0){EofReads++;AfterEof?.Invoke();}
            else AfterRead?.Invoke(inner.Position);
            return n;
        }
        public override long Seek(long offset,SeekOrigin origin){_pastEnd=false;return inner.Seek(offset,origin);}
        protected override void Dispose(bool disposing){WasDisposed=true;base.Dispose(disposing);}
        public override void Flush()=>throw new NotSupportedException();
        public override void SetLength(long value)=>throw new NotSupportedException();
        public override void Write(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
    }
}
