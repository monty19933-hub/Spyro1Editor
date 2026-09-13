using System.Collections;
using System.Reflection;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Scene;

internal static class FamilyRowSpanCases
{
    private const string WizardId="ffffffffffffffffffffffffffffffff",GrassId="11111111111111111111111111111111";

    internal static void Free(Action<bool,string> check)
    {
        var type=typeof(NativeFreshMobyFamilyRowSpanPlan);
        check(type.IsSealed&&!type.IsPublic&&type.GetConstructors().Length==0,
            "Neutral row-span preflight has no public caller-byte or destination authority.");
        check(type.GetProperties(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)
            .All(p=>p.Name is not ("SourceCertified" or "NativeImportApproved" or "DestinationBound" or "RuntimeVerified" or "PublicationApproved")),
            "Schema geometry does not issue source, destination, native or publication claims.");

        // Neutral schema geometry only. No graph, template, captured image or
        // invented source certificate is constructed to calculate final sizes.
        var domain=new NativeAddressSpace(NativeAddressKind.LevelData,"neutral-row-span-schema",NativeAddressRevision.Original,0x20000);
        NativeMobyDependencyPrivateSchema Row(int i,int length,IReadOnlyList<NativeMobyDependencyOwnerField>? owners=null)=>
            new("role."+i,20+i,(ushort)(0x100+i),
                new("row",new(domain,0x100+i*0x58,0x58),new string('a',64)),
                new("private",new(domain,0x4000+i*0x100,length),new string('b',64)),0x3000,owners??[],true);
        var wizard=Row(0,0x50);var key=Row(1,4);
        List<NativeMobyDependencyOwnerField> links=[new(0,0)];var child=Row(2,0x18,links);var grass=Row(3,8);
        List<NativeMobyDependencyPrivateSchema> wizardRows=[wizard],familyRows=[key,child],grassRows=[grass];
        List<NativeFreshMobyScenePointerReference> internalPointers=[
            new(0,NativeFreshMobySceneOwnerKind.Private,0,0,NativeFreshMobySceneOwnerKind.Private,0x28,0x1028)];
        List<NativeFreshMobyFamilyRowSpanRequest> requests=[
            new(WizardId,wizardRows,internalPointers),new(GrassId,familyRows,[]),
            new("22222222222222222222222222222222",grassRows,[])];
        var plan=NativeFreshMobyFamilyRowSpanPlan.Create(requests);
        check(plan.Placements.Select(p=>p.EditorId).SequenceEqual(requests.Select(p=>p.EditorId))&&
            plan.Placements.Select(p=>p.FirstRowIndex).SequenceEqual(new[]{0,1,3})&&
            plan.Placements.Select(p=>p.RowCount).SequenceEqual(new[]{1,2,1})&&
            plan.Rows.Select(r=>r.SourceInstanceIndex).SequenceEqual(new[]{20,21,22,23}),
            "Authored placement order and its contiguous row spans survive lexical ID order and multirow expansion.");
        check(plan.Layout.PrivateByteLengths.SequenceEqual(new[]{0x50,4,0x18,8})&&
            plan.Layout.PlacedCount==4&&plan.Layout.DynamicCapacity==32&&plan.Layout.FixupCount==5,
            "Final geometry derives complete private lengths and native rows before any destination graph exists.");
        var expected=new NativeFreshMobyScenePointerReference[]{
            Pointer(0,0x1000),internalPointers[0],Pointer(1,0x1100),Pointer(2,0x1200),Pointer(3,0x1300)};
        check(plan.PointerReferences.SequenceEqual(expected),
            "Fixups preserve each placement's row pointer then its internal pointers before the next row.");
        check(plan.Rows[2].OwnerFields.Single()==new NativeMobyDependencyOwnerField(0,0)&&
            !plan.PointerReferences.Any(p=>p.FieldRowIndex==2&&p.FieldOwner==NativeFreshMobySceneOwnerKind.Private&&p.FieldOffset==0),
            "Family integer owner links remain local role indices for the allocator; they are not scene pointers.");

        var lifted=NativeFreshMobyFamilyRowSpanPlan.Create([
            new(WizardId,[grass],[]),new(GrassId,[wizard],[internalPointers[0]])]);
        check(lifted.PointerReferences.SequenceEqual(new[]{Pointer(0,0x1300),Pointer(1,0x1000),
            internalPointers[0] with{FieldRowIndex=1,TargetRowIndex=1}}),
            "Explicit private pointers lift local family indices through the placement's actual first row.");
        var withinFamily=NativeFreshMobyFamilyRowSpanPlan.Create([
            new(WizardId,[wizard,child],[internalPointers[0]])]);
        check(withinFamily.PointerReferences.SequenceEqual(new[]{Pointer(0,0x1000),internalPointers[0],Pointer(1,0x1200)}),
            "A multirow family's first-row internal pointer precedes that family's next row pointer.");

        wizardRows.Clear();familyRows.Reverse();grassRows.Clear();links[0]=new(4,1);
        internalPointers[0]=internalPointers[0] with{ExpectedSourceValue=0};requests.Reverse();requests.Clear();
        check(plan.Placements.Select(p=>p.FirstRowIndex).SequenceEqual(new[]{0,1,3})&&
            plan.Rows.Select(r=>r.SourceInstanceIndex).SequenceEqual(new[]{20,21,22,23})&&
            plan.Rows[2].OwnerFields.Single()==new NativeMobyDependencyOwnerField(0,0)&&
            plan.PointerReferences.SequenceEqual(expected),
            "Caller request, row, owner-field and pointer mutations cannot change retained preflight.");
        ReadOnly(plan.Placements,check);ReadOnly(plan.Rows,check);ReadOnly(plan.PointerReferences,check);
        foreach(var row in plan.Rows)ReadOnly(row.OwnerFields,check);

        Reject<ArgumentNullException>(()=>NativeFreshMobyFamilyRowSpanPlan.Create(null!),check);
        Reject<InvalidDataException>(()=>NativeFreshMobyFamilyRowSpanPlan.Create([]),check);
        Reject<InvalidDataException>(()=>NativeFreshMobyFamilyRowSpanPlan.Create([new(WizardId,[],[])]),check);
        Reject<InvalidDataException>(()=>NativeFreshMobyFamilyRowSpanPlan.Create([new(WizardId,[wizard],[]),new(WizardId,[grass],[])]),check);
        foreach(string id in new[]{"",new string('0',32),new string('F',32),"not-an-editor-id"})
            Reject<InvalidDataException>(()=>NativeFreshMobyFamilyRowSpanPlan.Create([new(id,[wizard],[])]),check);
        Reject<InvalidDataException>(()=>NativeFreshMobyFamilyRowSpanPlan.Create([new(WizardId,new CountOnlyRows(257),[])]),check);
        Reject<InvalidDataException>(()=>NativeFreshMobyFamilyRowSpanPlan.Create([
            new(WizardId,new CountOnlyRows(128),[]),new(GrassId,new CountOnlyRows(129),[])]),check);
        var bounded=NativeFreshMobyFamilyRowSpanPlan.Create([
            new(WizardId,Enumerable.Range(0,128).Select(i=>Row(i,4)).ToArray(),[]),
            new(GrassId,Enumerable.Range(0,128).Select(i=>Row(i,4)).ToArray(),[])]);
        check(bounded.Rows.Count==256&&bounded.Placements[1].FirstRowIndex==128&&bounded.Layout.FixupCount==256,
            "The bound applies to expanded rows, including repeated source schemas in separate authored families.");

        var final=new NativeAddressSpace(domain.Kind,domain.OwnerIdentity,NativeAddressRevision.Final,domain.ByteLength);
        Reject<InvalidDataException>(()=>NativeFreshMobyFamilyRowSpanPlan.Create([new(WizardId,
            [wizard with{Row=wizard.Row with{Extent=new(final,wizard.Row.Extent.Offset,0x58)}}],[])]),check);
        foreach(var bad in new[]{
            new NativeFreshMobyScenePointerReference(1,NativeFreshMobySceneOwnerKind.Private,0,0,NativeFreshMobySceneOwnerKind.Private,0x28,0x1028),
            new(0,NativeFreshMobySceneOwnerKind.Private,0,1,NativeFreshMobySceneOwnerKind.Private,0x28,0x1028),
            new(0,NativeFreshMobySceneOwnerKind.Private,1,0,NativeFreshMobySceneOwnerKind.Private,0x28,0x1028),
            new(0,NativeFreshMobySceneOwnerKind.Private,0,0,NativeFreshMobySceneOwnerKind.Private,0x50,0x1050),
            Pointer(0,0x1000)})
            Reject<InvalidDataException>(()=>NativeFreshMobyFamilyRowSpanPlan.Create([new(WizardId,[wizard],[bad])]),check);
        var ownLink=Row(2,0x18,[new(0,0)]);
        Reject<InvalidDataException>(()=>NativeFreshMobyFamilyRowSpanPlan.Create([new(WizardId,[key,ownLink],
            [new(1,NativeFreshMobySceneOwnerKind.Private,0,0,NativeFreshMobySceneOwnerKind.Private,0,0x1100)])]),check);
        Reject<ArgumentNullException>(()=>plan.RequireTemplates(null!),check);
        Reject<ArgumentNullException>(()=>plan.RequireAllocation(null!),check);
        Console.WriteLine("NATIVE_FRESH_FAMILY_ROW_SPAN_PREFLIGHT_FREE_OK destination=false runtime=false");
    }

    internal static void Source(NativeWizardFamilyAssetPlan wizard,NativeMobyFamilyAssetPlan grass,
        NativeFreshMobyFamilyCompositionPlan p1,NativeFreshMobyFamilyCompositionPlan p2,Action<bool,string> check)
    {
        var wizardRows=wizard.CapturePrivateStorageRows();var wizardPointers=wizard.CapturePrivateStorageInternalPointers();
        check(wizardRows.Count==1&&Same(wizardRows[0],p1.Allocation.Families[0].Template.Rows[0])&&
            wizardPointers.SequenceEqual(new[]{new NativeFreshMobyScenePointerReference(0,NativeFreshMobySceneOwnerKind.Private,0,
                0,NativeFreshMobySceneOwnerKind.Private,0x28,0xC378)}),
            "Wizard preflight exposes the actual original schema and checked owned internal pointer without a destination graph.");
        check(ReferenceEquals(wizardRows[0].Row.Extent.Space,wizard.OriginalDataDomain)&&
            ReferenceEquals(wizardRows[0].Properties.Extent.Space,wizard.OriginalDataDomain)&&
            System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(wizard.ReadPrivateSource(wizardRows[0].Properties.Extent))==0xC378,
            "The Wizard pointer preimage is read from its actual complete retained private source.");
        ReadOnly(wizardRows,check);ReadOnly(wizardRows[0].OwnerFields,check);ReadOnly(wizardPointers,check);
        var inputs=new[]{new NativeFreshMobyFamilyRowSpanRequest(WizardId,wizardRows,wizardPointers),
            new(GrassId,grass.InstanceTemplate.Rows,[])};
        var preflight=NativeFreshMobyFamilyRowSpanPlan.Create(inputs);
        var templates=p2.Allocation.Families.Select(f=>new NativeMobyDependencyInstantiationRequest(f.Id,f.Template)).ToArray();
        preflight.RequireTemplates(templates);preflight.RequireAllocation(p2.Allocation);
        p1.RowSpans.RequireAllocation(p1.Allocation);p2.RowSpans.RequireTemplates(templates);p2.RowSpans.RequireAllocation(p2.Allocation);
        check(ReferenceEquals(p1.RowSpans.Layout,p1.Layout)&&ReferenceEquals(p2.RowSpans.Layout,p2.Layout)&&
            p2.RowSpans.PointerReferences.SequenceEqual(preflight.PointerReferences)&&
            preflight.PointerReferences.Select(p=>p.FieldOwner).SequenceEqual(new[]{
                NativeFreshMobySceneOwnerKind.Row,NativeFreshMobySceneOwnerKind.Private,NativeFreshMobySceneOwnerKind.Row})&&
            preflight.PointerReferences.Select(p=>p.ExpectedSourceValue).SequenceEqual(new uint[]{0xC350,0xC378,0x11ACC}),
            "Composer retains its preflight layout and preserves historical Wizard-row/internal/Grass-row fixup order.");
        Reject<InvalidDataException>(()=>preflight.RequireTemplates(templates.Reverse().ToArray()),check);
        Reject<InvalidDataException>(()=>preflight.RequireTemplates(templates[..1]),check);
        Reject<InvalidDataException>(()=>preflight.RequireTemplates([templates[0],templates[1] with{Id=WizardId}]),check);
        Reject<InvalidDataException>(()=>preflight.RequireAllocation(p1.Allocation),check);

        var row=wizardRows[0];var original=row.Row.Extent.Space;
        var equalDomain=new NativeAddressSpace(original.Kind,original.OwnerIdentity,original.Revision,original.ByteLength);
        var changes=new[]{
            row with{Role="changed-role"},row with{SourceInstanceIndex=row.SourceInstanceIndex+1},row with{NativeClass=0x11C},
            row with{RequiresBytePersistentOwner=!row.RequiresBytePersistentOwner},
            row with{Row=row.Row with{Id="changed-read"}},row with{Row=row.Row with{ExpectedSha256=new string('a',64)}},
            row with{Properties=row.Properties with{ExpectedSha256=new string('b',64)}},
            row with{PropertiesPointerBaseOffset=row.PropertiesPointerBaseOffset+4},
            row with{OwnerFields=[new(4,0)]},
            row with{Row=row.Row with{Extent=new(equalDomain,row.Row.Extent.Offset,row.Row.Extent.ByteLength)},
                Properties=row.Properties with{Extent=new(equalDomain,row.Properties.Extent.Offset,row.Properties.Extent.ByteLength)}},
            row with{Row=row.Row with{Extent=new(original,row.Row.Extent.Offset+4,row.Row.Extent.ByteLength)}},
            row with{Properties=row.Properties with{Extent=new(original,row.Properties.Extent.Offset+4,row.Properties.Extent.ByteLength)}}};
        foreach(var changed in changes)
            Reject<InvalidDataException>(()=>NativeFreshMobyFamilyRowSpanPlan.Create([
                new(WizardId,[changed],wizardPointers),inputs[1]]).RequireTemplates(templates),check);
        check(wizardRows.Count==1&&Same(wizardRows[0],row)&&p2.CopyInverseBaseBody().SequenceEqual(p2.BaseBodyAncestor.CopyLevelData()),
            "Rejected schema/domain substitutions leave the real source and complete retained inverse unchanged.");
        Console.WriteLine("NATIVE_FRESH_FAMILY_ROW_SPAN_ACTUAL_BINDINGS_OK chestAdmission=false runtime=false");
    }

    private static NativeFreshMobyScenePointerReference Pointer(int index,uint before)=>
        new(index,NativeFreshMobySceneOwnerKind.Row,0,index,NativeFreshMobySceneOwnerKind.Private,0,before);
    private static bool Same(NativeMobyDependencyPrivateSchema a,NativeMobyDependencyPrivateSchema b)=>
        a.Role==b.Role&&a.SourceInstanceIndex==b.SourceInstanceIndex&&a.NativeClass==b.NativeClass&&a.Row==b.Row&&a.Properties==b.Properties&&
        a.PropertiesPointerBaseOffset==b.PropertiesPointerBaseOffset&&a.RequiresBytePersistentOwner==b.RequiresBytePersistentOwner&&
        a.OwnerFields.SequenceEqual(b.OwnerFields);
    private static void ReadOnly<T>(IReadOnlyList<T> values,Action<bool,string> check)
    {if(values is IList<T> list)Reject<NotSupportedException>(()=>list.Clear(),check);else check(true,"No mutable list interface.");}
    private static void Reject<T>(Action action,Action<bool,string> check)where T:Exception
    {try{action();}catch(T){check(true,"Expected row-span refusal "+typeof(T).Name);return;}throw new InvalidDataException("Expected "+typeof(T).Name);}
    private sealed class CountOnlyRows(int count):IReadOnlyList<NativeMobyDependencyPrivateSchema>
    {
        public int Count=>count;
        public NativeMobyDependencyPrivateSchema this[int index]=>throw new InvalidOperationException("Expanded row bound must be checked before reading or allocating row snapshots.");
        public IEnumerator<NativeMobyDependencyPrivateSchema> GetEnumerator()=>throw new InvalidOperationException("Expanded row bound must be checked before enumeration.");
        IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
    }
}
