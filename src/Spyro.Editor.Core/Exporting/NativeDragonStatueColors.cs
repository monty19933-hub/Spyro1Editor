using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.Core.Exporting;

/// <summary>Per-instance crystal tint in the native USA shiny-moby renderer. The original
/// palettes stay immutable; only the current draw's existing fog-palette scratch is tinted. The shards a rescue
/// throws off take the freed statue's colour too.</summary>
public static class NativeDragonStatueColors
{
    public const uint ColourTag = 0x7E000000;
    public sealed record Patch(uint Address, byte[] Before, byte[] After);
    private sealed record Segment(uint Address, string Original, string Installed);
    // Four whole, unused native math functions, certified against all 35 level overlays,
    // title/credits code and WAD address owners. The adjacent live functions are untouched.
    // Generated R3000 payload: tools/Build-DragonStatueTint.py. T0-T2 and LO are preserved;
    // V0-V1/A0-A3 are dead on every native continuation. No shared model is modified.
    private static readonly Segment[] Segments =
    [
        new(0x80017188, "0000a18c0400a28c0800a38c0200210400002520222805000200410400004620223006000200610400006720223807002528a6002528a70000f08548000000000000000000f80548110006202228c5000400a018000000000708a1000710a2000718a300000081ac040082ac0800e003080083ac", "3000818f00000000020e01007e002138b5012014000000000180013c847828ac887829ac8c782aac3000818f0200e497ff00253002320100ff00c630023c0100ff00e730211020030780033cf40d63240000489001004990000000002b500901d70040110000000021402001525d000800000000"),
        new(0x80017548, "0000818c0400828c0800838c0000a88c0400a98c0800aa8c0000c48c0400c58c0800c68c222081002228a2002230c3002208010122102201221843010048844800508548005886480000c1480010c2480020c34800000000000000000c00704b0000f9e80400fae80800e0030800fbe8", "02004990000000002b5009010200401100000000214020011900050112480000024a090004004224190006011250000000ff4a3125482a01190007011250000002520a0000540a0025482a01ffff4a90ffff842400560a0025482a01000069ac0bff801404006324c75d000800000000"),
        new(0x8001771C, "0000818c0000a28c0400838c090022140400a18c0800828c060061140800a18c0000000003002214000000000800e003010002200800e00300000220", "824005000043080082480600804909002540090182480700254009018000013c254001010780193cf40d39270180013c907828ac155e000800000000"),
        new(0x80017854, "0000a18c0400a28c0800a38c0000c1480010c2480020c3480000c9c80400cac80800cbc800000000000000000c00704b000099e804009ae80800e00308009be8", "8478288c8878298c8c782a8c9078218c13008003d5850008000000003000818fd585000800000000000000000000000000000000000000000000000000000000"),
        // The crystal shards a rescue throws off (class 0xFB) are lit faces drawn with material 14 at 0x8006E410
        // (green 0x0A206020, read at 0x80023538..0x800235F4); no tint reaches them. At every rescue start
        // (0x8002C924, where it stores the statue at 0x8002CABC) this sets material 14 from the statue being freed:
        // a tagged statue's colour scaled like the crystal's (x 96/256), otherwise the original green. Host: an
        // unreferenced library function (no call, jump, branch or address to it in the executable or any WAD entry).
        new(0x8005F428, "0780023c664a4290e0ffbd271400b1af1000b0af0780103c674a10261c00bfaf1800b2af000012920200422c08004014218880000180043cdc1784240780023c604a428c0000000009f84000212820020780023c5c4a428c000011a22800428c0000000009f8400008000424000003920000000004006010",
            "680007ae4c00e88c200a0a3c024e08007e0029391500201520604a35000a0a3cff0009314058090021482b01c248090025504901024a0800ff0029314058090021482b01c2480900004a090025504901024c0800ff0029314058090021482b01c2480900004c0900255049010780013c0800e00310e42aac"),
        new(0x8002CABC, "680007ae", "0a7d010c"),
        new(0x8002174C, "3000818f", "625c0008"),
    ];
    private static readonly (uint Address, int Size, string Hash)[] Contexts =
    [
        (0x80020F34, 0x1B6C, "1e786903f4137b0b84da199c3b793076eaebc0bd21d5def4acc292b1b6c61894"),
        (0x800196C0, 0x28, "f6a2505fc165bf6054d4c71bce35987c50998552faffbb789ffb640a2abdea7d"),
        (0x8001D5A8, 0x28, "f4e2f6d5d24fd7feda80b6d8a5ed84e32d4ef636d480ec4de1713351b64b8750"),
        // The rescue start (statue store at 0x8002CABC), the lit-face material read and material 14's green.
        (0x8002C924, 0x248, "8f24d37e489577218325bab7ec639dc51a720890da1c7c729526507cea0702e7"),
        (0x80023538, 0xC0, "fa3024959d876013c6dc804201fa451000e7cdb05cf4084d7a0730ccc69e771c"),
        (0x8006E410, 4, "cfa1a337257c96f386a05290182197ae4570d1a080c35a29e98bf10b328f8a24"),
    ];
    private static int Offset(uint address, int size, byte[] exe)
    {
        long offset=(long)address-0x8000F800;
        if(offset<0x800 || offset+size>exe.Length)throw new InvalidDataException("The executable does not contain the dragon colour renderer.");
        return (int)offset;
    }
    private static bool Matches(byte[] exe, Segment s, string hex)
    {
        byte[] bytes=Convert.FromHexString(hex);
        return exe.AsSpan(Offset(s.Address,bytes.Length,exe),bytes.Length).SequenceEqual(bytes);
    }
    private static void RequireContext(byte[] originalView)
    {
        foreach(var c in Contexts)
            if(Convert.ToHexStringLower(SHA256.HashData(originalView.AsSpan(Offset(c.Address,c.Size,originalView),c.Size)))!=c.Hash)
                throw new InvalidDataException($"The native dragon colour renderer owner changed at 0x{c.Address:X8}.");
    }
    /// <summary>A strict view for native consumer proofs when this exact tint extension is installed.
    /// Unknown or partly installed hooks are refused; unrelated executable bytes are retained.</summary>
    public static byte[] OriginalRendererView(byte[] exe)
    {
        var site=Segments[^1];
        if(Matches(exe,site,site.Original))return exe;
        if(!Segments.All(s=>Matches(exe,s,s.Installed)))
            throw new InvalidDataException("The dragon colour renderer is incomplete or has been changed.");
        byte[] view=exe.ToArray();
        foreach(var s in Segments)Convert.FromHexString(s.Original).CopyTo(view,Offset(s.Address,s.Original.Length/2,view));
        RequireContext(view);
        return view;
    }
    public static IReadOnlyList<Patch> Plan(byte[] exe)
    {
        byte[] original=OriginalRendererView(exe);RequireContext(original);
        if(!ReferenceEquals(original,exe))return [];
        if(!Segments.All(s=>Matches(exe,s,s.Original)))
            throw new InvalidDataException("The verified dragon colour code space is already in use.");
        return Segments.Select(s=>new Patch(s.Address,Convert.FromHexString(s.Original),Convert.FromHexString(s.Installed))).ToArray();
    }
    public static void Install(byte[] exe)
    {
        foreach(var p in Plan(exe))p.After.CopyTo(exe,Offset(p.Address,p.After.Length,exe));
    }
    /// <summary>Every animation must fit the native 256-colour fog buffer, including its far palette.</summary>
    public static void RequirePalette(byte[] package)
    {
        if(package.Length<0x3C)throw new InvalidDataException("The dragon model is incomplete.");
        int U32(int offset)=>BinaryPrimitives.ReadInt32LittleEndian(package.AsSpan(offset));
        int n=U32(0),model=U32(0x34);
        if(n is <1 or >256 || 0x38L+n*4>package.Length || model<0 || model>=package.Length)
            throw new InvalidDataException("The dragon model has an invalid animation table.");
        int palettes=0;
        for(int i=0;i<n;i++)
        {
            int animation=U32(0x38+i*4);
            if(animation==-1)continue;
            if(animation<0 || animation+0x24L>package.Length)throw new InvalidDataException("The dragon animation is incomplete.");
            int count=BinaryPrimitives.ReadUInt16LittleEndian(package.AsSpan(animation+2));
            if(count is <1 or >256)throw new InvalidDataException("The dragon palette exceeds the native colour buffer.");
            palettes++;
            foreach(int field in new[]{0x18,0x20})
            {
                long start=(long)model+U32(animation+field);
                if(start<0 || start+count*4L>package.Length)throw new InvalidDataException("The dragon colour table is incomplete.");
            }
        }
        if(palettes==0)throw new InvalidDataException("The dragon model has no colour palette.");
    }
}
