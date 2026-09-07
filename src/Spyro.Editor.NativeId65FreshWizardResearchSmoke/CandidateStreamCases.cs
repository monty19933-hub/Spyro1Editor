using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;

/// <summary>Actual issued Candidate writer loop; no image, stage, publication, or fake issued object is created.</summary>
internal static class CandidateStreamCases
{
    private const int SectorLength=2352,SectorCount=281270;
    private const long ImageLength=661547040;
    private const string OriginalHash="fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37";
    private const string ImageHash="c4a70f826b1a9a3d4ad8e36512be354b6205d27ea2c7a314776a51067f35b5a6";

    internal static void Source(string bin,NativeId65FreshWizardResearchCandidatePlan plan,Action<bool,string> check)
    {
        check(Path.GetFullPath(bin)==plan.SourceImagePath,"Writer stream cases use the actual captured source path and Candidate.");
        // The sink hashes the buffers actually passed to Write without retaining
        // them. This exercises successful return, not filesystem flush/reopen or
        // independent afterimage construction (the physical oracle is separate).
        Streams((source,output)=>
        {
            source.Position=123;int callbacks=0;
            NativeId65FreshDiscVerification result=plan.WriteImageForSmoke(source,output,default,()=>callbacks++);
            check(result.OutputSha256==ImageHash&&result.ChangedSectors==1290&&output.FinishHash()==ImageHash,
                "Whole actual writer return and non-storing sink match the independent full image/count.");
            check(output.BytesWritten==ImageLength&&output.WriteCalls==SectorCount&&callbacks==1,
                "Whole writer consumed every sector and invoked the first-sector callback exactly once.");
            check(source.Passes.Count(p=>p.Kind==PassKind.Pairs&&p.CompleteEof&&p.Bytes==ImageLength&&p.Sha256==OriginalHash)==1,
                "The middle sector pass independently consumed the full original and actual EOF.");
            FinalOriginal(source);
        });

        Streams((source,output)=>
        {
            using var cancel=new CancellationTokenSource();cancel.Cancel();
            Reject<OperationCanceledException>(()=>plan.WriteImageForSmoke(source,output,cancel.Token,()=>{}));
            check(source.ReadCalls==0&&output.WriteCalls==0,"Pre-canceled writer performs no source or output reads/writes.");
        });
        foreach(int invalid in Enumerable.Range(0,4))
            Streams((source,output)=>
            {
                if(invalid==0)source.Readable=false;
                if(invalid==1)source.Seekable=false;
                if(invalid>=2)source.ReportedLength=ImageLength+(invalid==2?-1:1);
                Reject<InvalidDataException>(()=>plan.WriteImageForSmoke(source,output,default,()=>{}));
                check(source.ReadCalls==0&&output.WriteCalls==0,"Initial source capabilities/length refuse before reading; no finally is claimed before the precheck succeeds.");
            });
        foreach(int invalid in Enumerable.Range(0,6))
            Streams((source,output)=>
            {
                if(invalid==0)output.Writable=false;
                if(invalid==1)output.Seekable=false;
                if(invalid==2)output.InitialLength=1;
                if(invalid==3)output.InitialPosition=1;
                if(invalid<4)Reject<InvalidDataException>(()=>plan.WriteImageForSmoke(source,output,default,()=>{}));
                else Reject<ArgumentNullException>(()=>plan.WriteImageForSmoke(source,invalid==4?null!:output,default,invalid==5?null!:()=>{}));
                check(output.WriteCalls==0&&!source.Passes.Any(p=>p.Kind==PassKind.Pairs),
                    "Null callback/output or nonempty/nonwritable/nonseekable output refuses before sector streaming.");
                FinalOriginal(source);
            });

        // LBA 0 is untouched; 37 is the WAD directory; 55125 is in the new
        // beyond-old-profile EXE owner. Mutations affect returned loop bytes
        // only, never the FileStream, original precheck, or final identity pass.
        foreach(int lba in new[]{0,37,55125})
            Streams((source,output)=>
            {
                source.CorruptPairLba=lba;
                Reject<InvalidDataException>(()=>plan.WriteImageForSmoke(source,output,default,()=>{}));
                check(source.Corruptions==1&&output.WriteCalls==lba&&output.BytesWritten==lba*(long)SectorLength,
                    "Per-LBA authentication rejects transient original corruption before writing that changed/unchanged pair.");
                FinalOriginal(source);
            });
        Streams((source,output)=>
        {
            source.ShortPair=true;
            Reject<EndOfStreamException>(()=>plan.WriteImageForSmoke(source,output,default,()=>{}));
            check(source.Passes.Single(p=>p.Kind==PassKind.Pairs).Bytes==SectorLength-1&&source.ShortEofReads==1&&output.WriteCalls==0,
                "An exact declared source length cannot hide a short sector followed by real zero-byte Read.");
            FinalOriginal(source);
        });
        foreach(bool sourceSide in new[]{true,false})
            foreach(bool lengthField in new[]{true,false})
                Streams((source,output)=>
                {
                    if(sourceSide)source.AfterPair=end=>
                    {
                        if(end!=SectorLength)return;
                        if(lengthField)source.OnceLengthDelta=1;else source.OncePositionDelta=1;
                    };
                    else output.AfterWrite=end=>
                    {
                        if(end!=SectorLength)return;
                        if(lengthField)output.OnceLengthDelta=1;else output.OncePositionDelta=1;
                    };
                    Reject<InvalidDataException>(()=>plan.WriteImageForSmoke(source,output,default,()=>{}));
                    check(source.PerturbedReads+output.PerturbedReads==1&&output.WriteCalls==(sourceSide?0:1),
                        "Transient source/output Length/Position drift refuses at its pair, before later hashes could hide it.");
                    FinalOriginal(source);
                });
        Streams((source,output)=>
        {
            output.ShortWrite=true;
            Reject<InvalidDataException>(()=>plan.WriteImageForSmoke(source,output,default,()=>{}));
            check(output.WriteCalls==1&&output.BytesWritten==SectorLength-1,"A Write returning normally after a partial sector cannot pass output position/length checks.");
            FinalOriginal(source);
        });
        Streams((source,output)=>
        {
            output.ThrowOnWrite=2;
            Reject<IOException>(()=>plan.WriteImageForSmoke(source,output,default,()=>{}));
            check(output.WriteCalls==1&&output.WriteAttempts==2,"Actual output Write failure is observed after one complete sector.");
            FinalOriginal(source);
        });
        Streams((source,output)=>
        {
            int callbacks=0;
            Reject<IOException>(()=>plan.WriteImageForSmoke(source,output,default,()=>{callbacks++;throw new IOException("first-sector witness");}));
            check(callbacks==1&&output.WriteCalls==1,"Actual first-sector callback failure cannot escape the complete original finally.");
            FinalOriginal(source);
        });
        foreach(int sectors in new[]{1,SectorCount/2,SectorCount})
            Streams((source,output)=>
            {
                using var cancel=new CancellationTokenSource();
                output.AfterWrite=end=>{if(end==sectors*(long)SectorLength)cancel.Cancel();};
                Reject<OperationCanceledException>(()=>plan.WriteImageForSmoke(source,output,cancel.Token,()=>{}));
                check(cancel.IsCancellationRequested&&output.WriteCalls==sectors,"First/middle/final output pair cancellation cannot return partial success.");
                FinalOriginal(source);
            });
        Streams((source,output)=>
        {
            using var cancel=new CancellationTokenSource();
            source.AfterPair=end=>{if(end==SectorLength)cancel.Cancel();};
            Reject<OperationCanceledException>(()=>plan.WriteImageForSmoke(source,output,cancel.Token,()=>{}));
            check(output.WriteCalls==1,"Cancellation arriving during a pair is checked after that authenticated complete pair, not misreported as zero writes.");
            FinalOriginal(source);
        });
        Streams((source,output)=>
        {
            using var cancel=new CancellationTokenSource();int callbacks=0;
            Reject<OperationCanceledException>(()=>plan.WriteImageForSmoke(source,output,cancel.Token,()=>{callbacks++;cancel.Cancel();}));
            check(callbacks==1&&output.WriteCalls==1,"First-sector callback cancellation stops before the next source/output pair.");
            FinalOriginal(source);
        });
        foreach(bool cancelAtEof in new[]{false,true})
            Streams((source,output)=>
            {
                using var cancel=new CancellationTokenSource();
                if(cancelAtEof)source.AfterPairEof=cancel.Cancel;else source.ExtraPairEofByte=true;
                if(cancelAtEof)Reject<OperationCanceledException>(()=>plan.WriteImageForSmoke(source,output,cancel.Token,()=>{}));
                else Reject<InvalidDataException>(()=>plan.WriteImageForSmoke(source,output,default,()=>{}));
                check(output.WriteCalls==SectorCount&&(cancelAtEof?cancel.IsCancellationRequested:source.ExtraEofReads==1),
                    "Actual end-of-stream cancellation/extra byte refuses after all pairs without returning verified success.");
                FinalOriginal(source);
            });
        Streams((source,output)=>
        {
            source.CorruptFinalIdentity=true;
            Reject<InvalidDataException>(()=>plan.WriteImageForSmoke(source,output,default,()=>throw new IOException("must not bypass full final SHA")),
                "Wizard original full source SHA changed.");
            Pass[] identities=source.Passes.Where(p=>p.Kind==PassKind.Identity).ToArray();
            check(identities.Length==2&&identities[0].Sha256==OriginalHash&&identities[1].CompleteEof&&
                identities[1].Bytes==ImageLength&&identities[1].Sha256!=OriginalHash&&source.Corruptions==1,
                "A final-pass-only returned-byte mutation overrides the callback error with an actual full-original SHA refusal; source file stays read-only.");
        });

        void FinalOriginal(OriginalProbe source)
        {
            Pass[] identities=source.Passes.Where(p=>p.Kind==PassKind.Identity).ToArray();
            check(identities.Length==2&&identities.All(p=>p.CompleteEof&&p.Bytes==ImageLength&&p.Sha256==OriginalHash),
                "Both actual full original identity passes reached EOF with the exact SHA, including noncancellable finally after error/cancellation.");
        }
        void Streams(Action<OriginalProbe,CountingOutput> action)
        {
            using var original=new FileStream(bin,FileMode.Open,FileAccess.Read,FileShare.Read);
            using var source=new OriginalProbe(original);using var output=new CountingOutput();
            action(source,output);
            check(!source.WasDisposed&&!output.WasDisposed&&original.CanRead,
                "Writer leaves caller-owned source/output streams open; this harness writes no filesystem output.");
        }
        void Reject<T>(Action action,string? exactMessage=null)where T:Exception
        {
            try{action();}
            catch(T error){check(exactMessage==null||error.Message==exactMessage,"Expected actual writer refusal "+typeof(T).Name);return;}
            throw new InvalidDataException("Expected actual writer refusal "+typeof(T).Name);
        }
    }

    private enum PassKind{Unknown,Identity,Pairs}
    private sealed class Pass
    {
        internal PassKind Kind;
        internal long Bytes;
        internal bool CompleteEof;
        internal string? Sha256;
    }

    // Non-owning wrapper: distinguish actual block identity reads from 2352-byte
    // writer reads, and hash precisely the returned bytes through the actual EOF.
    private sealed class OriginalProbe(Stream inner):Stream
    {
        private IncrementalHash? _hash;
        private Pass? _pass;
        private bool _pastEnd;
        internal readonly List<Pass> Passes=[];
        internal bool Readable=true,Seekable=true,WasDisposed,ShortPair,ExtraPairEofByte,CorruptFinalIdentity;
        internal long? ReportedLength,OnceLengthDelta,OncePositionDelta;
        internal int? CorruptPairLba;
        internal int ReadCalls,PerturbedReads,Corruptions,ShortEofReads,ExtraEofReads;
        internal Action<long>? AfterPair;
        internal Action? AfterPairEof;
        public override bool CanRead=>Readable;
        public override bool CanSeek=>Seekable;
        public override bool CanWrite=>false;
        public override long Length=>Property(ReportedLength??inner.Length,ref OnceLengthDelta);
        public override long Position
        {
            get=>Property(inner.Position+(_pastEnd?1:0),ref OncePositionDelta);
            set
            {
                inner.Position=value;_pastEnd=false;
                if(value!=0)return;
                _hash?.Dispose();_hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                _pass=new Pass();Passes.Add(_pass);
            }
        }
        private long Property(long value,ref long? delta)
        {if(delta is not long change)return value;delta=null;PerturbedReads++;return checked(value+change);}
        public override int Read(byte[] buffer,int offset,int count)=>Read(buffer.AsSpan(offset,count));
        public override int Read(Span<byte> buffer)
        {
            ReadCalls++;if(buffer.IsEmpty)return 0;
            Pass pass=_pass??throw new InvalidDataException("Harness read requires actual writer position reset.");
            if(pass.Kind==PassKind.Unknown)pass.Kind=buffer.Length==SectorLength?PassKind.Pairs:PassKind.Identity;
            bool pairs=pass.Kind==PassKind.Pairs;
            if(pairs&&ExtraPairEofByte&&inner.Position==ImageLength&&!_pastEnd)
            {buffer[0]=0xA5;_pastEnd=true;ExtraEofReads++;pass.Bytes++;_hash!.AppendData(buffer[..1]);return 1;}
            int allowed=buffer.Length;
            if(pairs&&ShortPair)allowed=(int)Math.Min(allowed,Math.Max(0,SectorLength-1-pass.Bytes));
            if(allowed==0){ShortEofReads++;return 0;}
            long before=inner.Position;int n=_pastEnd?0:inner.Read(buffer[..allowed]);
            if(n==0)
            {
                if(inner.Position==ImageLength&&!_pastEnd&&!pass.CompleteEof)
                {pass.CompleteEof=true;pass.Sha256=Convert.ToHexStringLower(_hash!.GetHashAndReset());if(pairs)AfterPairEof?.Invoke();}
                return 0;
            }
            if(pairs&&CorruptPairLba is int lba&&before==lba*(long)SectorLength)
            {buffer[24]^=1;Corruptions++;CorruptPairLba=null;}
            if(!pairs&&CorruptFinalIdentity&&Passes.Count(p=>p.Kind==PassKind.Identity)==2&&before==0)
            {buffer[24]^=1;Corruptions++;CorruptFinalIdentity=false;}
            pass.Bytes+=n;_hash!.AppendData(buffer[..n]);if(pairs)AfterPair?.Invoke(inner.Position);return n;
        }
        public override long Seek(long offset,SeekOrigin origin)
        {Position=checked((origin switch{SeekOrigin.Begin=>0,SeekOrigin.Current=>inner.Position,SeekOrigin.End=>inner.Length,_=>throw new ArgumentOutOfRangeException(nameof(origin))})+offset);return Position;}
        protected override void Dispose(bool disposing){WasDisposed=true;if(disposing)_hash?.Dispose();base.Dispose(disposing);}
        public override void Flush()=>throw new NotSupportedException();
        public override void SetLength(long value)=>throw new NotSupportedException();
        public override void Write(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
    }

    private sealed class CountingOutput:Stream
    {
        private readonly IncrementalHash _hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        internal bool Writable=true,Seekable=true,WasDisposed,ShortWrite;
        internal long InitialLength,InitialPosition,BytesWritten;
        internal long? OnceLengthDelta,OncePositionDelta;
        internal int WriteCalls,WriteAttempts,PerturbedReads;
        internal int? ThrowOnWrite;
        internal Action<long>? AfterWrite;
        public override bool CanRead=>false;
        public override bool CanSeek=>Seekable;
        public override bool CanWrite=>Writable;
        public override long Length=>Property(BytesWritten+InitialLength,ref OnceLengthDelta);
        public override long Position{get=>Property(BytesWritten+InitialPosition,ref OncePositionDelta);set=>throw new NotSupportedException();}
        private long Property(long value,ref long? delta)
        {if(delta is not long change)return value;delta=null;PerturbedReads++;return checked(value+change);}
        internal string FinishHash()=>Convert.ToHexStringLower(_hash.GetHashAndReset());
        public override void Write(byte[] buffer,int offset,int count)=>Write(buffer.AsSpan(offset,count));
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            WriteAttempts++;
            if(!Writable)throw new NotSupportedException();
            if(ThrowOnWrite==WriteAttempts)throw new IOException("actual output Write witness");
            if(buffer.Length!=SectorLength||BytesWritten+buffer.Length>ImageLength)throw new InvalidDataException("Harness output sector/bound violation.");
            int n=ShortWrite?buffer.Length-1:buffer.Length;
            _hash.AppendData(buffer[..n]);BytesWritten+=n;WriteCalls++;AfterWrite?.Invoke(BytesWritten);
        }
        protected override void Dispose(bool disposing){WasDisposed=true;if(disposing)_hash.Dispose();base.Dispose(disposing);}
        public override int Read(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
        public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();
        public override void Flush()=>throw new NotSupportedException();
        public override void SetLength(long value)=>throw new NotSupportedException();
    }
}
