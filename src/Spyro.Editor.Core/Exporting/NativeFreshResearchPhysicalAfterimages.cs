using System.Buffers.Binary;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Shared exact physical transformation for concrete source-issued plans only.
/// The private payload snapshots are selected by closed typed factories, never
/// caller bytes, hashes or sector callbacks. No writer or publication is issued.
/// </summary>
internal sealed class NativeFreshResearchPhysicalAfterimages
{
    private static readonly DiscLayout Layout=new(2352,24,22,2048);
    private readonly NativeFreshResearchPhysicalSourceOwners _owners;
    private readonly NativeId65FreshWizardDiscGeometry _geometry;
    private readonly byte[] _body,_overlay,_executable,_wad,_root;
    private readonly byte _wadFlags,_exeFlags;
    private NativeFreshResearchPhysicalAfterimages(NativeFreshResearchPhysicalSourceOwners owners,
        NativeId65FreshWizardDiscGeometry geometry,byte[] body,byte[] overlay,byte[] executable)
    {
        // Every byte array is a fresh detached copy made by the closed factory.
        _owners=owners;_geometry=geometry;_body=body;_overlay=overlay;_executable=executable;
        if(_body.Length!=geometry.BodyByteLength||_overlay.Length!=geometry.OverlayByteLength||_executable.Length!=0x66000)
            throw new InvalidDataException("Actual physical payload snapshots differ from their closed source geometry.");
        _wad=owners.CopyWadHeader();_root=owners.CopyRoot();
        if(!_wad.AsSpan(79*8,16).SequenceEqual(new byte[16])||!_wad.AsSpan(81*8,8).SequenceEqual(Convert.FromHexString("0078C90500200000")))
            throw new InvalidDataException("Reserved Wizard WAD pair or adjacent occupied owner changed.");
        BinaryPrimitives.WriteInt32LittleEndian(_wad.AsSpan(79*8),geometry.OverlayWadOffset);
        BinaryPrimitives.WriteInt32LittleEndian(_wad.AsSpan(79*8+4),_overlay.Length);
        BinaryPrimitives.WriteInt32LittleEndian(_wad.AsSpan(80*8),geometry.BodyWadOffset);
        BinaryPrimitives.WriteInt32LittleEndian(_wad.AsSpan(80*8+4),_body.Length);
        foreach(var row in owners.RootFiles.Where(r=>r.Name is "WAD.WAD" or "SCUS_942.28"))
            PatchRoot(_root,row,row.Name=="WAD.WAD"?37:geometry.FinalExecutableLba,row.Name=="WAD.WAD"?geometry.FinalWadByteLength:0x66000);
        _wadFlags=(byte)(owners.CopyPreimage(53874)[18]&0x81);_exeFlags=(byte)(owners.CopyPreimage(54078)[18]&0x81);
    }
    internal static NativeFreshResearchPhysicalAfterimages ForWizard(NativeId65FreshWizardResearchDiscSource source)
    {
        ArgumentNullException.ThrowIfNull(source);source.RequireAncestry();
        var executable=source.Executable;var body=executable.BodyAncestor;
        return new(source.PhysicalOwners,source.Geometry,source.FamilyCompositionAncestor?.CopyLevelData()??body.CopyLevelData(),
            body.CopyOverlay(),executable.CopyExecutable());
    }
    internal static NativeFreshResearchPhysicalAfterimages ForChest(NativeFreshResearchPhysicalSourceOwners owners,
        NativeFreshLockedChestExperimentalExecutable executable)
    {
        ArgumentNullException.ThrowIfNull(owners);ArgumentNullException.ThrowIfNull(executable);
        var geometry=NativeId65FreshWizardDiscGeometry.CompileChest(executable);
        owners.RequireRetainedGeometry(geometry);
        return new(owners,geometry,executable.PlacementAncestor.CopyLevelData(),
            executable.HostAncestor.CopyOverlay(),executable.CopyExecutable());
    }
    internal byte[] CopyBody()=>_body.ToArray();
    internal byte[] CopyOverlay()=>_overlay.ToArray();
    internal byte[] CopyExecutable()=>_executable.ToArray();
    internal byte[] CopyWadDirectory()=>_wad.ToArray();
    internal byte[] CopyIsoRootDirectory()=>_root.ToArray();

    internal static void PatchRoot(Span<byte> root,NativeFlatIsoFile row,int lba,int length)
    {
        int at=row.RecordOffset;
        if(row.IsDirectory||row.Name is not("WAD.WAD" or "SCUS_942.28")||lba<0||length<=0||at<0||at>root.Length-34||
            BinaryPrimitives.ReadInt32LittleEndian(root[(at+2)..])!=row.Lba||BinaryPrimitives.ReadInt32BigEndian(root[(at+6)..])!=row.Lba||
            BinaryPrimitives.ReadInt32LittleEndian(root[(at+10)..])!=row.Size||BinaryPrimitives.ReadInt32BigEndian(root[(at+14)..])!=row.Size)
            throw new InvalidDataException("Wizard ISO fields lost exact bounded both-endian preimages.");
        BinaryPrimitives.WriteInt32LittleEndian(root[(at+2)..],lba);BinaryPrimitives.WriteInt32BigEndian(root[(at+6)..],lba);
        BinaryPrimitives.WriteInt32LittleEndian(root[(at+10)..],length);BinaryPrimitives.WriteInt32BigEndian(root[(at+14)..],length);
    }
    internal byte[] CreateExpectedRawSector(int lba,ReadOnlySpan<byte> sourceSector)
    {
        _owners.RequireRawSector(lba,sourceSector);byte[] before=sourceSector.ToArray();
        if(!IsChangedOwner(lba))return before;
        byte[] after=lba>=54079?RawMode2Form1SectorIntegrity.PrepareOwnedZeroPaddingSector(before,Layout,lba):before.ToArray();
        RawMode2Form1SectorIntegrity.RequireLogicalWriteSector(after,Layout,lba);
        if(lba==22)_root.CopyTo(after,24);
        else if(lba==37)_wad.CopyTo(after,24);
        else if(lba>=53875&&lba<_geometry.FinalExecutableLba)
        {
            int at=checked((lba-53875)*2048);
            if(at<_overlay.Length)_overlay.AsSpan(at,2048).CopyTo(after.AsSpan(24,2048));
            else _body.AsSpan(at-_overlay.Length,2048).CopyTo(after.AsSpan(24,2048));
        }
        else if(lba>=_geometry.FinalExecutableLba)_executable.AsSpan((lba-_geometry.FinalExecutableLba)*2048,2048).CopyTo(after.AsSpan(24,2048));
        int flags=lba==22?before[18]&0x81:lba==_geometry.FinalExecutableLba-1?_wadFlags:lba==_geometry.FinalExecutableEndExclusive-1?_exeFlags:0;
        after[18]=after[22]=(byte)((after[18]&~0x81)|flags);
        RawMode2Form1SectorIntegrity.RebuildLogicalWriteSector(after,Layout,lba);return after;
    }
    internal byte[] CreateOriginalRawSector(int lba,ReadOnlySpan<byte> finalSector)
    {
        if(lba<0||lba>=281270||finalSector.Length!=2352)throw new InvalidDataException("Wizard inverse requires one bounded physical sector.");
        if(!IsChangedOwner(lba)){_owners.RequireRawSector(lba,finalSector);return finalSector.ToArray();}
        byte[] before=_owners.CopyPreimage(lba);
        if(!finalSector.SequenceEqual(CreateExpectedRawSector(lba,before)))throw new InvalidDataException("Wizard inverse afterimage payload/header/parity changed.");
        return before;
    }
    internal NativeId65FreshDiscVerification VerifyImage(Stream original,Stream output,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();ArgumentNullException.ThrowIfNull(original);ArgumentNullException.ThrowIfNull(output);
        var result=NativeTownSquareClosurePhysicalReadback.Verify(original,output,NativeId65FreshDiscSource.ImageByteLength,
            NativeId65FreshDiscSource.OriginalSha256,(lba,before)=>
        {
            token.ThrowIfCancellationRequested();long position=(lba+1)*2352L;
            if(original.Length!=NativeId65FreshDiscSource.ImageByteLength||output.Length!=NativeId65FreshDiscSource.ImageByteLength||
                original.Position!=position||output.Position!=position)throw new InvalidDataException("Wizard physical readback length/position changed during a sector read.");
            return CreateExpectedRawSector(lba,before);
        });
        NativeId65FreshDiscSource.RequireActualEof(original,token);NativeId65FreshDiscSource.RequireActualEof(output,token);
        token.ThrowIfCancellationRequested();return new(result.Hash,result.Changed);
    }
    private bool IsChangedOwner(int lba)=>lba is 22 or 37||lba>=53874&&lba<_geometry.FinalExecutableEndExclusive;
}
