using System.Security.Cryptography;
using System.Buffers.Binary;
using Spyro.Editor.Core.Exporting;
namespace Spyro.Editor.Core.Universal;
/// <summary>Private prerequisite for the genuine Alpine0/1 quarantine over intact
/// Blowhard. Authenticates retained wall/reward code/resources and the exact
/// derived callback graph. It does not issue source/contact gameplay admission.</summary>
internal static class UniversalAlpineArmRetainedRewardOwners
{
    private const string HostOverlayHash="1ee0030607139491b6d076c389837d5b6c0a38e87d324fec2e14b58b47f95aef";
    private const string SharedHash="b73da4852629d2035f9a1f6541f0ebda45ce61762f8b09ec62d8fe293f5e1f86";
    private static readonly (int Class,string Hash)[] LocalModels=
    [ (14,"ca65a14da356546aa00e308319e637809c87c9de8f0901b353de9b377a4a4a62"),
      (15,"6c75840f4ed4be3d6e0e488bf58ec6271b1de00654e2e56beb293727ee7d62b0"),
      (16,"ae079bfe065336baa36063eb61f43597cd41d7a291ddd0fba79fcf6db40447e3"),
      (120,"549bc2666e4ad412bfdc59cd6a14ba3ff5846d408a50b0aee10349744c318124") ];
    internal static void Require(UniversalLevelData source,UniversalOverlay sourceOverlay,
        UniversalLevelData originalHost,UniversalOverlay originalHostOverlay,UniversalLevelData writtenHost,
        byte[] originalExe,byte[] writtenExe,byte[] originalSharedEntry8,byte[] writtenSharedEntry8,
        byte[] writtenOverlay,IReadOnlyDictionary<int,int> map,uint baseAddress,byte[] writtenCode,
        IReadOnlyDictionary<string,uint> labels,IReadOnlyList<int>? sourceRows=null)
    {
        UniversalAlpineArmResources.ProveSource(source,sourceOverlay);
        UniversalAlpineArmNative.RequireNative(originalExe);
        Hash(originalHost.Bytes,UniversalAlpineArmArenaStorage.HostHash,"original Blowhard data");
        Hash(originalHostOverlay.Bytes,HostOverlayHash,"original Blowhard callbacks");
        Hash(originalSharedEntry8,SharedHash,"original shared reward/digit resources");
        if(!originalSharedEntry8.AsSpan().SequenceEqual(writtenSharedEntry8))
            throw new InvalidDataException("Alpine native reward/digit resources must retain original shared entry8.");
        var occupied=originalHost.Registrations().Select(r=>r.ClassId).ToHashSet();
        for(int field=4;field+8<=0x800;field+=8)
            if(BinaryPrimitives.ReadUInt32LittleEndian(originalSharedEntry8.AsSpan(field))!=0)
                occupied.Add(BinaryPrimitives.ReadInt32LittleEndian(originalSharedEntry8.AsSpan(field+4)));
        if(!map.Keys.Order().SequenceEqual(new[]{0xFD,0x10F,0x13B})||map.Values.Distinct().Count()!=3||
            map.Values.Any(c=>c<0||c>=512||occupied.Contains(c)))
            throw new InvalidDataException("Alpine cohort requires three distinct unused physical classes.");
        if(writtenHost.Scene().RowCount!=89)
            throw new InvalidDataException("This private Alpine owner covers one appended original0/1 cohort only.");
        for(int ordinal=0;ordinal<2;ordinal++)
        {
            int sourceRow=sourceRows?[ordinal]??ordinal;
            if(sourceRows!=null&&!sourceRows.Order().SequenceEqual(new[]{0,1}))throw new InvalidDataException("Alpine retained owner requires exact original0/1 allocation.");
            var row=writtenHost.Row(87+ordinal);var originalRow=source.Row(sourceRow);
            if(row[58]!=originalRow[58]||row[83]!=originalRow[83]||row[72]!=originalRow[72]||
                row[73]!=originalRow[73]||row[70]!=originalRow[70]||row[74]!=255||
                BinaryPrimitives.ReadUInt16LittleEndian(row.AsSpan(54))!=map[sourceRow==0?0xFD:0x10F])
                throw new InvalidDataException("Alpine initialized source reward/group/state/yaw owner changed.");
        }
        foreach(var (cls,hash) in LocalModels)
        {
            var original=originalHost.Package(cls);Hash(original,hash,$"native reward class{cls:X3}");
            if(!original.AsSpan().SequenceEqual(writtenHost.Package(cls)))
                throw new InvalidDataException($"Alpine fallback requires retained native model class{cls:X3}.");
        }
        var originalSounds=new NativeLevelSoundLayout(originalHost.Bytes).Sound;
        var writtenSounds=new NativeLevelSoundLayout(writtenHost.Bytes).Sound;
        for(int selector=0;selector<originalSounds.Map.Count;selector++)
        {
            int index=originalSounds.Map[selector];if(index==255)continue;
            if(writtenSounds.Map[selector]!=index)
                throw new InvalidDataException("Alpine native reward/fallback sound selector changed.");
            var a=originalSounds.Descriptors[index];var b=writtenSounds.Descriptors[index];
            if(!a.CopyDescriptor().AsSpan(4).SequenceEqual(b.CopyDescriptor().AsSpan(4))||
                !a.Sample.CopyBytes().AsSpan().SequenceEqual(b.Sample.CopyBytes()))
                throw new InvalidDataException("Alpine retained reward/fallback sound controls or ADPCM sample changed.");
        }
        // Complete original loader/reward/allocator/floor/contact/GTE/sound/RNG
        // owners, independent of bundle allocation or an entire final-image hash.
        foreach(var (start,end) in new (uint,uint)[]{(0x800133E0,0x80013490),
            (0x800169AC,0x80017CA0),(0x80037E98,0x8003B0DC),
            (0x8004BE4C,0x8004EBA8),(0x800524C4,0x80053600),
            (0x800557CC,0x80055954),(0x8006272C,0x8006276C)})
        {
            int offset=checked((int)(start-UniversalExeBindings.ExeBase)),length=checked((int)(end-start));
            if(writtenExe.Length<offset+length||!originalExe.AsSpan(offset,length).SequenceEqual(writtenExe.AsSpan(offset,length)))
                throw new InvalidDataException($"Alpine retained native reward/wall owner {start:X8} changed.");
        }
        int prefix=checked((int)(originalHostOverlay.CopyBuffer-UniversalOverlay.Base));
        if(writtenOverlay.Length<prefix||!originalHostOverlay.Bytes[..prefix].SequenceEqual(writtenOverlay.AsSpan(0,prefix)))
            throw new InvalidDataException("Alpine retained reward constructors/updaters and tables changed.");
        UniversalAlpineArmQuarantine.RequireWritten(source,sourceOverlay,originalHostOverlay,originalExe,map,
            baseAddress,writtenCode,labels);
        int codeOffset=checked((int)(baseAddress-UniversalOverlay.Base));
        if(writtenOverlay.Length<codeOffset+writtenCode.Length||!writtenOverlay.AsSpan(codeOffset,writtenCode.Length).SequenceEqual(writtenCode))
            throw new InvalidDataException("Alpine derived callback code is not the emitted overlay region.");
    }
    private static void Hash(ReadOnlySpan<byte> bytes,string expected,string owner)
    {
        if(Convert.ToHexStringLower(SHA256.HashData(bytes))!=expected)
            throw new InvalidDataException($"Alpine retained {owner} differs from its original native owner.");
    }
}
