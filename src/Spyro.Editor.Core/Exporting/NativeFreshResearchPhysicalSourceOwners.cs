namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Complete Original physical owners for an already selected closed geometry.
/// This captures no generated payload and grants no executable, candidate or
/// publication authority. The concrete issuer owns admission and stream lifetime.
/// </summary>
internal sealed class NativeFreshResearchPhysicalSourceOwners
{
    private readonly NativeId65FreshDiscSource _original;
    private readonly NativeId65FreshWizardDiscGeometry _geometry;
    private readonly IReadOnlyDictionary<int,byte[]> _preimages;
    private readonly byte[] _root,_wad;
    private NativeFreshResearchPhysicalSourceOwners(NativeId65FreshDiscSource original,
        NativeId65FreshWizardDiscGeometry geometry,byte[] root,byte[] wad,Dictionary<int,byte[]> preimages)
    {
        // These arrays/dictionary are created only by Capture, never supplied
        // by a caller or returned directly from this immutable owner.
        _original=original;_geometry=geometry;_root=root;_wad=wad;
        _preimages=new System.Collections.ObjectModel.ReadOnlyDictionary<int,byte[]>(preimages);
    }
    internal IReadOnlyList<NativeFlatIsoFile> RootFiles=>_original.RootFiles;
    internal void RequireRawSector(int lba,ReadOnlySpan<byte> bytes)=>_original.RequireRawSector(lba,bytes);
    internal byte[] CopyRoot()=>_root.ToArray();
    internal byte[] CopyWadHeader()=>_wad.ToArray();
    internal byte[] CopyPreimage(int lba)=>_preimages.TryGetValue(lba,out var b)?b.ToArray():
        throw new InvalidDataException("No newly captured Wizard physical preimage owner.");
    internal void RequireRetainedGeometry(NativeId65FreshWizardDiscGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        RequireRetainedOriginalOwners(_original,geometry);
    }
    internal void RequireRetainedOriginalOwners(NativeId65FreshDiscSource original,
        NativeId65FreshWizardDiscGeometry geometry)
    {
        if(!ReferenceEquals(original,_original)||geometry.OverlayByteLength!=_geometry.OverlayByteLength||
            geometry.BodyByteLength!=_geometry.BodyByteLength||
            !_root.SequenceEqual(_original.CopyRoot())||!_wad.SequenceEqual(_original.CopyWadHeader())||
            !_preimages.Keys.Order().SequenceEqual(new[]{22,37}.Concat(Enumerable.Range(53874,geometry.FinalExecutableEndExclusive-53874))))
            throw new InvalidDataException("Retained Wizard physical source owners changed.");
        foreach(var owner in _preimages)_original.RequireRawSector(owner.Key,owner.Value);
    }

    internal static NativeFreshResearchPhysicalSourceOwners Capture(Stream image,NativeId65FreshDiscSource original,
        NativeId65FreshWizardDiscGeometry geometry,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(original);ArgumentNullException.ThrowIfNull(geometry);
        using var reader=new AuthenticatedReader(image,original,token);
        DiscLayout layout=DiscImage.DetectLayout(reader);
        if(layout!=new DiscLayout(2352,24,22,2048))throw new InvalidDataException("Wizard raw ISO geometry changed.");
        var rows=NativeWadGrowthSectorPreparation.ReadFlatRootFiles(reader,layout);
        if(!rows.SequenceEqual(original.RootFiles))throw new InvalidDataException("New authenticated ISO records differ from the actual original snapshot.");
        // The old capture checked only its old allocation. This new call
        // validates all metadata and every sector of this arm's complete
        // gap. The arithmetic record is never passed to a writer.
        _=NativeWadGrowthSectorPreparation.Create(reader,layout,geometry.CreateGrowthGeometry());
        byte[] root=DiscImage.ReadFileBytes(reader,layout,22,0,2048),wad=DiscImage.ReadFileBytes(reader,layout,37,0,2048);
        if(!root.SequenceEqual(original.CopyRoot())||!wad.SequenceEqual(original.CopyWadHeader()))
            throw new InvalidDataException("Complete original root/WAD owners changed during new physical capture.");
        var preimages=new Dictionary<int,byte[]>(geometry.ChangedSectorOwnerCount);
        foreach(int lba in new[]{22,37}.Concat(Enumerable.Range(53874,geometry.FinalExecutableEndExclusive-53874)))
        {
            token.ThrowIfCancellationRequested();byte[] raw=new byte[2352];reader.Position=lba*2352L;
            reader.ReadExactly(raw);preimages.Add(lba,raw);
        }
        if(preimages.Count!=geometry.ChangedSectorOwnerCount)throw new InvalidDataException("Wizard physical owner partition changed.");
        token.ThrowIfCancellationRequested();return new(original,geometry,root,wad,preimages);
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
