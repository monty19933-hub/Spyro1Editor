using System.Buffers.Binary;
using Spyro.Editor.Core.Exporting;

internal static class NativeWizardDeathRewardCases
{
    internal static void Run(Action<bool,string> check,Action<Action,string> reject)
    {
        var original=new NativeAddressSpace(NativeAddressKind.ExecutableFile,"retail-fixture",NativeAddressRevision.Original,0x66000);
        var final=new NativeAddressSpace(NativeAddressKind.ExecutableFile,"final-fixture",NativeAddressRevision.Final,0x66000);
        byte[] retail=new byte[0x66000];BinaryPrimitives.WriteUInt16LittleEndian(retail.AsSpan(0x5FC3C),100);
        byte[] overlay=retail.ToArray();overlay[0x4B048]=9;
        var patch=NativeWizardTreasureTargetPatch.Create(new(original,0x5FC3C,2),new(final,0x5FC3C,2),retail,overlay);
        byte[] output=patch.CopyExecutable();
        check(BinaryPrimitives.ReadUInt16LittleEndian(output.AsSpan(0x5FC3C))==110&&
            output.AsSpan(0,0x5FC3C).SequenceEqual(overlay.AsSpan(0,0x5FC3C))&&output.AsSpan(0x5FC3E).SequenceEqual(overlay.AsSpan(0x5FC3E)),"Native reward changes more than its exact two-byte treasure target.");
        check(patch.Original.Space==original&&patch.Final.Space==final&&patch.Revert(output).SequenceEqual(overlay),"Native reward typed source/final identity or exact revert failed.");
        output[0]^=1;reject(()=>patch.Revert(output),"readback");
        check(patch.CopyExecutable()[0]==overlay[0],"Native reward output was not detached.");
        var stale=retail.ToArray();stale[0x5FC3C]=101;
        reject(()=>NativeWizardTreasureTargetPatch.Create(new(original,0x5FC3C,2),new(final,0x5FC3C,2),stale,overlay),"preimage");
        reject(()=>NativeWizardTreasureTargetPatch.Create(new(original,0x5FC3C,2),new(final,0x5FC3C,2),retail,patch.CopyExecutable()),"preimage");
        reject(()=>NativeWizardTreasureTargetPatch.Create(new(original,0x5FC3E,2),new(final,0x5FC3E,2),retail,overlay),"field");
        reject(()=>NativeWizardTreasureTargetPatch.Create(new(original,0x5FC3C,2),new(original,0x5FC3C,2),retail,overlay),"domain");
        reject(()=>NativeWizardTreasureTargetPatch.Create(new(original,0x5FC3C,2),new(final,0x5FC3C,2),retail[..^1],overlay),"length");
        byte[] data=new byte[0x13000];BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(0x18),0x800);BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(0x1C),0x11800);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(0x800+0x8C34),129);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(0x800+0xB838+0x36),0x11B);data[0x800+0xB838+0x53]=0x56;data[0x800+0xB838+0x3A]=0x7F;
        NativeDarkHollowWizardDeathRewardPlan.RequireSingleRewardWizard(data);check(true,"One appended native Wizard reward was refused.");
        data[0x800+0xB838+0x53]=0x55;reject(()=>NativeDarkHollowWizardDeathRewardPlan.RequireSingleRewardWizard(data),"reward");
        data[0x800+0xB838+0x53]=0x56;data[0x800+0xB838+0x3A]=0xFF;reject(()=>NativeDarkHollowWizardDeathRewardPlan.RequireSingleRewardWizard(data),"reward");
        data[0x800+0xB838+0x3A]=0x7F;BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(0x800+0x8C38+0x36),0x11B);
        reject(()=>NativeDarkHollowWizardDeathRewardPlan.RequireSingleRewardWizard(data),"one");
        Console.WriteLine("NATIVE_WIZARD_DEATH_REWARD_TYPED_TARGET_COMPLEMENT_REVERT_AND_COUNT_OK");
    }
    internal static void Source(string image,NativeDarkHollowWizardOverlayPlan overlay,Action<bool,string> check,Action<Action,string> reject)
    {
        var plan=NativeDarkHollowWizardDeathRewardPlan.Capture(image,overlay);byte[] final=plan.CopyExecutable(),baseline=overlay.CopyExecutable();
        plan.RequireExecutable(final);
        check(plan.ExecutableSha256=="ec1e8ae38fed404f9a28ccc7fbf17d81e92a341aab89906e94c5e6c793f4c3b4"&&plan.BaseExecutableSha256=="14aa937f198740373b37312eb3054cad46dd1ae05f1920c145d6bc1fde9a8635",
            "Native death reward output differs from the independent raw-source oracle.");
        check(plan.RewardSelector==0x56&&plan.NativeGemValue==10&&plan.TreasureTargetBefore==100&&plan.TreasureTargetAfter==110,"Native source death reward/treasure values differ.");
        check(final.AsSpan(0,0x5FC3C).SequenceEqual(baseline.AsSpan(0,0x5FC3C))&&final.AsSpan(0x5FC3E).SequenceEqual(baseline.AsSpan(0x5FC3E)),"Death reward changed an unowned overlay EXE byte.");
        check(BinaryPrimitives.ReadUInt16LittleEndian(final.AsSpan(0x5FC3C))==110&&plan.RevertToOverlayExecutable(final).SequenceEqual(baseline),"Native reward output/revert target mismatch.");
        check(plan.SourceCertificates.Any(c=>c.Owner=="PETE-model56"&&c.SourceOffset==0x7EBF24&&c.ByteLength==0x108&&c.Sha256=="52404b4a45b86d26298f62a8b97fa92703fdd7ca6ea5cbde025e17ac8ce46ea8"),"Shared native reward model owner is not source-bound.");
        check(!plan.CompleteNativeClosureProven&&!plan.RuntimeVerified&&!plan.PublicationApproved,"Death reward accounting promoted unproved native gates.");
        final[0x4B048]^=1;reject(()=>plan.RequireExecutable(final),"readback");
        Console.WriteLine($"NATIVE_DARK_WIZARD_DEATH_REWARD_SOURCE_OK selector=56 value=10 target=100->110 exe={plan.ExecutableSha256} runtime/publication=false");
    }
}
