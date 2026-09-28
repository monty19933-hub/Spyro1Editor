using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;

int checks = 0;
void Check(bool ok,string why) { if (!ok) throw new Exception(why); checks++; }
void Refuse(Action action,string why)
{
    try { action(); }
    catch (Exception e) when (e is ArgumentException or InvalidOperationException or InvalidDataException or IOException) { checks++; return; }
    throw new Exception("Expected refusal: " + why);
}
static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
Check(typeof(NativeCollectibleCardAdmissionResearchPlan).IsSealed &&
    typeof(NativeCollectibleCardAdmissionResearchPlan).GetConstructors().Length == 0,"Closed research issuer.");
Check(typeof(NativeCollectibleCardAdmissionResearchPlan).GetMethod("CaptureAsync")!.GetParameters().Select(p => p.Name)
    .SequenceEqual(new[] { "originalImagePath","originalCuePath","token" }),"No caller payload/manifest/slot authority.");
Check(NativeCollectibleCardAdmissionResearchPlan.Profile == "native-collectible-card-admission-research-v1" &&
    NativeCollectibleCardAdmissionResearchPlan.ImageName == "collectible-card-admission-research-r01.bin" &&
    NativeCollectibleCardAdmissionResearchPlan.CueName == "collectible-card-admission-research-r01.cue","Fixed isolated identity.");
Check(!NativeCollectibleCardAdmissionResearchPlan.OutputNames.Intersect(NativeCollectibleCardLoadResearchPlan.OutputNames).Any(),
    "R01 card-load artifact names are protected.");
Check(typeof(NativeWizardResearchPublicationBinding).GetMethods(BindingFlags.Static|BindingFlags.NonPublic)
    .Where(m => m.Name.StartsWith("From",StringComparison.Ordinal)).All(m => m.GetParameters().Length == 1 &&
        m.GetParameters()[0].ParameterType.IsSealed && !typeof(Delegate).IsAssignableFrom(m.GetParameters()[0].ParameterType)),
    "Shared physical engine still has only closed typed plan bindings.");
Refuse(() => NativeCollectibleCardAdmissionResearchPlan.CaptureAsync("","missing").GetAwaiter().GetResult(),"empty source");
Refuse(() => NativeCollectibleCardAdmissionResearchPublisher.PublishAsync(null!,"unused").GetAwaiter().GetResult(),"null publish authority");
Refuse(() => NativeCollectibleCardAdmissionResearchPublisher.VerifyAsync(null!,"unused").GetAwaiter().GetResult(),"null verify authority");
Refuse(() => NativeWizardResearchPublicationBinding.FromCardAdmission(null!),"null binding");
Refuse(() => NativePackedResearchImageOwner.CaptureCardAdmission("unused",null!),"null physical owner");
Refuse(() => NativePackedResearchEntryComposer.ComposeCardAdmission(new byte[0x800],0x1000,null!,(_,_) => throw new Exception("Unexpected read")),
    "null typed entry owner");
var fixture = NativeCollectibleCardAdmissionPlan.CreateFixture();
Refuse(() => NativePackedResearchImageOwner.CaptureCardAdmission("unused",fixture),"detached fixture cannot reach source I/O");
Refuse(() => NativePackedResearchEntryComposer.ComposeCardAdmission(new byte[0x800],0x1000,fixture,(_,_) => throw new Exception("Unexpected read")),
    "detached fixture cannot issue packed entry bytes");
using (var cancel = new CancellationTokenSource())
{
    cancel.Cancel();
    try { await NativeCollectibleCardAdmissionResearchPlan.CaptureAsync("missing","missing",cancel.Token); throw new Exception("Cancellation ignored"); }
    catch (OperationCanceledException) { checks++; }
}
if (args.Length == 0)
{
    Console.WriteLine($"COLLECTIBLE_CARD_ADMISSION_RESEARCH_SOURCE_FREE_OK checks={checks} sourceReads=0 writes=0"); return;
}
if (args.Length is not (3 or 4) || args[0] is not ("--source" or "--describe" or "--publication-faults" or "--verify" or "--publish") ||
    (args[0] is "--source" or "--describe") != (args.Length == 3))
    throw new ArgumentException("Usage: no args | --source|--describe USA.bin USA.cue | --publication-faults|--verify|--publish USA.bin USA.cue directory. Publication/faults require an explicit NEW directory; no default writes.");
var plan = await NativeCollectibleCardAdmissionResearchPlan.CaptureAsync(args[1],args[2]);
var composition = plan.Composition; var reader = composition.Reader; var kernel = composition.Kernel;
var binding = NativeWizardResearchPublicationBinding.FromCardAdmission(plan);
Check(binding.SourceImagePath == plan.SourceImagePath && binding.SourceCuePath == plan.SourceCuePath && binding.JapaneseSourceImagePath == null,
    "Only original USA/CUE source ownership.");
Check(binding.PlanSha256 == plan.PlanSha256 && binding.ImageName == NativeCollectibleCardAdmissionResearchPlan.ImageName &&
    binding.CueName == NativeCollectibleCardAdmissionResearchPlan.CueName && binding.ReceiptName == NativeCollectibleCardAdmissionResearchPlan.ReceiptName &&
    binding.OutputNames.SequenceEqual(NativeCollectibleCardAdmissionResearchPlan.OutputNames),"Closed publication identity.");
Check(binding.StagePrefix == ".native-collectible-card-admission-stage-" && binding.LeaseName == ".native-collectible-card-admission.writer.lock",
    "Independent staging and lease names.");
Check(plan.ResearchOnly && plan.ResearchDiscoveryPublicationEnabled && plan.GuardedReadyAdmissionEmitted &&
    !plan.NativeInstallationApproved && !plan.ReadyStateIssued && !plan.CollectorEnabled && !plan.CardWrites &&
    !plan.LifetimeProven && !plan.WholeEngineFitProven && !plan.RuntimeVerified && !plan.NormalCreateBinApproved,
    "Emitted guarded admission is not a runtime/live-residence/save certificate.");
Refuse(plan.RequireNativeInstallation,"production installation gate stays closed");
Check(plan.GrowthBytes == 0x9800 && plan.RelocatedExecutableLba == 53894 && plan.FinalExecutableEndLba == 54098,
    "New overlay growth and physical extent.");
Check(kernel.Layout.RetainedByteLength == 0xA800 && kernel.Layout.FinalByteLength == 0xD000 && kernel.Layout.CopyAnchor == 0x80087A38,
    "Actual admission append lineage.");
byte[] original = plan.CopyOriginalExecutable(), executable = plan.CopyExecutable(), overlay = composition.CopyOverlay();
Check(plan.RevertExecutableExact(executable).SequenceEqual(original) &&
    composition.RevertOverlayExact(overlay).SequenceEqual(composition.CopyOriginalOverlay()),"Complete original EXE/overlay inverses.");
Check(composition.RevertToReaderOverlayExact(overlay).SequenceEqual(reader.CopyOverlay()) &&
    composition.RevertToReaderExecutableExact(executable).SequenceEqual(composition.Ancestry!.CopyExecutable()),"Unchanged R01 parent inverse.");
Refuse(() => plan.RevertExecutableExact(composition.Ancestry!.CopyExecutable()),"R01 parent is not new afterimage");
Refuse(() => composition.Ancestry!.RevertExecutableExact(executable),"new admission cannot impersonate R01");
Refuse(() => plan.RevertExecutableExact(executable[..^1]),"short EXE");
Refuse(() => plan.RevertExecutableExact([..executable,0]),"long EXE");
foreach (int at in new[] { 0,0x10,0x14,0x800,0x2EFC,0x2F00,0x2F4F,0x2F50,0x3030,0x3038,0x1EAFC,0x23E24,0x65DA4,0x65DA8,0x65E40,0x65E6C,0x65FFF })
{
    byte[] bad = executable.ToArray(); bad[at] ^= 1;
    Refuse(() => plan.RevertExecutableExact(bad),"EXE owner/complement mutation " + at.ToString("X"));
}
foreach (int at in new[] { 0,0x174,0x17B,0x17C,0x2060,0x2064,0x21C8,0x21CC,0x37FF,0x3800,0xA7FF,0xA800,0xC7FF,0xC800,0xC880,0xCFFF })
{
    byte[] bad = overlay.ToArray(); bad[at] ^= 1;
    Refuse(() => composition.RevertOverlayExact(bad),"overlay owner/complement mutation " + at.ToString("X"));
}
byte[] independent = original.ToArray();
void Put(int at,uint word) => BinaryPrimitives.WriteUInt32LittleEndian(independent.AsSpan(at),word);
foreach (int at in new[] { 0x3030,0x1EAFC,0x23E24 }) { Put(at,0x3C028008); Put(at+4,0x24427A38); }
Put(0x10,0x80075640); Put(0x65DA4,0xA00);
uint[] stub = [0x3C02801F,0x3442F600,0x3C03801F,0x3463F800,0xAC400000,0x24420004,0x0043082B,0x1420FFFC,0,0x08016E38,0];
for (int i = 0; i < stub.Length; i++) Put(0x65E40+i*4,stub[i]);
uint[] reset = [0x3C018007,0xAC205860,0xAC205750,0xAC205810,0xAC20587C,0xAC2056C8,0xAC205830,0xAC2058E8,
    0xAC22580C,0xAC205838,0xAC20583C,0x8C2155A4,0,0x2421F600,0x14200003,0,0x3C018020,0xA020F616,0x3C018007,0];
for (int i = 0; i < reset.Length; i++) Put(0x2F00+i*4,reset[i]);
Check(independent.SequenceEqual(executable),"Independent fixed anchor/bootstrap/reserve/reset EXE recipe and complete complement.");
byte[] copy = plan.CopyExecutable(); copy[0] ^= 1;
Check(plan.CopyExecutable().SequenceEqual(executable),"Defensive complete EXE copy.");
byte[] wad = plan.CopyWad(); int originalCursor = 0x800, finalCursor = 0x800;
foreach (var entry in plan.Entries)
{
    Check(entry.OriginalOffset == originalCursor && entry.FinalOffset == finalCursor,"Packed coverage " + entry.Index);
    Check(BinaryPrimitives.ReadInt32LittleEndian(wad.AsSpan(entry.Index*8)) == entry.FinalOffset &&
        BinaryPrimitives.ReadInt32LittleEndian(wad.AsSpan(entry.Index*8+4)) == entry.FinalLength,"Actual directory " + entry.Index);
    Check(Hash(wad.AsSpan(entry.FinalOffset,entry.FinalLength)) == entry.FinalSha256,"Complete entry hash " + entry.Index);
    if (entry.Index != 2) Check(entry.OriginalLength == entry.FinalLength && entry.OriginalSha256 == entry.FinalSha256,"Untouched entry " + entry.Index);
    originalCursor += entry.OriginalLength; finalCursor += entry.FinalLength;
}
Check(originalCursor == 0x6927000 && finalCursor == 0x6930800 && wad.Length == finalCursor,"Whole packed WAD length.");
Check(wad.AsSpan(0x5B800,0xD000).SequenceEqual(overlay),"Physical overlay is exact source-issued admission.");
plan.RequireStageReversion(); await plan.RequireFreshAsync(); checks++;
using var description = JsonDocument.Parse(plan.BuildDescription()); var desc = description.RootElement;
Check(desc.GetProperty("ExecutableSha256").GetString() == Hash(executable) && desc.GetProperty("OverlaySha256").GetString() == Hash(overlay),
    "Description binds actual composed owners.");
byte[] describedExe = original.ToArray(), describedOverlay = new byte[0xD000];
composition.CopyOriginalOverlay().CopyTo(describedOverlay,0);
void ApplySpans(JsonElement spans,byte[] source,byte[] after)
{
    int end = 0;
    foreach (var span in spans.EnumerateArray())
    {
        int offset = span.GetProperty("offset").GetInt32(), length = span.GetProperty("byteLength").GetInt32();
        byte[] bytes = Convert.FromBase64String(span.GetProperty("bytesBase64").GetString()!);
        Check(offset >= end && length == bytes.Length && offset+length <= source.Length,"Bounded disjoint patch span.");
        Check(span.GetProperty("originalSha256").GetString() == Hash(source.AsSpan(offset,length)) &&
            span.GetProperty("sha256").GetString() == Hash(bytes),"Exact patch beforeimage/afterimage hashes.");
        bytes.CopyTo(after,offset); end = offset+length;
    }
}
ApplySpans(desc.GetProperty("executablePatchSpans"),original,describedExe);
ApplySpans(desc.GetProperty("overlay").GetProperty("patchSpans"),composition.CopyOriginalOverlay(),describedOverlay);
int blockCursor = 0x3800;
foreach (var block in desc.GetProperty("overlay").GetProperty("blocks").EnumerateArray())
{
    int offset = block.GetProperty("offset").GetInt32(), length = block.GetProperty("byteLength").GetInt32();
    Check(offset == blockCursor && length > 0 && offset+length <= describedOverlay.Length,"Complete ordered append block owners.");
    if (block.GetProperty("bytesBase64").ValueKind == JsonValueKind.String)
    {
        byte[] bytes = Convert.FromBase64String(block.GetProperty("bytesBase64").GetString()!);
        Check(bytes.Length == length,"Entire code capacity described, not just used prefix."); bytes.CopyTo(describedOverlay,offset);
    }
    else Check(block.GetProperty("zeroInitialized").GetBoolean(),"Every non-code owner starts zero.");
    Check(Hash(describedOverlay.AsSpan(offset,length)) == block.GetProperty("sha256").GetString(),"Description complete block hash.");
    blockCursor += length;
}
Check(blockCursor == 0xD000 && describedOverlay.SequenceEqual(overlay) && describedExe.SequenceEqual(executable),
    "Description independently reconstructs complete EXE/overlay and all complements.");
Check(desc.GetProperty("manifest").GetProperty("sha256").GetString() == "ceb1dcaafe6e33f586996409ffd5ae400e89686c4c435e9ec3d8adde402eebfe",
    "Unchanged fixed two-identity manifest.");
if (args[0] == "--describe") { Console.WriteLine(Encoding.UTF8.GetString(plan.BuildDescription())); return; }
using var sourceImage = new FileStream(args[1],FileMode.Open,FileAccess.Read,FileShare.Read);
// Separate test recipe: derive every packed offset from the original on-disc
// directory, not the new packer's entry reports or physical ExpectedSector.
byte[] originalWad = DiscImage.ReadFileBytes(sourceImage,plan.Layout,37,0,0x6927000);
byte[] independentWad = new byte[0x6930800]; originalWad.AsSpan(0,0x800).CopyTo(independentWad);
int expectedCursor = 0x800, oldCursor = 0x800;
for (int index = 0; index < 256; index++)
{
    int offset = BinaryPrimitives.ReadInt32LittleEndian(originalWad.AsSpan(index*8));
    int length = BinaryPrimitives.ReadInt32LittleEndian(originalWad.AsSpan(index*8+4));
    if (offset == 0 && length == 0) continue;
    Check(offset == oldCursor,"Independent source directory order/coverage.");
    byte[] body = index == 2 ? describedOverlay : originalWad.AsSpan(offset,length).ToArray();
    BinaryPrimitives.WriteInt32LittleEndian(independentWad.AsSpan(index*8),expectedCursor);
    BinaryPrimitives.WriteInt32LittleEndian(independentWad.AsSpan(index*8+4),body.Length);
    body.CopyTo(independentWad,expectedCursor); oldCursor += length; expectedCursor += body.Length;
}
Check(oldCursor == originalWad.Length && expectedCursor == independentWad.Length && independentWad.SequenceEqual(wad),
    "Independent complete WAD reconstruction and every unchanged payload.");
byte[] independentRoot = DiscImage.ReadFileBytes(sourceImage,plan.Layout,plan.Layout.RootExtent,0,plan.Layout.RootLength);
int rootPatched = 0;
for (int at = 0; at < independentRoot.Length;)
{
    int length = independentRoot[at];
    if (length == 0) { at = ((at/0x800)+1)*0x800; continue; }
    string name = Encoding.ASCII.GetString(independentRoot,at+33,independentRoot[at+32]).Split(';')[0];
    if (name is "WAD.WAD" or "SCUS_942.28")
    {
        int extent = name == "WAD.WAD" ? 37 : 53894, bytes = name == "WAD.WAD" ? 0x6930800 : 0x66000;
        BinaryPrimitives.WriteInt32LittleEndian(independentRoot.AsSpan(at+2),extent);
        BinaryPrimitives.WriteInt32BigEndian(independentRoot.AsSpan(at+6),extent);
        BinaryPrimitives.WriteInt32LittleEndian(independentRoot.AsSpan(at+10),bytes);
        BinaryPrimitives.WriteInt32BigEndian(independentRoot.AsSpan(at+14),bytes); rootPatched++;
    }
    at += length;
}
Check(rootPatched == 2 && independentRoot.SequenceEqual(plan.CopyRoot()),"Independent complete ISO root and complement.");
byte[] IndependentSector(int lba,byte[] before)
{
    byte[] result = lba is >= 54079 and < 54098
        ? RawMode2Form1SectorIntegrity.PrepareOwnedZeroPaddingSector(before,plan.Layout,lba) : before.ToArray();
    if (lba is >= 37 and < 53894) independentWad.AsSpan((lba-37)*0x800,0x800).CopyTo(result.AsSpan(24));
    if (lba is >= 53894 and < 54098) independent.AsSpan((lba-53894)*0x800,0x800).CopyTo(result.AsSpan(24));
    int rootAt = (lba-plan.Layout.RootExtent)*0x800;
    if (rootAt >= 0 && rootAt < independentRoot.Length)
        independentRoot.AsSpan(rootAt,Math.Min(0x800,independentRoot.Length-rootAt)).CopyTo(result.AsSpan(24));
    if (lba is 53874 or 54078 or 53893 or 54097)
        result[18] = result[22] = (byte)((result[18]&~0x81)|(lba is 53893 or 54097 ? 0x81 : 0));
    return result.AsSpan().SequenceEqual(before) ? result : RawMode2Form1SectorIntegrity.RebuildRawSectorCopy(result,lba);
}
sourceImage.Position = 0;
using var originalHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
using var imageHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
int changed = 0; byte[] sector = new byte[2352];
for (int lba = 0; lba < sourceImage.Length/2352; lba++)
{
    sourceImage.ReadExactly(sector); originalHash.AppendData(sector); byte[] expected = plan.ExpectedSector(lba,sector); imageHash.AppendData(expected);
    if (!expected.AsSpan().SequenceEqual(IndependentSector(lba,sector))) throw new Exception("Independent whole-sector recipe differs at " + lba);
    if (!expected.AsSpan().SequenceEqual(sector)) changed++;
    if ((lba < 37 || lba >= 54098) && lba != plan.Layout.RootExtent && !expected.AsSpan().SequenceEqual(sector))
        throw new Exception("Physical complement changed at " + lba);
    if (lba is 53874 or 53893 or 54078 or 54079 or 54097)
    {
        RawMode2Form1SectorIntegrity.RequireRawSectorIntegrity(expected,lba);
        Check(expected[18] == expected[22] && (expected[18]&0x81) == (lba is 53893 or 54097 ? 0x81 : 0),"Moved EOF/EOR and regenerated integrity " + lba);
    }
}
string expectedImage = Convert.ToHexStringLower(imageHash.GetHashAndReset());
Check(Convert.ToHexStringLower(originalHash.GetHashAndReset()) == NativeMobySourceInventory.OriginalImageSha256,"Whole original remains exact.");
Check(sourceImage.Position == sourceImage.Length,"Independent every-sector comparison completed including outer complement.");
using var receipt = JsonDocument.Parse(plan.BuildReceipt((expectedImage,changed)));
Check(receipt.RootElement.GetProperty("profile").GetString() == NativeCollectibleCardAdmissionResearchPlan.Profile &&
    receipt.RootElement.GetProperty("GuardedReadyAdmissionEmitted").GetBoolean() &&
    !receipt.RootElement.GetProperty("ReadyStateIssued").GetBoolean() && !receipt.RootElement.GetProperty("RuntimeVerified").GetBoolean(),
    "Receipt distinguishes emitted code from actual runtime admission.");
if (args[0] == "--publication-faults") await PublicationFaults.Run(plan,args[3],expectedImage);
if (args[0] == "--publish") Console.WriteLine("COLLECTIBLE_CARD_ADMISSION_RESEARCH_PUBLICATION " + JsonSerializer.Serialize(
    await NativeCollectibleCardAdmissionResearchPublisher.PublishAsync(plan,args[3])));
if (args[0] == "--verify") Console.WriteLine("COLLECTIBLE_CARD_ADMISSION_RESEARCH_VERIFIED " + JsonSerializer.Serialize(
    await NativeCollectibleCardAdmissionResearchPublisher.VerifyAsync(plan,args[3])));
Console.WriteLine($"COLLECTIBLE_CARD_ADMISSION_RESEARCH_SOURCE_OK checks={checks} plan={plan.PlanSha256} exe={plan.ExecutableSha256} overlay={plan.OverlaySha256} wad={plan.WadSha256} root={plan.RootSha256} expectedImage={expectedImage} changed={changed} publishedImages={(args[0]=="--publish"?1:0)} runtime=false");
