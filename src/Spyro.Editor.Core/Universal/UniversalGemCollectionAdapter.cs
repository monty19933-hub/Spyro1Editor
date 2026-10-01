using System.Runtime.CompilerServices;
using System.Security.Cryptography;

[assembly: InternalsVisibleTo("Spyro.Editor.UniversalGemCollectionSmoke")]

namespace Spyro.Editor.Core.Universal;

/// <summary>The unchanged engine collection routine interprets native gem class IDs directly.
/// Adapt only authenticated direct calls from a source-scoped remapped gem package. The physical
/// actor stays fixed, so the engine's collected/save-row translation still receives the same actor.</summary>
internal static class UniversalGemCollectionAdapter
{
    internal const uint CollectItem = 0x8003B9D4;
    internal const int OwnerLength = 0xFC;
    internal const uint Colours = 0x8006E47C;
    internal const int ColourLength = 20;
    internal const string OwnerSha256 = "f32d0d21dd85a992d342e7bcfd309860fdc4ba791b525872f22e336ff59c8101";
    internal const string ColourSha256 = "847d54f7009fd319644237a71af989f2e6e32cf4ce9c83bf18fa3bd8f918b7f9";

    internal static void RequireNativeOwner(byte[]? executable)
    {
        if (executable == null || executable.Length != 0x66000)
            throw new InvalidDataException("A remapped gem collection call requires the complete source-checked USA executable.");
        void Require(uint address, int length, string hash, string name)
        {
            int offset = checked((int)(address - UniversalExeBindings.ExeBase));
            if (Convert.ToHexStringLower(SHA256.HashData(executable.AsSpan(offset, length))) != hash)
                throw new InvalidDataException($"Remapped gem collection {name} differs from the certified native USA owner.");
        }
        Require(CollectItem, OwnerLength, OwnerSha256, "helper");
        Require(Colours, ColourLength, ColourSha256, "colour table");
    }

    internal static KeyValuePair<int, int>[] GemMappings(IReadOnlyDictionary<int, int> map)
    {
        var gems = map.Where(p => p.Key is >= 0x53 and <= 0x57 && p.Key != p.Value).OrderBy(p => p.Key).ToArray();
        if (gems.Length != 0 && (map.Any(p => p.Key is < 0 or >= 0x200 || p.Value is < 0 or >= 0x200) ||
            map.Values.Distinct().Count() != map.Count))
            throw new InvalidDataException("Remapped gem collection requires an unambiguous native class namespace.");
        return gems;
    }

    internal static IReadOnlyList<uint> PatchCalls(UniversalAsmPart part, UniversalOverlay source,
        IReadOnlyDictionary<int, int> map, string label, byte[]? executable)
    {
        if (GemMappings(map).Length == 0) return [];
        var calls = part.Items.Select((item, index) => (item, index)).Where(p => p.item.Word == UniversalMips.EncodeJ(3, CollectItem)).ToArray();
        if (calls.Length == 0) return [];
        RequireNativeOwner(executable);
        // Authenticate every caller before changing any instruction; copied source provenance and
        // the original JAL delay slot remain intact. Generated or already redirected calls decline.
        foreach (var (item, _) in calls)
        {
            uint? pc = item.SourcePc ?? item.Orig;
            if (pc is not uint owned || !source.Contains(owned) || source.Word(owned) != item.Word ||
                item.Fix.Kind != UniversalFixKind.None ||
                (source.Contains(owned - 4) && UniversalMips.IsBranch(source.Word(owned - 4))))
                throw new InvalidDataException("Remapped gem collection call lacks an unambiguous native source instruction owner.");
        }
        foreach (var (item, index) in calls)
            part.Items[index] = item with { Fix = new(UniversalFixKind.JalLabel, Label: label) };
        return calls.Select(p => (p.item.SourcePc ?? p.item.Orig)!.Value).Distinct().Order().ToArray();
    }

    internal static UniversalAsmPart Build(IReadOnlyDictionary<int, int> map, string label)
    {
        var gems = GemMappings(map);
        return BuildMappings(gems, label);
    }

    internal static UniversalAsmPart BuildMappings(IReadOnlyList<KeyValuePair<int, int>> gems, string label)
    {
        // Several source packages may have the same canonical gem class, but each actual
        // destination ID belongs to exactly one package and canonical value.
        if (gems.Any(p => p.Key is < 0x53 or > 0x57 || p.Value is < 0 or >= 0x200 || p.Key == p.Value) ||
            gems.Select(p => p.Value).Distinct().Count() != gems.Count)
            throw new InvalidDataException("Destination gem collection requires unique source-scoped actual classes.");
        if (gems.Count == 0) throw new InvalidDataException("A gem adapter needs at least one remapped native gem class.");
        UniversalAsmPart p = new();
        void I(int op, int rs, int rt, int imm) => p.Emit(UniversalMips.EncodeI(op, rs, rt, imm));
        p.Label(label);
        I(9, 29, 29, -48);
        // Keep the callee argument home area (sp+0..15) free.
        I(43, 29, 8, 16); I(43, 29, 9, 20); I(43, 29, 4, 24); I(43, 29, 31, 32);
        I(37, 4, 8, 0x36); p.Emit(UniversalMips.Nop); I(43, 29, 8, 28);
        for (int n = 0; n < gems.Count; n++)
        {
            I(9, 0, 9, gems[n].Value);
            p.Emit(UniversalMips.EncodeI(5, 8, 9, 0), new(UniversalFixKind.BranchLabel, Label: $"{label}_n{n}"));
            p.Emit(UniversalMips.Nop);
            I(9, 0, 9, gems[n].Key); I(41, 4, 9, 0x36);
            p.Emit(UniversalMips.EncodeJ(2, 0), new(UniversalFixKind.JumpLabel, Label: label + "_call"));
            p.Emit(UniversalMips.Nop); p.Label($"{label}_n{n}");
        }
        p.Label(label + "_call");
        I(35, 29, 8, 16); I(35, 29, 9, 20);
        p.Emit(UniversalMips.EncodeJ(3, CollectItem)); p.Emit(UniversalMips.Nop);
        // The native helper never writes class or frees/reuses the caller. Its synchronous native
        // particle12 callback allocates a separate particle pool and copies actor coordinates.
        // Save helper-result temporaries before restoring the class: v0/v1 and all other helper
        // outputs stay untouched, including the physical actor used by MarkCollected.
        I(43, 29, 8, 36); I(43, 29, 9, 40);
        I(35, 29, 8, 24); I(35, 29, 9, 28); p.Emit(UniversalMips.Nop); I(41, 8, 9, 0x36);
        I(35, 29, 8, 36); I(35, 29, 9, 40); I(35, 29, 31, 32);
        I(9, 29, 29, 48); p.Emit(UniversalMips.JrRa); p.Emit(UniversalMips.Nop);
        return p;
    }
}
