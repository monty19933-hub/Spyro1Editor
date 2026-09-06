using System.Buffers.Binary;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

internal static class NativeWizardOverlayCases
{
    internal static void Run(Action<bool,string> check,Action<Action,string> reject)
    {
        var domain=new NativeAddressSpace(NativeAddressKind.LevelData,"owned-overlay-fixture",NativeAddressRevision.Final,0x20000);
        var owner=new NativeByteExtent(domain,0xB100,0x44);var lightning=new NativeByteExtent(domain,0xB000,4);var wizard=new NativeByteExtent(domain,0xB040,4);
        byte[] original=Convert.FromHexString("fb000224f4186210fc00622847004010");
        byte[] stub=NativeDarkHollowWizardOverlayPlan.BuildDispatch(original,owner,lightning,wizard);
        for(uint id=0;id<512;id++)
        {
            var before=Execute(original.AsSpan(0,12).ToArray(),0x8007AF24,id);
            var after=Execute(stub,0x8007AA38+(uint)owner.Offset,id);
            uint expected=id==0x26?0x8007AA38+(uint)lightning.Offset:id==0x11B?0x8007AA38+(uint)wizard.Offset:before.Pc;
            check(after.Pc==expected,"Private class hook changed an unmatched native dispatch destination.");
            if(id!=0x26&&id!=0x11B)check(before.Registers.SequenceEqual(after.Registers),"Private hook changed unmatched native branch/delay register semantics.");
        }
        reject(()=>NativeDarkHollowWizardOverlayPlan.BuildDispatch(new byte[16],owner,lightning,wizard),"preimage");
        var other=new NativeAddressSpace(NativeAddressKind.LevelData,"owned-overlay-fixture",NativeAddressRevision.Final,0x20000);
        reject(()=>NativeDarkHollowWizardOverlayPlan.BuildDispatch(original,owner,new(other,0xB000,4),wizard),"domain");
        var arena=new NativeByteExtent(domain,0xB000,16);
        NativeWizardOverlayBlock[] blocks=[new("a",new(domain,0xB000,8),new byte[8]),new("b",new(domain,0xB008,8),new byte[8])];
        NativeWizardOverlayEntry[] entries=[new("target","b",new(domain,0xB008,4))];
        check(NativeDarkHollowWizardOverlayPlan.Materialize(arena,blocks,entries,[entries[0].Owner]).Length==16,"Owned code materialization changed exact arena coverage.");
        reject(()=>NativeDarkHollowWizardOverlayPlan.Materialize(arena,blocks[..1],entries,[entries[0].Owner]),"coverage");
        reject(()=>NativeDarkHollowWizardOverlayPlan.Materialize(arena,[blocks[0],blocks[0]],[],[]),"overlap");
        reject(()=>NativeDarkHollowWizardOverlayPlan.Materialize(arena,blocks,entries,[new(domain,0xB00C,4)]),"entry");
        reject(()=>NativeDarkHollowWizardOverlayPlan.Materialize(arena,blocks,entries,[new(other,0xB008,4)]),"domain");
        reject(()=>NativeDarkHollowWizardOverlayPlan.Materialize(arena,[blocks[0] with{Bytes=new byte[4]},blocks[1]],entries,[]),"length");
        check(NativeDarkHollowWizardOverlayPlan.Materialize(arena,blocks.Reverse(),entries,[entries[0].Owner]).SequenceEqual(new byte[16]),"Materialization depends on caller enumeration order.");
        reject(()=>NativeDarkHollowWizardOverlayPlan.Materialize(arena,Enumerable.Repeat(blocks[0],65),[],[]),"budget");
        reject(()=>NativeDarkHollowWizardOverlayPlan.Materialize(arena,blocks,Enumerable.Repeat(entries[0],257),[]),"budget");
        reject(()=>NativeDarkHollowWizardOverlayPlan.Materialize(arena,blocks,entries,Enumerable.Repeat(entries[0].Owner,1025)),"budget");
        Console.WriteLine("NATIVE_WIZARD_OWNED_OVERLAY_DISPATCH_AND_MATERIALIZATION_OK");
    }
    internal static async Task Source(string image,Action<bool,string> check,Action<Action,string> reject)
    {
        var inventory=NativeMobySourceInventory.Capture(image,LevelCatalog.Load(Environment.CurrentDirectory));
        var playerSource=await NativePlayerAnimationSource.CaptureAsync(image);var player=await playerSource.CompileMergeAsync(image,33,12,[8]);
        var soundSource=await NativeGlobalSoundSource.CaptureAsync(image);
        var sound=await soundSource.CompileModelSlotMergeAsync(image,inventory,33,12,0x11B,[0,1,2],
            [new(0x1F,NativeGlobalSoundMappingPolicy.ExplicitRebindUnresolvedConsumers),new(0x26)]);
        var texture=NativeDarkHollowWizardTexturePlan.Capture(image);var scene=NativeDarkHollowWizardScenePlan.Capture(image);
        var descriptor=await NativeDarkHollowWizardParticlePlan.CaptureAsync(image,texture);
        var resources=await NativeDarkHollowWizardResourcePlan.CompileWithParticleAsync(image,inventory,player,sound,texture,descriptor,scene);
        var source=NativeDarkHollowWizardBehaviorSource.Capture(image);var particle=NativeDarkHollowWizardParticleBehaviorSource.Capture(image);
        var plan=NativeDarkHollowWizardOverlayPlan.Compile(source,resources,particle);
        byte[] overlay=plan.CopyOverlay(),exe=plan.CopyExecutable(),data=plan.CopyFinalLevelData();plan.RequireFinal(overlay,exe,data);
        check(plan.OverlaySha256=="10f82ee1028a9ad4fa9045d948a88e76545847031b954ac08ad0e74e6e9c7083"&&
            plan.ExecutableSha256=="14aa937f198740373b37312eb3054cad46dd1ae05f1920c145d6bc1fde9a8635",
            "Appended overlay differs from the independent raw-source byte oracle.");
        check(overlay.Length>0xB000&&overlay.Length%0x800==0&&plan.AllCodeTargetsMaterialized&&plan.ActiveDispatchEncoded,"Owned native code and hooks were not completely materialized.");
        check(plan.CodeOwners[0].OverlayOffset==0xB000&&plan.CodeOwners.Zip(plan.CodeOwners.Skip(1)).All(p=>p.First.OverlayOffset+p.First.ByteLength==p.Second.OverlayOffset)&&
            plan.CodeOwners[^1].OverlayOffset+plan.CodeOwners[^1].ByteLength==overlay.Length,"Public code owner evidence does not exactly partition all appended bytes.");
        check(plan.CodeOwners.All(o=>o.RuntimePc==0x8007AA38+o.OverlayOffset&&o.Sha256==Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(overlay.AsSpan(o.OverlayOffset,o.ByteLength)))),
            "Native code owner byte hashes/runtime addresses differ from actual materialized bytes.");
        byte[] original=plan.CopyOriginalOverlay();int[] fields=[0x4EC,0x4F0,0x158,0x240,0x29C,0x384];
        check(Enumerable.Range(0,0xB000).Where(at=>!fields.Any(f=>at>=f&&at<f+4)).All(at=>overlay[at]==original[at]),"Unowned original overlay byte changed.");
        check(overlay.AsSpan(0xAB5C,0x4A4).SequenceEqual(original.AsSpan(0xAB5C,0x4A4)),"Nonzero source tail was treated as free code space.");
        var nativeExe=plan.CopyOriginalExecutable();
        check(exe.AsSpan(0,0x4B048).SequenceEqual(nativeExe.AsSpan(0,0x4B048))&&exe.AsSpan(0x4B050).SequenceEqual(nativeExe.AsSpan(0x4B050)),"EXE copy-buffer relocation changed other native bytes.");
        check(plan.OriginalCopyBufferAddress==0x80085594&&plan.FinalCopyBufferAddress==0x8007AA38+overlay.Length,"New copy buffer does not follow the complete overlay owner.");
        check(plan.RuntimeMemory.CopyBufferAddress==plan.FinalCopyBufferAddress&&plan.RuntimeMemory.SceneReadEnd==resources.RuntimeMemory.SceneReadEnd+plan.FinalCopyBufferAddress-plan.OriginalCopyBufferAddress&&
            plan.RuntimeMemory.KnownPostLoadExtentsDisjoint&&plan.RuntimeMemory.DynamicMobyCapacity==90,"Composed native resident-memory/capacity accounting did not include code growth.");
        check(plan.RuntimeMemory.UsedLevelDataBytes==0x387AC&&plan.RuntimeMemory.ModelBytesRead==0x72800&&plan.RuntimeMemory.SceneBytesRead==0x11800&&
            plan.RuntimeMemory.ModelRamStart==0x800BF9E4&&plan.RuntimeMemory.SceneRamStart==0x801321E4&&plan.RuntimeMemory.SceneReadEnd==0x801439E4&&
            plan.RuntimeMemory.NormalReservedBufferGapBytes==0x441CC,"Source-derived overlay/data/model/scene native RAM endpoints changed.");
        check(!plan.CompleteNativeClosureProven&&!plan.GteStateClosureProven&&!plan.TransitiveGlobalClosureProven&&!plan.RuntimeFitProven&&!plan.PlacementApproved&&!plan.PublicationApproved,
            "Encoded native hooks promoted unresolved transitive/runtime/placement/publication gates.");
        byte[] bad=overlay.ToArray();bad[0]^=1;reject(()=>plan.RequireFinal(bad,exe,data),"overlay");
        bad=overlay.ToArray();bad[0xB000]^=1;reject(()=>plan.RequireFinal(bad,exe,data),"overlay");
        var badExe=exe.ToArray();badExe[0x4B048]^=1;reject(()=>plan.RequireFinal(overlay,badExe,data),"executable");
        var badData=data.ToArray();badData[^1]^=1;reject(()=>plan.RequireFinal(overlay,exe,badData),"data");
        var missingScene=await NativeDarkHollowWizardResourcePlan.CompileWithParticleAsync(image,inventory,player,sound,texture,descriptor);
        reject(()=>NativeDarkHollowWizardOverlayPlan.Compile(source,missingScene,particle),"scene");
        var missingDescriptor=await NativeDarkHollowWizardResourcePlan.CompileAsync(image,inventory,player,sound,texture,scene);
        reject(()=>NativeDarkHollowWizardOverlayPlan.Compile(source,missingDescriptor,particle),"particle");
        check(plan.CopyOverlay()[0]!=bad[0]||plan.CopyOverlay()[0xB000]!=bad[0xB000],"Returned overlay is not detached.");
        Console.WriteLine($"NATIVE_DARK_WIZARD_OWNED_OVERLAY_SOURCE_OK overlay={plan.OverlaySha256} exe={plan.ExecutableSha256} copy={plan.FinalCopyBufferAddress:X8} gap={plan.RuntimeMemory.NormalReservedBufferGapBytes:X} runtime/publication=false");
        NativeWizardDeathRewardCases.Source(image,plan,check,reject);
    }
    private static (uint Pc,uint[] Registers) Execute(byte[] code,uint start,uint actorClass)
    {
        uint[] r=Enumerable.Range(0,32).Select(i=>0xDADA0000u+(uint)i).ToArray();r[0]=0;r[3]=actorClass;
        uint pc=start;uint? delayed=null;
        for(int steps=0;steps<128;steps++)
        {
            if(pc<start||pc>=start+code.Length)return(pc,r);
            uint w=BinaryPrimitives.ReadUInt32LittleEndian(code.AsSpan((int)(pc-start)));int rs=(int)(w>>21)&31,rt=(int)(w>>16)&31;short imm=(short)w;
            var next=delayed;delayed=null;
            switch(w>>26)
            {
                case 0:if(w!=0)throw new InvalidDataException("Unsupported dispatch SPECIAL.");break;
                case 2:delayed=((pc+4)&0xF0000000)|((w&0x3FFFFFF)*4);break;
                case 4:delayed=r[rs]==r[rt]?unchecked(pc+4+(uint)(imm*4)):pc+8;break;
                case 5:delayed=r[rs]!=r[rt]?unchecked(pc+4+(uint)(imm*4)):pc+8;break;
                case 9:r[rt]=unchecked(r[rs]+(uint)imm);break;
                case 10:r[rt]=(int)r[rs]<imm?1u:0u;break;
                default:throw new InvalidDataException("Unsupported dispatch instruction.");
            }
            r[0]=0;pc=next??pc+4;
        }
        throw new InvalidDataException("Dispatch interpreter exceeded bounded steps.");
    }
}
