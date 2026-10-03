using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
namespace Spyro.Editor.Core.Universal;

/// <summary>Exact Haunted scene Superflame material. Dynamic native flame UVs
/// retain a whole 4bpp page and all eight fade palettes, independently of particles.</summary>
internal sealed class UniversalHauntedSuperflameScene
{
 internal const int PixelBytes=32768,PaletteBytes=256;
 private readonly byte[] tile,pixels,palettes;
 private UniversalHauntedSuperflameScene(byte[] t,byte[] p,byte[] c){tile=t.ToArray();pixels=p.ToArray();palettes=c.ToArray();}
 private static string H(ReadOnlySpan<byte>b)=>Convert.ToHexStringLower(SHA256.HashData(b));
 private static Span<byte> Tile(UniversalLevelData d)=>d.Bytes.AsSpan(d.Subfiles()[3].Offset+0x78,8);
 internal static void RequireNative(byte[] exe)
 {
  // Complete native flame renderer through its return delay slot, and both
  // complete presentation-header reload owners. This is not a UV-envelope proof.
  foreach(var(a,n,h)in new[]{(0x80058DB4u,0x6C0,"a9aab7cf86ef43f4153c1ca947a5be518ba4e09a8101531852eba7c5147ccc94"),(0x80013C38u,0x60,"499f10f94325d20cbb6828dbf1a35b75bae208e35a6abcd1cd320c85cfaab0ac"),(0x80013B88u,0xEC,"be89b0a7f46830d578c18164a5494ca86d6fa2fd5a3a741b6714e7d5b62075bc")})
   if(exe.Length!=0x66000||H(exe.AsSpan((int)(a-0x8000F800),n))!=h)throw new InvalidDataException("Superflame needs its complete original native flame renderer and scene-header reload owners.");
 }
 private static NativeId65SceneHeaderTextureFootprint Layout(ReadOnlySpan<byte>t)=>NativeId65SceneHeaderTextureOwners.DecodeAvailableTiledef(13,t);
 private static byte[] Copy(UniversalLevelData d,IReadOnlyList<NativeId65VramRange>rows)
 {byte[] b=new byte[rows.Sum(r=>r.ByteLength)];int at=0;foreach(var r in rows){d.Vram().Slice((int)r.VramRelativeOffset,r.ByteLength).CopyTo(b.AsSpan(at));at+=r.ByteLength;}return b;}
 internal static UniversalHauntedSuperflameScene Bind(UniversalLevelData source,UniversalOverlay overlay,UniversalLevelData destination,UniversalVramAllocator alloc,byte[] exe)
 {
  UniversalHauntedWizardPlacement.RequireSource(source,overlay);RequireNative(exe);
  byte[] t=Tile(source).ToArray();if(Convert.ToHexStringLower(t)!="8000e0199f002d80")throw new InvalidDataException("The original Haunted scene Superflame Tiledef changed.");
  if(Tile(destination).ContainsAnyExcept((byte)0))throw new InvalidDataException("This exact Dark Hollow Superflame route requires its original unavailable scene material slot.");
  var original=Layout(t);byte[] p=Copy(source,original.PixelRows),c=Copy(source,original.PaletteRows);
  if(p.Length!=PixelBytes||c.Length!=PaletteBytes)throw new InvalidDataException("Superflame lost its complete original page and eight fade palettes.");
  var page=alloc.Rectangle(128,256)??throw new InvalidDataException("Superflame needs a free complete texture page (32,768 bytes). Remove optional object families to free a whole page before adding this group.");
  (int X,int Y)? palette=null;
  for(int y=0;y+8<=512&&palette==null;y++)for(int x=0;x+32<=1024;x+=32)if(Enumerable.Range(0,8).All(r=>alloc.IsFree((y+r)*1024+x,32))){palette=(x,y);break;}
  if(palette==null)throw new InvalidDataException("Superflame needs eight adjacent free fade palettes (256 bytes). Remove optional object families before adding this group.");
  var clut=palette.Value;for(int r=0;r<8;r++)alloc.Reserve((clut.Y+r)*1024+clut.X,32);
  BinaryPrimitives.WriteUInt16LittleEndian(t.AsSpan(2),(ushort)((clut.Y<<6)|((clut.X+1024)/32)));
  BinaryPrimitives.WriteUInt16LittleEndian(t.AsSpan(6),(ushort)((BinaryPrimitives.ReadUInt16LittleEndian(t.AsSpan(6))&~0x1F)|(8+page.X/128)|(page.Y>=256?16:0)));
  var relocated=Layout(t);int at=0;foreach(var r in relocated.PixelRows){p.AsSpan(at,r.ByteLength).CopyTo(destination.Vram().Slice((int)r.VramRelativeOffset,r.ByteLength));at+=r.ByteLength;}at=0;foreach(var r in relocated.PaletteRows){c.AsSpan(at,r.ByteLength).CopyTo(destination.Vram().Slice((int)r.VramRelativeOffset,r.ByteLength));at+=r.ByteLength;}t.CopyTo(Tile(destination));
  var result=new UniversalHauntedSuperflameScene(t,p,c);result.Require(destination);return result;
 }
 internal void Require(UniversalLevelData destination)
 {
  if(!Tile(destination).SequenceEqual(tile))throw new InvalidDataException("Superflame scene Tiledef changed after authenticated binding.");var l=Layout(tile);
  if(!Copy(destination,l.PixelRows).SequenceEqual(pixels)||!Copy(destination,l.PaletteRows).SequenceEqual(palettes))throw new InvalidDataException("Superflame complete native page or fade palettes changed after authenticated binding.");
 }
 internal static void RequireEquivalent(UniversalLevelData source,UniversalLevelData destination)
 {
  var s=Layout(Tile(source));var d=Layout(Tile(destination));byte[] a=Tile(source).ToArray(),b=Tile(destination).ToArray();a.AsSpan(2,2).Clear();b.AsSpan(2,2).Clear();BinaryPrimitives.WriteUInt16LittleEndian(a.AsSpan(6),(ushort)(BinaryPrimitives.ReadUInt16LittleEndian(a.AsSpan(6))&~0x1F));BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(6),(ushort)(BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(6))&~0x1F));
  if(!a.SequenceEqual(b)||!Copy(source,s.PixelRows).SequenceEqual(Copy(destination,d.PixelRows))||!Copy(source,s.PaletteRows).SequenceEqual(Copy(destination,d.PaletteRows)))throw new InvalidDataException("Superflame requires its complete original scene material, dynamic texture page and fade palettes.");
 }
}
