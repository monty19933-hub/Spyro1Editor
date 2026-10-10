using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.Core.Exporting;

/// <summary>A bounds check on the terrain renderer's two subdivision queues (close +0x1E00 and normal +0x2000 from
/// 0x8006FCF4, 127 entries each before the terminator). The face pass (quads 0x80026C80.., triangles 0x80027244..)
/// pushes a sector header once per sector and queue, then the face, with no check: a level denser than the game's
/// own overruns the queue into the next buffer. With the guard a face that would not fit (header and face) is drawn
/// whole instead, through the pass's own path for distant faces. Retail levels never fill a queue, so nothing they
/// draw changes. Installed for levels imported from another game, whose density the editor cannot bound.</summary>
public static class NativeSubdivisionQueueGuard
{
    private sealed record Site(uint At, uint Header, uint Face, uint Direct, int Pointer, int Bit, uint Limit);
    private const uint Normal = 0x80071CF4, Close = 0x80071AF4, RoomForTwo = 0x1F4;
    private static readonly Site[] Sites =
    [
        new(0x80026D3C, 0x80026D44, 0x80026D50, 0x80026DB8, 28, 1, Normal + RoomForTwo),   // quads, normal queue ($gp)
        new(0x80026D84, 0x80026D8C, 0x80026D98, 0x80026DB8, 29, 2, Close + RoomForTwo),    // quads, close queue ($sp)
        new(0x8002728C, 0x80027294, 0x800272A0, 0x80027310, 28, 1, Normal + RoomForTwo),   // triangles, normal queue
        new(0x800272D8, 0x800272E0, 0x800272EC, 0x80027310, 29, 2, Close + RoomForTwo),    // triangles, close queue
    ];
    /// <summary>Unreferenced library code (no call, jump, pointer or branch to it in the executable or the WAD).</summary>
    public const uint Cave = 0x80068A2C;
    private const int StubWords = 13;
    private const string CaveSha256 = "8509935a9bc1b1918b0965c8093db520c0da4130088e1ff50169486ae7aa8581";

    /// <summary>Installs the guard (refusing an executable that differs from the USA game there); installing it again is
    /// a no-op.</summary>
    public static void PatchExe(byte[] exe)
    {
        int Off(uint address) => (int)(address - UniversalExeBindings.ExeBase);
        uint Word(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(exe.AsSpan(Off(address)));
        var stubs = Sites.Select((s, i) => Stub(s, Cave + (uint)(i * StubWords * 4))).ToArray();
        bool installed = Sites.Select((s, i) => Word(s.At) == UniversalMips.EncodeJ(2, Cave + (uint)(i * StubWords * 4))).ToArray() is var flags && flags.All(f => f);
        if (installed)
        {
            for (int i = 0; i < Sites.Length; i++)
                for (int k = 0; k < StubWords; k++)
                    if (Word(Cave + (uint)((i * StubWords + k) * 4)) != stubs[i][k]) throw new InvalidDataException("The executable's subdivision queue guard differs from this editor's.");
            return;
        }
        foreach (var s in Sites)
            if (Word(s.At) != 0x1D600004 || Word(s.At + 4) != (0x37390000u | (uint)s.Bit))
                throw new InvalidDataException("The executable differs from the USA game where the subdivision queue guard hooks in.");
        if (Convert.ToHexStringLower(SHA256.HashData(exe.AsSpan(Off(Cave), Sites.Length * StubWords * 4))) != CaveHash)
            throw new InvalidDataException("The executable differs from the USA game where the subdivision queue guard is stored.");
        for (int i = 0; i < Sites.Length; i++)
        {
            uint at = Cave + (uint)(i * StubWords * 4);
            for (int k = 0; k < StubWords; k++) BinaryPrimitives.WriteUInt32LittleEndian(exe.AsSpan(Off(at) + 4 * k), stubs[i][k]);
            BinaryPrimitives.WriteUInt32LittleEndian(exe.AsSpan(Off(Sites[i].At)), UniversalMips.EncodeJ(2, at));
            BinaryPrimitives.WriteUInt32LittleEndian(exe.AsSpan(Off(Sites[i].At) + 4), 0);
        }
    }

    /// <summary>Levels with sectors imported from another game need the guard.</summary>
    public static bool Needed(Spyro.Editor.Core.Editing.SceneDocument document) => document.Surfaces.Any(s => s.Sector != null);

    public static bool IsInstalled(byte[] exe) =>
        BinaryPrimitives.ReadUInt32LittleEndian(exe.AsSpan((int)(Sites[0].At - UniversalExeBindings.ExeBase))) == UniversalMips.EncodeJ(2, Cave);

    /// <summary>The replaced pair was "bgtz t3, face; ori t9, t9, bit" (t3 = t9 &amp; bit: this sector's header is
    /// already queued). The stub keeps that, after checking room for a header and a face.</summary>
    /// <summary>Lab only: a smaller limit (bytes of queue allowed before "full"), to exercise the full-queue path.</summary>
    internal static uint? TestRoomOverride;
    private static uint[] Stub(Site s, uint at)
    {
        if (TestRoomOverride is uint room) s = s with { Limit = s.Limit - RoomForTwo + room };
        const int t3 = 11, t9 = 25;
        uint Branch(int op, int rs, int rt, int fromWord, int toWord) => UniversalMips.EncodeI(op, rs, rt, toWord - (fromWord + 1));
        return
        [
            UniversalMips.EncodeI(15, 0, t3, (int)(s.Limit >> 16)),          // lui t3, limit
            UniversalMips.EncodeI(13, t3, t3, (int)(s.Limit & 0xFFFF)),       // ori t3, t3, limit
            (uint)(t3 << 21 | s.Pointer << 16 | t3 << 11 | 0x2B),            // sltu t3, t3, queue pointer
            Branch(5, t3, 0, 3, 11),                                         // bne t3, zero, full
            UniversalMips.EncodeI(12, t9, t3, s.Bit),                        //  andi t3, t9, bit
            Branch(7, t3, 0, 5, 9),                                          // bgtz t3, face
            UniversalMips.EncodeI(13, t9, t9, s.Bit),                        //  ori t9, t9, bit
            UniversalMips.EncodeJ(2, s.Header), 0,                           // j header push
            UniversalMips.EncodeJ(2, s.Face), 0,                             // face: j face push
            UniversalMips.EncodeJ(2, s.Direct), 0,                           // full: j draw whole
        ];
    }

    private static string CaveHash => CaveSha256;
}
