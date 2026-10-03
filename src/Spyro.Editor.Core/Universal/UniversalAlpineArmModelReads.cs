namespace Spyro.Editor.Core.Universal;

/// <summary>AlpineFD's three current-animation speed lookups keep the allocated
/// physical class in g_Models. Source/copy/native residual-value ownership only;
/// this adds no machine instructions and grants no placement/state authority.</summary>
internal static class UniversalAlpineArmModelReads
{
    internal static readonly uint[] Reads=[0x80082C58,0x80082E2C,0x80082F04];
    internal static IReadOnlyList<uint> MarkOwned(UniversalAsmPart part,UniversalLevelData data,UniversalOverlay source,byte[] exe,IReadOnlyDictionary<int,int>? classMap=null)
    {
        var present=Reads.Where(pc=>part.Items.Any(i=>(i.SourcePc??i.Orig)==pc)).ToArray();if(present.Length==0)return [];
        UniversalAlpineArmResources.ProveSource(data,source);UniversalAlpineArmNative.RequireNative(exe);
        // This private prerequisite is issued before other adapters mutate the part.
        // Authenticate the COMPLETE emitted source owner so newly added incoming
        // code/table/label/raw materialization cannot inherit the packet proof.
        string tag=part.Items.FirstOrDefault(i=>i.Label!=null)?.Label??throw new InvalidDataException("Missing Alpine copied owner label.");
        var expected=UniversalBehaviorBundleBuilder.BuildPrunedUpdate(source,[0xFD,0x10F,0x13B],tag,classMap).Part;
        if(part.Items.Any(i=>i.ClassReadOwned&&!Reads.Contains((i.SourcePc??i.Orig)??0))||part.LenientTables!=expected.LenientTables||!part.Items.Select(i=>i with{ClassReadOwned=false}).SequenceEqual(expected.Items)||part.Tables.Count!=expected.Tables.Count||expected.Tables.Any(t=>!part.Tables.TryGetValue(t.Key,out var values)||!t.Value.SequenceEqual(values)))
            throw new InvalidDataException("Alpine model read complete copied source/replay/argument/table/incoming owner changed.");
        var sw=UniversalDispatchAnalysis.FindUpdateSwitch(source);uint dispatch=source.Word(sw.TreeStart-4);
        if(UniversalMips.Rs(dispatch)!=19||UniversalMips.SImm(dispatch)!=54)throw new InvalidDataException("AlpineFD model read lost its original dispatch actor.");
        var (code,tables)=UniversalDispatchAnalysis.Closure(source,sw.Function,[sw.Function],new HashSet<uint>());
        var predecessors=UniversalDestinationGemCollectionAdapter.Predecessors(source,code,tables);
        foreach(uint read in present)
        {
            uint[] packet=[0x86630036,0x9262003C,0x00031880,0x3C018007,0x00230821,0x8C236378,0x00021080,0x00431021,0x8C420038,0,0x9042000C,
                read==0x80082E2C?0x08022132u:0u,0xA2620041];
            // C58 kills the residual model root in v1 with the following LBU.
            // E2C jumps to native529E4 with A1=1: its bound prefix never reads
            // incoming v1 and replaces it with row+3C before any later use.
            if(read==0x80082C58&&source.Word(0x80082C8C)!=0x9263003C)throw new InvalidDataException("AlpineFD residual model root kill changed.");
            RequirePacket(read,packet);
            // F04's conditional successors either call529E4(A1=1), or run
            // native17990 (which does not read v1) and overwrite v1 at82F78.
            // Full source + complete copied owner above bind this branch graph;
            // no ABI clobber or skipped native call is assumed.
            if(read==0x80082F04)
            {
                uint[] distance=[0x8C810000,0x8C820004,0x8CA60000,0x8CA70004];
                for(int i=0;i<distance.Length;i++)if(System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(exe.AsSpan((int)(0x80017990-UniversalExeBindings.ExeBase)+4*i))!=distance[i])throw new InvalidDataException("Alpine model read distance helper producer changed.");
            }
        }
        if(present.Contains(0x80082E2C))RequirePacket(0x800884C8,[0x02602021,0x0C014A79,0x24050001],false);
        foreach(uint read in present)for(int i=0;i<part.Items.Count;i++)if((part.Items[i].SourcePc??part.Items[i].Orig)==read)part.Items[i]=part.Items[i] with{ClassReadOwned=true};
        return present;
        void RequirePacket(uint start,uint[] words,bool linear=true)
        {
            for(int n=0;n<words.Length;n++){uint at=start+(uint)n*4;if(source.Word(at)!=words[n]||linear&&n>0&&(!predecessors.TryGetValue(at,out var incoming)||!incoming.SetEquals([at-4])))throw new InvalidDataException($"AlpineFD model read changed source packet/incoming owner at {at:X8}.");}
            foreach(int first in part.Items.Select((i,n)=>(i,n)).Where(x=>(x.i.SourcePc??x.i.Orig)==start).Select(x=>x.n))
            {
                if(first+words.Length>part.Items.Count)throw new InvalidDataException("AlpineFD model read missing copied packet.");
                for(int n=0;n<words.Length;n++){uint at=start+(uint)n*4;var i=part.Items[first+n];var fix=UniversalMips.Op(words[n])==2?new UniversalFix(UniversalFixKind.Jump,UniversalMips.BranchTarget(words[n],at)):default;
                    if((i.SourcePc??i.Orig)!=at||i.Word!=words[n]||i.Fix!=fix)throw new InvalidDataException("AlpineFD model read copied bytes/arguments/relocations changed.");}
                if(linear)UniversalNativePlayerDamageCalls.RequireCopiedEntries(part,start,start+(uint)(words.Length-1)*4,first,first+words.Length-1);
            }
            if(!part.Items.Any(i=>(i.SourcePc??i.Orig)==start))throw new InvalidDataException("AlpineFD model read retained native tail missing.");
        }
    }
}
