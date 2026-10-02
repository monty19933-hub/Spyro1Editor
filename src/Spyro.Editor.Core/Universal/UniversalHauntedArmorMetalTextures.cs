using System.Security.Cryptography;
namespace Spyro.Editor.Core.Universal;
/// <summary>Original Haunted armor uses the native shared metal-mode1 bank.
/// Its ordinary model textures are retained and relocated; no shader placeholders are issued.</summary>
public static class UniversalHauntedArmorMetalTextures
{
 public static UniversalMetalModelCertificate Capture(UniversalLevelData source,UniversalOverlay overlay,byte[] exe)
 {
  UniversalHauntedWizardPlacement.RequireSource(source,overlay);UniversalHauntedWizardPlacement.RequireNative(exe);UniversalCliffMetalTextures.ProveNative(exe);
  byte[] model=source.Package(0xCB);
  if(Convert.ToHexStringLower(SHA256.HashData(model))!="d6b88688660e09f0105ffc3fda9d6a5ebd53aae2b97adce4bd3af3f1f12036ff" || UniversalAssetImport.TextureDescriptorOffsets(model).Count!=250)
   throw new InvalidDataException("Haunted armor ordinary model texture owner changed.");
  var rows=Enumerable.Range(0,source.Scene().RowCount).Select(r=>source.CaptureRow("hauntedtowers",r)).Where(t=>t.ClassId==0xCB).ToArray();
  if(rows.Length!=25 || rows.Any(t=>t.Row[0x4B]!=0x98 || t.Row[0x4F]!=1))throw new InvalidDataException("Haunted armor native shiny renderer domain changed.");
  // 2176C/22700 chooses mode1; 221E4 loads the shared zero-UV Tiledef,
  // never a model texture descriptor. 22294 issues that bank and the eight
  // S2 fog CLUT rows. Ordinary triangle textures still follow 21CDC..21D54.
  return new(model,[]);
 }
 public static UniversalMetalTextureBinding BindDestination(UniversalMetalModelCertificate certificate,UniversalLevelData source,UniversalOverlay overlay,UniversalLevelData destination,UniversalVramAllocator alloc,byte[] exe)
 {
  UniversalHauntedWizardPlacement.RequireSource(source,overlay);
  // This empty placeholder certificate cannot exempt any ordinary texture job.
  if(certificate.ShaderFieldOffsets.Count!=0)throw new InvalidDataException("Haunted armor cannot use an ordinary texture exemption.");
  return UniversalCliffMetalTextures.BindAuthenticatedMaterial(certificate,source,destination,alloc,exe,0xCB);
 }
}
