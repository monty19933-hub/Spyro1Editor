using System.Security.Cryptography;
namespace Spyro.Editor.Core.Universal;
/// <summary>Private source/retained-native poof resource/effect ownership. Particle
/// byte0 dispatches classes11/72; byte1 is render mode/free-end status. This does
/// not enroll the Alpine cohort or replace its ordinary product guards.</summary>
internal static class UniversalAlpineArmPoofOwners
{
 internal static void Require(UniversalLevelData source,UniversalOverlay sourceOverlay,byte[] originalExe,UniversalLevelData originalHost,UniversalOverlay host,UniversalLevelData written)
 {
  UniversalAlpineArmResources.ProveSource(source,sourceOverlay);UniversalAlpineArmNative.RequireNative(originalExe);
  if(Convert.ToHexStringLower(SHA256.HashData(originalHost.Bytes))!=UniversalAlpineArmArenaStorage.HostHash||Convert.ToHexStringLower(SHA256.HashData(host.Bytes))!="1ee0030607139491b6d076c389837d5b6c0a38e87d324fec2e14b58b47f95aef")throw new InvalidDataException("Alpine poof requires the complete intact original Blowhard native manager/resource owner.");
  var st=UniversalParticleDependencies.Records(source);var ht=UniversalParticleDependencies.Records(originalHost);var wt=UniversalParticleDependencies.Records(written);
  if(!st.TryGetValue(0,out var s)||!ht.TryGetValue(0,out var h)||!wt.TryGetValue(0,out var w)||!h.SequenceEqual(w)||!UniversalParticleDependencies.EquivalentTexture(s,source.Vram(),h,originalHost.Vram())||!UniversalParticleDependencies.EquivalentTexture(h,originalHost.Vram(),w,written.Vram()))throw new InvalidDataException("Alpine native poof quad texture0 descriptor/pixels/palette lost its authenticated source-equivalent retained owner.");
  // Explicit compiler allocation. Values, field offsets, native callees and
  // control topology are never normalized. Exact complete overlays authenticate
  // all incoming entries, tables, manager setup and return-save ownership.
  int R(int r)=>r switch{18=>17,19=>21,21=>22,22=>30,_=>r};
  var blocks=new[]{(Type:11,Source:0x8008BA3Cu,Host:0x80084F58u,Bytes:0x44),(Type:72,Source:0x8008C3A4u,Host:0x8008590Cu,Bytes:0x13C),(Type:-1,Source:0x8008C970u,Host:0x80085ED8u,Bytes:0x34)};
  uint Target(uint v)=>v switch{0x8008C970=>0x80085ED8,0x8008C980=>0x80085EE8,0x8008C988=>0x80085EF0,0x8008AFAC=>0x8008488C,_=>Relative(v)};
  uint Relative(uint v){var owners=blocks.Where(b=>v>=b.Source&&v<b.Source+b.Bytes).ToArray();if(owners.Length!=1)throw new InvalidDataException("Alpine poof selected branch has no exact retained body owner.");return owners[0].Host+v-owners[0].Source;}
  foreach(var b in blocks)
  {
   if(b.Type>=0&&(sourceOverlay.Word(0x8007B854+(uint)b.Type*4)!=b.Source||host.Word(0x8007AC5C+(uint)b.Type*4)!=b.Host))throw new InvalidDataException("Alpine poof byte0 dispatch table changed.");
   for(int off=0;off<b.Bytes;off+=4)
   {
    uint pc=b.Source+(uint)off,dp=b.Host+(uint)off,word=sourceOverlay.Word(pc);int op=UniversalMips.Op(word),rs=UniversalMips.Rs(word),rt=UniversalMips.Rt(word),rd=(int)(word>>11&31),im=(int)(word&65535);uint normalized;
    if(op is 2 or 3){uint t=UniversalMips.BranchTarget(word,pc);if(t>=UniversalOverlay.Base)t=Target(t);normalized=UniversalMips.EncodeJ(op,t);}
    else if(op==0)normalized=(word&~((31u<<21)|(31u<<16)|(31u<<11)))|((uint)R(rs)<<21)|((uint)R(rt)<<16)|((uint)R(rd)<<11);
    else
    {
     if(op is 1 or 4 or 5 or 6 or 7)im=(int)((Target(UniversalMips.BranchTarget(word,pc))-dp-4)/4)&65535;
     if(op is 32 or 33 or 35 or 36 or 37 or 40 or 41 or 43 && rs==29 && im is 0x80 or 0x84 or 0x88)im-=0x30;
     normalized=(word&~((31u<<21)|(31u<<16)|65535u))|((uint)R(rs)<<21)|((uint)R(rt)<<16)|(uint)im;
    }
    if(normalized!=host.Word(dp))throw new InvalidDataException("Alpine poof retained manager changed an actual selected instruction/value/field/native callee/control owner.");
   }
  }
  UniversalAlpineArmDeathSound.Require(source,sourceOverlay,originalExe,originalHost,written);
 }
 internal static void RequireCalls(UniversalAsmPart update,UniversalLevelData source,UniversalOverlay overlay,byte[] originalExe,IReadOnlyDictionary<int,int> map)
 {
  var expected=UniversalAlpineArmWorldCalls.Build(source,overlay,originalExe,map);
  if(!update.Items.SequenceEqual(expected.Update.Items)||update.Tables.Count!=expected.Update.Tables.Count||expected.Update.Tables.Any(p=>!update.Tables.TryGetValue(p.Key,out var targets)||!targets.SequenceEqual(p.Value)))throw new InvalidDataException("Alpine poof caller closure/incoming ownership changed.");
  var calls=update.Items.Where(i=>i.Word==UniversalMips.EncodeJ(3,0x800385BC)).ToArray();
  if(!calls.Select(i=>i.SourcePc??i.Orig??0).Order().SequenceEqual(new uint[]{0x8007D914,0x80082B20}))throw new InvalidDataException("Alpine actual native poof caller inventory changed.");
  foreach(var(pc,arg)in new[]{(0x8007D914u,24),(0x80082B20u,70)})
   if(overlay.Word(pc-4)!=0x02602021||overlay.Word(pc)!=UniversalMips.EncodeJ(3,0x800385BC)||overlay.Word(pc+4)!=UniversalMips.EncodeI(9,0,5,arg))throw new InvalidDataException("Alpine poof actor/second-argument/delay packet changed.");
 }
}
