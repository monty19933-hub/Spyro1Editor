using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;

namespace Spyro.Editor.Core.Universal;

/// <summary>Private native path ownership prerequisite for the actual Alpine FD/10F retained path calls.
/// This helper does not issue selector pruning or admit the missing13B resource.</summary>
internal static class UniversalAlpineArmPathFollowing
{
    internal const uint SourceCall = 0x800875F8, Entry = 0x80039E94, End = 0x8003A16C;
    internal const string EntryLabel = "alpine_cohort_native_path", WrapperLabel = "alpine_cohort_path_shadow",
        ShadowLabel = "alpine_cohort_shadow_data", CallLabel = "alpine_cohort_source_path_call";
    private const int Frame = 80, CacheBytes = 1024;
    private sealed record Function(uint Entry, uint End, string SHA256, IReadOnlyList<uint> Words);
    private static string H(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Require(bool ok, string why)
    { if (!ok) throw new InvalidDataException("Magic native path owner: " + why); }
    private static string PC(uint pc) => $"alpine_cohort_path_pc_{pc:X8}";
    private static Function Load()
    {
        using var s = typeof(UniversalAlpineArmPathFollowing).Assembly.GetManifestResourceStream("Spyro.Editor.Core.Universal.PrivateMagicPathFollowingSource.json")
            ?? throw new InvalidDataException("Missing original native path schema");
        using var b = new MemoryStream(); s.CopyTo(b); var bytes = b.ToArray();
        Require(H(bytes) == "59e4c4b98b5e32e281d785e5e15a9e706ea5aa6256244fc3c06bcfceecb2f2d4", "complete immutable schema changed");
        var f = JsonSerializer.Deserialize<Function>(bytes) ?? throw new InvalidDataException("Invalid native path schema");
        Require(f.Entry == Entry && f.End == End && f.Words.Count * 4 == End - Entry, "whole native function extent changed");
        byte[] packet = new byte[f.Words.Count * 4];
        for (int i = 0; i < f.Words.Count; i++) BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(i * 4), f.Words[i]);
        Require(H(packet) == "87bf3554f489b3b84623027b279df666cc11c7d0b022f5dfc9fe3fde4c6c1f43" && f.SHA256 == H(packet), "whole native path packet changed");
        return f;
    }
    internal static UniversalAsmPart Build()
    {
        var f = Load(); UniversalAsmPart part = new();
        part.Label(WrapperLabel); part.Items.AddRange(Wrapper());
        part.Label(EntryLabel);
        for (int i = 0; i < f.Words.Count; i++)
        {
            uint pc = Entry + (uint)i * 4, word = f.Words[i]; part.Label(PC(pc)); UniversalFix fix = default;
            if (UniversalMips.IsPcRelative(word) || UniversalMips.Op(word) == 2)
            {
                uint target = UniversalMips.BranchTarget(word, pc);
                Require(target >= Entry && target < End, "direct path transfer leaves whole function");
                fix = new(UniversalMips.Op(word) == 2 ? UniversalFixKind.JumpLabel : UniversalFixKind.BranchLabel, Label: PC(target));
            }
            Require(!UniversalMips.IsJalr(word) && (!UniversalMips.IsJr(word) || word == UniversalMips.JrRa), "native path acquires an unowned indirect transfer");
            if (pc == 0x8003A068)
            {
                Require(word == UniversalMips.EncodeJ(3, 0x80039398), "exact inner movement call changed");
                fix = new(UniversalFixKind.JalLabel, Label: UniversalAlpineArmMovementBoundary.EntryLabel(0x80039398));
            }
            part.Emit(word, fix);
        }
        part.Label(ShadowLabel); for (int i = 0; i < CacheBytes / 4; i++) part.Emit(0);
        return part;
    }
    private static IReadOnlyList<UniversalAsmItem> Wrapper()
    {
        List<UniversalAsmItem> a = [];
        void Emit(uint word, UniversalFix fix = default) => a.Add(new(word,fix,null,null));
        uint I(int op,int rs,int rt,int imm) => UniversalMips.EncodeI(op,rs,rt,imm);
        Emit(I(9,29,29,-Frame)); Emit(I(43,29,31,76));
        void Temps(bool save) { for (int r=8;r<12;r++) Emit(I(save?43:35,29,r,40+4*(r-8))); if(!save) Emit(0); }
        Temps(true);
        // Native path has FIVE stacked arguments; the pair's two-argument movement
        // wrapper is not interchangeable. O32 argument home area remains0..15.
        for(int off=16;off<=32;off+=4) { Emit(I(35,29,8,Frame+off));Emit(0);Emit(I(43,29,8,off)); }
        void Copy(bool save)
        {
            Emit(I(15,0,8,UniversalMips.Hi(UniversalAlpineArmWorldCalls.Cache)));
            Emit(I(9,8,8,UniversalMips.Lo(UniversalAlpineArmWorldCalls.Cache)));
            Emit(I(15,0,9,0),new(UniversalFixKind.LabelAddrHi,Label:ShadowLabel));
            Emit(I(9,9,9,0),new(UniversalFixKind.LabelAddrLo,Label:ShadowLabel));
            Emit(I(9,0,10,CacheBytes/4));int start=a.Count;
            Emit(I(35,save?8:9,11,0)); Emit(I(9,save?8:9,save?8:9,4));
            Emit(I(43,save?9:8,11,0)); Emit(I(9,save?9:8,save?9:8,4));
            Emit(I(9,10,10,-1)); Emit(I(5,10,0,start-a.Count-1)); Emit(0);
        }
        Copy(true);Temps(false);Emit(UniversalMips.EncodeJ(3,Entry),new(UniversalFixKind.JalLabel,Label:EntryLabel));
        Emit(0);Emit(0);Temps(true);Copy(false);Temps(false);Emit(I(35,29,31,76));Emit(0);
        Emit(I(9,29,29,Frame));Emit(UniversalMips.JrRa);Emit(0);
        Require(a.Count==67,"complete five-argument path wrapper extent changed");return a;
    }
}
