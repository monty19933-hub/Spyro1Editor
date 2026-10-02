using System.Buffers.Binary;
using System.Security.Cryptography;
namespace Spyro.Editor.Core.Universal;

/// <summary>Source ownership for one original Flight chest replacement. Requires one original Flight chest
/// replacement and preserves all native course objectives, controller and result callbacks.
/// This certificate is source/static evidence, never runtime acceptance.</summary>
public sealed class UniversalFlightChestCertificate
{
    internal sealed record Word(uint Pc, uint Value, string FixKind, uint FixTarget);
    internal sealed record Spawn(uint Call, int Class, uint Producer, uint Start, Word[] Words);
    internal sealed record Time(uint Subtract, int Register, uint? Load, Word[] Words);
    internal sealed record Owner(string Key, string DataHash, string OverlayHash, uint Update, uint Init, uint Copy, int Child, int Category, int[] Rows, Spawn[] Spawns, Time[] Times, IReadOnlyDictionary<uint,UniversalExeBinding> Callbacks);
    private readonly Owner _source, _destination;
    private readonly UniversalLevelData _originalDestination;
    private readonly UniversalLevelData _preparedDestination;
    private readonly int _removed;
    private readonly byte[] _destinationOverlay;
    private readonly UniversalLevelData _sourceData;
    private readonly UniversalRowTemplate _sourceRow;
    internal UniversalFlightChestCertificate(Owner s, Owner d, UniversalLevelData original, int removed, UniversalRowTemplate row,byte[] destinationOverlay,UniversalLevelData sourceData,UniversalLevelData prepared) { _preparedDestination=new(prepared.Bytes.ToArray());_sourceData=new(sourceData.Bytes.ToArray());_destinationOverlay=destinationOverlay.ToArray();_source=s;_destination=d;_originalDestination=new(original.Bytes.ToArray());_removed=removed;_sourceRow=row; }
    public string SourceKey => _source.Key;
    public string DestinationKey => _destination.Key;
    public int BonusChildClass => _source.Child;
    public int ObjectiveCategory => _source.Category;
    internal UniversalClassRemapping.SpawnArgument? SpawnAt(UniversalOverlay source,uint pc) { RequireSource(source);var s=_source.Spawns.FirstOrDefault(s=>s.Call==pc);return s==null?null:new(s.Call,s.Producer,s.Class); }
    internal IReadOnlySet<uint> SpawnCalls => _source.Spawns.Select(s=>s.Call).ToHashSet();
    internal IReadOnlyList<int> Classes => [0x12B,_source.Child,0x1DE,0x1DF,0x1E0];
    internal void RequireSource(UniversalOverlay overlay) => UniversalFlightChestOwnership.RequireOverlay(_source,overlay);
    internal void RequireBundle(UniversalOverlay source, UniversalOverlay destination, IReadOnlyList<int> classes, byte[]? exe)
    {
        RequireSource(source);UniversalFlightChestOwnership.RequireOverlay(_destination,destination);UniversalFlightChestOwnership.RequireNative(exe);
        if(!classes.Order().SequenceEqual(Classes.Order())) throw new InvalidDataException("Flight chest copy requires its exact issued five-class dependency closure.");
    }
    internal void AddDependencies(UniversalOverlay source,int root,SortedSet<int> spawns)
    {
        RequireSource(source);if(root==0x12B)spawns.UnionWith(Classes.Skip(1));
    }
    internal void RequireParticleInputs(UniversalLevelData source,UniversalLevelData destination)
    {
        if(!source.Bytes.SequenceEqual(_sourceData.Bytes) || !destination.Bytes.SequenceEqual(_preparedDestination.Bytes))throw new InvalidDataException("Flight chest particle context requires its issued original source/course inputs.");
    }
    internal void RequireRow(byte[] row, byte[] props, int expectedClass)
    {
        if(row.Length!=UniversalLevelData.RowStride || !props.SequenceEqual(_sourceRow.Properties))throw new InvalidDataException("Flight chest authored properties differ from its issued original selector/category.");
        for(int i=4;i<row.Length;i++)
        {
            if(i is >=0x0C and <0x18 or 0x43 or 0x46 or 0x52 or 0x36 or 0x37)continue;
            if(i==0x4A && row[i]==0xFF)continue;
            if(row[i]!=_sourceRow.Row[i])throw new InvalidDataException("Flight chest authored state differs from its issued original row.");
        }
        if(BinaryPrimitives.ReadUInt16LittleEndian(row.AsSpan(0x36))!=expectedClass)throw new InvalidDataException("Flight chest authored class identity changed.");
    }
    internal void RequireFinal(UniversalLevelData data,byte[] overlay,int importedRow,int mappedClass)
    {
        int keep=checked((int)(_destination.Copy-UniversalOverlay.Base));
        var originalOverlay=_destinationOverlay;
        if(overlay.Length<keep || !overlay.AsSpan(0,keep).SequenceEqual(originalOverlay.AsSpan(0,keep)))throw new InvalidDataException("Flight chest destination controller/results code changed.");
        var sc=_originalDestination.Scene();if(data.Scene().RowCount!=sc.RowCount || importedRow!=_removed)throw new InvalidDataException("Flight chest must replace exactly its issued native objective row.");
        for(int i=0;i<sc.RowCount;i++)
        {
            var actual=data.CaptureRow(DestinationKey,i);
            if(i==importedRow){RequireRow(actual.Row,actual.Properties,mappedClass);continue;}
            var original=_originalDestination.CaptureRow(DestinationKey,i);
            byte[] ap=actual.Properties.ToArray(),op=original.Properties.ToArray();
            if(!actual.InternalPointers.SequenceEqual(original.InternalPointers) || !actual.ExternalRowPointers.SequenceEqual(original.ExternalRowPointers))throw new InvalidDataException($"Flight chest retained row{i} changed its path/link topology.");
            foreach(var (offset,target) in original.InternalPointers){ap.AsSpan(offset,4).Clear();op.AsSpan(offset,4).Clear();}
            if(!actual.Row.AsSpan(4).SequenceEqual(original.Row.AsSpan(4)) || !ap.SequenceEqual(op))throw new InvalidDataException($"Flight chest changed retained native row{i} objective/controller input.");
        }
        UniversalFlightChestOwnership.RequireParticleTextures(_sourceData,data);
        // Eight roots per original category remain: every unrelated original objective is
        // byte-identical, one chest is replaced with an equal category and original state.
    }
    internal void Patch(UniversalAsmPart part,UniversalOverlay source,IReadOnlyDictionary<int,int> map)
    {
        RequireSource(source);if(!map.Keys.Order().SequenceEqual(Classes.Order()) || map.Values.Distinct().Count()!=map.Count)throw new InvalidDataException("Flight chest has a mixed or incomplete source namespace.");
        UniversalFlightChestOwnership.RequireSourcePacketEdges(_source,source);
        var included=part.Items.Where(x=>x.Word!=null).Select(x=>x.Orig??x.SourcePc).Where(x=>x!=null).Select(x=>x!.Value).ToHashSet();
        foreach(var spawn in _source.Spawns)
        {
            if(!included.Contains(spawn.Call))throw new InvalidDataException("Flight chest copy omitted an issued child spawn.");
            var indices=RequirePacket(part,source,spawn.Words);
            foreach(int at in indices)
            {
                int pi=part.Items.FindIndex(at, x=>x.Word!=null && (x.Orig??x.SourcePc)==spawn.Producer);
                var item=part.Items[pi];uint patched=spawn.Class==_source.Child?UniversalMips.EncodeI(9,0,4,map[spawn.Class]):UniversalMips.WithImm(item.Word!.Value,map[spawn.Class]);
                part.Items[pi]=item with{Word=patched};
            }
        }
        // Native model-table lookup must retain the mapped current class; this source
        // packet uses the authenticated update's saved native gModels table in s0 and
        // sends the model-local sound byte to PlaySound with output actor+54.
        foreach(uint pc in included.Where(pc=>source.Contains(pc)&&source.Word(pc)==0x86620036).ToArray())
        {
            uint[] expected=[0x86620036,0,0x00021080,0x00501021,0x8C420000,0x24060008,0x90440005,0x0C01569E,0x24A70054];
            if(!expected.Select((w,i)=>source.Word(pc+(uint)i*4)==w).All(x=>x))continue;
            var packet=expected.Select((w,i)=>new Word(pc+(uint)i*4,w,"None",0)).ToArray();
            foreach(int at in RequirePacket(part,source,packet))part.Items[at]=part.Items[at] with{ClassReadOwned=true};
        }
        foreach(var time in _source.Times)
        {
            if(!included.Contains(time.Subtract))throw new InvalidDataException("Flight chest copy omitted issued native bonus arithmetic.");
            var indices=RequirePacket(part,source,time.Words);
            foreach(int at in indices)
            {
                int pi=part.Items.FindIndex(at,x=>x.Word!=null && (x.Orig??x.SourcePc)==time.Subtract);
                part.Items[pi]=part.Items[pi] with{Word=UniversalMips.EncodeI(9,0,time.Register,_source.Child-0x158)};
                if(time.Load is uint pc)
                {
                    int li=part.Items.FindIndex(at,x=>x.Word!=null&&(x.Orig??x.SourcePc)==pc);
                    part.Items[li]=part.Items[li] with{ClassReadOwned=true};
                }
            }
        }
    }
    private static List<int> RequirePacket(UniversalAsmPart part,UniversalOverlay source,Word[] packet)
    {
        var starts=part.Items.Select((x,i)=>(x,i)).Where(x=>x.x.Word!=null && (x.x.Orig??x.x.SourcePc)==packet[0].Pc).Select(x=>x.i).ToList();
        if(starts.Count==0)throw new InvalidDataException("Flight chest emitted packet is missing its source entry.");
        var interior=packet.Skip(1).Select(x=>x.Pc).ToHashSet();
        foreach(int start in starts)
        {
            int j=start;
            foreach(var expected in packet)
            {
                if(j<part.Items.Count&&part.Items[j].Word==null)throw new InvalidDataException("Flight chest emitted packet gained a new interior entry label.");
                if(j>=part.Items.Count)throw new InvalidDataException("Flight chest emitted packet is truncated.");
                var item=part.Items[j++];var fk=Enum.Parse<UniversalFixKind>(expected.FixKind);
                if((item.Orig??item.SourcePc)!=expected.Pc || item.Word!=expected.Value || item.Fix.Kind!=fk || item.Fix.Orig!=expected.FixTarget)throw new InvalidDataException("Flight chest emitted packet changed, reordered or gained an unowned consumer.");
            }
        }
        // Repeated source packets are all authenticated; no extra copied interior can hide
        // outside a matching packet, and newly introduced direct interior entries refuse.
        foreach(var expected in packet)
            if(part.Items.Count(x=>x.Word!=null&&(x.Orig??x.SourcePc)==expected.Pc)!=starts.Count)throw new InvalidDataException("Flight chest emitted packet has an unmatched duplicate interior.");
        foreach(var item in part.Items.Where(x=>x.Fix.Kind is UniversalFixKind.Branch or UniversalFixKind.Jump && interior.Contains(x.Fix.Orig)))
        {
            if((item.Orig??item.SourcePc) is not uint pc || source.Word(pc)!=item.Word || UniversalMips.BranchTarget(source.Word(pc),pc)!=item.Fix.Orig)throw new InvalidDataException("Flight chest emitted flow has an unowned incoming packet entry.");
        }
        return starts;
    }
}

public static class UniversalFlightChestOwnership
{
    private static string Hash(ReadOnlySpan<byte> x)=>Convert.ToHexStringLower(SHA256.HashData(x));
    internal static void RequireSourcePacketEdges(UniversalFlightChestCertificate.Owner owner,UniversalOverlay overlay)
    {
        var packets=owner.Spawns.Select(p=>p.Words).Concat(owner.Times.Select(p=>p.Words)).ToArray();
        var interior=packets.SelectMany(p=>p.Skip(1)).Select(w=>w.Pc).ToHashSet();
        for(uint pc=overlay.Update;pc<overlay.Init;pc+=4)
        {
            uint w=overlay.Word(pc);if((UniversalMips.IsPcRelative(w)||UniversalMips.Op(w) is 2 or 3)&&interior.Contains(UniversalMips.BranchTarget(w,pc)))throw new InvalidDataException("Flight chest original packet has an alternate interior entry.");
        }
        var sw=UniversalDispatchAnalysis.FindUpdateSwitch(overlay);
        foreach(int c in new[]{0x12B,owner.Child,0x1DE,0x1DF,0x1E0})
        {
            var tr=UniversalDispatchAnalysis.Trace(overlay,sw.TreeStart,c,sw.Seeds,sw.ClassRegister);
            var cl=UniversalDispatchAnalysis.Closure(overlay,sw.Function,[tr.Stop],new HashSet<uint>{sw.TreeStart});
            if(cl.Tables.SelectMany(t=>t.Targets).Any(interior.Contains))throw new InvalidDataException("Flight chest original jump table enters packet interior.");
        }
    }
    // Original USA full source owners and exact typed packets; proof scripts/receipts
    // are private evidence. No caller can supply authority bytes or callback identities.
    private static readonly Dictionary<string,UniversalFlightChestCertificate.Owner> Owners=new(StringComparer.OrdinalIgnoreCase)
    {
        ["sunnyflight"]=new("sunnyflight","c5c03eed4a43a1f2557b248851438583509895aa95efa7e0e92236c9b7243dca","42b4e8a637ff91f87b884a9beea3266299d01ae36d36b437436d247f78812f59",0x8007CFB4,0x8008249C,0x80084ED0,0x15B,3,
            [8,38,39,40,41,42,43,44],
            [
                new(0x8007FF30,0x15B,0x8007FF34,0x8007FF20,[
                    new(0x8007FF20,0x8E040000,"None",0x00000000),
                    new(0x8007FF24,0x3C028007,"None",0x00000000),
                    new(0x8007FF28,0x8C4258CC,"None",0x00000000),
                    new(0x8007FF2C,0x00000000,"None",0x00000000),
                    new(0x8007FF30,0x0040F809,"None",0x00000000),
                    new(0x8007FF34,0x24840158,"None",0x00000000),
                ]),
                new(0x8007FF90,0x1DF,0x8007FF80,0x8007FF80,[
                    new(0x8007FF80,0x240401DF,"None",0x00000000),
                    new(0x8007FF84,0x3C028007,"None",0x00000000),
                    new(0x8007FF88,0x8C4258CC,"None",0x00000000),
                    new(0x8007FF8C,0x00000000,"None",0x00000000),
                    new(0x8007FF90,0x0040F809,"None",0x00000000),
                    new(0x8007FF94,0x02602821,"None",0x00000000),
                ]),
                new(0x8007FF78,0x1DE,0x8007FF64,0x8007FF60,[
                    new(0x8007FF60,0x14400010,"Branch",0x8007FFA4),
                    new(0x8007FF64,0x240401DE,"None",0x00000000),
                    new(0x8007FF68,0x02602821,"None",0x00000000),
                    new(0x8007FF6C,0x3C028007,"None",0x00000000),
                    new(0x8007FF70,0x8C4258CC,"None",0x00000000),
                    new(0x8007FF74,0x00000000,"None",0x00000000),
                    new(0x8007FF78,0x0040F809,"None",0x00000000),
                    new(0x8007FF7C,0x26100001,"None",0x00000000),
                ]),
                new(0x8007FFDC,0x1E0,0x8007FFC8,0x8007FFC4,[
                    new(0x8007FFC4,0x1440000A,"Branch",0x8007FFF0),
                    new(0x8007FFC8,0x240401E0,"None",0x00000000),
                    new(0x8007FFCC,0x02602821,"None",0x00000000),
                    new(0x8007FFD0,0x3C028007,"None",0x00000000),
                    new(0x8007FFD4,0x8C4258CC,"None",0x00000000),
                    new(0x8007FFD8,0x00000000,"None",0x00000000),
                    new(0x8007FFDC,0x0040F809,"None",0x00000000),
                    new(0x8007FFE0,0x26100001,"None",0x00000000),
                ]),
            ],[
                new(0x80080588,3,0x80080580,[
                    new(0x80080580,0x86630036,"None",0x00000000),
                    new(0x80080584,0x00000000,"None",0x00000000),
                    new(0x80080588,0x2463FEA8,"None",0x00000000),
                    new(0x8008058C,0x00031100,"None",0x00000000),
                    new(0x80080590,0x00431023,"None",0x00000000),
                    new(0x80080594,0x3C038007,"None",0x00000000),
                    new(0x80080598,0x8C635908,"None",0x00000000),
                    new(0x8008059C,0x00021080,"None",0x00000000),
                    new(0x800805A0,0x00621821,"None",0x00000000),
                    new(0x800805A4,0x3C018007,"None",0x00000000),
                    new(0x800805A8,0xAC235908,"None",0x00000000),
                ]),
                new(0x800806B8,2,0x800806AC,[
                    new(0x800806AC,0x86620036,"None",0x00000000),
                    new(0x800806B0,0x3C048007,"None",0x00000000),
                    new(0x800806B4,0x8C8461D4,"None",0x00000000),
                    new(0x800806B8,0x2442FEA8,"None",0x00000000),
                    new(0x800806BC,0x00021900,"None",0x00000000),
                    new(0x800806C0,0x00621823,"None",0x00000000),
                    new(0x800806C4,0x3C028007,"None",0x00000000),
                    new(0x800806C8,0x8C425840,"None",0x00000000),
                    new(0x800806CC,0x00031880,"None",0x00000000),
                    new(0x800806D0,0x00431021,"None",0x00000000),
                    new(0x800806D4,0x3C018007,"None",0x00000000),
                    new(0x800806D8,0xAC225840,"None",0x00000000),
                ]),
            ],new Dictionary<uint,UniversalExeBinding>
            {
                [0x800785D8]=new(0x800785D8,0x8005A980,0x8005A984,0x80084ED0),
                [0x800758CC]=new(0x800758CC,0x8005A990,0x8005A994,0x8008249C),
                [0x80075734]=new(0x80075734,0x8005A9A0,0x8005A9A4,0x8007CFB4),
                [0x800758E4]=new(0x800758E4,0x8005A9B0,0x8005A9B4,0x80084128),
                [0x800756BC]=new(0x800756BC,0x8005A9C0,0x8005A9C4,0x80083330),
                [0x80075694]=new(0x80075694,0x8005A9D0,0x8005A9D4,0x8007AE08),
                [0x800757A8]=new(0x800757A8,0x8005A9E0,0x8005A9E4,0x8007AEDC),
                [0x800757C0]=new(0x800757C0,0x8005A9F0,0x8005A9F4,0x8007B1FC),
                [0x8007567C]=new(0x8007567C,0x8005AA00,0x8005AA04,0x8007B68C),
                [0x800758C4]=new(0x800758C4,0x8005AA10,0x8005AA14,0x8007B4B0),
            }),
        ["nightflight"]=new("nightflight","8b419612d1764fbaf1b13afea845ef853de5a632a8a371e095270ff099bcaa2d","3891e7af5f83825dbebc80952e173fbbea8125653d4b00fc7b1980a1128b873a",0x8007CFB4,0x800819BC,0x80083BF0,0x15A,1,
            [0,1,2,3,4,26,27,28],
            [
                new(0x8007FCD8,0x15A,0x8007FCDC,0x8007FCC8,[
                    new(0x8007FCC8,0x8E040000,"None",0x00000000),
                    new(0x8007FCCC,0x3C028007,"None",0x00000000),
                    new(0x8007FCD0,0x8C4258CC,"None",0x00000000),
                    new(0x8007FCD4,0x00000000,"None",0x00000000),
                    new(0x8007FCD8,0x0040F809,"None",0x00000000),
                    new(0x8007FCDC,0x24840158,"None",0x00000000),
                ]),
                new(0x8007FD38,0x1DF,0x8007FD28,0x8007FD28,[
                    new(0x8007FD28,0x240401DF,"None",0x00000000),
                    new(0x8007FD2C,0x3C028007,"None",0x00000000),
                    new(0x8007FD30,0x8C4258CC,"None",0x00000000),
                    new(0x8007FD34,0x00000000,"None",0x00000000),
                    new(0x8007FD38,0x0040F809,"None",0x00000000),
                    new(0x8007FD3C,0x02602821,"None",0x00000000),
                ]),
                new(0x8007FD20,0x1DE,0x8007FD0C,0x8007FD08,[
                    new(0x8007FD08,0x14400010,"Branch",0x8007FD4C),
                    new(0x8007FD0C,0x240401DE,"None",0x00000000),
                    new(0x8007FD10,0x02602821,"None",0x00000000),
                    new(0x8007FD14,0x3C028007,"None",0x00000000),
                    new(0x8007FD18,0x8C4258CC,"None",0x00000000),
                    new(0x8007FD1C,0x00000000,"None",0x00000000),
                    new(0x8007FD20,0x0040F809,"None",0x00000000),
                    new(0x8007FD24,0x26100001,"None",0x00000000),
                ]),
                new(0x8007FD84,0x1E0,0x8007FD70,0x8007FD6C,[
                    new(0x8007FD6C,0x1440000A,"Branch",0x8007FD98),
                    new(0x8007FD70,0x240401E0,"None",0x00000000),
                    new(0x8007FD74,0x02602821,"None",0x00000000),
                    new(0x8007FD78,0x3C028007,"None",0x00000000),
                    new(0x8007FD7C,0x8C4258CC,"None",0x00000000),
                    new(0x8007FD80,0x00000000,"None",0x00000000),
                    new(0x8007FD84,0x0040F809,"None",0x00000000),
                    new(0x8007FD88,0x26100001,"None",0x00000000),
                ]),
            ],[
                new(0x80080088,3,0x80080080,[
                    new(0x80080080,0x86630036,"None",0x00000000),
                    new(0x80080084,0x00000000,"None",0x00000000),
                    new(0x80080088,0x2463FEA8,"None",0x00000000),
                    new(0x8008008C,0x00031100,"None",0x00000000),
                    new(0x80080090,0x00431023,"None",0x00000000),
                    new(0x80080094,0x3C038007,"None",0x00000000),
                    new(0x80080098,0x8C635908,"None",0x00000000),
                    new(0x8008009C,0x00021080,"None",0x00000000),
                    new(0x800800A0,0x00621821,"None",0x00000000),
                    new(0x800800A4,0x3C018007,"None",0x00000000),
                    new(0x800800A8,0xAC235908,"None",0x00000000),
                ]),
                new(0x800801B4,2,0x800801A8,[
                    new(0x800801A8,0x86620036,"None",0x00000000),
                    new(0x800801AC,0x3C048007,"None",0x00000000),
                    new(0x800801B0,0x8C8461D4,"None",0x00000000),
                    new(0x800801B4,0x2442FEA8,"None",0x00000000),
                    new(0x800801B8,0x00021900,"None",0x00000000),
                    new(0x800801BC,0x00621823,"None",0x00000000),
                    new(0x800801C0,0x3C028007,"None",0x00000000),
                    new(0x800801C4,0x8C425840,"None",0x00000000),
                    new(0x800801C8,0x00031880,"None",0x00000000),
                    new(0x800801CC,0x00431021,"None",0x00000000),
                    new(0x800801D0,0x3C018007,"None",0x00000000),
                    new(0x800801D4,0xAC225840,"None",0x00000000),
                ]),
            ],new Dictionary<uint,UniversalExeBinding>
            {
                [0x800785D8]=new(0x800785D8,0x8005AC90,0x8005AC94,0x80083BF0),
                [0x800758CC]=new(0x800758CC,0x8005ACA0,0x8005ACA4,0x800819BC),
                [0x80075734]=new(0x80075734,0x8005ACB0,0x8005ACB4,0x8007CFB4),
                [0x800758E4]=new(0x800758E4,0x8005ACC0,0x8005ACC4,0x80082F58),
                [0x800756BC]=new(0x800756BC,0x8005ACD0,0x8005ACD4,0x80082300),
                [0x80075694]=new(0x80075694,0x8005ACE0,0x8005ACE4,0x8007AE08),
                [0x800757A8]=new(0x800757A8,0x8005ACF0,0x8005ACF4,0x8007AEDC),
                [0x800757C0]=new(0x800757C0,0x8005AD00,0x8005AD04,0x8007B1FC),
                [0x8007567C]=new(0x8007567C,0x8005AD10,0x8005AD14,0x8007B68C),
                [0x800758C4]=new(0x800758C4,0x8005AD20,0x8005AD24,0x8007B4B0),
            }),
        ["crystalflight"]=new("crystalflight","3871b093ef681dc64ec693dd3ba11021949251122459f57773636a69c05e8be0","e8d91a7c5f9f5d06b95ac01ade6fa4423485fd3d5dd75dd0dc15cc8319d46a59",0x8007CFB4,0x80081F0C,0x80084390,0x15A,3,
            [0,1,2,3,5,6,23,24],
            [
                new(0x8007FD2C,0x15A,0x8007FD30,0x8007FD1C,[
                    new(0x8007FD1C,0x8E040000,"None",0x00000000),
                    new(0x8007FD20,0x3C028007,"None",0x00000000),
                    new(0x8007FD24,0x8C4258CC,"None",0x00000000),
                    new(0x8007FD28,0x00000000,"None",0x00000000),
                    new(0x8007FD2C,0x0040F809,"None",0x00000000),
                    new(0x8007FD30,0x24840158,"None",0x00000000),
                ]),
                new(0x8007FD8C,0x1DF,0x8007FD7C,0x8007FD7C,[
                    new(0x8007FD7C,0x240401DF,"None",0x00000000),
                    new(0x8007FD80,0x3C028007,"None",0x00000000),
                    new(0x8007FD84,0x8C4258CC,"None",0x00000000),
                    new(0x8007FD88,0x00000000,"None",0x00000000),
                    new(0x8007FD8C,0x0040F809,"None",0x00000000),
                    new(0x8007FD90,0x02602821,"None",0x00000000),
                ]),
                new(0x8007FD74,0x1DE,0x8007FD60,0x8007FD5C,[
                    new(0x8007FD5C,0x14400010,"Branch",0x8007FDA0),
                    new(0x8007FD60,0x240401DE,"None",0x00000000),
                    new(0x8007FD64,0x02602821,"None",0x00000000),
                    new(0x8007FD68,0x3C028007,"None",0x00000000),
                    new(0x8007FD6C,0x8C4258CC,"None",0x00000000),
                    new(0x8007FD70,0x00000000,"None",0x00000000),
                    new(0x8007FD74,0x0040F809,"None",0x00000000),
                    new(0x8007FD78,0x26100001,"None",0x00000000),
                ]),
                new(0x8007FDD8,0x1E0,0x8007FDC4,0x8007FDC0,[
                    new(0x8007FDC0,0x1440000A,"Branch",0x8007FDEC),
                    new(0x8007FDC4,0x240401E0,"None",0x00000000),
                    new(0x8007FDC8,0x02602821,"None",0x00000000),
                    new(0x8007FDCC,0x3C028007,"None",0x00000000),
                    new(0x8007FDD0,0x8C4258CC,"None",0x00000000),
                    new(0x8007FDD4,0x00000000,"None",0x00000000),
                    new(0x8007FDD8,0x0040F809,"None",0x00000000),
                    new(0x8007FDDC,0x26100001,"None",0x00000000),
                ]),
            ],[
                new(0x800804F8,3,null,[
                    new(0x800804F8,0x2463FEA8,"None",0x00000000),
                    new(0x800804FC,0x00031100,"None",0x00000000),
                    new(0x80080500,0x00431023,"None",0x00000000),
                    new(0x80080504,0x3C038007,"None",0x00000000),
                    new(0x80080508,0x8C635908,"None",0x00000000),
                    new(0x8008050C,0x00021080,"None",0x00000000),
                    new(0x80080510,0x00621821,"None",0x00000000),
                    new(0x80080514,0x3C018007,"None",0x00000000),
                    new(0x80080518,0xAC235908,"None",0x00000000),
                ]),
                new(0x80080628,2,0x8008061C,[
                    new(0x8008061C,0x86620036,"None",0x00000000),
                    new(0x80080620,0x3C048007,"None",0x00000000),
                    new(0x80080624,0x8C8461D4,"None",0x00000000),
                    new(0x80080628,0x2442FEA8,"None",0x00000000),
                    new(0x8008062C,0x00021900,"None",0x00000000),
                    new(0x80080630,0x00621823,"None",0x00000000),
                    new(0x80080634,0x3C028007,"None",0x00000000),
                    new(0x80080638,0x8C425840,"None",0x00000000),
                    new(0x8008063C,0x00031880,"None",0x00000000),
                    new(0x80080640,0x00431021,"None",0x00000000),
                    new(0x80080644,0x3C018007,"None",0x00000000),
                    new(0x80080648,0xAC225840,"None",0x00000000),
                ]),
            ],new Dictionary<uint,UniversalExeBinding>
            {
                [0x800785D8]=new(0x800785D8,0x8005AF10,0x8005AF14,0x80084390),
                [0x800758CC]=new(0x800758CC,0x8005AF20,0x8005AF24,0x80081F0C),
                [0x80075734]=new(0x80075734,0x8005AF30,0x8005AF34,0x8007CFB4),
                [0x800758E4]=new(0x800758E4,0x8005AF40,0x8005AF44,0x800836F8),
                [0x800756BC]=new(0x800756BC,0x8005AF50,0x8005AF54,0x80082AA0),
                [0x80075694]=new(0x80075694,0x8005AF60,0x8005AF64,0x8007AE08),
                [0x800757A8]=new(0x800757A8,0x8005AF70,0x8005AF74,0x8007AEDC),
                [0x800757C0]=new(0x800757C0,0x8005AF80,0x8005AF84,0x8007B1FC),
                [0x8007567C]=new(0x8007567C,0x8005AF90,0x8005AF94,0x8007B68C),
                [0x800758C4]=new(0x800758C4,0x8005AFA0,0x8005AFA4,0x8007B4B0),
            }),
        ["wildflight"]=new("wildflight","364fe3d540e2308b548f290f923994f7ccba13b357632e8bf67d9d4af3d31c48","5069a570d9cce769fed272d8c8fe24a56ade3317370fe5c688ebb43126570e97",0x8007CFB4,0x8008223C,0x80084844,0x15A,3,
            [8,15,16,17,18,19,20,21],
            [
                new(0x80080104,0x15A,0x80080108,0x800800F4,[
                    new(0x800800F4,0x8E040000,"None",0x00000000),
                    new(0x800800F8,0x3C028007,"None",0x00000000),
                    new(0x800800FC,0x8C4258CC,"None",0x00000000),
                    new(0x80080100,0x00000000,"None",0x00000000),
                    new(0x80080104,0x0040F809,"None",0x00000000),
                    new(0x80080108,0x24840158,"None",0x00000000),
                ]),
                new(0x80080164,0x1DF,0x80080154,0x80080154,[
                    new(0x80080154,0x240401DF,"None",0x00000000),
                    new(0x80080158,0x3C028007,"None",0x00000000),
                    new(0x8008015C,0x8C4258CC,"None",0x00000000),
                    new(0x80080160,0x00000000,"None",0x00000000),
                    new(0x80080164,0x0040F809,"None",0x00000000),
                    new(0x80080168,0x02602821,"None",0x00000000),
                ]),
                new(0x8008014C,0x1DE,0x80080138,0x80080134,[
                    new(0x80080134,0x14400010,"Branch",0x80080178),
                    new(0x80080138,0x240401DE,"None",0x00000000),
                    new(0x8008013C,0x02602821,"None",0x00000000),
                    new(0x80080140,0x3C028007,"None",0x00000000),
                    new(0x80080144,0x8C4258CC,"None",0x00000000),
                    new(0x80080148,0x00000000,"None",0x00000000),
                    new(0x8008014C,0x0040F809,"None",0x00000000),
                    new(0x80080150,0x26100001,"None",0x00000000),
                ]),
                new(0x800801B0,0x1E0,0x8008019C,0x80080198,[
                    new(0x80080198,0x1440000A,"Branch",0x800801C4),
                    new(0x8008019C,0x240401E0,"None",0x00000000),
                    new(0x800801A0,0x02602821,"None",0x00000000),
                    new(0x800801A4,0x3C028007,"None",0x00000000),
                    new(0x800801A8,0x8C4258CC,"None",0x00000000),
                    new(0x800801AC,0x00000000,"None",0x00000000),
                    new(0x800801B0,0x0040F809,"None",0x00000000),
                    new(0x800801B4,0x26100001,"None",0x00000000),
                ]),
            ],[
                new(0x8008074C,3,null,[
                    new(0x8008074C,0x2463FEA8,"None",0x00000000),
                    new(0x80080750,0x00031100,"None",0x00000000),
                    new(0x80080754,0x00431023,"None",0x00000000),
                    new(0x80080758,0x3C038007,"None",0x00000000),
                    new(0x8008075C,0x8C635908,"None",0x00000000),
                    new(0x80080760,0x00021080,"None",0x00000000),
                    new(0x80080764,0x00621821,"None",0x00000000),
                    new(0x80080768,0x3C018007,"None",0x00000000),
                    new(0x8008076C,0xAC235908,"None",0x00000000),
                ]),
                new(0x8008087C,2,0x80080870,[
                    new(0x80080870,0x86620036,"None",0x00000000),
                    new(0x80080874,0x3C048007,"None",0x00000000),
                    new(0x80080878,0x8C8461D4,"None",0x00000000),
                    new(0x8008087C,0x2442FEA8,"None",0x00000000),
                    new(0x80080880,0x00021900,"None",0x00000000),
                    new(0x80080884,0x00621823,"None",0x00000000),
                    new(0x80080888,0x3C028007,"None",0x00000000),
                    new(0x8008088C,0x8C425840,"None",0x00000000),
                    new(0x80080890,0x00031880,"None",0x00000000),
                    new(0x80080894,0x00431021,"None",0x00000000),
                    new(0x80080898,0x3C018007,"None",0x00000000),
                    new(0x8008089C,0xAC225840,"None",0x00000000),
                ]),
            ],new Dictionary<uint,UniversalExeBinding>
            {
                [0x800785D8]=new(0x800785D8,0x8005B190,0x8005B194,0x80084844),
                [0x800758CC]=new(0x800758CC,0x8005B1A0,0x8005B1A4,0x8008223C),
                [0x80075734]=new(0x80075734,0x8005B1B0,0x8005B1B4,0x8007CFB4),
                [0x800758E4]=new(0x800758E4,0x8005B1C0,0x8005B1C4,0x80083BAC),
                [0x800756BC]=new(0x800756BC,0x8005B1D0,0x8005B1D4,0x80082F54),
                [0x80075694]=new(0x80075694,0x8005B1E0,0x8005B1E4,0x8007AE08),
                [0x800757A8]=new(0x800757A8,0x8005B1F0,0x8005B1F4,0x8007AEDC),
                [0x800757C0]=new(0x800757C0,0x8005B200,0x8005B204,0x8007B1FC),
                [0x8007567C]=new(0x8007567C,0x8005B210,0x8005B214,0x8007B68C),
                [0x800758C4]=new(0x800758C4,0x8005B220,0x8005B224,0x8007B4B0),
            }),
        ["icyflight"]=new("icyflight","1b92411d127c0de04b4760f194dbc38583ef28dc7e18ef1e3b18150c64bd2d5c","67de47f547e65d4345401dedbb988b38c0de61648c903ab98d1a64f64fce9f0e",0x8007CFB4,0x80082028,0x80084934,0x15A,1,
            [28,29,38,39,40,41,42,43],
            [
                new(0x80080188,0x15A,0x8008018C,0x80080178,[
                    new(0x80080178,0x8E040000,"None",0x00000000),
                    new(0x8008017C,0x3C028007,"None",0x00000000),
                    new(0x80080180,0x8C4258CC,"None",0x00000000),
                    new(0x80080184,0x00000000,"None",0x00000000),
                    new(0x80080188,0x0040F809,"None",0x00000000),
                    new(0x8008018C,0x24840158,"None",0x00000000),
                ]),
                new(0x800801E8,0x1DF,0x800801D8,0x800801D8,[
                    new(0x800801D8,0x240401DF,"None",0x00000000),
                    new(0x800801DC,0x3C028007,"None",0x00000000),
                    new(0x800801E0,0x8C4258CC,"None",0x00000000),
                    new(0x800801E4,0x00000000,"None",0x00000000),
                    new(0x800801E8,0x0040F809,"None",0x00000000),
                    new(0x800801EC,0x02602821,"None",0x00000000),
                ]),
                new(0x800801D0,0x1DE,0x800801BC,0x800801B8,[
                    new(0x800801B8,0x14400010,"Branch",0x800801FC),
                    new(0x800801BC,0x240401DE,"None",0x00000000),
                    new(0x800801C0,0x02602821,"None",0x00000000),
                    new(0x800801C4,0x3C028007,"None",0x00000000),
                    new(0x800801C8,0x8C4258CC,"None",0x00000000),
                    new(0x800801CC,0x00000000,"None",0x00000000),
                    new(0x800801D0,0x0040F809,"None",0x00000000),
                    new(0x800801D4,0x26100001,"None",0x00000000),
                ]),
                new(0x80080234,0x1E0,0x80080220,0x8008021C,[
                    new(0x8008021C,0x1440000A,"Branch",0x80080248),
                    new(0x80080220,0x240401E0,"None",0x00000000),
                    new(0x80080224,0x02602821,"None",0x00000000),
                    new(0x80080228,0x3C028007,"None",0x00000000),
                    new(0x8008022C,0x8C4258CC,"None",0x00000000),
                    new(0x80080230,0x00000000,"None",0x00000000),
                    new(0x80080234,0x0040F809,"None",0x00000000),
                    new(0x80080238,0x26100001,"None",0x00000000),
                ]),
            ],[
                new(0x800803B4,3,null,[
                    new(0x800803B4,0x2463FEA8,"None",0x00000000),
                    new(0x800803B8,0x00031100,"None",0x00000000),
                    new(0x800803BC,0x00431023,"None",0x00000000),
                    new(0x800803C0,0x3C038007,"None",0x00000000),
                    new(0x800803C4,0x8C635908,"None",0x00000000),
                    new(0x800803C8,0x00021080,"None",0x00000000),
                    new(0x800803CC,0x00621821,"None",0x00000000),
                    new(0x800803D0,0x3C018007,"None",0x00000000),
                    new(0x800803D4,0xAC235908,"None",0x00000000),
                ]),
                new(0x800804E4,2,0x800804D8,[
                    new(0x800804D8,0x86620036,"None",0x00000000),
                    new(0x800804DC,0x3C048007,"None",0x00000000),
                    new(0x800804E0,0x8C8461D4,"None",0x00000000),
                    new(0x800804E4,0x2442FEA8,"None",0x00000000),
                    new(0x800804E8,0x00021900,"None",0x00000000),
                    new(0x800804EC,0x00621823,"None",0x00000000),
                    new(0x800804F0,0x3C028007,"None",0x00000000),
                    new(0x800804F4,0x8C425840,"None",0x00000000),
                    new(0x800804F8,0x00031880,"None",0x00000000),
                    new(0x800804FC,0x00431021,"None",0x00000000),
                    new(0x80080500,0x3C018007,"None",0x00000000),
                    new(0x80080504,0xAC225840,"None",0x00000000),
                ]),
            ],new Dictionary<uint,UniversalExeBinding>
            {
                [0x800785D8]=new(0x800785D8,0x8005B410,0x8005B414,0x80084934),
                [0x800758CC]=new(0x800758CC,0x8005B420,0x8005B424,0x80082028),
                [0x80075734]=new(0x80075734,0x8005B430,0x8005B434,0x8007CFB4),
                [0x800758E4]=new(0x800758E4,0x8005B440,0x8005B444,0x80083B8C),
                [0x800756BC]=new(0x800756BC,0x8005B450,0x8005B454,0x80082D94),
                [0x80075694]=new(0x80075694,0x8005B460,0x8005B464,0x8007AE08),
                [0x800757A8]=new(0x800757A8,0x8005B470,0x8005B474,0x8007AEDC),
                [0x800757C0]=new(0x800757C0,0x8005B480,0x8005B484,0x8007B1FC),
                [0x8007567C]=new(0x8007567C,0x8005B490,0x8005B494,0x8007B68C),
                [0x800758C4]=new(0x800758C4,0x8005B4A0,0x8005B4A4,0x8007B4B0),
            }),
    };
    internal static void RequireOverlay(UniversalFlightChestCertificate.Owner o,UniversalOverlay overlay)
    {
        if(Hash(overlay.Bytes)!=o.OverlayHash || overlay.Update!=o.Update || overlay.Init!=o.Init || overlay.CopyBuffer!=o.Copy || o.Callbacks.Any(b=>!overlay.Bindings.TryGetValue(b.Key,out var actual)||actual!=b.Value))throw new InvalidDataException("Flight chest original overlay/callback owner changed.");
    }
    internal static void RequireParticleTextures(UniversalLevelData source,UniversalLevelData destination)
    {
        var s=UniversalParticleDependencies.Records(source);var d=UniversalParticleDependencies.Records(destination);
        foreach(int type in new[]{1,2})if(!s.TryGetValue(type,out var a)||!d.TryGetValue(type,out var b)||!UniversalParticleDependencies.EquivalentTexture(a,source.Vram(),b,destination.Vram()))throw new InvalidDataException("Flight chest native particle footprint/palette differs from its original source.");
    }
    internal static void RequireNative(byte[]? exe)
    {
        if(exe==null || Hash(exe)!="a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998")throw new InvalidDataException("Flight chest original native engine/context owner changed.");
    }
    internal static void RequirePrepared(UniversalLevelData original,UniversalLevelData prepared,int removedRow)
    {
        if(prepared.Bytes.SequenceEqual(original.Bytes))return;
        // Normal Create BIN first exports a native removal as exactly these three
        // raw coordinate words. The removed objective is overwritten in AppendRows;
        // no other prepared controller, actor, properties or resource may change.
        byte[] canonical=prepared.Bytes.ToArray();var scene=original.Scene();
        if(canonical.Length!=original.Bytes.Length || removedRow<0 || removedRow>=scene.RowCount)throw new InvalidDataException("Flight chest destination differs from its issued original course.");
        int start=original.Subfiles()[3].Offset+scene.RowsOffset+removedRow*UniversalLevelData.RowStride+0x0C;
        foreach(int offset in new[]{0,4,8})if(BinaryPrimitives.ReadInt32LittleEndian(canonical.AsSpan(start+offset))!=-480000)throw new InvalidDataException("Flight chest destination has an unowned prepared-row edit.");
        original.Bytes.AsSpan(start,12).CopyTo(canonical.AsSpan(start,12));
        if(!canonical.SequenceEqual(original.Bytes))throw new InvalidDataException("Flight chest destination has an unowned prepared-course edit.");
    }
    internal static UniversalFlightChestCertificate? Capture(string destinationKey,UniversalLevelData prepared,UniversalOverlay destination,UniversalLevelData original,UniversalOverlay originalOverlay,IReadOnlyList<UniversalMobyPlacement> placements,IReadOnlySet<int> removed,Func<string,(Spyro.Editor.Core.Levels.LevelDefinition Level,UniversalLevelData Data,UniversalOverlay Overlay)> source,byte[] exe)
    {
        if(placements.Count!=1 || removed.Count!=1)return null;
        var placement=placements[0];if(placement.SpecialId!=null || !Owners.TryGetValue(placement.SourceLevelKey,out var s) || !Owners.TryGetValue(destinationKey,out var d))return null;
        var donor=source(s.Key);var t=donor.Data.CaptureRow(s.Key,placement.SourceRowIndex);if(t.ClassId!=0x12B)return null;
        if(s.Category!=d.Category)throw new InvalidDataException("Flight chest replacement must retain the original objective category.");
        RequireNative(exe);RequireOverlay(s,donor.Overlay);RequireOverlay(d,destination);RequireOverlay(d,originalOverlay);
        if(Hash(donor.Data.Bytes)!=s.DataHash || Hash(original.Bytes)!=d.DataHash)throw new InvalidDataException("Flight chest replacement requires original source and destination course data.");
        int removedRow=removed.Single();if(!d.Rows.Contains(removedRow) || !s.Rows.Contains(placement.SourceRowIndex))throw new InvalidDataException("Flight chest replacement has no original chest objective owner.");
        RequirePrepared(original,prepared,removedRow);
        // Keep only immutable private original snapshots; no caller-controlled authority object.
        RequireParticleTextures(donor.Data,original);
        return new(s,d,original,removedRow,t,originalOverlay.Bytes.ToArray(),donor.Data,prepared);
    }
}
