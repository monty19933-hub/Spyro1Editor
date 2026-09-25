using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Spyro.Editor.Core.Exporting;

internal static class NativePackedResearchEntryComposer
{
    internal static NativeWizardResearchPackedWad ComposeDark(ReadOnlySpan<byte> header,int originalLength,
        IEnumerable<NativeWizardResearchReplacement> replacements,Func<int,int,byte[]> read)=>Compose(header,originalLength,replacements,read,13,14);
    internal static NativeWizardResearchPackedWad ComposeArtisans(ReadOnlySpan<byte> header,int originalLength,
        NativeJapaneseEggArtisansIntegrationPlan integration,Func<int,int,byte[]> read)
    {
        ArgumentNullException.ThrowIfNull(integration);
        if(!integration.SourceCertified||header.Length!=0x800)throw new InvalidDataException("Artisans packing requires actual source-issued integration and directory.");
        byte[] originalOverlay=integration.CopyOriginalOverlay(),originalData=integration.CopyOriginalLevelData();
        return Compose(header,originalLength,[
            new(9,checked((int)U32(header,72)),originalOverlay.Length,Hash(originalOverlay),integration.CopyOverlay()),
            new(10,checked((int)U32(header,80)),originalData.Length,Hash(originalData),integration.CopyLevelData())],read,9,10);
    }
    internal static NativeWizardResearchPackedWad ComposeAuthoredArtisans(ReadOnlySpan<byte> header,int originalLength,
        NativeJapaneseEggAuthoredClassesPlan plan,Func<int,int,byte[]> read)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if(!plan.SourceCertified||header.Length!=0x800)throw new InvalidDataException("Authored Artisans packing requires the actual source-issued composition.");
        byte[] overlay=plan.Integration.CopyOriginalOverlay(),data=plan.Integration.CopyOriginalLevelData();
        return Compose(header,originalLength,[
            new(9,checked((int)U32(header,72)),overlay.Length,Hash(overlay),plan.CopyOverlay()),
            new(10,checked((int)U32(header,80)),data.Length,Hash(data),plan.CopyLevelData())],read,9,10);
    }
    private static NativeWizardResearchPackedWad Compose(ReadOnlySpan<byte> header,int originalLength,
        IEnumerable<NativeWizardResearchReplacement> replacements,Func<int,int,byte[]> read,int first,int second)
    {
        ArgumentNullException.ThrowIfNull(replacements);ArgumentNullException.ThrowIfNull(read);
        if(header.Length!=0x800)throw new InvalidDataException("Research WAD header owner must be exactly one sector.");
        if(originalLength is <0x800 or >0x10000000||originalLength%0x800!=0)throw new InvalidDataException("Research original WAD exceeds its aligned budget.");
        Dictionary<int,NativeWizardResearchReplacement> installed=[];
        int raw=0;
        foreach(var replacement in replacements)
        {
            if(++raw>2)throw new InvalidDataException("Research replacement enumeration exceeds its two-owner budget.");
            if(replacement==null||replacement.Index!=first&&replacement.Index!=second||replacement.Bytes==null)
                throw new InvalidDataException("Research replacement is not a recognized Dark entry owner.");
            if(replacement.Bytes.Length<replacement.OriginalLength||replacement.Bytes.Length>0x10000000||replacement.Bytes.Length%0x800!=0)
                throw new InvalidDataException("Research replacement alignment/length exceeds its bounded owner.");
            // Snapshot only the bounded immutable record references here. Do
            // not clone payloads until actual directory owners and the complete
            // final allocation budget have both been validated below.
            if(!installed.TryAdd(replacement.Index,replacement))
                throw new InvalidDataException("Research replacement contains a duplicate owner.");
        }
        if(installed.Count!=2)throw new InvalidDataException("Research requires exactly both Dark owners.");
        List<(int Index,int Offset,int Length)> entries=[];
        for(int index=0;index<256;index++)
        {
            uint offset=U32(header,index*8),length=U32(header,index*8+4);
            if(offset==0&&length==0)continue;
            if(offset<0x800||length==0||offset%0x800!=0||length%0x800!=0||offset+(long)length>originalLength)
                throw new InvalidDataException("Research source entry owner is out of bounds or unaligned.");
            entries.Add((index,(int)offset,(int)length));
        }
        var sorted=entries.OrderBy(e=>e.Offset).ToArray();int oldCursor=0x800,newLength=0x800;
        foreach(var e in sorted)
        {
            if(e.Offset!=oldCursor)throw new InvalidDataException("Research source WAD entries are not fully packed.");
            oldCursor=checked(oldCursor+e.Length);
            if(installed.TryGetValue(e.Index,out var r)&&(r.OriginalOffset!=e.Offset||r.OriginalLength!=e.Length))
                throw new InvalidDataException("Research replacement preimage does not match its actual directory owner.");
            newLength=checked(newLength+(installed.GetValueOrDefault(e.Index)?.Bytes.Length??e.Length));
            if(newLength>0x10000000)throw new InvalidDataException("Research final WAD exceeds its bounded budget.");
        }
        if(oldCursor!=originalLength||!installed.Keys.All(k=>entries.Any(e=>e.Index==k)))
            throw new InvalidDataException("Research packed WAD is incomplete or misses an owned entry.");
        foreach(int index in installed.Keys.ToArray())
        {
            var replacement=installed[index];
            installed[index]=replacement with{Bytes=replacement.Bytes.ToArray()};
        }
        byte[] final=new byte[newLength],finalHeader=header.ToArray();List<NativeWizardResearchEntry> reports=[];int cursor=0x800;
        using var sourceHash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);sourceHash.AppendData(header);
        foreach(var e in sorted)
        {
            byte[] before=read(e.Offset,e.Length);
            if(before==null||before.Length!=e.Length)throw new InvalidDataException("Research source reader returned a different owner length before copying.");
            before=before.ToArray();string hash=Hash(before);sourceHash.AppendData(before);
            byte[] after=before;
            if(installed.TryGetValue(e.Index,out var r))
            {
                if(hash!=r.OriginalSha256)throw new InvalidDataException("Research replacement source preimage hash changed.");
                after=r.Bytes;
            }
            after.CopyTo(final,cursor);Put(finalHeader,e.Index*8,cursor);Put(finalHeader,e.Index*8+4,after.Length);
            reports.Add(new(e.Index,e.Offset,cursor,e.Length,after.Length,hash,Hash(after)));cursor+=after.Length;
        }
        finalHeader.CopyTo(final,0);
        return new(final,Array.AsReadOnly(reports.ToArray()),Convert.ToHexStringLower(sourceHash.GetHashAndReset()));
    }
    private static uint U32(ReadOnlySpan<byte> bytes,int offset)=>BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
    private static void Put(byte[] bytes,int offset,int value)=>BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset),value);
    private static string Hash(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
}
