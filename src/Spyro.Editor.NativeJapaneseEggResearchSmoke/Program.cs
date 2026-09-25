using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;

int checks=0;
void Check(bool condition,string why){if(!condition)throw new Exception(why);checks++;}
void Refuse(Action action,string why){try{action();}catch(ArgumentException){checks++;return;}catch(InvalidDataException){checks++;return;}throw new Exception("Expected refusal: "+why);}
static string Hash(ReadOnlySpan<byte> b)=>Convert.ToHexStringLower(SHA256.HashData(b));
Check(typeof(NativeJapaneseEggResearchCandidatePlan).GetConstructors(BindingFlags.Public|BindingFlags.Instance).Length==0,"No caller constructed candidate authority.");
Check(typeof(NativeJapaneseEggResearchCandidatePlan).GetMethod("CaptureAsync")!.GetParameters().Select(p=>p.Name).SequenceEqual(
    new[]{"japaneseImagePath","originalImagePath","originalCuePath","token"}),"Fixed source factory accepts no caller coordinates, modes, callbacks, approval flags or receipt.");
Check(!typeof(NativeDarkHollowWizardResearchCandidatePlan).IsAssignableFrom(typeof(NativeJapaneseEggResearchCandidatePlan)),"Egg profile cannot masquerade as a Wizard.");
Refuse(()=>NativeJapaneseEggResearchCandidatePlan.CaptureAsync("", "missing.bin","missing.cue").GetAwaiter().GetResult(),"missing JP before source I/O");
Refuse(()=>NativeJapaneseEggResearchCandidatePublisher.PublishAsync(null!,"unused-output").GetAwaiter().GetResult(),"metadata cannot issue a publication plan");
Refuse(()=>NativeJapaneseEggResearchCandidatePublisher.VerifyAsync(null!,"unused-output").GetAwaiter().GetResult(),"output metadata cannot issue source authority");
using(var canceled=new CancellationTokenSource())
{
    canceled.Cancel();try{await NativeJapaneseEggResearchCandidatePlan.CaptureAsync("missing-jp","missing-usa","missing-cue",canceled.Token);throw new Exception("Cancellation ignored");}
    catch(OperationCanceledException){checks++;}
}
Refuse(()=>NativePackedResearchEntryComposer.ComposeArtisans(new byte[0x800],0x1000,null!,(_,_)=>throw new Exception("Unexpected source I/O")),"packed Artisans entry requires actual integration");
Refuse(()=>NativeWizardResearchPublicationBinding.FromJapaneseEgg(null!),"null cannot bind F5 role");
Check(typeof(NativeWizardResearchPublicationBinding).GetConstructors(BindingFlags.Public|BindingFlags.Instance).Length==0,"Publication role constructor is private.");
Check(typeof(NativePackedResearchImageOwner).GetConstructors(BindingFlags.Public|BindingFlags.Instance).Length==0,"Physical payload metadata cannot construct a source owner.");
Check(typeof(NativeWizardResearchPublicationBinding).GetMethods(BindingFlags.Static|BindingFlags.NonPublic).Where(m=>m.Name.StartsWith("From")).All(m=>
    m.GetParameters().Length==1&&m.GetParameters()[0].ParameterType.IsSealed&&!typeof(Delegate).IsAssignableFrom(m.GetParameters()[0].ParameterType)),"Binding factories accept only closed sealed actual plans, not callbacks or profile labels.");
Check(NativeJapaneseEggResearchCandidatePlan.OutputNames.Distinct().Count()==3&&NativeJapaneseEggResearchCandidatePlan.OutputNames.All(n=>!n.Contains("wizard",StringComparison.OrdinalIgnoreCase)),"Fixed F5 output identity cannot collide with legacy Wizard artifacts.");
Console.WriteLine($"NATIVE_JP_EGG_RESEARCH_SOURCE_FREE_OK checks={checks} sourceOrOutputIo=0");
if(args.Length==0)return;
if(args.Length is not(4 or 5)||args[0] is not("--source" or "--publish" or "--verify" or "--publication-faults")||
    args[0]=="--source"&&args.Length!=4||args[0]!="--source"&&args.Length!=5)
    throw new ArgumentException("Usage: --source JP.bin USA.bin USA.cue | --publish|--verify|--publication-faults JP.bin USA.bin USA.cue NEW-directory. Source mode writes no image; publication-faults never commits a candidate.");
var plan=await NativeJapaneseEggResearchCandidatePlan.CaptureAsync(args[1],args[2],args[3]);
var binding=NativeWizardResearchPublicationBinding.FromJapaneseEgg(plan);
Check(binding.SourceImagePath==plan.SourceImagePath&&binding.JapaneseSourceImagePath==plan.JapaneseSourceImagePath&&binding.SourceCuePath==plan.SourceCuePath,"All three actual source paths survive closed-role binding.");
Check(binding.PlanSha256==plan.PlanSha256&&binding.ImageName==NativeJapaneseEggResearchCandidatePlan.ImageName&&binding.CueName==NativeJapaneseEggResearchCandidatePlan.CueName&&binding.ReceiptName==NativeJapaneseEggResearchCandidatePlan.ReceiptName,"Closed role preserves fixed candidate/receipt identity.");
Check(binding.LeaseName==".native-japanese-eggs.writer.lock"&&binding.StagePrefix==".native-japanese-eggs-stage-"&&binding.OutputNames.SequenceEqual(NativeJapaneseEggResearchCandidatePlan.OutputNames),"Distinct F5 stage/lease owners cannot be selected by caller metadata.");
Check(binding.CopyCueBytes().SequenceEqual(plan.CopyCueBytes()),"Closed role returns actual source-bound CUE bytes.");
Check(plan.GrowthBytes==0x1800&&plan.RelocatedExecutableLba==53878&&plan.FinalExecutableEndLba==54082,"Exactly3-sector model/overlay growth.");
Check(plan.Integration.Rows.Count==2&&plan.Integration.Rows.All(r=>r.Placement.ProfileIndex==1),"Only two original native mode1 rows.");
plan.Placement.RequireIntegration(plan.Integration);checks++;
Check(ReferenceEquals(plan.RenderAdmission.Integration,plan.Integration)&&plan.Memory.IntegrationPlanSha256==plan.Integration.PlanSha256,"Actual render and memory ancestors are bound to this integration.");
Check(plan.Memory.ModeledLoadExtentsFitProven&&plan.Memory.SynchronousVisualStackFitProven&&!plan.Memory.FullRuntimePeakProven,"Bounded memory evidence is not full peak approval.");
Check(!plan.Integration.ActivationApproved&&!plan.NormalCreateBinApproved&&!plan.CompleteNativeClosureProven&&!plan.RuntimeVerified&&!plan.PickupEnabled&&!plan.PersistentSaveChanges&&!plan.IndependentColorVariants,"Visual research plan never promotes general export, pickup, save or independent color semantics.");
Refuse(plan.Integration.RequireNativeActivation,"old activation gate remains intact");
byte[] wad=plan.CopyWad();
foreach(var e in plan.Entries)
{
    Check(BinaryPrimitives.ReadInt32LittleEndian(wad.AsSpan(e.Index*8))==e.FinalOffset&&BinaryPrimitives.ReadInt32LittleEndian(wad.AsSpan(e.Index*8+4))==e.FinalLength,"Actual packed directory owner "+e.Index);
    Check(Hash(wad.AsSpan(e.FinalOffset,e.FinalLength))==e.FinalSha256,"Complete final packed entry "+e.Index);
    if(e.Index is not(9 or 10))Check(e.OriginalLength==e.FinalLength&&e.OriginalSha256==e.FinalSha256,"Every unrelated WAD owner remains byte-exact.");
}
Check(wad.AsSpan(plan.Entries.Single(e=>e.Index==9).FinalOffset,0xE800).SequenceEqual(plan.Integration.CopyOverlay()),"Actual final overlay from integration.");
Check(wad.AsSpan(plan.Entries.Single(e=>e.Index==10).FinalOffset,plan.Integration.CopyLevelData().Length).SequenceEqual(plan.Integration.CopyLevelData()),"Actual final data from integration.");
Check(plan.CopyExecutable().SequenceEqual(plan.Integration.CopyExecutable()),"Executable has only already-owned integration edits.");
plan.RequireStageReversion();await plan.RequireFreshAsync();checks++;
using FileStream source=new(args[2],FileMode.Open,FileAccess.Read,FileShare.Read);
foreach(int lba in new[]{53874,53877,54078,54079,54080,54081})
{
    byte[] sector=new byte[2352];source.Position=(long)lba*2352;source.ReadExactly(sector);byte[] after=plan.ExpectedSector(lba,sector);
    RawMode2Form1SectorIntegrity.RequireLogicalWriteSector(after,plan.Layout,lba);
    Check(after[18]==after[22]&&(after[18]&0x81)==(lba is 53877 or 54081?0x81:0),"Raw gap/EOF owner "+lba);
}
using var expectedHash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
using var sourceHash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
source.Position=0;byte[] raw=new byte[2352];int changed=0;
for(int lba=0;lba<source.Length/2352;lba++)
{
    source.ReadExactly(raw);sourceHash.AppendData(raw);byte[] expected=plan.ExpectedSector(lba,raw);expectedHash.AppendData(expected);
    if(!expected.AsSpan().SequenceEqual(raw))changed++;
    if(lba!=plan.Layout.RootExtent&&(lba<37||lba>=54082))Check(expected.AsSpan().SequenceEqual(raw),"Protected raw-sector complement "+lba);
}
string expectedImage=Convert.ToHexStringLower(expectedHash.GetHashAndReset());
Check(Convert.ToHexStringLower(sourceHash.GetHashAndReset())=="fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37","Whole original physical source unchanged.");
using var receipt=JsonDocument.Parse(plan.BuildReceipt((expectedImage,changed)));
Check(receipt.RootElement.GetProperty("profile").GetString()==NativeJapaneseEggResearchCandidatePlan.Profile&&receipt.RootElement.GetProperty("opaqueRenderMode").GetInt32()==1,"Receipt preserves fixed visual-only profile.");
Check(!receipt.RootElement.GetProperty("PickupEnabled").GetBoolean()&&!receipt.RootElement.GetProperty("PersistentSaveChanges").GetBoolean(),"Receipt cannot imply collector/card behavior.");
if(args[0]=="--publish")Console.WriteLine("NATIVE_JP_EGG_RESEARCH_PUBLICATION "+JsonSerializer.Serialize(await NativeJapaneseEggResearchCandidatePublisher.PublishAsync(plan,args[4])));
if(args[0]=="--verify")Console.WriteLine("NATIVE_JP_EGG_RESEARCH_VERIFIED "+JsonSerializer.Serialize(await NativeJapaneseEggResearchCandidatePublisher.VerifyAsync(plan,args[4])));
if(args[0]=="--publication-faults")await PublicationFaults.Run(plan,args[4],expectedImage);
Console.WriteLine($"NATIVE_JP_EGG_RESEARCH_SOURCE_OK checks={checks} plan={plan.PlanSha256} wad={plan.WadSha256} expectedImage={expectedImage} changed={changed} publishedImages={(args[0]=="--publish"?1:0)} disposableStagingCopies={(args[0]=="--publication-faults"?7:0)} runtime=false normalExport=false");
