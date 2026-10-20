using System.Buffers.Binary;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.Core.Exporting;

/// <summary>Spyro 3's hover in the USA game (Game Settings, the whole game): Triangle while gliding makes Spyro rise a
/// little, carrying on forward at half his glide speed, then fall; he cannot glide again until he lands, as in Spyro 2
/// and 3.
/// <list type="bullet">
/// <item>The game already turns Triangle in a glide (movement states 15 and 23, after 17 frames) into state 16, the
/// straight drop. State 16's entry code (in the state setter, 0x8003EEF0) copied the glide's sinking speed into the
/// vertical speed and zeroed the forward speed; it now gives an upward speed (<see cref="RiseSpeed"/>) and keeps half the
/// forward speed when the previous state was a glide. A drop out of the flight states (32-34: flight levels and Fly
/// Anywhere) is left as it was. The rest of its entry is state 6's (0x8003EEB4, the same steps), so it fits in place.</item>
/// <item>State 16 lets ✕ start a new glide (0x80042D1C tests ✕ in the new presses); that test is masked out, or
/// hover, glide and hover again would climb forever.</item>
/// <item>Spyro keeps the drop's own animation (state 16's, from the shared state -> animation table, which other
/// features check unchanged). Movement facts: vertical speed 0x80078B54 (height changes by speed/32 per game tick),
/// gravity -192 per tick in state 16, forward speed 0x80078B50.</item>
/// </list>
/// No code cave is used. <see cref="ApplyForExport"/> installs it in the game Create BIN makes when the export's scope
/// (<see cref="Use"/>, opened with the project's setting) asks for it.</summary>
public static class NativeHover
{
    /// <summary>The upward speed a hover starts with: about 50 editor units of rise in 0.4 seconds (measured: 3,200 gave 32).</summary>
    public const int RiseSpeed = 4096;
    public const uint EnterAddress = 0x8003EEF0, RegrabAddress = 0x80042D1C, SharedTail = 0x8003EEB4, SetGravity = 0x8003F8D4;
    private const uint ExeBase = UniversalExeBindings.ExeBase;
    private const int EnterWords = 24;

    // The USA game's state-16 entry (24 words), state 6's tail it shares, and the ✕ test in state 16.
    private const string EnterOriginal = "0880023c6c8b428c0880033cf08a638c0880013cd48a20ac0880013c4c8b20ac0880013c508b20ac0880013c548b22ac6c02601440ff02240880023c088b428c00000000200042286602401040ff022443fa000c0000000035fe000840ff0224";
    private const string TailOriginal = "0880023cf08a428c000000008402401440ff02240880023c088b428c00000000200042287e02401040ff022443fa000c0000000035fe000840ff0224";
    private const uint RegrabOriginal = 0x30420040, RegrabHover = 0x30420000; // andi v0,v0,0x40 -> andi v0,v0,0

    private const int Zero = 0, At = 1, V0 = 2, T0 = 8, T1 = 9, T2 = 10, T3 = 11;
    private static uint I(int op, int rs, int rt, int imm) => UniversalMips.EncodeI(op, rs, rt, imm);
    private static uint Lui(int rt, int imm) => I(15, 0, rt, imm);
    private static uint Lw(int rt, int offset, int rs) => I(35, rs, rt, offset);
    private static uint Sw(int rt, int offset, int rs) => I(43, rs, rt, offset);
    private static uint Addiu(int rt, int rs, int imm) => I(9, rs, rt, imm);
    private static uint Beq(int rs, int rt, int fromSlot, int toSlot) => I(4, rs, rt, toSlot - fromSlot - 1);
    private static uint Bne(int rs, int rt, int fromSlot, int toSlot) => I(5, rs, rt, toSlot - fromSlot - 1);
    private static uint Sra(int rd, int rt, int shift) => (uint)(rt << 16 | rd << 11 | shift << 6 | 3);
    private static uint Or(int rd, int rs, int rt) => (uint)(rs << 21 | rt << 16 | rd << 11 | 0x25);

    /// <summary>The new state-16 entry. at = 0x80080000 for every field (0x80078AD0 state, 0x80078B6C the glide's vertical
    /// speed, 0x80078B50 forward, 0x80078B54 vertical, 0x80078AD4 flight sub-state, 0x80078B4C sideways).</summary>
    public static uint[] EnterCode()
    {
        uint[] w = new uint[EnterWords];
        w[0] = Lui(At, 0x8008);
        w[1] = Lw(T0, -0x7530, At);              // the state Spyro leaves
        w[2] = Lw(V0, -0x7494, At);              // drop: the glide's sinking speed
        w[3] = Lw(T3, -0x74B0, At);              // forward speed
        w[4] = Addiu(T1, T0, -15);
        w[5] = Beq(T1, Zero, 5, 9);              // from a glide (15): hover
        w[6] = Addiu(T1, T0, -23);
        w[7] = Bne(T1, Zero, 7, 11);             // not from the other glide (23): the drop as it was
        w[8] = Or(T2, Zero, Zero);               //   (its forward speed is 0)
        w[9] = Addiu(V0, Zero, RiseSpeed);       // hover: rise ...
        w[10] = Sra(T2, T3, 1);                  //   ... carrying on at half the forward speed
        w[11] = Sw(V0, -0x74AC, At);             // vertical speed
        w[12] = Sw(T2, -0x74B0, At);             // forward speed
        w[13] = Sw(Zero, -0x752C, At);
        w[14] = UniversalMips.EncodeJ(2, SharedTail); // state 6's steps (wing sound, gravity -192, the common tail)
        w[15] = Sw(Zero, -0x74B4, At);
        return w;                                // 16..23: never reached (nop)
    }

    /// <summary>The changes: address, the USA game's bytes there, Hover's bytes.</summary>
    public static IReadOnlyList<(uint Address, byte[] Before, byte[] After)> Patches()
    {
        byte[] enter = new byte[EnterWords * 4];
        var code = EnterCode();
        for (int i = 0; i < code.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(enter.AsSpan(4 * i), code[i]);
        return
        [
            (EnterAddress, Convert.FromHexString(EnterOriginal), enter),
            (RegrabAddress, Word(RegrabOriginal), Word(RegrabHover)),
        ];
    }

    public static bool IsInstalled(byte[] exe) =>
        Patches().All(p => exe.Length >= Off(p.Address) + p.After.Length && exe.AsSpan(Off(p.Address), p.After.Length).SequenceEqual(p.After));

    /// <summary>Installs Hover (again: no change). Refuses an executable whose code differs from the USA game's at the
    /// patched places or at the state-6 steps the new entry continues into.</summary>
    public static void Install(byte[] exe)
    {
        if (exe.Length != 0x66000) throw new InvalidDataException("Hover needs the complete USA executable.");
        if (!exe.AsSpan(Off(SharedTail), TailOriginal.Length / 2).SequenceEqual(Convert.FromHexString(TailOriginal)))
            throw new InvalidDataException("The executable's falling code differs from the USA game's, so Hover can't be added.");
        var patches = Patches();
        foreach (var (address, before, after) in patches)
        {
            var current = exe.AsSpan(Off(address), before.Length);
            if (!current.SequenceEqual(before) && !current.SequenceEqual(after))
                throw new InvalidDataException($"The executable differs from the USA game where Hover goes (0x{address:X8}): another change uses that code.");
        }
        foreach (var (address, _, after) in patches) after.CopyTo(exe, Off(address));
    }

    // ---- Create BIN's choice ----
    private static Scope? s_scope;
    private static readonly object ScopeLock = new();

    /// <summary>Whether the game Create BIN is making gets Hover.</summary>
    public static bool Enabled => Volatile.Read(ref s_scope)?.On == true;

    /// <summary>Create BIN's choice until the scope is disposed (scopes nest).</summary>
    public static Scope Use(bool on)
    {
        lock (ScopeLock)
        {
            var scope = new Scope(on, s_scope);
            Volatile.Write(ref s_scope, scope);
            return scope;
        }
    }

    /// <summary>The export's last executable step: installs Hover when the export asks for it.</summary>
    public static void ApplyForExport(byte[] exe)
    {
        if (Enabled) Install(exe);
    }

    public sealed class Scope : IDisposable
    {
        private readonly Scope? _previous;
        private bool _disposed;
        internal Scope(bool on, Scope? previous) { On = on; _previous = previous; }
        public bool On { get; }
        public void Dispose()
        {
            lock (ScopeLock)
            {
                if (_disposed) return;
                _disposed = true;
                if (ReferenceEquals(Volatile.Read(ref s_scope), this)) Volatile.Write(ref s_scope, _previous);
            }
        }
    }

    private static int Off(uint address) => checked((int)(address - ExeBase));
    private static byte[] Word(uint w) { byte[] b = new byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(b, w); return b; }
}
