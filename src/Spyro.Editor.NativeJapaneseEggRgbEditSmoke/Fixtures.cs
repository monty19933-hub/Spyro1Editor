using System.Buffers.Binary;

internal static class Fixtures
{
    internal static void Put(byte[] b,int at,uint value)=>BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(at),value);
    // Uses the existing decoder's two-frame geometry. Palette count is variable
    // solely to prove the new edit scope refuses otherwise-valid non70 models.
    internal static byte[] Model(int colors=70)
    {
        int near=0x88,far=near+colors*4,frame0=far+colors*4,frame1=frame0+8;
        byte[] b=new byte[frame1+8];Put(b,0,1);for(int p=4;p<0x14;p+=4)Put(b,p,uint.MaxValue);
        Put(b,0x34,0x70);Put(b,0x38,0x3C);Put(b,0x3C,(uint)(colors<<16)|2);b[0x42]=2;b[0x44]=b[0x45]=3;
        Put(b,0x54,(uint)(near-0x70));Put(b,0x58,0xC);Put(b,0x5C,(uint)(far-0x70));
        Put(b,0x60,0xFF000000|(uint)(frame0-0x70));Put(b,0x64,0x01000000);
        Put(b,0x68,0xFF000000|(uint)(frame1-0x70));Put(b,0x6C,0x01000000);
        Put(b,0x70,8);Put(b,0x74,(1u<<13)|(2u<<4));Put(b,0x78,(uint)(colors-1)<<1);
        Put(b,0x7C,8);Put(b,0x80,(1u<<13)|(2u<<4));Put(b,0x84,(uint)(colors-1)<<19);
        for(int i=0;i<colors;i++)
        {
            uint rgb=(uint)(i*3&255)|(uint)(i*5&255)<<8|(uint)(i*7&255)<<16;
            Put(b,near+4*i,0xFF000000|rgb);Put(b,far+4*i,rgb^0x557799);
        }
        Put(b,frame0,1);Put(b,frame0+4,0x00020003);Put(b,frame1,1);Put(b,frame1+4,0x00020003);return b;
    }
}
