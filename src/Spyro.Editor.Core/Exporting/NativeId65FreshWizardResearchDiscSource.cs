using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Spyro.Editor.NativeId65FreshWizardDiscSmoke")]

namespace Spyro.Editor.Core.Exporting;

/// <summary>Actual-EXE source capture with one retained original per-LBA index and a newly validated larger gap.</summary>
internal sealed class NativeId65FreshWizardResearchDiscSource
{
    private readonly NativeId65FreshDiscSource _original;
    private readonly IReadOnlyDictionary<int,byte[]> _preimages;
    private readonly byte[] _root,_wad;
    private NativeId65FreshWizardResearchDiscSource(NativeId65FreshDiscSource original,
        NativeId65FreshWizardResearchExecutablePlan executable,byte[] root,byte[] wad,
        Dictionary<int,byte[]> preimages)
    {
        _original=original;Executable=executable;_root=root;_wad=wad;
        _preimages=new System.Collections.ObjectModel.ReadOnlyDictionary<int,byte[]>(preimages);
    }
    internal NativeId65FreshWizardResearchExecutablePlan Executable{get;}
    internal IReadOnlyList<NativeFlatIsoFile> RootFiles=>_original.RootFiles;
    internal byte[] CopyRoot()=>_root.ToArray();
    internal byte[] CopyWadHeader()=>_wad.ToArray();
    internal byte[] CopyPreimage(int lba)=>_preimages.TryGetValue(lba,out var b)?b.ToArray():
        throw new InvalidDataException("No newly captured Wizard physical preimage owner.");
    internal void RequireRawSector(int lba,ReadOnlySpan<byte> bytes)=>_original.RequireRawSector(lba,bytes);
    internal static void RequireExecutable(NativeId65FreshWizardResearchExecutablePlan executable)
    {
        ArgumentNullException.ThrowIfNull(executable);
        if(executable.IsMaterialAuthored)
            throw new InvalidDataException("Material-authored Wizard executable disc composition is not enabled.");
        var body=executable.BodyAncestor;
        executable.RequireAncestry(body);executable.RequireExactExecutable(executable.CopyExecutable());
        NativeId65FreshWizardResearchExecutableSource.RequireBody(body);
        var g=NativeId65FreshWizardDiscGeometry.Compile(body.CopyOverlay().Length,body.CopyLevelData().Length);
        if(executable.CopyExecutable().Length!=0x66000||body.CopyBufferAddress!=0x8007AA38u+g.OverlayByteLength||
            body.ModelRuntimeAddress!=body.CopyBufferAddress+0x8FC||body.SceneRuntimeAddress!=body.ModelRuntimeAddress+0x19000||
            body.KnownSceneEndAddress!=body.SceneRuntimeAddress+0x3000||body.KnownSceneEndAddress>body.NormalReservedLowerAddress)
            throw new InvalidDataException("Wizard disc requires the complete actual fixed EXE/Body/Host layout.");
    }
    internal static Task<NativeId65FreshWizardResearchDiscSource> CaptureAsync(Stream image,
        NativeId65FreshWizardResearchExecutablePlan executable,CancellationToken token)=>CaptureCoreAsync(image,executable,null,token);
    // Only a mutation trigger after the actual old source/index capture. It
    // cannot submit an index, bytes, metadata, hashes or an issued source object.
    internal static Task<NativeId65FreshWizardResearchDiscSource> CaptureForSmokeAsync(Stream image,
        NativeId65FreshWizardResearchExecutablePlan executable,Action? afterOriginalCapture,CancellationToken token)
        =>CaptureCoreAsync(image,executable,afterOriginalCapture,token);
    private static async Task<NativeId65FreshWizardResearchDiscSource> CaptureCoreAsync(Stream image,
        NativeId65FreshWizardResearchExecutablePlan executable,Action? afterOriginalCapture,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();ArgumentNullException.ThrowIfNull(image);RequireExecutable(executable);
        await NativeId65FreshDiscSource.RequireWholeOriginalAsync(image,token);
        try
        {
            var original=await NativeId65FreshDiscSource.CaptureAsync(image,token);
            if(!executable.RevertExact(executable.CopyExecutable()).SequenceEqual(original.CopyExecutable()))
                throw new InvalidDataException("Actual retained EXE is not bound to this authenticated original executable.");
            afterOriginalCapture?.Invoke();token.ThrowIfCancellationRequested();
            using var reader=new AuthenticatedReader(image,original,token);
            DiscLayout layout=DiscImage.DetectLayout(reader);
            if(layout!=new DiscLayout(2352,24,22,2048))throw new InvalidDataException("Wizard raw ISO geometry changed.");
            var rows=NativeWadGrowthSectorPreparation.ReadFlatRootFiles(reader,layout);
            if(!rows.SequenceEqual(original.RootFiles))throw new InvalidDataException("New authenticated ISO records differ from the actual original snapshot.");
            var geometry=NativeId65FreshWizardDiscGeometry.Compile(0x11000,0x20C800);
            // The old capture checked only its old allocation. This new call
            // validates all metadata and every gap sector through55162, including
            // the extra37. The geometry record is never passed to a writer.
            _=NativeWadGrowthSectorPreparation.Create(reader,layout,geometry.CreateGrowthGeometry());
            byte[] root=DiscImage.ReadFileBytes(reader,layout,22,0,2048),wad=DiscImage.ReadFileBytes(reader,layout,37,0,2048);
            if(!root.SequenceEqual(original.CopyRoot())||!wad.SequenceEqual(original.CopyWadHeader()))
                throw new InvalidDataException("Complete original root/WAD owners changed during new physical capture.");
            var preimages=new Dictionary<int,byte[]>(1290);
            foreach(int lba in new[]{22,37}.Concat(Enumerable.Range(53874,1288)))
            {
                token.ThrowIfCancellationRequested();byte[] raw=new byte[2352];reader.Position=lba*2352L;
                reader.ReadExactly(raw);preimages.Add(lba,raw);
            }
            if(preimages.Count!=1290)throw new InvalidDataException("Wizard physical owner partition changed.");
            token.ThrowIfCancellationRequested();return new(original,executable,root,wad,preimages);
        }
        finally
        {
            await NativeId65FreshDiscSource.RequireWholeOriginalAsync(image,CancellationToken.None);
            token.ThrowIfCancellationRequested();
        }
    }
    // All contributing later physical reads authenticate before exposing even a
    // requested logical fragment. This non-owning wrapper does not close source.
    private sealed class AuthenticatedReader(Stream source,NativeId65FreshDiscSource original,CancellationToken token):Stream
    {
        private long _position;
        public override bool CanRead=>true;
        public override bool CanSeek=>true;
        public override bool CanWrite=>false;
        public override long Length{get{Guard();return NativeId65FreshDiscSource.ImageByteLength;}}
        public override long Position{get=>_position;set{Guard();if(value<0||value>NativeId65FreshDiscSource.ImageByteLength)throw new InvalidDataException("Authenticated physical seek escaped source.");_position=value;}}
        private void Guard()
        {
            token.ThrowIfCancellationRequested();
            if(!source.CanRead||!source.CanSeek||source.Length!=NativeId65FreshDiscSource.ImageByteLength)
                throw new InvalidDataException("Authenticated physical source length/readability changed.");
        }
        public override int Read(byte[] b,int at,int n)=>Read(b.AsSpan(at,n));
        public override int Read(Span<byte> destination)
        {
            Guard();int count=(int)Math.Min(destination.Length,Length-_position),copied=0;
            while(copied<count)
            {
                Guard();int lba=checked((int)(_position/2352)),offset=(int)(_position%2352);
                int part=Math.Min(2352-offset,count-copied);byte[] raw=new byte[2352];
                source.Position=lba*2352L;source.ReadExactly(raw);Guard();
                if(source.Position!=(lba+1)*2352L)throw new InvalidDataException("Authenticated physical read position changed.");
                original.RequireRawSector(lba,raw);raw.AsSpan(offset,part).CopyTo(destination[copied..]);
                _position+=part;copied+=part;
            }
            Guard();return count;
        }
        public override ValueTask<int> ReadAsync(Memory<byte> b,CancellationToken cancellationToken=default)
        {cancellationToken.ThrowIfCancellationRequested();return ValueTask.FromResult(Read(b.Span));}
        public override long Seek(long offset,SeekOrigin origin)
        {Position=checked((origin switch{SeekOrigin.Begin=>0,SeekOrigin.Current=>Position,SeekOrigin.End=>Length,_=>throw new ArgumentOutOfRangeException(nameof(origin))})+offset);return Position;}
        public override void Flush()=>throw new NotSupportedException();
        public override void SetLength(long n)=>throw new NotSupportedException();
        public override void Write(byte[] b,int at,int n)=>throw new NotSupportedException();
    }
}

/// <summary>Fixed arithmetic only; not source, payload or gap ownership.</summary>
internal sealed class NativeId65FreshWizardDiscGeometry
{
    private NativeId65FreshWizardDiscGeometry(){}
    internal bool SourceCertified=>false;
    internal int OverlayWadOffset=>0x6927000;
    internal int OverlayByteLength=>0x11000;
    internal int BodyWadOffset=>checked(OverlayWadOffset+OverlayByteLength);
    internal int BodyByteLength=>0x20C800;
    internal int GrowthByteLength=>checked(OverlayByteLength+BodyByteLength);
    internal int FinalWadByteLength=>checked(OverlayWadOffset+GrowthByteLength);
    internal int FinalExecutableLba=>checked(53875+GrowthByteLength/2048);
    internal int FinalExecutableEndExclusive=>checked(FinalExecutableLba+204);
    internal static NativeId65FreshWizardDiscGeometry Compile(int overlay,int body)
    {
        if(overlay!=0x11000||body!=0x20C800)throw new InvalidDataException("Only the current actual Wizard Host11000/Body20C800 allocation is supported.");
        return new();
    }
    internal NativeSkyWadRelocationPlan CreateGrowthGeometry()=>new(true,37,0x6927000,FinalWadByteLength,GrowthByteLength,
        53875,FinalExecutableLba,0x66000,60000,checked((60000-54079)*2048),new Dictionary<int,int>(),new Dictionary<int,long>(),Array.Empty<NativeSkyRelocatedPatch>());
}
