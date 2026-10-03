namespace Spyro.Editor.Core.Universal;
internal sealed partial class UniversalGreenDruidGroup8Actors
{
 private byte[]? _code;private uint _base;private Dictionary<string,uint>? _labels;private Dictionary<uint,uint>? _origins;
 internal (UniversalAsmPart Update,IReadOnlyList<UniversalAsmPart> Constructors) ReplayActorParts(string particleSpawnLabel)
 {
  Require(_map!=null&&_groups!=null,"issued groups and namespace precede actor replay");var map=_map!;
  var update=UniversalBehaviorBundleBuilder.BuildPrunedUpdate(_overlay,Classes,"u_alpineridge",map).Part;PatchActor(update,_overlay,particleSpawnLabel);
  UniversalNativeModelTailOwnership.MarkOwned(update,_overlay,_exe);UniversalClassRemapping.PatchOwnedClassReads(update,_overlay,map,Classes);UniversalClassRemapping.PatchConstantSpawns(update,_overlay,map);
  var constructors=new Dictionary<int,UniversalAsmPart>();bool remapped=map.Any(m=>m.Key!=m.Value);
  foreach(int c in remapped?Classes:Classes.Take(1))
  {
   var init=UniversalBehaviorBundleBuilder.BuildSpecializedInit(_overlay,remapped?[c]:Classes,remapped?$"i_alpineridge_{c:X3}":"i_alpineridge").Part;
   RequireInitializer(init,_overlay,remapped?[c]:Classes);UniversalClassRemapping.PatchInitClassStore(init,_overlay,c,map[c]);
   if(remapped){UniversalClassRemapping.PatchInitAllocatedClassReads(init,_overlay,map,c);UniversalClassRemapping.PatchInitParentClassReads(init,_overlay,map);UniversalClassRemapping.RequireNoUnownedClassMemory(init,true);}
   UniversalClassRemapping.PatchConstantSpawns(init,_overlay,map);constructors.Add(c,init);
  }
  UniversalNativeLinkedActorOwnership.Patch(update,_overlay,map,constructors);
  if(remapped)UniversalClassRemapping.RequireNoUnownedClassMemory(update,false,_overlay);
  return(update,constructors.Values.ToArray());
 }
 internal void RequireCompiled(byte[]code,uint address,IReadOnlyList<UniversalAsmPart>parts,IReadOnlyDictionary<string,uint>labels,UniversalOverlay destination)
 {
  RequireDestination(destination);Require(_map!=null&&_groups!=null&&address>=destination.CopyBuffer&&address%8==0,"issued complete bundle phase and retained callback bounds");
  var assembled=UniversalBehaviorBundleBuilder.Assemble(parts,address);Require(code.SequenceEqual(assembled.Code)&&labels.Count==assembled.Labels.Count&&labels.All(p=>assembled.Labels.TryGetValue(p.Key,out uint v)&&v==p.Value),"final compiled bytes/labels must replay actual parts");
  var profile=Dispatcher(destination).Profile;var owned=UniversalOwnedParticleDispatch.Build(profile,"particle_alpineridge");var expected=ReplayActorParts(owned.SpawnLabel);
  foreach(var part in new[]{expected.Update}.Concat(expected.Constructors).Concat(new[]{owned.Spawn,owned.Update,UniversalOwnedParticleDispatch.ChainUpdates([owned.UpdateLabel],destination.Bindings[UniversalExeBindings.ParticleUpdateGlobal].Value,"wrap_owned_particles")}))
  {string label=part.Items.First().Label!;var actual=parts.Where(p=>p.Items.FirstOrDefault()?.Label==label).ToArray();Require(actual.Length==1,"one complete emitted actor/constructor/particle owner"+label);RequireSamePart(actual[0],part,"complete final relocated actor/constructor/particle instructions/edges/tables/labels changed");}
  if(_code!=null)Require(_base==address&&_code.SequenceEqual(code)&&_labels!.Count==labels.Count&&_labels.All(p=>labels.TryGetValue(p.Key,out uint v)&&v==p.Value),"issued compiled bundle cannot be rebound");
  _code=code.ToArray();_base=address;_labels=labels.ToDictionary();var origins=new Dictionary<uint,uint>();uint pc=address;foreach(var part in parts){bool actor=part.Items.FirstOrDefault()?.Label=="u_alpineridge";foreach(var i in part.Items){if(i.Word==null)continue;if(actor&&i.Orig is uint orig)Require(origins.TryAdd(orig,pc),"unique final actor origin");pc+=4;}}_origins=origins;
 }
 internal IReadOnlyDictionary<uint,uint> CopyActorOrigins()=>_origins?.ToDictionary()??throw new InvalidDataException("Missing compiled linked actor owner.");
 internal (byte[]Code,IReadOnlyDictionary<string,uint>Labels,uint Base) CopyReplay()=> (_code?.ToArray()??throw new InvalidDataException("Missing compiled linked actor owner."),_labels!.ToDictionary(),_base);
}
