using System.Buffers.Binary;
using System.Collections.Frozen;
using System.Security.Cryptography;
namespace Spyro.Editor.Core.Universal;

/// <summary>Issued original-model authority for seven shader placeholders, never ordinary textures.</summary>
public sealed class UniversalMetalModelCertificate
{
    private readonly byte[] _package;
    private readonly FrozenSet<int> _fields;
    internal UniversalMetalModelCertificate(byte[] package,IEnumerable<int> fields)
    { _package=package.ToArray(); _fields=fields.ToFrozenSet(); }
    public IReadOnlySet<int> ShaderFieldOffsets=>_fields;
    public IReadOnlyList<int> TextureDescriptorOffsets(byte[] package)
    {
        if(!package.SequenceEqual(_package))throw new InvalidDataException("Metal texture authority requires its exact original model package before relocation.");
        return UniversalAssetImport.TextureDescriptorOffsets(package).Where(f=>!_fields.Contains(f)).ToArray();
    }
    public void RequireShaderFields(byte[] package)
    {
        // AppendModels aligns the final package extent to a sector. Its extra tail
        // must be only zero padding; the issued source extent and every shader field remain.
        if(package.Length<_package.Length||package.Length-_package.Length>=0x800||package.AsSpan(_package.Length).IndexOfAnyExcept((byte)0)>=0||_fields.Any(f=>!package.AsSpan(f,12).SequenceEqual(_package.AsSpan(f,12))))
            throw new InvalidDataException("An issued E2 shader placeholder changed during model relocation.");
    }
}

/// <summary>The destination's immutable issued metal material and complete sampled storage.</summary>
public sealed class UniversalMetalTextureBinding
{
    internal UniversalMetalTextureBinding(byte[] tile,byte[] pixels,byte[] palette,int x,int pageY,bool imported)
    { _tile=tile.ToArray(); _pixels=pixels.ToArray(); _palette=palette.ToArray(); X=x; PageY=pageY; Imported=imported; }
    internal ReadOnlySpan<byte> Tile=>_tile;
    private readonly byte[] _tile;
    internal ReadOnlySpan<byte> Pixels=>_pixels;
    private readonly byte[] _pixels;
    internal ReadOnlySpan<byte> Palette=>_palette;
    private readonly byte[] _palette;
    internal int X {get;}
    internal int PageY {get;}
    public bool Imported {get;}
    public string MaterialOrigin=>Imported?"imported-source-metal-material":"destination-context-metal-material";
    public int PixelBytes=>UniversalCliffMetalTextures.PixelBytes;
    public int PaletteBytes=>UniversalCliffMetalTextures.PaletteBytes;
    public string TileHex=>Convert.ToHexStringLower(Tile);
    public string PixelsSha256=>Convert.ToHexStringLower(SHA256.HashData(Pixels));
    public string PaletteSha256=>Convert.ToHexStringLower(SHA256.HashData(Palette));
}

/// <summary>Source-authenticated Cliff Town E2 metal-mode support. Native shared environmental
/// rendering retains a valid destination metal material. A zero slot gains a separately allocated
/// original source resource; unrelated scene Tiledefs and protected VRAM are never replaced.</summary>
public static class UniversalCliffMetalTextures
{
    public const int MinV=4,MaxV=124,Width=256,Height=MaxV-MinV+1,PixelBytes=Width*Height,PaletteRows=8,PaletteWidth=512,PaletteBytes=PaletteRows*PaletteWidth;
    private const string DataHash="8246261aa9d5fe9f83bc138051bbfc9734af4673928808b08d1b414674babdc0";
    private const string OverlayHash="25f0fc287cc8b6b5b70d960630025e009e0e425f7af723dadf008605af5b0286";
    private const string ModelHash="54f245b28f384467f8cc8eb0c6216bd78641670b4f760d3673423b8305ee7ce4";
    private static readonly int[] Fields=[2084,2124,2144,2172,2192,2212,2232];
    private static readonly (uint Pc,int Length,string Hash)[] Native=
    [
        (0x80020C74,0x34,"f12ae319a81a8a2fe88cf6b43d619583677d0cca9a9b2e03bb794259484f6462"),
        (0x800215A8,0x194,"ec6b2ea5c779e59433addeb0a4048656d463c45b853b385af800af5122fbf758"),
        (0x8002173C,0x44,"ad7c9b5294699cfb84a018e9ecf284c7ae715910e03b2219dfa729777438da92"),
        (0x80021AF8,0x124,"3aa0678edff2d39fb283bd4779ec17ec88499827b0e797df165022b6a7ebbbf7"),
        (0x80021DB4,0x12C,"664f426a6c40efd58cd35bfc1efb2cf43aac6c2aeef24463d17f77d4ad30f874"),
        (0x800220E8,0x1D8,"1b75b9290a6248099cabd2e19de9744d08382f222b7a270099bf482fd1304e44"),
        (0x800222C0,0x2AC,"4765afd2c2c34c236fddea622511246f99195fa589a68f11cf3b96a8c670e4a1"),
        (0x800226F8,0x180,"eefb9a9cad67aa6a961ed9089954ba56aabb92f13faa2e269ed7ba71d6fe3e4f"),
        (0x80013C38,0x60,"499f10f94325d20cbb6828dbf1a35b75bae208e35a6abcd1cd320c85cfaab0ac"),
        (0x8006D808,0x100,"bfd15b1bc78bdb15ba9dbb3f7ef311fbae7f98bdf7f6a78917b1459bf90cc942"),
        (0x8006D908,0x80,"be23403c566e06156b0c13dd224b93497eaca0cffaedcb5d29fdab7f04ce204a"),
        (0x8006CBF8,0x200,"877872b39150488ce0baad5491d04ec492f0a7d4ba45248244c46a0ea548a82b"),
        (0x80074B84,0xC0,"f50f5b59b59d22e64e299c926f16f3829520e02acaf5133816c422934bdb7d5e")
    ];
    private static string Hash(ReadOnlySpan<byte> b)=>Convert.ToHexStringLower(SHA256.HashData(b));
    public static void ProveNative(byte[] exe)
    {
        exe=Spyro.Editor.Core.Exporting.NativeDragonStatueColors.OriginalRendererView(exe);
        foreach(var n in Native){long offset=(long)n.Pc-0x8000F800;if(offset<0||offset+n.Length>exe.Length||Hash(exe.AsSpan((int)offset,n.Length))!=n.Hash)throw new InvalidDataException($"Native E2 metal producer/consumer/table owner changed at 0x{n.Pc:X8}.");}
        // Native215A8..21738 builds S2 as either n<<22(n0..7) or a
        // low-word fade amount0..0xFFE. With exactzero UV0, the latter cannot
        // carry into the CLUT halfword. The former selects eight consecutive
        // 256-color CLUT rows via22294..222AC; all eight are owned resources.
        // The native signed-byte V table is addressed only after explicit0..255
        // clamping at224A0..224BC. Every entry is positive and lies in4..124.
        var table=exe.AsSpan((int)(0x8006D808-0x8000F800),256);
        if(table.ToArray().Any(v=>v<MinV||v>MaxV))throw new InvalidDataException("Native generated metal V leaves its issued sample envelope.");
        // U is masked to255 and halved for mode1; seam correction may add128
        // (22270..22290). Thus every emitted U remains inside0..255. The signed
        // rotated mode2 is not enrolled by this model/row certificate.
    }
    public static UniversalMetalModelCertificate? Capture(string sourceKey,UniversalLevelData data,UniversalOverlay overlay,byte[] package,byte[] exe)
    {
        if(!string.Equals(sourceKey,"clifftown",StringComparison.OrdinalIgnoreCase)||Hash(package)!=ModelHash)return null;
        ProveNative(exe);
        if(Hash(data.Bytes)!=DataHash||Hash(overlay.Bytes)!=OverlayHash||overlay.Update!=0x8007E240||overlay.Init!=0x80088B88||overlay.CopyBuffer!=0x8008BAF8||!data.Package(0xE2).SequenceEqual(package))
            throw new InvalidDataException("E2 shader-field authority requires the complete original Cliff Town source/model and callback owners.");
        var refs=UniversalAssetImport.TextureDescriptorOffsets(package);
        if(refs.Count!=57||Fields.Any(f=>!refs.Contains(f)||(BinaryPrimitives.ReadUInt32LittleEndian(package.AsSpan(f-8))&0x80000006)!=6))
            throw new InvalidDataException("E2 shader placeholders lost their exact textured/specular triangle command owners.");
        var sw=UniversalDispatchAnalysis.FindUpdateSwitch(overlay);var trace=UniversalDispatchAnalysis.Trace(overlay,sw.TreeStart,0xE2,sw.Seeds,sw.ClassRegister);
        var update=UniversalDispatchAnalysis.Closure(overlay,sw.Function,[trace.Stop],new HashSet<uint>{sw.TreeStart,sw.DefaultStop}).Instructions;
        var init=UniversalSpecializer.Specialize(overlay,overlay.Init,4,[0xE2]).Instructions;
        if(update.Concat(init).Any(pc=>UniversalMips.IsStore(overlay.Word(pc))&&UniversalMips.Rs(overlay.Word(pc))!=29&&UniversalMips.SImm(overlay.Word(pc)) is 0x4B or 0x4C or 0x4F))
            throw new InvalidDataException("E2 source has a new unauthenticated shader-mode producer.");
        var rows=Enumerable.Range(0,data.Scene().RowCount).Where(r=>BinaryPrimitives.ReadUInt16LittleEndian(data.Row(r).AsSpan(0x36))==0xE2).ToArray();
        if(!rows.SequenceEqual(new[]{24,25,26,32,33,34,35,40,43,56})||rows.Any(r=>data.Row(r)[0x4B]!=0x90||data.Row(r)[0x4F]!=1))throw new InvalidDataException("E2 placed source no longer owns its shiny renderer/metal-mode domain.");
        return new(package,Fields);
    }
    public static void RequireRow(UniversalMetalModelCertificate certificate,ReadOnlySpan<byte> row)
    {
        if(certificate is null||row.Length!=UniversalLevelData.RowStride||(row[0x4B]&0x80)==0||row[0x4F]!=1)throw new InvalidDataException("E2 shader placeholder imports require the source shiny renderer in exact metal mode1.");
    }
    private static Span<byte> Tile(UniversalLevelData data)=>data.Bytes.AsSpan(data.Subfiles()[3].Offset+0x80,8);
    private static (int X,int Y,int Palette) Layout(ReadOnlySpan<byte> tile)
    {
        ushort page=BinaryPrimitives.ReadUInt16LittleEndian(tile[6..]),pal=BinaryPrimitives.ReadUInt16LittleEndian(tile[2..]);
        int x=(page&15)*128-1024,y=(page&16)!=0?256:0,palette=(pal>>6)*1024+(pal&63)*32-1024;
        if(BinaryPrimitives.ReadUInt16LittleEndian(tile)!=0||BinaryPrimitives.ReadUInt16LittleEndian(tile[4..])!=31||(page&~0x1F)!=0x80A0||x<0||x+Width>1024||palette<0||(pal>>6)+PaletteRows>512||palette+PaletteWidth>UniversalVramAllocator.ImageBytes||(pal&63)<32||(pal&63)+16>64)
            throw new InvalidDataException("Destination metal Tiledef lacks its exact zero-base UV, native8bpp page/blend tag or right-half CLUT owner.");
        return(x,y,palette);
    }
    private static byte[] Pixels(UniversalLevelData data,int x,int pageY)
    { byte[] b=new byte[PixelBytes];for(int row=0;row<Height;row++)data.Vram().Slice((pageY+MinV+row)*1024+x,Width).CopyTo(b.AsSpan(row*Width));return b; }
    private static byte[] Palettes(UniversalLevelData data,int offset)
    {byte[] b=new byte[PaletteBytes];for(int r=0;r<PaletteRows;r++)data.Vram().Slice(offset+r*1024,PaletteWidth).CopyTo(b.AsSpan(r*PaletteWidth));return b;}
    private static void ReservePalette(UniversalVramAllocator alloc,int offset)
    {for(int r=0;r<PaletteRows;r++)alloc.Reserve(offset+r*1024,PaletteWidth);}
    private static void Reserve(UniversalVramAllocator alloc,int x,int y)
    {for(int row=0;row<Height;row++)alloc.Reserve((y+MinV+row)*1024+x,Width);}
    private static (int X,int Y)? Allocate(UniversalVramAllocator alloc)
    {
        foreach(int y in new[]{0,256})for(int x=0;x+Width<=1024;x+=128){if(Enumerable.Range(0,Height).All(r=>alloc.IsFree((y+MinV+r)*1024+x,Width))){Reserve(alloc,x,y);return(x,y);}}
        return null;
    }
    private static (int X,int Y)? AllocatePalette(UniversalVramAllocator alloc)
    {
        for(int y=0;y+PaletteRows<=512;y++)for(int x=0;x+PaletteWidth<=1024;x+=32){if(Enumerable.Range(0,PaletteRows).All(r=>alloc.IsFree((y+r)*1024+x,PaletteWidth))){ReservePalette(alloc,y*1024+x);return(x,y);}}
        return null;
    }
    public static UniversalMetalTextureBinding BindDestination(UniversalMetalModelCertificate certificate,UniversalLevelData source,UniversalLevelData destination,UniversalVramAllocator alloc,byte[] exe)
    {
        if(certificate is null)throw new InvalidDataException("Metal resource binding requires an issued model certificate.");
        ProveNative(exe);if(Hash(source.Bytes)!=DataHash)throw new InvalidDataException("An issued metal fallback lost its original source data owner.");
        _=certificate.TextureDescriptorOffsets(source.Package(0xE2));
        byte[] current=Tile(destination).ToArray();bool imported=!current.Any(b=>b!=0);int x,y,palette;byte[] pixels,pal;
        if(!imported){var layout=Layout(current);x=layout.X;y=layout.Y;palette=layout.Palette;pixels=Pixels(destination,x,y);pal=Palettes(destination,palette);Reserve(alloc,x,y);ReservePalette(alloc,palette);}
        else{
            byte[] tile=Tile(source).ToArray();var original=Layout(tile);pixels=Pixels(source,original.X,original.Y);pal=Palettes(source,original.Palette);
            var allocated=Allocate(alloc)??throw new InvalidDataException("Not enough free texture memory for the E2 native metal shader sample envelope.");x=allocated.X;y=allocated.Y;
            var clut=AllocatePalette(alloc)??throw new InvalidDataException("No free CLUT bank remains for the E2 native metal shader eight fog palettes.");palette=clut.Y*1024+clut.X;for(int row=0;row<Height;row++)pixels.AsSpan(row*Width,Width).CopyTo(destination.Vram().Slice((y+MinV+row)*1024+x,Width));for(int r=0;r<PaletteRows;r++)pal.AsSpan(r*PaletteWidth,PaletteWidth).CopyTo(destination.Vram().Slice(palette+r*1024,PaletteWidth));
            BinaryPrimitives.WriteUInt16LittleEndian(tile.AsSpan(2),(ushort)((clut.Y<<6)|((clut.X+1024)/32)));
            BinaryPrimitives.WriteUInt16LittleEndian(tile.AsSpan(6),(ushort)((BinaryPrimitives.ReadUInt16LittleEndian(tile.AsSpan(6))&~0x1F)|(8+x/128)|(y>=256?16:0)));tile.CopyTo(Tile(destination));current=tile;
        }
        var binding=new UniversalMetalTextureBinding(current,pixels,pal,x,y,imported);RequireDestination(binding,destination);return binding;
    }
    public static void RequireDestination(UniversalMetalTextureBinding binding,UniversalLevelData destination)
    {
        if(!Tile(destination).SequenceEqual(binding.Tile))throw new InvalidDataException("Issued E2 destination metal Tiledef changed after resource binding.");var layout=Layout(binding.Tile);
        if(layout.X!=binding.X||layout.Y!=binding.PageY||!Pixels(destination,layout.X,layout.Y).SequenceEqual(binding.Pixels)||!Palettes(destination,layout.Palette).AsSpan().SequenceEqual(binding.Palette))throw new InvalidDataException("Issued E2 destination metal pixels/CLUT changed after resource binding.");
    }
}
