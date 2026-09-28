using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;

int checks=0;
void Check(bool value,string why){checks++;if(!value)throw new Exception(why);}
void Refuse(Action action,string why)
{try{action();}catch(Exception e)when(e is InvalidDataException or ArgumentException){checks++;return;}throw new Exception("Expected refusal: "+why);}
static uint W(byte[] b,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(at));
static string Hash(byte[] b)=>Convert.ToHexStringLower(SHA256.HashData(b));
foreach(uint start in new uint[]{0x800C0000,0x800C0004,0x800C0008,0x800C000C,0x80167D54})
{
    var g=NativeCollectibleSaveWorkspacePlan.Measure(start,0x801879B0);
    Check(g.TailAddress==start&&g.ArenaAddress%16==0&&g.ArenaAddress-start==g.LeadingBytes,"Measured word-aligned tail has explicit sixteen-byte arena alignment");
    Check(g.SemanticBytes==0x49A0&&g.GrowthBytes==0x5000&&g.EndAddress==start+0x5000,"Actual preparation arena plus separate explicit buffer/state owners round to5000");
    Check(g.ExpectedOrdinaryAddress==g.ArenaAddress+NativeCollectibleSaveTailKernel.ArenaBytes&&
        g.OrdinaryReadbackAddress==g.ExpectedOrdinaryAddress+0x600&&g.TailReadbackAddress==g.OrdinaryReadbackAddress+0x600&&
        g.TransactionStateAddress==g.TailReadbackAddress+0x100&&g.TransactionStateAddress+0x100+g.TrailingBytes==g.EndAddress,"All separate owners partition the complete insertion");
    var k=NativeCollectibleSaveTailKernel.CreateFixture(g.ArenaAddress);
    Check(k.WorkspaceAddress+NativeSha256Kernel.RequiredWorkspaceByteLength<=g.ExpectedOrdinaryAddress&&k.CodeByteLength<=0x1000,"Measured real kernel highwater does not overlap ordinary snapshot");
    Check(g.EndAddress<=0x801879B0&&g.TailGapToFloor==0x801879B0L-g.EndAddress,"Tail-only arithmetic excludes the following scene, not lifetime or whole-resource fit approval");
}
foreach(var(start,floor)in new(uint,uint)[]{(0,0x801879B0),(0x800C0001,0x801879B0),(0x801879B0,0x801879B0),
    (0x801F0000,0x80200000),(0xFFFFFFFC,0xFFFFFFFF),(0x800C0000,0x800C4FFF),(0x800C0000,0xFFFFFFFF)})
    Refuse(()=>NativeCollectibleSaveWorkspacePlan.Measure(start,floor),"Unaligned/overflowing/overlapping or unbounded draft geometry");
var exact=NativeCollectibleSaveWorkspacePlan.Measure(0x80167D54,0x8016CD54);
Check(exact.TailGapToFloor==0&&exact.LeadingBytes==12&&exact.TrailingBytes==0x654,"Exact half-open floor and every padding byte accounted");
Refuse(()=>NativeCollectibleSaveWorkspacePlan.Measure(0x80167D54,0x8016CD53),"One-byte short floor");
Refuse(()=>NativeCollectibleSaveWorkspacePlan.Capture(null!,null!),"No caller-byte/synthetic source authority");
if(args.Length==0)
{Console.WriteLine($"COLLECTIBLE_SAVE_WORKSPACE_SOURCE_FREE_OK checks={checks} sourceReads=0 residence=false cardWrites=false publication=false");return;}
if(args.Length!=3||args[0]!="--source")throw new ArgumentException("Usage: no arguments or --source <JP-BIN> <USA-BIN>");
using var jp=NativeRegionalSourceSession.Open(args[1],NativeDiscRevision.JapanScps10083);
using var usa=NativeRegionalSourceSession.Open(args[2],NativeDiscRevision.UsaScus94228);
var ancestry=NativeJapaneseEggAdmittedLifecyclePlan.Capture(jp,usa);
var plan=NativeCollectibleSaveWorkspacePlan.Capture(usa,ancestry);
byte[] before=ancestry.CopyLevelData(),after=plan.CopyLevelData();
Check(ReferenceEquals(plan.Ancestry,ancestry)&&plan.SourceCertified&&plan.Preparation.SourceCertified,"Actual admitted source composition plus actual source preparation issuer");
Check(plan.GrowthBytes==0x5000&&after.Length==0x38A800&&W(after,0x14)==0x55000,"Complete measured model descriptor and body growth");
Check(plan.Memory.ModelStart==0x80117D54&&plan.Memory.OriginalSceneStart==0x80167D54&&plan.Memory.FinalSceneStart==0x8016CD54&&
    plan.Memory.OriginalSceneEnd==0x8017C554&&plan.Memory.FinalSceneEnd==0x80181554&&plan.Memory.NormalReservedStart==0x801879B0&&
    plan.Memory.OriginalNormalGap==0xB45C&&plan.Memory.FinalNormalGap==0x645C,"Independent exact final projection golden usesA00 not original800 floor");
Check(after.AsSpan(0x50,0x180).SequenceEqual(before.AsSpan(0x50,0x180)),"Every registered root/class stays byte-identical; no new actor registration");
Check(plan.Registrations.Count==NativeMobyModelRegistrationSource.DecodeFixture(plan.OriginalDomain,before).Registrations.Count,"Complete registration count preserved");
var headerChanges=Enumerable.Range(0x14,4).ToHashSet();
for(int i=0;i<10;i++)
{
    int n=(int)W(before,i*8+4);if(n==0)continue;
    int old=(int)W(before,i*8),now=(int)W(after,i*8);
    Check(after.AsSpan(now,n).SequenceEqual(before.AsSpan(old,n)),"Every retained subfile byte survives insertion "+i);
    Check(plan.Layout.Map(new(plan.OriginalDomain,old,n)).Final.Offset==now,"Complete retained owner relocation "+i);
    if(i>2)headerChanges.UnionWith(Enumerable.Range(i*8,4));
}
Check(Enumerable.Range(0,0x800).Where(i=>!headerChanges.Contains(i)).All(i=>after[i]==before[i]),"Full descriptor/header complement unchanged");
long cursor=plan.TailExtent.Offset;uint ram=plan.Geometry.TailAddress;
foreach(var owner in plan.Owners)
{
    byte[] bytes=plan.CopyOwnerBytes(owner.Role);
    Check(owner.Extent.Offset==cursor&&owner.Address==ram&&bytes.Length==owner.Extent.ByteLength&&Hash(bytes)==owner.InitialSha256,"Exact physical/normal-RAM partition "+owner.Role);
    if(owner.Role!="preparation-code")Check(!bytes.AsSpan().ContainsAnyExcept((byte)0),"Explicit zero initialization, not inferred free space "+owner.Role);
    cursor=owner.Extent.EndExclusive;ram+=checked((uint)owner.Extent.ByteLength);
}
Check(cursor==W(after,0x18)&&ram==plan.Memory.FinalSceneStart,"All alignment and semantic owners fill the new model tail exactly");
foreach(var block in plan.CodeBlocks)
    Check(after.AsSpan((int)block.Extent.Offset,(int)block.Extent.ByteLength).SequenceEqual(block.Bytes)&&Hash(block.Bytes)==block.Sha256,"Exact generated bounded code block "+block.Role);
Check(plan.RevertExact(after).SequenceEqual(before)&&plan.RevertToOriginalSourceExact(after).SequenceEqual(usa.ReadWadEntry(10)),"Complete admitted-parent and original-USA inverse");
plan.RequireCurrent(ancestry);checks++;
Refuse(()=>plan.RequireCurrent(null!),"Missing ancestry");
var sameBytesOtherOwner=NativeJapaneseEggAdmittedLifecyclePlan.Compose(ancestry.Admission,ancestry.Authored,ancestry.Lifecycle);
Refuse(()=>plan.RequireCurrent(sameBytesOtherOwner),"Identical bytes do not silently substitute a different composition owner");
foreach(int at in new[]{0,0x14,0x18,0x50,0x150,0x7FF,0x800,(int)plan.TailExtent.Offset,after.Length-1}.Concat(plan.Owners.SelectMany(o=>new[]{(int)o.Extent.Offset,(int)o.Extent.EndExclusive-1})).Distinct())
{var bad=after.ToArray();bad[at]^=1;Refuse(()=>plan.RevertExact(bad),"Complete afterimage corruption "+at);}
Refuse(()=>plan.RevertExact(after[..^1]),"Truncation");Refuse(()=>plan.RevertExact([..after,0]),"Extra afterimage bytes");
Refuse(()=>plan.CopyOwnerBytes("unknown"),"No arbitrary physical-owner selection");
Refuse(()=>plan.Layout.Map(new(plan.FinalDomain,0,4)),"Final revision cannot be claimed as parent");
Refuse(()=>plan.Layout.Map(new(plan.OriginalDomain,plan.TailExtent.Offset-4,8)),"Cannot cross old model/shifted scene ownership");
var changed=plan.CopyLevelData();changed[0]^=1;Check(plan.CopyLevelData().SequenceEqual(after),"Output copy cannot mutate the plan");
var code=plan.CodeBlocks[0].Bytes;code[0]^=1;Check(plan.CodeBlocks[0].Sha256==Hash(plan.CodeBlocks[0].Bytes),"Code evidence returns defensive bytes");
Check(plan.RequiredRowRebindings.All(r=>r.NewRowAddress==r.OldRowAddress+0x5000&&r.NewPrivateAddress==r.OldPrivateAddress+0x5000)&&
    plan.RequiredRowRebindings.Count==2&&!plan.CurrentGuardMatchesProjection,"Explicit shifted rows/private addresses invalidate the current guard");
Check(!plan.NativeResidenceIssued&&!plan.HooksInstalled&&!plan.InstructionCacheProven&&!plan.InterruptSafetyProven&&!plan.LifetimeProven&&
    !plan.OriginalModelReferenceClosureProven&&!plan.FullRuntimePeakProven&&!plan.RuntimeVerified&&
    !plan.AsynchronousCardLeaseProven&&!plan.FutureControllerFitProven&&!plan.CardWrites&&!plan.PublicationApproved,"All dynamic/execution/controller/publication gates remain closed");
Check(plan.Geometry.TailGapToFloor-plan.Memory.FinalNormalGap==plan.Memory.SceneBytes,"Whole-scene charge is not confused with the larger tail-only gap");
Check(plan.RequiredGlobalRebindings.Count==3&&plan.RequiredGlobalRebindings.All(g=>g.NewValue==g.OldValue+0x5000),"Scene, row-table and dynamic-row globals require explicit regeneration");
Refuse(plan.RequireNativeInstallation,"Detached insertion is not a publishable or installed composition");
Refuse(()=>NativeCollectibleSaveWorkspacePlan.Capture(jp,ancestry),"Wrong regional source");
NativeLoaderCases.Run(plan,Check);
jp.VerifyUnchanged();usa.VerifyUnchanged();
Console.WriteLine($"COLLECTIBLE_SAVE_WORKSPACE_SOURCE_OK checks={checks} growth={plan.GrowthBytes:X} normalGap={plan.Memory.FinalNormalGap:X} finalSha256={plan.FinalDataSha256} hooks=false cardWrites=false publication=false");
