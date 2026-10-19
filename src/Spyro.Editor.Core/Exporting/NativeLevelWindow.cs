using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.Core.Exporting;

/// <summary>One extra level the level window runs (a world-7 level id 70-74).</summary>
/// <param name="LevelId">70-74; 70 is the world's homeworld (Return Home and Game Over in 71-74 lead to it).</param>
/// <param name="Name">Its name: capital letters, spaces and apostrophes (the game's font), 1-15 characters.</param>
/// <param name="TemplateLevelId">Whose per-level initializer (jump-table case) it borrows: a retail level id (10-64,
/// e.g. 10 for Artisans' own overlay) or <see cref="NativeLevelWindow.FreshTemplate"/> (65) for a level the editor
/// compiled, homeworld or not (Town Square's case, as ID65 uses: a compiled overlay's library callbacks sit where Town
/// Square's do).</param>
/// <param name="MusicTrack">The music-table word for the level (the song it plays).</param>
/// <param name="KillHeight">Its kill-height word (world units).</param>
/// <param name="ArrivalHeight">How far above its start Spyro appears when he arrives through a doorway or Return Home
/// (the retail arrival table's word: 8192 for normal levels).</param>
/// <param name="GemTotal">The gems it holds (its inventory total).</param>
/// <param name="DragonTotal">The dragons it holds.</param>
/// <param name="OverlayEntry">The WAD entry of its overlay (entries 102+ written by the editor).</param>
/// <param name="DataEntry">The WAD entry of its level data.</param>
/// <param name="Bindings">The initializer bindings it was compiled with (<see cref="UniversalExeBindings.Read"/> of the
/// executable it was composed against, for the id it was compiled as). Where they differ from the template's case in
/// the final executable, the window sets them after the case runs; only the copy buffer, init and update callbacks may
/// differ. Null: the template's case as it is.</param>
public sealed record WindowGuest(int LevelId, string Name, int TemplateLevelId, int MusicTrack, int KillHeight, int ArrivalHeight,
    int GemTotal, int DragonTotal, int OverlayEntry, int DataEntry, IReadOnlyDictionary<uint, uint>? Bindings = null);

/// <summary>The level window: world 7 (level ids 70-74) for the USA game. The engine has 36 level slots (index =
/// (id/10-1)*6 + id%10) and tables of 36 entries; ids 70-74 would index past all of them. Every window level runs in
/// slot 35 instead (the slot of the unused level 65, File › New Level's slot): when one loads, its own progress
/// (visited, finished, dragons, gems, 256 collected-object bits) is swapped into slot 35 and its fixed facts (WAD row,
/// music, kill height, arrival height, gem and dragon totals, initializer callbacks) are written into slot 35's table
/// words; level 65 is the window's record 0. Retail levels and their overlays are untouched.
/// <list type="bullet">
/// <item>Loader (0x8001565C, right after the per-level switch): record switch, callbacks, index = 35.</item>
/// <item>Save: each card slot grows from 0x600 to 0x680 bytes in the same one-block file; slot+0x590 holds the records
/// 1-5 ('LW', version, count, a word checksum, 5 x 40 bytes). The save file is renamed (<see cref="SaveName"/>) so a
/// retail save and a window save never mix. Serializer, deserializer and New Game keep the records in step. The fairy
/// save serializes into the end of the second packet buffer; its dialog lays its letters just below the slot, so they
/// start 0x80 lower (0x8001D754) or they would overwrite the bigger slot's first bytes while it is written.</item>
/// <item>Names: the three name readers (entering banner 0x800197D4/0x800197F4, doorway name 0x8003C3A4) take names
/// 36-40 (ids 70-74; a doorway's lettering word is <see cref="VirtualIndex"/>) from the window.</item>
/// <item>Inventory: a seventh page titled with the world's name (world-name slot 7), rows from the records (the sixth row,
/// id 75, is always empty); the page opens on the current world, so it shows in world 7. The arrows step one page as in
/// retail: page 6 -> 7 when homeworld 70 has been visited, 7 -> 6 when Gnasty's World has.</item>
/// <item>Doorways may lead to ids up to 74 (0x80057110); leaving a homeworld for another homeworld through a doorway
/// records where Spyro came from, so he arrives at the doorway back (0x80057178).</item>
/// </list>
/// Install it last, after every other executable edit (it copies slot 35's words and the template cases as they are
/// then). Its code lives in <see cref="NativeExecutableCaves.LevelWindow"/>.</summary>
public static class NativeLevelWindow
{
    public const int FirstId = 70, LastId = 74, MaxGuests = LastId - FirstId + 1, FreshTemplate = 65, WindowIndex = 35;
    public const int MaxNameLength = 15;
    /// <summary>A card slot's size with the window (retail 0x600) and the bytes the menu reads and writes (retail 0x1400).</summary>
    public const int SlotBytes = 0x680, SaveFileBytes = 0x200 + 3 * SlotBytes;
    public const string RetailSaveName = "BASCUS-94228SPYRO", SaveName = "BASCUS-94228SPYNW";
    /// <summary>The initializer globals a window level may set differently from its template's case.</summary>
    public static IReadOnlyList<uint> OverriddenGlobals { get; } = [UniversalExeBindings.CopyBufferGlobal, UniversalExeBindings.InitGlobal, UniversalExeBindings.UpdateGlobal];
    /// <summary>The number every level-name reader uses for level <paramref name="levelId"/> (a doorway's lettering word):
    /// (id/10-1)*6 + id%10, so 70-74 are 36-40.</summary>
    public static int VirtualIndex(int levelId) => (levelId / 10 - 1) * 6 + levelId % 10;

    // ---- native addresses (USA) ----
    private const uint ExeBase = UniversalExeBindings.ExeBase, JumpTable = UniversalExeBindings.LevelInitJumpTable, DefaultCase = 0x8005B6E0;
    private const uint Names = 0x8006F7F0, WorldNames = 0x8006F7D0, Music = 0x8006EF9C, Kill = 0x8006E9A4, Arrival = 0x8006EDB4,
        GemTotals = 0x8006F438, DragonTotals = 0x8006F414;
    private const uint Checksum = 0x8005956C, Memset = 0x80016914, Strlen = 0x8006276C, CardWrite = 0x800670E4, CardRead = 0x80066E28;
    private const uint ExeSaveName = 0x80010DB8, CardSaveName = 0x8007AA3C, CardOverlayBase = UniversalOverlay.Base;
    private const int ExeLength = 0x66000, CardOverlayLength = 0x3800, WadRow = 0x48;
    // save slot offsets (index 35's native fields, the window's extension)
    private const int SVisited = 0x63, SDone = 0x87, SDragons = 0xAB, SGems = 0xF2, SCollected = 0x56C;
    private const int SMagic = 0x590, SVersion = 0x592, SChecksum = 0x594, SRecords = 0x598, Magic = 0x574C, Version = 1;
    private const int RecordBytes = 40, Records = 1 + MaxGuests, ExtWords = MaxGuests * RecordBytes / 4;

    private static readonly string[] CaveSha256 =
    [
        "d565d94a57a1d73b2f09626fe1020b5bc53ab6f95c53e815c8fc0ad7f3868b8c", "d28dc1a3cc3aa06fe129edcdefc55f4c8d8a2b76dcbcce750146a545e2f7801d",
        "474462180b622dc3da6738b5adb21760a3b4a9fb5b7a5689d595c296c8f2b5bc", "462ef72a24fdcef5049ecf309b855d42eb7fbb01ddfa8b34d1dfdbcfabef745b",
        "af3ab6a6d391ec64d018dd4b2a5db2008f6851d053e08178477b2cb245d8f8f9", "a87b028f55911d4f014332e38c7d28e176a70e49098a45e71f8336b4b42b63d9",
        "1d7c673076379ad9b996a785ae9850ac8349550a877c9211c3c3b9591782fc8b", "964c061abd19ae96f011bd0c4e5f720b1eaa63813f17e523f8ce7edd84ca63e3",
        "65393d8ac0059592b7c728456cd065ea31e693c95d73f3e0a6594cc8fe7c5934", "080a5cf9b4fe6a76b5a4455b032ba317de9967bfe69c02490cf4ea4b71716454",
        "2d5d1a02ccc70310756f29a0cf67e9a97e23230b76541c4c0bd47f7b5d60d5f1", "462aefcefbdfb2be0b9b3f695de3a11dc6af6f48163c792ceea2d1ec9334800c",
        "69afc3a4659e9907f7daedbd45e0bba204b5caa3f985ba0b91cd053652a83ba8", "68b7a9334903eb4a9d512073e62fa9ea4a62ed6cf32c2ebf3c3eac4123b9c8f8",
        "e739c11e762a1358e0cca99fe2a99e92ffd6d8536257f61849a8b9c9cd72abb9", "65ad21dc2a0829994aa9a9944b722951047d64c4437de77b758ee1a96218be0e",
        "6b3b9e6ef9c2f26150e98bce50f9f9276b9db534e1d652b1d9ddb6f75f6bf28b", "b359adf178cd517769b5770297a27045deec77cc4bd01b7b15689314f24450c9",
        "3a196dffe604207cd6eca8bcfbcdc8bf9e603f57130a1a71a13fae68b44fc112",
    ];
    /// <summary>Tests only: accept caves whose bytes are not the USA game's (synthetic executables).</summary>
    internal static bool TestSkipCaveHashes;

    /// <summary>Null when <paramref name="name"/> can be shown by the game's font, otherwise why not.</summary>
    public static string? NameRefusal(string? name)
    {
        if (string.IsNullOrEmpty(name)) return "A name is needed.";
        if (name.Length > MaxNameLength) return $"A name may have at most {MaxNameLength} letters.";
        if (name.Any(c => c is not ((>= 'A' and <= 'Z') or ' ' or '\''))) return "Names may use only capital letters A-Z, spaces and apostrophes.";
        if (name.Trim().Length == 0) return "A name needs a letter.";
        return null;
    }

    public static bool IsInstalled(byte[] exe) => exe.Length == ExeLength && Word(exe, 0x8001565C) is var w && w >> 26 == 3 &&
        NativeExecutableCaves.LevelWindow.Any(c => ((w & 0x03FFFFFF) << 2 | 0x80000000) is var t && t >= c.Start && t < c.End);

    /// <summary>Installs the window for <paramref name="guests"/> (refusing an executable that differs from the USA game
    /// where it hooks in or stores itself); installing it again with the same guests is a no-op, with different ones it
    /// is refused. <paramref name="wadTable"/> is the first 0x800 bytes of the final WAD.WAD (rows of entries 79/80 and of
    /// the guests' entries).</summary>
    public static void Install(byte[] exe, ReadOnlySpan<byte> wadTable, IReadOnlyList<WindowGuest> guests, string worldName)
    {
        var patches = Plan(exe, wadTable, guests, worldName).Patches;
        bool installed = IsInstalled(exe);
        if (installed)
        {
            foreach (var p in patches)
                if (!exe.AsSpan(Off(p.Address), p.After.Length).SequenceEqual(p.After))
                    throw new InvalidDataException($"The executable already has a level window for different levels (0x{p.Address:X8}).");
            return;
        }
        foreach (var p in patches)
            if (p.Before != null && !exe.AsSpan(Off(p.Address), p.Before.Length).SequenceEqual(p.Before) &&
                !(p.AlreadyAfterAllowed && exe.AsSpan(Off(p.Address), p.After.Length).SequenceEqual(p.After)))
                throw new InvalidDataException($"The executable differs from the USA game where the level window goes ({p.Name}, 0x{p.Address:X8}).");
        if (!TestSkipCaveHashes)
            for (int i = 0; i < NativeExecutableCaves.LevelWindow.Count; i++)
            {
                var c = NativeExecutableCaves.LevelWindow[i];
                if (Convert.ToHexStringLower(SHA256.HashData(exe.AsSpan(Off(c.Start), c.Length))) != CaveSha256[i])
                    throw new InvalidDataException($"The executable's code at 0x{c.Start:X8} is not the USA game's: another feature uses the level window's room.");
            }
        foreach (var p in patches) p.After.CopyTo(exe.AsSpan(Off(p.Address)));
    }

    /// <summary>One change the window makes: <see cref="Before"/> null for cave contents (checked by hash).</summary>
    public sealed record Patch(string Name, uint Address, byte[] After, byte[]? Before, bool AlreadyAfterAllowed = false);

    /// <summary>The window's changes and where its routines and data went (for tests and emulator scripts).</summary>
    public sealed record WindowPlan(IReadOnlyList<Patch> Patches, IReadOnlyDictionary<string, uint> Labels);

    /// <summary>The complete set of changes for these guests (does not modify <paramref name="exe"/>).</summary>
    public static WindowPlan Plan(byte[] exe, ReadOnlySpan<byte> wadTable, IReadOnlyList<WindowGuest> guests, string worldName)
    {
        if (exe.Length != ExeLength) throw new InvalidDataException("The level window needs the complete USA executable.");
        if (wadTable.Length < 0x800) throw new InvalidDataException("The level window needs WAD.WAD's 256-entry table.");
        if (NameRefusal(worldName) is { } worldWhy) throw new InvalidDataException("World name: " + worldWhy);
        if (guests.Count is < 1 or > MaxGuests) throw new InvalidDataException($"A world has 1 to {MaxGuests} levels.");
        if (guests.Select(g => g.LevelId).Distinct().Count() != guests.Count) throw new InvalidDataException("Two world levels share a level id.");
        var entries = new HashSet<int>();
        byte[] table = wadTable[..0x800].ToArray();
        (uint Off, uint Len) Row(int entry) => ((uint)BinaryPrimitives.ReadInt32LittleEndian(table.AsSpan(8 * entry)), (uint)BinaryPrimitives.ReadInt32LittleEndian(table.AsSpan(8 * entry + 4)));
        foreach (var g in guests)
        {
            if (g.LevelId is < FirstId or > LastId) throw new InvalidDataException($"World levels use ids {FirstId}-{LastId}, not {g.LevelId}.");
            if (NameRefusal(g.Name) is { } why) throw new InvalidDataException($"Level {g.LevelId}'s name: {why}");
            if (g.TemplateLevelId != FreshTemplate && (g.TemplateLevelId is < 10 or > 64 || g.TemplateLevelId % 10 > 5 ||
                Word(exe, JumpTable + 4 * (uint)g.TemplateLevelId) == DefaultCase))
                throw new InvalidDataException($"Level {g.LevelId}'s template {g.TemplateLevelId} is not a level whose initializer it can borrow.");
            foreach (int e in new[] { g.OverlayEntry, g.DataEntry })
            {
                if (e is < 1 or > 255 || e is 79 or 80) throw new InvalidDataException($"Level {g.LevelId} cannot use WAD entry {e}.");
                if (!entries.Add(e)) throw new InvalidDataException($"WAD entry {e} is used by two world levels.");
                if (Row(e).Len == 0) throw new InvalidDataException($"WAD entry {e} of level {g.LevelId} is empty.");
            }
            if (g.MusicTrack is < 0 or > 255 || g.GemTotal is < 0 or > 0xFFFF || g.DragonTotal is < 0 or > 255 || g.ArrivalHeight is < short.MinValue or > short.MaxValue)
                throw new InvalidDataException($"Level {g.LevelId}'s music, totals or arrival height are out of range.");
        }

        var asm = new NativeCaveAssembler();
        // ---- data ----
        var state = asm.Data("LW_STATE");                 // owner word, then records 0-5 (40 bytes each)
        state.Word(0).Label("LW_BANKS_AT").Bytes(new byte[Records * RecordBytes]);
        byte[] stats = new byte[Records * RecordBytes];
        void Stat(int record, (uint, uint) overlay, (uint, uint) data, int kill, int arrival, int gems, int music, int dragons, uint copy, uint init, uint update)
        {
            var r = stats.AsSpan(record * RecordBytes, RecordBytes);
            BinaryPrimitives.WriteUInt32LittleEndian(r, overlay.Item1); BinaryPrimitives.WriteUInt32LittleEndian(r[4..], overlay.Item2);
            BinaryPrimitives.WriteUInt32LittleEndian(r[8..], data.Item1); BinaryPrimitives.WriteUInt32LittleEndian(r[12..], data.Item2);
            BinaryPrimitives.WriteInt32LittleEndian(r[16..], kill);
            BinaryPrimitives.WriteUInt32LittleEndian(r[20..], copy); BinaryPrimitives.WriteUInt32LittleEndian(r[24..], init); BinaryPrimitives.WriteUInt32LittleEndian(r[28..], update);
            BinaryPrimitives.WriteInt16LittleEndian(r[32..], (short)arrival); BinaryPrimitives.WriteUInt16LittleEndian(r[34..], (ushort)gems);
            r[36] = (byte)music; r[37] = (byte)dragons;
        }
        // record 0: level 65 as this executable has it (its own slot words, WAD entries 79/80)
        Stat(0, Row(79), Row(80), (int)Word(exe, Kill + 4 * WindowIndex), (short)Word(exe, Arrival + 4 * WindowIndex),
            BinaryPrimitives.ReadUInt16LittleEndian(exe.AsSpan(Off(GemTotals + 2 * WindowIndex))), (int)Word(exe, Music + 4 * WindowIndex),
            exe[Off(DragonTotals + WindowIndex)], 0, 0, 0);
        var strings = new List<(string Label, string Text)>();
        foreach (var g in guests)
        {
            int record = g.LevelId - FirstId + 1;
            uint baseCase = g.TemplateLevelId == FreshTemplate ? Word(exe, JumpTable + 13 * 4) : Word(exe, JumpTable + 4 * (uint)g.TemplateLevelId);
            uint[] ov = new uint[3];
            if (g.Bindings != null)
            {
                var template = UniversalExeBindings.Read(exe, g.TemplateLevelId == FreshTemplate ? 13 : g.TemplateLevelId);
                List<string> refused = [];
                foreach (var (global, value) in g.Bindings)
                {
                    uint current = template.TryGetValue(global, out var b) ? b.Value : 0;
                    if (current == value) continue;
                    int k = OverriddenGlobals.ToList().IndexOf(global);
                    if (k < 0) { refused.Add($"0x{global:X8} = 0x{value:X8}"); continue; }
                    if (value == 0) throw new InvalidDataException($"Level {g.LevelId}'s initializer callback 0x{global:X8} is null.");
                    ov[k] = value;
                }
                if (refused.Count > 0)
                    throw new InvalidDataException($"Level {g.LevelId} needs initializer globals the level window cannot set: {string.Join(", ", refused)}.");
            }
            if (baseCase == DefaultCase) throw new InvalidDataException($"Level {g.LevelId}'s template case is missing.");
            Stat(record, Row(g.OverlayEntry), Row(g.DataEntry), g.KillHeight, g.ArrivalHeight, g.GemTotal, g.MusicTrack, g.DragonTotal, ov[0], ov[1], ov[2]);
            strings.Add(($"LW_NAME_{g.LevelId}", g.Name));
        }
        asm.Data("LW_STATS").Bytes(stats);
        strings.Add(("LW_WORLD_NAME", worldName));
        var text = asm.Data("LW_STRINGS");
        foreach (var (label, s) in strings) { text.Label(label); text.Bytes([.. Encoding.ASCII.GetBytes(s), 0]); }
        var names = asm.Data("LW_GUEST_NAMES");              // 5 pointers, ids 70-74 (an id without a level: empty), filled once placed
        for (int id = FirstId; id <= LastId; id++) names.Word(0);
        asm.Data("LW_SHADOW").Bytes(new byte[104]);
        EmitCode(asm, worldName.Length);
        var labels = asm.Place(NativeExecutableCaves.LevelWindow);
        for (int id = FirstId; id <= LastId; id++) names.SetWord(id - FirstId, labels.TryGetValue($"LW_NAME_{id}", out var a) ? a : labels["LW_WORLD_NAME"] + (uint)worldName.Length);

        List<Patch> patches = [];
        foreach (var b in asm.Blocks)
        {
            var words = b.Encode(labels);
            byte[] bytes = new byte[words.Length * 4];
            for (int i = 0; i < words.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4 * i), words[i]);
            patches.Add(new(b.Name, b.Address, bytes, null));
        }
        void Hook(string name, uint at, uint before, uint after) => patches.Add(new(name, at, W(after), W(before)));
        void HookN(string name, uint at, uint[] before, uint[] after) => patches.Add(new(name, at, W(after), W(before)));
        uint Jal(string label) => 0x0C000000u | ((labels[label] >> 2) & 0x03FFFFFF);
        uint J(string label) => 0x08000000u | ((labels[label] >> 2) & 0x03FFFFFF);
        // loader, save, new game
        Hook("loader index", 0x8001565C, 0x3C018007, Jal("LW_LOAD"));
        Hook("serializer checksum", 0x80059A24, 0x0C01655B, Jal("LW_SAVE"));
        Hook("deserializer checksum", 0x80059834, 0x0C01655B, Jal("LW_LOAD_SAVE"));
        Hook("new game reset", 0x80012764, 0x0C005A45, Jal("LW_NEW_GAME"));
        Hook("serializer clears the slot", 0x80059874, 0x24060600, 0x24060000 | SlotBytes);
        Hook("save: serialize", 0x800321F8, 0x2484FA00, 0x24840000 | Imm(-SlotBytes));
        Hook("save: size", 0x8003220C, 0x24020600, 0x24020000 | SlotBytes);
        Hook("save: buffer", 0x80032220, 0x24C6FA00, 0x24C60000 | Imm(-SlotBytes));
        Hook("save: write", 0x80032230, 0x0C019C39, Jal("LW_CARD_WRITE"));
        Hook("save: read back size", 0x80032298, 0x24020600, 0x24020000 | SlotBytes);
        Hook("save: read back buffer", 0x800322A8, 0x24C6FA00, 0x24C60000 | Imm(-SlotBytes));
        Hook("save: read back", 0x800322B8, 0x0C019B8A, Jal("LW_CARD_READ"));
        Hook("save: check", 0x800322FC, 0x2484FA00, 0x24840000 | Imm(-SlotBytes));
        Hook("save: check word", 0x8003230C, 0x8C63FF8C, 0x8C630000 | Imm(0x58C - SlotBytes));
        // The save dialog lays its letters (0x58-byte font objects) downward from second packet buffer start + 0x1BA00,
        // just below the retail slot; start them below the bigger one or they overwrite its first 0x80 bytes mid-save.
        Hook("save: dialog letters below the slot", 0x8001D754, 0x3463BA00, 0x34630000 | (uint)((0x1C000 - SlotBytes) & 0xFFFF));
        byte[] retailName = [.. Encoding.ASCII.GetBytes(RetailSaveName), 0], windowName = [.. Encoding.ASCII.GetBytes(SaveName), 0];
        patches.Add(new("save name", ExeSaveName, windowName, retailName));
        // names: entering banner (two) and doorway lettering
        foreach (var (at, load) in new (uint, uint)[] { (0x800197D4, 0x8C26F7F0), (0x800197F4, 0x8C26F7F0), (0x8003C3A4, 0x8C32F7F0) })
            HookN("name reader", at, [0x3C018007, 0x00220821, load], [Jal("LW_NAME"), 0, load & 0xFFFF0000]);
        // world-name slot 7 (the leftover "THIGH MASTERS"): inventory page 7 and "ENTERING ... WORLD"
        // (Renamed levels repack the name pool, which moves the leftover; it must still be the leftover.)
        uint leftover = Word(exe, WorldNames + 7 * 4);
        Hook("world name", WorldNames + 7 * 4, IsLeftoverWorldName(exe, leftover) ? leftover : 0x8001000C, labels["LW_WORLD_NAME"]);
        // doorways to 70-74; homeworld-to-homeworld doorways remember where Spyro came from
        Hook("doorway guard", 0x80057110, 0x28420041, 0x28420000 | (LastId + 1));
        HookN("doorway arrival", 0x80057178, [0x10820003, 0], [J("LW_PREV"), 0]);
        // inventory
        Hook("inventory page", 0x8002C784, 0x01031823, 0x8C23595C);
        Hook("inventory fill", 0x8001BB90, 0x0C0189DB, Jal("LW_INVENTORY"));
        uint sh = labels["LW_SHADOW"];
        uint Hi(uint a) => (uint)(((a + 0x8000) >> 16) & 0xFFFF);
        uint Lo(uint a) => a & 0xFFFF;
        uint[] RowOnly(string v) => v == "v1" ? [0x00001821, 0, 0] : [0x00001021, 0, 0];
        uint[] Page6(string v) => v == "v1" ? [0x00021840, 0x00621821, 0x00031840] : [0x00031040, 0x00431021, 0x00021040];
        HookN("inventory visited row", 0x8001BE20, Page6("v1"), RowOnly("v1"));
        Hook("inventory visited table", 0x8001BE30, 0x3C018008, 0x3C010000 | Hi(sh + 84));
        Hook("inventory visited read", 0x8001BE38, 0x90228E78, 0x90220000 | Lo(sh + 84));
        HookN("inventory name row", 0x8001BF80, Page6("v0"), RowOnly("v0"));
        Hook("inventory name table", 0x8001BF94, 0x3C018007, 0x3C010000 | Hi(sh));
        Hook("inventory name read", 0x8001BF9C, 0x8C24F7F0, 0x8C240000 | Lo(sh));
        HookN("inventory gems row", 0x8001BFC8, Page6("v0"), RowOnly("v0"));
        HookN("inventory gems table", 0x8001BFD8, [0x3C038007, 0x24637420], [0x3C030000 | Hi(sh + 24), 0x24630000 | Lo(sh + 24)]);
        HookN("inventory gem total row", 0x8001C0CC, Page6("v0"), RowOnly("v0"));
        Hook("inventory gem total table", 0x8001C0E0, 0x3C018007, 0x3C010000 | Hi(sh + 72));
        Hook("inventory gem total read", 0x8001C0E8, 0x8426F438, 0x84260000 | Lo(sh + 72));
        HookN("inventory dragon total table", 0x8001C118, [0x3C098007, 0x2529F414], [0x3C090000 | Hi(sh + 90), 0x25290000 | Lo(sh + 90)]);
        HookN("inventory dragon row", 0x8001C120, Page6("v0"), RowOnly("v0"));
        Hook("inventory dragons table", 0x8001C15C, 0x3C018007, 0x3C010000 | Hi(sh + 48));
        Hook("inventory dragons read", 0x8001C164, 0x8C2672D8, 0x8C260000 | Lo(sh + 48));
        HookN("inventory dragon total table 2", 0x8001C1C4, [0x3C098007, 0x2529F414], [0x3C090000 | Hi(sh + 90), 0x25290000 | Lo(sh + 90)]);
        HookN("inventory dragon total row 2", 0x8001C1E4, Page6("v0"), RowOnly("v0"));
        HookN("inventory current row", 0x8001C27C, [0x3C028007, 0x8C425964], [0x3C020000 | Hi(sh + 100), 0x8C420000 | Lo(sh + 100)]);
        Hook("inventory right arrow page", 0x8001BB18, 0x28620005, 0x28620006);
        HookN("inventory right arrow world", 0x8001BB2C, [0x3C018008, 0x00220821, 0x90228E7E], [0x3C010000 | Hi(sh + 96), 0, 0x90220000 | Lo(sh + 96)]);
        Hook("inventory left page", 0x8002EC34, 0x28620005, 0x28620006);
        Hook("inventory right page", 0x8002EC80, 0x28620005, 0x28620006);
        HookN("inventory right world", 0x8002EC94, [0x3C018008, 0x00220821, 0x90228E7E], [0x3C010000 | Hi(sh + 96), 0, 0x90220000 | Lo(sh + 96)]);
        // the window's level ids run their template's per-level initializer
        foreach (var g in guests)
        {
            uint target = g.TemplateLevelId == FreshTemplate ? Word(exe, JumpTable + 13 * 4) : Word(exe, JumpTable + 4 * (uint)g.TemplateLevelId);
            Hook($"initializer {g.LevelId}", JumpTable + 4 * (uint)g.LevelId, DefaultCase, target);
        }
        // index 35's late music plays music slot 35 (the ID65 bridge; already there when an ID65 level was built)
        foreach (var m in Id65BuiltInMusicSelectionCompiler.Bridge)
            patches.Add(new("late music " + m.Name, (uint)(m.FileOffset + ExeBase), Convert.FromHexString(m.AfterHex), Convert.FromHexString(m.BeforeHex), AlreadyAfterAllowed: true));
        return new(patches, labels);
    }

    /// <summary>The routines (see the class summary). Registers: hooks that replace a call keep the C convention (a0 is
    /// the argument passed on); the loader hook and the name helper keep everything their callers use.</summary>
    private static void EmitCode(NativeCaveAssembler asm, int worldNameLength)
    {
        // Blocks are placed into separate caves: a branch only targets its own block; blocks continue with "j".
        // DYN_OUT: slot 35 -> record at t0 (t0-t4)
        asm.Code("LW_DYN_OUT")
           .Lui("t1", 0x8008).Lbu("t2", -0x7165, "t1").Lbu("t3", -0x5935, "t1").Sb("t2", 0, "t0").Sb("t3", 1, "t0")
           .Lui("t1", 0x8007).Lw("t2", 0x7364, "t1").Lw("t3", 0x74AC, "t1").Addiu("t4", "t1", 0x7D68).Sb("t2", 2, "t0").Sh("t3", 4, "t0")
           .Addiu("t0", "t0", 8).Li("t3", 8)
           .Label("lw_out_loop").Lw("t2", 0, "t4").Addiu("t4", "t4", 4).Sw("t2", 0, "t0").Addiu("t3", "t3", -1).Bnez("t3", "lw_out_loop").Addiu("t0", "t0", 4)
           .Jr("ra").Nop();
        // DYN_IN: record at t0 -> slot 35 (t0-t4)
        asm.Code("LW_DYN_IN")
           .Lbu("t2", 0, "t0").Lbu("t3", 1, "t0").Lui("t1", 0x8008).Sb("t2", -0x7165, "t1").Sb("t3", -0x5935, "t1")
           .Lbu("t2", 2, "t0").Lhu("t3", 4, "t0").Lui("t1", 0x8007).Sw("t2", 0x7364, "t1").Sw("t3", 0x74AC, "t1").Addiu("t4", "t1", 0x7D68)
           .Addiu("t0", "t0", 8).Li("t3", 8)
           .Label("lw_in_loop").Lw("t2", 0, "t0").Addiu("t0", "t0", 4).Sw("t2", 0, "t4").Addiu("t3", "t3", -1).Bnez("t3", "lw_in_loop").Addiu("t4", "t4", 4)
           .Jr("ra").Nop();
        // STAT_IN: static record at t0 -> slot 35's WAD row and table words (t1-t3)
        asm.Code("LW_STAT_IN")
           .Lui("t1", 0x8008).Addiu("t1", "t1", -0x56B8)                                                // 0x8007A948, WAD row 35
           .Lw("t2", 0, "t0").Lw("t3", 4, "t0").Sw("t2", 0, "t1").Sw("t3", 4, "t1")
           .Lw("t2", 8, "t0").Lw("t3", 12, "t0").Sw("t2", 8, "t1").Sw("t3", 12, "t1")
           .Lui("t1", 0x8007)
           .Lw("t2", 16, "t0").Lh("t3", 32, "t0").Sw("t2", -0x15D0, "t1").Sw("t3", -0x11C0, "t1")      // kill 0x8006EA30, arrival 0x8006EE40
           .Lhu("t2", 34, "t0").Lbu("t3", 36, "t0").Sh("t2", -0xB82, "t1").Sw("t3", -0xFD8, "t1")      // gem total 0x8006F47E, music 0x8006F028
           .Lbu("t2", 37, "t0").Jr("ra").Sb("t2", -0xBC9, "t1");                                         // dragon total 0x8006F437
        static void Record(NativeCaveAssembler.Block b, string reg, string table, string rec, string tmp) =>
            b.Sll(reg, rec, 5).Sll(tmp, rec, 3).Addu(reg, reg, tmp).LuiHi(tmp, table).Addu(reg, reg, tmp).LoLoad(0x09, reg, reg, table);
        // LOAD (after the per-level switch; v0 = the index the loader computed, already stored): window ids run in slot 35
        var l = asm.Code("LW_LOAD");
        l.Lui("t1", 0x8007).Lw("t2", 0x596C, "t1").Nop()
         .Addiu("t3", "t2", -FreshTemplate).Beqz("t3", "lw_load_rec").Move("t5", "zero")
         .Addiu("t5", "t2", -(FirstId - 1)).Addiu("t3", "t5", -1).Sltiu("t3", "t3", MaxGuests).Beqz("t3", "lw_load_ret").Nop()
         .Label("lw_load_rec").Move("t9", "ra")
         .LuiHi("t6", "LW_STATE").LoLoad(0x23, "t7", "t6", "LW_STATE").Nop()
         .Beq("t7", "t5", "lw_load_same").LoLoad(0x2B, "t5", "t6", "LW_STATE");
        Record(l, "t0", "LW_BANKS_AT", "t7", "t1"); l.Jal("LW_DYN_OUT").Nop();
        Record(l, "t0", "LW_BANKS_AT", "t5", "t1"); l.Jal("LW_DYN_IN").Nop();
        Record(l, "t0", "LW_STATS", "t5", "t1"); l.Jal("LW_STAT_IN").Nop();
        l.Label("lw_load_same").J("LW_LOAD_B").Nop()
         .Label("lw_load_ret").Jr("ra").Nop();
        var lb = asm.Code("LW_LOAD_B");                    // the record's callbacks after its template's case, then index 35
        lb.Beqz("t5", "lw_load_index").Nop();
        Record(lb, "t0", "LW_STATS", "t5", "t1");
        lb.Lw("t1", 20, "t0").Lw("t2", 24, "t0").Lw("t3", 28, "t0").Lui("t4", 0x8008)
          .Beqz("t1", "lw_load_init").Nop().Sw("t1", -0x7A28, "t4").Sw("t1", -0x7A24, "t4")          // copy buffer 0x800785D8 and its copy 0x800785DC
          .Label("lw_load_init").Lui("t4", 0x8007).Beqz("t2", "lw_load_update").Nop().Sw("t2", 0x58CC, "t4")
          .Label("lw_load_update").Beqz("t3", "lw_load_index").Nop().Sw("t3", 0x5734, "t4")
          .Label("lw_load_index").Li("v0", WindowIndex).Lui("t1", 0x8007).Sw("v0", 0x5964, "t1").Move("ra", "t9").Jr("ra").Nop();
        // SAVE: replaces the serializer's checksum call (a0 = slot buffer), then tail-calls it
        var sv = asm.Code("LW_SAVE");
        sv.Move("t9", "ra").LuiHi("t6", "LW_STATE").LoLoad(0x23, "t5", "t6", "LW_STATE").Nop();
        Record(sv, "t0", "LW_BANKS_AT", "t5", "t1");
        sv.Jal("LW_DYN_OUT").Nop()
          .La("t0", "LW_BANKS_AT")
          .Lbu("t2", 0, "t0").Lbu("t3", 1, "t0").Sb("t2", SVisited, "a0").Sb("t3", SDone, "a0")
          .Lbu("t2", 2, "t0").Lhu("t3", 4, "t0").Sb("t2", SDragons, "a0").Sh("t3", SGems, "a0")
          .Addiu("t0", "t0", 8).Addiu("t1", "a0", SCollected).Li("t3", 8)
          .Label("lw_save_coll").Lw("t2", 0, "t0").Addiu("t0", "t0", 4).Sw("t2", 0, "t1").Addiu("t3", "t3", -1).Bnez("t3", "lw_save_coll").Addiu("t1", "t1", 4)
          .J("LW_SAVE_B").Nop();
        asm.Code("LW_SAVE_B")
          .Li("t2", Magic).Sh("t2", SMagic, "a0").Li("t2", Version | MaxGuests << 8).Sh("t2", SVersion, "a0")
          .La("t0", "LW_BANKS_AT", RecordBytes).Addiu("t1", "a0", SRecords).Li("t3", ExtWords).Move("t4", "zero")
          .Label("lw_save_ext").Lw("t2", 0, "t0").Addiu("t0", "t0", 4).Addu("t4", "t4", "t2").Sw("t2", 0, "t1").Addiu("t3", "t3", -1).Bnez("t3", "lw_save_ext").Addiu("t1", "t1", 4)
          .Sw("t4", SChecksum, "a0").Move("ra", "t9").J(Checksum).Nop();
        // LOAD_SAVE: replaces the deserializer's checksum call (a0 = slot buffer), then tail-calls it
        asm.Code("LW_LOAD_SAVE")
          .Move("t9", "ra").LuiHi("t6", "LW_STATE").LoLoad(0x2B, "zero", "t6", "LW_STATE")
          .La("t0", "LW_STATS").Jal("LW_STAT_IN").Nop()
          .La("t0", "LW_BANKS_AT").Jal("LW_DYN_OUT").Nop()
          .J("LW_LS_EXT").Nop();
        asm.Code("LW_LS_EXT")                              // a valid extension (magic, word checksum) is copied into records 1-5
          .Lhu("t2", SMagic, "a0").Li("t3", Magic).La("t1", "LW_BANKS_AT", RecordBytes)
          .Bne("t2", "t3", "lw_ls_bad").Addiu("t0", "a0", SRecords)
          .Move("t4", "zero").Li("t3", ExtWords).Move("t5", "t0")
          .Label("lw_ls_sum").Lw("t2", 0, "t5").Addiu("t3", "t3", -1).Addiu("t5", "t5", 4).Bnez("t3", "lw_ls_sum").Addu("t4", "t4", "t2")
          .Lw("t2", SChecksum, "a0").Nop().Bne("t2", "t4", "lw_ls_bad").Li("t3", ExtWords)
          .Label("lw_ls_copy").Lw("t2", 0, "t0").Addiu("t0", "t0", 4).Sw("t2", 0, "t1").Addiu("t3", "t3", -1).Bnez("t3", "lw_ls_copy").Addiu("t1", "t1", 4)
          .J("LW_LS_TOTAL").Nop()
          .Label("lw_ls_bad").J("LW_LS_CLEAR").Nop();
        asm.Code("LW_LS_CLEAR")                            // otherwise records 1-5 start empty; then the totals
          .Li("t3", ExtWords)
          .Label("lw_ls_clear_loop").Sw("zero", 0, "t1").Addiu("t3", "t3", -1).Bnez("t3", "lw_ls_clear_loop").Addiu("t1", "t1", 4)
          .Label("LW_LS_TOTAL").La("t0", "LW_BANKS_AT", RecordBytes).Li("t3", MaxGuests).Lui("t6", 0x8007).Lw("t4", 0x5860, "t6").Lw("t5", 0x5750, "t6")
          .Label("lw_ls_total").Lhu("t2", 4, "t0").Lbu("t1", 2, "t0").Addu("t4", "t4", "t2").Addu("t5", "t5", "t1").Addiu("t3", "t3", -1).Bnez("t3", "lw_ls_total").Addiu("t0", "t0", RecordBytes)
          .Sw("t4", 0x5860, "t6").Sw("t5", 0x5750, "t6").Move("ra", "t9").J(Checksum).Nop();
        // NEW_GAME: replaces the reset's collected-bit memset; clears owner and records, restores level 65's words
        asm.Code("LW_NEW_GAME")
          .Move("t9", "ra").La("t1", "LW_STATE").Li("t3", 1 + Records * RecordBytes / 4)
          .Label("lw_ng_clear").Sw("zero", 0, "t1").Addiu("t3", "t3", -1).Bnez("t3", "lw_ng_clear").Addiu("t1", "t1", 4)
          .LuiHi("t0", "LW_STATS").Jal("LW_STAT_IN").LoLoad(0x09, "t0", "t0", "LW_STATS")
          .Move("ra", "t9").J(Memset).Nop();
        // NAME: at = &name pointer for the index*4 in v0 (36-40: the window's names); only at changes
        asm.Code("LW_NAME")
          .Sltiu("at", "v0", 4 * (WindowIndex + 1)).Beqz("at", "lw_name_guest").Lui("at", 0x8007)
          .Addu("at", "at", "v0").Jr("ra").Addiu("at", "at", -0x810)
          .Label("lw_name_guest").LuiHi("at", "LW_GUEST_NAMES", -4 * (WindowIndex + 1)).Addu("at", "at", "v0").Jr("ra")
          .LoLoad(0x09, "at", "at", "LW_GUEST_NAMES", -4 * (WindowIndex + 1));
        // PREV: a doorway from a homeworld to another homeworld records where Spyro came from (a0 = current id, v0 = its homeworld)
        asm.Code("LW_PREV")
          .Bne("a0", "v0", "lw_prev_store").Lui("t0", 0x8007).Lw("t1", 0x58B4, "t0").Lui("t2", 0x6666).Ori("t2", "t2", 0x6667)
          .Mult("t1", "t2").Mfhi("t2").Sra("t2", "t2", 2).Sll("t3", "t2", 2).Addu("t3", "t3", "t2").Sll("t3", "t3", 1)
          .Bne("t1", "t3", "lw_prev_skip").Nop()
          .Label("lw_prev_store").J(0x80057180).Nop()
          .Label("lw_prev_skip").J(0x80057188).Nop();
        // CARD: the gameplay save's card offset for 0x680-byte slots (slot * 0x680 + 0x200), then the card call
        asm.Code("LW_CARD_WRITE")
          .LuiHi("t9", CardWrite).B("lw_card_io").LoLoad(0x09, "t9", "t9", CardWrite)
          .Label("LW_CARD_READ").LuiHi("t9", CardRead).LoLoad(0x09, "t9", "t9", CardRead)
          .Label("lw_card_io").Lui("t0", 0x8008).Lw("t0", -0x72E4, "t0").Nop()
          .Sll("t1", "t0", 11).Sll("t2", "t0", 8).Subu("t1", "t1", "t2").Sll("t2", "t0", 7).Subu("t1", "t1", "t2")
          .Jr("t9").Addiu("a3", "t1", 0x200);
        // INVENTORY: fills the 6-row shadow for the page being drawn, then the strlen the hook replaced (a0 intact)
        var iv = asm.Code("LW_INVENTORY");
        iv.Move("v1", "ra").LuiHi("t1", "LW_STATE").LoLoad(0x23, "t7", "t1", "LW_STATE").Nop();
        Record(iv, "t0", "LW_BANKS_AT", "t7", "t1");
        iv.Jal("LW_DYN_OUT").Nop()
          .Lui("t0", 0x8007).Lw("t9", 0x5744, "t0").Move("t8", "zero")
          .Label("lw_inv_row").Sll("t6", "t9", 1).Addu("t6", "t6", "t9").Sll("t6", "t6", 1).Addu("t6", "t6", "t8")
          .Sll("v0", "t6", 2).Jal("LW_NAME").Nop().Lw("t4", 0, "at")
          .Sltiu("t0", "t9", 6).Beqz("t0", "lw_inv_page7").Li("t1", WindowIndex)
          .Bne("t6", "t1", "lw_inv_native").Nop()
          .B("lw_inv_bank").Move("t5", "zero")
          .Label("lw_inv_page7").Sltiu("t0", "t8", MaxGuests).Bnez("t0", "lw_inv_bank").Addiu("t5", "t8", 1)
          .J("LW_INV_BLANK").Move("t2", "zero")
          .Label("lw_inv_native").Lui("t0", 0x8008).Addu("t0", "t0", "t6").Lbu("t2", -0x7188, "t0")
          .Sll("t3", "t6", 2).Lui("t0", 0x8007).Addu("t0", "t0", "t3").Lw("t5", 0x7420, "t0").Lw("a1", 0x72D8, "t0")
          .Sll("t3", "t6", 1).Lui("t0", 0x8007).Addu("t0", "t0", "t3").Lhu("t3", -0xBC8, "t0")
          .Lui("t0", 0x8007).Addu("t0", "t0", "t6").B("lw_inv_store").Lbu("t1", -0xBEC, "t0")
          .Label("lw_inv_bank");
        Record(iv, "t0", "LW_BANKS_AT", "t5", "t1");
        iv.Lbu("t2", 0, "t0").Lbu("a1", 2, "t0").Lhu("t6", 4, "t0");
        Record(iv, "t0", "LW_STATS", "t5", "t1");
        iv.Lhu("t3", 34, "t0").Lbu("t1", 37, "t0").Move("t5", "t6")
          .Label("lw_inv_store").Sll("t6", "t8", 2).La("t0", "LW_SHADOW").Addu("t6", "t6", "t0")
          .Sw("t4", 0, "t6").Sw("t5", 24, "t6").Sw("a1", 48, "t6")
          .Sll("t6", "t8", 1).Addu("t6", "t6", "t0").Sh("t3", 72, "t6")
          .Addu("t6", "t8", "t0").Sb("t2", 84, "t6").Sb("t1", 90, "t6")
          .Addiu("t8", "t8", 1).Sltiu("t6", "t8", 6).Bnez("t6", "lw_inv_row").Nop()
          .J("LW_INVENTORY_B").Nop();
        // page 7's sixth row (there is no id 75): unvisited, empty name (the world name's terminator), no gems or dragons
        asm.Code("LW_INV_BLANK")
          .Move("t5", "zero").Move("a1", "zero").Move("t3", "zero").Move("t1", "zero")
          .La("t4", "LW_WORLD_NAME", worldNameLength).J("lw_inv_store").Nop();
        // ... then whether the next page's homeworld was visited, and which row is the current level
        asm.Code("LW_INVENTORY_B")
          .Sltiu("t6", "t9", 5).Beqz("t6", "lw_inv_next5").Addiu("t6", "t9", 1)
          .Sll("t1", "t6", 1).Addu("t6", "t6", "t1").Sll("t6", "t6", 1).Lui("t1", 0x8008).Addu("t1", "t1", "t6").B("lw_inv_next").Lbu("t2", -0x7188, "t1")
          .Label("lw_inv_next5").Li("t1", 5).Bne("t9", "t1", "lw_inv_next").Move("t2", "zero")
          .LuiHi("t1", "LW_BANKS_AT", RecordBytes).LoLoad(0x24, "t2", "t1", "LW_BANKS_AT", RecordBytes)
          .Label("lw_inv_next").Nop().Sb("t2", 96, "t0")                                              // (load delay on both paths)
          .Lui("t1", 0x8007).Lw("t2", 0x596C, "t1").Lw("t3", 0x5964, "t1").Nop()
          .Sltiu("t4", "t2", FirstId).Bnez("t4", "lw_inv_cur").Nop().Addiu("t3", "t2", -(FirstId - (WindowIndex + 1)))
          .Label("lw_inv_cur").Sw("t3", 100, "t0").Move("ra", "v1").J(Strlen).Nop();
    }

    // ---------------- the card overlay (WAD entry 2): 0x680-byte slots, the new save name ----------------
    private static readonly (uint At, uint Before, uint After)[] CardSites = BuildCardSites();
    private static (uint, uint, uint)[] BuildCardSites()
    {
        List<(uint, uint, uint)> s = [];
        foreach (uint at in new uint[] { 0x8007B364, 0x8007BE88, 0x8007BEF8 }) s.Add((at, 0x24021400, 0x24020000 | SaveFileBytes));
        s.Add((0x8007BE68, 0x26100600, 0x26100000 | SlotBytes));
        foreach (uint at in new uint[] { 0x8007B3D0, 0x8007B4C0, 0x8007B4E4, 0x8007BF58, 0x8007C044, 0x8007C068 }) s.Add((at, 0x24840800, 0x24840000 | (0x200 + SlotBytes)));
        s.Add((0x8007C4F4, 0x24820800, 0x24820000 | (0x200 + SlotBytes)));
        foreach (uint at in new uint[] { 0x8007B3F4, 0x8007B4F4, 0x8007B518, 0x8007BF7C, 0x8007C078, 0x8007C09C }) s.Add((at, 0x24840E00, 0x24840000 | (0x200 + 2 * SlotBytes)));
        s.Add((0x8007C500, 0x24820E00, 0x24820000 | (0x200 + 2 * SlotBytes)));
        foreach (uint at in new uint[] { 0x8007B3E0, 0x8007B4D0, 0x8007BF68, 0x8007C054 }) s.Add((at, 0x8C830D8C, 0x8C830000 | (0x200 + SlotBytes + 0x58C)));
        foreach (var (at, before) in new (uint, uint)[] { (0x8007B404, 0x8C63138C), (0x8007B504, 0x8C83138C), (0x8007BF8C, 0x8C63138C), (0x8007C088, 0x8C83138C) })
            s.Add((at, before, (before & 0xFFFF0000) | (0x200 + 2 * SlotBytes + 0x58C)));
        return [.. s];
    }

    public static bool IsSaveOverlayPatched(byte[] cardOverlay) =>
        cardOverlay.Length == CardOverlayLength && CardSites.All(s => OverlayWord(cardOverlay, s.At) == s.After) &&
        cardOverlay.AsSpan((int)(CardSaveName - CardOverlayBase), SaveName.Length).SequenceEqual(Encoding.ASCII.GetBytes(SaveName));

    /// <summary>Patches the card-menu overlay (WAD entry 2) for 0x680-byte slots and the window's save name; again: no-op.</summary>
    public static void PatchSaveOverlay(byte[] cardOverlay)
    {
        if (cardOverlay.Length != CardOverlayLength) throw new InvalidDataException("The card overlay is not the USA game's.");
        if (IsSaveOverlayPatched(cardOverlay)) return;
        int name = (int)(CardSaveName - CardOverlayBase);
        byte[] retail = [.. Encoding.ASCII.GetBytes(RetailSaveName), 0];
        if (!cardOverlay.AsSpan(name, retail.Length).SequenceEqual(retail) ||
            CardSites.Any(s => OverlayWord(cardOverlay, s.At) != s.Before))
            throw new InvalidDataException("The card overlay differs from the USA game where the level window's save layout goes.");
        foreach (var s in CardSites) BinaryPrimitives.WriteUInt32LittleEndian(cardOverlay.AsSpan((int)(s.At - CardOverlayBase)), s.After);
        Encoding.ASCII.GetBytes(SaveName).CopyTo(cardOverlay, name);
    }

    /// <summary><see cref="PatchSaveOverlay"/> on entry 2 of a WAD.WAD image, in place.</summary>
    public static void PatchSaveOverlayInWad(byte[] wad)
    {
        int offset = BinaryPrimitives.ReadInt32LittleEndian(wad.AsSpan(16)), length = BinaryPrimitives.ReadInt32LittleEndian(wad.AsSpan(20));
        if (length != CardOverlayLength || offset < 0x800 || offset + length > wad.Length) throw new InvalidDataException("WAD entry 2 is not the USA card overlay.");
        byte[] overlay = wad.AsSpan(offset, length).ToArray();
        PatchSaveOverlay(overlay);
        overlay.CopyTo(wad, offset);
    }

    private static bool IsLeftoverWorldName(byte[] exe, uint pointer)
    {
        const string leftover = "THIGH MASTERS";
        if (pointer < ExeBase + 0x800 || pointer - ExeBase + leftover.Length + 1 > exe.Length) return false;
        return exe.AsSpan(Off(pointer), leftover.Length + 1).SequenceEqual((ReadOnlySpan<byte>)[.. Encoding.ASCII.GetBytes(leftover), (byte)0]);
    }

    private static uint Imm(int value) => (uint)value & 0xFFFF;
    private static uint OverlayWord(byte[] overlay, uint at) => BinaryPrimitives.ReadUInt32LittleEndian(overlay.AsSpan((int)(at - CardOverlayBase)));
    private static int Off(uint address) => checked((int)(address - ExeBase));
    private static uint Word(byte[] exe, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(exe.AsSpan(Off(address)));
    private static byte[] W(params uint[] words)
    {
        byte[] b = new byte[4 * words.Length];
        for (int k = 0; k < words.Length; k++) BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(4 * k), words[k]);
        return b;
    }
}
