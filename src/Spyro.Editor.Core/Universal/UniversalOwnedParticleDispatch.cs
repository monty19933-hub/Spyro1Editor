using System.Buffers.Binary;
using System.Security.Cryptography;
namespace Spyro.Editor.Core.Universal;

public sealed class UniversalOwnedParticleCertificate
{
 internal UniversalOwnedParticleCertificate(UniversalOwnedParticleProfile profile,string destinationKey,uint nativeUpdate,
  Action<UniversalAsmPart,UniversalOverlay,string> patch,Action<UniversalOverlay> requireDestination)
 { Profile=profile;DestinationLevelKey=destinationKey;NativeUpdate=nativeUpdate;Patch=patch;RequireDestination=requireDestination; }
 internal UniversalOwnedParticleProfile Profile {get;}
 internal uint NativeUpdate {get;}
 internal Action<UniversalAsmPart,UniversalOverlay,string> Patch {get;}
 internal Action<UniversalOverlay> RequireDestination {get;}
 public string SourceLevelKey=>Profile.SourceKey;
 public string DestinationLevelKey {get;}
 public IReadOnlyList<int> TypeIds=>Profile.Spawn.Types;
}

// Issued by a dedicated source/profile validator, not a public generic permission.
internal sealed record UniversalOwnedParticleFunction(uint Entry,int Length,string Sha,uint Table,uint Default,IReadOnlyList<int> Types);
internal sealed record UniversalOwnedParticleProfile(string SourceKey,UniversalOverlay Source,
    UniversalOwnedParticleFunction Spawn,UniversalOwnedParticleFunction Update)
{ internal string OverlaySha256 {get;init;}=Convert.ToHexStringLower(SHA256.HashData(Source.Bytes)); }
internal sealed record UniversalOwnedParticleParts(UniversalAsmPart Spawn,UniversalAsmPart Update,string SpawnLabel,string UpdateLabel);

/// <summary>Preserve the original callback ABI and lifetime code while routing only issued particle types.
/// All other copied table entries enter the original no-op tail. The destination manager runs once.
/// This builder grants no semantic authority: each issuer proves aliases, tables, native callees and resources.</summary>
internal static class UniversalOwnedParticleDispatch
{
 internal static UniversalOwnedParticleParts Build(UniversalOwnedParticleProfile profile,string tag)
 {
  if(Convert.ToHexStringLower(SHA256.HashData(profile.Source.Bytes))!=profile.OverlaySha256)throw new InvalidDataException("Issued particle source overlay changed after capture.");
  return new(BuildOne(profile.Source,profile.Spawn,tag+"_spawn"),BuildOne(profile.Source,profile.Update,tag+"_update"),tag+"_spawn",tag+"_update");
 }
 private static UniversalAsmPart BuildOne(UniversalOverlay original,UniversalOwnedParticleFunction owner,string tag)
 {
  if(!original.Contains(owner.Entry) || (ulong)owner.Entry+(uint)owner.Length>original.End ||
    Convert.ToHexStringLower(SHA256.HashData(original.Bytes.Slice((int)(owner.Entry-UniversalOverlay.Base),owner.Length)))!=owner.Sha ||
    owner.Types.Count==0 || owner.Types.Distinct().Count()!=owner.Types.Count || owner.Types.Any(t=>t<0 || t>80))
   throw new InvalidDataException("An issued particle callback lost its exact source function/type owner.");
  byte[] view=original.Bytes.ToArray();
  // Only the primary, exactly 81-entry particle dispatcher is rewritten. Body
  // branches, secondary tables, allocator calls, timing and RNG remain native.
  for(int t=0;t<=80;t++)if(!owner.Types.Contains(t))BinaryPrimitives.WriteUInt32LittleEndian(view.AsSpan((int)(owner.Table-UniversalOverlay.Base)+t*4),owner.Default);
  var scoped=new UniversalOverlay(view,original.Bindings);
  var part=UniversalBehaviorBundleBuilder.BuildWholeFunction(scoped,owner.Entry,tag).Part;
  RequireExact(part,original,owner,tag);
  return part;
 }
 internal static void RequireExact(UniversalAsmPart part,UniversalOverlay original,UniversalOwnedParticleFunction owner,string tag)
 {
  // Reconstruct the expected issued table view from authenticated original bytes;
  // a caller cannot nominate its own modified dispatcher as the expected body.
  if(Convert.ToHexStringLower(SHA256.HashData(original.Bytes.Slice((int)(owner.Entry-UniversalOverlay.Base),owner.Length)))!=owner.Sha)
   throw new InvalidDataException("Particle expected body lost its source callback owner.");
  byte[] view=original.Bytes.ToArray();
  for(int t=0;t<=80;t++)if(!owner.Types.Contains(t))BinaryPrimitives.WriteUInt32LittleEndian(view.AsSpan((int)(owner.Table-UniversalOverlay.Base)+t*4),owner.Default);
  var expected=UniversalBehaviorBundleBuilder.BuildWholeFunction(new UniversalOverlay(view,original.Bindings),owner.Entry,tag).Part;
  if(!part.Items.SequenceEqual(expected.Items) || part.Tables.Count!=expected.Tables.Count ||
    expected.Tables.Any(p=>!part.Tables.TryGetValue(p.Key,out var t)||!t.SequenceEqual(p.Value)))
   throw new InvalidDataException("An issued particle body/table gained a changed instruction, target, order or entry.");
 }
 internal static UniversalAsmPart ChainUpdates(IReadOnlyList<string> ownedUpdates,uint nativeUpdate,string tag)
 {
  if(ownedUpdates.Count==0 || ownedUpdates.Distinct().Count()!=ownedUpdates.Count)throw new InvalidDataException("Particle update chain has a missing or repeated owner.");
  UniversalAsmPart p=new();p.Label(tag);
  p.Emit(UniversalMips.EncodeI(9,29,29,-0x20));p.Emit(UniversalMips.EncodeI(43,29,31,0x18));p.Emit(UniversalMips.EncodeI(43,29,4,0x14));
  foreach(string entry in ownedUpdates){p.Emit(UniversalMips.EncodeI(35,29,4,0x14));p.Emit(0);p.Emit(UniversalMips.EncodeJ(3,0),new(UniversalFixKind.JalLabel,Label:entry));p.Emit(0);}
  p.Emit(UniversalMips.EncodeI(35,29,31,0x18));p.Emit(UniversalMips.EncodeI(35,29,4,0x14));p.Emit(UniversalMips.EncodeI(9,29,29,0x20));
  p.Emit(UniversalMips.EncodeJ(2,nativeUpdate));p.Emit(0);
  return p;
 }
}
