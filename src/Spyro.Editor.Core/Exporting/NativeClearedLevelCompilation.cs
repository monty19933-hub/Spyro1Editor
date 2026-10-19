using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.Core.Exporting;

public interface INativeAuthoredCompilation
{
    UniversalComposeResult Compose(UniversalSourceLibrary donors, IReadOnlyList<UniversalMobyPlacement> placements,
        Func<string, UniversalSpecialAsset?>? special = null);
}

/// <summary>A rebuilt normal retail slot. Original donors remain separate from the empty host,
/// including when a user adds an object from the level they just cleared.</summary>
public sealed class NativeClearedLevelCompilation : INativeAuthoredCompilation
{
    private readonly byte[] _body, _overlay, _exe;
    private readonly string _baseline;
    internal NativeFreshRewardFallbackResources RewardFallbacks { get; }
    private NativeClearedLevelCompilation(SceneDocument document, LevelDefinition target, string baseline,
        byte[] body, byte[] overlay, byte[] exe, NativeFreshRewardFallbackResources rewardFallbacks, NativeHubPortals? portals,
        NativeAuthoredMeshRenderer.DrawPlan? drawPlan, NativeSuperchargeAnimations? supercharge)
    { Document=document; Target=target; _baseline=baseline; _body=body; _overlay=overlay; _exe=exe; RewardFallbacks=rewardFallbacks; Portals=portals; DrawPlan=drawPlan; Supercharge=supercharge; }
    /// <summary>Spyro's supercharge animations, carried when the scene has supercharge ground (null otherwise): the
    /// rebuilt body is the kernel's, whose player stream is Town Square's whatever the slot had.</summary>
    internal NativeSuperchargeAnimations? Supercharge { get; }
    /// <summary>How the terrain loads the frame's draw buffer (<see cref="NativeFreshMainCompilation.DrawPlan"/>).</summary>
    internal NativeAuthoredMeshRenderer.DrawPlan? DrawPlan { get; }
    public int NearDistance => DrawPlan?.NearDistance ?? NativeDrawBufferGuard.MaxNearDistance;
    /// <summary>A rebuilt homeworld's doorways (their objects join the placements at composition).</summary>
    public NativeHubPortals? Portals { get; }
    public SceneDocument Document { get; }
    public LevelDefinition Target { get; }
    public byte[] CopyLevelData() => _body.ToArray();
    public byte[] CopyOverlay() => _overlay.ToArray();
    public byte[] CopyExecutable() => _exe.ToArray();

    public static async Task<NativeClearedLevelCompilation> CaptureAsync(string originalUSA, SceneDocument document,
        LevelCatalog catalog, string scratchBaselinePath, CancellationToken token=default, Func<int,byte[]?>? destinationSky=null)
    {
        ExportArtifactTransaction.RequireSourceSeparation(originalUSA,null,scratchBaselinePath);
        NativeAuthoredSpawn.RequirePortalArrivalSupported(document);
        var binding=document.TargetBinding ?? throw new InvalidDataException("The replacement scene has no retail target.");
        var target=document.ResolveTarget(catalog);
        var original=UniversalDiscImage.Read(originalUSA);
        int prefix=new UniversalLevelData(original.EntryBytes(target.SourceWadEntry)).Scene().RowCount;
        if(binding.NativePrefixRows!=prefix)
            throw new InvalidDataException("The replacement scene's saved object identities do not match its original level.");
        byte[] exe=NativeClearedRetailBinding.Prepare(original.Exe,original.Exe,target);
        NativeKillHeights.LowerFor(original.Exe,exe,target.LevelId,document);
        if(NativeSubdivisionQueueGuard.Needed(document))NativeSubdivisionQueueGuard.PatchExe(exe);
        string kernelPath=scratchBaselinePath+".kernel-"+Guid.NewGuid().ToString("N");
        string stage=scratchBaselinePath+".pending-"+Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(scratchBaselinePath))!);
        try
        {
            // Reuse only the authenticated neutral engine/resources and compiled authored body.
            // The ID65 bootstrap and its WAD slots are never installed into this retail image.
            // The kernel's music only reaches that ID65 bootstrap; a song from another game (played from an unused
            // XA slot, mapped by the project exporter) stands in as one of Spyro's own tracks here.
            int kernelMusic=Spyro.Editor.Core.Music.MusicTrackCatalog.IsCustom(document.MusicTrack)
                ?Spyro.Editor.Core.Music.MusicTrackCatalog.SelectableTracks[0].TrackId:document.MusicTrack;
            var portals=NativeHubPortals.Build(originalUSA,catalog,document,destinationSky);
            var kernel=await NativeFreshMainCompilation.CaptureAsync(originalUSA,
                document with{TargetBinding=null,Objects=[],Ledger=UniversalPlacementLedger.Empty,MusicTrack=kernelMusic,Portals=null},kernelPath,token,portals:portals);
            byte[] body=kernel.CopyLevelData(),overlay=kernel.CopyOverlay();
            // The level bounds its terrain's share of the frame's draw buffer at the near distance its terrain was planned for.
            NativeDrawBufferGuard.PatchExe(exe,new Dictionary<int,int>{[target.LevelId]=kernel.NearDistance});
            // The rebuilt level must fit the game's level memory on its own (composition re-checks it with objects).
            // (GameRam: the whole load below the draw buffers with 2 MB; with 8 MB RAM the scene has its own room.)
            var bodyData=new UniversalLevelData(body);uint bodyCopy=new UniversalOverlay(overlay,UniversalExeBindings.Read(exe,target.LevelId)).CopyBuffer;
            if(!GameRam.Fits(bodyData,bodyCopy))
                throw new InvalidDataException(kernel.Supercharge!=null&&GameRam.Shortfall(bodyData,bodyCopy)<=NativeSuperchargeAnimations.ByteLength
                    ?NativeSuperchargeAnimations.NoRoomMessage(document.Name,GameRam.Shortfall(bodyData,bodyCopy))
                    :$"{document.Name} needs {GameRam.Shortfall(bodyData,bodyCopy):N0} more bytes of level memory than the game has. Remove some geometry.");
            var replacements=new Dictionary<int,byte[]>{{target.SourceWadEntry,body},{target.SourceWadEntry-1,overlay}};
            original.WriteImage(stage,original.RebuildWad(replacements),exe);
            token.ThrowIfCancellationRequested();
            var read=UniversalDiscImage.Read(stage);
            if(!read.Exe.AsSpan().SequenceEqual(exe)||!read.EntryBytes(target.SourceWadEntry).AsSpan().SequenceEqual(body)||
                !read.EntryBytes(target.SourceWadEntry-1).AsSpan().SequenceEqual(overlay))
                throw new InvalidDataException("The rebuilt retail level failed independent disc readback.");
            ExportArtifactTransaction.RequireSourceSeparation(originalUSA,null,scratchBaselinePath);
            File.Move(stage,scratchBaselinePath,true);
            return new(document,target,scratchBaselinePath,body,overlay,exe,kernel.RewardFallbacks,portals,kernel.DrawPlan,kernel.Supercharge);
        }
        finally {if(File.Exists(stage))File.Delete(stage);if(File.Exists(kernelPath))File.Delete(kernelPath);}
    }

    public UniversalComposeResult Compose(UniversalSourceLibrary donors,IReadOnlyList<UniversalMobyPlacement> placements,
        Func<string,UniversalSpecialAsset?>? special=null) => ComposeWithExecutable(donors,placements,_exe,special);

    /// <summary>Water, lava or ooze in the level need their splash models (cleared levels drop the native ones).</summary>
    public bool NeedsComposition(IReadOnlyList<UniversalMobyPlacement> placements)=>
        placements.Count>0||Portals!=null||UniversalLiquidSplashes.Liquids(Document.GroundKinds()).Count>0;
    private IReadOnlyList<(int Class,string SourceKey)> LiquidSplashes(UniversalSourceLibrary donors)=>
        UniversalLiquidSplashes.Dependencies(UniversalLiquidSplashes.Liquids(Document.GroundKinds()),Target.Key,k=>donors.Get(k).Data);

    public UniversalComposeResult ComposeWithExecutable(UniversalSourceLibrary donors,IReadOnlyList<UniversalMobyPlacement> placements,
        byte[] executable,Func<string,UniversalSpecialAsset?>? special=null)
    {
        placements=KeepOwnDragonIdentities(donors,placements);
        // A homeworld's doorways: their objects come after the scene's own, then each record names its path object's row.
        if(Portals!=null)
        {
            var all=Portals.WithObjects(placements);
            var result=ComposeOwn(donors,all,executable,special);
            Portals.BindPathRows(result.LevelData,all,result.NewRowIndices);
            return result;
        }
        return ComposeOwn(donors,placements,executable,special);
    }

    private const int RescueActorClass=0xFB;
    /// <summary>Rows that keep their original save identity when put back in their rebuilt level: the dragon family
    /// (statue, dragon, pedestal) and the egg thief, whose bit is the one the game sets when its egg is collected
    /// (egg handler -> mark 0x8003B854 on the thief) and reads at load (0x800142C8: a set bit makes +0x53 0xFF, no egg).</summary>
    public static readonly IReadOnlySet<int> OwnIdentityClasses=new HashSet<int>{0xFA,0x6E,0x14B,0x14C,0x14D,LevelCompletionCheck.EggThiefClass};

    private IReadOnlyList<UniversalMobyPlacement> KeepOwnDragonIdentities(UniversalSourceLibrary donors,IReadOnlyList<UniversalMobyPlacement> placements)
    {
        if(Document.TargetBinding?.NativePrefixRows is not int prefix)return placements;
        var original=donors.Get(Target.Key).Data;
        return KeepOwnIdentities(Target.Key,prefix,placements,row=>System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(original.Row(row).AsSpan(0x36)));
    }

    /// <summary>The level's own dragons and egg thieves keep their original save identities: a card that rescued a
    /// dragon or got the egg in the original level has it here too (the thief then carries no egg), and they need no
    /// new identity. Only the first copy of a row keeps it; <paramref name="originalClass"/> gives an original row's class.</summary>
    public static IReadOnlyList<UniversalMobyPlacement> KeepOwnIdentities(string levelKey,int prefix,IReadOnlyList<UniversalMobyPlacement> placements,Func<int,int> originalClass)
    {
        HashSet<int> taken=[..placements.Where(p=>p.SaveIdentity!=null).Select(p=>p.SaveIdentity!.Value)];
        return placements.Select(p=>!p.Retired&&p.SaveIdentity==null&&p.SpecialId==null&&string.Equals(p.SourceLevelKey,levelKey,StringComparison.OrdinalIgnoreCase)&&
            p.SourceRowIndex>=0&&p.SourceRowIndex<prefix&&OwnIdentityClasses.Contains(originalClass(p.SourceRowIndex))&&
            taken.Add(p.SourceRowIndex)?p with{SaveIdentity=p.SourceRowIndex}:p).ToList();
    }

    /// <summary>The native classes the rebuilt level's own objects keep (UniversalNativeRelease): the kernel overlay runs
    /// another level's code for them, so they run their own level's imported code under their own numbers.
    /// Dragons: statue 0xFA, dragon 0x6E and the rescue actor 0xFB (the rescue loader finds the statue by number).
    /// Egg thief: 0x21 and its egg 0x22. The thief's update spawns the egg itself through the spawn pointer 0x800758CC
    /// (Town Square 0x8007F40C: while +0x53 != 0xFF and it holds none, spawn(0x22, thief), egg pointer at props+4); the
    /// overlay's particle code tells the egg apart by class 0x22 (Town Square 0x800892E0), so a renumbered egg would
    /// sparkle in the wrong place.</summary>
    public static IReadOnlySet<int>? ReleasedClasses(string levelKey,IEnumerable<UniversalMobyPlacement> placements,Func<int,int> originalClass)
    {
        var own=placements.Where(p=>!p.Retired&&p.SpecialId==null&&p.SourceRowIndex>=0&&string.Equals(p.SourceLevelKey,levelKey,StringComparison.OrdinalIgnoreCase))
            .Select(p=>originalClass(p.SourceRowIndex)).ToHashSet();
        HashSet<int> released=[];
        if(own.Contains(NativeDragonRescueCamera.StatueClass))
        {
            released.Add(NativeDragonRescueCamera.StatueClass);
            if(own.Contains(0x6E))released.Add(0x6E);
            released.Add(RescueActorClass);
        }
        if(own.Contains(LevelCompletionCheck.EggThiefClass)){released.Add(LevelCompletionCheck.EggThiefClass);released.Add(LevelCompletionCheck.DragonEggClass);}
        return released.Count>0?released:null;
    }

    private UniversalComposeResult ComposeOwn(UniversalSourceLibrary donors,IReadOnlyList<UniversalMobyPlacement> placements,
        byte[] executable,Func<string,UniversalSpecialAsset?>? special)
    {
        // Built levels reserve the retail spare-object arena: drops beyond it would overwrite level data.
        int active=placements.Count(p=>!p.Retired);
        // Doorway names spawn their letters into the spare slots; a dragon rescue needs about fifty more.
        int dynamicSlots=Math.Min(256-active,UniversalMobyComposer.BuiltLevelDynamicSlots(active)+(Portals?.SpawnedLetters??0));
        if(UniversalGreenDruidLinkedWorldInstallation.IsRequested(placements))
        {
            var source=donors.Get("alpineridge");
            // Preserve the original encounter's allocator reservation, including
            // its unchanged per-fragment reserve guard. Allocate real storage.
            dynamicSlots=UniversalGreenDruidGroup8Actors.CaptureSource(source.Data,source.Overlay,donors.Disc.Exe).OriginalDynamicCapacity;
        }
        byte[] prepared=NativeFreshPlacementArena.Prepare(_body,placements.Count(p=>!p.Retired),dynamicSlots);
        // The level's own dragons rescue through its original rescue streams (subfiles 4 onward), which the rebuilt
        // body lacks: they are appended unchanged, so each dragon's stream number still names its own scene.
        var original=donors.Get(Target.Key).Data;
        bool ownDragons=placements.Any(p=>!p.Retired&&string.Equals(p.SourceLevelKey,Target.Key,StringComparison.OrdinalIgnoreCase)&&
            System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(original.Row(p.SourceRowIndex).AsSpan(0x36))==250);
        // The kernel overlay carries another level's code for the dragons (statue 0xFA, dragon 0x6E), the rescue scene's
        // actor (0xFB, created and initialised by number during the rescue) and the egg thief and its egg (0x21, 0x22):
        // the level's own ones keep their numbers and run this level's code instead (ReleasedClasses).
        IReadOnlySet<int>? released=ReleasedClasses(Target.Key,placements,row=>System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(original.Row(row).AsSpan(0x36)));
        var dependencies=LiquidSplashes(donors).ToList();
        if(ownDragons)
        {
            dependencies.Add((RescueActorClass,Target.Key));
            var withStreams=new UniversalLevelData(prepared);
            // The kernel's own trailing subfiles belong to the donor slot it was issued from.
            // A dragon placed elsewhere takes its rescue scene's camera track along.
            var statues=placements.Where(p=>!p.Retired&&p.SpecialId==null&&string.Equals(p.SourceLevelKey,Target.Key,StringComparison.OrdinalIgnoreCase)&&
                System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(original.Row(p.SourceRowIndex).AsSpan(0x36))==NativeDragonRescueCamera.StatueClass).ToList();
            if(statues.GroupBy(p=>p.SourceRowIndex).Any(g=>g.Count()>1))
                throw new InvalidDataException($"Each of {Target.DisplayName}'s dragons can be placed once: its rescue scene is filmed where it stands.");
            withStreams.ReplaceSubfilesFrom(4,original.Subfiles().Skip(4).Select(f=>
            {
                byte[] stream=original.Bytes.AsSpan(f.Offset,f.Length).ToArray();
                foreach(var p in statues)
                    if(NativeDragonRescueCamera.Films(stream,original.CaptureRow(Target.Key,p.SourceRowIndex).Properties))
                    {
                        byte[] row=original.Row(p.SourceRowIndex);
                        stream=NativeDragonRescueCamera.MoveStream(stream,row,p.X,p.Y,p.Z,p.Yaw??row[0x46]);
                    }
                return stream;
            }).ToArray());
            prepared=withStreams.Bytes;
        }
        var nativeOwner=new UniversalOverlay(_overlay,UniversalExeBindings.Read(executable,Target.LevelId));
        // The level's own objects whose code reads other objects' class numbers (a Wizard Peak wizard's spell checks
        // what it hits) can't be renumbered: those the rebuilt level's code also handles keep their numbers and run this
        // level's code, like the dragons (the others are not renumbered for this level's code to begin with).
        HashSet<int> keep=[..released??new HashSet<int>()];
        var kernelSwitch=UniversalDispatchAnalysis.FindUpdateSwitch(nativeOwner);
        int[] Clashing(IEnumerable<int> classes)=>[..classes.Where(c=>UniversalDispatchAnalysis.Handles(nativeOwner,kernelSwitch,c))];
        UniversalComposeResult composed;
        for(int attempt=0;;attempt++)
        {
            try
            {
                composed=UniversalMobyComposer.Compose(Target,prepared,_overlay,executable,donors.Get,donors.SharedClasses,
                    UniversalMobyExport.DefaultProtectedVram(_baseline,Target).Concat(Supercharge?.ProtectedVram??[]),placements,special,
                    sharedModelPackage:donors.SharedModelPackage,playerAnimationSource:donors.PlayerAnimations,
                    persistentNativePrefixRows:Document.TargetBinding!.NativePrefixRows,nativeDestinationOwner:nativeOwner,nativeOriginalExecutable:donors.Disc.Exe,
                    dependencyClasses:dependencies,releasedNativeClasses:keep.Count>0?keep:null);
                break;
            }
            catch(InvalidDataException e)when(attempt<8&&UniversalClassFieldRefusal.TryRead(e,out string key,out var classes)&&
                string.Equals(key,Target.Key,StringComparison.OrdinalIgnoreCase)&&!keep.IsSupersetOf(Clashing(classes)))
            { keep.UnionWith(Clashing(classes)); }
        }
        RewardFallbacks.RequireInstalled(composed.LevelData);
        Supercharge?.RequireDestination(composed.LevelData);
        return composed;
    }
}
