using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;

internal static class SourceChecks
{
    internal static void Run(NativeCollectibleCardAdmissionPlan plan,NativeRegionalSourceSession source,Action<bool,string> check)
    {
        const uint Base=0x8000F800,Live=0x801FF600,Phase=Live+0x16,Stop=0x80010000;
        byte[] before=plan.CopyOriginalExecutable(),after=plan.CopyExecutable();
        check(plan.SourceCertified,"Real source-issued admission composition.");
        check(Hash(plan.Ancestry!.CopyExecutable())=="4f904e0c6db385ea1e8d82fb0565cf5b0cc5d413b03cf74e050a8766e9349e49"&&
            Hash(plan.Reader.CopyOverlay())=="773e51d5529280b678c0566afea189bc74408540a964fdd57f8a2354cfdb92b0",
            "Published R01 source recipe remains byte-for-byte unchanged.");
        check(Hash(NativeCollectibleCardAdmissionPlan.CopyResetWords())=="54c681de7e529f6595bbcc497e06d7821bfad3eb0ceaf0efeb4d3f185f352205",
            "Independently reviewed complete reset patch golden.");
        int[] anchors=[0x3030,0x1EAFC,0x23E24];byte[] ancestor=plan.Ancestry.CopyExecutable();
        check(Enumerable.Range(0,after.Length).Where(i=>!(i>=0x2F00&&i<0x2F50)&&!anchors.Any(a=>i>=a&&i<a+8))
            .All(i=>after[i]==ancestor[i]),"Every other complete R01 executable byte preserved.");
        foreach(int at in anchors)
        {
            uint hi=BinaryPrimitives.ReadUInt32LittleEndian(after.AsSpan(at)),lo=BinaryPrimitives.ReadUInt32LittleEndian(after.AsSpan(at+4));
            check(unchecked((hi<<16)+(uint)(int)(short)lo)==plan.Kernel.Layout.CopyAnchor,"Every card anchor covers final successor owner.");
        }
        void Census(byte[] bytes,uint @base,int start)
        {
            for(int at=start;at<bytes.Length;at+=4)
            {
                uint w=BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at)),op=w>>26,pc=@base+(uint)at;
                uint? target=op is 2 or 3?((pc+4)&0xF0000000)|((w&0x3FFFFFF)<<2):
                    op is 1 or 4 or 5 or 6 or 7?unchecked(pc+4+(uint)((short)w*4)):null;
                if(target is >=0x80012700 and <0x80012750)throw new Exception("Unexpected direct entry into compacted source reset window.");
            }
        }
        Census(before,Base,0x800);int owners=0;
        foreach(int entry in new[]{2,7}.Concat(Enumerable.Range(0,35).Select(i=>9+i*2)).Append(81))
        {Census(source.ReadWadEntry(entry),0x8007AA38,0);owners++;}
        check(owners==38,"Exact EXE and bounded38WAD-owner direct-entry census; no indirect dispatch claim.");
        check(Enumerable.Range(0x800/4,(before.Length-0x800)/4).Count(i=>BinaryPrimitives.ReadUInt32LittleEndian(before.AsSpan(i*4))==0x0C004981)==1,
            "Single original direct reset callee entry from127A8.");

        foreach(uint start in new uint[]{0x80012604,0x8001277C})
        foreach(uint reserve in new uint[]{0,0x800,0xA00,0xB00,uint.MaxValue})
        {
            RenderProbeCpu Make(byte[] exe)
            {
                var c=new RenderProbeCpu{StrictSignedArithmetic=true};c.Copy(Base,exe);
                c.Copy(Live,Enumerable.Repeat((byte)0xEC,0x200).ToArray());c.Put(Phase,1,1);c.Put(0x800755A4,reserve);
                for(int r=1;r<32;r++)c.Regs[r]=0xA0000000u+(uint)r;
                c.Regs[29]=0x801FFCA0;c.Regs[31]=Stop;c.Hi=0x11223344;c.Lo=0x55667788;return c;
            }
            var original=Make(before);var patched=Make(after);original.Run(start,Stop);patched.Run(start,Stop);
            check(original.Regs.SequenceEqual(patched.Regs)&&original.Hi==patched.Hi&&original.Lo==patched.Lo,
                "Complete real initializer/reset/clear execution retains all architectural registers including at.");
            check(original.Writes.SequenceEqual(patched.Writes.Where(a=>a!=Phase)),"Every native store remains in original order and byte width.");
            check(original.Writes.Distinct().All(a=>original.Get(a,1)==patched.Get(a,1)),"All native state outputs equal original execution.");
            check(patched.Get(Phase,1)==(reserve==0xA00?0:1)&&patched.Writes.Count(a=>a==Phase)==(reserve==0xA00?1:0),
                "Real reset invalidates only under exact owned A00 reservation.");
            check(Enumerable.Range(0,0x200).Where(i=>i!=0x16).All(i=>patched.Get(Live+(uint)i,1)==0xEC),
                "Reset preserves native sentinel guard and all old progress bytes; it does not manufacture empty Ready.");
            check(patched.Calls.SequenceEqual(original.Calls),"Reset introduces no resident call into transient code or new native service.");
        }
        foreach(int at in new[]{0,0x10,0x2EFF,0x2F00,0x2F44,0x2F4C,0x2F50,0x3030,0x65DA4,0x65E40,0x65FFF})
        {
            byte[] bad=after.ToArray();bad[at]^=1;
            try{plan.RevertExecutableExact(bad);throw new Exception("Mutated successor EXE admitted.");}
            catch(InvalidDataException){check(true,"Exact successor EXE rejects mutation of owned span or complement.");}
        }
        check(plan.Kernel.Layout.FinalByteLength==0xD000&&plan.Kernel.Layout.CopyAnchor==0x80087A38,
            "Final aligned overlay extent independently includes all admission code/state/padding.");
    }
    private static string Hash(byte[] b)=>Convert.ToHexStringLower(SHA256.HashData(b));
}
