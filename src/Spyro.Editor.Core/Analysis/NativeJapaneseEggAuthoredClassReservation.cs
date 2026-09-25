using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.Core.Analysis;

/// <summary>
/// Exact-USA, fixed-Artisans visual-research reservation of two authored IDs.
/// This is not a recovered native alias or an exhaustive free-class namespace.
/// Complete known placed/local/shared owners, two proven dynamic engine owners,
/// and a conservative exact-revision literal/direct-slot screen are retained.
/// Computed dynamic identities and arbitrary behavior/effect paths are NOT
/// thereby certified; only the closed placed visual composition may consume it.
/// </summary>
public sealed class NativeJapaneseEggAuthoredClassReservation
{
    private static readonly ushort[] Selected=[0x1E9,0x1F2];
    private static readonly NativeJapaneseEggRenderConsumer[] Windows=
    [
        new("local-clear-primary",0x80015A80,0x4C,"1c161a44c43da7bfda6310d6b3a89f65335c71b0ede94e884f04790473f97488"),
        new("local-clear-alternate",0x80015284,0x44,"c7f876530a77217f04c82d826365258dc71292bbf5817860bed4a11db1a726a8"),
        new("native-registration",0x80015ACC,0x68,"e3333647e8548d11fd19653ac0d706b78571d6dbe1fa439b6db10b5b59235a9b"),
        new("dynamic-1fe-1ff",0x800304D0,0x1B8,"c541d22f387a5b674676d4fa34bf512fc68c1ebebe0a6d00cc357a41bfde3aaf"),
        new("special-model-slots-primary",0x80014D40,0x50,"3bd8a4550876e924b3a7133e35bd564e235e497fc96d9484b01f4774f3aae575"),
        new("special-model-slots-alternate",0x80015060,0x74,"1c0fab8da05d9f34620b33b49d0df80201e040051fbc1aac8eb3f1dd7f00a85e"),
        new("unapproved-class-indexed-effects",0x8003B974,0x154,"3e942ab08753551a949ddbdca8fed002546ac3195913fd596d86e902c57ed262")
    ];
    private readonly byte[] _exe;
    private NativeJapaneseEggAuthoredClassReservation(IEnumerable<ushort> known,IEnumerable<ushort> shared,
        int rows,int registrations,string evidence,byte[] exe,bool certified)
    {
        ushort[] reserved=known.Distinct().Order().ToArray(),sharedIds=shared.Distinct().Order().ToArray();
        if(reserved.Any(c=>c>=512)||!reserved.Contains((ushort)0x1FE)||!reserved.Contains((ushort)0x1FF)||
            Selected.Any(reserved.Contains)||sharedIds.Any(c=>!reserved.Contains(c)))
            throw new InvalidDataException("Authored class reservation collides with known/native dynamic owners or loses a shared reservation.");
        SourceCertified=certified;_exe=exe.ToArray();KnownReservedClasses=Array.AsReadOnly(reserved);SharedClasses=Array.AsReadOnly(sharedIds);
        PlacedRowCount=rows;LocalRegistrationCount=registrations;
        if(certified)foreach(var window in Windows)
            if(Hash(_exe.AsSpan(checked((int)(window.Pc-0x8000F800)),window.ByteLength))!=window.Sha256)
                throw new InvalidDataException("Authored reservation native owner changed: "+window.Role);
        PlanSha256=Hash(Encoding.UTF8.GetBytes("usa-artisans-authored-f5-1e9-1f2-v1\n"+certified+"\n"+evidence+"\n"+
            string.Join(',',reserved)+"\n"+string.Join(',',sharedIds)+"\n"));
    }
    public bool SourceCertified{get;}
    public string SourceImageSha256=>SourceCertified?NativeMobySourceInventory.OriginalImageSha256:"fixture";
    public string PlanSha256{get;}
    public int PlacedRowCount{get;}
    public int LocalRegistrationCount{get;}
    public IReadOnlyList<ushort> KnownReservedClasses{get;}
    public IReadOnlyList<ushort> SharedClasses{get;}
    public IReadOnlyList<ushort> AuthoredDestinationClasses=>Array.AsReadOnly(Selected);
    public bool GloballyUnusedNamespaceProven=>false;
    public bool GenericEffectsApproved=>false;
    public bool DynamicSpawnApproved=>false;
    public bool PickupApproved=>false;
    public bool PublicationApproved=>false;
    public void RequireSelected(ushort classId)
    {
        if(!Selected.Contains(classId)||KnownReservedClasses.Contains(classId))
            throw new InvalidDataException("Only this exact Artisans profile's two explicitly authored IDs are reserved; no arbitrary class allocation is authorized.");
    }
    public byte[] CopyConsumer(string role)
    {
        if(!SourceCertified)throw new InvalidDataException("A fixture has no native consumer evidence.");
        var w=Windows.SingleOrDefault(w=>w.Role==role)??throw new InvalidDataException("Unknown authored-class consumer.");
        return _exe.AsSpan((int)(w.Pc-0x8000F800),w.ByteLength).ToArray();
    }
    public static NativeJapaneseEggAuthoredClassReservation Capture(NativeRegionalSourceSession usa)
    {
        ArgumentNullException.ThrowIfNull(usa);
        if(usa.Revision!=NativeDiscRevision.UsaScus94228)throw new InvalidDataException("Authored Artisans classes require the exact USA revision.");
        try
        {
        byte[] exe=usa.ReadExecutableRange(0,usa.ExecutableByteLength);List<string> evidence=[Hash(exe)];
        var known=new HashSet<ushort>();int rows=0,registrations=0;
        Screen(exe.AsSpan(0x800,0x55800));
        for(int entry=10;entry<=78;entry+=2)
        {
            var owner=usa.Entries[entry];byte[] header=usa.ReadWadRange(owner.Offset,0x800);
            int model=Int(header,0x10),modelLength=Int(header,0x14),sceneOffset=Int(header,0x18),sceneLength=Int(header,0x1C);
            if(model+(long)modelLength>sceneOffset||sceneOffset+(long)sceneLength>owner.ByteLength||sceneLength<0xC8)
                throw new InvalidDataException("Known native resource subfile owners exceed their exact directory entry.");
            var roots=NativeMobyRegistrationTableLayout.Decode(header,model,modelLength);
            registrations+=roots.Registrations.Count;known.UnionWith(roots.Registrations.Select(r=>r.ActorId));
            byte[] scene=usa.ReadWadRange(owner.Offset+(long)sceneOffset,sceneLength);
            // Same declared seven-component chain as NativeMobySourceInventory;
            // no model data or guessed package extent is read by this census.
            int at=0x88;
            for(int i=0;i<7;i++)at=End(scene,at,8);
            int end=End(scene,at,8),count=Int(scene,at+4);
            if(count>4096||at+8L+count*0x58L>end)throw new InvalidDataException("Known native row census exceeds its declared component.");
            for(int i=0;i<count;i++)known.Add(BinaryPrimitives.ReadUInt16LittleEndian(scene.AsSpan(at+8+i*0x58+0x36)));
            rows+=count;byte[] overlay=usa.ReadWadEntry(entry-1);Screen(overlay);
            evidence.Add($"{entry}:{Hash(header)}:{Hash(scene)}:{Hash(overlay)}");
        }
        var pete=usa.Entries[8];byte[] peteHeader=usa.ReadWadRange(pete.Offset,0x800);
        var shared=NativeSharedMobySourceInventory.Decode(peteHeader,usa.ReadWadRange(pete.Offset+0x800L,Int(peteHeader,0)));
        known.UnionWith(shared.Registrations.Select(r=>r.ActorId));
        if(rows!=4877||registrations!=1403||known.Count!=368||shared.Registrations.Count!=54)
            throw new InvalidDataException("The complete known USA placed/local/shared namespace no longer matches its exact source census.");
        // These engine-constructed identities are absent from that census.
        // Their assignments AND direct loader slot stores are pinned above.
        known.Add(0x1FE);known.Add(0x1FF);evidence.Add(shared.HeaderSha256);
        var result=new NativeJapaneseEggAuthoredClassReservation(known,shared.Registrations.Select(r=>r.ActorId),rows,registrations,
            string.Join('\n',evidence),exe,true);return result;
        }
        finally { usa.VerifyUnchanged(); }
    }
    internal static NativeJapaneseEggAuthoredClassReservation Fixture(IEnumerable<ushort> reserved)=>
        new(reserved,[0],0,0,"neutral-explicit-fixture",[],false);
    // Conservative aligned-word screening is intentionally not claimed as
    // exhaustive control-flow or dynamically-computed class-ID recovery.
    private static void Screen(ReadOnlySpan<byte> bytes)
    {
        for(int at=0;at+4<=bytes.Length;at+=4)
        {
            uint w=BinaryPrimitives.ReadUInt32LittleEndian(bytes[at..]);uint op=w>>26,imm=w&65535;
            foreach(ushort cls in Selected)
            {
                bool literal=op is 8 or 9 or 10 or 11 or 12 or 13 or 14&&imm==cls;
                bool slot=op is 0x20 or 0x21 or 0x23 or 0x24 or 0x25 or 0x28 or 0x29 or 0x2B&&imm==((0x6378u+4u*cls)&65535);
                if(literal||slot)throw new InvalidDataException("Authored class literal/direct-slot screen found an unowned candidate use.");
            }
        }
    }
    private static int End(byte[] b,int at,int minimum)
    {if(at<0||at>b.Length-4)throw new InvalidDataException("Native component header is outside scene.");int n=Int(b,at);if(n<minimum||n%4!=0||n>b.Length-at)throw new InvalidDataException("Native component owner is malformed.");return at+n;}
    private static int Int(byte[] b,int at)=>checked((int)BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(at)));
    private static string Hash(ReadOnlySpan<byte> b)=>Convert.ToHexStringLower(SHA256.HashData(b));
}
