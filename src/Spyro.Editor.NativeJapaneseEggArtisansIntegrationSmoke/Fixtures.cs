using System.Buffers.Binary;

internal static class Fixtures
{
    internal static void Put(byte[] b,int p,uint value)=>BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(p),value);
    internal static byte[] Words(params uint[] w){byte[] b=new byte[4*w.Length];for(int i=0;i<w.Length;i++)Put(b,4*i,w[i]);return b;}
    internal static byte[] Model()
    {
        byte[] b=new byte[0xA0];Put(b,0,1);for(int i=4;i<0x14;i+=4)Put(b,i,uint.MaxValue);
        Put(b,0x34,0x70);Put(b,0x38,0x3C);Put(b,0x3C,0x10002);b[0x42]=2;b[0x44]=b[0x45]=3;
        Put(b,0x54,0x18);Put(b,0x58,0xC);Put(b,0x5C,0x1C);Put(b,0x60,0xFF000020);Put(b,0x64,0x1000000);Put(b,0x68,0xFF000028);Put(b,0x6C,0x1000000);
        Put(b,0x70,8);Put(b,0x74,(1u<<13)|(2u<<4));Put(b,0x7C,8);Put(b,0x80,(1u<<13)|(2u<<4));
        Put(b,0x88,0xFF010203);Put(b,0x8C,0x10203);Put(b,0x90,1);Put(b,0x94,0x20003);Put(b,0x98,1);Put(b,0x9C,0x20003);return b;
    }
    internal static byte[] Scene(ushort actor)
    {
        byte[] b=new byte[0x3000];int p=0x88;for(int i=0;i<7;i++){Put(b,p,8);p+=8;}
        Put(b,p,0x2E0);Put(b,p+4,1);int row=p+8,props=p+0x2E0;
        Put(b,row,(uint)(props+4));BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(row+0x36),actor);b[row+0x4F]=1;
        Put(b,props,0x10);Put(b,props+0x10,4);Put(b,props+0x14,4);Put(b,props+0x18,1);Put(b,props+0x1C,(uint)row);return b;
    }
    internal static byte[] Resource()
    {
        byte[] b=new byte[0x5800];int p=0x800;int[] lengths=[0x800,0x800,0x800,0x3000,0x800];
        for(int i=0;i<lengths.Length;i++){Put(b,i*8,(uint)p);Put(b,i*8+4,(uint)lengths[i]);p+=lengths[i];}
        Put(b,0x50,0x1800);p=0x1000;for(int i=0;i<6;i++){Put(b,p,4);p+=4;}p+=4;Put(b,p,4);Put(b,p+4,4);
        Scene(1).CopyTo(b,0x2000);return b;
    }
    internal static byte[] JapaneseOverlay()
    {
        byte[] b=new byte[0xF800];Words(0x92620049,0x8E700000,0x14400031,0).CopyTo(b,0x93F4);
        Words(0x8E620014,0,0xAE020000).CopyTo(b,0x949C);
        Words(0x24020001,0xA2620049,0x8E040004,0x0C00588F,0,0x8E030000,0x00021183,0x00621821,
            0xAE630014,0x8E020004,0,0x24420080,0xAE020004,0x92630049,0x240A0001,0x246200FF,
            0x146A0009,0xA2620049,0x0C019D6A,0,0x3042001F,0x24420010,0x0C019D6A,0xA2620049,
            0x304200FF,0xAE020008,0x02602021,0x24060008,0xAFA00010,0x8E050008,0x0C00F12D,0x24070008).CopyTo(b,0x94BC);return b;
    }
    internal static byte[] UsaOverlay()
    {
        byte[] b=new byte[0xE000];Words(0x0801F6F5,0).CopyTo(b,0x3064); // Synthetic tree goes to the actual default leaf.
        Words(0x080215E0,0).CopyTo(b,0x319C);return b;
    }
    internal static byte[] Executable()
    {
        byte[] b=new byte[0x66000];Words(0x3C028009,0x24428620,0x3C018008,0xAC2285D8).CopyTo(b,0x4AF18);
        Words(0x24020180,0x03E00008,0).CopyTo(b,0x7458);
        Words(0x3C0341C6,0x3C028007,0x8C425AC0,0x34634E6D,0x00430018,0x00002012,
            0x24823039,0x3C018007,0xAC225AC0,0x00021402,0x03E00008,0x30427FFF).CopyTo(b,0x52F2C);
        Words(0xA0850046,0x03E00008,0).CopyTo(b,0x296E0);return b;
    }
    internal static byte[] Profiles()
    {byte[] b=new byte[128];Put(b,4,0x0A606060);Put(b,36,0x0A006000);return b;}
}
