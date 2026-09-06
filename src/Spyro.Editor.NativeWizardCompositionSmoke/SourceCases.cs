using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Levels;

internal static class SourceCases
{
    internal static async Task Run(string path,string workspace)
    {
        var catalog=LevelCatalog.Load(workspace);
        var inventory=NativeMobySourceInventory.Capture(path,catalog);
        var ps=await NativePlayerAnimationSource.CaptureAsync(path);
        var player=await ps.CompileMergeAsync(path,33,12,[8]);
        var ss=await NativeGlobalSoundSource.CaptureAsync(path);
        var sound=await ss.CompileModelSlotMergeAsync(path,inventory,33,12,0x11B,[0,1,2],
            [new(0x1F,NativeGlobalSoundMappingPolicy.ExplicitRebindUnresolvedConsumers),new(0x26)]);
        var texture=NativeDarkHollowWizardTexturePlan.Capture(path);
        var particle=await NativeDarkHollowWizardParticlePlan.CaptureAsync(path,texture);
        var behavior=NativeDarkHollowWizardBehaviorSource.Capture(path);
        var effects=NativeDarkHollowWizardParticleBehaviorSource.Capture(path);
        var z=new NativeWizardPlacementIntent(92832,25248,10445,91808,25248);
        var a=new NativeWizardPlacementIntent(86688,25248,10445,87712,25248);
        var legacyScene=NativeDarkHollowWizardScenePlan.Capture(path);
        var legacyPlace=NativeDarkHollowWizardPlacementSource.Capture(path,legacyScene).Compile(z);
        var legacy=await NativeDarkHollowWizardResourcePlan.CompileWithPlacementAsync(path,inventory,player,sound,texture,particle,legacyScene,legacyPlace);
        var legacyOverlay=NativeDarkHollowWizardOverlayPlan.Compile(behavior,legacy,effects);
        var legacyReward=NativeDarkHollowWizardDeathRewardPlan.Capture(path,legacyOverlay);
        Program.Check(legacy.FinalDataSha256=="6ed74dbce04d6712e0d9ac12cfd05ae6ac334e0ec39fb7420f2105076132e537","legacy placed resource golden");
        Program.Check(Hash(legacyOverlay.CopyOverlay())=="10f82ee1028a9ad4fa9045d948a88e76545847031b954ac08ad0e74e6e9c7083","legacy shared behavior golden");
        Program.Check(Hash(legacyOverlay.CopyExecutable())=="14aa937f198740373b37312eb3054cad46dd1ae05f1920c145d6bc1fde9a8635","legacy copy-buffer executable golden");
        Program.Check(legacyReward.ExecutableSha256=="ec1e8ae38fed404f9a28ccc7fbf17d81e92a341aab89906e94c5e6c793f4c3b4","legacy reward golden");
        var issuer=NativeDarkHollowWizardAppendSource.Capture(path,catalog);
        var first=issuer.CompileActive(issuer.EmptyOwners(),[new("z")]);
        var firstPlace=NativeDarkHollowWizardAppendPlacementSource.Capture(path,first).Compile([new("z",z)]);
        var one=await Compose(first,firstPlace);
        var oneOverlay=NativeDarkHollowWizardOverlayPlan.Compile(behavior,one,effects);
        var oneReward=NativeDarkHollowWizardDeathRewardPlan.Capture(path,oneOverlay);
        Program.Check(one.CopyFinalLevelData().SequenceEqual(legacy.CopyFinalLevelData())&&oneOverlay.CopyOverlay().SequenceEqual(legacyOverlay.CopyOverlay())&&
            oneOverlay.CopyExecutable().SequenceEqual(legacyOverlay.CopyExecutable())&&oneReward.CopyExecutable().SequenceEqual(legacyReward.CopyExecutable()),"new one-owner path reuses exact legacy byte owners");
        Program.Check(JsonSerializer.Serialize(oneReward)==JsonSerializer.Serialize(legacyReward),"legacy reward public serialized shape/value preserved");
        Program.Check(JsonSerializer.Serialize(oneOverlay)==JsonSerializer.Serialize(legacyOverlay)&&one.GraphFingerprint==legacy.GraphFingerprint,
            "same-byte single-owner overlay full serializer and immutable resource graph retain legacy values");
        byte[] accepted=first.ProposedOwners.Serialize();
        var reopened=NativeDarkHollowWizardAppendSource.Capture(path,catalog);
        var two=reopened.CompileActive(reopened.RestoreOwners(accepted),[new("a"),new("z")]);
        var twoPlace=NativeDarkHollowWizardAppendPlacementSource.Capture(path,two).Compile([new("a",a),new("z",z)]);
        var resources=await Compose(two,twoPlace);
        var overlay=NativeDarkHollowWizardOverlayPlan.Compile(behavior,resources,effects);
        var reward=NativeDarkHollowWizardDeathRewardPlan.Capture(path,overlay);
        byte[] data=resources.CopyFinalLevelData();int scene=I32(data,0x18);
        // These new expectations were independently obtained by the raw-sector
        // oracle before receiving the C# output; no old golden was changed.
        Program.Check(Hash(data)=="375b9dc41a76d507ded9ffdf442e4febbb1916e68bb5d4597477bb9031e2fd90"&&
            reward.ExecutableSha256=="ce9c99ba467b089603850989bd141f374a7f86cf8e4deeab3fea1a3fc30164d1","independent complete two-resource and reward120 oracle");
        Program.Check(data.AsSpan(scene,0x11800).SequenceEqual(twoPlace.CopyScene()),"complete issued two-owner placement materialized once");
        byte[] oldData=legacy.CopyFinalLevelData();
        Program.Check(data.AsSpan(0,scene).SequenceEqual(oldData.AsSpan(0,scene))&&data.AsSpan(scene+0x11800).SequenceEqual(oldData.AsSpan(scene+0x11800)),
            "whole model/player/VRAM/SPU/descriptor/data complement outside the one scene owner");
        Program.Check(data.AsSpan(scene+0x8C38,128*0x58).SequenceEqual(two.CopyOriginalScene().AsSpan(0x8C38,128*0x58)),"all original128 rows exactly preserved");
        Program.Check(I32(data,scene+0x10F90)==151&&two.FixupCountAfter==151,"all151 native pointer fixups retained/appended");
        Program.Check(I32(data,scene+0x8C34)==130&&resources.RuntimeMemory.PlacedMobyCount==130&&resources.RuntimeMemory.DynamicMobyCapacity==89,"actual native row count and shared dynamic debit");
        Program.Check(resources.GenericCountAfter==legacy.GenericCountAfter&&resources.Resources.SequenceEqual(legacy.Resources),"immutable models registered only once for both instances");
        Program.Check(overlay.CopyOverlay().SequenceEqual(legacyOverlay.CopyOverlay())&&overlay.CopyExecutable().SequenceEqual(legacyOverlay.CopyExecutable()),"count-aware native loops require no duplicate behavior code or changed allocator guard");
        Program.Check(reward.TreasureTargetAfter==120&&BinaryPrimitives.ReadUInt16LittleEndian(reward.CopyExecutable().AsSpan(0x5FC3C))==120,"two actual ten-value owner identities derive target120");
        byte[] inverse=reward.RevertToOverlayExecutable(reward.CopyExecutable());
        Program.Check(inverse.SequenceEqual(overlay.CopyExecutable()),"whole EXE reward inverse");
        byte[] expected=inverse.ToArray();BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(0x5FC3C),120);
        Program.Check(expected.SequenceEqual(reward.CopyExecutable()),"only target u16 differs from exact installed executable");
        for(int i=0;i<2;i++)
        {
            int row=scene+0x8C38+(128+i)*0x58,props=I32(data,row);
            Program.Check(props==0xEED4+i*0x50&&I32(data,scene+props)==props+0x28,"distinct source-owned private object/internal pointer");
            Program.Check(data[row+0x53]==0x56&&data[row+0x3A]==0x7F&&data[row+0x48]==0&&data[row+0x49]==0,"actual native deterministic initial reward state per owner");
        }
        // Same complete bytes do not confer the same temporal identity or ancestry.
        var alias=issuer.CompileActive(issuer.EmptyOwners(),[new("other")]);
        var aliasPlace=NativeDarkHollowWizardAppendPlacementSource.Capture(path,alias).Compile([new("other",z)]);
        Program.Check(alias.CopyScene().SequenceEqual(first.CopyScene())&&aliasPlace.CopyScene().SequenceEqual(firstPlace.CopyScene())&&!alias.ProposedOwners.Serialize().SequenceEqual(accepted),"equal-byte/different-ledger hostile fixture is real");
        Program.RejectData(()=>Compose(first,aliasPlace).GetAwaiter().GetResult(),"actual append and ledger ancestor","equal-byte different-ledger cross pair");
        var retry=issuer.CompileActive(issuer.RestoreOwners(accepted),[new("z")]);
        Program.RejectData(()=>Compose(retry,firstPlace).GetAwaiter().GetResult(),"actual append and ledger ancestor","equal-ledger recapture still requires actual placement ancestor");
        Program.Reject(()=>Compose(two,firstPlace).GetAwaiter().GetResult(),"different-count cross pair");
        var three=issuer.CompileActive(issuer.RestoreOwners(two.ProposedOwners.Serialize()),[new("z"),new("a"),new("third")]);
        Program.RejectData(()=>Compose(three,twoPlace).GetAwaiter().GetResult(),"exactly one or two source-issued owners","profile refuses more than two actual owners before cross-pair/materialization");
        Program.Reject(()=>issuer.CompileActive(two.ProposedOwners.Retire(["z"]),[new("a")]),"retired reservation cannot become an emitted native tombstone");
        Program.Reject(()=>issuer.CompileActive(two.ProposedOwners,[new("a")]),"existing owner cannot disappear or be compacted from composition history");
        using var canceled=new CancellationTokenSource();canceled.Cancel();
        try { await NativeDarkHollowWizardResourcePlan.CompileWithAppendPlacementAsync("missing-do-not-open.bin",inventory,player,sound,texture,particle,two,twoPlace,canceled.Token);throw new InvalidOperationException("Cancellation not observed before I/O"); }
        catch(OperationCanceledException){Program.Check(true,"cancellation before opening nonexistent source");}
        foreach(int relative in new[]{0x8C34,0x8C38,0xB838,0xB838+0x36,0xB838+0x53,0xB890+0x3A,0xB890+0x48,0xEF24,0x10F90,0x117FF})
        { byte[] bad=data.ToArray();bad[scene+relative]^=1;Program.Reject(()=>resources.RequireFinal(bad),"resource scene ancestor readback mismatch");Program.Reject(()=>overlay.RequireFinal(overlay.CopyOverlay(),overlay.CopyExecutable(),bad),"complete installed scene mismatch"); }
        foreach(int at in new[]{0,0x4B048,0x5FC3C,0x5FC3D,0x65FFF})
        {byte[] bad=reward.CopyExecutable();bad[at]^=1;Program.Reject(()=>reward.RequireExecutable(bad),"reward owner/complement mutation");Program.Reject(()=>reward.RevertToOverlayExecutable(bad),"reward inverse refuses altered owner/complement");}
        Program.Check(!overlay.CompleteNativeClosureProven&&!overlay.RuntimeFitProven&&!overlay.PublicationApproved&&!reward.RuntimeVerified&&!reward.PublicationApproved,"count-aware source composition grants no gameplay/export authority");
        Program.Check(first.ProposedOwners.Serialize().SequenceEqual(accepted),"accepted owner state remains unchanged after success/refusal");
        Console.WriteLine($"two data={Hash(data)} overlay={Hash(overlay.CopyOverlay())} executable={Hash(overlay.CopyExecutable())} reward={reward.ExecutableSha256}");
        Task<NativeDarkHollowWizardResourcePlan> Compose(NativeDarkHollowWizardAppendPlan append,NativeDarkHollowWizardAppendPlacementPlan placement)=>
            NativeDarkHollowWizardResourcePlan.CompileWithAppendPlacementAsync(path,inventory,player,sound,texture,particle,append,placement);
    }
    private static int I32(byte[] b,int at)=>BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(at));
    private static string Hash(byte[] b)=>Convert.ToHexStringLower(SHA256.HashData(b));
}
