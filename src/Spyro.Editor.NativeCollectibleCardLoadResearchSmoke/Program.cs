using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

int checks = 0;
void Check(bool ok,string why) { if (!ok) throw new Exception(why); checks++; }
void Refuse(Action action,string why)
{
    try { action(); }
    catch (Exception e) when (e is ArgumentException or InvalidDataException or IOException) { checks++; return; }
    throw new Exception("Expected refusal: " + why);
}
static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
Check(typeof(NativeCollectibleCardLoadResearchPlan).IsSealed && typeof(NativeCollectibleCardLoadResearchPlan).GetConstructors().Length == 0,
    "Research profile cannot accept caller payloads.");
Check(typeof(NativeCollectibleCardLoadResearchPlan).GetMethod("CaptureAsync")!.GetParameters().Select(p => p.Name)
    .SequenceEqual(new[] { "originalImagePath", "originalCuePath", "token" }), "Source-only factory, no manifest/slot/payload authority.");
Check(NativeCollectibleCardLoadResearchPlan.Profile == "native-collectible-card-load-research-v1" &&
    NativeCollectibleCardLoadResearchPlan.ImageName == "collectible-card-load-research-r01.bin" &&
    NativeCollectibleCardLoadResearchPlan.CueName == "collectible-card-load-research-r01.cue", "Fixed research identity.");
Check(!NativeCollectibleCardLoadResearchPlan.OutputNames.Intersect(new[] { NativeCollectibleStartupResearchPlan.ImageName,
    NativeCollectibleStartupResearchPlan.CueName, NativeCollectibleStartupResearchPlan.ReceiptName }).Any(), "Startup R06 names protected.");
Check(typeof(NativeWizardResearchPublicationBinding).GetMethods(BindingFlags.Static|BindingFlags.NonPublic)
    .Where(m => m.Name.StartsWith("From",StringComparison.Ordinal)).All(m => m.GetParameters().Length == 1 &&
        m.GetParameters()[0].ParameterType.IsSealed && !typeof(Delegate).IsAssignableFrom(m.GetParameters()[0].ParameterType)),
    "Bindings still accept only sealed source plans.");
Refuse(() => NativeCollectibleCardLoadResearchPlan.CaptureAsync("","missing.cue").GetAwaiter().GetResult(), "empty source");
Refuse(() => NativeCollectibleCardLoadResearchPublisher.PublishAsync(null!,"unused").GetAwaiter().GetResult(), "null publisher authority");
Refuse(() => NativeCollectibleCardLoadResearchPublisher.VerifyAsync(null!,"unused").GetAwaiter().GetResult(), "null verifier authority");
Refuse(() => NativeWizardResearchPublicationBinding.FromCardLoad(null!), "null binding");
Refuse(() => NativeCollectibleCardLoadResearchComposition.Create(null!,null!), "null composition");
Refuse(() => NativePackedResearchImageOwner.CaptureCardLoad("unused",null!), "null physical ancestry");
Refuse(() => NativeWizardResearchPackedEntryComposer.ComposeCardLoad(new byte[0x800],0x1000,null!,(_,_) => throw new Exception("Unexpected I/O")),
    "null typed card packer");
using (var cancel = new CancellationTokenSource())
{
    cancel.Cancel();
    try { await NativeCollectibleCardLoadResearchPlan.CaptureAsync("missing","missing",cancel.Token); throw new Exception("Cancellation ignored."); }
    catch (OperationCanceledException) { checks++; }
}
if (args.Length == 0)
{
    Console.WriteLine($"COLLECTIBLE_CARD_LOAD_RESEARCH_SOURCE_FREE_OK checks={checks} sourceReads=0 writes=0"); return;
}
if (args.Length is not (3 or 4) || args[0] is not ("--source" or "--describe" or "--publication-faults" or "--verify" or "--publish") ||
    (args[0] is "--source" or "--describe") != (args.Length == 3))
    throw new ArgumentException("Usage: no args | --source|--describe USA.bin USA.cue | --publication-faults|--verify|--publish USA.bin USA.cue directory. Publication/faults require an explicit NEW directory; no default writes.");
var plan = await NativeCollectibleCardLoadResearchPlan.CaptureAsync(args[1],args[2]);
var composition = plan.Composition; var card = composition.Card; var bootstrap = composition.Bootstrap;
var binding = NativeWizardResearchPublicationBinding.FromCardLoad(plan);
Check(binding.SourceImagePath == plan.SourceImagePath && binding.SourceCuePath == plan.SourceCuePath && binding.JapaneseSourceImagePath == null,
    "Binding preserves original USA/CUE only.");
Check(binding.PlanSha256 == plan.PlanSha256 && binding.ImageName == NativeCollectibleCardLoadResearchPlan.ImageName &&
    binding.CueName == NativeCollectibleCardLoadResearchPlan.CueName && binding.ReceiptName == NativeCollectibleCardLoadResearchPlan.ReceiptName &&
    binding.OutputNames.SequenceEqual(NativeCollectibleCardLoadResearchPlan.OutputNames), "Closed role names and identity.");
Check(binding.StagePrefix == ".native-collectible-card-load-stage-" && binding.LeaseName == ".native-collectible-card-load.writer.lock",
    "Independent stage and lease owners.");
Check(plan.ResearchOnly && plan.ResearchDiscoveryPublicationEnabled && !plan.NativeInstallationApproved && !plan.ReadyStateIssued &&
    !plan.CollectorEnabled && !plan.CardWrites && !plan.LifetimeProven && !plan.WholeEngineFitProven && !plan.RuntimeVerified && !plan.NormalCreateBinApproved,
    "Research publication is not gameplay or live RAM/card certification.");
Refuse(plan.RequireNativeInstallation,"detached installation gate remains closed");
Check(plan.GrowthBytes == 0x7000 && plan.RelocatedExecutableLba == 53889 && plan.FinalExecutableEndLba == 54093,
    "Card entry growth relocates following resources and EXE by fourteen sectors.");
byte[] original = plan.CopyOriginalExecutable(), executable = plan.CopyExecutable(), overlay = card.CopyOverlay();
Check(plan.RevertExecutableExact(executable).SequenceEqual(original), "Exact combined inverse reaches complete original EXE.");
Refuse(() => card.RevertExecutableExact(executable), "composed bytes cannot impersonate card-only afterimage");
Refuse(() => bootstrap.RevertExact(executable), "composed bytes cannot impersonate startup-only afterimage");
Refuse(() => plan.RevertExecutableExact(card.CopyExecutable()), "card-only is not a combined afterimage");
Refuse(() => plan.RevertExecutableExact(bootstrap.CopyExecutable()), "startup-only is not a combined afterimage");
Refuse(() => plan.RevertExecutableExact(executable[..^1]), "short afterimage");
Refuse(() => plan.RevertExecutableExact([..executable,0]), "long afterimage");
foreach (int at in new[] { 0,0x10,0x14,0x800,0x3030,0x3034,0x3038,0x1EAFC,0x1EB00,0x23E24,0x23E28,0x65DA4,0x65DA8,0x65E40,0x65E6C,0x65FFF })
{
    byte[] bad = executable.ToArray(); bad[at] ^= 1;
    Refuse(() => plan.RevertExecutableExact(bad), "combined owner/complement mutation at " + at.ToString("X"));
}
foreach (int at in new[] { 0,0x2060,0x2064,0x21C8,0x21CC,0x37FF,0x3800,0xA7FF })
{
    byte[] bad = overlay.ToArray(); bad[at] ^= 1;
    Refuse(() => card.RevertExact(bad), "overlay owner/complement mutation at " + at.ToString("X"));
}
byte[] independent = original.ToArray();
foreach (int at in new[] { 0x3030,0x1EAFC,0x23E24 })
{
    BinaryPrimitives.WriteUInt32LittleEndian(independent.AsSpan(at),0x3C028008);
    BinaryPrimitives.WriteUInt32LittleEndian(independent.AsSpan(at+4),0x24425238);
}
BinaryPrimitives.WriteUInt32LittleEndian(independent.AsSpan(0x10),0x80075640);
BinaryPrimitives.WriteUInt32LittleEndian(independent.AsSpan(0x65DA4),0xA00);
uint[] stub = [0x3C02801F,0x3442F600,0x3C03801F,0x3463F800,0xAC400000,0x24420004,0x0043082B,0x1420FFFC,0,0x08016E38,0];
for (int i = 0; i < stub.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(independent.AsSpan(0x65E40+i*4),stub[i]);
Check(independent.SequenceEqual(executable), "Independent full EXE anchor/bootstrap/reserve recipe and complete complement.");
Check(executable.AsSpan(0x65E6C,0x194).IndexOfAnyExcept((byte)0) < 0, "Complete unused startup tail stays zero.");
byte[] copy = plan.CopyExecutable(); copy[0] ^= 1;
Check(plan.CopyExecutable().SequenceEqual(executable), "Defensive complete executable copy.");
using (var source = NativeRegionalSourceSession.Open(args[1],NativeDiscRevision.UsaScus94228))
{
    var manifest = NativeCollectibleCardLoadResearchComposition.FixedManifest();
    Refuse(() => NativeCollectibleCardLoadResearchComposition.Create(NativeCollectibleCardLoadPlan.CreateFixture(manifest,0),bootstrap),
        "source-looking fixture cannot become research ancestry");
    var wrongSlot = NativeCollectibleCardLoadPlan.Capture(source,manifest,1);
    Refuse(() => NativeCollectibleCardLoadResearchComposition.Create(wrongSlot,bootstrap), "slot one cannot enter fixed slot-zero profile");
    var wrongManifest = DetachedCollectibleManifest.CreateNew(Guid.Parse("33333333-3333-3333-3333-333333333333"),
        [Guid.Parse("11111111-1111-1111-1111-111111111111"),Guid.Parse("22222222-2222-2222-2222-222222222222")]);
    var other = NativeCollectibleCardLoadPlan.Capture(source,wrongManifest,0);
    Refuse(() => NativeCollectibleCardLoadResearchComposition.Create(other,bootstrap), "another manifest cannot enter closed composition");
    var wrongBootstrap = NativeCollectibleColdBootBootstrap.Create(NativeCollectibleReservationMechanics.Create(
        NativeCollectibleSelectedSlotResidence.Capture(source,wrongManifest,0)));
    Refuse(() => NativeCollectibleCardLoadResearchComposition.Create(card,wrongBootstrap), "bootstrap descriptor must own fixed manifest");
    source.VerifyUnchanged();
}
byte[] wad = plan.CopyWad();
int originalCursor = 0x800, finalCursor = 0x800;
foreach (var entry in plan.Entries)
{
    Check(entry.OriginalOffset == originalCursor && entry.FinalOffset == finalCursor,"Packed directory coverage " + entry.Index);
    Check(BinaryPrimitives.ReadInt32LittleEndian(wad.AsSpan(entry.Index*8)) == entry.FinalOffset &&
        BinaryPrimitives.ReadInt32LittleEndian(wad.AsSpan(entry.Index*8+4)) == entry.FinalLength,"Actual directory pair " + entry.Index);
    Check(Hash(wad.AsSpan(entry.FinalOffset,entry.FinalLength)) == entry.FinalSha256,"Complete payload hash " + entry.Index);
    if (entry.Index != 2) Check(entry.OriginalLength == entry.FinalLength && entry.OriginalSha256 == entry.FinalSha256,
        "Unrelated payload exact " + entry.Index);
    originalCursor += entry.OriginalLength; finalCursor += entry.FinalLength;
}
Check(originalCursor == 0x6927000 && finalCursor == 0x692E000 && wad.Length == finalCursor,"Complete original/final WAD coverage.");
Check(wad.AsSpan(0x5B800,0xA800).SequenceEqual(overlay), "Actual packed overlay is source-issued card afterimage.");
plan.RequireStageReversion(); await plan.RequireFreshAsync(); checks++;
using var description = JsonDocument.Parse(plan.BuildDescription());
Check(description.RootElement.GetProperty("ExecutableSha256").GetString() == Hash(executable),"Description binds whole composed EXE.");
Check(description.RootElement.GetProperty("manifest").GetProperty("sha256").GetString() ==
    "ceb1dcaafe6e33f586996409ffd5ae400e89686c4c435e9ec3d8adde402eebfe" &&
    description.RootElement.GetProperty("referenceFixture").GetProperty("envelopeSha256").GetString() ==
    "5d6ab1292c7b0b68634130df93aec36b4257af3cfe2369059ab6423b464c1da5",
    "Independent Python canonical manifest and 01/00/00 envelope fixture goldens.");
foreach (var block in description.RootElement.GetProperty("overlay").GetProperty("blocks").EnumerateArray())
{
    int offset = block.GetProperty("offset").GetInt32(), length = block.GetProperty("byteLength").GetInt32();
    Check(block.GetProperty("sha256").GetString() == Hash(overlay.AsSpan(offset,length)),"Source recipe block hash.");
    if (block.GetProperty("bytesBase64").ValueKind == JsonValueKind.String)
        Check(Convert.FromBase64String(block.GetProperty("bytesBase64").GetString()!).AsSpan().SequenceEqual(overlay.AsSpan(offset,length)),
            "Recipe code bytes reproduce exact complete generated block.");
    else Check(block.GetProperty("zeroInitialized").GetBoolean() && overlay.AsSpan(offset,length).IndexOfAnyExcept((byte)0) < 0,
        "Every non-code append owner starts zero.");
}
if (args[0] == "--describe")
{
    Console.WriteLine(Encoding.UTF8.GetString(plan.BuildDescription())); return;
}
using var sourceImage = new FileStream(args[1],FileMode.Open,FileAccess.Read,FileShare.Read);
using var originalHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
using var imageHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
int changed = 0; byte[] sector = new byte[2352];
for (int lba = 0; lba < sourceImage.Length/2352; lba++)
{
    sourceImage.ReadExactly(sector); originalHash.AppendData(sector); byte[] expected = plan.ExpectedSector(lba,sector); imageHash.AppendData(expected);
    if (!expected.AsSpan().SequenceEqual(sector)) changed++;
    if ((lba < 37 || lba >= 54093) && lba != plan.Layout.RootExtent)
        if (!expected.AsSpan().SequenceEqual(sector)) throw new Exception("Physical complement changed at " + lba);
    if (lba is 53874 or 53888 or 54078 or 54079 or 54092)
    {
        RawMode2Form1SectorIntegrity.RequireRawSectorIntegrity(expected,lba);
        Check(expected[18] == expected[22] && (expected[18]&0x81) == (lba is 53888 or 54092 ? 0x81 : 0),
            "EOF/EOR and complete regenerated EDC/ECC " + lba);
    }
}
string expectedImage = Convert.ToHexStringLower(imageHash.GetHashAndReset());
Check(Convert.ToHexStringLower(originalHash.GetHashAndReset()) == NativeMobySourceInventory.OriginalImageSha256,"Whole original image remains exact.");
Check(expectedImage == "ef36363011f74a1f2bdb56f1e709d5654f8de0bbec497c95c16e3debf8e355e7" && changed == 53526,
    "Independent original-only Python physical reconstruction golden, frozen before candidate publication.");
Check(plan.ExecutableSha256 == "4f904e0c6db385ea1e8d82fb0565cf5b0cc5d413b03cf74e050a8766e9349e49" &&
    plan.WadSha256 == "13d8b2e3d1c25fa8a4cfd445d25bafd8f54b21bcf261177a703a4d52ae744041" &&
    plan.RootSha256 == "681c96eef72811d2bdae8c9e2c0cce548ccefcc6bd9afa4d52f9a0788d800c49",
    "Independently reconstructed whole EXE, packed WAD and ISO root goldens.");
using var receipt = JsonDocument.Parse(plan.BuildReceipt((expectedImage,changed)));
Check(receipt.RootElement.GetProperty("profile").GetString() == NativeCollectibleCardLoadResearchPlan.Profile &&
    !receipt.RootElement.GetProperty("CardWrites").GetBoolean() && !receipt.RootElement.GetProperty("ReadyStateIssued").GetBoolean(),
    "Receipt cannot imply live Ready or card write authority.");
if (args[0] == "--publication-faults") await PublicationFaults.Run(plan,args[3],expectedImage);
if (args[0] == "--publish") Console.WriteLine("COLLECTIBLE_CARD_LOAD_RESEARCH_PUBLICATION " + JsonSerializer.Serialize(
    await NativeCollectibleCardLoadResearchPublisher.PublishAsync(plan,args[3])));
if (args[0] == "--verify") Console.WriteLine("COLLECTIBLE_CARD_LOAD_RESEARCH_VERIFIED " + JsonSerializer.Serialize(
    await NativeCollectibleCardLoadResearchPublisher.VerifyAsync(plan,args[3])));
Console.WriteLine($"COLLECTIBLE_CARD_LOAD_RESEARCH_SOURCE_OK checks={checks} plan={plan.PlanSha256} exe={plan.ExecutableSha256} wad={plan.WadSha256} expectedImage={expectedImage} changed={changed} publishedImages={(args[0]=="--publish"?1:0)} runtime=false");
