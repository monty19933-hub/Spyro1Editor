using System.Buffers.Binary;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.Core.Exporting;

/// <summary>Spyro 3's hover in the USA game (Game Settings, the whole game): Triangle while gliding makes Spyro flap his
/// wings and rise a little, carrying on forward at half his glide speed, then fall; he cannot glide again until he lands,
/// as in Spyro 2 and 3.
/// <list type="bullet">
/// <item>The game already turns Triangle in a glide (movement states 15 and 23, after 17 frames) into state 16, the
/// straight drop. State 16's entry code (in the state setter, 0x8003EEF0) copied the glide's sinking speed into the
/// vertical speed and zeroed the forward speed; it now gives an upward speed (<see cref="RiseSpeed"/>) and keeps half the
/// forward speed when the previous state was a glide. A drop out of the flight states (32-34: flight levels and Fly
/// Anywhere) is left as it was. The rest of its entry is state 6's (0x8003EEB4, the same steps), so it fits in place.</item>
/// <item>The flap: the entry also writes state 16's byte of the state -> animation table (0x8006C470 + 16, in RAM only)
/// before the game picks the state's animation, so a hover plays the flight levels' climbing flap (animation
/// <see cref="FlapAnimation"/>) and a drop keeps the drop pose (17). The flap is a level's own player record: only the
/// flight levels and Gnasty's Loot carry it, so Create BIN adds it to the other levels (<see cref="NativeHoverFlap"/>),
/// and the entry checks the level has it (the player package's slot, 0 when missing) and otherwise hovers in the drop
/// pose. The table bytes in the executable stay the game's own, as the features that check them expect.</item>
/// <item>State 16's input step tested ✕ to start a new glide (0x80042D10); that test is replaced (else hover, glide and
/// hover again would climb forever) by the switch back to the drop pose: once Spyro stops rising it writes the drop pose
/// back into the table and marks his animation as the glide's, so the game blends into the drop as from a glide.</item>
/// </list>
/// No code cave is used. <see cref="ApplyForExport"/> installs it in the game Create BIN makes when the export's scope
/// (<see cref="Use"/>, opened with the project's setting) asks for it.</summary>
public static class NativeHover
{
    /// <summary>The upward speed a hover starts with: about 50 editor units of rise in 0.4 seconds (measured: 3,200 gave 32).</summary>
    public const int RiseSpeed = 4096;
    /// <summary>The flight levels' climbing flap (state 33's animation) and the drop pose (state 16's own).</summary>
    public const int FlapAnimation = 34, DropAnimation = 17;
    public const uint EnterAddress = 0x8003EEF0, FrameAddress = 0x80042D10, SharedTail = 0x8003EEB4, SetGravity = 0x8003F8D4;
    /// <summary>State 16's byte of the state -> animation table, the player package pointer, the state input step's
    /// common continuation.</summary>
    public const uint DropAnimationByte = 0x8006C480, PlayerPackage = 0x80076378, InputContinue = 0x80042204;
    private const uint ExeBase = UniversalExeBindings.ExeBase;
    private const int EnterWords = 24, FrameWords = 12;

    // The USA game's state-16 entry (24 words), state 6's tail it shares, and state 16's ✕ test (12 words).
    private const string EnterOriginal = "0880023c6c8b428c0880033cf08a638c0880013cd48a20ac0880013c4c8b20ac0880013c508b20ac0880013c548b22ac6c02601440ff02240880023c088b428c00000000200042286602401040ff022443fa000c0000000035fe000840ff0224";
    private const string TailOriginal = "0880023cf08a428c000000008402401440ff02240880023c088b428c00000000200042287e02401040ff022443fa000c0000000035fe000840ff0224";
    private const string FrameOriginal = "0780023c7873428c000000004000423038fd4010000000000880023ca88b428c00000000a20440100f0004240880023c";

    private const int Zero = 0, At = 1, V0 = 2, T0 = 8, T1 = 9, T2 = 10, T3 = 11, T4 = 12, T5 = 13;
    private static uint I(int op, int rs, int rt, int imm) => UniversalMips.EncodeI(op, rs, rt, imm);
    private static uint Lui(int rt, int imm) => I(15, 0, rt, imm);
    private static uint Lw(int rt, int offset, int rs) => I(35, rs, rt, offset);
    private static uint Lbu(int rt, int offset, int rs) => I(36, rs, rt, offset);
    private static uint Sw(int rt, int offset, int rs) => I(43, rs, rt, offset);
    private static uint Sb(int rt, int offset, int rs) => I(40, rs, rt, offset);
    private static uint Addiu(int rt, int rs, int imm) => I(9, rs, rt, imm);
    private static uint Beq(int rs, int rt, int fromSlot, int toSlot) => I(4, rs, rt, toSlot - fromSlot - 1);
    private static uint Bne(int rs, int rt, int fromSlot, int toSlot) => I(5, rs, rt, toSlot - fromSlot - 1);
    private static uint Bgtz(int rs, uint from, uint to) => I(7, rs, 0, (int)((to - from - 4) / 4));
    private static uint BeqTo(int rs, int rt, uint from, uint to) => I(4, rs, rt, (int)((to - from - 4) / 4));
    private static uint Sra(int rd, int rt, int shift) => (uint)(rt << 16 | rd << 11 | shift << 6 | 3);
    private static uint Or(int rd, int rs, int rt) => (uint)(rs << 21 | rt << 16 | rd << 11 | 0x25);
    private static int Low(uint address) => (short)(address & 0xFFFF);
    private static int High(uint address) => (int)((address + 0x8000) >> 16);

    /// <summary>The new state-16 entry. at = 0x80080000 for Spyro's fields (0x80078AD0 the state he leaves, 0x80078B6C the
    /// glide's vertical speed, 0x80078B50 forward, 0x80078B54 vertical, 0x80078AD4 flight sub-state, 0x80078B4C sideways).</summary>
    public static uint[] EnterCode()
    {
        uint[] w = new uint[EnterWords];
        w[0] = Lui(At, 0x8008);
        w[1] = Lw(T0, -0x7530, At);              // the state Spyro leaves
        w[2] = Lw(V0, -0x7494, At);              // drop: the glide's sinking speed
        w[3] = Addiu(T4, Zero, DropAnimation);   //   in the drop pose
        w[4] = Addiu(T1, T0, -15);
        w[5] = Beq(T1, Zero, 5, 9);              // from a glide (15): hover
        w[6] = Addiu(T1, T0, -23);
        w[7] = Bne(T1, Zero, 7, 17);             // not from the other glide (23): the drop as it was
        w[8] = Or(T2, Zero, Zero);               //   (its forward speed is 0)
        w[9] = Lui(T5, High(PlayerPackage));     // hover:
        w[10] = Lw(T5, Low(PlayerPackage), T5);  //   the player package
        w[11] = Lw(T3, -0x74B0, At);             //   forward speed
        w[12] = Lw(T5, 0x38 + 4 * FlapAnimation, T5); // the flap's record (0: this level lacks it)
        w[13] = Addiu(V0, Zero, RiseSpeed);      //   rise ...
        w[14] = Beq(T5, Zero, 14, 17);
        w[15] = Sra(T2, T3, 1);                  //   ... carrying on at half the forward speed
        w[16] = Addiu(T4, Zero, FlapAnimation);  //   flapping when the level has the flap
        w[17] = Sw(V0, -0x74AC, At);             // vertical speed
        w[18] = Sw(T2, -0x74B0, At);             // forward speed
        w[19] = Sw(Zero, -0x752C, At);
        w[20] = Lui(T1, High(DropAnimationByte));
        w[21] = Sb(T4, Low(DropAnimationByte), T1); // state 16's animation, picked when the game next looks
        w[22] = UniversalMips.EncodeJ(2, SharedTail); // state 6's steps (wing sound, gravity -192, the common tail)
        w[23] = Sw(Zero, -0x74B4, At);
        return w;
    }

    /// <summary>State 16's input step after its landing tests (0x80042D10, every frame of the drop): while Spyro rises it
    /// goes on as the game does; once he stops, a flap gives way to the drop pose (the table byte back to 17, his
    /// animation marked as the glide's: 0x80078AB4 = 15). It always continues where the old ✕ test did when ✕ was not
    /// pressed, so state 16 never starts a new glide.</summary>
    public static uint[] FrameCode()
    {
        uint[] w = new uint[FrameWords];
        uint At0(int slot) => FrameAddress + 4 * (uint)slot;
        w[0] = Lui(At, 0x8008);
        w[1] = Lw(T0, -0x74AC, At);              // vertical speed
        w[2] = Lui(T1, High(DropAnimationByte));
        w[3] = Bgtz(T0, At0(3), InputContinue);  // still rising
        w[4] = Lbu(T2, Low(DropAnimationByte), T1);
        w[5] = Addiu(T3, Zero, DropAnimation);
        w[6] = BeqTo(T2, T3, At0(6), InputContinue); // already the drop pose
        w[7] = 0;
        w[8] = Sb(T3, Low(DropAnimationByte), T1);
        w[9] = Addiu(T3, Zero, 15);
        w[10] = UniversalMips.EncodeJ(2, InputContinue);
        w[11] = Sw(T3, -0x754C, At);             // his animation belongs to the glide: the game blends into the drop
        return w;
    }

    /// <summary>The changes: address, the USA game's bytes there, Hover's bytes.</summary>
    public static IReadOnlyList<(uint Address, byte[] Before, byte[] After)> Patches()
    {
        return
        [
            (EnterAddress, Convert.FromHexString(EnterOriginal), Bytes(EnterCode())),
            (FrameAddress, Convert.FromHexString(FrameOriginal), Bytes(FrameCode())),
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

    /// <summary>The levels of the export in progress where Spyro hovers without flapping.</summary>
    public static IReadOnlyList<string> CurrentLevelsWithoutFlap => Volatile.Read(ref s_scope)?.LevelsWithoutFlap ?? [];

    /// <summary>Notes levels of the game being made that had no room for the flap (<see cref="NativeHoverFlap"/>).</summary>
    internal static void RecordWithoutFlap(IEnumerable<string> levels) => Volatile.Read(ref s_scope)?.Add(levels);

    public sealed class Scope : IDisposable
    {
        private readonly Scope? _previous;
        private readonly List<string> _withoutFlap = [];
        private bool _disposed;
        internal Scope(bool on, Scope? previous) { On = on; _previous = previous; }
        public bool On { get; }
        /// <summary>The levels of this export where Spyro hovers without flapping (no room for the flap).</summary>
        public IReadOnlyList<string> LevelsWithoutFlap { get { lock (_withoutFlap) return [.. _withoutFlap]; } }
        internal void Add(IEnumerable<string> levels) { lock (_withoutFlap) foreach (string l in levels) if (!_withoutFlap.Contains(l)) _withoutFlap.Add(l); }
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
    private static byte[] Bytes(uint[] words)
    {
        byte[] b = new byte[words.Length * 4];
        for (int i = 0; i < words.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(4 * i), words[i]);
        return b;
    }
}
