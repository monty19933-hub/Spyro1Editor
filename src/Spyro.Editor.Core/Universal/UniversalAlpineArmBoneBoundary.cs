namespace Spyro.Editor.Core.Universal;

/// <summary>Private exact linked-FD bone0 output backstop. Source in-domain
/// bone output is unchanged; outside-grid output restores prior actorXYZ before
/// the unchanged source continuation reinserts its native collision chain.</summary>
internal static class UniversalAlpineArmBoneBoundary
{
    internal const uint Native=0x80052D64;
    internal const string Label="alpine_cohort_linked_bone_boundary";
    internal static readonly uint[] Calls=[0x800862F4];
    internal static string Caller(uint pc)=>$"alpine_cohort_linked_bone_call_{pc:X8}";
    internal static void Bind(UniversalAsmPart part,UniversalOverlay source)
    {
        foreach(uint pc in Calls)
        {
            // Whole source/copied graph is checked by WorldCalls. Bind the exact
            // native parentFD / bone0 / own actorXYZ O32 argument packet again.
            for(int n=-2;n<=1;n++)
            {
                uint q=unchecked(pc+(uint)(n*4));uint expected=n switch{-2=>0x02002021,-1=>0x00002821,0=>0x0C014B59,_=>0x2666000C};
                var items=part.Items.Where(i=>(i.SourcePc??i.Orig)==q).ToArray();
                if(source.Word(q)!=expected||items.Length!=1||items[0].Word!=expected||items[0].Fix.Kind!=UniversalFixKind.None)
                    throw new InvalidDataException("Alpine linked bone0 requires its exact owned source/copied parent/output argument packet.");
            }
            int at=part.Items.FindIndex(i=>(i.SourcePc??i.Orig)==pc);
            part.Items[at]=part.Items[at] with{Fix=new(UniversalFixKind.JalLabel,Label:Label)};
            part.Items.Insert(at,new(null,default,null,Caller(pc)));
        }
    }
    internal static UniversalAsmPart Build()
    {
        UniversalAsmPart a=new();a.Label(Label);
        uint I(int op,int rs,int rt,int imm)=>UniversalMips.EncodeI(op,rs,rt,imm);
        a.Emit(I(9,29,29,-64));a.Emit(I(43,29,31,60));a.Emit(I(43,29,6,28));
        // T0/T1 are only wrapper scratch. First snapshot originalXYZ; restore
        // them before the original helper so native inputs/used GTE state match.
        a.Emit(I(43,29,8,32));a.Emit(I(43,29,9,36));
        foreach(int off in new[]{0,4,8}){a.Emit(I(35,6,8,off));a.Emit(0);a.Emit(I(43,29,8,16+off));}
        a.Emit(I(35,29,8,32));a.Emit(I(35,29,9,36));a.Emit(0);
        a.Emit(UniversalMips.EncodeJ(3,Native));a.Emit(0);
        a.Emit(I(43,29,8,32));a.Emit(I(43,29,9,36));
        foreach(int off in new[]{0,4})
        {
            a.Emit(I(35,29,8,28));a.Emit(0);a.Emit(I(35,8,8,off));a.Emit(0);
            a.Emit(I(9,8,8,-8192));a.Emit(I(15,0,9,3));a.Emit(I(13,9,9,0xC000));a.Emit(0x0109402B);
            a.Emit(I(4,8,0,0),new(UniversalFixKind.BranchLabel,Label:Label+"_restore_xyz"));a.Emit(0);
        }
        a.Emit(UniversalMips.EncodeJ(2,0),new(UniversalFixKind.JumpLabel,Label:Label+"_return"));a.Emit(0);
        a.Label(Label+"_restore_xyz");a.Emit(I(35,29,8,28));a.Emit(0);
        foreach(int off in new[]{0,4,8}){a.Emit(I(35,29,9,16+off));a.Emit(0);a.Emit(I(43,8,9,off));}
        a.Label(Label+"_return");a.Emit(I(35,29,8,32));a.Emit(I(35,29,9,36));a.Emit(I(35,29,31,60));a.Emit(0);
        a.Emit(I(9,29,29,64));a.Emit(UniversalMips.JrRa);a.Emit(0);return a;
    }
}
