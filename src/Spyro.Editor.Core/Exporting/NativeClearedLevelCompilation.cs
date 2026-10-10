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
        byte[] body, byte[] overlay, byte[] exe, NativeFreshRewardFallbackResources rewardFallbacks, NativeHubPortals? portals)
    { Document=document; Target=target; _baseline=baseline; _body=body; _overlay=overlay; _exe=exe; RewardFallbacks=rewardFallbacks; Portals=portals; }
    /// <summary>A rebuilt homeworld's doorways (their objects join the placements at composition).</summary>
    public NativeHubPortals? Portals { get; }
    public SceneDocument Document { get; }
    public LevelDefinition Target { get; }
    public byte[] CopyLevelData() => _body.ToArray();
    public byte[] CopyOverlay() => _overlay.ToArray();
    public byte[] CopyExecutable() => _exe.ToArray();

    public static async Task<NativeClearedLevelCompilation> CaptureAsync(string originalUSA, SceneDocument document,
        LevelCatalog catalog, string scratchBaselinePath, CancellationToken token=default)
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
            var portals=NativeHubPortals.Build(originalUSA,catalog,document);
            var kernel=await NativeFreshMainCompilation.CaptureAsync(originalUSA,
                document with{TargetBinding=null,Objects=[],Ledger=UniversalPlacementLedger.Empty,MusicTrack=kernelMusic,Portals=null},kernelPath,token,portals:portals);
            byte[] body=kernel.CopyLevelData(),overlay=kernel.CopyOverlay();
            // The rebuilt level must fit the game's level memory on its own (composition re-checks it with objects).
            uint end=UniversalMobyComposer.RuntimeEnd(new UniversalLevelData(body),new UniversalOverlay(overlay,UniversalExeBindings.Read(exe,target.LevelId)).CopyBuffer);
            if(end>UniversalMobyComposer.NormalBufferFloor-UniversalMobyComposer.RamSafetyMargin)
                throw new InvalidDataException($"{document.Name} needs {end-(UniversalMobyComposer.NormalBufferFloor-UniversalMobyComposer.RamSafetyMargin):N0} more bytes of level memory than the game has. Remove some geometry.");
            var replacements=new Dictionary<int,byte[]>{{target.SourceWadEntry,body},{target.SourceWadEntry-1,overlay}};
            original.WriteImage(stage,original.RebuildWad(replacements),exe);
            token.ThrowIfCancellationRequested();
            var read=UniversalDiscImage.Read(stage);
            if(!read.Exe.AsSpan().SequenceEqual(exe)||!read.EntryBytes(target.SourceWadEntry).AsSpan().SequenceEqual(body)||
                !read.EntryBytes(target.SourceWadEntry-1).AsSpan().SequenceEqual(overlay))
                throw new InvalidDataException("The rebuilt retail level failed independent disc readback.");
            ExportArtifactTransaction.RequireSourceSeparation(originalUSA,null,scratchBaselinePath);
            File.Move(stage,scratchBaselinePath,true);
            return new(document,target,scratchBaselinePath,body,overlay,exe,kernel.RewardFallbacks,portals);
        }
        finally {if(File.Exists(stage))File.Delete(stage);if(File.Exists(kernelPath))File.Delete(kernelPath);}
    }

    public UniversalComposeResult Compose(UniversalSourceLibrary donors,IReadOnlyList<UniversalMobyPlacement> placements,
        Func<string,UniversalSpecialAsset?>? special=null) => ComposeWithExecutable(donors,placements,_exe,special);

    /// <summary>Water, lava or ooze in the level need their splash models (cleared levels drop the native ones).</summary>
    public bool NeedsComposition(IReadOnlyList<UniversalMobyPlacement> placements)=>
        placements.Count>0||Portals!=null||UniversalLiquidSplashes.Liquids(Document.Surfaces.Select(s=>s.Kind)).Count>0;
    private IReadOnlyList<(int Class,string SourceKey)> LiquidSplashes(UniversalSourceLibrary donors)=>
        UniversalLiquidSplashes.Dependencies(UniversalLiquidSplashes.Liquids(Document.Surfaces.Select(s=>s.Kind)),Target.Key,k=>donors.Get(k).Data);

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
    private static readonly HashSet<int> DragonFamily=[0xFA,0x6E,0x14B];   // statue, dragon, pedestal

    /// <summary>The level's own dragons (statue, dragon and pedestal rows) keep their original save identities: a card
    /// that rescued one in the original level has it rescued here too, and they need no new identity.</summary>
    private IReadOnlyList<UniversalMobyPlacement> KeepOwnDragonIdentities(UniversalSourceLibrary donors,IReadOnlyList<UniversalMobyPlacement> placements)
    {
        if(Document.TargetBinding?.NativePrefixRows is not int prefix)return placements;
        var original=donors.Get(Target.Key).Data;
        HashSet<int> taken=[..placements.Where(p=>p.SaveIdentity!=null).Select(p=>p.SaveIdentity!.Value)];
        return placements.Select(p=>!p.Retired&&p.SaveIdentity==null&&p.SpecialId==null&&string.Equals(p.SourceLevelKey,Target.Key,StringComparison.OrdinalIgnoreCase)&&
            p.SourceRowIndex>=0&&p.SourceRowIndex<prefix&&DragonFamily.Contains(System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(original.Row(p.SourceRowIndex).AsSpan(0x36)))&&
            taken.Add(p.SourceRowIndex)?p with{SaveIdentity=p.SourceRowIndex}:p).ToList();
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
        // The kernel overlay carries its donor homeworld's dragon code; the level's own dragons (statue 0xFA, dragon 0x6E)
        // keep their numbers, which the rescue loader looks for, and run this level's code instead. So does the rescue
        // scene's actor (0xFB), which the game creates and initialises by number during the rescue.
        IReadOnlySet<int>? released=null;
        var dependencies=LiquidSplashes(donors).ToList();
        if(ownDragons)
        {
            released=new HashSet<int>{0xFA,0x6E}.Where(c=>placements.Any(p=>!p.Retired&&string.Equals(p.SourceLevelKey,Target.Key,StringComparison.OrdinalIgnoreCase)&&
                System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(original.Row(p.SourceRowIndex).AsSpan(0x36))==c)).Append(RescueActorClass).ToHashSet();
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
        var composed=UniversalMobyComposer.Compose(Target,prepared,_overlay,executable,donors.Get,donors.SharedClasses,
            UniversalMobyExport.DefaultProtectedVram(_baseline,Target),placements,special,
            sharedModelPackage:donors.SharedModelPackage,playerAnimationSource:donors.PlayerAnimations,
            persistentNativePrefixRows:Document.TargetBinding!.NativePrefixRows,nativeDestinationOwner:nativeOwner,nativeOriginalExecutable:donors.Disc.Exe,
            dependencyClasses:dependencies,releasedNativeClasses:released);
        RewardFallbacks.RequireInstalled(composed.LevelData);
        return composed;
    }
}
