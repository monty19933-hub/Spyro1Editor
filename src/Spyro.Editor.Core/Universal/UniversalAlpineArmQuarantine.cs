namespace Spyro.Editor.Core.Universal;
/// <summary>Private experiment-only joining of authenticated complete cohort and
/// retained host callbacks. Not reachable from product Composer or catalog.</summary>
internal static class UniversalAlpineArmQuarantine
{
 internal static (byte[] Code,Dictionary<string,uint> Labels) Build(UniversalLevelData source,UniversalOverlay sourceOverlay,UniversalOverlay host,byte[] originalExe,IReadOnlyDictionary<int,int> map,uint address)
 {
  UniversalAlpineArmNative.RequireNative(originalExe);
  var p=UniversalAlpineArmWorldCalls.Build(source,sourceOverlay,originalExe,map);
  UniversalAlpineArmWorldCalls.RequireCopied(source,sourceOverlay,originalExe,map,p.Update,p.Constructors,p.World);
  UniversalAsmPart wrappers=new();void E(uint word,UniversalFix f=default)=>wrappers.Emit(word,f);
  void I(int op,int rs,int rt,int value)=>E(UniversalMips.EncodeI(op,rs,rt,value));
  wrappers.Label("quarantine_wrap_update");I(9,29,29,-24);I(43,29,31,16);
  E(UniversalMips.EncodeJ(3,host.Update));E(0);
  E(UniversalMips.EncodeJ(3,0),new(UniversalFixKind.JalLabel,Label:"alpine_cohort_owned"));E(0);
  I(35,29,31,16);E(0);E(UniversalMips.JrRa);I(9,29,29,24);
  wrappers.Label("quarantine_wrap_init");
  foreach(int c in new[]{0xFD,0x10F,0x13B})
  { I(9,0,1,map[c]);E(UniversalMips.EncodeI(5,4,1,0),new(UniversalFixKind.BranchLabel,Label:$"quarantine_init_next_{c:X3}"));E(0);E(UniversalMips.EncodeJ(2,0),new(UniversalFixKind.JumpLabel,Label:$"alpine_cohort_ctor_{c:X3}"));I(9,0,4,c);wrappers.Label($"quarantine_init_next_{c:X3}"); }
  E(UniversalMips.EncodeJ(2,host.Init));E(0);
  return UniversalBehaviorBundleBuilder.Assemble(new[]{p.Update}.Concat(p.Constructors).Append(p.World).Append(wrappers).ToList(),address);
 }
 internal static void RequireWritten(UniversalLevelData source,UniversalOverlay sourceOverlay,UniversalOverlay host,byte[] originalExe,IReadOnlyDictionary<int,int> map,uint address,byte[] code,IReadOnlyDictionary<string,uint> labels)
 {
  var expected=Build(source,sourceOverlay,host,originalExe,map,address);
  if(!code.SequenceEqual(expected.Code)||labels.Count!=expected.Labels.Count||expected.Labels.Any(p=>!labels.TryGetValue(p.Key,out var a)||a!=p.Value))throw new InvalidDataException("Private Alpine quarantine final code/labels/callbacks differ from independently assembled exact owners and actual allocation.");
 }
}
