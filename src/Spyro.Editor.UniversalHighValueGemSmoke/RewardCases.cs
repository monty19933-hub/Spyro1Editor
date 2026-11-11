using System.Buffers.Binary;
using System.Text.Json;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Universal;

/// <summary>Teal and rainbow rewards without a disc: the reward bytes, the three added routines run on a test CPU over a
/// made-up level (placed rows, spawned gems, chests that paid and did not), the catalogue entries, the saved placement
/// and the editor's treasure count.</summary>
internal static class RewardCases
{
    const uint Placed = 0x80100000, Code = 0x80090000, SpawnStub = 0x80098000;
    const uint CollectItem = 0x8003B9D4, OrientMoby = 0x800529E4, MarkCollected = 0x8003B854;
    const int Teal = UniversalHighValueGems.TealClass, Rainbow = UniversalHighValueGems.RainbowClass;

    internal static void Run(Action<bool, string> check)
    {
        // Reward bytes: bit 7 over the gold and purple gems the game makes in their place.
        check(UniversalHighValueRewards.TealReward == (0x80 | UniversalHighValueRewards.TealStandInClass) &&
              UniversalHighValueRewards.RainbowReward == (0x80 | UniversalHighValueRewards.RainbowStandInClass), "reward bytes are the stand-in gems with bit 7");
        check(UniversalHighValueRewards.TealStandInClass == GemValue.Yellow.IdByte && UniversalHighValueRewards.RainbowStandInClass == GemValue.Purple.IdByte, "stand-ins: gold and purple");
        for (int b = 0; b < 256; b++)
        {
            int value = UniversalHighValueRewards.ValueFor(b);
            check(value == (b == 0xD6 ? 50 : b == 0xD7 ? 100 : 0) && UniversalHighValueRewards.IsReward(b) == value > 0, "only 0xD6 and 0xD7 are high-value rewards");
            check(!UniversalRewardChoice.IsGem(b) || !UniversalHighValueRewards.IsReward(b), "a high-value reward is not one of the five gems");
            check(GemValue.TryFromRewardByte(b, out var gem) == (UniversalRewardChoice.IsGem(b) || value > 0) && (value == 0 || gem.Value == value), "reward byte to gem");
        }
        check(GemValue.Teal.RewardByte == 0xD6 && GemValue.Rainbow.RewardByte == 0xD7 && GemValue.Known.All(g => g.RewardByte == g.IdByte) && GemValue.Unknown.RewardByte == -1, "gem to reward byte");
        check(UniversalHighValueRewards.GemClassFor(0xD6) == Teal && UniversalHighValueRewards.GemClassFor(0xD7) == Rainbow &&
              UniversalHighValueRewards.RewardForGemClass(Teal) == 0xD6 && UniversalHighValueRewards.RewardForGemClass(Rainbow) == 0xD7, "reward byte and gem class");
        for (int c = 0; c < 0x200; c++) check(UniversalHighValueRewards.Supports(c) == (c is 0xC7 or 0x149), "only spring chests carry a high-value reward");
        check(UniversalHighValueRewards.Tints.Length == 6 && UniversalHighValueRewards.Tints.Skip(1).SequenceEqual(new uint[] { 0x00A30000, 0x00A00C00, 0x00A00030, 0x00A30C00, 0x00A30030 }), "lid tints: the game's five, then teal at entry 0");
        Routines(check);
        Catalogue(check);
        Saved(check);
        Editor(check);
    }

    static uint Row(int i) => Placed + (uint)(i * 0x58);

    static void Routines(Action<bool, string> check)
    {
        var parts = new[] { UniversalSpecialBehaviors.HighValueRewardPre(), UniversalSpecialBehaviors.HighValueRewardUpdate([0x149, 0xC7]), UniversalSpecialBehaviors.HighValueRewardCollect() };
        var (code, labels) = UniversalBehaviorBundleBuilder.Assemble(parts, Code);
        check(code.Length <= 0x340, $"the three routines stay small ({code.Length} bytes)");
        check(UniversalBehaviorBundleBuilder.Assemble([UniversalSpecialBehaviors.HighValueRewardUpdate([])], Code).Code.Length < code.Length, "no tint scan without tinted classes");
        bool Refused(Action a) { try { a(); return false; } catch (InvalidDataException) { return true; } }
        check(Refused(() => UniversalSpecialBehaviors.HighValueRewardUpdate([0x149, 0x149])) && Refused(() => UniversalSpecialBehaviors.HighValueRewardUpdate([0x200])), "bad tint class lists refused");
        uint pre = labels[UniversalSpecialBehaviors.HighValueRewardPreLabel], update = labels[UniversalSpecialBehaviors.HighValueRewardUpdateLabel], collect = labels[UniversalSpecialBehaviors.HighValueRewardCollectLabel];

        RewardCpu Level(out uint[] gems)
        {
            var cpu = new RewardCpu(); cpu.Load(Code, code);
            // Placed rows: four spring chests (teal paid, rainbow paid, blue paid, teal not paid), a Gnorc Cove chest with
            // a teal reward that is no longer alive, and a basket with byte 0xD6 that has paid.
            (int Class, int Reward, bool Paid, int State)[] placed = [(0x149, 0xD6, true, 1), (0x149, 0xD7, true, 1), (0x149, 0x55, true, 1), (0x149, 0xD6, false, 0), (0xC7, 0xD6, true, -3), (0xC2, 0xD6, true, 1)];
            for (int i = 0; i < placed.Length; i++)
            {
                uint r = Row(i); cpu.W16(r + 0x36, placed[i].Class); cpu.W8(r + 0x53, placed[i].Reward); cpu.W8(r + 0x3A, 0x7D | (placed[i].Paid ? 0x80 : 0));
                cpu.W8(r + 0x48, placed[i].State); cpu.W32(r + 0x4C, 0x12345678); cpu.W32(r, 0x80110000 + (uint)(i * 0x10));
            }
            uint spawned = Row(placed.Length);
            cpu.W32(0x80075828, Placed); cpu.W32(0x80075890, spawned); cpu.W32(UniversalHighValueGems.FrameStep, 2);
            cpu.W32(0x800758E4, SpawnStub);
            // Spawned: gems made by rows 0..5, a freed slot, a gold gem from row 0 that row 0 does not hold, the terminator.
            (int Class, int Parent, int State)[] list = [(0x56, 0, 0), (0x57, 1, 0), (0x55, 2, 0), (0x56, 3, 0), (0x56, 4, 0), (0x56, 5, 0), (0x56, 0, -2), (0x56, 0, 0)];
            gems = new uint[list.Length];
            for (int i = 0; i < list.Length; i++)
            {
                uint g = spawned + (uint)(i * 0x58); gems[i] = g;
                cpu.W16(g + 0x36, list[i].Class); cpu.W8(g + 0x56, list[i].Parent); cpu.W8(g + 0x48, list[i].State); cpu.W8(g + 0x53, 0xFF); cpu.W8(g + 0x4F, 4);
                if (i < placed.Length) cpu.W32(0x80110000 + (uint)(i * 0x10), g);       // each chest holds the gem it made
            }
            cpu.W8(spawned + (uint)(list.Length * 0x58) + 0x48, 0xFF);
            foreach (uint stub in new[] { CollectItem, OrientMoby, MarkCollected, SpawnStub }) cpu.Stubs[stub] = _ => { };
            return cpu;
        }

        var cpu = Level(out var g);
        for (int r = 16; r < 24; r++) cpu.Regs[r] = 0x5A5A0000u + (uint)r;
        cpu.Regs[29] = 0x801FFF00;
        cpu.Call(update);
        int entry = UniversalHighValueGems.RainbowEntry(4);
        check(cpu.U16(g[0] + 0x36) == Teal && cpu.U8(g[0] + 0x53) == 0xD6 && cpu.U8(g[0] + 0x4F) == UniversalHighValueGems.TealEntry, "a teal chest's gem is marked and shown as the teal gem");
        check(cpu.U16(g[1] + 0x36) == Rainbow && cpu.U8(g[1] + 0x53) == 0xD7 && cpu.U8(g[1] + 0x4F) == entry, "a rainbow chest's gem is marked and walks the rainbow's hues");
        check(cpu.U16(g[2] + 0x36) == 0x55 && cpu.U8(g[2] + 0x53) == 0xFF, "an ordinary chest's gem is left alone");
        check(cpu.U16(g[3] + 0x36) == 0x56 && cpu.U8(g[3] + 0x53) == 0xFF, "a gem from a chest that has not paid is left alone");
        check(cpu.U16(g[4] + 0x36) == 0x56 && cpu.U8(g[4] + 0x53) == 0xFF, "a gem from a chest no longer alive is left alone");
        check(cpu.U16(g[6] + 0x36) == 0x56 && cpu.U16(g[7] + 0x36) == 0x56 && cpu.U8(g[7] + 0x53) == 0xFF, "a freed slot and a gem the chest does not hold are left alone");
        check(cpu.U16(g[5] + 0x36) == Teal, "the routine trusts the reward byte alone for the class of the payer (the editor gives it only to spring chests)");
        check(cpu.U32(Row(0) + 0x4C) == UniversalHighValueRewards.Tints[0] && cpu.U32(Row(3) + 0x4C) == UniversalHighValueRewards.Tints[0], "teal chests' lids are tinted teal, paid or not");
        check(cpu.U32(Row(1) + 0x4C) == UniversalHighValueRewards.Tints[entry], "the rainbow chest's lid follows its hue");
        check(cpu.U32(Row(2) + 0x4C) == 0x12345678 && cpu.U32(Row(4) + 0x4C) == 0x12345678 && cpu.U32(Row(5) + 0x4C) == 0x12345678, "other chests' lids are left alone (blue, freed, untinted class)");
        check(Enumerable.Range(16, 8).All(r => cpu.Regs[r] == 0x5A5A0000u + (uint)r) && cpu.Regs[29] == 0x801FFF00, "the update keeps saved registers and the stack");
        cpu.Call(pre);
        check(cpu.U16(g[0] + 0x36) == 0x56 && cpu.U16(g[1] + 0x36) == 0x57 && cpu.U8(g[0] + 0x53) == 0xD6 && cpu.U8(g[1] + 0x53) == 0xD7, "before the game's code runs the gems are gold and purple again, still marked");
        check(cpu.U16(g[5] + 0x36) == 0x56 && cpu.U16(g[2] + 0x36) == 0x55, "the restore touches only high-value classes");
        check(Enumerable.Range(16, 8).All(r => cpu.Regs[r] == 0x5A5A0000u + (uint)r) && cpu.Regs[29] == 0x801FFF00, "the restore keeps saved registers and the stack");
        cpu.W8(Row(0) + 0x3A, 0x7D);                                     // the chest closed again (its paid bit cleared)
        cpu.Call(update);
        check(cpu.U16(g[0] + 0x36) == Teal, "a marked gem stays teal once its chest closes");
        int phase = (int)cpu.U32(labels[UniversalSpecialBehaviors.HighValueRewardUpdateLabel + "_phase"]);
        check(phase == 8, "the hue phase steps two frame steps per update");
        // Placed rows never change class or byte.
        check(Enumerable.Range(0, 6).All(i => cpu.U16(Row(i) + 0x36) is 0x149 or 0xC7 or 0xC2), "placed rows keep their classes");

        // Collection: a marked spawned gem is awarded 50 or 100 by the game's own award routine, after the steps the game's
        // collection takes; everything else goes on to the game's collection routine unchanged.
        foreach (var (gem, award, colour) in new[] { (0, 50u, 0x2C808000u), (1, 100u, 0x2C808080u) })
        {
            cpu = Level(out g); cpu.Call(update); cpu.Calls.Clear();
            cpu.W32(0x80075758, g[gem]); cpu.W32(0x80075830, 0);
            for (int r = 16; r < 24; r++) cpu.Regs[r] = 0x5A5A0000u + (uint)r;
            cpu.Regs[29] = 0x801FFF00; cpu.Regs[4] = g[gem];
            cpu.Call(collect);
            check(cpu.Calls.Count == 3 && cpu.Calls[0] == (OrientMoby, g[gem], 4, cpu.Calls[0].A2, cpu.Calls[0].A3) &&
                  cpu.Calls[1] == (SpawnStub, 6, 12, g[gem], colour) && cpu.Calls[2] == (MarkCollected, award, g[gem], cpu.Calls[2].A2, cpu.Calls[2].A3),
                  $"a {(gem == 0 ? "teal" : "rainbow")} gem: turned, six stars in its colour, {award} awarded for it");
            check(!cpu.Calls.Any(c => c.At == CollectItem), "the game's collection is not called for it");
            check(cpu.U32(0x80075830) == 3, "Sparx lets go of the gem he was fetching");
            check(Enumerable.Range(16, 8).All(r => cpu.Regs[r] == 0x5A5A0000u + (uint)r) && cpu.Regs[29] == 0x801FFF00, "collection keeps saved registers and the stack");
        }
        foreach (var (what, at, mark) in new[] { ("an ordinary gem", 2, -1), ("a gem from a chest that never paid", 3, -1), ("a placed row with byte 0xD6", -1, -1), ("a gem marked with another byte", 6, 0x57) })
        {
            cpu = Level(out g); cpu.Call(update); cpu.Calls.Clear();
            uint a0 = at < 0 ? Row(0) : g[at];
            if (mark >= 0) cpu.W8(a0 + 0x53, mark);
            cpu.Regs[4] = a0; cpu.Call(collect);
            check(cpu.Calls.Count == 1 && cpu.Calls[0].At == CollectItem && cpu.Calls[0].A0 == a0, what + " goes to the game's collection routine");
        }
        // A damaged list without a terminator still ends.
        cpu = Level(out g); uint end = cpu.U32(0x80075890) + 8 * 0x58; cpu.W8(end + 0x48, 0);
        for (uint p = end; p < end + 600 * 0x58; p += 0x58) cpu.W8(p + 0x48, 0);
        cpu.Call(update); cpu.Call(pre);
        check(true, "both scans stop after their bound");
        // Every call to the game's collection in added code goes through the routine; calls already taken over are left.
        var part = new UniversalAsmPart(); part.Emit(UniversalMips.EncodeJ(3, CollectItem)); part.Emit(0);
        part.Emit(UniversalMips.EncodeJ(3, CollectItem), new(UniversalFixKind.JalLabel, Label: "other")); part.Emit(0);
        check(UniversalSpecialBehaviors.RedirectGemCollection([part]) == 1 && part.Items[0].Fix.Label == UniversalSpecialBehaviors.HighValueRewardCollectLabel && part.Items[2].Fix.Label == "other", "added code's collection calls redirected once");
        byte[] overlay = new byte[0x100];
        BinaryPrimitives.WriteUInt32LittleEndian(overlay.AsSpan(0x10), UniversalMips.EncodeJ(3, CollectItem));
        BinaryPrimitives.WriteUInt32LittleEndian(overlay.AsSpan(0x20), UniversalMips.EncodeJ(3, 0x80099000));
        check(UniversalHighValueRewards.Apply(overlay, [UniversalOverlay.Base + 0x10, UniversalOverlay.Base + 0x20], 0x80090100) == 1 &&
              BinaryPrimitives.ReadUInt32LittleEndian(overlay.AsSpan(0x10)) == UniversalMips.EncodeJ(3, 0x80090100) &&
              BinaryPrimitives.ReadUInt32LittleEndian(overlay.AsSpan(0x20)) == UniversalMips.EncodeJ(3, 0x80099000), "the level's own calls redirected, others untouched");
    }

    static void Catalogue(Action<bool, string> check)
    {
        UniversalMobyVariant V(int cls, string level, int row) => new(cls, level, row, 16, 2000, 3000, true, false, true);
        var spring = new UniversalMobyCatalogEntry(0x149, "Spring chest (Yellow reward)", "Interactive", "Chests & Containers", [V(0x149, "townsquare", 82), V(0x149, "drycanyon", 38)], V(0x149, "townsquare", 82), ["townsquare", "drycanyon"], null);
        var cove = new UniversalMobyCatalogEntry(0xC7, "Spring chest (Blue reward)", "Interactive", "Chests & Containers", [V(0xC7, "gnorccove", 46)], V(0xC7, "gnorccove", 46), ["gnorccove"], null);
        var basket = new UniversalMobyCatalogEntry(0xC2, "Basket", "Interactive", "Chests & Containers", [V(0xC2, "artisans", 1)], V(0xC2, "artisans", 1), ["artisans"], null);
        check(UniversalHighValueRewards.CatalogEntries([basket]).Count == 0, "no spring chest, no entries");
        var made = UniversalHighValueRewards.CatalogEntries([basket, cove, spring]);
        check(made.Count == 2 && made[0].Id == "chest:spring-teal" && made[1].Id == "chest:spring-rainbow", "stable ids chest:spring-teal and chest:spring-rainbow");
        check(made[0].Name == "Spring chest (Teal reward)" && made[1].Name == "Spring chest (Rainbow reward)" && made.All(e => e.Category == "Interactive" && e.SubCategory == "Chests & Containers"), "listed with the chests, named like them");
        check(made.All(e => e.Variants.Count == 3 && e.Default.SourceLevelKey == "townsquare" && e.ClassId == 0x149 && e.SourceLevels.Count == 3), "every spring chest row of both kinds, the common one first");
        check(made[0].Variants.All(v => v.RewardByte == 0xD6) && made[1].Variants.All(v => v.RewardByte == 0xD7) && made[0].Default.RewardByte == 0xD6, "each version gives the gem");
        check(!made[0].Variants[0].Equals(spring.Variants[0]) && made[0].Variants[0] with { RewardByte = null } == spring.Variants[0], "a reward version is its own key in the browser's caches");
        check(UniversalHighValueRewards.FindEntry([spring, .. made], "chest:spring-rainbow") == made[1], "found by id");
        check(made.All(e => e.Notes!.Contains("springs up", StringComparison.Ordinal)), "the browser explains how to get the gem");
        check(UniversalHighValueRewards.CatalogEntries([.. made, spring]).Count == 2, "editor entries are not made from editor entries");
    }

    static void Saved(Action<bool, string> check)
    {
        string Edit(string template, string flag, string? chosen) => $$"""
            {"added": true, "removed": false, "flag4BEditedHex": "{{flag}}", {{(chosen == null ? "" : $"\"{UniversalPlacementReader.RewardByteField}\": \"{chosen}\",")}}
             "crossLevelTemplate": {"id": "{{template}}", "sourceLevelKey": "townsquare", "sourceTrueIndex": 82, "requiredExporterFeature": "{{UniversalMobyExport.RequiredExporterFeature}}"},
             "rawEdited": {"x": 100, "y": 200, "z": 300}, "universalPlacementId": "0123456789abcdef0123456789abcdef"}
            """;
        using var teal = JsonDocument.Parse(Edit("universal:townsquare:82", "0xD6", null));
        using var chosen = JsonDocument.Parse(Edit("universal:townsquare:82", "0x55", "0xD7"));
        using var plain = JsonDocument.Parse(Edit("universal:townsquare:82", "0x55", null));
        using var gem = JsonDocument.Parse(Edit("special:teal-gem-50", "0xD6", null));
        check(UniversalPlacementReader.RewardByteOf(teal.RootElement) == 0xD6 && UniversalPlacementReader.RewardByteOf(chosen.RootElement) == 0xD7 && UniversalPlacementReader.RewardByteOf(plain.RootElement) == null, "what an added object gives, as saved");
        check(UniversalPlacementReader.KindOf(teal.RootElement) == "chest:spring-teal" && UniversalPlacementReader.KindOf(chosen.RootElement) == "chest:spring-rainbow" &&
              UniversalPlacementReader.KindOf(plain.RootElement) == "universal:townsquare:82" && UniversalPlacementReader.KindOf(gem.RootElement) == "special:teal-gem-50", "a teal or rainbow chest is its own kind (Fix It)");
        string path = Path.Combine(Path.GetTempPath(), "reward-placement-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(path, $$"""{"edits": [{{Edit("universal:townsquare:82", "0xD6", null)}}]}""");
            var read = UniversalPlacementReader.Read(path);
            check(read.Count == 1 && read[0].SpecialId == null && read[0].RowByteOverrides![0x53] == 0xD6 && read[0].SourceLevelKey == "townsquare" && read[0].SourceRowIndex == 82, "Create BIN writes the reward into the copied chest's row");
        }
        catch (Exception e) when (e is InvalidDataException or KeyNotFoundException or JsonException) { check(false, "saved reward placement: " + e.Message); }
        finally { File.Delete(path); }
    }

    static void Editor(Action<bool, string> check)
    {
        Moby Chest(int cls, int reward) => new() { SourceByte36 = cls & 0xFF, SourceByte37 = cls >> 8, Flag4B = reward, Type = 0x20, Flag4A = 0xFF, Label = "Spring chest" };
        check(Chest(0x149, 0xD6).TreasureValue == 50 && Chest(0x149, 0xD7).TreasureValue == 100 && Chest(0xC7, 0xD7).TreasureValue == 100, "the editor counts a teal or rainbow chest");
        check(Chest(0x149, 0x56).TreasureValue == 10 && Chest(0xC2, 0xD6).TreasureValue == 0, "other chests as before; a high-value byte elsewhere counts nothing");
        check(Chest(0x149, 0xD6).RewardGem == GemValue.Unknown && Chest(0x149, 0xD6).GivesHighValueGem && !Chest(0xC2, 0xD6).GivesHighValueGem, "the five-gem reward choice does not take it");
        int[] rows = [0x149, 0xC7];
        foreach (int cls in rows)
            foreach (byte b in new byte[] { 0xD6, 0xD7 })
            {
                byte[] row = new byte[0x58]; BinaryPrimitives.WriteUInt16LittleEndian(row.AsSpan(0x36), (ushort)cls); row[0x50] = 0x20; row[0x53] = b;
                check(UniversalMobyComposer.TreasureValue(row) == UniversalHighValueRewards.ValueFor(b), "Create BIN counts the chest in the level's treasure target");
            }
    }
}
