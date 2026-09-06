namespace Spyro.Editor.Core.Exporting;

internal sealed record NativeMobyIndexedTextureLocation(int PixelOffset,int PixelRowBytes,int Rows,int PaletteOffset,
    int U,int V,ushort Page,ushort Palette);

/// <summary>Neutral deterministic geometry, never a source-ownership certificate.</summary>
internal sealed class NativeMobyIndexedTextureAllocation
{
    private bool[] _occupied;
    internal NativeMobyIndexedTextureAllocation(ReadOnlySpan<byte> original,IReadOnlyList<NativeTexturePageOwnedRange> protectedRanges)
    {
        if(original.Length!=0x80000)throw new InvalidDataException("Native indexed texture allocator requires the fixed right-half VRAM upload.");
        _occupied=new bool[original.Length];
        for(int i=0;i<original.Length;i++)_occupied[i]=original[i]!=0;
        foreach(var range in protectedRanges)
        {
            if(range==null||range.Offset<0||range.Length<=0||range.Offset+(long)range.Length>original.Length)
                throw new InvalidDataException("Native texture protected extent is outside its upload owner.");
            Array.Fill(_occupied,true,(int)range.Offset,range.Length);
        }
    }
    internal NativeMobyIndexedTextureLocation Reserve(int width,int height)
    {
        if(width<2||width>256||width%2!=0||height<1||height>256)
            throw new InvalidDataException("Native 4-bpp texture request has unsupported rectangle dimensions.");
        int bytes=width/2,pixel=-1,u=0,v=0;ushort page=0;
        for(int y=0;y<=512-height&&pixel<0;y++)
        {
            if(y%256+height>256)continue;
            for(int x=0;x<=1024-bytes;x+=2)
            {
                int pageX=(x+1024)/128,localU=((x+1024)-pageX*128)*2;
                if(localU+width>256)continue;
                bool free=true;
                for(int row=0;row<height&&free;row++)for(int b=0;b<bytes;b++)
                    if(_occupied[(y+row)*1024+x+b]){free=false;break;}
                if(free){pixel=y*1024+x;u=localU;v=y%256;page=(ushort)(pageX|(y>=256?16:0));break;}
            }
        }
        if(pixel<0)throw new InvalidDataException("No descriptor-encodable private indexed pixel rectangle fits.");
        bool[] pending=_occupied.ToArray();for(int row=0;row<height;row++)Array.Fill(pending,true,pixel+row*1024,bytes);
        int clut=-1;
        for(int p=0;p<=pending.Length-32;p+=32)
        {if(pending.AsSpan(p,32).Contains(true))continue;clut=p;break;}
        if(clut<0)throw new InvalidDataException("No independently owned aligned CLUT row fits the pixel transaction.");
        Array.Fill(pending,true,clut,32);_occupied=pending;
        int palette=(clut/1024)*64+((clut%1024+1024)/32);
        return new(pixel,bytes,height,clut,u,v,page,checked((ushort)palette));
    }
}
