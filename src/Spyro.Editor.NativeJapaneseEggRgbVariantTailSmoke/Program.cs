using System.Buffers.Binary;
using System.Reflection;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;

int checks=0;
void Check(bool condition,string reason){if(!condition)throw new Exception(reason);checks++;}
void Refuse(Action action,string reason){try{action();}catch(InvalidDataException){checks++;return;}catch(ArgumentException){checks++;return;}throw new Exception("Expected refusal: "+reason);}
static uint W(byte[] b,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(at));
NativeJapaneseEggVisualPlacement[] placements=[new("egg-a",72704,47104,6656,32,1),new("egg-b",72704,49152,6656,96,1)];

void Verify(NativeJapaneseEggRgbVariantTailPlan p,NativeJapaneseEggRgbEditPlan first,NativeJapaneseEggRgbEditPlan second)
{
    byte[] before=p.CopyCurrentLevelData(),after=p.CopyLevelData();
    Check(before.SequenceEqual(p.Integration.CopyLevelData())&&p.RevertExact(after).SequenceEqual(before),"Complete current-body ancestry and inverse.");
    Check(p.Integration.RevertDataExact(p.RevertExact(after)).SequenceEqual(p.Integration.CopyOriginalLevelData()),"Composition inverse reaches the unchanged original retail/fixture resource.");
    Check(p.DistinctPackageOwnersProven&&p.Variants.Count==2&&p.Rows.Count==2,"Two explicit detached package owners and two intended row bindings.");
    Check(p.Variants.Select(v=>v.ProjectedLoadedRoot).Distinct().Count()==2,"Projected model roots are genuinely disjoint.");
    Check(p.GrowthBytes==((first.CopyPackage().Length+second.CopyPackage().Length+0x7FF)&~0x7FF)&&after.Length==before.Length+p.GrowthBytes,"Exact aggregate sector growth, not a guessed root gap.");
    Check(before.AsSpan(0x50,0x180).SequenceEqual(after.AsSpan(0x50,0x180)),"All 64 root and class slots remain byte-exact; no aliases.");
    Check(p.CopyScene().SequenceEqual(p.Integration.CopyScene()),"Every existing/appended row, private byte, property and scene fixup is unchanged.");
    Check(p.ProjectedModelRamStart==p.Integration.RuntimeMemory.ModelRamStart&&p.ProjectedSceneRamStart==p.Integration.RuntimeMemory.SceneRamStart+p.GrowthBytes&&
        p.ProjectedSceneReadEnd==p.Integration.RuntimeMemory.SceneReadEnd+p.GrowthBytes,"Normal model start retained; complete scene shifts by exact model growth.");
    Check(p.ProjectedNormalGap==p.Integration.RuntimeMemory.NormalReservedBufferGapBytes-p.GrowthBytes&&p.ProjectedNormalGap>=0,"Normal gap is charged, without claiming peak safety.");
    Check(!p.NativePerInstanceBindingProven&&!p.LoadGenerationOwnerProven&&!p.RuntimeFitProven&&!p.SelectorHookImplemented&&!p.PublicationApproved,"Allocation cannot confer relocation callback/scene-generation/hook/publication authority.");
    Refuse(p.RequireNativePerInstanceBinding,"unregistered packages are not an active per-instance selector");
    Check(p.Graph.Installs.Count==2&&p.Graph.References.Count==0,"Existing dependency compiler installs exactly two source-form packages, no pre-rebased disk pointers.");
    foreach(var (variant,edit) in p.Variants.Zip(new[]{first,second}))
    {
        Check(p.CopyVariantPackage(variant.IntendedRowId).SequenceEqual(edit.CopyPackage())&&variant.PackageSha256==edit.FinalPackageSha256,"Exact authored or native-original variant bytes in its new owner.");
        Check(p.VariantExtent(variant.IntendedRowId).Space==p.FinalDomain,"Every variant has the actual Final domain, not a caller-labelled replacement.");
        foreach(var field in variant.LoaderFields)
            Check(W(after,variant.PackageDataOffset+field.PackageFieldOffset)==field.SourceWord&&field.RuntimeTargetAddress>=variant.ProjectedLoadedRoot&&
                field.RuntimeTargetAddress+4<=variant.ProjectedLoadedRoot+variant.ByteLength,"All eight disk pointers remain original-relative and project inside the independent root.");
        var row=p.Rows.Single(r=>r.Id==variant.IntendedRowId);var old=p.Integration.Rows.Single(r=>r.Placement.Id==row.Id);
        Check(row.ProjectedRowAddress==p.ProjectedSceneRamStart+old.RowSceneOffset&&row.ProjectedPrivateAddress==p.ProjectedSceneRamStart+old.PrivateSceneOffset&&
            row.IntendedModelRoot==variant.ProjectedLoadedRoot,"Intended mapping uses actual row/private schema metadata, not guessed row fields.");
        byte[] copied=p.CopyVariantPackage(variant.IntendedRowId);copied[0]^=1;
        Check(p.CopyVariantPackage(variant.IntendedRowId).SequenceEqual(edit.CopyPackage()),"Returned package copies cannot mutate owned output.");
    }
    foreach(var r in p.Layout.Regions.Where(r=>r.Original is not null&&r.Id!="header"))
        Check(before.AsSpan((int)r.Original!.Offset,(int)r.Original.ByteLength).SequenceEqual(after.AsSpan((int)r.Final.Offset,(int)r.Final.ByteLength)),"Whole retained subfile complement: "+r.Id);
    var padding=p.PaddingOwner;
    Check(padding is not null&&after.AsSpan((int)padding.Offset,(int)padding.ByteLength).ContainsAnyExcept((byte)0)==false,"New sector padding is exact zero, not recycled source bytes.");
    var wrongDomain=new NativeAddressSpace(p.BeforeDomain.Kind,p.BeforeDomain.OwnerIdentity,p.BeforeDomain.Revision,p.BeforeDomain.ByteLength);
    Refuse(()=>p.Layout.Map(new(wrongDomain,0,4)),"equal-labelled replacement source domain");
    Refuse(()=>p.Layout.Map(new(p.FinalDomain,0,4)),"Final owner passed as source");
    Refuse(()=>p.CopyVariantPackage("absent"),"unknown intended row");
    HashSet<int> afterOffsets=[0,0x10,0x14,0x18,0x50,0x150,after.Length-1];
    foreach(var r in p.Layout.Regions){afterOffsets.Add((int)r.Final.Offset);afterOffsets.Add((int)r.Final.EndExclusive-1);}
    foreach(var v in p.Variants)foreach(var f in v.LoaderFields)afterOffsets.Add(v.PackageDataOffset+f.PackageFieldOffset);
    foreach(var v in p.Variants)foreach(int offset in new[]{first.Source.NearRange.PackageOffset,first.Source.FarRange.PackageOffset})
    {afterOffsets.Add(v.PackageDataOffset+offset);afterOffsets.Add(v.PackageDataOffset+offset+3);afterOffsets.Add(v.PackageDataOffset+offset+69*4+3);}
    foreach(int at in afterOffsets)
    {
        byte[] changed=after.ToArray();changed[at]^=1;
        Refuse(()=>p.RevertExact(changed),"tampered complete afterimage byte "+at.ToString("X"));
    }
    foreach(int at in new[]{0,0x10,0x50,0x150,p.OriginalModelOffset,checked((int)W(before,0x18)),before.Length-1})
    {byte[] changed=before.ToArray();changed[at]^=1;Refuse(()=>p.RequireCurrent(changed),"tampered current body "+at.ToString("X"));}
    Refuse(()=>p.RevertExact(after[..^1]),"truncated output");Refuse(()=>p.RevertExact(after.Concat(new byte[1]).ToArray()),"extra output tail");
    Refuse(()=>p.RequireCurrent(before[..^1]),"truncated current");Refuse(()=>p.RequireCurrent(before.Concat(new byte[1]).ToArray()),"extra current tail");
    after[0]^=1;before[0]^=1;
    Check(p.CopyLevelData()[0]!=(after[0])&&p.CopyCurrentLevelData()[0]!=(before[0]),"Whole-body copy outputs are detached.");
}

void ExecuteNative(NativeJapaneseEggRgbVariantTailPlan p)
{
    var cpu=new RenderProbeCpu();const uint stop=0x80010000,slot=0x8007674C;
    cpu.Copy(0x8000F800,p.Integration.CopyOriginalExecutable());byte[] body=p.CopyLevelData();
    cpu.Copy(p.ProjectedModelRamStart,body.AsSpan(p.OriginalModelOffset,checked((int)W(body,0x14))).ToArray());
    cpu.Put(slot,0x12345678); // guarded canary, not a claimed native registration
    foreach(var v in p.Variants)
    {
        cpu.Regs[4]=v.ProjectedLoadedRoot;cpu.Regs[31]=stop;cpu.Writes.Clear();cpu.Run(0x800133E0,stop);
        Check(cpu.Regs[2]==v.ProjectedLoadedRoot&&cpu.Get(slot)==0x12345678,"Real loader returns independently projected root without registering or changing class F5.");
        Check(cpu.Writes.All(a=>a>=v.ProjectedLoadedRoot&&a<v.ProjectedLoadedRoot+v.ByteLength),"Loader writes only this exact model allocation.");
        foreach(var f in v.LoaderFields)Check(cpu.Get(v.ProjectedLoadedRoot+(uint)f.PackageFieldOffset)==f.ProjectedLoadedWord,"Native loader equals typed address projection.");
        var pointerBytes=v.LoaderFields.SelectMany(f=>Enumerable.Range(f.PackageFieldOffset,4)).ToHashSet();byte[] bytes=p.CopyVariantPackage(v.IntendedRowId);
        for(int i=0;i<bytes.Length;i++)if(!pointerBytes.Contains(i))Check(cpu.Get(v.ProjectedLoadedRoot+(uint)i,1)==bytes[i],"Native loader preserves complete RGB/geometry/frame/CODE complement.");
    }
    byte[] scene=p.CopyScene();cpu.Copy(p.ProjectedSceneRamStart,scene);
    int moby=p.Integration.OriginalSceneSource.RowTableOffset-8;
    cpu.Regs[16]=p.ProjectedSceneRamStart+(uint)moby;cpu.Regs[5]=cpu.Regs[16];cpu.Run(0x80013F24,0x80013FC4);
    Check(cpu.Get(0x80075828)==p.ProjectedSceneRamStart+(uint)moby+8,"Actual scene loader uses the shifted complete scene row base.");
    Check(cpu.Get(0x80075890)==p.Rows[^1].ProjectedRowAddress+0x58,"Both intended rows remain before the actual native sentinel.");
    int fix=p.Integration.OriginalSceneSource.FixupOffset+24;
    cpu.Regs[16]=p.ProjectedSceneRamStart+(uint)fix;cpu.Regs[17]=0;cpu.Regs[18]=p.ProjectedSceneRamStart;cpu.Writes.Clear();cpu.Run(0x80014040,0x80014084);
    var fields=Enumerable.Range(0,(int)W(scene,fix)).Select(i=>W(scene,fix+4+4*i)).ToArray();
    foreach(uint f in fields)Check(cpu.Get(p.ProjectedSceneRamStart+f)==p.ProjectedSceneRamStart+W(scene,(int)f),"Actual shifted-scene fixup is owned and unchanged.");
    Check(cpu.Writes.Count==fields.Length*4&&cpu.Writes.All(a=>fields.Any(f=>a>=p.ProjectedSceneRamStart+f&&a<p.ProjectedSceneRamStart+f+4)),"All native fixup writes remain inside declared fields.");
    foreach(var row in p.Rows)Check(cpu.Get(row.ProjectedRowAddress)==row.ProjectedPrivateAddress&&cpu.Get(row.ProjectedRowAddress+0x36,2)==0xF5,"Actual row+00 private pointer and unchanged class remain source-schema exact.");
}

static byte[] Model70()
{
    byte[] old=Fixtures.Model(),b=new byte[0x2C8];old.AsSpan(0,0x88).CopyTo(b);
    Fixtures.Put(b,0x3C,70u<<16|2);Fixtures.Put(b,0x5C,0x130);Fixtures.Put(b,0x60,0xFF000248);Fixtures.Put(b,0x68,0xFF000250);
    for(int i=0;i<70;i++){Fixtures.Put(b,0x88+i*4,0xFF102030u+(uint)i);Fixtures.Put(b,0x1A0+i*4,0x102030u+(uint)i);}
    old.AsSpan(0x90,16).CopyTo(b.AsSpan(0x2B8));return b;
}
Check(typeof(NativeJapaneseEggRgbVariantTailPlan).GetConstructors(BindingFlags.Public|BindingFlags.Instance).Length==0,"No caller body/label can directly create source-issued tail ownership.");
if(args.Length==0)
{
    var asset=NativeJapaneseEggModelAssetPlan.Fixture(Model70());var source=NativeJapaneseEggRgbEditSource.Fixture(asset);
    var first=source.NativeOriginal();var second=source.CreateAuthored(new uint[70],Enumerable.Repeat(0xFFFFFFu,70).ToArray());
    byte[] original=Fixtures.Resource();var visual=NativeJapaneseEggVisualBehaviorPlan.Fixture(asset,original,Fixtures.JapaneseOverlay(),Fixtures.UsaOverlay(),Fixtures.Executable());
    var integration=NativeJapaneseEggArtisansIntegrationPlan.Fixture(asset,visual,original,Fixtures.Scene(0xF5),NativeJapaneseObjectAppearance.DecodeFixture(Fixtures.Profiles()),placements);
    var p=NativeJapaneseEggRgbVariantTailPlan.Fixture(integration,first,second);Verify(p,first,second);
    Check(!p.SourceCertified&&!p.NormalLoadProjectionSourceBound,"Fixture cannot certify source or native projection.");
    Check(p.PlanSha256==NativeJapaneseEggRgbVariantTailPlan.Fixture(integration,first,second).PlanSha256,"Deterministic detached fixture output and identity.");
    var reissued=NativeJapaneseEggRgbEditSource.Fixture(asset).NativeOriginal();
    Refuse(()=>NativeJapaneseEggRgbVariantTailPlan.Fixture(integration,first,reissued),"different RGB source identity");
    Refuse(()=>NativeJapaneseEggRgbVariantTailPlan.Capture(null!,integration,first,second),"null USA issuer");
    Console.WriteLine($"JP_EGG_RGB_VARIANT_TAIL_SOURCE_FREE_OK checks={checks} sourceReads=0 nativeBinding=false");
}
else if(args.Length==3&&args[0]=="--source")
{
    using var jp=NativeRegionalSourceSession.Open(args[1],NativeDiscRevision.JapanScps10083);
    using var usa=NativeRegionalSourceSession.Open(args[2],NativeDiscRevision.UsaScus94228);
    var integration=NativeJapaneseEggArtisansIntegrationPlan.Capture(jp,usa,placements);
    var asset=NativeJapaneseEggModelAssetPlan.Capture(jp,usa);var source=NativeJapaneseEggRgbEditSource.Capture(asset,usa);
    var first=source.CreateAuthored(Enumerable.Repeat(0x332211u,70).ToArray(),Enumerable.Repeat(0x665544u,70).ToArray());
    var second=source.CreateAuthored(Enumerable.Repeat(0xCCBBAAu,70).ToArray(),Enumerable.Repeat(0xFFEEDDu,70).ToArray());
    var p=NativeJapaneseEggRgbVariantTailPlan.Capture(usa,integration,first,second);Verify(p,first,second);ExecuteNative(p);
    Check(p.SourceCertified&&p.NormalLoadProjectionSourceBound&&p.GrowthBytes==0x1800&&p.Variants.All(v=>v.ByteLength==0x808),"Actual two-package tail and source-bound normal projection.");
    Refuse(()=>NativeJapaneseEggRgbVariantTailPlan.Capture(jp,integration,first,second),"Japanese destination issuer");
    Refuse(()=>NativeJapaneseEggRgbVariantTailPlan.Fixture(integration,first,second),"native ancestry cannot be downgraded to fixture");
    Refuse(()=>NativeJapaneseEggRgbVariantTailPlan.Capture(usa,null!,first,second),"missing integration");
    Refuse(()=>NativeJapaneseEggRgbVariantTailPlan.Capture(usa,integration,null!,second),"missing first variant");
    Refuse(()=>NativeJapaneseEggRgbVariantTailPlan.Capture(usa,integration,first,null!),"missing second variant");
    Check(p.PlanSha256==NativeJapaneseEggRgbVariantTailPlan.Capture(usa,integration,first,second).PlanSha256,"Actual source plan determinism.");
    jp.VerifyUnchanged();usa.VerifyUnchanged();
    Console.WriteLine($"JP_EGG_RGB_VARIANT_TAIL_SOURCE_OK checks={checks} growth={p.GrowthBytes:X} roots={string.Join(',',p.Variants.Select(v=>v.ProjectedLoadedRoot.ToString("X8")))} scene={p.ProjectedSceneRamStart:X8} sceneEnd={p.ProjectedSceneReadEnd:X8} normalGap={p.ProjectedNormalGap:X} plan={p.PlanSha256} data={p.FinalDataSha256} graph={p.DependencyGraphSha256} nativeBinding=false sourceOrCandidateWrites=0");
}
else throw new ArgumentException("Usage: no arguments, or --source <exact-JP-BIN> <exact-USA-BIN>.");
