using System.Buffers.Binary;
using System.Security.Cryptography;
namespace Spyro.Editor.Core.Universal;

/// <summary>Scoped original-course source/context authority. Requires one original Flight gate
/// replacement and preserves all native course objectives, controller and result callbacks.
/// This certificate is source/static evidence, never runtime acceptance.</summary>
public sealed class UniversalFlightGateCertificate
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
    private readonly UniversalRowTemplate _targetRow;
    internal UniversalFlightGateCertificate(Owner s, Owner d, UniversalLevelData original, int removed, UniversalRowTemplate row,byte[] destinationOverlay,UniversalLevelData sourceData,UniversalLevelData prepared) { _preparedDestination=new(prepared.Bytes.ToArray());_sourceData=new(sourceData.Bytes.ToArray());_destinationOverlay=destinationOverlay.ToArray();_source=s;_destination=d;_originalDestination=new(original.Bytes.ToArray());_removed=removed;_sourceRow=row;_targetRow=original.CaptureRow(d.Key,removed); }
    public string SourceKey => _source.Key;
    public string DestinationKey => _destination.Key;
    public int BonusChildClass => _source.Child;
    public int ObjectiveCategory => _source.Category;
    internal UniversalClassRemapping.SpawnArgument? SpawnAt(UniversalOverlay source,uint pc) { RequireSource(source);var s=_source.Spawns.FirstOrDefault(s=>s.Call==pc);return s==null?null:new(s.Call,s.Producer,s.Class); }
    internal IReadOnlySet<uint> SpawnCalls => _source.Spawns.Select(s=>s.Call).ToHashSet();
    internal IReadOnlyList<int> Classes => [0x161,_source.Child,.. Enumerable.Range(0x176,13)];
    internal void RequireSource(UniversalOverlay overlay) => UniversalFlightGateOwnership.RequireOverlay(_source,overlay);
    internal void RequireBundle(UniversalOverlay source, UniversalOverlay destination, IReadOnlyList<int> classes, byte[]? exe)
    {
        RequireSource(source);UniversalFlightGateOwnership.RequireOverlay(_destination,destination);UniversalFlightGateOwnership.RequireNative(exe);
        if(!classes.Order().SequenceEqual(Classes.Order())) throw new InvalidDataException("Flight gate copy requires its exact issued fifteen-class dependency closure.");
    }
    internal void AddDependencies(UniversalOverlay source,int root,SortedSet<int> spawns)
    {
        RequireSource(source);if(root==0x161)spawns.UnionWith(Classes.Skip(1));
    }
    internal void RequireParticleInputs(UniversalLevelData source,UniversalLevelData destination)
    {
        if(!source.Bytes.SequenceEqual(_sourceData.Bytes) || !destination.Bytes.SequenceEqual(_preparedDestination.Bytes))throw new InvalidDataException("Flight gate particle context requires its issued original source/course inputs.");
    }
    internal UniversalRowTemplate PrepareTemplate(UniversalRowTemplate template)
    {
        if(template.SourceLevelKey!=SourceKey || template.SourceRowIndex!=_sourceRow.SourceRowIndex)throw new InvalidDataException("Flight gate template has no issued source owner.");
        byte[] row=template.Row.ToArray(),props=template.Properties.ToArray();
        _targetRow.Row.AsSpan(0x0C,12).CopyTo(row.AsSpan(0x0C));
        _targetRow.Row.AsSpan(0x44,3).CopyTo(row.AsSpan(0x44));
        _targetRow.Properties.AsSpan(0,16).CopyTo(props);
        return template with{Row=row,Properties=props};
    }
    internal void RequireRow(byte[] row,byte[] props,int expectedClass)
    {
        var issued=PrepareTemplate(_sourceRow);
        if(row.Length!=UniversalLevelData.RowStride || !props.SequenceEqual(issued.Properties))throw new InvalidDataException("Flight gate must retain its issued native target plane and source bonus/category.");
        for(int i=4;i<row.Length;i++)
        {
            if(i is 0x43 or 0x52 or 0x36 or 0x37)continue;
            if(i==0x4A && row[i]==0xFF)continue;
            if(row[i]!=issued.Row[i])throw new InvalidDataException("Flight gate requires its exact issued target pose and original state.");
        }
        if(BinaryPrimitives.ReadUInt16LittleEndian(row.AsSpan(0x36))!=expectedClass)throw new InvalidDataException("Flight gate authored class changed.");
    }
    internal void RequireFinal(UniversalLevelData data,byte[] overlay,int importedRow,int mappedClass)
    {
        int keep=checked((int)(_destination.Copy-UniversalOverlay.Base));
        var originalOverlay=_destinationOverlay;
        if(overlay.Length<keep || !overlay.AsSpan(0,keep).SequenceEqual(originalOverlay.AsSpan(0,keep)))throw new InvalidDataException("Flight gate destination controller/results code changed.");
        var sc=_originalDestination.Scene();if(data.Scene().RowCount!=sc.RowCount || importedRow!=_removed)throw new InvalidDataException("Flight gate must replace exactly its issued native objective row.");
        for(int i=0;i<sc.RowCount;i++)
        {
            var actual=data.CaptureRow(DestinationKey,i);
            if(i==importedRow){RequireRow(actual.Row,actual.Properties,mappedClass);continue;}
            var original=_originalDestination.CaptureRow(DestinationKey,i);
            byte[] ap=actual.Properties.ToArray(),op=original.Properties.ToArray();
            if(!actual.InternalPointers.SequenceEqual(original.InternalPointers) || !actual.ExternalRowPointers.SequenceEqual(original.ExternalRowPointers))throw new InvalidDataException($"Flight gate retained row{i} changed its path/link topology.");
            foreach(var (offset,target) in original.InternalPointers){ap.AsSpan(offset,4).Clear();op.AsSpan(offset,4).Clear();}
            if(!actual.Row.AsSpan(4).SequenceEqual(original.Row.AsSpan(4)) || !ap.SequenceEqual(op))throw new InvalidDataException($"Flight gate changed retained native row{i} objective/controller input.");
        }
        UniversalFlightGateOwnership.RequireParticleTextures(_sourceData,data);
        // Eight roots per original category remain: every unrelated original objective is
        // byte-identical, one gate is replaced with an equal category and original state.
    }
    internal void Patch(UniversalAsmPart part,UniversalOverlay source,IReadOnlyDictionary<int,int> map)
    {
        RequireSource(source);if(!map.Keys.Order().SequenceEqual(Classes.Order()) || map.Values.Distinct().Count()!=map.Count)throw new InvalidDataException("Flight gate has a mixed or incomplete source namespace.");
        UniversalFlightGateOwnership.RequireSourcePacketEdges(_source,source);
        RequireRootFlow(part,source);
        var included=part.Items.Where(x=>x.Word!=null).Select(x=>x.Orig??x.SourcePc).Where(x=>x!=null).Select(x=>x!.Value).ToHashSet();
        foreach(var spawn in _source.Spawns)
        {
            if(!included.Contains(spawn.Call))throw new InvalidDataException("Flight gate copy omitted an issued child spawn.");
            var indices=RequirePacket(part,source,spawn.Words);
            foreach(int at in indices.OrderDescending())
            {
                int pi=part.Items.FindIndex(at, x=>x.Word!=null && (x.Orig??x.SourcePc)==spawn.Producer);
                if(spawn.Class==0x176)
                {
                    // Original delay computes176+s4. The exact original CFG bounds s4
                    // to0..12. V0 is dead here and the next original LUI overwrites it.
                    // Preserve all other registers and every original delay instruction.
                    UniversalAsmPart dispatch=new();string tag="flight_gate_fragment_"+at;
                    foreach(int c in Enumerable.Range(0x176,13))
                    {
                        dispatch.Emit(UniversalMips.EncodeI(9,0,2,c));
                        dispatch.Emit(UniversalMips.EncodeI(5,4,2,0),new(UniversalFixKind.BranchLabel,Label:tag+"_"+c));dispatch.Emit(0);
                        dispatch.Emit(UniversalMips.EncodeI(9,0,4,map[c]));
                        dispatch.Emit(UniversalMips.EncodeJ(2,0),new(UniversalFixKind.JumpLabel,Label:tag+"_done"));dispatch.Emit(0);
                        dispatch.Label(tag+"_"+c);
                    }
                    dispatch.Label(tag+"_done");
                    part.Items.InsertRange(at+3,dispatch.Items);
                    continue;
                }
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
            if(!included.Contains(time.Subtract))throw new InvalidDataException("Flight gate copy omitted issued native bonus arithmetic.");
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
    private static void RequireRootFlow(UniversalAsmPart part,UniversalOverlay source)
    {
        var sw=UniversalDispatchAnalysis.FindUpdateSwitch(source);
        var root=UniversalDispatchAnalysis.Trace(source,sw.TreeStart,0x161,sw.Seeds,sw.ClassRegister);
        var closure=UniversalDispatchAnalysis.Closure(source,sw.Function,[root.Stop],new HashSet<uint>{sw.TreeStart,sw.DefaultStop});
        foreach(uint pc in closure.Instructions)
        {
            var copies=part.Items.Where(x=>x.Word!=null&&(x.Orig??x.SourcePc)==pc).ToArray();
            if(copies.Length==0)throw new InvalidDataException("Flight gate emitted flow omitted a crossing, objective or loop instruction.");
            uint word=source.Word(pc);var kind=UniversalMips.IsPcRelative(word)?UniversalFixKind.Branch:UniversalMips.Op(word)==2?UniversalFixKind.Jump:UniversalFixKind.None;
            uint target=kind==UniversalFixKind.None?0:UniversalMips.BranchTarget(word,pc);
            if(copies.Any(x=>x.Word!=word || x.Fix.Kind!=kind || x.Fix.Orig!=target))throw new InvalidDataException("Flight gate emitted crossing/category/fragment loop changed.");
            if(closure.Instructions.Contains(pc+4))
                foreach(int at in part.Items.Select((x,i)=>(x,i)).Where(x=>x.x.Word!=null&&x.x.Orig==pc).Select(x=>x.i))
                    if(at+1>=part.Items.Count || part.Items[at+1].Word==null || (part.Items[at+1].Orig??part.Items[at+1].SourcePc)!=pc+4)throw new InvalidDataException($"Flight gate emitted original fallthrough/delay adjacency changed at{pc:X8} index{at} next{part.Items[at+1].Orig??part.Items[at+1].SourcePc:X8}.");
        }
        foreach(var item in part.Items.Where(x=>x.Fix.Kind is UniversalFixKind.Branch or UniversalFixKind.Jump && closure.Instructions.Contains(x.Fix.Orig)))
            if((item.Orig??item.SourcePc)==null && item.Fix.Orig!=root.Stop)throw new InvalidDataException("Flight gate emitted flow gained an unowned crossing/loop entry.");
    }
    private static List<int> RequirePacket(UniversalAsmPart part,UniversalOverlay source,Word[] packet)
    {
        var starts=part.Items.Select((x,i)=>(x,i)).Where(x=>x.x.Word!=null && (x.x.Orig??x.x.SourcePc)==packet[0].Pc).Select(x=>x.i).ToList();
        if(starts.Count==0)throw new InvalidDataException("Flight gate emitted packet is missing its source entry.");
        var interior=packet.Skip(1).Select(x=>x.Pc).ToHashSet();
        foreach(int start in starts)
        {
            int j=start;
            foreach(var expected in packet)
            {
                if(j<part.Items.Count&&part.Items[j].Word==null)throw new InvalidDataException("Flight gate emitted packet gained a new interior entry label.");
                if(j>=part.Items.Count)throw new InvalidDataException("Flight gate emitted packet is truncated.");
                var item=part.Items[j++];var fk=Enum.Parse<UniversalFixKind>(expected.FixKind);
                if((item.Orig??item.SourcePc)!=expected.Pc || item.Word!=expected.Value || item.Fix.Kind!=fk || item.Fix.Orig!=expected.FixTarget)throw new InvalidDataException("Flight gate emitted packet changed, reordered or gained an unowned consumer.");
            }
        }
        // Repeated source packets are all authenticated; no extra copied interior can hide
        // outside a matching packet, and newly introduced direct interior entries refuse.
        foreach(var expected in packet)
            if(part.Items.Count(x=>x.Word!=null&&(x.Orig??x.SourcePc)==expected.Pc)!=starts.Count)throw new InvalidDataException("Flight gate emitted packet has an unmatched duplicate interior.");
        foreach(var item in part.Items.Where(x=>x.Fix.Kind is UniversalFixKind.Branch or UniversalFixKind.Jump && interior.Contains(x.Fix.Orig)))
        {
            if((item.Orig??item.SourcePc) is not uint pc || source.Word(pc)!=item.Word || UniversalMips.BranchTarget(source.Word(pc),pc)!=item.Fix.Orig)throw new InvalidDataException("Flight gate emitted flow has an unowned incoming packet entry.");
        }
        return starts;
    }
}

public static class UniversalFlightGateOwnership
{
    private static string Hash(ReadOnlySpan<byte> x)=>Convert.ToHexStringLower(SHA256.HashData(x));
    internal static void RequireSourcePacketEdges(UniversalFlightGateCertificate.Owner owner,UniversalOverlay overlay)
    {
        var packets=owner.Spawns.Select(p=>p.Words).Concat(owner.Times.Select(p=>p.Words)).ToArray();
        var interior=packets.SelectMany(p=>p.Skip(1)).Select(w=>w.Pc).ToHashSet();
        for(uint pc=overlay.Update;pc<overlay.Init;pc+=4)
        {
            uint w=overlay.Word(pc);if((UniversalMips.IsPcRelative(w)||UniversalMips.Op(w) is 2 or 3)&&interior.Contains(UniversalMips.BranchTarget(w,pc)))throw new InvalidDataException("Flight gate original packet has an alternate interior entry.");
        }
        var sw=UniversalDispatchAnalysis.FindUpdateSwitch(overlay);
        foreach(int c in new[]{0x161,owner.Child}.Concat(Enumerable.Range(0x176,13)))
        {
            var tr=UniversalDispatchAnalysis.Trace(overlay,sw.TreeStart,c,sw.Seeds,sw.ClassRegister);
            var cl=UniversalDispatchAnalysis.Closure(overlay,sw.Function,[tr.Stop],new HashSet<uint>{sw.TreeStart});
            if(cl.Tables.SelectMany(t=>t.Targets).Any(interior.Contains))throw new InvalidDataException("Flight gate original jump table enters packet interior.");
        }
    }
    // Original USA full source owners and exact typed packets; proof scripts/receipts
    // are private evidence. No caller can supply authority bytes or callback identities.
    private static readonly Dictionary<string,UniversalFlightGateCertificate.Owner> Owners=new(StringComparer.OrdinalIgnoreCase)
    {
["sunnyflight"]=new("sunnyflight","c5c03eed4a43a1f2557b248851438583509895aa95efa7e0e92236c9b7243dca","42b4e8a637ff91f87b884a9beea3266299d01ae36d36b437436d247f78812f59",0x8007CFB4,0x8008249C,0x80084ED0,0x15B,1,[0,1,2,3,4,5,6,7],[
new(0x80080738,0x15B,0x8008073C,0x80080728,[new(0x80080728,0x8E240014,"None",0x00000000),new(0x8008072C,0x3C028007,"None",0x00000000),new(0x80080730,0x8C4258CC,"None",0x00000000),new(0x80080734,0x00000000,"None",0x00000000),new(0x80080738,0x0040F809,"None",0x00000000),new(0x8008073C,0x24840158,"None",0x00000000)]),
new(0x80080854,0x176,0x80080840,0x8008083C,[new(0x8008083C,0x1440003E,"Branch",0x80080938),new(0x80080840,0x26840176,"None",0x00000000),new(0x80080844,0x02602821,"None",0x00000000),new(0x80080848,0x3C028007,"None",0x00000000),new(0x8008084C,0x8C4258CC,"None",0x00000000),new(0x80080850,0x00000000,"None",0x00000000),new(0x80080854,0x0040F809,"None",0x00000000),new(0x80080858,0x26940001,"None",0x00000000)])
],[
new(0x80080588,3,0x80080580,[new(0x80080580,0x86630036,"None",0x00000000),new(0x80080584,0x00000000,"None",0x00000000),new(0x80080588,0x2463FEA8,"None",0x00000000),new(0x8008058C,0x00031100,"None",0x00000000),new(0x80080590,0x00431023,"None",0x00000000),new(0x80080594,0x3C038007,"None",0x00000000),new(0x80080598,0x8C635908,"None",0x00000000),new(0x8008059C,0x00021080,"None",0x00000000),new(0x800805A0,0x00621821,"None",0x00000000),new(0x800805A4,0x3C018007,"None",0x00000000),new(0x800805A8,0xAC235908,"None",0x00000000)]),
new(0x800806B8,2,0x800806AC,[new(0x800806AC,0x86620036,"None",0x00000000),new(0x800806B0,0x3C048007,"None",0x00000000),new(0x800806B4,0x8C8461D4,"None",0x00000000),new(0x800806B8,0x2442FEA8,"None",0x00000000),new(0x800806BC,0x00021900,"None",0x00000000),new(0x800806C0,0x00621823,"None",0x00000000),new(0x800806C4,0x3C028007,"None",0x00000000),new(0x800806C8,0x8C425840,"None",0x00000000),new(0x800806CC,0x00031880,"None",0x00000000),new(0x800806D0,0x00431021,"None",0x00000000),new(0x800806D4,0x3C018007,"None",0x00000000),new(0x800806D8,0xAC225840,"None",0x00000000)])
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
["nightflight"]=new("nightflight","8b419612d1764fbaf1b13afea845ef853de5a632a8a371e095270ff099bcaa2d","3891e7af5f83825dbebc80952e173fbbea8125653d4b00fc7b1980a1128b873a",0x8007CFB4,0x800819BC,0x80083BF0,0x15A,2,[14,15,16,17,18,19,30,31],[
new(0x8008022C,0x15A,0x80080230,0x8008021C,[new(0x8008021C,0x8E240014,"None",0x00000000),new(0x80080220,0x3C028007,"None",0x00000000),new(0x80080224,0x8C4258CC,"None",0x00000000),new(0x80080228,0x00000000,"None",0x00000000),new(0x8008022C,0x0040F809,"None",0x00000000),new(0x80080230,0x24840158,"None",0x00000000)]),
new(0x80080348,0x176,0x80080334,0x80080330,[new(0x80080330,0x1440003E,"Branch",0x8008042C),new(0x80080334,0x26840176,"None",0x00000000),new(0x80080338,0x02602821,"None",0x00000000),new(0x8008033C,0x3C028007,"None",0x00000000),new(0x80080340,0x8C4258CC,"None",0x00000000),new(0x80080344,0x00000000,"None",0x00000000),new(0x80080348,0x0040F809,"None",0x00000000),new(0x8008034C,0x26940001,"None",0x00000000)])
],[
new(0x80080088,3,0x80080080,[new(0x80080080,0x86630036,"None",0x00000000),new(0x80080084,0x00000000,"None",0x00000000),new(0x80080088,0x2463FEA8,"None",0x00000000),new(0x8008008C,0x00031100,"None",0x00000000),new(0x80080090,0x00431023,"None",0x00000000),new(0x80080094,0x3C038007,"None",0x00000000),new(0x80080098,0x8C635908,"None",0x00000000),new(0x8008009C,0x00021080,"None",0x00000000),new(0x800800A0,0x00621821,"None",0x00000000),new(0x800800A4,0x3C018007,"None",0x00000000),new(0x800800A8,0xAC235908,"None",0x00000000)]),
new(0x800801B4,2,0x800801A8,[new(0x800801A8,0x86620036,"None",0x00000000),new(0x800801AC,0x3C048007,"None",0x00000000),new(0x800801B0,0x8C8461D4,"None",0x00000000),new(0x800801B4,0x2442FEA8,"None",0x00000000),new(0x800801B8,0x00021900,"None",0x00000000),new(0x800801BC,0x00621823,"None",0x00000000),new(0x800801C0,0x3C028007,"None",0x00000000),new(0x800801C4,0x8C425840,"None",0x00000000),new(0x800801C8,0x00031880,"None",0x00000000),new(0x800801CC,0x00431021,"None",0x00000000),new(0x800801D0,0x3C018007,"None",0x00000000),new(0x800801D4,0xAC225840,"None",0x00000000)])
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
["crystalflight"]=new("crystalflight","3871b093ef681dc64ec693dd3ba11021949251122459f57773636a69c05e8be0","e8d91a7c5f9f5d06b95ac01ade6fa4423485fd3d5dd75dd0dc15cc8319d46a59",0x8007CFB4,0x80081F0C,0x80084390,0x15A,1,[15,16,17,18,19,20,21,22],[
new(0x800806A8,0x15A,0x800806AC,0x80080698,[new(0x80080698,0x8E240014,"None",0x00000000),new(0x8008069C,0x3C028007,"None",0x00000000),new(0x800806A0,0x8C4258CC,"None",0x00000000),new(0x800806A4,0x00000000,"None",0x00000000),new(0x800806A8,0x0040F809,"None",0x00000000),new(0x800806AC,0x24840158,"None",0x00000000)]),
new(0x800807C4,0x176,0x800807B0,0x800807AC,[new(0x800807AC,0x1440003E,"Branch",0x800808A8),new(0x800807B0,0x26840176,"None",0x00000000),new(0x800807B4,0x02602821,"None",0x00000000),new(0x800807B8,0x3C028007,"None",0x00000000),new(0x800807BC,0x8C4258CC,"None",0x00000000),new(0x800807C0,0x00000000,"None",0x00000000),new(0x800807C4,0x0040F809,"None",0x00000000),new(0x800807C8,0x26940001,"None",0x00000000)])
],[
new(0x800804F8,3,null,[new(0x800804F8,0x2463FEA8,"None",0x00000000),new(0x800804FC,0x00031100,"None",0x00000000),new(0x80080500,0x00431023,"None",0x00000000),new(0x80080504,0x3C038007,"None",0x00000000),new(0x80080508,0x8C635908,"None",0x00000000),new(0x8008050C,0x00021080,"None",0x00000000),new(0x80080510,0x00621821,"None",0x00000000),new(0x80080514,0x3C018007,"None",0x00000000),new(0x80080518,0xAC235908,"None",0x00000000)]),
new(0x80080628,2,0x8008061C,[new(0x8008061C,0x86620036,"None",0x00000000),new(0x80080620,0x3C048007,"None",0x00000000),new(0x80080624,0x8C8461D4,"None",0x00000000),new(0x80080628,0x2442FEA8,"None",0x00000000),new(0x8008062C,0x00021900,"None",0x00000000),new(0x80080630,0x00621823,"None",0x00000000),new(0x80080634,0x3C028007,"None",0x00000000),new(0x80080638,0x8C425840,"None",0x00000000),new(0x8008063C,0x00031880,"None",0x00000000),new(0x80080640,0x00431021,"None",0x00000000),new(0x80080644,0x3C018007,"None",0x00000000),new(0x80080648,0xAC225840,"None",0x00000000)])
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
["wildflight"]=new("wildflight","364fe3d540e2308b548f290f923994f7ccba13b357632e8bf67d9d4af3d31c48","5069a570d9cce769fed272d8c8fe24a56ade3317370fe5c688ebb43126570e97",0x8007CFB4,0x8008223C,0x80084844,0x15A,1,[9,10,11,12,13,14,23,24],[
new(0x800808FC,0x15A,0x80080900,0x800808EC,[new(0x800808EC,0x8E240014,"None",0x00000000),new(0x800808F0,0x3C028007,"None",0x00000000),new(0x800808F4,0x8C4258CC,"None",0x00000000),new(0x800808F8,0x00000000,"None",0x00000000),new(0x800808FC,0x0040F809,"None",0x00000000),new(0x80080900,0x24840158,"None",0x00000000)]),
new(0x80080A18,0x176,0x80080A04,0x80080A00,[new(0x80080A00,0x1440003E,"Branch",0x80080AFC),new(0x80080A04,0x26840176,"None",0x00000000),new(0x80080A08,0x02602821,"None",0x00000000),new(0x80080A0C,0x3C028007,"None",0x00000000),new(0x80080A10,0x8C4258CC,"None",0x00000000),new(0x80080A14,0x00000000,"None",0x00000000),new(0x80080A18,0x0040F809,"None",0x00000000),new(0x80080A1C,0x26940001,"None",0x00000000)])
],[
new(0x8008074C,3,null,[new(0x8008074C,0x2463FEA8,"None",0x00000000),new(0x80080750,0x00031100,"None",0x00000000),new(0x80080754,0x00431023,"None",0x00000000),new(0x80080758,0x3C038007,"None",0x00000000),new(0x8008075C,0x8C635908,"None",0x00000000),new(0x80080760,0x00021080,"None",0x00000000),new(0x80080764,0x00621821,"None",0x00000000),new(0x80080768,0x3C018007,"None",0x00000000),new(0x8008076C,0xAC235908,"None",0x00000000)]),
new(0x8008087C,2,0x80080870,[new(0x80080870,0x86620036,"None",0x00000000),new(0x80080874,0x3C048007,"None",0x00000000),new(0x80080878,0x8C8461D4,"None",0x00000000),new(0x8008087C,0x2442FEA8,"None",0x00000000),new(0x80080880,0x00021900,"None",0x00000000),new(0x80080884,0x00621823,"None",0x00000000),new(0x80080888,0x3C028007,"None",0x00000000),new(0x8008088C,0x8C425840,"None",0x00000000),new(0x80080890,0x00031880,"None",0x00000000),new(0x80080894,0x00431021,"None",0x00000000),new(0x80080898,0x3C018007,"None",0x00000000),new(0x8008089C,0xAC225840,"None",0x00000000)])
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
    };
    internal static void RequireOverlay(UniversalFlightGateCertificate.Owner o,UniversalOverlay overlay)
    {
        if(Hash(overlay.Bytes)!=o.OverlayHash || overlay.Update!=o.Update || overlay.Init!=o.Init || overlay.CopyBuffer!=o.Copy || o.Callbacks.Any(b=>!overlay.Bindings.TryGetValue(b.Key,out var actual)||actual!=b.Value))throw new InvalidDataException("Flight gate original overlay/callback owner changed.");
    }
    internal static void RequireParticleTextures(UniversalLevelData source,UniversalLevelData destination)
    {
        var s=UniversalParticleDependencies.Records(source);var d=UniversalParticleDependencies.Records(destination);
        foreach(int type in new[]{1,2})if(!s.TryGetValue(type,out var a)||!d.TryGetValue(type,out var b)||!UniversalParticleDependencies.EquivalentTexture(a,source.Vram(),b,destination.Vram()))throw new InvalidDataException("Flight gate native particle footprint/palette differs from its original source.");
    }
    internal static void RequireNative(byte[]? exe)
    {
        if(exe==null || Hash(exe)!="a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998")throw new InvalidDataException("Flight gate original native engine/context owner changed.");
    }
    internal static void RequirePrepared(UniversalLevelData original,UniversalLevelData prepared,int removedRow)
    {
        if(prepared.Bytes.SequenceEqual(original.Bytes))return;
        // Normal Create BIN first exports a native removal as exactly these three
        // raw coordinate words. The removed objective is overwritten in AppendRows;
        // no other prepared controller, actor, properties or resource may change.
        byte[] canonical=prepared.Bytes.ToArray();var scene=original.Scene();
        if(canonical.Length!=original.Bytes.Length || removedRow<0 || removedRow>=scene.RowCount)throw new InvalidDataException("Flight gate destination differs from its issued original course.");
        int start=original.Subfiles()[3].Offset+scene.RowsOffset+removedRow*UniversalLevelData.RowStride+0x0C;
        foreach(int offset in new[]{0,4,8})if(BinaryPrimitives.ReadInt32LittleEndian(canonical.AsSpan(start+offset))!=-480000)throw new InvalidDataException("Flight gate destination has an unowned prepared-row edit.");
        original.Bytes.AsSpan(start,12).CopyTo(canonical.AsSpan(start,12));
        if(!canonical.SequenceEqual(original.Bytes))throw new InvalidDataException("Flight gate destination has an unowned prepared-course edit.");
    }
    internal static UniversalFlightGateCertificate? Capture(string destinationKey,UniversalLevelData prepared,UniversalOverlay destination,UniversalLevelData original,UniversalOverlay originalOverlay,IReadOnlyList<UniversalMobyPlacement> placements,IReadOnlySet<int> removed,Func<string,(Spyro.Editor.Core.Levels.LevelDefinition Level,UniversalLevelData Data,UniversalOverlay Overlay)> source,byte[] exe)
    {
        if(placements.Count!=1 || removed.Count!=1)return null;
        var placement=placements[0];if(placement.SpecialId!=null || !Owners.TryGetValue(placement.SourceLevelKey,out var s) || !Owners.TryGetValue(destinationKey,out var d))return null;
        var donor=source(s.Key);var t=donor.Data.CaptureRow(s.Key,placement.SourceRowIndex);if(t.ClassId!=0x161)return null;
        if(s.Category!=d.Category)throw new InvalidDataException("Flight gate replacement must retain the original objective category.");
        RequireNative(exe);RequireOverlay(s,donor.Overlay);RequireOverlay(d,destination);RequireOverlay(d,originalOverlay);
        if(Hash(donor.Data.Bytes)!=s.DataHash || Hash(original.Bytes)!=d.DataHash)throw new InvalidDataException("Flight gate destination and source data must match the original course.");
        int removedRow=removed.Single();if(!d.Rows.Contains(removedRow) || !s.Rows.Contains(placement.SourceRowIndex))throw new InvalidDataException("Flight gate replacement has no original gate objective owner.");
        RequirePrepared(original,prepared,removedRow);
        var target=original.CaptureRow(destinationKey,removedRow);
        if(placement.X!=BinaryPrimitives.ReadInt32LittleEndian(target.Row.AsSpan(12)) || placement.Y!=BinaryPrimitives.ReadInt32LittleEndian(target.Row.AsSpan(16)) || placement.Z!=BinaryPrimitives.ReadInt32LittleEndian(target.Row.AsSpan(20)) || placement.Yaw!=target.Row[0x46])throw new InvalidDataException("Flight gate replacement requires its original target pose.");
        // Keep only immutable private original snapshots; no caller-controlled authority object.
        RequireParticleTextures(donor.Data,original);
        return new(s,d,original,removedRow,t,originalOverlay.Bytes.ToArray(),donor.Data,prepared);
    }
}
