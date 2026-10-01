using System.Buffers.Binary;
using System.Text.Json;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Universal;

int checks = 0;
void Check(bool condition, string label) { checks++; if (!condition) throw new InvalidOperationException(label); }
uint I(int op,int rs,int rt,int imm) => UniversalMips.EncodeI(op,rs,rt,imm);
uint R(int rd,int rs,int rt,int fn=0x21) => ((uint)rs<<21)|((uint)rt<<16)|((uint)rd<<11)|(uint)fn;
uint Call(int rs=2) => ((uint)rs<<21)|(31u<<11)|9;
(UniversalOverlay Overlay,SortedSet<uint> Instructions) Fixture(uint[] words)
{
    byte[] bytes=new byte[words.Length*4];
    for(int n=0;n<words.Length;n++) BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(n*4),words[n]);
    var bindings=new Dictionary<uint,UniversalExeBinding>();
    foreach(uint global in new[]{UniversalExeBindings.UpdateGlobal,UniversalExeBindings.InitGlobal,UniversalExeBindings.CopyBufferGlobal})
        bindings[global]=new(global,0,0,UniversalOverlay.Base);
    return(new(bytes,bindings),new(Enumerable.Range(0,words.Length).Select(n=>UniversalOverlay.Base+(uint)n*4)));
}
IReadOnlyList<UniversalDependencyCall> Recover(uint[] words)
{
    var fixture=Fixture(words);return UniversalDependencyAnalysis.Calls(fixture.Overlay,fixture.Instructions);
}
uint hi=I(15,0,2,0x8007),spawn=I(35,2,2,0x58CC),particle=I(35,2,2,0x58E4);
for(int gap=0;gap<=120;gap+=8)
{
    uint[] words=[I(9,0,5,17),..Enumerable.Repeat(0u,gap),hi,particle,0,Call(),0,UniversalMips.JrRa,0];
    var calls=Recover(words);Check(calls.Count==1&&calls[0].Argument1==17,$"long argument window {gap}");
}
var zero=Recover([hi,particle,0,R(5,0,0),Call(),0,UniversalMips.JrRa,0]);
Check(zero.Count==1&&zero[0].Argument1==0,"native move-zero particle argument");
var computed=Recover([I(9,0,8,0x1A5),I(9,8,4,1),hi,spawn,0,Call(),0,UniversalMips.JrRa,0]);
Check(computed.Count==1&&computed[0].Argument0==0x1A6,"computed source child class");
var delay=Recover([hi,particle,0,Call(),I(9,0,5,8),UniversalMips.JrRa,0]);
Check(delay.Count==1&&delay[0].Argument1==8,"callee sees argument delay slot");
var targetDelay=Recover([hi,particle,0,I(9,0,5,8),Call(),I(9,0,2,0),UniversalMips.JrRa,0]);
Check(targetDelay.Count==1&&targetDelay[0].Global==UniversalExeBindings.ParticleSpawnGlobal,"target captured before delay slot overwrite");
var alias=Recover([hi,particle,0,R(16,2,0),I(9,0,5,31),Call(16),0,UniversalMips.JrRa,0]);
Check(alias.Count==1&&alias[0].Argument1==31,"cached callback alias");
Check(Recover([hi,particle,Call(),I(9,0,5,8),UniversalMips.JrRa,0]).Count==0,"load-delay callback is not the new target");
Check(Recover([hi,particle,R(16,2,0),I(9,0,5,8),Call(16),0,UniversalMips.JrRa,0]).Count==0,"alias must not consume load-delay value");
Check(Recover([hi,particle,0,Call(),UniversalMips.EncodeJ(2,UniversalOverlay.Base),UniversalMips.JrRa,0]).Count==0,"control transfer in delay slot is not guessed");
Check(Recover([hi,I(35,2,2,0x58B0),0,Call(),I(9,0,5,8),UniversalMips.JrRa,0]).Count==0,"unrelated callback global is not a dependency");
Check(Recover([hi,particle,0,Call(),I(9,0,5,8),(3u<<21)|8,0]).Count==0,"unresolved indirect entry declines supplemental recovery");
var memory=Recover([hi,particle,0,I(35,16,5,0),Call(),0,UniversalMips.JrRa,0]);
Check(memory.Count==1&&memory[0].Argument1==null,"mutable argument remains unknown");
var overwritten=Recover([I(9,0,5,8),I(9,16,5,0),hi,particle,0,Call(),0,UniversalMips.JrRa,0]);
Check(overwritten.Count==1&&overwritten[0].Argument1==null,"unknown argument overwrite invalidates constant");
Check(Recover([hi,I(9,16,2,0),particle,0,Call(),I(9,0,5,8),UniversalMips.JrRa,0]).Count==0,"unknown callback base remains unknown");
var branch=Recover([I(9,0,5,8),I(4,16,0,1),0,hi,particle,0,Call(),0,UniversalMips.JrRa,0]);
Check(branch.Count==1&&branch[0].Argument1==null,"independent branch entry fences predecessor constants");
var callFence=Recover([I(9,0,5,8),UniversalMips.EncodeJ(3,0x80010000),0,hi,particle,0,Call(),0,UniversalMips.JrRa,0]);
Check(callFence.Count==1&&callFence[0].Argument1==null,"previous call cannot provide caller argument");
var range=Fixture([I(9,0,5,0x51),hi,particle,0,Call(),0,UniversalMips.JrRa,0]);
Check(!UniversalDispatchAnalysis.Dependencies(range.Overlay,range.Instructions).ParticleTypes.Contains(0x51),"out-of-range particle not enrolled");
var added=Fixture([R(5,0,0),hi,particle,0,Call(),0,UniversalMips.JrRa,0]);
Check(UniversalDispatchAnalysis.Dependencies(added.Overlay,added.Instructions).ParticleTypes.Contains(0),"supplement actually enrolls recovered dependency");
var gapFixture=Fixture([I(9,0,5,8),hi,particle,0,Call(),0,UniversalMips.JrRa,0]);gapFixture.Instructions.Remove(UniversalOverlay.Base+4);
Check(UniversalDependencyAnalysis.Calls(gapFixture.Overlay,gapFixture.Instructions).Count==0,"missing instruction ownership cannot be borrowed");
Check(Recover([I(9,0,5,17),..Enumerable.Repeat(0u,132),hi,particle,0,Call(),0,UniversalMips.JrRa,0])[0].Argument1==null,"bounded expression window refuses distant value");
if(args.Length==2)
{
    UniversalSourceLibrary.RequireOriginal(args[0]);var catalog=LevelCatalog.Load(args[1]);var library=new UniversalSourceLibrary(args[0],catalog);
    List<object> evidence=[];
    foreach(var example in new[]{("artisans",0x800849E0u,14u,0x187),("alpineridge",0x80081644u,17u,0x098),("magiccrafters",0x8008591Cu,8u,0x10E),("highcaves",0x80082B50u,31u,0x0E3)})
    {
        var source=library.Get(example.Item1);uint start=source.Overlay.FunctionOf(example.Item2),end=source.Overlay.FunctionEnd(start);
        var owned=new SortedSet<uint>(Enumerable.Range(0,(int)(end-start)/4).Select(i=>start+(uint)i*4));
        var call=UniversalDependencyAnalysis.Calls(source.Overlay,owned).Single(c=>c.CallPc==example.Item2);
        Check(call.Global==UniversalExeBindings.ParticleSpawnGlobal&&call.Argument1==example.Item3,$"actual source {example.Item1} call");
        Check(UniversalDispatchAnalysis.Dependencies(source.Overlay,owned).ParticleTypes.Contains((int)example.Item3),$"actual source {example.Item1} enrolled");
        var dispatch=UniversalDispatchAnalysis.FindUpdateSwitch(source.Overlay);
        var trace=UniversalDispatchAnalysis.Trace(source.Overlay,dispatch.TreeStart,example.Item4,dispatch.Seeds,dispatch.ClassRegister);
        var closure=UniversalDispatchAnalysis.Closure(source.Overlay,dispatch.Function,[trace.Stop],new HashSet<uint>{dispatch.TreeStart,dispatch.DefaultStop}).Instructions;
        Check(closure.Contains(example.Item2),$"actual class {example.Item4:X3} owns call");
        Check(UniversalDispatchAnalysis.Dependencies(source.Overlay,closure).ParticleTypes.Contains((int)example.Item3),$"actual class {example.Item4:X3} enrolls recovered particle");
        evidence.Add(new{Source=example.Item1,Call=call,RuntimeTested=false});
    }
    var destination=library.Get("toasty").Level;
    var magicRefusal=UniversalMobyCatalogBuilder.DryRun(library,destination,[new("magiccrafters",1,0,0,0)]);
    Check(magicRefusal.Result==null&&magicRefusal.Error?.Contains("Class 0x13B",StringComparison.Ordinal)==true,"unsupported Magic Crafters child remains refused");
    var cliffRefusal=UniversalMobyCatalogBuilder.DryRun(library,destination,[new("clifftown",24,0,0,0)]);
    Check(cliffRefusal.Result==null&&cliffRefusal.Error?.Contains("reserved texture depth",StringComparison.OrdinalIgnoreCase)==true,"source FFFF texture pages remain refused");
    evidence.Add(new{Scope="unsupported-dependencies-still-refused",Magic=magicRefusal.Error,Clifftown=cliffRefusal.Error});
    Console.WriteLine(JsonSerializer.Serialize(new{Scope="source-original-call-readback",SourceSha256=UniversalSourceLibrary.OriginalUsaSha256,Evidence=evidence,RuntimeTested=false}));
}
else if(args.Length!=0)throw new ArgumentException("Optional arguments: original-USA-BIN source-workspace");
Console.WriteLine($"UNIVERSAL_DEPENDENCY_CHECKS_OK checks={checks}");
