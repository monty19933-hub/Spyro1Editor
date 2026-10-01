using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Universal;

int checks = 0;
void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); checks++; }
const uint read = UniversalNativeJumpModelOwnership.ClassRead, tail = UniversalNativeJumpModelOwnership.ModelTail;
uint[] producer = [0x86620036, 0x3C098007, 0x25296378, 0x080210FF, 0xA2600049];
uint[] consumer = [0xA2630048, 0xA2600040, 0x00021080, 0x00491021, 0x8C420000, 0];
var witnesses = Enumerable.Range(0, producer.Length).Select(n => read + (uint)n * 4)
    .Concat(Enumerable.Range(0, consumer.Length).Select(n => tail + (uint)n * 4)).ToArray();
UniversalOverlay Image()
{
    byte[] bytes = new byte[(int)(tail + 24 - UniversalOverlay.Base)];
    for (int n=0; n<producer.Length; n++) BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan((int)(read - UniversalOverlay.Base) + n*4), producer[n]);
    for (int n=0; n<consumer.Length; n++) BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan((int)(tail - UniversalOverlay.Base) + n*4), consumer[n]);
    var bindings = new Dictionary<uint, UniversalExeBinding>();
    foreach (uint global in new[]{UniversalExeBindings.InitGlobal, UniversalExeBindings.UpdateGlobal, UniversalExeBindings.CopyBufferGlobal})
        bindings[global] = new(global, 0, 0, read);
    return new(bytes, bindings);
}
UniversalAsmPart Part(UniversalOverlay image)
{
    var p = new UniversalAsmPart();
    foreach(uint pc in witnesses) p.Emit(image.Word(pc), pc == read+12 ? new(UniversalFixKind.Jump,tail) : default, orig:pc);
    return p;
}
void PacketCases(UniversalOverlay image, Func<UniversalAsmPart> make, string scope)
{
    var original = make(); var words = original.Items.Select(i=>i.Word).ToArray();
    Check(UniversalNativeJumpModelOwnership.MarkOwned(original,image),scope+" exact packets are owned");
    Check(original.Items.Where(i=>(i.Orig??i.SourcePc)==read).All(i=>i.ClassReadOwned),scope+" every class-read copy is owned");
    Check(original.Items.Select(i=>i.Word).SequenceEqual(words),scope+" ownership adds no instructions or changes");
    foreach(uint pc in witnesses)
    {
        var p=make(); int at=p.Items.FindIndex(i=>(i.Orig??i.SourcePc)==pc);
        p.Items[at]=p.Items[at] with {Word=p.Items[at].Word ^ 1};
        Check(!UniversalNativeJumpModelOwnership.MarkOwned(p,image),scope+" modified copied witness refused "+pc.ToString("X8"));
        var bytes=image.Bytes.ToArray(); BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan((int)(pc-UniversalOverlay.Base)),image.Word(pc)^1);
        Check(!UniversalNativeJumpModelOwnership.MarkOwned(make(),new(bytes,image.Bindings)),scope+" modified source witness refused "+pc.ToString("X8"));
    }
    var missing=make(); missing.Items.RemoveAt(missing.Items.FindIndex(i=>(i.Orig??i.SourcePc)==tail));
    Check(!UniversalNativeJumpModelOwnership.MarkOwned(missing,image),scope+" absent copied model entry refused");
    var target=make(); int j=target.Items.FindIndex(i=>(i.Orig??i.SourcePc)==read+12);
    target.Items[j]=target.Items[j] with {Fix=new(UniversalFixKind.Jump,tail+4)};
    Check(!UniversalNativeJumpModelOwnership.MarkOwned(target,image),scope+" changed relocated target refused");
    var inserted=make(); inserted.Items.Insert(inserted.Items.FindIndex(i=>(i.Orig??i.SourcePc)==read+4),new(0x00402021,default,null,null));
    Check(!UniversalNativeJumpModelOwnership.MarkOwned(inserted,image),scope+" extra unowned consumer refused");
    var control=make();control.Items.Insert(control.Items.FindIndex(i=>(i.Orig??i.SourcePc)==read),new(UniversalMips.EncodeJ(2,tail),default,null,null));
    Check(!UniversalNativeJumpModelOwnership.MarkOwned(control,image),scope+" copied class read in a new branch delay refused");
    var duplicate=make(); var extra=Part(image).Items.Select(i=>i with {Orig=null,SourcePc=i.Orig}).ToArray();
    duplicate.Items.AddRange(extra);
    Check(UniversalNativeJumpModelOwnership.MarkOwned(duplicate,image),scope+" complete identical packet copy allowed");
    duplicate=make(); duplicate.Items.AddRange(extra);duplicate.Items[^1]=duplicate.Items[^1] with {Word=0x00402021};
    Check(!UniversalNativeJumpModelOwnership.MarkOwned(duplicate,image),scope+" correct packet cannot bless modified duplicate");
    var fix=make();int f=fix.Items.FindIndex(i=>(i.Orig??i.SourcePc)==read+4);
    fix.Items[f]=fix.Items[f] with {Fix=new(UniversalFixKind.AddrHi,tail)};
    Check(!UniversalNativeJumpModelOwnership.MarkOwned(fix,image),scope+" relocated literal-table base refused");
    var delay=image.Bytes.ToArray();BinaryPrimitives.WriteUInt32LittleEndian(delay.AsSpan((int)(read-4-UniversalOverlay.Base)),UniversalMips.EncodeJ(2,tail));
    Check(!UniversalNativeJumpModelOwnership.MarkOwned(make(),new(delay,image.Bindings)),scope+" class read in control delay slot refused");
}
PacketCases(Image(),()=>Part(Image()),"synthetic");
// Independent scalar executor includes R3000 load and branch delay slots. Verify every
// destination class reaches its own model slot even when v0 initially holds another index.
void Scalar(Func<uint,uint> word, uint start=read, uint model=tail)
{
    for(uint cls=0;cls<512;cls++)
    {
        uint[] r=new uint[32];r[19]=0x80110000;r[2]=0xDEAD;r[3]=1;
        uint pc=start;uint? pendingLoad=null,pendingJump=null;int pendingRegister=0;int budget=32;
        var stores=new Dictionary<uint,uint>();uint requested=0,root=0x80130000+cls*32;
        while(pc!=model+24&&budget-->0)
        {
            uint w=word(pc),op=w>>26;int rs=(int)(w>>21)&31,rt=(int)(w>>16)&31,rd=(int)(w>>11)&31;
            int imm=(short)w;uint value=0;bool write=false,load=false;uint? nextJump=null;
            switch(op)
            {
                case 33:Check(r[rs]+(uint)imm==0x80110036,"scalar actor class field");value=cls;load=true;break;
                case 15:value=(w&65535)<<16;write=true;break;
                case 9:value=unchecked(r[rs]+(uint)imm);write=true;break;
                case 2:nextJump=((pc+4)&0xF0000000)|((w&0x3FFFFFF)<<2);break;
                case 40:stores[unchecked(r[rs]+(uint)imm)]=r[rt]&255;break;
                case 0:if(w!=0){value=(w&63)==0?r[rt]<<((int)(w>>6)&31):unchecked(r[rs]+r[rt]);rt=rd;write=true;}break;
                case 35:requested=unchecked(r[rs]+(uint)imm);value=root;load=true;break;
                default:throw new InvalidOperationException("Unexpected scalar instruction");
            }
            if(pendingLoad is uint prior)r[pendingRegister]=prior;
            pendingLoad=null;if(load){pendingLoad=value;pendingRegister=rt;}else if(write&&rt!=0)r[rt]=value;
            uint next=pendingJump??pc+4;pendingJump=nextJump;pc=next;
        }
        Check(budget>0&&requested==0x80076378+cls*4&&r[2]==root,"scalar exact destination model root "+cls);
        Check(stores.Count==3&&stores[0x80110049]==0&&stores[0x80110048]==1&&stores[0x80110040]==0,"scalar native flag writes "+cls);
    }
}
Scalar(Image().Word);
Console.WriteLine($"UNIVERSAL_JUMP_MODEL_FREE_OK checks={checks}");
if(args.Length==0)return;
if(args.Length<3||args[0]!="--source")throw new ArgumentException("--source <USA.bin> <workspace> [baseline.json.gz output.json.gz]");
var catalog=LevelCatalog.Load(args[2]);var library=new UniversalSourceLibrary(args[1],catalog);var source=library.Get("gnorccove");
Func<UniversalAsmPart> native=()=>UniversalBehaviorBundleBuilder.BuildPrunedUpdate(source.Overlay,[0x09A,0x0CA,0x0CF],"jump_model",
    new Dictionary<int,int>{{0x09A,0x1FD},{0x0CA,0x0CA},{0x0CF,0x0CF}}).Part;
PacketCases(source.Overlay,native,"native pruned");Scalar(source.Overlay.Word);
var integration=native();UniversalClassRemapping.PatchOwnedClassReads(integration,source.Overlay,new Dictionary<int,int>{{0x09A,0x1FD}},[0x09A,0x0CA,0x0CF]);
Check(integration.Items.Where(i=>(i.Orig??i.SourcePc)==read).All(i=>i.ClassReadOwned),"ordinary remapper invokes jump-model ownership");
var nativePart=native();Check(UniversalNativeJumpModelOwnership.MarkOwned(nativePart,source.Overlay),"native assembly retains its authenticated owner");
const uint assembledBase=0x80150000;
var assembled=UniversalBehaviorBundleBuilder.Assemble([nativePart],assembledBase).Code;
var addresses=new Dictionary<uint,uint>();uint assembledPc=assembledBase;
foreach(var item in nativePart.Items){if(item.Word==null)continue;if(item.Orig is uint origin)addresses[origin]=assembledPc;assembledPc+=4;}
uint assembledRead=addresses[read],assembledTail=addresses[tail];
Check(UniversalMips.BranchTarget(BinaryPrimitives.ReadUInt32LittleEndian(assembled.AsSpan((int)(addresses[read+12]-assembledBase))),addresses[read+12])==assembledTail,
    "actual assembled jump selects the relocated model tail");
Scalar(pc=>BinaryPrimitives.ReadUInt32LittleEndian(assembled.AsSpan((int)(pc-assembledBase))),assembledRead,assembledTail);
var isolated=Part(source.Overlay);UniversalClassRemapping.PatchOwnedClassReads(isolated,source.Overlay,new Dictionary<int,int>{{0x09A,0x1FD}},[0x09A]);
UniversalClassRemapping.RequireNoUnownedClassMemory(isolated,false,source.Overlay);Check(true,"final remap guard accepts the complete exact owner");
var corrupt=Part(source.Overlay);corrupt.Items[^2]=corrupt.Items[^2] with{Word=0xAC420000};
UniversalClassRemapping.PatchOwnedClassReads(corrupt,source.Overlay,new Dictionary<int,int>{{0x09A,0x1FD}},[0x09A]);
bool refused=false;try{UniversalClassRemapping.RequireNoUnownedClassMemory(corrupt,false,source.Overlay);}catch(InvalidDataException){refused=true;}
Check(refused,"ordinary final remap guard refuses a copied class-derived store");
string hash;using(var original=File.OpenRead(args[1]))hash=Convert.ToHexStringLower(SHA256.HashData(original));
Check(hash==UniversalSourceLibrary.OriginalUsaSha256,"authenticated original USA image remains intact");
Console.WriteLine($"UNIVERSAL_JUMP_MODEL_SOURCE_OK checks={checks} sourceSha256={hash}");
if(args.Length!=5)return;
if(File.Exists(args[4]))throw new IOException("Refuse to replace existing delta report.");
using var input=File.OpenRead(args[3]);using var zipped=new GZipStream(input,CompressionMode.Decompress);
var baseline=JsonSerializer.Deserialize<UniversalCoverageReport>(zipped)!;var variants=baseline.Variants.ToDictionary(v=>v.VariantId);
var cells=new List<UniversalCoverageCell>();var protectedVram=new Dictionary<string,IReadOnlyList<(long Offset,int Length)>>();
foreach(var old in baseline.Cells.Where(c=>c.FailureKind=="class-remap-ownership"&&c.Detail=="Source class remapping needs ownership for class-field access at 0x8007EE0C."))
{
    var v=variants[old.VariantId];Check(v.SourceLevelKey=="gnorccove"&&v.ClassId==0x09A,"focused exact refusal family");
    var dest=library.Get(old.Destination);var row=source.Data.Row(v.SourceRow);
    if(!protectedVram.TryGetValue(old.Destination,out var ranges))protectedVram[old.Destination]=ranges=UniversalMobyExport.DefaultProtectedVram(args[1],dest.Level).ToArray();
    var placement=new UniversalMobyPlacement(v.SourceLevelKey,v.SourceRow,BitConverter.ToInt32(row,0x0C),BitConverter.ToInt32(row,0x10),BitConverter.ToInt32(row,0x14));
    try
    {
        var result=UniversalMobyComposer.Compose(dest.Level,dest.Data.Bytes,dest.Overlay.Bytes.ToArray(),library.Disc.Exe,library.Get,library.SharedClasses,ranges,[placement],sharedModelPackage:library.SharedModelPackage,playerAnimationSource:library.PlayerAnimations);
        cells.Add(new(old.Destination,v.VariantId,"listed",result.ParticleDependencies.Any(p=>p.Status=="dependency-unverified")||result.UnresolvedObjectLinks>0?"composed-dependency-unverified":"composed-static-only","not-tested",null,null,result.Classes,result.Warnings,result.Ram)
            {ParticleDependencies=result.ParticleDependencies,Behavior=result.Behavior,RestoredObjectLinks=result.RestoredObjectLinks,UnresolvedObjectLinks=result.UnresolvedObjectLinks});
    }
    catch(Exception e) when(e is InvalidDataException or IOException or InvalidOperationException or ArgumentException)
    {cells.Add(new(old.Destination,v.VariantId,"listed","failed","not-tested",UniversalMobyCoverage.FailureKind(e),e.Message,[],[],e is UniversalBudgetException b?b.Budget:null));}
    if(cells.Count%50==0)Console.WriteLine($"JUMP_MODEL_DELTA_PROGRESS cells={cells.Count}");
}
var report=baseline with{Cells=cells,ImplementationModuleVersionId=typeof(UniversalMobyComposer).Module.ModuleVersionId.ToString(),
    SourceCodeFingerprint=null,SourceCodeFingerprintScope="Isolated build recorded by Core module SHA256 in console output.",
    RuntimeEvidenceScope="Exact recomposition of prior 8007EE0C ownership refusals only; gameplay not tested."};
using(var output=File.Create(args[4]))using(var compressed=new GZipStream(output,CompressionLevel.SmallestSize))JsonSerializer.Serialize(compressed,report);
Console.WriteLine(JsonSerializer.Serialize(new{Scope="Gnorc Cove jump model affected cells only",Cells=cells.Count,Composed=cells.Count(c=>c.DependencyExportStatus.StartsWith("composed")),
    Outcomes=cells.GroupBy(c=>c.Detail??c.DependencyExportStatus).ToDictionary(g=>g.Key,g=>g.Count()),Runtime="not-tested",
    CoreModuleSha256=Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(typeof(UniversalMobyComposer).Assembly.Location)))}));
