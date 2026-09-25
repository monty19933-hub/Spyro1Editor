using System.Buffers.Binary;
using System.Reflection;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;

int checks=0;
void Check(bool ok,string reason){if(!ok)throw new Exception(reason);checks++;}
void Refuse(Action action,string reason){try{action();}catch(InvalidDataException){checks++;return;}catch(ArgumentException){checks++;return;}throw new Exception("Expected refusal: "+reason);}
static uint W(byte[] b,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(at));
static ushort H(byte[] b,int at)=>BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(at));
NativeJapaneseEggVisualPlacement[] placements=[new("egg-a",72704,47104,6656,32,1),new("egg-b",72704,49152,6656,96,1)];

void Verify(NativeJapaneseEggAuthoredClassesPlan p)
{
    byte[] before=p.Integration.CopyLevelData(),after=p.CopyLevelData(),scene=p.CopyScene();
    Check(p.RevertDataExact(after).SequenceEqual(before),"Exact inverse to current full resource.");
    Check(p.Integration.RevertDataExact(p.RevertDataExact(after)).SequenceEqual(p.Integration.CopyOriginalLevelData()),"Full inverse reaches retail/fixture source.");
    Check(p.RevertOverlayExact(p.CopyOverlay()).SequenceEqual(p.Integration.CopyOverlay()),"Exact full overlay inverse.");
    Check(p.Integration.RevertOverlayExact(p.RevertOverlayExact(p.CopyOverlay())).SequenceEqual(p.Integration.CopyOriginalOverlay()),"Overlay inverse reaches original source.");
    Check(p.CopyExecutable().SequenceEqual(p.Integration.CopyExecutable()),"No renderer hook or further executable changes.");
    Check(p.Bindings.Select(b=>b.AuthoredDestinationClass).SequenceEqual(new ushort[]{0x1E9,0x1F2})&&p.Bindings.All(b=>b.SourceActorId==0xF5),"Authored destination identities are separate from native F5 donor.");
    Check(p.Bindings.Select(b=>b.ProjectedRoot).Distinct().Count()==2,"Distinct complete model roots.");
    Check(p.Bindings.Select(b=>b.ProjectedPrivateAddress).Distinct().Count()==2,"Independent private state owners.");
    Check(p.Bindings.All(b=>b.PaletteOrigin=="authored-rgb"),"Explicit authored palette provenance.");
    Check(!p.GenericEffectsApproved&&!p.DynamicSpawnApproved&&!p.PickupApproved&&!p.PublicationApproved&&!p.LitPixelsVerified&&!p.RuntimeFitProven,"No effect/spawn/pickup/publication/pixel or peak authority.");
    Check(p.GlobalUnusedNamespaceProven==false&&p.NativeRendererUnchanged,"No broad free-namespace or custom EXE renderer claim.");
    Refuse(p.RequireActivation,"detached composition does not activate");
    Check(p.Graph.References.Count==2,"Existing dependency compiler resolves two actual header root references.");
    foreach(var b in p.Bindings)
    {
        Check(H(after,b.ClassFieldOffset)==b.AuthoredDestinationClass&&W(after,b.RootFieldOffset)==b.PackageDataOffset,"Owned native registration fields.");
        Check(H(scene,b.RowSceneOffset+0x36)==b.AuthoredDestinationClass,"Only the intended row uses the authored class.");
        Check(H(p.Integration.CopyScene(),b.RowSceneOffset+0x36)==0xF5,"Native donor row remains F5 in ancestor.");
        Check(p.CopyVariantPackage(b.AuthoredDestinationClass).SequenceEqual(b.Edit.CopyPackage()),"Source-form RGB-only package remains exact.");
        foreach(var f in b.LoaderFields)Check(W(after,b.PackageDataOffset+f.PackageFieldOffset)==f.SourceWord,"Disk pointers not prematurely rebased.");
        foreach(ushort id in new ushort[]{0,0xF5,0x1FE,0x1FF,0x200,0x1F9})Refuse(()=>p.Reservation.RequireSelected(id),"unissued destination "+id.ToString("X"));
    }
    byte[] oldScene=p.Integration.CopyScene();var changed=p.Bindings.SelectMany(b=>new[]{b.RowSceneOffset+0x36,b.RowSceneOffset+0x37}).ToHashSet();
    Check(Enumerable.Range(0,scene.Length).Where(i=>!changed.Contains(i)).All(i=>scene[i]==oldScene[i]),"Every row/private/fixup/scene complement byte is preserved.");
    byte[] originalOverlay=p.Integration.CopyOriginalOverlay(),finalOverlay=p.CopyOverlay();
    foreach(int id in Enumerable.Range(0,65536))
    {
        var original=DispatchProbe.Trace(originalOverlay,(ushort)id,0,0);
        var actual=DispatchProbe.Trace(finalOverlay,(ushort)id,p.GuardPc,p.GuardByteLength);
        bool selected=id is 0xF5 or 0x1E9 or 0x1F2;
        if(actual.Target!=(selected?p.Integration.VisualAncestor.EntryPc:original.Target)||!actual.Registers.SequenceEqual(original.Registers))
            throw new Exception("Dispatch/register fallback differs for class "+id.ToString("X4"));
    }
    Check(true,"All65,536 class identities preserve dispatch/register fallback except the three explicit visual entries.");
    foreach(int at in new[]{0,0x10,0x14,0x50,0x150,after.Length-1}.Concat(p.Bindings.SelectMany(b=>new[]{b.RootFieldOffset,b.ClassFieldOffset,b.PackageDataOffset,b.PackageDataOffset+0x45C,(int)W(after,0x18)+b.RowSceneOffset+0x36})).Distinct())
    {byte[] bad=after.ToArray();bad[at]^=1;Refuse(()=>p.RevertDataExact(bad),"tampered complete data "+at.ToString("X"));}
    foreach(int at in new[]{0,0x319C,0x32C4,(int)(p.GuardPc-0x8007AA38),p.CopyOverlay().Length-1})
    {byte[] bad=p.CopyOverlay();bad[at]^=1;Refuse(()=>p.RevertOverlayExact(bad),"tampered full overlay "+at.ToString("X"));}
    Refuse(()=>p.RevertDataExact(after[..^1]),"truncated data");Refuse(()=>p.RevertOverlayExact(p.CopyOverlay()[..^1]),"truncated overlay");
    byte originalByte=after[0];after[0]^=1;Check(p.CopyLevelData()[0]==originalByte,"Returned arrays cannot mutate plan.");
}

Check(typeof(NativeJapaneseEggAuthoredClassReservation).GetConstructors().Length==0&&typeof(NativeJapaneseEggAuthoredClassesPlan).GetConstructors().Length==0,"No public caller-authored reservation or composition constructor.");
if(args.Length==0)
{
    byte[] old=Fixtures.Model(),model=new byte[0x2C8];old.AsSpan(0,0x88).CopyTo(model);Fixtures.Put(model,0x3C,70u<<16|2);
    Fixtures.Put(model,0x5C,0x130);Fixtures.Put(model,0x60,0xFF000248);Fixtures.Put(model,0x68,0xFF000250);
    for(int i=0;i<70;i++){Fixtures.Put(model,0x88+4*i,0xFF102030);Fixtures.Put(model,0x1A0+4*i,0x102030);}old.AsSpan(0x90,16).CopyTo(model.AsSpan(0x2B8));
    var asset=NativeJapaneseEggModelAssetPlan.Fixture(model);var source=NativeJapaneseEggRgbEditSource.Fixture(asset);byte[] original=Fixtures.Resource();
    byte[] usaOverlay=Fixtures.UsaOverlay();Fixtures.Put(usaOverlay,0x32C4,0x080215E0);
    var visual=NativeJapaneseEggVisualBehaviorPlan.Fixture(asset,original,Fixtures.JapaneseOverlay(),usaOverlay,Fixtures.Executable());
    var integration=NativeJapaneseEggArtisansIntegrationPlan.Fixture(asset,visual,original,Fixtures.Scene(0xF5),NativeJapaneseObjectAppearance.DecodeFixture(Fixtures.Profiles()),placements);
    var a=source.CreateAuthored(Enumerable.Repeat(0x332211u,70).ToArray(),Enumerable.Repeat(0x665544u,70).ToArray());
    var b=source.CreateAuthored(Enumerable.Repeat(0xCCBBAAu,70).ToArray(),Enumerable.Repeat(0xFFEEDDu,70).ToArray());
    var reservation=NativeJapaneseEggAuthoredClassReservation.Fixture([0,1,0x1FE,0x1FF]);
    var p=NativeJapaneseEggAuthoredClassesPlan.Fixture(reservation,integration,a,b);Verify(p);
    Check(!p.SourceCertified&&!p.Reservation.SourceCertified,"Fixtures cannot issue source authority.");
    Check(p.PlanSha256==NativeJapaneseEggAuthoredClassesPlan.Fixture(reservation,integration,a,b).PlanSha256,"Deterministic composition.");
    foreach(ushort taken in new ushort[]{0x1E9,0x1F2})Refuse(()=>NativeJapaneseEggAuthoredClassReservation.Fixture([0,0x1FE,0x1FF,taken]),"candidate collision");
    Refuse(()=>NativeJapaneseEggAuthoredClassesPlan.Fixture(reservation,integration,a,NativeJapaneseEggRgbEditSource.Fixture(asset).NativeOriginal()),"reissued RGB source identity");
    Refuse(()=>NativeJapaneseEggAuthoredClassesPlan.Capture(null!,reservation,integration,a,b),"missing exact source session");
    Console.WriteLine($"JP_EGG_AUTHORED_CLASSES_SOURCE_FREE_OK checks={checks} exhaustiveDispatchClasses=65536");
}
else if(args.Length==3&&args[0]=="--source")
{
    using var jp=NativeRegionalSourceSession.Open(args[1],NativeDiscRevision.JapanScps10083);
    using var usa=NativeRegionalSourceSession.Open(args[2],NativeDiscRevision.UsaScus94228);
    var integration=NativeJapaneseEggArtisansIntegrationPlan.Capture(jp,usa,placements);
    var asset=NativeJapaneseEggModelAssetPlan.Capture(jp,usa);var source=NativeJapaneseEggRgbEditSource.Capture(asset,usa);
    var a=source.CreateAuthored(Enumerable.Repeat(0x332211u,70).ToArray(),Enumerable.Repeat(0x665544u,70).ToArray());
    var b=source.CreateAuthored(Enumerable.Repeat(0xCCBBAAu,70).ToArray(),Enumerable.Repeat(0xFFEEDDu,70).ToArray());
    var reservation=NativeJapaneseEggAuthoredClassReservation.Capture(usa);
    Check(reservation.KnownReservedClasses.Count==370&&reservation.LocalRegistrationCount==1403&&reservation.PlacedRowCount==4877&&reservation.SharedClasses.Count==54,"Complete known census plus two explicit dynamic engine reservations.");
    var p=NativeJapaneseEggAuthoredClassesPlan.Capture(usa,reservation,integration,a,b);Verify(p);
    NativeCases.Run(p,Check);Check(p.SourceCertified,"Exact source-issued composition.");
    Refuse(()=>NativeJapaneseEggAuthoredClassReservation.Capture(jp),"wrong regional reservation");
    Refuse(()=>NativeJapaneseEggAuthoredClassesPlan.Capture(jp,reservation,integration,a,b),"wrong destination region");
    Refuse(()=>NativeJapaneseEggAuthoredClassesPlan.Fixture(reservation,integration,a,b),"source authority cannot downgrade to fixture");
    jp.VerifyUnchanged();usa.VerifyUnchanged();
    Console.WriteLine($"JP_EGG_AUTHORED_CLASSES_SOURCE_OK checks={checks} exhaustiveDispatchClasses=65536 plan={p.PlanSha256} data={p.FinalDataSha256} overlay={p.FinalOverlaySha256} roots={string.Join(',',p.Bindings.Select(b=>b.ProjectedRoot.ToString("X8")))} publication=false");
}
else throw new ArgumentException("Usage: no arguments, or --source <exact-JP-BIN> <exact-USA-BIN>.");
