using System.Text;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Persistence;

// App session mechanics over actual tiny Store fixtures. No Avalonia startup,
// source ROM, fake Snapshot, compilation, publication or runtime authority.
internal static class NativeFreshLevelDocumentSessionCases
{
    private const string A="11111111111111111111111111111111",B="22222222222222222222222222222222";
    private static int _checks;
    private static string _root="";
    internal static void Run()
    {
        _checks=0;_root=Directory.CreateTempSubdirectory("spyro-aggregate-session-").FullName;bool passed=false;
        try
        {
            AbsentAndWholeApply();
            RouteIdentityAndHistory();
            NativeMobys();
            TiledGeometry();
            MultipleAndMaximum();
            SaveAndReopen();
            FailedReadsAndRawCas();
            Cancellation();
            ActualFork();
            FailedForks();
            foreach(string sentinel in Directory.EnumerateFiles(_root,"retail-and-lab-sentinel.txt",SearchOption.AllDirectories))
                Check(File.ReadAllText(sentinel)=="not an aggregate owner","every unrelated tiny retail/Lab sentinel remains unchanged");
            passed=true;
        }
        finally
        {
            if(passed)Directory.Delete(_root,recursive:true); // Only this suite's fresh tiny fixtures.
            else Console.Error.WriteLine("Retained failed aggregate session fixtures: "+_root);
        }
        Console.WriteLine($"FRESH_LEVEL_DOCUMENT_SESSION_OK checks={_checks} sourceFree=true windowStarted=false exportInvoked=false");
    }
    private static void AbsentAndWholeApply()
    {
        var project=Project("absent");var absent=NativeFreshLevelDocumentStore.Capture(project);
        string[] names=Names(project);var session=new NativeFreshLevelDocumentSession(absent);
        Throws<ArgumentNullException>(()=>new NativeFreshLevelDocumentSession(null!));
        Check(ReferenceEquals(session.Snapshot,absent)&&session.CurrentIntent==null&&session.SelectedEditorId==null&&
            !session.HasUnsavedChanges&&!session.CanUndo&&!session.CanRedo,"actual absent snapshot, no implicit starter or history");
        session.Select(null);Check(!session.Undo()&&!session.Redo(),"empty history/select-null are no-ops");
        Throws<InvalidOperationException>(()=>session.AddWizardRoute(9984,9984,8909,9472,9984));
        Throws<InvalidOperationException>(()=>session.Save());
        Throws<ArgumentNullException>(()=>session.Apply(null!));
        Check(Names(project).SequenceEqual(names),"construction and failed neutral edits create no files");
        var document=Document();session.Apply(document);
        Check(ReferenceEquals(session.CurrentIntent,document)&&ReferenceEquals(session.Snapshot,absent)&&session.HasUnsavedChanges&&
            session.CanUndo&&!File.Exists(absent.Path),"whole actual aggregate Apply is in-memory only");
        Check(session.Undo()&&session.CurrentIntent==null&&!session.HasUnsavedChanges&&session.CanRedo,"explicit first Apply can undo to absence");
        Check(session.Redo()&&ReferenceEquals(session.CurrentIntent,document)&&session.HasUnsavedChanges,"redo restores exact aggregate object");
        var selected=session.SelectedEditorId;Throws<InvalidDataException>(()=>session.Select("missing"));
        Check(session.SelectedEditorId==selected&&ReferenceEquals(session.CurrentIntent,document),"unknown selection refuses without mutation");
    }
    private static void RouteIdentityAndHistory()
    {
        var (project,session)=SavedSession("history",Document(Wizard(A),Wizard(B,9472)));
        var original=session.CurrentIntent!;session.Select(A);
        session.ReplaceWizardRoute(A,9984,9984,8910,9472,9984);
        var changed=session.CurrentIntent!;
        Check(!ReferenceEquals(changed,original)&&ReferenceEquals(changed.Base,original.Base)&&ReferenceEquals(changed.Placements[1],original.Placements[1])&&
            changed.Placements[0].EditorId==A&&((NativeFreshWizardRouteIntent)changed.Placements[0]).OriginZ==8910&&session.SelectedEditorId==A,
            "route replacement keeps ID, Base and every unrelated actual placement");
        Check(session.Undo()&&ReferenceEquals(session.CurrentIntent,original)&&session.SelectedEditorId==A&&session.CanRedo,"undo restores exact prior document/selection");
        var equivalent=NativeFreshLevelDocumentCodec.Parse(Canonical(original));session.Apply(equivalent);
        Check(ReferenceEquals(session.CurrentIntent,original)&&session.CanRedo&&!session.HasUnsavedChanges,"canonical no-op preserves current identity and redo");
        session.ReplaceWizardRoute(A,9984,9984,8909,9472,9984);
        Check(ReferenceEquals(session.CurrentIntent,original)&&session.CanRedo,"same-route Apply is also a no-op, not an ID regeneration");
        Check(session.Redo()&&ReferenceEquals(session.CurrentIntent,changed)&&session.SelectedEditorId==A,"redo survives no-op Apply");
        foreach(Action invalid in new Action[]{
            ()=>session.ReplaceWizardRoute("missing",9984,9984,8909,9472,9984),
            ()=>session.ReplaceWizardRoute(A,9985,9984,8909,9472,9984),
            ()=>session.ReplaceWizardRoute(A,8448,9984,8909,9472,9984),
            ()=>session.ReplaceWizardRoute(A,9984,9984,8909,9984,9984),
            ()=>session.RemovePlacement("missing")})
        {Throws<InvalidDataException>(invalid);Check(ReferenceEquals(session.CurrentIntent,changed)&&session.SelectedEditorId==A&&session.CanUndo&&!session.CanRedo,"invalid route mutation leaves all current/history/selection state intact");}
        var added=session.AddWizardRoute(9728,9728,8909,9472,9728);var withAdded=session.CurrentIntent!;
        Check(added.EditorId.Length==32&&added.EditorId.Any(c=>c!='0')&&added.EditorId.All(c=>c is >= '0' and <= '9' or >= 'a' and <= 'f')&&
            added.EditorId!=A&&added.EditorId!=B&&ReferenceEquals(withAdded.Placements[2],added)&&session.SelectedEditorId==added.EditorId,
            "only explicit Add creates one new stable lowercase editor ID and selects it");
        Check(withAdded.Placements.Take(2).SequenceEqual(changed.Placements)&&ReferenceEquals(withAdded.Base,changed.Base),"Add never substitutes existing routes or floor");
        Check(session.Undo()&&ReferenceEquals(session.CurrentIntent,changed)&&session.SelectedEditorId==A,"undo Add restores old selection");
        Check(session.Redo()&&ReferenceEquals(session.CurrentIntent,withAdded)&&session.SelectedEditorId==added.EditorId,"redo Add restores SAME ID and aggregate, not a new GUID");
        session.RemovePlacement(added.EditorId);var removed=session.CurrentIntent!;
        Check(removed.Placements.Count==2&&session.SelectedEditorId==null,"removing selected row clears selection without selecting another implicitly");
        Check(session.Undo()&&ReferenceEquals(session.CurrentIntent,withAdded)&&session.SelectedEditorId==added.EditorId,"undo removal restores selected actual ID");
        Check(session.Redo()&&ReferenceEquals(session.CurrentIntent,removed)&&session.SelectedEditorId==null,"redo removal restores cleared selection");
        session.Select(B);session.RemovePlacement(A);Check(session.SelectedEditorId==B&&session.CurrentIntent!.Placements.Single().EditorId==B,"removing unselected row preserves selected ID");
        var reversed=NativeFreshLevelDocumentIntent.Create(withAdded.Base,withAdded.Placements.Reverse());session.Apply(reversed);
        Check(ReferenceEquals(session.CurrentIntent,reversed)&&session.SelectedEditorId==B,"whole ordered Apply retains selected ID across reorder");
        Check(File.ReadAllBytes(session.Snapshot.Path).SequenceEqual(Canonical(original)),"all route/history operations remain unsaved");
        var editedBase=Base(20);var allChanged=NativeFreshLevelDocumentIntent.Create(editedBase,[Wizard(A,z:0),Wizard(B,z:262128)]);
        session.Apply(allChanged);Check(ReferenceEquals(session.CurrentIntent,allChanged)&&ReferenceEquals(session.CurrentIntent.Base,editedBase)&&session.SelectedEditorId==B,
            "neutral session does not impose source floor-height, music or recipe gates or snap Z");
        for(int i=0;i<105;i++)session.ReplaceWizardRoute(A,9984,9984,i,9472,9984);
        int undos=0;while(session.Undo())undos++;
        Check(undos==100&&!session.CanUndo&&session.CanRedo,"history bounded at100 actual whole-document changes");
        Check(Names(project).All(n=>!Path.GetFileName(n).StartsWith('.')),"editing creates no leases/stages");
    }
    private static void NativeMobys()
    {
        const string variant="spyro1-usa-retail-mode2-2352-v1";
        var source=NativeFreshRetailMobyReference.Create(variant,10,121,501);
        var unresolved=NativeFreshRetailMobyReference.Create(variant,78,4095,65535);
        foreach(bool materials in new[]{false,true})
        {
            var old=Document(Wizard(A));
            if(materials)old=old.WithFloorMaterials(NativeFreshFloorMaterialsIntent.Create(
                NativeFreshFloorMaterialIntent.RetailTerrain(variant,14,1),NativeFreshFloorMaterialIntent.RetailTerrain(variant,70,1)));
            var (project,session)=SavedSession(materials?"native-moby-materials":"native-moby-color",old);session.Select(A);
            var original=session.CurrentIntent!;var originalSnapshot=session.Snapshot;byte[] disk=File.ReadAllBytes(originalSnapshot.Path);
            foreach(var point in new[]{(8448,9001,0,0),(10496,9001,0,0),(9001,8448,0,0),(9001,10496,0,0),
                (9001,9001,-1,0),(9001,9001,65536,0),(9001,9001,0,-1),(9001,9001,0,256)})
            {
                Throws<InvalidDataException>(()=>session.AddNativeMoby(source,point.Item1,point.Item2,point.Item3,point.Item4));
                Check(ReferenceEquals(session.CurrentIntent,original)&&ReferenceEquals(session.Snapshot,originalSnapshot)&&
                    session.SelectedEditorId==A&&!session.CanUndo&&!session.CanRedo&&!session.HasUnsavedChanges,
                    "invalid native Add cannot upgrade old version or alter selection/history");
            }
            Throws<ArgumentNullException>(()=>session.AddNativeMoby(null!,9001,9001,0,0));
            var added=session.AddNativeMoby(source,9001,9001,65535,64);var upgraded=session.CurrentIntent!;
            Check(upgraded.SchemaVersion==3&&ReferenceEquals(upgraded.FloorMaterials,original.FloorMaterials)&&
                ReferenceEquals(upgraded.Base,original.Base)&&ReferenceEquals(upgraded.Placements[0],original.Placements[0])&&
                ReferenceEquals(upgraded.Placements[1],added)&&ReferenceEquals(added.Source,source)&&
                added.X==9001&&added.Y==9001&&added.Z==65535&&added.NativeYawByte==64&&session.SelectedEditorId==added.EditorId&&
                added.EditorId.Length==32&&added.EditorId.Any(c=>c!='0')&&added.EditorId!=A,
                "explicit valid Add atomically upgrades once, retains unrelated owners and selects stable raw-transform ID");
            Check(session.Undo()&&ReferenceEquals(session.CurrentIntent,original)&&session.SelectedEditorId==A&&session.CanRedo,
                "undo native Add restores exact old schema and prior selection");
            Check(session.Redo()&&ReferenceEquals(session.CurrentIntent,upgraded)&&session.SelectedEditorId==added.EditorId,
                "redo native Add restores the same schema3 object and generated ID");
            session.ReplaceNativeMoby(added.EditorId,unresolved,9002,9003,1,255);var edited=session.CurrentIntent!;
            var replacement=(NativeFreshMobyPlacementIntent)edited.Placements[1];
            Check(replacement.EditorId==added.EditorId&&ReferenceEquals(replacement.Source,unresolved)&&replacement.X==9002&&replacement.Y==9003&&
                replacement.Z==1&&replacement.NativeYawByte==255&&ReferenceEquals(edited.Placements[0],upgraded.Placements[0]),
                "neutral replacement permits unresolved generic reference while preserving ID and unrelated Wizard");
            foreach(Action wrong in new Action[]{()=>session.ReplaceWizardRoute(added.EditorId,9984,9984,8909,9472,9984),
                ()=>session.ReplaceNativeMoby(A,source,9001,9001,0,0),()=>session.ReplaceNativeMoby("missing",source,9001,9001,0,0),
                ()=>session.ReplaceNativeMoby(added.EditorId,source,8448,9001,0,0)})
            {
                Throws<InvalidDataException>(wrong);
                Check(ReferenceEquals(session.CurrentIntent,edited)&&session.SelectedEditorId==added.EditorId&&session.CanUndo&&!session.CanRedo,
                    "wrong-type/missing/outside replacement does not convert variant or partially mutate session");
            }
            Check(session.Undo()&&ReferenceEquals(session.CurrentIntent,upgraded)&&session.CanRedo,"native edit undo restores original reference/transform");
            session.ReplaceNativeMoby(added.EditorId,source,9001,9001,65535,64);
            Check(ReferenceEquals(session.CurrentIntent,upgraded)&&session.CanRedo,"semantic no-op native replacement preserves actual object and redo");
            session.Redo();session.Save();session.Reopen();
            Check(session.CurrentIntent!.SchemaVersion==3&&session.SelectedEditorId==added.EditorId&&!session.HasUnsavedChanges&&!session.CanUndo&&
                ((NativeFreshMobyPlacementIntent)session.CurrentIntent.Placements[1]).Source.SourceTrueIndex==4095,
                "generic source/transform persist through actual Save/Reopen without family compilation");
            var snapshot=session.Snapshot;session.ReplaceNativeMoby(added.EditorId,source,9004,9005,2,0);var draft=session.CurrentIntent;
            File.AppendAllText(snapshot.Path," \n");byte[] external=File.ReadAllBytes(snapshot.Path);
            Throws<InvalidDataException>(()=>session.Save());
            Check(ReferenceEquals(session.Snapshot,snapshot)&&ReferenceEquals(session.CurrentIntent,draft)&&session.SelectedEditorId==added.EditorId&&
                session.CanUndo&&File.ReadAllBytes(snapshot.Path).SequenceEqual(external),"stale schema3 Save retains draft/history/selection and external raw bytes");
            File.WriteAllBytes(snapshot.Path,Canonical(snapshot.Intent!));
            session.RemovePlacement(added.EditorId);var removed=session.CurrentIntent!;
            Check(removed.SchemaVersion==3&&removed.Placements.Single() is NativeFreshWizardRouteIntent&&session.SelectedEditorId==null&&
                (removed.FloorMaterials!=null)==materials,"removing last native placement retains schema3/material intent and clears selection");
            Check(session.Undo()&&ReferenceEquals(session.CurrentIntent,draft)&&session.SelectedEditorId==added.EditorId,"undo removal restores the exact selected native row");
            session.Redo();session.Save();session.Reopen();
            Check(session.CurrentIntent!.SchemaVersion==3&&session.CurrentIntent.Placements.Count==1&&
                disk.SequenceEqual(Canonical(original)),"retained schema3 persists after final-native removal; original schema fixture bytes unchanged");
        }
        var maximum=Document(Enumerable.Range(1,128).Select(i=>Wizard(i.ToString("x32"))).ToArray());
        var (_,full)=SavedSession("native-moby-maximum",maximum);var before=full.CurrentIntent;
        Throws<InvalidDataException>(()=>full.AddNativeMoby(source,9001,9001,0,0));
        Check(ReferenceEquals(full.CurrentIntent,before)&&full.CurrentIntent!.SchemaVersion==1&&!full.CanUndo&&!full.HasUnsavedChanges,
            "129th native Add refuses without even upgrading the valid old128-row document");
        var absentProject=Project("native-moby-absent");var absent=new NativeFreshLevelDocumentSession(NativeFreshLevelDocumentStore.Capture(absentProject));
        Throws<InvalidOperationException>(()=>absent.AddNativeMoby(source,9001,9001,0,0));
        Check(absent.CurrentIntent==null&&!absent.CanUndo&&!absent.HasUnsavedChanges,"native Add does not infer a missing floor/aggregate");
    }
    private static void TiledGeometry()
    {
        var (_, session) = SavedSession("tiled-history", Document());
        var legacy = session.CurrentIntent!;
        session.UpgradeToTiles(); var first = session.CurrentIntent!;
        string firstId = session.SelectedTileId!;
        Check(first.SchemaVersion == 4 && first.TiledContent!.Tiles[0].EditorId == firstId && session.CanUndo,
            "explicit tiled upgrade selects the real new tile within existing history");
        Check(session.Undo() && ReferenceEquals(session.CurrentIntent, legacy) && session.SelectedTileId == null,
            "undo upgrade restores actual old content with no tile selection");
        Check(session.Redo() && ReferenceEquals(session.CurrentIntent, first) && session.SelectedTileId == firstId,
            "redo upgrade retains tile identity and exact document");
        session.AddAdjacentTile(); session.AddAdjacentTile();
        string thirdId = session.SelectedTileId!; var three = session.CurrentIntent!;
        var source = NativeFreshRetailMobyReference.Create("spyro1-usa-retail-mode2-2352-v1",14,1,1);
        var moby = session.AddNativeMoby(source,13001,9001,1,0);
        Check(session.CurrentIntent!.SchemaVersion == 4 && ReferenceEquals(session.CurrentIntent.TiledContent, three.TiledContent) &&
            session.SelectedTileId == thirdId && session.SelectedEditorId == moby.EditorId,
            "native Add beyond old Base preserves schema4 content and independent tile/placement selections");
        var withMoby = session.CurrentIntent;
        Throws<InvalidDataException>(() => session.RemoveSelectedTile());
        Check(ReferenceEquals(session.CurrentIntent, withMoby) && session.SelectedTileId == thirdId,
            "failed tile removal preserves complete history and selection when a placement needs it");
        session.Save(); var snapshot = session.Snapshot;
        session.ReplaceNativeMoby(moby.EditorId,source,13002,9003,2,1); var draft = session.CurrentIntent;
        File.AppendAllText(snapshot.Path," \n"); byte[] external = File.ReadAllBytes(snapshot.Path);
        Throws<InvalidDataException>(() => session.Save());
        Check(ReferenceEquals(session.Snapshot,snapshot) && ReferenceEquals(session.CurrentIntent,draft) &&
            session.SelectedTileId == thirdId && session.SelectedEditorId == moby.EditorId &&
            File.ReadAllBytes(snapshot.Path).SequenceEqual(external),
            "schema4 stale raw save retains draft, both selections and external bytes");
        File.WriteAllBytes(snapshot.Path,Canonical(snapshot.Intent!));
        session.Reopen();
        Check(session.CurrentIntent!.SchemaVersion == 4 && session.SelectedTileId == thirdId &&
            session.SelectedEditorId == moby.EditorId && !session.CanUndo && !session.HasUnsavedChanges,
            "schema4 actual reopen retains valid IDs and resets history without reissuing source authority");
    }

    private static void MultipleAndMaximum()
    {
        var routes=Enumerable.Range(1,128).Select(i=>Wizard(i.ToString("x32"))).ToArray();
        var document=Document(routes);var (_,session)=SavedSession("maximum",document);
        session.Select(routes[64].EditorId);var previous=session.CurrentIntent!;
        Throws<InvalidDataException>(()=>session.AddWizardRoute(9728,9728,8909,9472,9728));
        Check(ReferenceEquals(session.CurrentIntent,previous)&&!session.HasUnsavedChanges&&!session.CanUndo&&session.SelectedEditorId==routes[64].EditorId,
            "129th Add refuses without altering valid128-placement draft");
        session.ReplaceWizardRoute(routes[64].EditorId,9984,9984,8909,9728,9984);session.Save();session.Reopen();
        Check(session.CurrentIntent!.Placements.Count==128&&session.CurrentIntent.Placements.Select(p=>p.EditorId).SequenceEqual(routes.Select(p=>p.EditorId))&&
            session.CurrentIntent.Placements.Cast<NativeFreshWizardRouteIntent>().All(p=>p.OriginZ==8909),"all128 IDs/order/non-grid heights survive actual Save/Reopen, no first-row export fallback");
        Check(session.SelectedEditorId==routes[64].EditorId&&!session.CanUndo&&!session.CanRedo,"Reopen keeps still-existing selection and clears history");
    }
    private static void SaveAndReopen()
    {
        var project=Project("save");var legacy=NativeId65FreshDocumentStore.Save(NativeId65FreshDocumentStore.Capture(project),Base(21));
        byte[] legacyBytes=File.ReadAllBytes(legacy.Path),manifest=File.ReadAllBytes(project.ManifestPath);
        var session=new NativeFreshLevelDocumentSession(NativeFreshLevelDocumentStore.Capture(project));var current=Document(Wizard(A));
        session.Apply(current);session.Select(A);var saved=session.Save();
        Check(ReferenceEquals(session.Snapshot,saved)&&ReferenceEquals(session.CurrentIntent,current)&&!ReferenceEquals(saved.Intent,current)&&
            Canonical(saved.Intent!).SequenceEqual(Canonical(current)),"Save retains applied reference but installs actual independently parsed returned Snapshot");
        Check(ReferenceEquals(session.Snapshot.Intent,saved.Intent)&&!session.HasUnsavedChanges&&!session.CanUndo&&!session.CanRedo,
            "future export must take Snapshot.Intent actual reference, not assume CurrentIntent identity after Save; absence history pruned");
        session.ReplaceWizardRoute(A,9984,9984,8910,9472,9984);session.Undo();Check(session.CanRedo,"redo meaningfully armed");
        session.Save();Check(session.CanRedo&&ReferenceEquals(session.CurrentIntent,current),"canonical no-op Save preserves applied identity and real redo");
        session.Redo();var edited=session.CurrentIntent!;session.Save();Check(ReferenceEquals(session.CurrentIntent,edited)&&session.CanUndo,"ordinary Save retains real edit history");
        string replacement=Path.Combine(project.RootPath,"same-byte-replacement.tmp");File.WriteAllBytes(replacement,File.ReadAllBytes(saved.Path));File.Move(replacement,saved.Path,overwrite:true);
        session.Save();Check(!session.HasUnsavedChanges,"actual same-byte replacement follows Store raw-SHA semantics, not inode guesses");
        var reopened=session.Reopen();Check(ReferenceEquals(session.Snapshot,reopened)&&ReferenceEquals(session.CurrentIntent,reopened.Intent)&&
            session.SelectedEditorId==A&&!session.CanUndo&&!session.CanRedo&&!session.HasUnsavedChanges,"Reopen installs only actual successful capture and clears history");
        File.Delete(reopened.Path);var absent=session.Reopen();
        Check(!absent.Exists&&session.CurrentIntent==null&&session.SelectedEditorId==null&&!session.HasUnsavedChanges,"external aggregate absence is explicit, no legacy/default substitution");
        Check(File.ReadAllBytes(legacy.Path).SequenceEqual(legacyBytes)&&File.ReadAllBytes(project.ManifestPath).SequenceEqual(manifest),"legacy and complete manifest preserved by all modern operations");
    }
    private static void FailedReadsAndRawCas()
    {
        foreach(string change in new[]{"whitespace","id","order","route","manifest","bad-manifest","malformed","oversize"})
        {
            var (project,session)=SavedSession("cas-"+change,Document(Wizard(A),Wizard(B)));
            session.Select(B);session.ReplaceWizardRoute(A,9984,9984,8910,9472,9984);
            var snapshot=session.Snapshot;var draft=session.CurrentIntent!;byte[] external;
            switch(change)
            {
                case "manifest": File.AppendAllText(project.ManifestPath," ");break;
                case "bad-manifest": File.WriteAllText(project.ManifestPath,"{not-json}");break;
                case "whitespace": File.AppendAllText(snapshot.Path," \n");break;
                case "malformed": File.WriteAllText(snapshot.Path,"{not-json}");break;
                case "oversize": File.WriteAllBytes(snapshot.Path,new byte[65537]);break;
                default:
                    var original=snapshot.Intent!;
                    var changed=change=="order"?NativeFreshLevelDocumentIntent.Create(original.Base,original.Placements.Reverse()):
                        Document(Wizard(change=="id"?"33333333333333333333333333333333":A,z:change=="route"?8908:8909),Wizard(B));
                    File.WriteAllBytes(snapshot.Path,Canonical(changed));break;
            }
            external=File.ReadAllBytes(snapshot.Path);Throws<InvalidDataException>(()=>session.Save());
            Check(ReferenceEquals(session.Snapshot,snapshot)&&ReferenceEquals(session.CurrentIntent,draft)&&session.SelectedEditorId==B&&
                session.CanUndo&&!session.CanRedo&&session.HasUnsavedChanges&&File.ReadAllBytes(snapshot.Path).SequenceEqual(external),"failed raw CAS/parse leaves snapshot/draft/history/selection intact: "+change);
            if(change is "malformed" or "oversize" or "bad-manifest")
            {Throws<InvalidDataException>(()=>session.Reopen());Check(ReferenceEquals(session.Snapshot,snapshot)&&ReferenceEquals(session.CurrentIntent,draft)&&session.SelectedEditorId==B&&session.CanUndo,"failed Reopen never partially installs state");}
            Check(Names(project).All(p=>!Path.GetFileName(p).StartsWith('.')),"failed ordinary CAS leaves no stage/lease residue");
        }
    }
    private static void Cancellation()
    {
        var (project,session)=SavedSession("cancel",Document(Wizard(A)));session.Select(A);session.ReplaceWizardRoute(A,9984,9984,8910,9472,9984);
        var snapshot=session.Snapshot;var draft=session.CurrentIntent;var names=Names(project);byte[] bytes=File.ReadAllBytes(snapshot.Path);
        using var canceled=new CancellationTokenSource();canceled.Cancel();
        Throws<OperationCanceledException>(()=>session.Save(canceled.Token));Throws<OperationCanceledException>(()=>session.Reopen(canceled.Token));
        Check(ReferenceEquals(session.Snapshot,snapshot)&&ReferenceEquals(session.CurrentIntent,draft)&&session.SelectedEditorId==A&&session.CanUndo&&
            Names(project).SequenceEqual(names)&&File.ReadAllBytes(snapshot.Path).SequenceEqual(bytes),"pre-cancel Save/Reopen preserve exact state and disk");
    }
    private static void ActualFork()
    {
        var project=Project("fork");var source=NativeId65FreshDocumentStore.Save(NativeId65FreshDocumentStore.Capture(project),Base(20));
        byte[] sourceBytes=File.ReadAllBytes(source.Path),manifest=File.ReadAllBytes(project.ManifestPath);
        var absent=NativeFreshLevelDocumentStore.Capture(project);var session=new NativeFreshLevelDocumentSession(absent);
        var forked=session.ForkLegacyFloor(source);
        Check(ReferenceEquals(session.Snapshot,forked)&&ReferenceEquals(session.CurrentIntent,forked.Intent)&&forked.Exists&&
            session.CurrentIntent!.Placements.Count==0&&session.CurrentIntent.Base.MusicTrackId==20&&session.SelectedEditorId==null&&
            !session.HasUnsavedChanges&&!session.CanUndo&&!session.CanRedo,"actual explicit Fork return owns empty aggregate, no recipe/default/placement substitution");
        Check(File.ReadAllBytes(forked.Path).SequenceEqual(Canonical(NativeFreshLevelDocumentIntent.Create(source.Intent!,[]))),"actual fork canonical output follows exact saved legacy Base");
        session.AddWizardRoute(9984,9984,8909,9472,9984);session.Save();session.Reopen();
        Check(session.CurrentIntent!.Placements.Count==1&&((NativeFreshWizardRouteIntent)session.CurrentIntent.Placements[0]).OriginZ==8909&&
            File.ReadAllBytes(source.Path).SequenceEqual(sourceBytes)&&File.ReadAllBytes(project.ManifestPath).SequenceEqual(manifest),"modern edit/save after Fork never synchronizes back into legacy");
    }
    private static void FailedForks()
    {
        foreach(string failure in new[]{"dirty","undone-draft","occupied","stale-source","stale-destination","wrong-project","cancel","transaction-lock"})
        {
            var project=Project("fork-"+failure);var source=NativeId65FreshDocumentStore.Save(NativeId65FreshDocumentStore.Capture(project),Base());
            var absent=NativeFreshLevelDocumentStore.Capture(project);var session=new NativeFreshLevelDocumentSession(absent);
            if(failure=="dirty")session.Apply(Document(Wizard(A)));
            if(failure=="undone-draft"){session.Apply(Document(Wizard(A)));session.Undo();Check(session.CurrentIntent==null&&session.CanRedo,"an undone unsaved draft still owns meaningful redo");}
            if(failure=="occupied"){session.Apply(Document());session.Save();}
            if(failure=="stale-source")File.AppendAllText(source.Path," ");
            if(failure=="stale-destination")NativeFreshLevelDocumentStore.Save(absent,Document(Wizard(B)));
            if(failure=="wrong-project")
            {var other=Project("other-source");source=NativeId65FreshDocumentStore.Save(NativeId65FreshDocumentStore.Capture(other),Base());}
            string lockPath=Path.Combine(project.RootPath,".export-publication.lock");
            if(failure=="transaction-lock")File.WriteAllText(lockPath,"real foreign transaction lock");
            var snapshot=session.Snapshot;var current=session.CurrentIntent;var selected=session.SelectedEditorId;bool undo=session.CanUndo,redo=session.CanRedo;
            using var cancellation=new CancellationTokenSource();if(failure=="cancel")cancellation.Cancel();
            Exception error=Throws<Exception>(()=>session.ForkLegacyFloor(source,cancellation.Token));
            Check(ReferenceEquals(session.Snapshot,snapshot)&&ReferenceEquals(session.CurrentIntent,current)&&session.SelectedEditorId==selected&&session.CanUndo==undo&&session.CanRedo==redo,
                "any failed actual Fork leaves all session state untouched: "+failure);
            if(failure=="transaction-lock")
            {
                Check(error is NativeFreshLevelDocumentForkPublicationUnconfirmedException&&File.ReadAllText(lockPath)=="real foreign transaction lock"&&
                    !session.Snapshot.Exists&&session.CurrentIntent==null,"actual invoked/unreturned Store uncertainty is propagated, not converted to adopted bytes/success");
                Check(File.ReadAllBytes(Path.Combine(project.RootPath,".native-id65-fresh-document.writer.lock")).Length==32&&
                    File.ReadAllBytes(Path.Combine(project.RootPath,".native-fresh-level.writer.lock")).Length==32,"actual Store's retained owner evidence is not cleaned by session");
            }
            else if(failure=="cancel")Check(error is OperationCanceledException,"pre-cancel Fork preserves cancellation type");
            else Check(error is InvalidDataException or InvalidOperationException,"pristine/actual Store contract refuses invalid fork intent");
        }
    }
    private static (EditorProjectLayout Project,NativeFreshLevelDocumentSession Session) SavedSession(string name,NativeFreshLevelDocumentIntent document)
    {var project=Project(name);var saved=NativeFreshLevelDocumentStore.Save(NativeFreshLevelDocumentStore.Capture(project),document);return(project,new(saved));}
    private static EditorProjectLayout Project(string name)
    {
        string path=Path.Combine(_root,name);Directory.CreateDirectory(path);var project=new EditorProjectLayout(path,name);
        File.WriteAllText(project.ManifestPath,"{\"schemaVersion\":1,\"projectId\":\""+name+"\",\"unrelated\":{\"preserve\":true}}\n");
        File.WriteAllText(Path.Combine(path,"retail-and-lab-sentinel.txt"),"not an aggregate owner");return project;
    }
    private static NativeId65FreshDocumentIntent Base(int music=19)=>NativeId65FreshDocumentIntent.Create(8448,8448,8704,2048,2048,8960,8960,64,new(80,128,112),new(128,96,64),music);
    private static NativeFreshWizardRouteIntent Wizard(string id,int x=9984,int z=8909)=>NativeFreshWizardRouteIntent.Create(id,x,9984,z,x==9472?9984:9472,9984);
    private static NativeFreshLevelDocumentIntent Document(params NativeFreshPlacementIntent[] placements)=>NativeFreshLevelDocumentIntent.Create(Base(),placements);
    private static byte[] Canonical(NativeFreshLevelDocumentIntent document)=>NativeFreshLevelDocumentCodec.SerializeCanonical(document);
    private static string[] Names(EditorProjectLayout project)=>Directory.EnumerateFileSystemEntries(project.RootPath).Order(StringComparer.Ordinal).ToArray();
    private static T Throws<T>(Action action)where T:Exception
    {try{action();}catch(T error){_checks++;return error;}throw new InvalidDataException("Expected "+typeof(T).Name);}
    private static void Check(bool ok,string message){if(!ok)throw new InvalidDataException(message);_checks++;}
}
