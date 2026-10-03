using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Spyro.Editor.Core.Universal;

public sealed record UniversalMagicPairShadowCall(uint SourceCall, uint NativeTarget, uint CopiedCall, uint Wrapper);
public sealed record UniversalMagicPairShadowReadback(uint Base, uint Shadow, int ShadowBytes,
    int StackBytes, IReadOnlyList<UniversalMagicPairShadowCall> Calls, string CodeSHA256)
{
    public int CodeBytes { get; init; }
    public UniversalMagicMovementBoundaryReadback? MovementBoundary { get; init; }
    public string Status { get; init; } = "PARAMETERIZED_MAGIC12_13_DH_SOURCE_CERTIFICATE_PRIVATE_REVIEW";
    public string Pending { get; init; } = "Source/native issuer is restricted to exact original pair12/13, original Dark Hollow assets, source-derived common translations/native yaw/pod/scene and independently relocated compiled owners; other Magic selectors and13B routes retain refusal. Edited/cleared hosts and native tests for different semantic placements remain separate.";
}

/// <summary>PRIVATE experiment: exact DH plus original Magic12/13 only. Never a product13B pruning issuer.</summary>
internal static class UniversalMagicPairCollisionShadow
{
    internal const string ShadowLabel = "private_magic_pair_shadow_data";
    internal const uint Cache = 0x800700F4;
    internal const int ShadowBytes = 1024, Frame = 64;
    internal static readonly (uint Call, uint Target)[] Calls = [(0x800877D0, 0x80039398), (0x80087C84, 0x80039910), (0x80088340, 0x80039398)];
    private sealed record Fixture(IReadOnlyList<UniversalAsmItem> Items, IReadOnlyDictionary<string, IReadOnlyList<uint>> Tables);
    private static string Hash(byte[] b) => Convert.ToHexStringLower(SHA256.HashData(b));
    private static void Require(bool ok, string message) { if (!ok) throw new InvalidDataException("Private Magic pair shadow: " + message); }
    private static string CallLabel(uint pc) => $"private_magic_pair_call_{pc:X8}";
    private static string WrapLabel(uint pc) => $"private_magic_pair_wrap_{pc:X8}";
    internal static void RequireScenario(string destination, byte[] data, byte[] overlay, byte[] exe,
        UniversalLevelData source, UniversalOverlay sourceOverlay, IReadOnlyList<UniversalMobyPlacement> placements, IReadOnlyCollection<int>? removals)
    {
        Require(destination == "darkhollow" && (removals == null || removals.Count == 0), "Use an untouched Dark Hollow level for this druid group; other destinations or native removals are not yet supported");
        Require(Hash(data) == "a937aecb3626ffd2311ba136684a34c9f36877054b52557b4524fcacee0e2d97" &&
            Hash(overlay) == "9920aa47b94005166f457b5b36df6dedd1f2400f91698bac21cdb01d8590ef32", "Use an untouched Dark Hollow level for this druid group; existing native-object, terrain or asset edits are not yet supported");
        Require(Hash(source.Bytes) == "ad34af2e3401514e7db740691c80fd2bbf59057a55bdfedbaaa39b6939141fcb", "complete original source data changed");
        RequireSource(sourceOverlay, exe);
        // Full ledger is checked before inactive entries are discarded. Its
        // metadata cannot execute, rebind a physical row or reuse a saved identity.
        if(placements.Any(p=>p.PersistentSlot!=null))
            Require(placements.All(p=>p.PersistentSlot!=null&&!string.IsNullOrWhiteSpace(p.EditorId))&&placements.Select(p=>p.EditorId).Distinct(StringComparer.Ordinal).Count()==placements.Count&&placements.Select(p=>p.PersistentSlot!.Value).Order().SequenceEqual(Enumerable.Range(0,placements.Count)),"persistent ledger requires unique IDs and contiguous lifetime slots; deleted identities cannot be reused");
        else Require(placements.All(p=>!p.Retired),"retired entries require their persistent lifetime slots");
        placements=placements.Where(p=>!p.Retired).ToArray();
        int nativePrefix=new UniversalLevelData(data).Scene().RowCount;
        Require(placements.Select(p=>p.PersistentSlot).Where(n=>n!=null).All(n=>n>=0&&n<256-nativePrefix),"druid lifetime identities exceed Dark Hollow's native 256-bit saved bitmap; retirements cannot be reused");
        Require(placements.Count == 2 && placements.All(p => p.SourceLevelKey == "magiccrafters") && placements.Select(p => p.SourceRowIndex).Order().SequenceEqual(new[] {12,13}), "exact original pair required, no duplicate/mixed/source row binding");
        var a = placements.Single(p => p.SourceRowIndex == 12); var b = placements.Single(p => p.SourceRowIndex == 13);
        Require(!string.IsNullOrWhiteSpace(a.LinkGroupId) && a.LinkGroupId == b.LinkGroupId && a.EditorId != b.EditorId && !string.IsNullOrWhiteSpace(a.EditorId) && !string.IsNullOrWhiteSpace(b.EditorId), "exact one unique authored group required");
        long[] da={ (long)a.X-BinaryPrimitives.ReadInt32LittleEndian(source.Row(12).AsSpan(12)), (long)a.Y-BinaryPrimitives.ReadInt32LittleEndian(source.Row(12).AsSpan(16)), (long)a.Z-BinaryPrimitives.ReadInt32LittleEndian(source.Row(12).AsSpan(20)) };
        long[] db={ (long)b.X-BinaryPrimitives.ReadInt32LittleEndian(source.Row(13).AsSpan(12)), (long)b.Y-BinaryPrimitives.ReadInt32LittleEndian(source.Row(13).AsSpan(16)), (long)b.Z-BinaryPrimitives.ReadInt32LittleEndian(source.Row(13).AsSpan(20)) };
        Require(da.All(v=>v>=int.MinValue&&v<=int.MaxValue),"native signed32 common translation overflow");
        Require(da.SequenceEqual(db),"authored group must retain original common XYZ translation, relative pose and native yaw");
        foreach(var p in placements)
        {
            var original=source.CaptureRow("magiccrafters",p.SourceRowIndex);
            foreach(int at in new[]{12,16})Require((at==12?p.X:p.Y)>=UniversalMagicPairMovementBoundary.Minimum&&(at==12?p.X:p.Y)<UniversalMagicPairMovementBoundary.MaximumExclusive,"initial actor XY leaves retained native neighbor grid");
            foreach(int at in new[]{4,0x58})for(int axis=0;axis<3;axis++)
            {
                long value=BinaryPrimitives.ReadInt32LittleEndian(original.Properties.AsSpan(at+4*axis))+da[axis];
                Require(value>=int.MinValue&&value<=int.MaxValue,"rebased home/path coordinate overflows native signed32 storage");
                if(axis<2)Require(value>=UniversalMagicPairMovementBoundary.Minimum&&value<UniversalMagicPairMovementBoundary.MaximumExclusive,"rebased home/path XY leaves retained native neighbor grid");
            }
        }
        Require(placements.All(p => !p.Retired && (p.Yaw == null || p.Yaw == source.Row(p.SourceRowIndex)[0x46]) && p.RowByteOverrides == null && p.Text == null && p.SpecialId == null && p.DragonColor == null), "no unproved row/selector/frame/pod/class overrides");
    }
    private static void RequireSource(UniversalOverlay source, byte[] exe, bool allowOwnedTreasureDelta = false)
    {
        Require(Hash(source.Bytes.ToArray()) == "98e135e18876ea094ca9484b33cfb38438e2b99b72ee4d6d66e907722c744a1d", "complete original source overlay changed");
        // Exact complete original image, or the independently read-back complete
        // same image with only DH treasure halfword8006F43C:100->107. The exact
        // source12/13 drop fields54/55 own delta2+5. No native code is omitted.
        string hash = Hash(exe);
        Require(hash == "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998" ||
            allowOwnedTreasureDelta && hash == "eeed983e527288f5ad508ecdb49a71a89c88860dc2b0a3250f110340bc43b8f7", "complete native executable/owned pair treasure delta changed");
    }
    internal static void RequireCopiedOwner(UniversalAsmPart part)
    {
        using var stream = typeof(UniversalMagicPairCollisionShadow).Assembly.GetManifestResourceStream("Spyro.Editor.Core.Universal.PrivateMagicPairSourcePart.json") ?? throw new InvalidDataException("Missing private copied source fixture");
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true }; options.Converters.Add(new JsonStringEnumConverter());
        var expected = JsonSerializer.Deserialize<Fixture>(stream, options) ?? throw new InvalidDataException("Invalid private source fixture");
        Require(part.Items.SequenceEqual(expected.Items), "complete copied preimage/producer/label/incoming schema changed");
        Require(part.Tables.Count == expected.Tables.Count && expected.Tables.All(k => part.Tables.TryGetValue(k.Key, out var v) && v.SequenceEqual(k.Value)), "complete copied native table/target namespace changed");
    }
    internal static UniversalAsmPart Apply(UniversalAsmPart part, UniversalOverlay source, byte[] exe)
    {
        RequireSource(source, exe, true); RequireCopiedOwner(part);
        foreach (var (pc, native) in Calls)
        {
            int at = part.Items.FindIndex(i => i.Orig == pc);
            Require(at >= 0 && part.Items[at].Word == UniversalMips.EncodeJ(3, native), "owned native call preimage missing");
            part.Items[at] = part.Items[at] with { Fix = new(UniversalFixKind.JalLabel, Label: WrapLabel(pc)) };
            part.Items.Insert(at, new(null, default, null, CallLabel(pc)));
        }
        UniversalAsmPart wrapper = new();
        foreach (var (pc, native) in Calls)
        {
            wrapper.Label(WrapLabel(pc));
            foreach (var item in Build(native)) wrapper.Items.Add(item);
        }
        wrapper.Items.AddRange(UniversalMagicPairMovementBoundary.Build().Items);
        wrapper.Label(ShadowLabel); for (int i = 0; i < ShadowBytes / 4; i++) wrapper.Emit(0);
        return wrapper;
    }
    private static IReadOnlyList<UniversalAsmItem> Build(uint native)
    {
        List<UniversalAsmItem> items = [];
        void Emit(uint w, UniversalFix fix = default) => items.Add(new(w, fix, null, null));
        uint I(int op,int s,int t,int n) => UniversalMips.EncodeI(op,s,t,n);
        Emit(I(9,29,29,-Frame)); Emit(I(43,29,31,60));
        void Temps(bool save) { for (int r = 8; r < 12; r++) Emit(I(save?43:35,29,r,32+4*(r-8))); if (!save) Emit(0); }
        Temps(true);
        foreach (int off in new[] {16,20}) { Emit(I(35,29,8,Frame+off)); Emit(0); Emit(I(43,29,8,off)); }
        void Loop(bool save)
        {
            Emit(I(15,0,8,UniversalMips.Hi(Cache))); Emit(I(9,8,8,UniversalMips.Lo(Cache)));
            Emit(I(15,0,9,0),new(UniversalFixKind.LabelAddrHi,Label:ShadowLabel));
            Emit(I(9,9,9,0),new(UniversalFixKind.LabelAddrLo,Label:ShadowLabel));
            Emit(I(9,0,10,ShadowBytes/4)); int start = items.Count;
            Emit(I(35,save?8:9,11,0)); Emit(I(9,save?8:9,save?8:9,4)); Emit(I(43,save?9:8,11,0)); Emit(I(9,save?9:8,save?9:8,4)); Emit(I(9,10,10,-1)); Emit(I(5,10,0,start-items.Count-1)); Emit(0);
        }
        Loop(true); Temps(false); Emit(UniversalMips.EncodeJ(3,native),new(UniversalFixKind.JalLabel,Label:UniversalMagicPairMovementBoundary.EntryLabel(native))); Emit(0); Emit(0); Temps(true); Loop(false); Temps(false);
        Emit(I(35,29,31,60)); Emit(0); Emit(I(9,29,29,Frame)); Emit(UniversalMips.JrRa); Emit(0);
        Require(items.Count == 58, "unexpected complete wrapper size"); return items;
    }
    internal static void RequireReadback(byte[] overlay, UniversalMagicPairShadowReadback proof)
    {
        Require(proof.CodeBytes > 0 && proof.Base >= UniversalOverlay.Base && (ulong)proof.Base + (uint)proof.CodeBytes <= (ulong)UniversalOverlay.Base + (uint)overlay.Length, "written bundle allocation missing");
        byte[] code = overlay.AsSpan(checked((int)(proof.Base - UniversalOverlay.Base)), proof.CodeBytes).ToArray();
        Require(proof.MovementBoundary != null,"written movement boundary proof missing");
        UniversalMagicPairMovementBoundary.RequireReadback(overlay,proof.MovementBoundary!);
        Require(Hash(code) == proof.CodeSHA256, "entire authenticated compiled-source/callback/data allocation changed after assembly");
        Dictionary<string,uint> labels = new() { [ShadowLabel] = proof.Shadow };
        foreach(var c in proof.Calls) { labels[CallLabel(c.SourceCall)] = c.CopiedCall; labels[WrapLabel(c.SourceCall)] = c.Wrapper; }
        var (_,nativeLabels) = UniversalBehaviorBundleBuilder.Assemble([UniversalMagicPairMovementBoundary.Build()],proof.MovementBoundary!.Base);
        foreach(var p in nativeLabels) labels[p.Key]=p.Value;
        var actual = RequireWritten(code, labels, proof.Base);
        Require(actual.Shadow == proof.Shadow && actual.Calls.SequenceEqual(proof.Calls), "written call/group shadow ownership changed");
    }
    internal static UniversalMagicPairShadowReadback RequireWritten(byte[] code, IReadOnlyDictionary<string,uint> labels, uint baseAddress)
    {
        Require(labels.TryGetValue(ShadowLabel,out uint shadow) && shadow%4==0 && shadow>=baseAddress && (ulong)shadow+ShadowBytes <= (ulong)baseAddress+(uint)code.Length, "private shadow lacks complete aligned behavior-allocation owner");
        uint Word(uint at) { Require(at>=baseAddress && (ulong)at+4 <= (ulong)baseAddress+(uint)code.Length,"readback leaves complete allocation"); return BinaryPrimitives.ReadUInt32LittleEndian(code.AsSpan(checked((int)(at-baseAddress)))); }
        Require(code.AsSpan(checked((int)(shadow-baseAddress)),ShadowBytes).IndexOfAnyExcept((byte)0)<0,"private shadow initialization changed");
        List<UniversalMagicPairShadowCall> calls = [];
        Dictionary<uint,uint> incoming = [];
        foreach (var (pc,native) in Calls)
        {
            bool hasCall=labels.TryGetValue(CallLabel(pc),out uint call); bool hasEntry=labels.TryGetValue(WrapLabel(pc),out uint entry);
            Require(hasCall && hasEntry,"private copied call/wrapper labels missing");
            Require(Word(call)==UniversalMips.EncodeJ(3,entry),"copied call does not bind its own exact wrapper");
            var items=Build(native); Require((ulong)entry+(uint)items.Count*4 <= shadow,"wrapper overlaps owned shadow data");
            for(int i=0;i<items.Count;i++)
            {
                uint expected=items[i].Word!.Value;
                if(items[i].Fix.Kind==UniversalFixKind.JalLabel) { expected=UniversalMips.WithJumpTarget(expected,labels[items[i].Fix.Label!]); incoming[entry+(uint)i*4]=labels[items[i].Fix.Label!]; }
                if(items[i].Fix.Kind==UniversalFixKind.LabelAddrHi)expected=UniversalMips.WithImm(expected,UniversalMips.Hi(shadow));
                if(items[i].Fix.Kind==UniversalFixKind.LabelAddrLo)expected=UniversalMips.WithImm(expected,UniversalMips.Lo(shadow));
                Require(Word(entry+(uint)i*4)==expected,"complete written native argument/temporary/cache/frame/return packet changed");
            }
            calls.Add(new(pc,native,call,entry));
        }
        var boundary=UniversalMagicPairMovementBoundary.RequireWritten(code,labels,baseAddress,incoming);
        Require((ulong)boundary.Base+(uint)boundary.Bytes<=shadow,"native copy overlaps shadow data");
        return new(baseAddress,shadow,ShadowBytes,Frame,calls,Hash(code)) { CodeBytes = code.Length, MovementBoundary=boundary };
    }
}
