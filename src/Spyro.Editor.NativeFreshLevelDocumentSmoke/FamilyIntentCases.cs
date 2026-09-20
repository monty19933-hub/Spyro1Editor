using System.Reflection;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using static Cases;

// Neutral model only. Schema5 codec/storage tests have a separate owner; this
// file never serializes a schema5 value or resolves an original source asset.
internal static class FamilyIntentCases
{
    internal const string FamilyId="00000000000000000000000000000003";
    internal static NativeFreshLockedChestFamilyReference Source()=>NativeFreshLockedChestFamilyReference.Create(
        MaterialIntentCases.Variant,"peace-keepers-locked-chest-v1");
    internal static NativeFreshLockedChestFamilyPlacementIntent Family(string id=FamilyId,
        int keyX=8705,int keyY=8961,int keyZ=0,int chestX=9217,int chestY=9217,int chestZ=262128,
        NativeFreshChestRewardDestinationOverrides? overrides=null)=>NativeFreshLockedChestFamilyPlacementIntent.Create(
            id,Source(),keyX,keyY,keyZ,chestX,chestY,chestZ,overrides);

    internal static void Run()
    {
        foreach(Type type in new[]{typeof(NativeFreshLockedChestFamilyReference),typeof(NativeFreshLockedChestFamilyPlacementIntent),
            typeof(NativeFreshChestRewardDestinationOverrides)})
            Check(type.IsPublic&&type.IsSealed&&type.GetConstructors().Length==0&&type.GetProperties().All(p=>p.SetMethod==null)&&
                type.GetMethods(BindingFlags.Public|BindingFlags.Static|BindingFlags.DeclaredOnly).All(m=>m.Name=="Create"),
                "Public closed family/reference/override values expose authored intent only, not source capture or mutable native owners.");
        Check(typeof(NativeFreshChestRewardRole).IsPublic&&typeof(NativeFreshChestRewardDestinationOverride).IsPublic&&
            Enum.GetValues<NativeFreshChestRewardRole>().Select(v=>(int)v).SequenceEqual(new[]{0,1,2,3,4}),
            "Existing semantic reward types are shared publicly, not duplicated as another enum or native role map.");
        Check(typeof(NativeFreshLockedChestFamilyReference).GetProperties().Select(p=>p.Name).Order().SequenceEqual(
            new[]{"DefinitionId","SourceVariant"})&&NativeFreshLockedChestFamilyReference.SupportedDefinitionId=="peace-keepers-locked-chest-v1",
            "Family reference contains only the exact neutral versioned definition and source variant.");
        Check(typeof(NativeFreshLockedChestFamilyPlacementIntent).GetProperties().Select(p=>p.Name).Order().SequenceEqual(new[]{
            "ChestX","ChestY","ChestZ","EditorId","KeyX","KeyY","KeyZ","RewardDestinations","Source","Tag"}),
            "Family placement contains no source row/index, raw bytes, yaw, flags, hashes or approval claims.");
        var source=Source();var plain=NativeFreshLockedChestFamilyPlacementIntent.Create(FamilyId,source,8705,8961,0,9217,9217,262128);
        Check(plain.Tag=="locked-chest-family"&&ReferenceEquals(plain.Source,source)&&plain.RewardDestinations==null&&
            (plain.KeyX,plain.KeyY,plain.KeyZ,plain.ChestX,plain.ChestY,plain.ChestZ)==(8705,8961,0,9217,9217,262128),
            "Raw non-grid WORLD anchors and absent source-relative overrides remain explicit neutral intent.");
        foreach(string? variant in new string?[]{null,"",MaterialIntentCases.Variant+" ",MaterialIntentCases.Variant.ToUpperInvariant(),"unknown"})
            Reject(()=>NativeFreshLockedChestFamilyReference.Create(variant!,"peace-keepers-locked-chest-v1"),"exact family source variant");
        foreach(string? definition in new string?[]{null,"","peace-keepers-locked-chest-v2","peace-keepers-locked-chest-v1 ","PEACE-KEEPERS-LOCKED-CHEST-V1"})
            Reject(()=>NativeFreshLockedChestFamilyReference.Create(MaterialIntentCases.Variant,definition!),"exact closed family definition");
        Reject(()=>NativeFreshLockedChestFamilyPlacementIntent.Create(FamilyId,null!,0,0,0,0,0,0),"family reference required");
        foreach(string? id in new string?[]{null,"",new string('0',32),FamilyId[..31],Id.ToUpperInvariant()})
            Reject(()=>Family(id!),"shared stable editor ID grammar");
        for(int axis=0;axis<6;axis++)
        foreach(int bad in new[]{int.MinValue,-1,262129,int.MaxValue})
        {
            int[] xyz=[0,0,0,262128,262128,262128];xyz[axis]=bad;
            Reject(()=>NativeFreshLockedChestFamilyPlacementIntent.Create(FamilyId,source,xyz[0],xyz[1],xyz[2],xyz[3],xyz[4],xyz[5]),
                "each WORLD anchor component is bounded without clamp/wrap");
        }
        var endpoints=NativeFreshLockedChestFamilyPlacementIntent.Create(FamilyId,source,0,0,0,262128,262128,262128);
        Check(endpoints.KeyX==0&&endpoints.ChestZ==262128,"Neutral XYZ endpoints are inclusive, independently of aggregate XY containment.");
        Check(Family(chestX:8705,chestY:8961).ChestX==8705,"Coincident neutral anchors do not invent a source placement or clearance gate.");

        OverrideCases(plain);
        LegacyCases(plain);
        TiledCases();
    }

    private static void OverrideCases(NativeFreshLockedChestFamilyPlacementIntent plain)
    {
        var input=new[]{new NativeFreshChestRewardDestinationOverride(NativeFreshChestRewardRole.Reward5,262128,0,1),
            new NativeFreshChestRewardDestinationOverride(NativeFreshChestRewardRole.Reward2,10454,10503,9216)};
        var overrides=NativeFreshChestRewardDestinationOverrides.Create(input);
        var authored=plain.WithRewardDestinations(overrides);
        Check(ReferenceEquals(authored.RewardDestinations,overrides)&&ReferenceEquals(authored.Source,plain.Source)&&
            authored.EditorId==plain.EditorId&&AnchorTuple(authored)==AnchorTuple(plain)&&plain.RewardDestinations==null&&
            overrides.Values.Select(v=>v.Role).SequenceEqual(new[]{NativeFreshChestRewardRole.Reward2,NativeFreshChestRewardRole.Reward5}),
            "Optional sorted override owner is retained whole; adding it neither moves anchors nor mutates the default family.");
        input[0]=new(NativeFreshChestRewardRole.Reward1,1,2,3);
        Check(overrides.Values[1]==new NativeFreshChestRewardDestinationOverride(NativeFreshChestRewardRole.Reward5,262128,0,1),
            "Public override snapshot does not alias caller slots.");
        var list=(IList<NativeFreshChestRewardDestinationOverride>)overrides.Values;
        bool refused=false;try{list[0]=input[0];}catch(NotSupportedException){refused=true;}
        Check(refused,"Public Values cannot be mutated through its list cast.");
        var moved=authored.WithAnchors(8711,8973,17,9201,9199,261999);
        Check(moved.EditorId==authored.EditorId&&ReferenceEquals(moved.Source,authored.Source)&&
            ReferenceEquals(moved.RewardDestinations,overrides)&&overrides.Values[0].X==10454&&overrides.Values[0].Y==10503&&
            AnchorTuple(moved)==(8711,8973,17,9201,9199,261999),
            "Moving either anchor retains explicit WORLD destinations instead of translating, clearing or snapping them.");
        var cleared=moved.WithRewardDestinations(null);
        Check(cleared.RewardDestinations==null&&ReferenceEquals(cleared.Source,moved.Source)&&
            cleared.EditorId==moved.EditorId&&AnchorTuple(cleared)==AnchorTuple(moved)&&authored.RewardDestinations!=null,
            "Restore-default is null absence, not an empty override owner or mutation of prior authorship.");
        Reject(()=>plain.WithAnchors(-1,0,0,0,0,0),"WithAnchors repeats finite WORLD validation");
        Reject(()=>NativeFreshChestRewardDestinationOverrides.Create([]),"empty public override owner remains forbidden");
        Reject(()=>NativeFreshChestRewardDestinationOverrides.Create(null!),"null override collection is not default absence");
        Reject(()=>NativeFreshChestRewardDestinationOverrides.Create([new((NativeFreshChestRewardRole)5,0,0,0)]),"undefined public semantic role");
        Reject(()=>NativeFreshChestRewardDestinationOverrides.Create([new(NativeFreshChestRewardRole.Reward2,0,0,0),new(NativeFreshChestRewardRole.Reward2,1,2,3)]),"duplicate semantic role");
        for(int count=1;count<=5;count++)
            Check(NativeFreshChestRewardDestinationOverrides.Create(Enumerable.Range(0,count).Reverse().Select(i=>
                new NativeFreshChestRewardDestinationOverride((NativeFreshChestRewardRole)i,0,262128,0)).ToArray()).Values.Count==count,
                "All one through five typed override counts remain bounded neutral values.");
        Reject(()=>NativeFreshChestRewardDestinationOverrides.Create(Enumerable.Range(0,6).Select(i=>
            new NativeFreshChestRewardDestinationOverride((NativeFreshChestRewardRole)(i%5),0,0,0)).ToArray()),"six overrides exceed ingestion bound");
    }

    private static void LegacyCases(NativeFreshLockedChestFamilyPlacementIntent family)
    {
        foreach(var old in new[]{Document(Wizard()),MaterialCodecCases.Value(),MobyIntentCases.Value(true)})
        {
            string original=Serialize(old);
            Reject(()=>old.WithPlacements([family]),"schemas1..3 never infer family upgrade");
            var upgraded=old.UpgradeForFamilies();
            Check(upgraded.SchemaVersion==5&&ReferenceEquals(upgraded.Content,old.Content)&&
                upgraded.Placements.SequenceEqual(old.Placements)&&ReferenceEquals(upgraded.UpgradeForFamilies(),upgraded)&&Serialize(old)==original,
                "Explicit schema5 upgrade is idempotent and preserves every existing content/placement value and old canonical bytes.");
            var mixed=upgraded.WithPlacements([MobyIntentCases.Moby(),family,Wizard()]);
            Check(mixed.SchemaVersion==5&&mixed.Placements.Select(p=>p.EditorId).SequenceEqual(new[]{Id2,FamilyId,Id})&&
                ReferenceEquals(mixed.Placements[1],family),"Three-variant schema5 list retains authored order and complete family identity.");
            Check(mixed.WithPlacements([]).SchemaVersion==5&&mixed.WithContent(mixed.Base,[Wizard()]).SchemaVersion==5&&
                mixed.WithFloorMaterials(MaterialIntentCases.Pair()).SchemaVersion==5&&ReferenceEquals(mixed.UpgradeForNativeMobys(),mixed),
                "Removing families, legacy content/material edits and old native upgrade never downgrade schema5.");
            var tiled=mixed.UpgradeToTiled(TiledIntentCases.TileId);
            Check(tiled.SchemaVersion==5&&tiled.LegacyContent==null&&tiled.TiledContent!=null&&
                tiled.Placements.SequenceEqual(mixed.Placements),"Explicit legacy-to-tiled conversion retains schema5 and every family parameter.");
        }
        Reject(()=>NativeFreshLevelDocumentIntent.Create(Base(),[family]),"original Create stays schema1");
        var five=Document().UpgradeForFamilies();
        foreach(var (x,y) in new[]{(8448,8961),(9472,8961),(8705,8448),(8705,9472)})
        {
            Reject(()=>five.WithPlacements([Family(keyX:x,keyY:y)]),"key strictly inside active legacy floor");
            Reject(()=>five.WithPlacements([Family(chestX:x,chestY:y)]),"chest strictly inside active legacy floor");
        }
        var outsideOverrides=NativeFreshChestRewardDestinationOverrides.Create([new(NativeFreshChestRewardRole.Reward1,0,262128,0)]);
        Check(five.WithPlacements([family.WithRewardDestinations(outsideOverrides)]).Placements.Count==1,
            "Neutral aggregate checks anchors only; out-of-floor override destinations remain authored without footprint or trajectory approval.");
        Reject(()=>five.WithPlacements([Family(Id),Wizard()]),"stable IDs unique across family and Wizard variants");
        Reject(()=>five.WithPlacements([Family(Id2),MobyIntentCases.Moby()]),"stable IDs unique across family and native-row variants");
        Reject(()=>five.WithPlacements([null!]),"schema5 still rejects null placement");
        var mutable=new List<NativeFreshPlacementIntent>{family};var captured=five.WithPlacements(mutable);mutable.Clear();
        Check(captured.Placements.Count==1&&ReferenceEquals(captured.Placements[0],family)&&
            captured.Placements is IList<NativeFreshPlacementIntent>{IsReadOnly:true},"Family list snapshot retains immutable family, not caller collection.");
        Check(five.WithPlacements(Enumerable.Range(1,128).Select(i=>Family(i.ToString("x32")))).Placements.Count==128,
            "128 neutral families is only document ingestion capacity, not native singleton/resource admission.");
        int visited=0;bool disposed=false;
        IEnumerable<NativeFreshPlacementIntent> Overflow()
        {try{for(int i=1;i<=130;i++){visited++;yield return Family(i.ToString("x32"));}}finally{disposed=true;}}
        Reject(()=>five.WithPlacements(Overflow()),"family ingestion stops before unbounded enumeration");
        Check(visited==129&&disposed,"Family129 refusal disposes input without reading130 or altering prior document.");
    }

    private static void TiledCases()
    {
        var content=NativeFreshTiledLevelContent.Create([TiledIntentCases.Tile(0,8448),TiledIntentCases.Tile(1,10496),TiledIntentCases.Tile(2,12544)],
            8960,8960,9216,64,new(128,96,64),19);
        var family=Family(keyX:10496,chestX:13056);
        var old=NativeFreshLevelDocumentIntent.CreateTiled(content,[]);string original=Serialize(old);
        Reject(()=>old.WithPlacements([family]),"schema4 cannot silently acquire a family");
        Reject(()=>NativeFreshLevelDocumentIntent.CreateTiled(content,[family]),"CreateTiled remains explicit schema4 only");
        var five=old.UpgradeForFamilies().WithPlacements([family]);
        Check(five.SchemaVersion==5&&ReferenceEquals(five.TiledContent,content)&&five.LegacyContent==null&&Serialize(old)==original,
            "Schema5 retains actual ordered tile content; key on an interior seam and chest on the third tile are valid neutral anchors.");
        var changed=content.WithSettings(8960,8960,9216,128,new(1,2,3),20);
        var edited=five.WithTiledContent(changed);
        Check(edited.SchemaVersion==5&&ReferenceEquals(edited.Content,changed)&&ReferenceEquals(edited.Placements[0],family)&&
            edited.Heading==128&&edited.MusicTrackId==20&&edited.WithPlacements([]).SchemaVersion==5,
            "Tiled geometry/common-setting edits and family removal retain schema5 and the active whole content.");
        Reject(()=>edited.WithContent(Base(),[]),"legacy edit cannot downgrade schema5 tiled content");
        Reject(()=>five.WithTiledContent(content.WithTiles([TiledIntentCases.Tile(0,8448)])),"tile edits recheck both family anchors across the whole union");
        foreach(var (x,y) in new[]{(8448,8961),(14592,8961),(13056,8448),(13056,10496)})
        {
            Reject(()=>five.WithPlacements([Family(keyX:x,keyY:y,chestX:13056)]),"tiled key refuses outer union edge");
            Reject(()=>five.WithPlacements([Family(keyX:10496,chestX:x,chestY:y)]),"tiled chest refuses outer union edge");
        }
        var corner=content.WithTiles([TiledIntentCases.Tile(0,8448),TiledIntentCases.Tile(1,10496,10496)]);
        Reject(()=>NativeFreshLevelDocumentIntent.CreateTiled(corner,[]).UpgradeForFamilies().WithPlacements([
            Family(keyX:10496,keyY:10496,chestX:11008,chestY:11008)]),"corner-only contact is not a family anchor interior");
    }

    private static (int,int,int,int,int,int) AnchorTuple(NativeFreshLockedChestFamilyPlacementIntent family)=>
        (family.KeyX,family.KeyY,family.KeyZ,family.ChestX,family.ChestY,family.ChestZ);
}
