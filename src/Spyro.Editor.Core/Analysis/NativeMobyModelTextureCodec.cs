using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Spyro.Editor.Core.Analysis;

/// <summary>One actual renderer command's twelve texture bytes, not an inferred pointer.</summary>
public sealed class NativeMobyModelTextureReference
{
    private readonly byte[] _bytes;
    internal NativeMobyModelTextureReference(int offset,ReadOnlySpan<byte> bytes)
    {PackageOffset=offset;_bytes=bytes.ToArray();}
    public int PackageOffset { get; }
    public ushort Palette=>BinaryPrimitives.ReadUInt16LittleEndian(_bytes.AsSpan(2));
    public ushort Page=>BinaryPrimitives.ReadUInt16LittleEndian(_bytes.AsSpan(6));
    public byte[] CopyBytes()=>_bytes.ToArray();
}

/// <summary>
/// Bounded animated-model texture grammar only. Does not certify arbitrary model
/// animation, vertex, collision, sound or runtime-pointer closure. Source-native
/// issuers must separately bind package intervals and loader/renderer evidence.
/// </summary>
public sealed class NativeMobyModelTextureLayout
{
    private NativeMobyModelTextureLayout(string hash,IEnumerable<int> tables,IEnumerable<NativeMobyModelTextureReference> references)
    {PackageSha256=hash;FaceTableOffsets=Array.AsReadOnly(tables.Order().ToArray());References=Array.AsReadOnly(references.OrderBy(r=>r.PackageOffset).ToArray());}
    public string PackageSha256 { get; }
    public IReadOnlyList<int> FaceTableOffsets { get; }
    public IReadOnlyList<NativeMobyModelTextureReference> References { get; }
    public bool CompleteModelClosureProven=>false;
    public bool PublicationApproved=>false;

    public static NativeMobyModelTextureLayout Decode(ReadOnlySpan<byte> package)
    {
        if(package.Length<0x40||package.Length>0x200000||package.Length%4!=0)
            throw new InvalidDataException("Moby texture package has no bounded aligned animated owner.");
        int animations=Int(package,0);
        if(animations<1||animations>1024||0x38L+animations*4L>package.Length)
            throw new InvalidDataException("Moby texture codec requires a bounded animated model; simple models need another schema.");
        int modelData=Int(package,0x34);Range(modelData,4,package.Length);
        List<(int Offset,int Length)> headers=[(0,0x38+animations*4)];HashSet<int> faceStarts=[];
        for(int i=0;i<animations;i++)
        {
            uint raw=Word(package,0x38+i*4);if(raw==uint.MaxValue)continue;
            int root=Checked(raw);Range(root,0x24,package.Length);
            int frameCount=BinaryPrimitives.ReadInt16LittleEndian(package[root..]);
            if(frameCount<0)throw new InvalidDataException("Moby animation frame count is outside its source owner.");
            // Native loader 80013518 selects packed single-word frames for a
            // nonzero +4 flag, and paired words for the zero-flag stream.
            int stride=package[root+4]==0?8:4;
            int length=checked(0x24+frameCount*stride);Range(root,length,package.Length);
            // Exact animation aliases are legal; partial overlaps or aliases into
            // the package header are not known ownership in this texture schema.
            if(headers.Any(h=>Overlaps(root,length,h.Offset,h.Length)&&(h.Offset!=root||h.Length!=length)))
                throw new InvalidDataException("Moby animation/header owners partially overlap.");
            if(!headers.Contains((root,length)))headers.Add((root,length));
            faceStarts.Add(Relative(modelData,Int(package,root+0x14),package.Length));
            int low=Int(package,root+0x1C);if(low!=0)faceStarts.Add(Relative(modelData,low,package.Length));
        }
        List<(int Offset,int Length)> tables=[];List<NativeMobyModelTextureReference> refs=[];
        foreach(int start in faceStarts.Order())
        {
            Range(start,4,package.Length);int length=Int(package,start);
            if(length>0x20000||length%4!=0)throw new InvalidDataException("Moby face table length exceeds its bounded renderer schema.");
            int extent=checked(length+4);Range(start,extent,package.Length);
            if(headers.Concat(tables).Any(h=>Overlaps(start,extent,h.Offset,h.Length)))
                throw new InvalidDataException("Moby face table overlaps another owned table/header.");
            tables.Add((start,extent));int cursor=start+4,end=start+extent;
            while(cursor<end)
            {
                Range(cursor,8,end);uint command=Word(package,cursor);int size,field;
                if((command&0x80000000)==0){size=(command&2)!=0?20:8;field=(command&2)!=0?cursor+8:-1;}
                else if((command&4)!=0){size=20;field=cursor+8;}
                else{size=(command&2)!=0?24:12;field=(command&2)!=0?cursor+12:-1;}
                Range(cursor,size,end);
                if(field>=0)
                {
                    ushort page=BinaryPrimitives.ReadUInt16LittleEndian(package[(field+6)..]);
                    if(((page>>7)&3)!=0)throw new InvalidDataException("Moby texture relocation currently requires native 4-bpp descriptors.");
                    refs.Add(new(field,package.Slice(field,12)));
                }
                cursor+=size;
            }
        }
        if(tables.Count==0)throw new InvalidDataException("No owned Moby renderer face table was found.");
        return new(Convert.ToHexStringLower(SHA256.HashData(package)),faceStarts,refs);
    }

    internal static byte[] Rebase(NativeMobyModelTextureReference reference,int deltaU,int deltaV,ushort palette,ushort page)
    {
        byte[] bytes=reference.CopyBytes();
        if(((page>>7)&3)!=0||(page&~0x1F)!=(reference.Page&~0x1F))
            throw new InvalidDataException("Texture relocation changed native depth/material bits.");
        foreach(int offset in new[]{0,4,8,10})
        {
            int u=bytes[offset]+deltaU,v=bytes[offset+1]+deltaV;
            if(u<0||u>255||v<0||v>255)throw new InvalidDataException("Rebased Moby UV exceeds its native texture page.");
            bytes[offset]=(byte)u;bytes[offset+1]=(byte)v;
        }
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2),palette);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6),page);return bytes;
    }
    private static bool Overlaps(int a,int n,int b,int m)=>a<(long)b+m&&b<(long)a+n;
    private static int Relative(int root,int offset,int owner)
    {long target=root+(long)offset;if(target>int.MaxValue)throw new InvalidDataException("Moby texture relative target exceeds its package owner.");Range((int)target,4,owner);return(int)target;}
    private static void Range(int offset,int length,int owner)
    {if(offset<0||length<0||offset%4!=0||offset+(long)length>owner)throw new InvalidDataException("Moby texture command/header exceeds its aligned package owner.");}
    private static int Int(ReadOnlySpan<byte>b,int offset)=>Checked(Word(b,offset));
    private static int Checked(uint value)=>value<=int.MaxValue?(int)value:throw new InvalidDataException("Moby texture pointer/count exceeds its package owner.");
    private static uint Word(ReadOnlySpan<byte>b,int offset)=>BinaryPrimitives.ReadUInt32LittleEndian(b[offset..]);
}
