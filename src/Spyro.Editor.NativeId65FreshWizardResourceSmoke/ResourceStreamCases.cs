using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;

internal static class ResourceStreamCases
{
    internal static async Task Run()
    {
        byte[] bytes = Enumerable.Range(0,32).Select(i=>(byte)i).ToArray();
        string hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        int checks=0;
        // This exact shared identity checker validates bytes only; no stream,
        // expected length/hash or test certificate can issue a resource plan.
        using(var valid=new Hostile(bytes))
        { await Guard(valid,CancellationToken.None); Check(valid.EofReads==1 && valid.Reads>0); }
        foreach(string mode in new[]{"oversize","position","short","extra-eof","final-length","final-position","wrong-bytes"})
        {
            using var stream=new Hostile(mode=="short" ? bytes[..^1] : bytes,mode);
            await Reject<InvalidDataException>(()=>Guard(stream,CancellationToken.None));
            if(mode is "oversize" or "position")Check(stream.Reads==0 && stream.EofReads==0);
            if(mode is "extra-eof" or "final-length" or "final-position")Check(stream.EofReads==1);
        }
        foreach(string mode in new[]{"early-cancel","mid-cancel","late-cancel"})
        {
            using var cancel=new CancellationTokenSource();
            using var stream=new Hostile(bytes,mode,cancel);
            if(mode=="early-cancel")cancel.Cancel();
            await Reject<OperationCanceledException>(()=>Guard(stream,cancel.Token));
            Check(mode!="early-cancel" || stream.Reads==0 && stream.EofReads==0);
            Check(mode!="late-cancel" || stream.EofReads==1);
        }
        // Remaining owner exceeds the actual65536-byte read request. A hostile
        // count must be rejected before indexing the returned buffer.
        byte[] large=new byte[65544];
        using(var overreport=new Hostile(large,"overreport"))
            await Reject<InvalidDataException>(()=>NativeId65FreshWizardResearchResourceSource.RequireSnapshotIdentityAsync(
                overreport,large.Length,Convert.ToHexStringLower(SHA256.HashData(large)),CancellationToken.None));
        Console.WriteLine($"FRESH_WIZARD_RESOURCE_STREAM_OK checks={checks} issuerCreated=false");
        Task Guard(Stream s,CancellationToken t)=>NativeId65FreshWizardResearchResourceSource.RequireSnapshotIdentityAsync(s,32,hash,t);
        void Check(bool ok){if(!ok)throw new InvalidDataException("Stream control was not exercised.");checks++;}
        async Task Reject<T>(Func<Task> action)where T:Exception
        {try{await action();}catch(T){checks++;return;}throw new InvalidDataException("Expected "+typeof(T).Name);}
    }

    private sealed class Hostile : Stream
    {
        private readonly MemoryStream inner;
        private readonly string mode;
        private readonly CancellationTokenSource? cancel;
        private bool afterEof;
        internal Hostile(byte[] bytes,string mode="valid",CancellationTokenSource? cancel=null)
        {
            byte[] owned=bytes.ToArray();if(mode=="wrong-bytes")owned[0]^=1;
            inner=new(owned,false);this.mode=mode;this.cancel=cancel;
            if(mode=="position")inner.Position=1;
        }
        internal int Reads{get;private set;}
        internal int EofReads{get;private set;}
        public override bool CanRead=>true;
        public override bool CanSeek=>true;
        public override bool CanWrite=>false;
        public override long Length=>mode=="overreport"?inner.Length:mode=="oversize"?33:afterEof&&mode=="final-length"?33:32;
        public override long Position{get=>afterEof&&mode=="final-position"?33:inner.Position;set=>inner.Position=value;}
        public override int Read(byte[] buffer,int offset,int count)=>Read(buffer.AsSpan(offset,count));
        public override int Read(Span<byte> buffer)
        {
            Reads++;int n=inner.Read(buffer[..Math.Min(7,buffer.Length)]);
            if(mode=="mid-cancel")cancel!.Cancel();return mode=="overreport"?buffer.Length+1:n;
        }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken token=default)
        {token.ThrowIfCancellationRequested();return ValueTask.FromResult(Read(buffer.Span));}
        public override Task<int> ReadAsync(byte[] b,int o,int n,CancellationToken t)=>ReadAsync(b.AsMemory(o,n),t).AsTask();
        public override int ReadByte()
        {EofReads++;afterEof=true;if(mode=="late-cancel")cancel!.Cancel();return mode=="extra-eof"?7:inner.ReadByte();}
        public override long Seek(long offset,SeekOrigin origin)=>inner.Seek(offset,origin);
        public override void Flush()=>throw new NotSupportedException();
        public override void SetLength(long value)=>throw new NotSupportedException();
        public override void Write(byte[] b,int o,int n)=>throw new NotSupportedException();
        protected override void Dispose(bool disposing){if(disposing)inner.Dispose();base.Dispose(disposing);}
    }
}
