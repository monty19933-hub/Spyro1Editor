using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Universal;

int checks = 0;
void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); checks++; }
uint I(int op,int rs,int rt,int imm)=>UniversalMips.EncodeI(op,rs,rt,imm);
uint R(int rs,int rt,int rd,int shift,int fn)=>(uint)((rs<<21)|(rt<<16)|(rd<<11)|(shift<<6)|fn);
UniversalOverlay Overlay(uint[] words)
{
    var bytes=new byte[0x400];for(int n=0;n<words.Length;n++)BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x100+4*n),words[n]);
    var bindings=new Dictionary<uint,UniversalExeBinding>();
    foreach(uint global in new[]{UniversalExeBindings.InitGlobal,UniversalExeBindings.UpdateGlobal,UniversalExeBindings.CopyBufferGlobal})
        bindings[global]=new(global,0,0,UniversalOverlay.Base+0x100);
    return new(bytes,bindings);
}
bool Proved(uint[] words)=>UniversalClassRemapping.FeedsModelTable(Overlay(words),UniversalOverlay.Base+0x100);
uint[] materialized=[I(33,4,2,0x36),I(15,0,9,0x8007),I(9,9,9,0x6378),R(0,2,2,2,0),R(2,9,2,0,33),I(35,2,2,0),0,UniversalMips.JrRa,0];
uint[] legacy=[I(37,4,2,0x36),I(15,0,9,0x8007),R(0,2,2,2,0),R(2,9,2,0,33),I(35,2,2,0x6378),0,UniversalMips.JrRa,0];
Check(Proved(materialized),"literal ADDIU model-table base is owned");
var delayOverlay=Overlay(materialized);var delayBytes=delayOverlay.Bytes.ToArray();
BinaryPrimitives.WriteUInt32LittleEndian(delayBytes.AsSpan(0xFC),I(4,3,0,0x10));
Check(!UniversalClassRemapping.FeedsModelTable(new(delayBytes,delayOverlay.Bindings),UniversalOverlay.Base+0x100),
    "a control-transfer delay slot does not inherit fall-through-only model ownership");
Check(Proved(legacy),"existing displacement-form model-table proof remains accepted");
var baseResult=legacy.ToArray();baseResult[3]=R(9,2,9,0,33);baseResult[4]=I(35,9,2,0x6378);
Check(Proved(baseResult),"native temporary base can hold the indexed table address");
var pointerStore=baseResult.Take(4).Concat(new[]{I(43,29,9,0)}).Concat(baseResult.Skip(4)).ToArray();
Check(!Proved(pointerStore),"indexed base pointer cannot escape into memory before the model lookup");
uint[] signed=[materialized[0],I(15,0,9,0x8008),I(9,9,9,-0x4000),I(9,9,9,-0x5C88),materialized[3],materialized[4],materialized[5],0];
Check(Proved(signed),"signed low-half address construction resolves exact native table");
var malformed=new (string Name,int At,uint Word)[]{
    ("wrong table",2,I(9,9,9,0x637C)),("wrong segment",1,I(15,0,9,0x8008)),
    ("unknown base input",2,I(9,8,9,0x6378)),("overwritten literal base",2,I(13,0,9,0x6378)),
    ("wrong shift",3,R(0,2,2,1,0)),("index copy escapes",3,R(0,2,3,2,0)),
    ("wrong index register",4,R(8,9,2,0,33)),("different result register",5,I(35,2,3,0)),
    ("halfword root",5,I(33,2,2,0)),("store instead of root load",5,I(43,2,2,0)),
    ("control transfer",2,UniversalMips.EncodeJ(3,0x800524C4)),("class passed to engine",2,I(9,2,4,0)),
    ("load delay consumes stale class",1,R(0,2,2,2,0)),("discarded class load",0,I(33,4,0,0x36)),
    ("wrong field offset",0,I(33,4,2,0x34)),("indexed base alias",2,I(9,2,9,0x6378))};
foreach(var extra in new (string Name,uint Word)[]{("repeated shift",materialized[3]),("repeated indexed add",materialized[4]),("class index store",I(43,29,2,0))})
{ var words=materialized.Take(5).Concat(new[]{extra.Word}).Concat(materialized.Skip(5)).ToArray();Check(!Proved(words),"refuse "+extra.Name); }
var escaped=materialized.ToArray();escaped[4]=R(2,9,4,0,33);escaped[5]=I(35,4,2,0);
Check(!Proved(escaped),"indexed pointer cannot escape through another register");
var zeroBase=legacy.ToArray();zeroBase[1]=I(15,0,0,0x8007);zeroBase[3]=R(2,0,2,0,33);
Check(!Proved(zeroBase),"a literal cannot be written to the immutable zero register");
foreach(var test in malformed){var words=materialized.ToArray();words[test.At]=test.Word;Check(!Proved(words),"refuse "+test.Name);}
for(int c=0;c<0x200;c++)
{
    // Independent arithmetic oracle for the complete native class namespace: the new
    // proof keeps destination IDs, so the exact 512-entry table owner is selected.
    var o=Overlay(materialized);var registers=new uint[32];registers[4]=0x800A0000;
    uint? pending=null;int pendingRegister=0;uint root=0x80100000+(uint)c*8;
    for(int n=0;n<6;n++)
    {
        uint w=o.Word(UniversalOverlay.Base+0x100+(uint)n*4),value=0;int rt=UniversalMips.Rt(w),rs=UniversalMips.Rs(w),rd=UniversalMips.Rd(w);bool write=false,load=false;
        switch(UniversalMips.Op(w))
        {
            case 33:value=(uint)c;load=true;break;
            case 15:value=(uint)UniversalMips.Imm(w)<<16;write=true;break;
            case 9:value=unchecked(registers[rs]+(uint)UniversalMips.SImm(w));write=true;break;
            case 0:value=UniversalMips.Fn(w)==0?registers[rt]<<UniversalMips.Sh(w):unchecked(registers[rs]+registers[rt]);rt=rd;write=true;break;
            case 35:Check(registers[rs]+(uint)UniversalMips.SImm(w)==0x80076378+(uint)c*4,"exact destination model root address "+c);value=root;load=true;break;
            default:throw new InvalidOperationException("unexpected scalar instruction");
        }
        if(pending is uint delayed)registers[pendingRegister]=delayed;
        pending=null;if(load){pending=value;pendingRegister=rt;}else if(write)registers[rt]=value;
    }
    Check(pending==root,"exact root pointer loaded without inverse class conversion "+c);
}
Console.WriteLine($"UNIVERSAL_CLASS_OWNERSHIP_FREE_OK checks={checks}");
if(args.Length==0)return;
if(args.Length<3||args[0]!="--source")throw new ArgumentException("--source <USA.bin> <sourceWorkspace> [baseline.json.gz output.json.gz]");
var catalog=LevelCatalog.Load(args[2]);var library=new UniversalSourceLibrary(args[1],catalog);
var known=new(string Key,uint Pc)[]{("icecavern",0x8007D3B0),("loftycastle",0x8007E144),("highcaves",0x8007F968),("metalhead",0x800819D0),("clifftown",0x8008214C),("darkpassage",0x8007F004),("wizardpeak",0x80080380),("gnorccove",0x8007D3F8),("twilightharbor",0x8007CF38),("beastmakers",0x8007FEBC)};
foreach(var site in known)
{
    var overlay=library.Get(site.Key).Overlay;
    Check(UniversalClassRemapping.FeedsModelTable(overlay,site.Pc),"exact source native model-table owner "+site.Key);
    var cloneBytes=overlay.Bytes.ToArray();var clone=new UniversalOverlay(cloneBytes,overlay.Bindings);
    BinaryPrimitives.WriteUInt32LittleEndian(cloneBytes.AsSpan((int)(site.Pc+8-UniversalOverlay.Base)),UniversalMips.WithImm(clone.Word(site.Pc+8),0x637C));
    Check(!UniversalClassRemapping.FeedsModelTable(clone,site.Pc),"source mutation invalidates owner "+site.Key);
    UniversalAsmPart Part(UniversalOverlay image)
    { var part=new UniversalAsmPart();for(uint pc=site.Pc;pc<=site.Pc+0x18;pc+=4)part.Emit(image.Word(pc),orig:pc);return part; }
    var originalPart=Part(overlay);var originalWords=originalPart.Items.Select(i=>i.Word).ToArray();
    var mapping=new Dictionary<int,int>{[0x53]=0x1FD};
    UniversalClassRemapping.PatchOwnedClassReads(originalPart,overlay,mapping,[0x53]);
    Check(originalPart.Items[0].ClassReadOwned&&originalPart.Items.Select(i=>i.Word).SequenceEqual(originalWords),
        "model lookup retains destination identity and all source instructions "+site.Key);
    UniversalClassRemapping.RequireNoUnownedClassMemory(originalPart,false,overlay);
    var corruptPart=Part(clone);UniversalClassRemapping.PatchOwnedClassReads(corruptPart,clone,mapping,[0x53]);
    bool refused=false;try{UniversalClassRemapping.RequireNoUnownedClassMemory(corruptPart,false,clone);}catch(InvalidDataException){refused=true;}
    Check(refused,"final remap guard refuses an ambiguous wrong-table access "+site.Key);
}
Console.WriteLine($"UNIVERSAL_CLASS_OWNERSHIP_SOURCE_OK checks={checks} sourceSha256={Convert.ToHexStringLower(SHA256.HashData(File.OpenRead(args[1])))}");
if(args.Length==5&&args[3]=="--export")
{ WrittenModelLookupCases.Run(library,catalog,args[4],Check);Console.WriteLine($"UNIVERSAL_CLASS_OWNERSHIP_WRITTEN_OK checks={checks}");return; }
if(args.Length==5)
{
    using var input=File.OpenRead(args[3]);using var zipped=new GZipStream(input,CompressionMode.Decompress);
    var baseline=JsonSerializer.Deserialize<UniversalCoverageReport>(zipped)!;
    using var sourceHash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    string coreRoot=Path.Combine(args[2],"src","Spyro.Editor.Core");
    foreach(string file in Directory.EnumerateFiles(coreRoot,"*.cs",SearchOption.AllDirectories)
        .Where(p=>!p.Contains(Path.DirectorySeparatorChar+"obj"+Path.DirectorySeparatorChar)&&!p.Contains(Path.DirectorySeparatorChar+"bin"+Path.DirectorySeparatorChar))
        .OrderBy(p=>Path.GetRelativePath(coreRoot,p),StringComparer.Ordinal))
    { sourceHash.AppendData(System.Text.Encoding.UTF8.GetBytes(Path.GetRelativePath(coreRoot,file)+"\0"));sourceHash.AppendData(File.ReadAllBytes(file)); }
    string fingerprint=Convert.ToHexStringLower(sourceHash.GetHashAndReset());
    var variantById=baseline.Variants.ToDictionary(v=>v.VariantId);
    List<UniversalCoverageCell> cells=[];
    var plans=new Dictionary<(string Destination,string Source,int Class,int Properties,int Fixups,int Links,int SelfLinks,int Treasure),UniversalCoverageCell>();
    var protectedVram=new Dictionary<string,IReadOnlyList<(long Offset,int Length)>>();
    int exact=0;
    foreach(var old in baseline.Cells.Where(c=>c.FailureKind=="class-remap-ownership"))
    {
        string? detail=old.Detail;if(detail==null||!detail.Contains("class-field access at 0x"))continue;
        var v=variantById[old.VariantId];if(v.SpecialId!=null)continue;
        uint pc=Convert.ToUInt32(detail.Split("0x")[1].TrimEnd('.'),16);
        var source=library.Get(v.SourceLevelKey);
        if(!source.Overlay.Contains(pc)||!UniversalClassRemapping.FeedsModelTable(source.Overlay,pc))continue;
        var dest=library.Get(old.Destination);var template=source.Data.CaptureRow(v.SourceLevelKey,v.SourceRow);var row=source.Data.Row(v.SourceRow);
        var key=(old.Destination,v.SourceLevelKey,v.ClassId,template.Properties.Length,template.InternalPointers.Count,template.DroppedExternalPointers,
            template.ExternalRowPointers.Count(l=>l.SourceRow==v.SourceRow),UniversalMobyComposer.TreasureValue(row));
        if(plans.TryGetValue(key,out var prior)){cells.Add(prior with{VariantId=v.VariantId,StaticAnalysisMethod="reused-identical-resource-plan"});continue;}
        if(!protectedVram.TryGetValue(old.Destination,out var ranges))protectedVram[old.Destination]=ranges=UniversalMobyExport.DefaultProtectedVram(args[1],dest.Level).ToArray();
        var placement=new UniversalMobyPlacement(v.SourceLevelKey,v.SourceRow,BitConverter.ToInt32(row,0x0C),BitConverter.ToInt32(row,0x10),BitConverter.ToInt32(row,0x14));
        UniversalCoverageCell cell;
        try
        {
            var result=UniversalMobyComposer.Compose(dest.Level,dest.Data.Bytes,dest.Overlay.Bytes.ToArray(),library.Disc.Exe,library.Get,library.SharedClasses,ranges,[placement],sharedModelPackage:library.SharedModelPackage);
            cell=new(old.Destination,v.VariantId,"listed",result.ParticleDependencies.Any(p=>p.Status=="dependency-unverified")||result.UnresolvedObjectLinks>0?"composed-dependency-unverified":"composed-static-only","not-tested",null,null,result.Classes,result.Warnings,result.Ram)
                {ParticleDependencies=result.ParticleDependencies,Behavior=result.Behavior,RestoredObjectLinks=result.RestoredObjectLinks,UnresolvedObjectLinks=result.UnresolvedObjectLinks};
        }
        catch(Exception e) when(e is InvalidDataException or IOException or InvalidOperationException or ArgumentException)
        {cell=new(old.Destination,v.VariantId,"listed","failed","not-tested",UniversalMobyCoverage.FailureKind(e),e.Message,[],[],e is UniversalBudgetException b?b.Budget:null);}
        cell=cell with{StaticPlanRepresentativeRow=v.SourceRow};plans[key]=cell;cells.Add(cell);exact++;
        if(exact%50==0)Console.WriteLine($"OWNERSHIP_DELTA_PROGRESS exact={exact} cells={cells.Count} destination={old.Destination}");
    }
    var report=baseline with{Cells=cells,ImplementationModuleVersionId=typeof(UniversalMobyComposer).Module.ModuleVersionId.ToString(),SourceCodeFingerprint=fingerprint,
        SourceCodeFingerprintScope="Isolated Core .cs content at delta start: sorted relative path + NUL + file bytes. Source snapshot is preserved V1 checkpoint plus owned class-ownership fix only.",
        RuntimeEvidenceScope="Focused current-source model-table ownership delta only. Baseline cells outside this file were not recomposed. Runtime remains not-tested."};
    using(var file=File.Create(args[4]))using(var compressed=new GZipStream(file,CompressionLevel.SmallestSize))JsonSerializer.Serialize(compressed,report);
    Console.WriteLine(JsonSerializer.Serialize(new{Scope="exact-source model-table ownership affected cells only",Cells=cells.Count,ExactCompositions=exact,ResourcePlanReuse=cells.Count-exact,
        Composed=cells.Count(c=>c.DependencyExportStatus.StartsWith("composed")),Outcomes=cells.GroupBy(c=>c.FailureKind??c.DependencyExportStatus).ToDictionary(g=>g.Key,g=>g.Count()),Runtime="not-tested"}));
}
