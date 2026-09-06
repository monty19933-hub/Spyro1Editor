using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

internal static class SourceCases
{
    internal static async Task Run(string image,string cue,string workspace)
    {
        var catalog=LevelCatalog.Load(workspace);
        var old=await NativeDarkHollowWizardResearchCandidatePlan.CaptureAsync(image,cue,catalog);
        var plan=await NativeDarkHollowTwoWizardResearchCandidatePlan.CaptureAsync(image,cue,catalog);
        Program.Check(plan.PlanSha256!=old.PlanSha256,"separate source-bound profile/owner identity");
        Program.Check(plan.Appended.Families.Select(f=>(f.FamilyId,f.NativeIndex)).SequenceEqual(new[]{("z",128),("a",129)}),"factory retains fixed temporal z then a identities, not sorted label reassignment");
        Program.Check(ReferenceEquals(plan.Appended.ProposedOwners,plan.Placement.ProposedOwners),"actual persistent proposed owners retained through placement");
        Program.Check(plan.Resources.FinalDataSha256=="375b9dc41a76d507ded9ffdf442e4febbb1916e68bb5d4597477bb9031e2fd90"&&plan.Reward.ExecutableSha256=="ce9c99ba467b089603850989bd141f374a7f86cf8e4deeab3fea1a3fc30164d1","preexisting independent two-owner resource/reward oracles");
        Program.Check(plan.Overlay.CopyOverlay().SequenceEqual(old.Overlay.CopyOverlay())&&plan.Overlay.CopyExecutable().SequenceEqual(old.Overlay.CopyExecutable()),"shared native overlay/copy-buffer bytes remain legacy exact");
        Program.Check(plan.GrowthBytes==41*2048&&plan.RelocatedExecutableLba==53916&&plan.FinalExecutableEndLba==54120,"derived same41-sector allocation without extra per-instance code/model copies");
        Program.Check(plan.Memory.PlacedMobyCount==130&&plan.Memory.DynamicMobyCapacity==89&&plan.Memory.NormalReservedBufferGapBytes==0x441CC,"actual placed/private/dynamic arena and native post-load arithmetic");
        Program.Check(plan.Reward.TreasureTargetAfter==120,"actual two independent reward owners account120");
        Program.Check(!plan.RuntimeVerified&&!plan.CompleteNativeClosureProven&&!plan.NormalCreateBinApproved&&!plan.TestLevelSelectorEnabled,"no native/runtime/normal exporter authority or test selector");
        // New-only plan/receipt includes the canonical proposed ledger, but is
        // not accepted App/card history. Verification must recompute the fixed
        // factory; receipt content is never an input granting source authority.
        using var receipt=JsonDocument.Parse(plan.BuildReceipt((new string('0',64),0)));
        Program.Check(receipt.RootElement.GetProperty("profile").GetString()==NativeDarkHollowTwoWizardResearchCandidatePlan.Profile,"separately named receipt");
        using var owners=JsonDocument.Parse(plan.Appended.ProposedOwners.Serialize());
        Program.Check(JsonElement.DeepEquals(receipt.RootElement.GetProperty("proposedOwners"),owners.RootElement),"actual canonical owner identities in new receipt");
        Program.Check(receipt.RootElement.GetProperty("treasureTarget").GetInt32()==120&&!receipt.RootElement.GetProperty("acceptedProjectHistory").GetBoolean(),"new receipt does not promote proposed identities to accepted history");
        byte[] wad=plan.CopyWad();
        Program.Check(plan.Entries.Where(e=>e.Index is not(13 or 14)).All(e=>e.OriginalLength==e.FinalLength&&e.OriginalSha256==e.FinalSha256),"all unrelated WAD owners exact");
        foreach(var entry in plan.Entries)
        {
            Program.Check(BinaryPrimitives.ReadInt32LittleEndian(wad.AsSpan(entry.Index*8))==entry.FinalOffset&&
                BinaryPrimitives.ReadInt32LittleEndian(wad.AsSpan(entry.Index*8+4))==entry.FinalLength,"actual directory owner mapping");
            Program.Check(Hash(wad.AsSpan(entry.FinalOffset,entry.FinalLength))==entry.FinalSha256,"actual complete final entry readback");
        }
        Program.Check(plan.RootSha256==old.RootSha256&&plan.OriginalWadSha256==old.OriginalWadSha256,"same physical relocation root and original source inverse");
        Program.Check(plan.CopyCueBytes().AsSpan().IndexOf("dark-hollow-native-two-wizards-r01.bin"u8)>=0&&
            !plan.CopyCueBytes().SequenceEqual(old.CopyCueBytes()),"new output CUE cannot boot old R01 filename");
        plan.RequireStageReversion();await plan.RequireFreshAsync();Program.Check(true,"complete original WAD inverse and fresh original BIN/CUE rechecks");
        using FileStream source=new(image,FileMode.Open,FileAccess.Read,FileShare.Read);
        foreach(int lba in Enumerable.Range(54079,41).Concat(new[]{53874,54078,53915,54119}).Distinct())
        {
            byte[] raw=new byte[2352];source.Position=(long)lba*2352;source.ReadExactly(raw);
            byte[] expected=plan.ExpectedSector(lba,raw);
            RawMode2Form1SectorIntegrity.RequireLogicalWriteSector(expected,plan.Layout,lba);
            Program.Check(expected[18]==expected[22]&&(expected[18]&0x81)==(lba is 53915 or 54119?0x81:0),"all41 growth and exact EOF/EOR boundary sectors");
        }
        byte[] untouched=new byte[2352];source.Position=30L*2352;source.ReadExactly(untouched);
        Program.Check(plan.ExpectedSector(30,untouched).SequenceEqual(untouched),"whole protected raw sector complement");
        // Independently derived BEFORE the C# comparison. Hash expected sectors
        // in memory only; this mode creates no image or staging/output artifact.
        Program.Check(plan.WadSha256=="4e7c47911822087eb30e531b8491b7548cfdf18d069e0a24a12e8711be971a94","independent whole two-owner WAD oracle");
        using var finalHash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var oldHash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var originalHash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        source.Position=0;byte[] sector=new byte[2352];int changed=0;List<int> vsOld=[];
        for(int lba=0;lba<source.Length/2352;lba++)
        {
            source.ReadExactly(sector);originalHash.AppendData(sector);
            byte[] after=plan.ExpectedSector(lba,sector),before=old.ExpectedSector(lba,sector);
            finalHash.AppendData(after);oldHash.AppendData(before);
            if(!after.AsSpan().SequenceEqual(sector))changed++;
            if(!after.AsSpan().SequenceEqual(before))vsOld.Add(lba);
        }
        string final=Convert.ToHexStringLower(finalHash.GetHashAndReset());
        Program.Check(final=="612e68e1a8642d42f2417fd6153368c9549094ac391a3835c9cf6dfee7808cba"&&changed==46341,"independent full physical image and changed-sector count");
        Program.Check(Convert.ToHexStringLower(oldHash.GetHashAndReset())=="2a87dc12f9398ef873267e0f17792cfe160952c9f1bf12c137f64029863a39db","whole legacy R01 image exact through extracted shared writer");
        Program.Check(vsOld.SequenceEqual(new[]{8585,8591,8596,8597,8601,8602,54107}),"exact seven-sector difference to legacy image");
        Program.Check(Convert.ToHexStringLower(originalHash.GetHashAndReset())=="fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37","complete original image unchanged during in-memory physical comparison");
        Console.WriteLine($"NATIVE_TWO_WIZARD_EXPECTED_IMAGE_ORACLE_OK sha={final} changed={changed} imageWrites=0");
        Console.WriteLine($"two candidate plan={plan.PlanSha256} wad={plan.WadSha256} root={plan.RootSha256}");
    }
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
}
