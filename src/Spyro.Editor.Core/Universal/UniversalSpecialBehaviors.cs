namespace Spyro.Editor.Core.Universal;

/// <summary>Native-engine behavior routines for recovered objects, assembled into the destination bundle.</summary>
public static partial class UniversalSpecialBehaviors
{
    // Engine addresses (USA SCUS_942.28).
    private const uint ActiveList = 0x800700F4;
    private const uint SpyroPosition = 0x80078A58;
    private const uint ParticleSpawnGlobal = 0x800758E4;
    private const uint MarkCollected = 0x8003B854;   // (gemValue, moby): adds gems and sets the durable collected bit
    private const uint FreeMoby = 0x80052568;
    private const int SparkleParticle = 0x4D;
    private const int PickupRadius = 0x280, PickupHeight = 0x380;

    private sealed class Asm
    {
        public readonly UniversalAsmPart Part = new();
        public void I(int op, int rs, int rt, int imm) => Part.Emit(UniversalMips.EncodeI(op, rs, rt, imm));
        public void R(int rs, int rt, int rd, int sh, int fn) => Part.Emit(((uint)rs << 21) | ((uint)rt << 16) | ((uint)rd << 11) | ((uint)sh << 6) | (uint)fn);
        public void Br(int op, int rs, int rt, string label) => Part.Emit(UniversalMips.EncodeI(op, rs, rt, 0), new(UniversalFixKind.BranchLabel, Label: label));
        public void J(string label) => Part.Emit(UniversalMips.EncodeJ(2, 0), new(UniversalFixKind.JumpLabel, Label: label));
        public void Jal(uint target) => Part.Emit(UniversalMips.EncodeJ(3, target));
        public void Nop() => Part.Emit(0);
        public void L(string name) => Part.Label(name);
        public void La(int reg, uint addr) { I(15, 0, reg, UniversalMips.Hi(addr)); I(9, reg, reg, UniversalMips.Lo(addr)); }
    }

    private const int Zero = 0, At = 1, V0 = 2, V1 = 3, A0 = 4, A1 = 5, A2 = 6, A3 = 7, T0 = 8, T1 = 9, T2 = 10, T3 = 11,
        T4 = 12, T5 = 13, T6 = 14, S0 = 16, S1 = 17, Sp = 29, Ra = 31;

    private const uint SpawnText = 0x800181AC;     // (text, position*, step*, width, style): native 3D HUD letters
    private const uint FlushHudText = 0x80018880;  // moves spawned letters into this frame's HUD list
    public const int SignLineBytes = 28, SignLines = 3, SignRadius = 0x700, SignHeight = 0x900;
    private const int SignTextStyle = 0xB, SignTextTop = 0x38;

    // Render hook. The engine rebuilds its visible-object list (0x800720F4) after the object updates,
    // so text spawned during an update is discarded. The gameplay renderer's call to the gem HUD
    // (0x8001EFE4: jal 0x80019300) is redirected to a small routine placed in an EXE function that
    // nothing in the game calls (0x8003A79C). It calls the gem HUD, then - only when the current
    // level's bound update function is preceded by SignHookMarker - the level's own sign drawing stub
    // at update-8. Levels without signs are unaffected.
    public const uint SignHookCallSite = 0x8001EFE4, SignHookCallPreimage = 0x0C0064C0; // jal 0x80019300
    public const uint SignHookHost = 0x8003A79C;
    public const int SignHookHostBytes = 45 * 4;
    public const uint SignHookMarker = 0x5349474E; // "NGIS"
    private const uint GemHud = 0x80019300, UpdateGlobal = 0x80075734;
    private const uint VisibleList = 0x800720F4;
    private const int VisibleListLimit = 150; // 256-entry list: leave room for 3x24 letters and the native HUD

    public static uint[] SignHookRoutine()
    {
        var a = new Asm();
        a.I(9, Sp, Sp, -0x18); a.I(43, Sp, Ra, 0x10);
        a.Jal(GemHud); a.Nop();
        a.I(15, 0, A0, (int)(UpdateGlobal >> 16)); a.I(35, A0, V0, (int)(UpdateGlobal & 0xFFFF)); a.Nop();
        a.R(0, V0, T0, 21, 0x02); a.I(9, Zero, T1, 0x400);          // srl t0, v0, 21: must be a KSEG0 RAM address
        a.Br(5, T0, T1, "done"); a.Nop();
        a.I(35, V0, V1, -12); a.I(15, 0, At, (int)(SignHookMarker >> 16)); a.I(13, At, At, (int)(SignHookMarker & 0xFFFF));
        a.Br(5, V1, At, "done"); a.I(9, V0, V0, -8);
        a.R(V0, 0, Ra, 0, 0x09); a.Nop();                            // jalr v0
        a.L("done");
        a.I(35, Sp, Ra, 0x10); a.Nop();
        a.Part.Emit(UniversalMips.JrRa); a.I(9, Sp, Sp, 0x18);
        var (code, _) = UniversalBehaviorBundleBuilder.Assemble([a.Part], SignHookHost);
        var words = new uint[code.Length / 4];
        for (int i = 0; i < words.Length; i++) words[i] = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(code.AsSpan(4 * i));
        if (words.Length * 4 > SignHookHostBytes) throw new InvalidOperationException("Sign hook does not fit its host.");
        return words;
    }

    // EXE cave: three consecutive retail functions that no EXE or overlay code references
    // (0x8003A79C, 0x8003A850, 0x8003A920). Sign hook first, dragonfly egg inventory row after it.
    public const uint CaveStart = 0x8003A79C, CaveEnd = 0x8003A9EC;
    // Dragonfly egg inventory column: header code in a larger unreferenced EXE function, its data (the
    // egg list) in the cave after the sign hook, and the per-row code plus a compact copy of the egg
    // model in a run of unused libgpu functions (no EXE/overlay call, jump, branch or pointer reaches it).
    public const uint EggInventoryHost = 0x800673A0, EggInventoryHostEnd = 0x800673A0 + 157 * 4;
    public const uint EggInventoryData = 0x8003A7F8;
    public const uint EggModelCave = 0x8005EC04, EggModelCaveEnd = 0x8005F2A4;
    public const uint EggModelAddress = 0x8005EC08; // 8-byte aligned resident HUD model; the row code ends the area
    // The inventory draws its divider line right after the 12-slot egg row; the column header is drawn first.
    public const uint EggInventoryCallSite = 0x8001BA04, EggInventoryCallPreimage = 0x0C006113; // jal 0x8001844c
    // After the last row the inventory draws the closing separators; the first of them also closes the egg column.
    public const uint EggInventoryLastLineSite = 0x8001C4FC;
    public const uint EggInventoryRowSite = 0x8001C4D4, EggInventoryRowPreimage = 0x26B50013; // addiu s5, s5, 0x13
    public const int EggInventoryMaxSlots = 6;
    private const uint DrawLine = 0x8001844C, DrawStatText = 0x80017FE4, Sprintf = 0x80062FD4, ClearMemory = 0x80016914;
    private const uint CollectedMasks = 0x80077908;
    private const uint SlashText = 0x8007561C, NumberFormat = 0x800755BC, ModelTable = 0x80076378, WobbleTable = 0x8006CC78;
    // One size for every inventory row: names, dragonfly eggs, treasure and dragons (native digits are 0x12 apart at depth 0x1100).
    public const int RowDigitStep = 0x0E, RowDigitDepth = 0x15E0, RowSlashDepth = 0x1EE0, RowLetterStep = 0x0C, RowLetterDepth = 0x19C0;
    // Dragonfly egg column: its "/" sits here (relative to the page origin), between the names and the treasure column.
    public const int EggColumnSlash = 0xFE;
    // Spinning gold egg above the column, like the native treasure chest (class 0x1D7) and dragon (0x1FA) headers.
    public const int EggHeaderClass = 0x1D2, EggHeaderY = 0x6E, EggHeaderDepth = 0xC00;

    /// <summary>Header code (the gold egg above the column) and the egg list.</summary>
    public static (uint[] Code, uint[] Data) DragonflyEggInventoryRoutine(IReadOnlyList<(int LevelIndex, int Row, int Colour)> eggs)
    {
        var result = BuildEggInventory(eggs);
        return (result.Header, result.Data);
    }

    private static uint[] Words(byte[] code) =>
        Enumerable.Range(0, code.Length / 4).Select(i => System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(code.AsSpan(4 * i))).ToArray();

    private static (uint[] Header, uint[] Row, uint[] Data, Dictionary<string, uint> Labels) BuildEggInventory(IReadOnlyList<(int LevelIndex, int Row, int Colour)> eggs)
    {
        if (eggs.Count == 0) throw new ArgumentException("No dragonfly eggs.");
        List<uint> data = [];
        foreach (var (lvl, row, colour) in eggs)
        {
            if (lvl is < 0 or >= 36 || row is < 0 or > 0xFF || colour < 0 || colour >= UniversalDragonflyEggs.Variants.Length)
                throw new InvalidDataException("Dragonfly egg level/row/colour out of range.");
            data.Add((uint)(colour << 24 | lvl << 16 | row));
        }
        const int S2 = 18, S5 = 21, S3 = 19;

        // Header: install the resident HUD egg model in its class slot when the slot is empty (level loads
        // clear it; a level that owns the slot keeps it and shows no header), spawn the gold spinning egg,
        // then draw the divider line the call site originally drew.
        var h = new Asm();
        h.I(9, Sp, Sp, -0x28); h.I(43, Sp, Ra, 0x24);
        h.I(43, Sp, A0, 0x10); h.I(43, Sp, A1, 0x14); h.I(43, Sp, A2, 0x18); h.I(43, Sp, A3, 0x1C);
        h.La(T2, (uint)(ModelTable + 4 * EggHeaderClass)); h.I(35, T2, T0, 0); h.La(T1, EggModelAddress);
        h.Br(5, T0, Zero, "check"); h.Nop();
        h.I(43, T2, T1, 0); h.R(T1, Zero, T0, 0, 0x21);
        h.L("check");
        h.Br(5, T0, T1, "line"); h.Nop();
        h.L("spawn");
        h.I(15, Zero, T0, 0x8007); h.I(35, T0, A0, 0x5710); h.Nop(); h.I(9, A0, A0, -0x58); h.I(43, T0, A0, 0x5710);
        h.R(Zero, Zero, A1, 0, 0x21); h.Jal(ClearMemory); h.I(9, Zero, A2, 0x58);
        h.I(15, Zero, T0, 0x8007); h.I(35, T0, A0, 0x5710); h.I(35, T0, V0, 0x57CC); h.I(9, Zero, V1, EggHeaderClass); h.I(41, A0, V1, 0x36);
        h.I(9, V0, V0, EggColumnSlash + 2); h.I(43, A0, V0, 0x0C); h.I(9, Zero, V0, EggHeaderY); h.I(43, A0, V0, 0x10);
        h.I(9, Zero, V0, EggHeaderDepth); h.I(43, A0, V0, 0x14);
        h.I(35, T0, V0, 0x7FE4); h.Nop(); h.I(40, A0, V0, 0x46);               // spin with the treasure chest
        h.R(Zero, V0, V0, 1, 0); h.La(At, WobbleTable); h.R(At, V0, At, 0, 0x21); h.I(37, At, V0, 0); h.Nop();
        h.R(Zero, V0, V0, 16, 0); h.R(Zero, V0, V0, 25, 3); h.I(40, A0, V0, 0x45); // and wobble like it
        h.I(9, Zero, V0, 0x7F); h.I(40, A0, V0, 0x47); h.I(9, Zero, V0, 0x0B); h.I(40, A0, V0, 0x4F);
        h.I(9, Zero, V0, 0xFF); h.I(40, A0, V0, 0x50);
        h.L("line");
        h.I(35, Sp, A0, 0x10); h.I(35, Sp, A1, 0x14); h.I(35, Sp, A2, 0x18); h.I(35, Sp, A3, 0x1C);
        h.I(35, Sp, Ra, 0x24); h.Nop(); h.Part.Emit(UniversalMips.EncodeJ(2, DrawLine)); h.I(9, Sp, Sp, 0x28);
        // Closing separator under the last row: the egg column's segment, then the native call.
        h.L("lastline");
        h.I(9, Sp, Sp, -0x28); h.I(43, Sp, Ra, 0x24);
        h.I(43, Sp, A0, 0x10); h.I(43, Sp, A1, 0x14); h.I(43, Sp, A2, 0x18); h.I(43, Sp, A3, 0x1C);
        h.I(15, Zero, T0, 0x8007); h.I(35, T0, T0, 0x57CC); h.Nop();
        h.I(9, T0, A0, EggColumnSlash - 32); h.Jal(DrawLine); h.I(9, T0, A2, EggColumnSlash + 32);
        h.J("line"); h.Nop();
        var (headerCode, headerLabels) = UniversalBehaviorBundleBuilder.Assemble([h.Part], EggInventoryHost);
        if (EggInventoryHost + headerCode.Length > UniversalEggProgress.BonusAddress)
            throw new InvalidDataException($"Egg inventory header needs {headerCode.Length} bytes; its code owner has {UniversalEggProgress.BonusAddress - EggInventoryHost}.");

        // Row: count K/Y for this level in the shared row size, right-aligned on the column's "/", or a dash
        // when the level has no dragonfly eggs; then the row separator. Replaces the row increment at 0x8001C4D4.
        var r = new Asm();
        r.L("level");
        r.I(15, Zero, T0, 0x8007); r.I(35, T0, A0, 0x5744); r.I(35, T0, T2, 0x57CC);
        r.R(Zero, A0, T1, 1, 0); r.R(T1, A0, A0, 0, 0x21); r.R(Zero, A0, A0, 1, 0);
        r.R(A0, S3, A0, 0, 0x21); r.I(9, A0, A0, -1);
        r.I(9, Sp, Sp, -0x60); r.I(43, Sp, Ra, 0x5C); r.I(43, Sp, S0, 0x50); r.I(43, Sp, S1, 0x54); r.I(43, Sp, S2, 0x58);
        r.I(9, T2, S1, EggColumnSlash); r.R(S5, Zero, S2, 0, 0x21);
        r.Jal(UniversalEggProgress.CountAddress); r.Nop();
        r.I(43, Sp, S2, 0x2C);
        r.Br(5, V1, Zero, "have"); r.R(V1, Zero, S0, 0, 0x21);
        void Text(int x, int depth, Action<Asm> text)
        {
            r.I(9, x == 0 ? S1 : S1, T0, x); r.I(43, Sp, T0, 0x28); r.I(43, Sp, S2, 0x2C); r.I(9, Zero, T0, depth); r.I(43, Sp, T0, 0x30);
            text(r); r.I(9, Sp, A1, 0x28); r.I(9, Zero, A2, RowDigitStep); r.Jal(DrawStatText); r.I(9, Zero, A3, 2);
        }
        Text(0, RowSlashDepth, q => q.La(A0, SlashText));
        r.I(15, Zero, T0, 0x8007); r.I(35, T0, T0, 0x5710); r.I(9, Zero, T1, 0x29);  // the native "no dragons" dash
        r.J("separator"); r.I(40, T0, T1, 0x44);
        r.L("have");
        r.R(V0, Zero, A2, 0, 0x21); r.I(9, Sp, A0, 0x40); r.La(A1, NumberFormat);
        r.I(10, V0, T1, 10); r.Jal(Sprintf); r.I(43, Sp, T1, 0x34);
        r.I(35, Sp, T1, 0x34); r.I(9, S1, T0, -2 * RowDigitStep);
        r.Br(4, T1, Zero, "wide"); r.Nop(); r.I(9, S1, T0, -RowDigitStep);
        r.L("wide");
        r.I(43, Sp, T0, 0x28); r.I(43, Sp, S2, 0x2C); r.I(9, Zero, T0, RowDigitDepth); r.I(43, Sp, T0, 0x30);
        r.I(9, Sp, A0, 0x40); r.I(9, Sp, A1, 0x28); r.I(9, Zero, A2, RowDigitStep); r.Jal(DrawStatText); r.I(9, Zero, A3, 2);
        Text(0, RowSlashDepth, q => q.La(A0, SlashText));
        r.I(9, Sp, A0, 0x40); r.La(A1, NumberFormat); r.Jal(Sprintf); r.R(S0, Zero, A2, 0, 0x21);
        Text(RowDigitStep, RowDigitDepth, q => q.I(9, Sp, A0, 0x40));
        r.L("separator");
        r.I(9, S1, A0, -32); r.I(9, S2, A1, -11); r.I(9, S1, A2, 32); r.Jal(DrawLine); r.I(9, S2, A3, -11);
        r.I(35, Sp, Ra, 0x5C); r.I(35, Sp, S0, 0x50); r.I(35, Sp, S1, 0x54); r.I(35, Sp, S2, 0x58);
        r.I(9, S5, S5, 0x13); r.Part.Emit(UniversalMips.JrRa); r.I(9, Sp, Sp, 0x60);
        int rowBytes = UniversalBehaviorBundleBuilder.Assemble([r.Part], EggModelCaveEnd).Code.Length;
        uint rowAddress = EggModelCaveEnd - (uint)rowBytes;
        var (rowCode, rowLabels) = UniversalBehaviorBundleBuilder.Assemble([r.Part], rowAddress);
        if (EggInventoryData + data.Count * 4 > CaveEnd)
            throw new InvalidDataException($"This project has {eggs.Count} dragonfly eggs, but the game can track at most {MaxDragonflyEggs}.");
        var labels = new Dictionary<string, uint>(headerLabels);
        foreach (var (k, v) in rowLabels) labels[k] = v;
        return (Words(headerCode), Words(rowCode), [.. data], labels);
    }

    public static int MaxDragonflyEggs => (int)((CaveEnd - EggInventoryData) / 4) - UniversalDragonflyEggs.Variants.Length - 2;

    /// <summary>Space for the HUD egg model: from <see cref="EggModelAddress"/> to the row code.</summary>
    public static int InventoryEggModelCapacity(IReadOnlyList<(int LevelIndex, int Row, int Colour)> eggs) =>
        (int)(EggModelCaveEnd - EggModelAddress) - 4 * BuildEggInventory(eggs).Row.Length;

    /// <summary>Installs the sign render hook and/or the dragonfly egg inventory column. The EXE caves, the egg
    /// column's host functions and all call sites must be the retail bytes. <paramref name="inventoryEggModel"/> is
    /// <see cref="UniversalJapaneseSource.InventoryEggModel"/>; without it the column has no egg header.</summary>
    public static void PatchExeCave(byte[] exe, ReadOnlySpan<byte> originalExe, bool signs, IReadOnlyList<(int LevelIndex, int Row, int Colour)> eggs,
        byte[]? inventoryEggModel = null)
    {
        if (!signs && eggs.Count == 0) return;
        byte[] original = originalExe.ToArray();
        void Retail(uint start, uint end, string what)
        {
            int o = (int)(start - UniversalExeBindings.ExeBase), n = (int)(end - start);
            if (!exe.AsSpan(o, n).SequenceEqual(original.AsSpan(o, n)))
                throw new InvalidDataException($"The executable's unused {what} is not the native USA code; signs and the dragonfly egg row cannot be installed.");
        }
        Retail(CaveStart, CaveEnd, "code area");
        void Site(uint address, uint preimage, uint target)
        {
            int o = (int)(address - UniversalExeBindings.ExeBase);
            if (System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(exe.AsSpan(o)) != preimage)
                throw new InvalidDataException($"The executable call at 0x{address:X8} is not the native USA code.");
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(exe.AsSpan(o), UniversalMips.EncodeJ(3, target));
        }
        void Write(uint address, uint[] words)
        {
            int o = (int)(address - UniversalExeBindings.ExeBase);
            for (int i = 0; i < words.Length; i++) System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(exe.AsSpan(o + 4 * i), words[i]);
        }
        if (signs)
        {
            uint[] hook = SignHookRoutine();
            if (SignHookHost + hook.Length * 4 > EggInventoryData) throw new InvalidOperationException("Sign hook overlaps the egg data.");
            Write(SignHookHost, hook);
            Site(SignHookCallSite, SignHookCallPreimage, SignHookHost);
        }
        if (eggs.Count > 0)
        {
            if (eggs.Count > MaxDragonflyEggs)
                throw new InvalidDataException($"This project has {eggs.Count} dragonfly eggs, but the game can track at most {MaxDragonflyEggs}. Remove {eggs.Count - MaxDragonflyEggs} and Create BIN again.");
            Retail(EggInventoryHost, EggInventoryHostEnd, "egg row host function");
            Retail(EggModelCave, EggModelCaveEnd, "egg column model area");
            var (header, row, data, labels) = BuildEggInventory(eggs);
            Write(EggInventoryHost, header);
            Write(EggInventoryData, data);
            Write(EggModelCaveEnd - (uint)row.Length * 4, row);
            if (inventoryEggModel != null)
            {
                byte[] model = UniversalDragonflyEggs.HudModel(inventoryEggModel, EggModelAddress);
                if (EggModelAddress + model.Length > EggModelCaveEnd - row.Length * 4) throw new InvalidDataException("The inventory egg model does not fit its executable area.");
                Buffer.BlockCopy(model, 0, exe, (int)(EggModelAddress - UniversalExeBindings.ExeBase), model.Length);
                Site(EggInventoryCallSite, EggInventoryCallPreimage, EggInventoryHost);
            }
            Site(EggInventoryRowSite, EggInventoryRowPreimage, labels["level"]);
            Site(EggInventoryLastLineSite, EggInventoryCallPreimage, labels["lastline"]);
            // Source-guarded layout: every row uses one text size so names, dragonfly eggs, treasure and dragons
            // line up; the treasure and dragon "/" and the column headers keep their native positions.
            void Word(uint address, uint before, uint after)
            {
                int o = (int)(address - UniversalExeBindings.ExeBase);
                if (System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(exe.AsSpan(o)) != before)
                    throw new InvalidDataException($"The inventory layout at 0x{address:X8} differs from the verified USA version.");
                Write(address, [after]);
            }
            uint Imm(uint w, int v) => (w & 0xFFFF0000) | (ushort)v;
            foreach (var (address, before, value) in InventoryLayout)
                Word(address, before, Imm(before, value));
            Word(0x8001BBC8, 0x00021880, 0x000218C0); // title centring: (n-1)*10 -> (n-1)*9 (sll 3, addu, nop)
            Word(0x8001BBD0, 0x00031840, 0x00000000);
            UniversalEggProgress.Patch(exe, original, eggs.Count, header.Length * 4);
        }
    }

    /// <summary>Inventory row layout immediates (address, retail word, new immediate).</summary>
    public static readonly (uint Address, uint Before, int Value)[] InventoryLayout =
    [
        // Page header: the divider moves up two pixels (still below the stolen-egg row) and the world title is
        // drawn 10% smaller and higher, so long titles clear the column icons.
        (0x8001B9FC, 0x24050045, 0x43), (0x8001BA08, 0x24070045, 0x43),
        (0x8001BA5C, 0x24020046, 0x44), (0x8001BA6C, 0x24020047, 0x45),
        (0x8001BA90, 0x24050047, 0x45), (0x8001BAA8, 0x24070047, 0x45),
        (0x8001BAF4, 0x24020048, 0x46),                     // page arrows beside the divider
        (0x8001BBA0, 0x2403005C, 0x53),                     // world title height
        (0x8001BBA8, 0x24030E00, 0xF80), (0x8001BBB0, 0x24030014, 0x12), (0x8001BBBC, 0x24031000, 0x11C0),
        (0x8001BC0C, 0x24070017, 0x15),
        (0x8001BF3C, 0x24091100, RowDigitDepth),            // row depth: name capitals and treasure count
        (0x8001BF48, 0x24020010, RowLetterStep),            // name letter spacing
        (0x8001BF58, 0x24021400, RowLetterDepth),           // name letter depth
        (0x8001BFAC, 0x24070012, RowDigitStep),             // name capital advance
        (0x8001BFB0, 0x26E40113, 0x15A - 4 * RowDigitStep), // treasure count field (4 digits right-aligned on "/")
        (0x8001BFFC, 0x24820012, RowDigitStep), (0x8001C020, 0x24420012, RowDigitStep), (0x8001C044, 0x24420012, RowDigitStep),
        (0x8001C068, 0x24060012, RowDigitStep),
        (0x8001C088, 0x24091800, RowSlashDepth),
        (0x8001C0A8, 0x24091100, RowDigitDepth),
        (0x8001C0BC, 0x26E5016B, 0x15A + RowDigitStep),     // treasure total
        (0x8001C104, 0x24060012, RowDigitStep),
        (0x8001C150, 0x244201BD, 0x1CE - RowDigitStep),     // dragons count
        (0x8001C180, 0x24060012, RowDigitStep),
        (0x8001C1A8, 0x24091800, RowSlashDepth),
        (0x8001C1BC, 0x24091100, RowDigitDepth),
        (0x8001C1DC, 0x244201DF, 0x1CE + RowDigitStep),     // dragons total
        (0x8001C214, 0x24060012, RowDigitStep),
        (0x8001C240, 0x24091800, RowSlashDepth),            // dash when a level has no dragons
        // Stolen-egg icons (first three worlds): smaller, between the names and the dragonfly egg column.
        (0x8001C378, 0x244200E8, 0xB8),
        (0x8001C3A4, 0x24880018, 0x15),
        (0x8001C3B0, 0x26A2FFF8, -7),
        (0x8001C3B8, 0x24020018, 0x14),
        (0x8001C3C0, 0x24020010, 0x0E),
        (0x8001C418, 0x2442000C, 0x0A),
        (0x8001C41C, 0x24091800, RowSlashDepth),
        (0x8001C494, 0x26310018, 0x15),
    ];

    /// <summary>Marker + stub placed immediately before the bundle's update wrapper.</summary>
    internal static UniversalAsmPart SignHookPreamble(bool hasSigns)
    {
        var p = new UniversalAsmPart();
        p.Emit(hasSigns ? SignHookMarker : 0); // only levels with signs carry the marker
        if (hasSigns) p.Emit(UniversalMips.EncodeJ(2, 0), new(UniversalFixKind.JumpLabel, Label: "sign_draw"));
        else p.Emit(UniversalMips.JrRa);
        p.Emit(UniversalMips.Nop);
        return p;
    }

    /// <summary>Render-time sign drawing: while Spyro is near a sign, show its own three message lines
    /// (stored in its private properties block) with the engine's native HUD letters.</summary>
    internal static UniversalAsmPart SignDraw(IReadOnlyCollection<int> signClasses, string tag)
    {
        if (signClasses.Count != 1) throw new InvalidOperationException("One sign class is supported.");
        int cls = signClasses.First();
        const int S2 = 18, S3 = 19;
        var a = new Asm();
        a.L(tag);
        a.I(9, Sp, Sp, -0x48); a.I(43, Sp, Ra, 0x40); a.I(43, Sp, S0, 0x34); a.I(43, Sp, S1, 0x38); a.I(43, Sp, S2, 0x3C); a.I(43, Sp, S3, 0x44);
        // Leave the visible list alone when it is already busy.
        a.La(T0, VisibleList); a.I(9, Zero, T1, 0);
        a.L(tag + "_count");
        a.I(35, T0, T2, 0); a.Nop();
        a.Br(4, T2, Zero, tag + "_counted"); a.I(9, T0, T0, 4);
        a.Br(4, Zero, Zero, tag + "_count"); a.I(9, T1, T1, 1);
        a.L(tag + "_counted");
        a.I(10, T1, V0, VisibleListLimit);
        a.Br(4, V0, Zero, tag + "_done"); a.Nop();
        a.La(S0, ActiveList);
        a.L(tag + "_loop");
        a.I(35, S0, S1, 0); a.Nop();
        a.Br(4, S1, Zero, tag + "_done"); a.I(9, S0, S0, 4);
        a.I(37, S1, V0, 0x36); a.Nop();
        a.I(9, Zero, At, cls);
        a.Br(5, V0, At, tag + "_loop"); a.Nop();
        a.I(36, S1, V0, 0x48); a.Nop(); a.I(11, V0, V0, 0x80);
        a.Br(4, V0, Zero, tag + "_loop"); a.Nop();
        a.La(T0, SpyroPosition);
        a.I(35, T0, T1, 0); a.I(35, T0, T2, 4); a.I(35, T0, T3, 8);
        a.I(35, S1, T4, 0xC); a.I(35, S1, T5, 0x10); a.I(35, S1, T6, 0x14); a.Nop();
        foreach (var (d, sx, r, lbl) in new[] { (T4, T1, SignRadius, "_x"), (T5, T2, SignRadius, "_y"), (T6, T3, SignHeight, "_z") })
        {
            a.R(d, sx, d, 0, 0x23); a.Br(1, d, 1, tag + lbl); a.Nop(); a.R(Zero, d, d, 0, 0x23);
            a.L(tag + lbl);
            a.I(11, d, V0, r); a.Br(4, V0, Zero, tag + "_loop"); a.Nop();
        }
        a.I(35, S1, S2, 0); a.Nop();                            // message = properties pointer
        a.Br(4, S2, Zero, tag + "_loop"); a.Nop();
        a.I(9, Zero, S3, 0);                                     // line = 0
        a.L(tag + "_line");
        a.I(36, S2, T1, 0); a.Nop();
        a.Br(4, T1, Zero, tag + "_next"); a.Nop();
        // strlen -> t2
        a.I(9, S2, T3, 0); a.I(9, Zero, T2, 0);
        a.L(tag + "_len");
        a.I(36, T3, T4, 0); a.Nop();
        a.Br(4, T4, Zero, tag + "_lend"); a.Nop();
        a.I(9, T3, T3, 1);
        a.Br(4, Zero, Zero, tag + "_len"); a.I(9, T2, T2, 1);
        a.L(tag + "_lend");
        // Same layout as the native "DEMO MODE" caption: position (centre - len*8, top + line*22, 0x1100), step (0x10, 1, 0x1400).
        a.R(0, T2, T2, 3, 0x00); a.I(9, Zero, V0, 0x10F); a.R(V0, T2, V0, 0, 0x23); a.I(43, Sp, V0, 0x18);
        a.R(0, S3, V0, 4, 0x00); a.R(0, S3, V1, 2, 0x00); a.R(V0, V1, V0, 0, 0x21); a.R(V0, S3, V0, 0, 0x21); a.R(V0, S3, V0, 0, 0x21); // v0 = line*22
        a.I(9, V0, V0, SignTextTop); a.I(43, Sp, V0, 0x1C);
        a.I(9, Zero, V0, 0x1100); a.I(43, Sp, V0, 0x20);
        a.I(9, Zero, V0, 0x10); a.I(43, Sp, V0, 0x28);
        a.I(9, Zero, V0, 1); a.I(43, Sp, V0, 0x2C);
        a.I(9, Zero, V0, 0x1400); a.I(43, Sp, V0, 0x30);
        a.I(9, Zero, V0, SignTextStyle); a.I(43, Sp, V0, 0x10);
        a.I(9, S2, A0, 0); a.I(9, Sp, A1, 0x18); a.I(9, Sp, A2, 0x28);
        a.Jal(SpawnText); a.I(9, Zero, A3, 0x12);
        a.L(tag + "_next");
        a.I(9, S2, S2, SignLineBytes); a.I(9, S3, S3, 1);
        a.I(10, S3, V0, SignLines);
        a.Br(5, V0, Zero, tag + "_line"); a.Nop();
        a.Jal(FlushHudText); a.Nop();
        a.L(tag + "_done");
        a.I(35, Sp, Ra, 0x40); a.I(35, Sp, S0, 0x34); a.I(35, Sp, S1, 0x38); a.I(35, Sp, S2, 0x3C); a.I(35, Sp, S3, 0x44); a.Nop();
        a.Part.Emit(UniversalMips.JrRa); a.I(9, Sp, Sp, 0x48);
        return a.Part;
    }

    /// <summary>Encodes up to three sign lines (native letters, digits, space and ! ? . ,) into a private block.</summary>
    public static byte[] EncodeSignText(IReadOnlyList<string> lines)
    {
        byte[] block = new byte[SignLineBytes * SignLines];
        for (int i = 0; i < Math.Min(SignLines, lines.Count); i++)
        {
            string line = NormalizeSignLine(lines[i]);
            for (int k = 0; k < line.Length; k++) block[i * SignLineBytes + k] = (byte)line[k];
        }
        return block;
    }

    public const int SignLineMaxChars = 24;

    public static string NormalizeSignLine(string text)
    {
        var chars = (text ?? "").ToUpperInvariant().Where(c => c is (>= 'A' and <= 'Z') or (>= '0' and <= '9') or ' ' or '!' or '?' or '.' or ',').Take(SignLineMaxChars);
        return new string(chars.ToArray()).TrimEnd();
    }

    public static byte[] DragonflyEggUpdateCode(IReadOnlyCollection<int> eggClasses, uint baseAddress, bool remapSaveRows = false) =>
        UniversalBehaviorBundleBuilder.Assemble([DragonflyEggUpdate(eggClasses, "egg_update", remapSaveRows)], baseAddress).Code;

    /// <summary>Per-frame dragonfly egg update: spin, pick up near Spyro, persist and count.</summary>
    internal static UniversalAsmPart DragonflyEggUpdate(IReadOnlyCollection<int> eggClasses, string tag, bool remapSaveRows = false)
    {
        int min = eggClasses.Min(), span = eggClasses.Max() - min + 1;
        if (span > 64) throw new InvalidOperationException("Dragonfly egg classes must span at most 64 IDs.");
        ulong mask = 0;
        foreach (int c in eggClasses) mask |= 1UL << (c - min);
        var a = new Asm();
        a.L(tag);
        a.I(9, Sp, Sp, -0x20); a.I(43, Sp, Ra, 0x18); a.I(43, Sp, S0, 0x10); a.I(43, Sp, S1, 0x14);
        a.La(S0, ActiveList);
        a.L(tag + "_loop");
        a.I(35, S0, S1, 0); a.Nop();
        a.Br(4, S1, Zero, tag + "_done"); a.I(9, S0, S0, 4);
        a.I(37, S1, V0, 0x36); a.Nop();                       // lhu v0, class
        a.I(9, V0, V0, -min);
        a.I(11, V0, V1, span);                                 // sltiu v1, v0, span
        a.Br(4, V1, Zero, tag + "_loop"); a.Nop();
        if (span > 32)
        {
            a.I(11, V0, V1, 32); a.Br(5, V1, Zero, tag + "_mask_low"); a.Nop();
            a.I(15, 0, V1, (int)(mask >> 48)); a.I(13, V1, V1, (int)((mask >> 32) & 0xFFFF));
            a.J(tag + "_mask_test"); a.I(9, V0, V0, -32);
            a.L(tag + "_mask_low");
        }
        a.I(15, 0, V1, (int)((mask >> 16) & 0xFFFF)); a.I(13, V1, V1, (int)(mask & 0xFFFF));
        a.L(tag + "_mask_test");
        a.R(V0, V1, V1, 0, 0x06);                              // srlv v1, v1, v0
        a.I(12, V1, V1, 1);
        a.Br(4, V1, Zero, tag + "_loop"); a.Nop();
        a.I(36, S1, V0, 0x48); a.Nop();                        // lbu state
        a.I(11, V0, V0, 0x80);
        a.Br(4, V0, Zero, tag + "_loop"); a.Nop();
        // The Japanese template's native reload kind is 0x7F, so the USA loader does not
        // retire it using its durable bit. Suppress it explicitly before any spin/pickup.
        // Source MarkCollected uses [g75828,g75890), stride 0x58, and level index g75964.
        a.I(15, Zero, T0, 0x8007); a.I(35, T0, T1, 0x5828); a.I(35, T0, T2, 0x5890);
        a.R(S1, T1, T3, 0, 0x2B); a.Br(5, T3, Zero, tag + "_loop"); a.Nop();
        a.R(S1, T2, T3, 0, 0x2B); a.Br(4, T3, Zero, tag + "_loop"); a.Nop();
        a.R(S1, T1, T3, 0, 0x23); a.I(9, Zero, T4, 0x58); a.R(T3, T4, 0, 0, 0x1B);
        a.R(0, 0, T3, 0, 0x12); a.R(0, 0, T4, 0, 0x10);
        a.Br(5, T4, Zero, tag + "_loop"); a.Nop();
        if (remapSaveRows)
        {
            // A reused physical object slot retains its own logical save identity.
            // The gateway preserves the live object/list registers and T0 native base.
            a.R(T3, Zero, A2, 0, 0x21);
            a.Jal(Spyro.Editor.Core.Exporting.NativeMobySaveMap.MapRowAddress); a.Nop();
            a.R(A2, Zero, T3, 0, 0x21);
        }
        a.I(11, T3, T4, 256); a.Br(4, T4, Zero, tag + "_loop"); a.Nop();
        a.I(35, T0, T1, 0x5964); a.Nop(); a.I(11, T1, T2, 36);
        a.Br(4, T2, Zero, tag + "_loop"); a.R(0, T1, T1, 5, 0);
        a.R(0, T3, T2, 5, 2); a.R(0, T2, T2, 2, 0); a.R(T1, T2, T1, 0, 0x21);
        a.La(T0, CollectedMasks); a.R(T0, T1, T0, 0, 0x21); a.I(35, T0, T1, 0);
        a.I(12, T3, T3, 31); a.R(T3, T1, T1, 0, 6); a.I(12, T1, T1, 1);
        a.Br(4, T1, Zero, tag + "_uncollected"); a.Nop();
        a.Jal(FreeMoby); a.R(S1, 0, A0, 0, 0x21);
        a.J(tag + "_loop"); a.Nop();
        a.L(tag + "_uncollected");
        a.I(36, S1, V0, 0x46); a.Nop(); a.I(9, V0, V0, 3); a.I(40, S1, V0, 0x46);   // spin
        a.La(T0, SpyroPosition);
        a.I(35, T0, T1, 0); a.I(35, T0, T2, 4); a.I(35, T0, T3, 8);
        a.I(35, S1, T4, 0xC); a.I(35, S1, T5, 0x10); a.I(35, S1, T6, 0x14); a.Nop();
        foreach (var (d, s, r, lbl) in new[] { (T4, T1, PickupRadius, "_x"), (T5, T2, PickupRadius, "_y"), (T6, T3, PickupHeight, "_z") })
        {
            a.R(d, s, d, 0, 0x23);                             // subu d, d, s
            a.Br(1, d, 1, tag + lbl);                          // bgez d (REGIMM rt=1)
            a.Nop();
            a.R(Zero, d, d, 0, 0x23);                          // d = -d
            a.L(tag + lbl);
            a.I(11, d, V0, r);
            a.Br(4, V0, Zero, tag + "_loop"); a.Nop();
        }
        // Collected: sparkles, durable collected bit, remove.
        a.La(V0, ParticleSpawnGlobal); a.I(35, V0, V0, 0); a.I(9, Zero, A0, 0x10);
        a.I(9, Zero, A1, SparkleParticle); a.I(9, S1, A2, 0xC);
        a.R(V0, 0, Ra, 0, 0x09); a.R(0, 0, A3, 0, 0x21);       // jalr v0 ; move a3, zero
        a.I(9, Zero, A0, 0); a.Jal(MarkCollected); a.R(S1, 0, A1, 0, 0x21);
        // Dragonfly eggs are their own collectible: the stolen dragon egg counters are left alone.
        // The durable bit set above is what the inventory's dragonfly egg row counts.
        a.Jal(FreeMoby); a.R(S1, 0, A0, 0, 0x21);
        a.J(tag + "_loop"); a.Nop();
        a.L(tag + "_done");
        a.I(35, Sp, Ra, 0x18); a.I(35, Sp, S0, 0x10); a.I(35, Sp, S1, 0x14); a.Nop();
        a.Part.Emit(UniversalMips.JrRa); a.I(9, Sp, Sp, 0x20);
        return a.Part;
    }
}
