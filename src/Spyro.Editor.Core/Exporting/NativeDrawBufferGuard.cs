using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.Core.Exporting;

/// <summary>A bound on the terrain's share of the frame's draw buffer, in the levels the editor built
/// (<see cref="NativeDrawLoad"/> has the buffer and the passes). The terrain function writes its packets with no check,
/// so a view with more terrain than the buffer holds runs into the buffer the GPU is drawing or into the ordering table
/// (a hang or a crash, seen in imported levels). With the guard the terrain stops where it would leave less than
/// <see cref="NativeDrawLoad.AfterTerrainReserve"/> for the passes drawn after it:
/// <list type="bullet">
/// <item>once a frame, at the terrain function's entry (0x800258F0), the limit is set from the buffer's end
/// (0x80075780) when the level being drawn (0x8007596C) is one the editor built, and cleared otherwise: retail levels
/// draw exactly as before;</item>
/// <item>before each far face (0x80026398) the far pass ends when the next packet is past the limit;</item>
/// <item>before each near face (0x80026B7C) the near face pass ends when the next packet plus what the faces already
/// queued for subdivision can write (320 bytes a normal entry, 1,280 a close one: four or sixteen pieces of at most two
/// triangles) is past it; the queued faces are still drawn.</item>
/// </list>
/// The rest of the frame stops itself 0x400 bytes before the end, so nothing then runs past the buffer. The stubs live
/// in unreferenced library code (no call, jump, branch, address pair or pointer to it in the executable or WAD.WAD).</summary>
public static class NativeDrawBufferGuard
{
    /// <summary>The stubs (unreferenced library code from 0x800694E8) and their data (unreferenced SPU library code from
    /// 0x8005D91C: the terrain limit, the limit plus the queue constant, a byte per level id 10..73 with its near distance
    /// in 256-unit steps, 0 for a level the editor did not build).</summary>
    public const uint Cave = 0x800694E8, CaveEnd = 0x80069608, DataCave = 0x8005D91C, DataCaveEnd = 0x8005D9EC;
    private const string CaveSha256 = "f6cbd4612249b19ffdd76f4142b444482d033f42bfcb93fe9e6ee3d797678e40";
    private const string DataCaveSha256 = "d1cd5eb5efbb20b0bf0cb5d3116780e6f30b3a8e2473ff59a90c6977a8190251";
    internal const uint TerrainEntry = 0x800258F0, FarFace = 0x80026398, NearFace = 0x80026B7C;
    internal const uint FarPassEnd = 0x80026788, NearPassEnd = 0x80027688, LevelId = 0x8007596C, BufferEnd = 0x80075780,
        NearDistanceWord = 0x800785CC;
    internal const uint NormalQueue = 0x80071CF4, CloseQueue = 0x80071AF4;
    /// <summary>Retail words at the hooks: lui at,0x8007 / addiu at,at,0x7DD8; srl t1,t5,23 / andi t1,t1,0x1F8;
    /// srl v0,t6,14 (srl v1,t6,6 stays, as the jump's delay slot).</summary>
    private static readonly (uint At, uint Word)[] Preimage =
        [(TerrainEntry, 0x3C018007), (TerrainEntry + 4, 0x24217DD8), (FarFace, 0x000D4DC2), (FarFace + 4, 0x312901F8), (NearFace, 0x000E1382), (NearFace + 4, 0x000E1982)];
    /// <summary>Bytes kept past the limit: the reserve plus one more face (two close queue entries, or a packet).</summary>
    public const int Slack = 0xC00;
    internal const uint Limit = DataCave, QueueLimit = DataCave + 4, NearTable = DataCave + 8;
    internal const uint FrameStub = Cave;
    private const int FrameWords = 29, FarWords = 11, NearWords = 18, LevelSlots = 64;
    internal const uint FarStub = FrameStub + 4 * FrameWords, NearStub = FarStub + 4 * FarWords;
    /// <summary>Queue bytes to packet bytes: a normal entry (4 bytes) may become 320 bytes, a close one 1,280.</summary>
    internal const uint NormalWeight = 80, CloseWeight = 320;
    internal static uint QueueConstant => unchecked(NormalWeight * NormalQueue + CloseWeight * CloseQueue);
    /// <summary>Near distances the guard can give a level (editor units): 1,024 to the retail 2,048 in 256-unit steps.</summary>
    public const int MinNearDistance = 1024, MaxNearDistance = 2048, NearStep = 256;

    /// <summary>Level ids the guard covers (one table byte each).</summary>
    public static bool Covers(int levelId) => levelId is >= 10 and < 10 + LevelSlots;

    /// <summary>Installs the guard for the built levels <paramref name="nearDistances"/> names (level id: the near
    /// distance its terrain was planned for, <see cref="NativeAuthoredMeshRenderer.DrawPlan"/>), refusing an executable
    /// that differs from the USA game at the hooks or in either code area; installing it again adds or updates levels.</summary>
    public static void PatchExe(byte[] exe, IReadOnlyDictionary<int, int> nearDistances, int? reserve = null)
    {
        foreach (var (id, near) in nearDistances)
        {
            if (!Covers(id)) throw new InvalidDataException($"Level id {id} is outside the draw-buffer guard's range.");
            if (near is < MinNearDistance or > MaxNearDistance || near % NearStep != 0) throw new InvalidDataException($"Near distance {near} is not one the draw-buffer guard can set.");
        }
        int r = reserve ?? NativeDrawLoad.AfterTerrainReserve + Slack;
        if (r is < Slack or > 0x7FFF) throw new InvalidDataException("The draw-buffer guard's reserve must fit one instruction.");
        var stubs = Stubs(r);
        if (IsInstalled(exe))
        {
            for (int k = 0; k < stubs.Length; k++)
                if (Word(exe, FrameStub + 4 * (uint)k) != stubs[k]) throw new InvalidDataException("The executable's draw-buffer guard differs from this editor's.");
        }
        else
        {
            foreach (var (at, word) in Preimage)
                if (Word(exe, at) != word) throw new InvalidDataException("The executable differs from the USA game where the draw-buffer guard hooks in.");
            if (Convert.ToHexStringLower(SHA256.HashData(exe.AsSpan(Off(Cave), (int)(CaveEnd - Cave)))) != CaveSha256 ||
                Convert.ToHexStringLower(SHA256.HashData(exe.AsSpan(Off(DataCave), (int)(DataCaveEnd - DataCave)))) != DataCaveSha256)
                throw new InvalidDataException("The executable differs from the USA game where the draw-buffer guard is stored.");
            for (int k = 0; k < stubs.Length; k++) Put(exe, FrameStub + 4 * (uint)k, stubs[k]);
            exe.AsSpan(Off(DataCave), 8 + LevelSlots).Clear();
            Put(exe, TerrainEntry, UniversalMips.EncodeJ(2, FrameStub)); Put(exe, TerrainEntry + 4, 0);
            Put(exe, FarFace, UniversalMips.EncodeJ(2, FarStub)); Put(exe, FarFace + 4, 0);
            Put(exe, NearFace, UniversalMips.EncodeJ(2, NearStub));
        }
        foreach (var (id, near) in nearDistances) exe[Off(NearTable) + id - 10] = (byte)(near / NearStep);
    }

    public static bool IsInstalled(byte[] exe) => Word(exe, TerrainEntry) == UniversalMips.EncodeJ(2, FrameStub);

    /// <summary>The levels an installed guard covers, with their near distances.</summary>
    public static IReadOnlyDictionary<int, int> CoveredLevels(byte[] exe)
    {
        if (!IsInstalled(exe)) return new Dictionary<int, int>();
        return Enumerable.Range(0, LevelSlots).Where(i => exe[Off(NearTable) + i] != 0).ToDictionary(i => i + 10, i => exe[Off(NearTable) + i] * NearStep);
    }

    private const int At = 1, V0 = 2, T0 = 8, T1 = 9, T2 = 10, T3 = 11, T5 = 13, T6 = 14, Gp = 28, Sp = 29, Fp = 30;
    private static uint I(int op, int rs, int rt, int imm) => UniversalMips.EncodeI(op, rs, rt, imm);
    private static uint R(int rs, int rt, int rd, int sa, int fn) => (uint)(rs << 21 | rt << 16 | rd << 11 | sa << 6 | fn);
    private static uint Lui(int rt, int imm) => I(15, 0, rt, imm);
    private static uint Lw(int rt, int offset, int rs) => I(35, rs, rt, offset);
    private static uint Lbu(int rt, int offset, int rs) => I(36, rs, rt, offset);
    private static uint Sw(int rt, int offset, int rs) => I(43, rs, rt, offset);
    private static uint Addiu(int rt, int rs, int imm) => I(9, rs, rt, imm);
    private static uint Ori(int rt, int rs, int imm) => I(13, rs, rt, imm);
    private static uint Addu(int rd, int rs, int rt) => R(rs, rt, rd, 0, 0x21);
    private static uint Subu(int rd, int rs, int rt) => R(rs, rt, rd, 0, 0x23);
    private static uint Sll(int rd, int rt, int sa) => R(0, rt, rd, sa, 0);
    private static uint Srl(int rd, int rt, int sa) => R(0, rt, rd, sa, 2);
    private static uint J(uint target) => UniversalMips.EncodeJ(2, target);
    /// <summary>A conditional branch from word <paramref name="from"/> to word <paramref name="to"/> of one stub.</summary>
    private static uint B(int op, int rs, int rt, int from, int to) => I(op, rs, rt, to - (from + 1));
    private static int Lo(uint address) => (short)(address & 0xFFFF);
    private static int Hi(uint address) => UniversalMips.Hi(address);

    /// <summary>The three stubs (frame, far face, near face), in that order, for a reserve of <paramref name="reserve"/> bytes.</summary>
    internal static uint[] Stubs(int reserve)
    {
        uint k = QueueConstant;
        uint[] frame =
        [
            Lui(T0, Hi(LevelId)), Lw(T0, Lo(LevelId), T0),                  // 0  t0 = level id
            Lui(T3, Hi(DataCave)), Addiu(T0, T0, -10),                      // 2  t0 = table index
            I(11, T0, T1, LevelSlots),                                      // 4  sltiu t1, t0, 64
            B(4, T1, 0, 5, 19), Addu(T1, T0, T3),                           // 5  outside: retail
            Lbu(T1, Lo(NearTable), T1), Lui(T2, Hi(NearDistanceWord)),       // 7  t1 = near distance / 256 (0: not built)
            B(4, T1, 0, 9, 19), Sll(T0, T1, 12),                            // 9  not built: retail; t0 = game units
            Sw(T0, Lo(NearDistanceWord), T2),                               // 11 the level's near distance
            Lui(T1, Hi(BufferEnd)), Lw(T1, Lo(BufferEnd), T1),              // 12 t1 = the buffer's end
            Lui(T2, Hi(k)), Addiu(T1, T1, -reserve),                        // 14 t1 = limit
            Addiu(T2, T2, Lo(k)),                                           // 16
            B(4, 0, 0, 17, 24), Addu(T2, T2, T1),                           // 17 store; t2 = limit + queue constant
            Lui(T2, Hi(NearDistanceWord)), Ori(T0, 0, 0x8000),              // 19 retail: near distance 2,048 (as set at 0x80012CC4)
            Sw(T0, Lo(NearDistanceWord), T2),                               // 21
            Addu(T1, 0, 0), Addu(T2, 0, 0),                                 // 22 no limit
            Sw(T1, Lo(Limit), T3), Sw(T2, Lo(QueueLimit), T3),              // 24 store:
            Lui(At, 0x8007), J(TerrainEntry + 8), Addiu(At, At, 0x7DD8),    // 26 the two replaced words, back
        ];
        uint[] far =
        [
            Lui(T1, Hi(Limit)), Lw(T1, Lo(Limit), T1), 0,                   // 0  t1 = limit
            B(4, T1, 0, 3, 6), R(Fp, T1, T1, 0, 0x2B),                      // 3  no limit: go; sltu t1, fp, t1
            B(4, T1, 0, 5, 9),                                              // 5  past it: end the far pass
            Srl(T1, T5, 23), J(FarFace + 8), I(12, T1, T1, 0x1F8),          // 6  go: the two replaced words, back
            J(FarPassEnd), 0,                                               // 9  full
        ];
        uint[] near =
        [
            Lui(T1, Hi(QueueLimit)), Lw(T1, Lo(QueueLimit), T1),            // 0  t1 = limit + queue constant
            Sll(T2, Gp, 4),                                                 // 2
            B(4, T1, 0, 3, 13), Sll(T3, Gp, 6),                             // 3  no limit: go
            Addu(T2, T2, T3), Sll(T3, Sp, 6), Addu(T2, T2, T3),             // 5  80 gp + 64 sp
            Sll(T3, Sp, 8), Addu(T2, T2, T3), Addu(T2, T2, Fp),             // 8  + 256 sp + next packet
            Subu(T2, T2, T1),                                               // 11 - (limit + queue constant)
            I(1, T2, 1, 16 - (12 + 1)),                                     // 12 bgez t2, full
            Srl(V0, T6, 14), J(NearFace + 8), 0,                            // 13 go: the replaced word, back
            J(NearPassEnd), 0,                                              // 16 full: subdivide what is queued
        ];
        if (frame.Length != FrameWords || far.Length != FarWords || near.Length != NearWords) throw new InvalidOperationException("Draw-buffer guard layout.");
        if (4 * (FrameWords + FarWords + NearWords) > CaveEnd - Cave || 8 + LevelSlots > DataCaveEnd - DataCave) throw new InvalidOperationException("Draw-buffer guard size.");
        return [.. frame, .. far, .. near];
    }

    private static int Off(uint address) => (int)(address - UniversalExeBindings.ExeBase);
    private static uint Word(byte[] exe, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(exe.AsSpan(Off(address)));
    private static void Put(byte[] exe, uint address, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(exe.AsSpan(Off(address)), value);
}
