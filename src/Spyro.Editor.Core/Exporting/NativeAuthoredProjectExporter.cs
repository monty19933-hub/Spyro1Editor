using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Music;
using Spyro.Editor.Core.Text;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.Core.Exporting;

public sealed record NativeAuthoredExportScene(SceneDocument Document,IReadOnlyList<UniversalMobyPlacement> Placements);

/// <summary>Composes every saved replacement with ordinary saved edits. Shared special hooks
/// are installed once, after all level-local writers, and the final BIN/CUE publish together.</summary>
public static class NativeAuthoredProjectExporter
{
    public static async Task<string> ExportAsync(UniversalSourceLibrary donors,LevelCatalog catalog,
        IReadOnlyList<NativeAuthoredExportScene> scenes,UniversalJapaneseSource? japanese,string outputImage,
        string workspace,int startLevelId,CancellationToken token=default,Action? requireCurrent=null,
        string? stagedRetailImage=null,IReadOnlyList<(int LevelIndex,int Row,int Colour)>? retailEggs=null,
        IReadOnlySet<string>? retailSignLevels=null,IReadOnlyList<LevelTextReplacement>? retailNames=null,
        NativeFreeFlightCompositionPlan? flight=null,PortalRouteDocument? portalRoutes=null,
        IReadOnlyDictionary<int,IReadOnlyList<NativePortalReturnClass>>? retailReturnClasses=null,
        Spyro.Editor.Core.Crash.CrashWarpedDisc? crash=null,Spyro.Editor.Core.Music.YearOfTheDragonDisc? yotd=null,
        NewWorldDocument? world=null,IReadOnlyList<NativeNewWorldExport.WorldLevel>? worldLevels=null)
    {
        // The staged disc images are working copies; remove them whether the export succeeds or fails.
        string scratch=Path.Combine(ExportScratchSpace.AggregateRoot(workspace),Guid.NewGuid().ToString("N"));
        try
        {
            // New Game in a level of the new world: the game is made starting in Artisans, then the world step moves it.
            int? worldStart=startLevelId is >=NativeLevelWindow.FirstId and <=NativeLevelWindow.LastId?startLevelId:null;
            if(worldStart!=null&&(world==null||worldLevels?.Any(l=>l.Document.WorldLevelId==worldStart)!=true))
                throw new InvalidDataException("New Game can only start in a level of your new world when the world is part of the game.");
            if(worldStart!=null)startLevelId=10;
            if(world==null||worldLevels is not {Count:>0})
                return await ExportCoreAsync(donors,catalog,scenes,japanese,outputImage,workspace,startLevelId,scratch,token,requireCurrent,
                    stagedRetailImage,retailEggs,retailSignLevels,retailNames,flight,portalRoutes,retailReturnClasses,crash,yotd);
            // With a new world the game is made beside the output first and the world goes in last (its levels after the
            // game's, the level window over the finished executable); only the complete image is published, so a world
            // that fails leaves the previous BIN as it was.
            string directory=Path.GetDirectoryName(Path.GetFullPath(outputImage))!,cue=Path.ChangeExtension(outputImage,".cue");
            string staged=Path.Combine(directory,"."+Path.GetFileNameWithoutExtension(outputImage)+".world-"+Guid.NewGuid().ToString("N")+".bin");
            string stagedCue=Path.ChangeExtension(staged,".cue");
            bool retain=false;
            try
            {
                await ExportCoreAsync(donors,catalog,scenes,japanese,staged,workspace,startLevelId,scratch,token,requireCurrent,
                    stagedRetailImage,retailEggs,retailSignLevels,retailNames,flight,portalRoutes,retailReturnClasses,crash,yotd,withWorld:true);
                requireCurrent?.Invoke();
                await NativeNewWorldExport.ApplyAsync(donors,catalog,world,worldLevels,staged,Path.Combine(scratch,"world"),
                    UniversalMobyExport.SpecialProvider(japanese,donors,crash,yotd),token,worldStart);
                await File.WriteAllLinesAsync(stagedCue,["FILE \""+Path.GetFileName(outputImage)+"\" BINARY","  TRACK 01 MODE2/2352","    INDEX 01 00:00:00"],token);
                requireCurrent?.Invoke();
                token.ThrowIfCancellationRequested();
                ExportArtifactTransaction.RequireSourceSeparation(donors.ImagePath,null,outputImage,cue);
                _=ExportArtifactTransaction.Publish(directory,[new(outputImage,staged),new(cue,stagedCue)]);
                await using var stream=File.OpenRead(outputImage);
                return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream,token));
            }
            catch(ExportArtifactRollbackException){retain=true;throw;}
            finally{if(!retain){if(File.Exists(staged))File.Delete(staged);if(File.Exists(stagedCue))File.Delete(stagedCue);}}
        }
        finally{ExportScratchSpace.Delete(scratch);}
    }

    private static async Task<string> ExportCoreAsync(UniversalSourceLibrary donors,LevelCatalog catalog,
        IReadOnlyList<NativeAuthoredExportScene> scenes,UniversalJapaneseSource? japanese,string outputImage,
        string workspace,int startLevelId,string scratch,CancellationToken token,Action? requireCurrent,
        string? stagedRetailImage,IReadOnlyList<(int LevelIndex,int Row,int Colour)>? retailEggs,
        IReadOnlySet<string>? retailSignLevels,IReadOnlyList<LevelTextReplacement>? retailNames,
        NativeFreeFlightCompositionPlan? flight,PortalRouteDocument? portalRoutes,
        IReadOnlyDictionary<int,IReadOnlyList<NativePortalReturnClass>>? retailReturnClasses,
        Spyro.Editor.Core.Crash.CrashWarpedDisc? crash,Spyro.Editor.Core.Music.YearOfTheDragonDisc? yotd,bool withWorld=false)
    {
        if(scenes.Count==0&&!withWorld)throw new InvalidDataException("No authored scenes were selected for export.");
        // ID65 is source-issued through Town's initializer. Composing two
        // distinct overlays would rewrite the same callback instruction pairs.
        // Refuse before reading donors, creating scratch files or staging edits.
        if(scenes.Any(s=>s.Document.TargetBinding?.LevelId==13)&&
            scenes.Any(s=>s.Document.TargetBinding==null))
            throw new InvalidDataException("Town Square and the new level cannot be exported together because they share native callback bindings. Export them separately or restore Town Square before combining them.");
        foreach(var scene in scenes)
        {
            NativeAuthoredSpawn.RequireSupported(scene.Document);
            if(scene.Document.TargetBinding!=null || portalRoutes?.Destinations.Any(d=>d.DestinationLevelId==65)==true)
                NativeAuthoredSpawn.RequirePortalArrivalSupported(scene.Document);
        }
        if(scenes.Any(s=>s.Document.TargetBinding==null&&MusicTrackCatalog.IsCustom(s.Document.MusicTrack)))
            throw new InvalidDataException("The new level can only play Spyro's own music. Choose a Spyro track for it, or give the Crash song to a rebuilt level.");
        var targets=scenes.Select(s=>s.Document.ResolveTarget(catalog)).ToArray();
        if(targets.Select(t=>t.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=targets.Length)
            throw new InvalidDataException("The same level cannot be rebuilt twice in one export.");
        if(startLevelId!=10&&!targets.Any(t=>t.LevelId==startLevelId))
            throw new InvalidDataException("Select Artisans or an included authored level for New Game.");
        string cue=Path.ChangeExtension(outputImage,".cue");
        ExportArtifactTransaction.RequireSourceSeparation(donors.ImagePath,null,outputImage,cue);
        ExportArtifactTransaction.RequireSourceSeparation(stagedRetailImage??donors.ImagePath,null,outputImage,cue);
        Directory.CreateDirectory(scratch);
        var baseline=UniversalDiscImage.Read(stagedRetailImage??donors.ImagePath);
        byte[] exe=baseline.Exe.ToArray();
        Dictionary<int,byte[]> replacements=[];
        Dictionary<int,IReadOnlyList<NativePortalReturnClass>> returnClasses=retailReturnClasses?.ToDictionary(p=>p.Key,p=>p.Value)??[];
        List<(int LevelIndex,int Row,int Colour)> eggs=retailEggs?.ToList()??[];
        HashSet<string> signs=new(retailSignLevels??new HashSet<string>(),StringComparer.OrdinalIgnoreCase);
        List<LevelTextReplacement> names=[..retailNames??[]];
        List<(string Key,int Prefix)> identities=[];
        // Rebuilt slots with supercharge ground: their final level data must still carry Spyro's supercharge animations.
        List<(int Entry,NativeSuperchargeAnimations Animations)> supercharged=[];
        // Each built level's near distance for the draw-buffer guard (a fresh level's is set when it is compiled, below).
        Dictionary<int,int> nearDistances=scenes.Any(s=>s.Document.TargetBinding==null)?new(){[65]=NativeDrawBufferGuard.MaxNearDistance}:[];
        // The user's Crash disc supplies Crash boxes and enemies, as in the retail-level export.
        var special=UniversalMobyExport.SpecialProvider(japanese,donors,crash,yotd);
        // A rebuilt homeworld's doorways carry their destinations' skies as this export makes them: a level rebuilt here
        // shows its borrowed sky (none borrowed: the doorway keeps the retail copy), any other level its sky from the
        // staged image (with its sky edits).
        byte[]? DestinationSky(int levelId)
        {
            var built=scenes.FirstOrDefault(s=>s.Document.TargetBinding?.LevelId==levelId)?.Document;
            if(built!=null)return NativeAuthoredMesh.CaptureSky(donors.ImagePath,built);
            var level=catalog.Levels.FirstOrDefault(l=>l.LevelId==levelId);
            if(level==null||level.SourceWadEntry<=0)return null;
            var data=new UniversalLevelData(baseline.EntryBytes(level.SourceWadEntry));var model=data.Subfiles()[1];
            return Spyro1SkyBlockAnalyzer.ReadPrimarySkyBlock(data.Bytes.AsSpan(model.Offset,model.Length).ToArray());
        }
        foreach(var scene in scenes.Where(s=>s.Document.TargetBinding!=null).OrderBy(s=>s.Document.TargetBinding!.LevelId))
        {
            token.ThrowIfCancellationRequested();requireCurrent?.Invoke();
            var doc=scene.Document;var target=doc.ResolveTarget(catalog);int prefix=doc.TargetBinding!.NativePrefixRows;
            UniversalNativeLayoutStore.Validate(workspace,target.Key,prefix);
            if(eggs.Any(e=>e.LevelIndex==UniversalMobyExport.CollectedMaskIndex(target))||signs.Contains(target.Key)||
                names.Any(n=>n.Target.LevelKey==target.Key))
                throw new InvalidDataException("Saved retail edits must not also target a cleared replacement level.");
            var compiled=await NativeClearedLevelCompilation.CaptureAsync(donors.ImagePath,doc,catalog,Path.Combine(scratch,target.Key+"-host.bin"),token,DestinationSky);
            nearDistances[target.LevelId]=compiled.NearDistance;
            exe=NativeClearedRetailBinding.Prepare(donors.Disc.Exe,exe,target);
            NativeKillHeights.LowerFor(donors.Disc.Exe,exe,target.LevelId,doc);
            var result=!compiled.NeedsComposition(scene.Placements)?null:compiled.ComposeWithExecutable(donors,scene.Placements,exe,special);
            exe=result?.Executable??exe;
            returnClasses[target.LevelId]=result==null?[]:NativePortalReturnClass.Capture(result.Classes);
            replacements.Add(target.SourceWadEntry,result?.LevelData??compiled.CopyLevelData());
            if(compiled.Supercharge is {} supercharge)supercharged.Add((target.SourceWadEntry,supercharge));
            replacements.Add(target.SourceWadEntry-1,result?.Overlay??compiled.CopyOverlay());
            for(int i=0;i<scene.Placements.Count;i++)
            {
                var p=scene.Placements[i];if(p.Retired)continue;
                if(p.SpecialId is {} id&&UniversalDragonflyEggs.Variants.Any(v=>v.Id==id))
                    eggs.Add((UniversalMobyExport.CollectedMaskIndex(target),result!.PlacementSaveRows[i],UniversalDragonflyEggs.VariantIndex(id)));
                if(UniversalSpecialCatalog.NeedsRenderHook(p.SpecialId))signs.Add(target.Key);
            }
            var title=TextTargetCatalog.CreateDefault().FindForLevel(target)??throw new InvalidDataException("Missing retail title slot.");
            names.Add(new(title,doc.Name));
            var music=LevelMusicPatchExporter.BuildBatchPlan(donors.ImagePath,"", "",[new(target,doc.MusicTrack)]);
            foreach(var patch in music.Patches)
            {
                int old=BinaryPrimitives.ReadInt32LittleEndian(exe.AsSpan(patch.ExeFileOffset));
                if(old!=patch.BeforeValue&&old!=patch.AfterValue)throw new InvalidDataException("The rebuilt level conflicts with a staged music edit.");
                BinaryPrimitives.WriteInt32LittleEndian(exe.AsSpan(patch.ExeFileOffset),patch.AfterValue);
            }
            identities.Add((target.Key,prefix));
        }
        // A private intermediate carries all rewritten retail slots into the existing
        // authored65 owner. It is not published or mistaken for a finished game image.
        var rebuilt=scenes.Where(s=>s.Document.TargetBinding!=null).Select(s=>s.Document.TargetBinding!.LevelId).ToHashSet();
        exe=NativeClearedTitleDemos.Prepare(donors.Disc.Exe,exe,rebuilt);
        // Songs from other games (Crash 3's): rendered once, their loop marks set in the executable here, their audio
        // written into every image this export writes.
        var songs=scenes.Select(s=>MusicTrackCatalog.FindAny(s.Document.MusicTrack)).OfType<MusicTrackEntry>()
            .Where(t=>t.CustomSlot!=null).DistinctBy(t=>t.TrackId)
            .Select(t=>CustomMusicSlots.Encode(t,crash,baseline.Path,yotd)).ToArray();
        foreach(var song in songs)CustomMusicSlots.PatchEndTable(exe,baseline.Path,song);
        if(scenes.Any(s=>NativeSubdivisionQueueGuard.Needed(s.Document)))NativeSubdivisionQueueGuard.PatchExe(exe);
        // Every level built here bounds its terrain's share of the frame's draw buffer (retail levels draw as before).
        NativeDrawBufferGuard.PatchExe(exe,nearDistances);
        // Kill heights change only in the rebuilt levels' own words (and only downwards).
        NativeKillHeights.RequireOwned(donors.Disc.Exe,exe,rebuilt);
        foreach(var(entry,animations) in supercharged)animations.RequireDestination(replacements[entry]);
        string stage=Path.Combine(scratch,"retail-stage.bin");
        baseline.WriteImage(stage,baseline.RebuildWad(replacements),exe);
        RequireReadback(stage,baseline,replacements,exe);
        foreach(var song in songs)CustomMusicSlots.WriteAudio(stage,song);
        var fresh=scenes.SingleOrDefault(s=>s.Document.TargetBinding==null);
        if(fresh!=null)
        {
            var compilation=await NativeFreshMainCompilation.CaptureAsync(donors.ImagePath,fresh.Document,
                Path.Combine(scratch,"fresh-host.bin"),token,flight);
            void RequirePublication()
            {
                requireCurrent?.Invoke();
                foreach(var identity in identities)UniversalNativeLayoutStore.Bind(workspace,identity.Key,identity.Prefix);
            }
            string hash=await compilation.ExportAsync(donors,fresh.Placements,japanese,outputImage,workspace,catalog,token,
                RequirePublication,stage,eggs,signs,names,startLevelId==65,startLevelId,portalRoutes,returnClasses,crash,yotd);
            return hash;
        }
        NativeClearedRetailBinding.ApplyNewGameStart(donors.Disc.Exe,exe,startLevelId);
        UniversalSpecialBehaviors.PatchExeCave(exe,donors.Disc.Exe,signs.Count>0,eggs,eggs.Count>0?japanese?.InventoryEggModel():null);
        exe=LevelTextPatchExporter.ComposeStagedExecutable(stage,exe,names);
        UniversalEggUnlocks.PatchOverlays(catalog,donors.Disc.EntryBytes,baseline.EntryBytes,replacements,eggs.Count);
        if(signs.Count>0)UniversalMobyExport.RequireSignMarkersOnlyIn(exe,catalog,signs,
            entry=>replacements.TryGetValue(entry,out var bytes)?bytes:baseline.EntryBytes(entry));
        if(flight?.Enabled==true)throw new InvalidDataException("Experimental flight cannot be merged into a rebuilt retail slot.");
        if(portalRoutes?.HasEdits==true)NativePortalRouteComposer.Apply(donors,catalog,exe,replacements,baseline.EntryBytes,portalRoutes,
            returnClasses:returnClasses);
        NativeKillHeights.RequireOwned(donors.Disc.Exe,exe,rebuilt);
        foreach(var(entry,animations) in supercharged)animations.RequireDestination(replacements[entry]);
        // Emulator only (8 MB RAM): the scenes load in the extra memory; every level is checked against that layout.
        NativeExtendedRam.ApplyForExport(exe,entry=>replacements.TryGetValue(entry,out var bytes)?bytes:baseline.EntryBytes(entry),catalog.Levels,donors.Disc);
        string pending=outputImage+".pending-"+Guid.NewGuid().ToString("N"),pendingCue=pending+".cue";
        bool retain=false;
        try
        {
            baseline.WriteImage(pending,baseline.RebuildWad(replacements),exe);
            RequireReadback(pending,baseline,replacements,exe);
            foreach(var song in songs)CustomMusicSlots.WriteAudio(pending,song);
            await File.WriteAllLinesAsync(pendingCue,["FILE \""+Path.GetFileName(outputImage)+"\" BINARY","  TRACK 01 MODE2/2352","    INDEX 01 00:00:00"],token);
            requireCurrent?.Invoke();token.ThrowIfCancellationRequested();
            foreach(var identity in identities)UniversalNativeLayoutStore.Bind(workspace,identity.Key,identity.Prefix);
            _=ExportArtifactTransaction.Publish(Path.GetDirectoryName(outputImage)!,[new(outputImage,pending),new(cue,pendingCue)]);
            await using var stream=File.OpenRead(outputImage);
            return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream,token));
        }
        catch(ExportArtifactRollbackException){retain=true;throw;}
        finally{if(!retain){if(File.Exists(pending))File.Delete(pending);if(File.Exists(pendingCue))File.Delete(pendingCue);}}
    }

    private static void RequireReadback(string image,UniversalDiscImage baseline,IReadOnlyDictionary<int,byte[]> replacements,byte[] exe)
    {
        var read=UniversalDiscImage.Read(image);
        if(!read.Exe.AsSpan().SequenceEqual(exe))throw new InvalidDataException("Authored project executable readback differs.");
        for(int entry=0;entry<256;entry++)
            if(!read.EntryBytes(entry).AsSpan().SequenceEqual(replacements.TryGetValue(entry,out var bytes)?bytes:baseline.EntryBytes(entry)))
                throw new InvalidDataException($"Authored project WAD entry {entry} failed readback.");
    }
}
