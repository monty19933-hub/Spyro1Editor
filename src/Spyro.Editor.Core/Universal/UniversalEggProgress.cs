using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Spyro.Editor.Core.Universal;

/// <summary>Required-egg weighting applied to the native live percentage function. No card
/// bytes or retail egg counters are added; K is read from the native saved bitmap.</summary>
public static class UniversalEggProgress
{
    public const uint PercentageAddress = 0x8002BB20;
    public const int PercentageBytes = 0xC0;
    public const string PercentageSourceSha256 = "8ccffbcd96e5afadd87bbe4a6b84f20c9ff4194d30b32aa6223cce2c80bd37e5";
    public const uint CountAddress = UniversalSpecialBehaviors.EggInventoryHostEnd - 32 * 4;
    public const uint BonusAddress = CountAddress - 15 * 4;

    private sealed class Asm
    {
        public UniversalAsmPart Part { get; } = new();
        public void I(int op,int rs,int rt,int imm) => Part.Emit(UniversalMips.EncodeI(op,rs,rt,imm));
        public void R(int rs,int rt,int rd,int sh,int fn) => Part.Emit((uint)(rs<<21|rt<<16|rd<<11|sh<<6|fn));
        public void B(int op,int rs,int rt,string label) => Part.Emit(UniversalMips.EncodeI(op,rs,rt,0),new(UniversalFixKind.BranchLabel,Label:label));
        public void La(int reg,uint address) { I(15,0,reg,UniversalMips.Hi(address)); I(9,reg,reg,UniversalMips.Lo(address)); }
        public void Nop() => Part.Emit(0);
        public byte[] Bytes(uint address) => UniversalBehaviorBundleBuilder.Assemble([Part],address).Code;
    }

    /// <summary>Counts active placed eggs in the native saved bitmap. a0 is a level index
    /// (0..35), or -1 for the entire project; v0 is collected and v1 is available.
    /// Filtering the authored table keeps retired identities out of both totals.</summary>
    public static byte[] CountRoutine(int total)
    {
        if (total is < 1 || total > UniversalSpecialBehaviors.MaxDragonflyEggs) throw new ArgumentOutOfRangeException(nameof(total));
        var a=new Asm();
        a.La(8,UniversalSpecialBehaviors.EggInventoryData); a.La(9,0x80077908);
        a.I(9,0,10,total); a.I(9,0,2,0); a.I(9,0,3,0);
        a.Part.Label("loop");
        a.I(35,8,11,0); a.I(9,8,8,4); // load delay
        a.R(0,11,12,16,2); a.I(12,12,12,255);
        a.B(1,4,0,"match"); a.Nop(); // bltz a0: project total
        a.B(5,12,4,"next"); a.Nop();
        a.Part.Label("match"); a.I(9,3,3,1); a.R(0,12,12,5,0);
        a.I(12,11,13,65535); a.R(0,13,14,5,2); a.R(0,14,14,2,0);
        a.R(12,14,12,0,0x21); a.R(12,9,12,0,0x21); a.I(35,12,12,0);
        a.I(12,13,13,31); a.R(13,12,12,0,6); a.I(12,12,12,1); a.R(2,12,2,0,0x21);
        a.Part.Label("next"); a.I(9,10,10,-1);
        a.B(5,10,0,"loop"); a.Nop(); a.Part.Emit(UniversalMips.JrRa); a.Nop();
        var bytes=a.Bytes(CountAddress);
        if (bytes.Length>128) throw new InvalidDataException("Egg bitmap counter exceeds its existing code owner.");
        return bytes;
    }

    /// <summary>Native bonus ending requirement: all authored eggs and exactly 14,000 gems.
    /// The surrounding native overlay still requires the original player state.</summary>
    public static byte[] BonusRoutine()
    {
        var a=new Asm();
        a.I(9,29,29,-24); a.I(43,29,31,16);
        a.Part.Emit(UniversalMips.EncodeJ(3,CountAddress)); a.I(9,0,4,-1);
        a.B(5,2,3,"done"); a.I(9,0,2,0);
        a.I(15,0,8,0x8007); a.I(35,8,8,0x5860);
        a.I(9,0,2,14000); a.R(8,2,2,0,0x26); a.I(11,2,2,1);
        a.Part.Label("done"); a.I(35,29,31,16); a.Nop();
        a.Part.Emit(UniversalMips.JrRa); a.I(9,29,29,24);
        var bytes=a.Bytes(BonusAddress);
        if (bytes.Length!=60) throw new InvalidDataException("The egg bonus requirement exceeds its code owner.");
        return bytes;
    }

    public static byte[] PercentageRoutine(int total)
    {
        if (total is < 1 || total > UniversalSpecialBehaviors.MaxDragonflyEggs) throw new ArgumentOutOfRangeException(nameof(total));
        var a=new Asm();
        a.I(9,29,29,-24); a.I(43,29,31,16); a.Part.Emit(UniversalMips.EncodeJ(3,CountAddress)); a.I(9,0,4,-1);
        a.R(2,0,15,0,0x21); // t7=earned active eggs
        a.I(15,0,8,0x8007); a.I(35,8,4,0x5860); a.I(35,8,5,0x5750); a.I(35,8,6,0x5810); a.I(9,0,9,total);
        // Bonus treasure cannot replace a missing required category.
        a.B(5,15,9,"base"); a.I(9,0,9,80);
        a.B(5,5,9,"base"); a.I(9,0,9,12);
        a.B(5,6,9,"base"); a.I(11,4,9,12000);
        a.B(5,9,0,"base"); a.Nop();
        a.I(9,4,2,-12000); a.I(9,0,9,100); a.R(2,9,0,0,0x1B); a.R(0,0,2,0,0x12);
        a.B(4,0,0,"done"); a.I(9,2,2,100);
        a.Part.Label("base");
        a.I(11,4,9,12001); a.B(5,9,0,"score"); a.Nop(); a.I(9,0,4,12000);
        a.Part.Label("score");
        a.I(9,0,9,50); a.R(4,9,0,0,0x19); a.R(0,0,10,0,0x12);
        a.I(9,0,9,6000); a.R(5,9,0,0,0x19); a.R(0,0,11,0,0x12); a.R(10,11,10,0,0x21);
        a.R(6,15,6,0,0x21); a.I(9,0,9,10000); a.R(6,9,0,0,0x19); a.R(0,0,11,0,0x12); a.R(10,11,10,0,0x21);
        a.I(9,0,9,12000+100*total); a.R(10,9,0,0,0x1B); a.R(0,0,2,0,0x12);
        a.Part.Label("done"); a.I(35,29,31,16); a.Nop(); a.Part.Emit(UniversalMips.JrRa); a.I(9,29,29,24);
        var bytes=a.Bytes(PercentageAddress);
        if (bytes.Length>PercentageBytes) throw new InvalidDataException($"Egg percentage routine needs {bytes.Length} bytes; native owner has {PercentageBytes}.");
        return bytes;
    }

    public static void Patch(byte[] exe, ReadOnlySpan<byte> original, int total, int inventoryCodeBytes)
    {
        if (total==0) return; // byte-identical native policy when no custom eggs remain
        int offset=(int)(PercentageAddress-UniversalExeBindings.ExeBase);
        if (Convert.ToHexStringLower(SHA256.HashData(original.Slice(offset,PercentageBytes)))!=PercentageSourceSha256 ||
            !exe.AsSpan(offset,PercentageBytes).SequenceEqual(original.Slice(offset,PercentageBytes)))
            throw new InvalidDataException("The native completion function differs from the verified USA version.");
        if (UniversalSpecialBehaviors.EggInventoryHost+inventoryCodeBytes>BonusAddress)
            throw new InvalidDataException("The egg inventory and completion routines would overlap.");
        byte[] count=CountRoutine(total),percent=PercentageRoutine(total),bonus=BonusRoutine();
        int counterOffset=(int)(CountAddress-UniversalExeBindings.ExeBase);
        if (!exe.AsSpan(counterOffset,count.Length).SequenceEqual(original.Slice(counterOffset,count.Length)))
            throw new InvalidDataException("The egg completion counter's code space is already in use.");
        int bonusOffset=(int)(BonusAddress-UniversalExeBindings.ExeBase);
        if (!exe.AsSpan(bonusOffset,bonus.Length).SequenceEqual(original.Slice(bonusOffset,bonus.Length)))
            throw new InvalidDataException("The egg bonus requirement's code space is already in use.");
        bonus.CopyTo(exe,bonusOffset); count.CopyTo(exe,counterOffset); percent.CopyTo(exe,offset);
    }
}
