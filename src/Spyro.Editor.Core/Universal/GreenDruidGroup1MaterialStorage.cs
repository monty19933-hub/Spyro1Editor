using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
namespace Spyro.Editor.Core.Universal;
/// <summary>Only exact issued Alpine group1 six-sector materials. Keeps original HQ4/HQ8
/// depth, blend flags and all sixteen LQ distance palettes. Generic texture
/// grammar is unchanged. This is resource ownership, never gameplay permission.</summary>
internal sealed class GreenDruidGroup1MaterialStorage
{
 internal sealed record Field(int Offset,bool Low,int Bpp,int Side,int Pixel,int Width,int Palette,int PaletteWidth,int PaletteRows);
 private readonly byte[] _gpu;private readonly byte[][] _records;
 internal int AllocatedByteCount=>Regions.Sum(r=>r.RowByteLength*r.RowCount);
 private readonly NativeStaticTerrainTextureAllocationRegion[] _regions;
 internal IReadOnlyList<NativeStaticTerrainTextureAllocationRegion> Regions=>_regions.ToArray();
 private static int I(ReadOnlySpan<byte>b,int at)=>BinaryPrimitives.ReadInt32LittleEndian(b[at..]);
 private static void W(Span<byte>b,int at,int v)=>BinaryPrimitives.WriteInt32LittleEndian(b[at..],v);
 private static void Require(bool ok,string why){if(!ok)throw new InvalidDataException("Green Druid issued material: "+why);}
 internal static Field[] Describe(ReadOnlySpan<byte> record)
 {
  Require(record.Length==184&&record[..8].SequenceEqual(record.Slice(8,8))&&record[..8].SequenceEqual(record.Slice(16,8)),"complete native LQ/HQ aliases");List<Field> result=[];
  for(int i=0;i<23;i++)
  {
   var r=record.Slice(i*8,8);bool low=i<3;int side=low?32:r[4]-r[0]+1,bpp=low?4:(r[6]&128)!=0?8:4;
   Require(r[5]==r[1]&&(low?(r[0]%2==0&&r[4]-r[0]==31&&r[7]==128&&(r[6]&136)==8):((r[7]&127)==0&&(i<7?side==32:side is 16 or 32)&&(r[6]&8)==8)),"original axis-aligned source depth/page/shape");
   int x=low?(r[6]&7)*128+r[0]/2:(r[6]&15)*128+r[0]/(bpp==4?2:1)-1024,y=(r[6]&16)!=0?256+r[1]:r[1],width=side/(bpp==4?2:1);
   Require(r[1]+side<=256&&(bpp!=4||r[0]%2==0),"native complete packed texel rectangle");int px,py,pw,pr;
   if(low){px=r[2]*32-1024;py=r[3]*4;pw=32;pr=16;}else{int clut=BinaryPrimitives.ReadUInt16LittleEndian(r[2..]);Require((clut&32768)==0,"native CLUT bounds");px=(clut&63)*32-1024;py=clut>>6;pw=bpp==4?32:512;pr=1;}
   Require(x>=0&&x+width<=1024&&y>=0&&y+side<=512&&px>=0&&px+pw<=1024&&py>=0&&py+pr<=512,"all issued samples belong to loaded right-half VRAM");result.Add(new(i*8,low,bpp,side,y*1024+x,width,py*1024+px,pw,pr));
  }
  return result.ToArray();
 }
 internal static byte[] CopyRectangle(ReadOnlySpan<byte>b,int offset,int width,int rows)
 {Require(b.Length==0x80000,"exact GPU plane");byte[] r=new byte[width*rows];for(int y=0;y<rows;y++)b.Slice(offset+y*1024,width).CopyTo(r.AsSpan(y*width));return r;}
 private GreenDruidGroup1MaterialStorage(byte[]source,IReadOnlyList<UniversalGreenDruidWorldPrerequisites.SourceMaterial> original,string owner,byte[]host,UniversalVramAllocator allocator)
 {
  Require(host.Length==0x80000,"exact destination GPU plane");_gpu=host.ToArray();_records=original.Select(m=>m.Record.ToArray()).ToArray();List<NativeStaticTerrainTextureAllocationRegion> regions=[];
  var staged=allocator.Clone();Dictionary<(int Offset,int Width,int Rows,int Bpp,bool Palette,bool Low),int> targets=[];
  int Allocate(int material,int offset,int width,int rows,int bpp,bool palette,bool low)
  {
   var key=(offset,width,rows,bpp,palette,low);if(targets.TryGetValue(key,out int cached))return cached;
   int target=-1;for(int y=0;y+rows<=512&&target<0;y+=palette&&low?4:1){if(!palette&&y%256+rows>256)continue;for(int x=0;x+width<=1024;x+=palette?32:16){if(!palette&&x%128*(bpp==4?2:1)+width*(bpp==4?2:1)>256)continue;bool free=true;for(int r=0;r<rows;r++)if(!staged.IsFree((y+r)*1024+x,width)){free=false;break;}if(free){target=y*1024+x;break;}}}
   Require(target>=0,"no full free rectangle remains; failed reservations discarded");var bytes=CopyRectangle(source,offset,width,rows);for(int r=0;r<rows;r++){bytes.AsSpan(r*width,width).CopyTo(_gpu.AsSpan(target+r*1024));staged.Reserve(target+r*1024,width);}targets[key]=target;
   regions.Add(new(material,owner,"original-alpine-gpu","green-druid-native-depth-preserving-r1",palette?"palette":"pixel",bpp,offset,target,width,rows,Convert.ToHexStringLower(SHA256.HashData(bytes))));return target;
  }
  for(int m=0;m<_records.Length;m++)foreach(var f in Describe(_records[m]))
  {
   int pixel=Allocate(m,f.Pixel,f.Width,f.Side,f.Bpp,false,f.Low),palette=Allocate(m,f.Palette,f.PaletteWidth,f.PaletteRows,f.Bpp,true,f.Low);var r=_records[m].AsSpan(f.Offset,8);int x=pixel%1024,y=pixel/1024,px=palette%1024,py=palette/1024,u=x%128*(f.Bpp==4?2:1);
   r[0]=(byte)u;r[1]=(byte)(y%256);r[4]=(byte)(u+f.Side-1);r[5]=r[1];r[6]=(byte)((r[6]&224)|(f.Bpp==8?128:0)|8|x/128|(y>=256?16:0));
   if(f.Low){r[2]=(byte)((px+1024)/32);r[3]=(byte)(py/4);}else BinaryPrimitives.WriteUInt16LittleEndian(r[2..],(ushort)(py*64+(px+1024)/32));
  }
  for(int m=0;m<_records.Length;m++){var a=Describe(original[m].Record);var b=Describe(_records[m]);for(int n=0;n<a.Length;n++){Require(a[n].Bpp==b[n].Bpp&&a[n].Side==b[n].Side,"native depth/shape retained");Require(CopyRectangle(source,a[n].Pixel,a[n].Width,a[n].Side).SequenceEqual(CopyRectangle(_gpu,b[n].Pixel,b[n].Width,b[n].Side))&&CopyRectangle(source,a[n].Palette,a[n].PaletteWidth,a[n].PaletteRows).SequenceEqual(CopyRectangle(_gpu,b[n].Palette,b[n].PaletteWidth,b[n].PaletteRows)),"every original pixel and complete palette copied exactly");}}
  for(int n=0;n<host.Length;n++)if(allocator.IsFree(n,1)==staged.IsFree(n,1))Require(host[n]==_gpu[n],"protected/unallocated GPU complement retained");_regions=regions.ToArray();
 }
 internal static GreenDruidGroup1MaterialStorage Capture(UniversalGreenDruidGroup1WorldPrerequisites proof,byte[]hostGpu,UniversalVramAllocator allocator)=>new(proof.CopySourceVram(),proof.CopyMaterials(),"original-alpine-group1-complete-route",hostGpu,allocator);
 internal byte[] CopyGpu()=>_gpu.ToArray();internal byte[][] CopyRecords()=>_records.Select(r=>r.ToArray()).ToArray();
 internal byte[] AppendTable(byte[]before)
 {
  int count=I(before,4);Require(count>=0&&count+_records.Length<=128&&before.Length==8+count*184,"complete retained native table");byte[] result=new byte[8+(count+_records.Length)*184];W(result,0,result.Length);W(result,4,count+_records.Length);before.AsSpan(8,count*16).CopyTo(result.AsSpan(8));before.AsSpan(8+count*16,count*168).CopyTo(result.AsSpan(8+(count+_records.Length)*16));for(int n=0;n<_records.Length;n++){_records[n].AsSpan(0,16).CopyTo(result.AsSpan(8+(count+n)*16));_records[n].AsSpan(16).CopyTo(result.AsSpan(8+(count+_records.Length)*16+(count+n)*168));}return result;
 }
 internal void RequireFinal(byte[]gpu,byte[][]records){Require(gpu.AsSpan().SequenceEqual(_gpu)&&records.Length==_records.Length&&records.Where((r,n)=>!r.SequenceEqual(_records[n])).Any()==false,"issued complete final GPU/material bytes changed");}
}
