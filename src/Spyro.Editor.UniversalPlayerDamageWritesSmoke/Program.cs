using System.Buffers.Binary;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Universal;
int checks=0;void Check(bool v,string label){checks++;if(!v)throw new InvalidOperationException(label);}
uint I(int op,int rs,int rt,int imm)=>UniversalMips.EncodeI(op,rs,rt,imm);
uint R(int rd,int rs,int rt,int fn=0x25)=>((uint)rs<<21)|((uint)rt<<16)|((uint)rd<<11)|(uint)fn;
(UniversalOverlay,SortedSet<uint>) Fixture(uint[] words){byte[] b=new byte[words.Length*4];for(int i=0;i<words.Length;i++)BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(i*4),words[i]);var bindings=new Dictionary<uint,UniversalExeBinding>();foreach(uint g in new[]{UniversalExeBindings.InitGlobal,UniversalExeBindings.UpdateGlobal,UniversalExeBindings.CopyBufferGlobal})bindings[g]=new(g,0,0,UniversalOverlay.Base);return(new(b,bindings),new(Enumerable.Range(0,words.Length).Select(i=>UniversalOverlay.Base+(uint)i*4)));}
UniversalPlayerDamageWriteAnalysis Read(uint[] words){var f=Fixture(words);return UniversalPlayerDamageWrites.AnalyzeOverlay(f.Item1,f.Item2);}
uint hi=I(15,0,8,0x8008),load=I(35,8,3,unchecked((short)0x8A84)),store=I(43,8,3,unchecked((short)0x8A84));
foreach(int mask in new[]{1,6,0x100,0x800,0x8100}){var r=Read([hi,load,0,I(13,3,3,mask),store,UniversalMips.JrRa,0]);Check(r.DamageMask==(uint)mask&&r.Writes.Count==1,"exact read/ORI/write mask");}
var alias=Read([hi,load,0,R(16,3,0,0x21),I(9,0,9,0x800),R(3,16,9),store,UniversalMips.JrRa,0]);Check(alias.DamageMask==0x800,"native alias and constant-register OR");
Check(Read([hi,load,0,I(13,3,3,0x100),I(13,3,3,0x800),store,UniversalMips.JrRa,0]).DamageMask==0x900,"multiple exact OR masks accumulate");
Check(Read([hi,load,I(13,3,3,0x800),store,UniversalMips.JrRa,0]).DamageMask==0,"load-delay consumer refused");
Check(Read([hi,load,R(16,3,0),I(13,16,3,0x800),store,UniversalMips.JrRa,0]).DamageMask==0,"load-delay alias refused");
Check(Read([hi,I(35,8,3,unchecked((short)0x8A80)),0,I(13,3,3,0x800),store,UniversalMips.JrRa,0]).DamageMask==0,"unrelated global load refused");
Check(Read([hi,load,0,I(13,3,3,0x800),I(43,8,3,unchecked((short)0x8A80)),UniversalMips.JrRa,0]).DamageMask==0,"unrelated store refused");
Check(Read([hi,load,0,I(14,3,3,0x800),store,UniversalMips.JrRa,0]).DamageMask==0,"XOR is not OR");
Check(Read([hi,load,0,I(12,3,3,0x800),store,UniversalMips.JrRa,0]).DamageMask==0,"AND is not additive damage");
Check(Read([hi,load,0,R(3,3,9),store,UniversalMips.JrRa,0]).DamageMask==0,"mutable register OR refused");
Check(Read([hi,load,0,I(9,0,3,0x800),store,UniversalMips.JrRa,0]).DamageMask==0,"overwrite without accumulator provenance refused");
Check(Read([hi,load,0,UniversalMips.EncodeJ(3,0x80010000),0,I(13,3,3,0x800),store,UniversalMips.JrRa,0]).DamageMask==0,"call boundary refused");
Check(Read([hi,load,0,I(4,16,0,1),0,I(13,3,3,0x800),store,UniversalMips.JrRa,0]).DamageMask==0,"branch join refused");
Check(Read([hi,load,0,I(13,3,3,0x800),store,(16u<<21)|8,0]).DamageMask==0,"unknown indirect entry declines analysis");
var hole=Fixture([hi,load,0,I(13,3,3,0x800),store,UniversalMips.JrRa,0]);hole.Item2.Remove(UniversalOverlay.Base+8);Check(UniversalPlayerDamageWrites.AnalyzeOverlay(hole.Item1,hole.Item2).DamageMask==0,"ownership hole refused");
if(args.Length==2){UniversalSourceLibrary.RequireOriginal(args[0]);var lib=new UniversalSourceLibrary(args[0],LevelCatalog.Load(args[1]));var s=lib.Get("wizardpeak");uint f=s.Overlay.FunctionOf(0x80084CC8),end=s.Overlay.FunctionEnd(f);var owned=new SortedSet<uint>(Enumerable.Range(0,(int)(end-f)/4).Select(i=>f+(uint)i*4));var a=UniversalPlayerDamageWrites.AnalyzeOverlay(s.Overlay,owned);foreach(var pair in new[]{(0x80084CC8u,0x80084CA8u),(0x8007BA94u,0x8007BA74u)})Check(a.Writes.Any(w=>w.StorePc==pair.Item1&&w.LoadPc==pair.Item2&&w.DamageMask==0x800),"exact original Wizard writer");Check(a.AnimationIds(lib.Disc.Exe).SequenceEqual([16,18]),"certified writer-to-animation map");var cut=new SortedSet<uint>(owned);cut.Remove(0x80084CC0);Check(!UniversalPlayerDamageWrites.AnalyzeOverlay(s.Overlay,cut).Writes.Any(w=>w.StorePc==0x80084CC8),"pruned-out OR cannot become a dependency");var dst=lib.Get("toasty").Overlay;
var yes=UniversalBehaviorBundleBuilder.Build(dst,[("wizardpeak",s.Overlay,(IReadOnlyList<int>)new[]{0x12C})],dst.CopyBuffer);
Check(yes.Sources.Single().PlayerDamageWrites.Any(w=>w.StorePc==0x80084CC8),"actual copied12C closure owns special writer");
var no=UniversalBehaviorBundleBuilder.Build(dst,[("wizardpeak",s.Overlay,(IReadOnlyList<int>)new[]{0x11C})],dst.CopyBuffer);
Check(!no.Sources.Single().PlayerDamageWrites.Any(w=>w.StorePc==0x80084CC8),"actual copied11C closure excludes other class writer");}
Console.WriteLine($"UNIVERSAL_PLAYER_DAMAGE_WRITES_OK checks={checks}");
