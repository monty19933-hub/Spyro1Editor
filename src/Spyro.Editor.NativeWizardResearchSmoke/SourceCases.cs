using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;

internal static class SourceCases
{
    internal static async Task<int> RunAsync(NativeDarkHollowWizardResearchCandidatePlan plan)
    {
        int checks=0;
        Check(typeof(NativeDarkHollowWizardResearchCandidatePlan).GetConstructors().Length==0&&
            typeof(NativeDarkHollowWizardOverlayPlan).GetConstructors().Length==0&&typeof(NativeDarkHollowWizardDeathRewardPlan).GetConstructors().Length==0,"no public actor/resource/candidate metadata authority");
        Check(!plan.RuntimeVerified&&!plan.NormalCreateBinApproved&&!plan.CompleteNativeClosureProven&&!plan.TestLevelSelectorEnabled,"research gates and normal portal baseline");
        Check(plan.GrowthBytes==41*2048&&plan.RelocatedExecutableLba==53916&&plan.FinalExecutableEndLba==54120,"derived41sector allocation");
        Check(plan.WadSha256=="d07c91d13d9a7653a3708afd5d6de9e0279e92b495883d519e967b618e83d1d3"&&
            plan.OriginalWadSha256=="7ba8961c3626bcec816fbd6feecc4eff67dc8018ba43974e7838f4c341bf54a1","independent complete WAD oracle");
        Check(plan.RootSha256=="7a881ab875392b331f65dc294368ebc6963998b5c24632acf2f63f3bff0cc6a0"&&
            plan.Resources.FinalDataSha256=="6ed74dbce04d6712e0d9ac12cfd05ae6ac334e0ec39fb7420f2105076132e537","independent root and placed-data oracle");
        Check(plan.Reward.ExecutableSha256=="ec1e8ae38fed404f9a28ccc7fbf17d81e92a341aab89906e94c5e6c793f4c3b4"&&
            plan.Overlay.OverlaySha256=="10f82ee1028a9ad4fa9045d948a88e76545847031b954ac08ad0e74e6e9c7083","independent overlay/reward oracle");
        Check(plan.Entries.Where(e=>e.Index is not(13 or 14)).All(e=>e.OriginalLength==e.FinalLength&&e.OriginalSha256==e.FinalSha256),"all unrelated complete WAD owners preserved");
        Check(plan.Entries.Single(e=>e.Index==13).FinalLength==0xC800&&plan.Entries.Single(e=>e.Index==14).FinalOffset==0xF02800&&
            plan.Entries.Single(e=>e.Index==14).FinalLength==0x2A0800,"actual two WAD entry owners");
        byte[] wad=plan.CopyWad();
        foreach(var entry in plan.Entries)
        {
            Check(BinaryPrimitives.ReadInt32LittleEndian(wad.AsSpan(entry.Index*8))==entry.FinalOffset&&
                BinaryPrimitives.ReadInt32LittleEndian(wad.AsSpan(entry.Index*8+4))==entry.FinalLength,"actual directory remap");
            Check(Hash(wad.AsSpan(entry.FinalOffset,entry.FinalLength))==entry.FinalSha256,"actual whole entry payload");
        }
        var data=plan.Resources.CopyFinalLevelData();int scene=BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(0x18));
        using FileStream source=new(plan.SourceImagePath,FileMode.Open,FileAccess.Read,FileShare.Read);
        var originalRows=DiscImage.ReadFileBytes(source,plan.Layout,37,0xF01000+0x194000+0x8C38,128*0x58);
        Check(data.AsSpan(scene+0x8C38,128*0x58).SequenceEqual(originalRows),"all128 native rows actual source complement");
        Check(BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(scene+0xB838+0x36))==0x11B&&data[scene+0xB838+0x53]==0x56,"one real source Wizard reward");
        Check(plan.Memory.KnownPostLoadExtentsDisjoint&&plan.Memory.NormalReservedBufferGapBytes==0x441CC&&plan.Memory.DynamicMobyCapacity==90&&!plan.Memory.RuntimeFitProven,"native extents not transient runtime claim");
        // All41 promoted gap sectors, plus old/new EOF/EOR boundary locations.
        foreach(int lba in Enumerable.Range(54079,41).Concat(new[]{53874,54078,53915,54119}).Distinct())
        {
            byte[] raw=new byte[2352];source.Position=(long)lba*2352;source.ReadExactly(raw);
            byte[] expected=plan.ExpectedSector(lba,raw);RawMode2Form1SectorIntegrity.RequireLogicalWriteSector(expected,plan.Layout,lba);
            byte flags=(byte)(expected[18]&0x81);
            Check(expected[18]==expected[22]&&flags==(lba is 53915 or 54119?0x81:0),"all41sector native integrity and exact boundary flags");
        }
        byte[] untouched=new byte[2352];source.Position=30L*2352;source.ReadExactly(untouched);
        Check(plan.ExpectedSector(30,untouched).SequenceEqual(untouched),"protected raw sector unchanged");
        plan.RequireStageReversion();Check(true,"complete original WAD and stage inverse preimages");
        await plan.RequireFreshAsync();Check(true,"fresh complete source and real CUE recheck");
        byte[] exe=plan.CopyExecutable();plan.Reward.RequireExecutable(exe);exe[0x5FC3C]^=1;
        try{plan.Reward.RequireExecutable(exe);throw new Exception("expected exact executable refusal");}catch(InvalidDataException){checks++;}
        using var receipt=JsonDocument.Parse(plan.BuildReceipt((new string('0',64),0)));
        Check(receipt.RootElement.GetProperty("researchOnly").GetBoolean()&&!receipt.RootElement.GetProperty("NormalCreateBinApproved").GetBoolean()&&
            !receipt.RootElement.GetProperty("crashAtomicPublication").GetBoolean(),"receipt authority remains experimental and non-crash-atomic");
        Console.WriteLine("NATIVE_WIZARD_RESEARCH_SOURCE_WAD_ISO_41SECTOR_INVERSE_AND_STAGE_BINDINGS_OK");return checks;
        void Check(bool value,string name){if(!value)throw new InvalidDataException(name);checks++;}
    }
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
}
