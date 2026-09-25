using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;

int checks=0;
void Check(bool ok,string why){if(!ok)throw new Exception(why);checks++;}
void Refuse(Action action,string why){try{action();}catch(ArgumentException){checks++;return;}catch(InvalidDataException){checks++;return;}throw new Exception("Expected refusal: "+why);}
static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
Check(typeof(NativeJapaneseEggAuthoredResearchCandidatePlan).IsSealed&&typeof(NativeJapaneseEggAuthoredResearchCandidatePlan).GetConstructors().Length==0,"No caller metadata construction.");
Check(typeof(NativeJapaneseEggAuthoredResearchCandidatePlan).GetMethod("CaptureAsync")!.GetParameters().Select(p=>p.Name).SequenceEqual(new[]{"japaneseImagePath","originalImagePath","originalCuePath","token"}),"Source-only factory has no palette/position/class/profile/receipt knobs.");
Check(!typeof(NativeJapaneseEggResearchCandidatePlan).IsAssignableFrom(typeof(NativeJapaneseEggAuthoredResearchCandidatePlan)),"New profile cannot impersonate native-original R01.");
Check(NativeJapaneseEggAuthoredResearchCandidatePlan.OutputNames.Intersect(NativeJapaneseEggResearchCandidatePlan.OutputNames).Any()==false,"No legacy output name collision.");
Check(NativeJapaneseEggResearchCandidatePlan.Profile=="native-artisans-two-japanese-eggs-visual-research-v1"&&NativeJapaneseEggResearchCandidatePlan.ImageName=="artisans-native-japanese-eggs-visual-r01.bin","Legacy profile constants unchanged.");
Refuse(()=>NativeJapaneseEggAuthoredResearchCandidatePlan.CaptureAsync("","missing.bin","missing.cue").GetAwaiter().GetResult(),"invalid JP argument before source I/O");
Refuse(()=>NativeJapaneseEggAuthoredResearchCandidatePublisher.PublishAsync(null!,"unused-output").GetAwaiter().GetResult(),"null publication authority");
Refuse(()=>NativeJapaneseEggAuthoredResearchCandidatePublisher.VerifyAsync(null!,"unused-output").GetAwaiter().GetResult(),"null verification authority");
Refuse(()=>NativeWizardResearchPublicationBinding.FromAuthoredJapaneseEgg(null!),"null closed role");
Refuse(()=>NativePackedResearchEntryComposer.ComposeAuthoredArtisans(new byte[0x800],0x1000,null!,(_,_)=>throw new Exception("Unexpected I/O")),"null composition cannot issue packed entries");
Refuse(()=>NativePackedResearchImageOwner.CaptureAuthoredEgg("unused",null!,null!,null!),"null physical ancestry before I/O");
using(var canceled=new CancellationTokenSource())
{canceled.Cancel();try{await NativeJapaneseEggAuthoredResearchCandidatePlan.CaptureAsync("missing","missing","missing",canceled.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){checks++;}}
Check(typeof(NativeWizardResearchPublicationBinding).GetMethods(BindingFlags.Static|BindingFlags.NonPublic).Where(m=>m.Name.StartsWith("From")).All(m=>m.GetParameters().Length==1&&m.GetParameters()[0].ParameterType.IsSealed&&!typeof(Delegate).IsAssignableFrom(m.GetParameters()[0].ParameterType)),"All binding factories still accept only closed sealed actual plans.");
Console.WriteLine($"NATIVE_JP_EGG_AUTHORED_RESEARCH_SOURCE_FREE_OK checks={checks} sourceOrOutputIo=0");
if(args.Length==0)return;
if(args.Length is not(4 or 5)||args[0] is not("--source" or "--publication-faults" or "--verify" or "--publish")||args[0]=="--source"&&args.Length!=4||args[0]!="--source"&&args.Length!=5)
    throw new ArgumentException("Usage: --source JP.bin USA.bin USA.cue | --publication-faults|--verify|--publish JP.bin USA.bin USA.cue NEW-directory. Source mode writes no image; faults never commit a candidate.");
var plan=await NativeJapaneseEggAuthoredResearchCandidatePlan.CaptureAsync(args[1],args[2],args[3]);
var binding=NativeWizardResearchPublicationBinding.FromAuthoredJapaneseEgg(plan);var composition=plan.Composition;
Check(binding.SourceImagePath==plan.SourceImagePath&&binding.JapaneseSourceImagePath==plan.JapaneseSourceImagePath&&binding.SourceCuePath==plan.SourceCuePath,"Exact source path ancestry survives actual role binding.");
Check(binding.PlanSha256==plan.PlanSha256&&binding.ImageName==NativeJapaneseEggAuthoredResearchCandidatePlan.ImageName&&binding.CueName==NativeJapaneseEggAuthoredResearchCandidatePlan.CueName&&binding.ReceiptName==NativeJapaneseEggAuthoredResearchCandidatePlan.ReceiptName,"New closed role names/identity.");
Check(binding.StagePrefix==".native-japanese-eggs-authored-colors-stage-"&&binding.LeaseName==".native-japanese-eggs-authored-colors.writer.lock"&&binding.OutputNames.SequenceEqual(NativeJapaneseEggAuthoredResearchCandidatePlan.OutputNames),"Independent new stage and lease owners.");
Check(binding.CopyCueBytes().SequenceEqual(plan.CopyCueBytes()),"Role CUE comes from source-owned plan.");
Check(plan.GrowthBytes==0x3000&&plan.RelocatedExecutableLba==53881&&plan.FinalExecutableEndLba==54085,"Actual six-sector aggregate growth.");
Check(plan.Memory.PlanSha256==composition.PlanSha256&&plan.Memory.FinalDataSha256==composition.FinalDataSha256&&plan.Memory.FinalOverlaySha256==composition.FinalOverlaySha256,"Memory binds the larger final composition, not only ancestor.");
Check(plan.Memory.Values.SceneEnd==0x8017AD54&&plan.Memory.Values.NormalGap==0xCE5C&&plan.Memory.Values.NormalGap==plan.Memory.Ancestor.Values.NormalGap-0x1800,"Actual final model/scene footprint charged.");
Check(plan.Memory.ModeledLoadExtentsFitProven&&plan.Memory.SynchronousVisualStackFitProven&&!plan.Memory.FullRuntimePeakProven,"Bounded load/stack is not full peak proof.");
Check(plan.IndependentColorVariants&&!plan.LitPixelsVerified&&!plan.RuntimeVerified&&!plan.PickupEnabled&&!plan.PersistentSaveChanges&&!plan.NormalCreateBinApproved,"Independent root structure is not lit pixels or gameplay.");
Refuse(composition.RequireActivation,"existing detached gate remains intact");Refuse(composition.Integration.RequireNativeActivation,"original activation gate remains intact");
plan.Placement.RequireIntegration(composition.Integration);checks++;
Check(composition.FinalDataSha256=="6675cf971f53b6b3fbb31b0d40ffd54f5404d3b8049c1ef64fff44a068ee4dac"&&composition.FinalOverlaySha256=="76911d8441beaaf7788b5d4a9cc8d785274ffcf0f1b96d417db255598957ba66","Frozen authored composition byte goldens.");
byte[] wad=plan.CopyWad();
foreach(var e in plan.Entries)
{
    Check(BinaryPrimitives.ReadInt32LittleEndian(wad.AsSpan(e.Index*8))==e.FinalOffset&&BinaryPrimitives.ReadInt32LittleEndian(wad.AsSpan(e.Index*8+4))==e.FinalLength,"Actual packed directory owner "+e.Index);
    Check(Hash(wad.AsSpan(e.FinalOffset,e.FinalLength))==e.FinalSha256,"Complete final entry "+e.Index);
    if(e.Index is not(9 or 10))Check(e.OriginalLength==e.FinalLength&&e.OriginalSha256==e.FinalSha256,"Unrelated WAD entry preserved "+e.Index);
}
foreach(int entry in new[]{9,10})
{var e=plan.Entries.Single(e=>e.Index==entry);Check(wad.AsSpan(e.FinalOffset,e.FinalLength).SequenceEqual(entry==9?composition.CopyOverlay():composition.CopyLevelData()),"Typed final authored payload is the packed owner.");}
Check(plan.CopyExecutable().SequenceEqual(composition.CopyExecutable()),"No extra EXE renderer changes.");
plan.RequireStageReversion();await plan.RequireFreshAsync();checks++;
using FileStream source=new(args[2],FileMode.Open,FileAccess.Read,FileShare.Read);
foreach(int lba in new[]{53874,53880,54078,54079,54084})
{
    byte[] raw=new byte[2352];source.Position=(long)lba*2352;source.ReadExactly(raw);byte[] expected=plan.ExpectedSector(lba,raw);
    RawMode2Form1SectorIntegrity.RequireLogicalWriteSector(expected,plan.Layout,lba);
    Check(expected[18]==expected[22]&&(expected[18]&0x81)==(lba is 53880 or 54084?0x81:0),"Exact physical growth/EOF flags "+lba);
}
using var expectedHash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);using var originalHash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
source.Position=0;byte[] sector=new byte[2352];int changed=0;bool protectedComplement=true;
for(int lba=0;lba<source.Length/2352;lba++)
{
    source.ReadExactly(sector);originalHash.AppendData(sector);byte[] expected=plan.ExpectedSector(lba,sector);expectedHash.AppendData(expected);
    if(!sector.AsSpan().SequenceEqual(expected))changed++;
    if(lba!=plan.Layout.RootExtent&&(lba<37||lba>=54085))protectedComplement&=sector.AsSpan().SequenceEqual(expected);
}
string imageHash=Convert.ToHexStringLower(expectedHash.GetHashAndReset());
Check(protectedComplement&&Convert.ToHexStringLower(originalHash.GetHashAndReset())=="fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37","Complete raw complement and original image identity preserved.");
Check(imageHash=="8d99201784ec1c6be4219c98ac72e148258cb13a7a9b539d5937af83a9b4c1ff"&&changed==49589,"Independent Python full physical reconstruction golden.");
Check(plan.WadSha256=="081a25785ddfd66d99635fbdf2522bfb575f3c03fd195c1d6b3fdc3a5645f5a6"&&plan.RootSha256=="b890557eff8613a9c052bcb20c2ae468aa44f0f3cae08ff142d63d7a30ca4464","Independent packed WAD and ISO root goldens.");
using var receipt=JsonDocument.Parse(plan.BuildReceipt((imageHash,changed)));var json=receipt.RootElement;
Check(json.GetProperty("profile").GetString()==NativeJapaneseEggAuthoredResearchCandidatePlan.Profile&&!json.GetProperty("sharedModelColors").GetBoolean()&&json.GetProperty("IndependentColorVariants").GetBoolean(),"Receipt names diagnostic authored independent roots.");
Check(!json.GetProperty("RuntimeVerified").GetBoolean()&&!json.GetProperty("PickupEnabled").GetBoolean()&&!json.GetProperty("PersistentSaveChanges").GetBoolean(),"Receipt cannot imply runtime/collector/card behavior.");
Check(json.GetProperty("bindings").GetArrayLength()==2&&json.GetProperty("Rows").GetArrayLength()==2&&json.GetProperty("LevelDataSha256").GetString()==composition.FinalDataSha256,"Receipt describes actual final bindings and bytes.");
if(args[0]=="--publication-faults")await PublicationFaults.Run(plan,args[4],imageHash);
if(args[0]=="--verify")Console.WriteLine("NATIVE_JP_EGG_AUTHORED_VERIFIED "+JsonSerializer.Serialize(await NativeJapaneseEggAuthoredResearchCandidatePublisher.VerifyAsync(plan,args[4])));
if(args[0]=="--publish")Console.WriteLine("NATIVE_JP_EGG_AUTHORED_PUBLICATION "+JsonSerializer.Serialize(await NativeJapaneseEggAuthoredResearchCandidatePublisher.PublishAsync(plan,args[4])));
Console.WriteLine($"NATIVE_JP_EGG_AUTHORED_RESEARCH_SOURCE_OK checks={checks} plan={plan.PlanSha256} wad={plan.WadSha256} expectedImage={imageHash} changed={changed} publishedImages={(args[0]=="--publish"?1:0)} runtime=false");
