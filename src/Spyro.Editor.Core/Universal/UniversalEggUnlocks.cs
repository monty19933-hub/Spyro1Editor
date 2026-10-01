using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Universal;

/// <summary>Extends the two native bonus gates without changing the card format or
/// removing bonus access already recorded by a player's existing save.</summary>
public static class UniversalEggUnlocks
{
    public const uint PortalAddress = 0x80081874;
    public const uint EndingAddress = 0x800806CC;
    public const string PortalSourceSha256 = "93ccb45fb2c474672bc7dbb538e1c9ab28a63fa8c041e1cd04508e9cc70260ca";
    public const string EndingSourceSha256 = "6f1d9a46373e71e0050b14487dfc40f0ac599bd0708f2ecf988681b0d615b57e";

    public static byte[] PortalGate()
    {
        List<uint> w=[];
        void I(int op,int rs,int rt,int imm) => w.Add(UniversalMips.EncodeI(op,rs,rt,imm));
        void B(int op,int rs,int rt,uint target) => I(op,rs,rt,checked((int)((long)target-(PortalAddress+w.Count*4+4))/4));
        w.Add(UniversalMips.EncodeJ(3,UniversalEggProgress.CountAddress)); I(9,0,4,-1);
        B(5,2,3,0x8008351C); I(15,0,8,0x8007);
        I(35,8,2,0x5860); I(35,8,3,0x5750); I(10,2,2,12000);
        B(5,2,0,0x8008351C); I(10,3,3,80);
        B(5,3,0,0x8008351C); w.Add(0); I(35,8,2,0x5810);
        I(9,0,4,2); I(10,2,2,12); B(5,2,0,0x8008351C); w.Add(0);
        while (w.Count<18) w.Add(0);
        return Bytes(w);
    }

    public static byte[] EndingGate() => Bytes([
        UniversalMips.EncodeJ(3,UniversalEggProgress.BonusAddress),0,
        UniversalMips.EncodeI(35,19,4,0),0,
        UniversalMips.EncodeI(4,2,0,(int)((0x8008077C-(EndingAddress+20))/4)),
        UniversalMips.EncodeI(9,0,2,17)]);

    public static void PatchOverlays(LevelCatalog catalog, Func<int,byte[]> originalEntry,
        Func<int,byte[]> currentEntry, IDictionary<int,byte[]> replacements, int eggCount)
    {
        if (eggCount==0) return;
        if (eggCount<1 || eggCount>UniversalSpecialBehaviors.MaxDragonflyEggs)
            throw new ArgumentOutOfRangeException(nameof(eggCount));
        Patch("gnastysworld",PortalAddress,PortalSourceSha256,PortalGate());
        Patch("gnastysloot",EndingAddress,EndingSourceSha256,EndingGate());
        void Patch(string key,uint address,string expected,byte[] code)
        {
            var level=catalog.FindByKey(key) ?? throw new InvalidDataException($"Missing native bonus destination '{key}'.");
            int entry=level.SourceWadEntry-1,offset=checked((int)(address-UniversalOverlay.Base));
            var original=originalEntry(entry);
            var staged=replacements.TryGetValue(entry,out var replacement)?replacement:currentEntry(entry);
            if (offset<0 || offset+code.Length>original.Length || offset+code.Length>staged.Length ||
                Convert.ToHexStringLower(SHA256.HashData(original.AsSpan(offset,code.Length)))!=expected ||
                !staged.AsSpan(offset,code.Length).SequenceEqual(original.AsSpan(offset,code.Length)))
                throw new InvalidDataException($"{level.DisplayName}: the native bonus gate differs from the verified USA version.");
            var result=staged.ToArray(); code.CopyTo(result,offset); replacements[entry]=result;
        }
    }

    private static byte[] Bytes(IEnumerable<uint> words)
    {
        uint[] values=words.ToArray(); byte[] bytes=new byte[values.Length*4];
        for (int i=0;i<values.Length;i++) BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i*4),values[i]);
        return bytes;
    }
}
