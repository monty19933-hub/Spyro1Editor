using System.Buffers.Binary;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Immutable neutral package bytes. An input instance is its retry-sharing
/// identity, not an authenticated native source or a complete resource closure.
/// Only the actual family adapter may supply these to a native composition.
/// </summary>
internal sealed class NativeModelTailPackage
{
    private readonly byte[] _bytes;
    internal NativeModelTailPackage(ushort actorId,string id,ReadOnlySpan<byte> bytes)
    {
        if(actorId==0||actorId>=512||string.IsNullOrWhiteSpace(id)||id.Length>1024||id.Any(char.IsControl)||
            bytes.Length<4||bytes.Length%4!=0||bytes.Length>NativeModelTailBatch.MaximumPayloadBytes)
            throw new InvalidDataException("A model-tail input requires a generic class, bounded label and complete aligned package bytes.");
        ActorId=actorId;Id=id;_bytes=bytes.ToArray();
    }
    internal ushort ActorId{get;}
    internal string Id{get;}
    internal int ByteLength=>_bytes.Length;
    internal byte[] CopyBytes()=>_bytes.ToArray();
}

internal sealed record NativeModelTailBatchEntry(NativeModelTailPackage Input,int Slot,int PackageOffset,
    int ByteLength,int RootFieldOffset,int ClassFieldOffset);

/// <summary>
/// Neutral append transaction over a complete current body, including generated
/// Fresh bytes. It never recaptures that body as an Original native source or
/// issues an address domain, source certificate, runtime or export capability.
/// The fixed declaration/shared-class/payload limits are input-safety ceilings,
/// not native demand, memory-fit, behavior or import approval.
/// </summary>
internal sealed class NativeModelTailBatch
{
    internal const int MaximumRequestDeclarations=4096,MaximumSharedClasses=64,MaximumPayloadBytes=0x800000;
    private readonly byte[] _current,_append,_final;
    private readonly NativeLevelDataGrowthLayout _growth;

    private NativeModelTailBatch(byte[] current,NativeModelTailPackage[] requests,ushort[] sharedClasses)
    {
        _current=current;
        if(sharedClasses.Any(c=>c>=512)||sharedClasses.Distinct().Count()!=sharedClasses.Length||requests.Any(p=>p is null))
            throw new InvalidDataException("Model-tail requests or bounded shared-class identities are missing, repeated or invalid.");
        uint rawOffset=Word(current,0x10),rawLength=Word(current,0x14);
        if(rawOffset>int.MaxValue||rawLength>int.MaxValue||rawOffset<0x800||rawLength==0||
            rawOffset+(long)rawLength>current.Length)
            throw new InvalidDataException("The current model allocation is outside its complete bounded body.");
        int modelOffset=(int)rawOffset,modelLength=(int)rawLength;
        var table=NativeMobyRegistrationTableLayout.Decode(current.AsSpan(0,0x800),modelOffset,modelLength);
        var shared=sharedClasses.ToHashSet();
        if(table.Registrations.Skip(1).Any(r=>shared.Contains(r.ActorId)))
            throw new InvalidDataException("An existing local registration collides with the supplied shared-class owner.");

        List<NativeModelTailPackage> unique=[];
        HashSet<NativeModelTailPackage> seen=new(ReferenceEqualityComparer.Instance);
        HashSet<ushort> classes=[];HashSet<string> ids=new(StringComparer.Ordinal);long byteCount=0;
        foreach(var input in requests)
        {
            if(!seen.Add(input))continue;
            if(!classes.Add(input.ActorId)||!ids.Add(input.Id))
                throw new InvalidDataException("Another package input aliases an existing class or label; equal reissued owners cannot auto-merge.");
            if(shared.Contains(input.ActorId)||table.Registrations.Any(r=>r.ActorId==input.ActorId))
                throw new InvalidDataException("A model-tail input collides with an existing local or supplied shared-class registration.");
            if((byteCount+=input.ByteLength)>MaximumPayloadBytes)
                throw new InvalidDataException("The model-tail aggregate exceeds its eight-MiB input-safety payload ceiling.");
            unique.Add(input);
        }
        if(table.Registrations.Count+unique.Count>64)
            throw new InvalidDataException("The model-tail batch exceeds63 generic slots plus the retained special player.");
        _append=new byte[(int)byteCount];int copied=0;
        foreach(var input in unique){input.CopyBytes().CopyTo(_append,copied);copied+=input.ByteLength;}

        // The existing outer grammar owns all descriptor and old-root shifts.
        // A zero-width tail payload also validates the complete no-op geometry.
        // Its locator fields are deliberately unused: this is not a WAD writer.
        _growth=new(current.AsSpan(0,0x800),current.Length,
            [new(0,0,0,0,modelLength,0,_append,2,false)]);
        ModelGrowthBytes=_growth.Subfiles[2].Growth;FinalModelLength=_growth.Subfiles[2].FinalLength;
        if(ModelGrowthBytes!=checked(((int)byteCount+0x7FF)&~0x7FF)||
            _growth.Subfiles[2].FinalOffset!=modelOffset||_growth.Growth!=ModelGrowthBytes)
            throw new InvalidDataException("Shared outer geometry disagrees with the single aggregate model-tail allocation.");
        _final=_growth.Expand(current);
        var appendedTable=NativeMobyRegistrationTableLayout.Decode(_final.AsSpan(0,0x800),modelOffset,FinalModelLength);
        List<NativeModelTailBatchEntry> entries=[];int cursor=checked(modelOffset+modelLength),slot=table.Registrations.Count;
        foreach(var input in unique)
        {
            int rootField=0x50+slot*4,classField=0x150+slot*2;
            if(Word(_final,rootField)!=0||BinaryPrimitives.ReadUInt16LittleEndian(_final.AsSpan(classField))!=0)
                throw new InvalidDataException("A new model-tail registration field is already occupied.");
            appendedTable=appendedTable.Append(input.ActorId,(uint)cursor);
            entries.Add(new(input,slot,cursor,input.ByteLength,rootField,classField));
            cursor=checked(cursor+input.ByteLength);slot++;
        }
        appendedTable.CopyHeader().CopyTo(_final,0);Packages=entries.AsReadOnly();
        if(!_final.AsSpan(modelOffset,modelLength).SequenceEqual(current.AsSpan(modelOffset,modelLength))||
            !_final.AsSpan(modelOffset+modelLength,_append.Length).SequenceEqual(_append)||
            _final.AsSpan(cursor,modelOffset+FinalModelLength-cursor).ContainsAnyExcept((byte)0))
            throw new InvalidDataException("The model-tail output changed an old owner, package payload or new alignment complement.");
        _=RevertExact(_final);
    }

    internal int ModelGrowthBytes{get;}
    internal int FinalModelLength{get;}
    internal IReadOnlyList<NativeModelTailBatchEntry> Packages{get;}
    internal byte[] CopyAppendBytes()=>_append.ToArray();
    internal byte[] CopyFinalBody()=>_final.ToArray();
    internal void RequireExactCurrentBody(ReadOnlySpan<byte> bytes)
    {
        if(!bytes.SequenceEqual(_current))
            throw new InvalidDataException("Model-tail composition requires its complete exact current-body preimage and complement.");
    }
    internal byte[] RevertExact(ReadOnlySpan<byte> bytes)
    {
        if(!bytes.SequenceEqual(_final))
            throw new InvalidDataException("Model-tail inverse requires the complete exact afterimage, including every package and complement byte.");
        byte[] inverse=new byte[_current.Length];bytes[..0x800].CopyTo(inverse);
        foreach(var subfile in _growth.Subfiles)
        {
            bytes.Slice(subfile.FinalOffset,subfile.OriginalLength).CopyTo(inverse.AsSpan(subfile.OriginalOffset));
            Put(inverse,subfile.Index*8,(uint)subfile.OriginalOffset);Put(inverse,subfile.Index*8+4,(uint)subfile.OriginalLength);
        }
        foreach(var package in Packages)
        {
            _current.AsSpan(package.RootFieldOffset,4).CopyTo(inverse.AsSpan(package.RootFieldOffset));
            _current.AsSpan(package.ClassFieldOffset,2).CopyTo(inverse.AsSpan(package.ClassFieldOffset));
        }
        if(!inverse.AsSpan().SequenceEqual(_current))
            throw new InvalidDataException("Model-tail inverse did not recover its complete current body and registration complement.");
        return inverse;
    }
    internal static NativeModelTailBatch Compile(ReadOnlySpan<byte> currentBody,
        IEnumerable<NativeModelTailPackage> requests,IEnumerable<ushort> sharedClasses)
    {
        ArgumentNullException.ThrowIfNull(requests);ArgumentNullException.ThrowIfNull(sharedClasses);
        // Bound the retained snapshot as input storage, not as a runtime claim.
        if(currentBody.Length<0x2800||currentBody.Length%0x800!=0||currentBody.Length>MaximumPayloadBytes)
            throw new InvalidDataException("Model-tail composition needs a complete sector-aligned current body within its eight-MiB input ceiling.");
        byte[] current=currentBody.ToArray();
        return new(current,Snapshot(requests,MaximumRequestDeclarations,"package declarations"),
            Snapshot(sharedClasses,MaximumSharedClasses,"shared classes"));
    }
    private static T[] Snapshot<T>(IEnumerable<T> values,int maximum,string role)
    {
        List<T> result=[];
        foreach(var value in values)
        {
            if(result.Count==maximum)throw new InvalidDataException("Model-tail "+role+" exceed their bounded input-safety ceiling.");
            result.Add(value);
        }
        return result.ToArray();
    }
    private static uint Word(ReadOnlySpan<byte> bytes,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(bytes[at..]);
    private static void Put(Span<byte> bytes,int at,uint value)=>BinaryPrimitives.WriteUInt32LittleEndian(bytes[at..],value);
}
