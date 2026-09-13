using System.Buffers.Binary;
using Spyro.Editor.Core.Analysis;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Shared neutral model-table grammar for retail and authored headers. Decoding
/// bytes does not authenticate a source, reserve model storage, establish PETE
/// compatibility, or grant native import authority. Those remain issuer duties.
/// Slot0 is the player; the generic loader consumes a positive prefix of63 slots.
/// </summary>
internal sealed class NativeMobyRegistrationTableLayout
{
    private readonly byte[] _header;
    private readonly int _modelOffset, _modelLength;

    private NativeMobyRegistrationTableLayout(ReadOnlySpan<byte> header,int modelOffset,int modelLength)
    {
        if(header.Length!=0x800||modelOffset<0x800||modelLength<=0||
            (modelOffset|modelLength)%4!=0||(long)modelOffset+modelLength>int.MaxValue)
            throw new InvalidDataException("Native registration grammar needs a complete header and bounded aligned model extent.");
        _header=header.ToArray();_modelOffset=modelOffset;_modelLength=modelLength;
        List<NativeMobySourceRegistration> roots=[];HashSet<ushort> classes=[];bool ended=false;uint previous=0;
        for(int i=0;i<64;i++)
        {
            uint root=BinaryPrimitives.ReadUInt32LittleEndian(header[(0x50+i*4)..]);
            ushort cls=BinaryPrimitives.ReadUInt16LittleEndian(header[(0x150+i*2)..]);
            if(i==0&&(root==0||cls!=0))throw new InvalidDataException("Native slot 0 must retain the special Spyro stream.");
            if(root==0){ended=true;if(cls!=0)throw new InvalidDataException("Native root terminator retains a class ID.");continue;}
            if(ended)throw new InvalidDataException("Native model registrations must form a contiguous positive prefix.");
            if(root>int.MaxValue)throw new InvalidDataException("Native loader requires positive signed root offsets.");
            if(i!=0&&cls==0)throw new InvalidDataException("Generic registrations cannot replace special class 0.");
            if(cls>=512)throw new InvalidDataException("Native class identity exceeds the 512-pointer table.");
            if(root<modelOffset||root+4L>modelOffset+(long)modelLength||root%4!=0)
                throw new InvalidDataException("Native registration points outside its aligned model owner.");
            if(root<=previous||!classes.Add(cls))throw new InvalidDataException("Native roots must be ascending and class identities unique.");
            roots.Add(new(i,cls,root));previous=root;
        }
        Registrations=Array.AsReadOnly(roots.ToArray());
    }

    internal IReadOnlyList<NativeMobySourceRegistration> Registrations{get;}
    internal byte[] CopyHeader()=>_header.ToArray();
    internal static NativeMobyRegistrationTableLayout Decode(ReadOnlySpan<byte> header,int modelOffset,int modelLength)=>
        new(header,modelOffset,modelLength);

    internal NativeMobyRegistrationTableLayout Append(ushort actorId,uint root)
    {
        if(Registrations.Count==64||actorId==0||actorId>=512||Registrations.Any(r=>r.ActorId==actorId)||
            root<=Registrations[^1].RootDataOffset||root%4!=0||root>int.MaxValue||
            root<_modelOffset||root+4L>_modelOffset+(long)_modelLength)
            throw new InvalidDataException("Native registration append requires a free generic slot, unique class and ascending owned model root.");
        byte[] header=CopyHeader();int slot=Registrations.Count;
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0x50+slot*4),root);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(0x150+slot*2),actorId);
        return new(header,_modelOffset,_modelLength);
    }
}
