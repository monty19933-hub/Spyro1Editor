using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Universal;
using Spyro.Editor.Core.Text;

namespace Spyro.Editor.Core.Exporting;

/// <summary>Source-bound native ID65 bootstrap plus the main editor's actual authored mesh.
/// The retained player/resources are engine dependencies; no source level terrain or placed actors survive.</summary>
public sealed class NativeFreshMainCompilation : INativeAuthoredCompilation
{
    private readonly byte[] _body, _overlay, _exe;
    private readonly string _baseline;
    internal NativeFreshRewardFallbackResources RewardFallbacks { get; }
    private readonly NativeFreeFlightCompositionPlan? _flight;
    private readonly IReadOnlyList<NativeId65FreshExecutableWindow> _bootstrapWindows;
    private NativeFreshMainCompilation(SceneDocument document, string baseline, byte[] body, byte[] overlay, byte[] exe, NativeFreeFlightCompositionPlan? flight,
        IReadOnlyList<NativeId65FreshExecutableWindow> bootstrapWindows, NativeFreshRewardFallbackResources rewardFallbacks, NativeAuthoredMeshRenderer.DrawPlan? drawPlan,
        NativeSuperchargeAnimations? supercharge)
    { Document=document;_baseline=baseline;_body=body;_overlay=overlay;_exe=exe;_flight=flight;_bootstrapWindows=bootstrapWindows; RewardFallbacks=rewardFallbacks; DrawPlan=drawPlan; Supercharge=supercharge; }
    /// <summary>Spyro's supercharge animations, carried when the scene has supercharge ground (null otherwise).</summary>
    internal NativeSuperchargeAnimations? Supercharge { get; }
    /// <summary>How the terrain loads the frame's draw buffer (null: no terrain survey); its near distance goes to the
    /// draw-buffer guard (<see cref="NativeDrawBufferGuard"/>).</summary>
    internal NativeAuthoredMeshRenderer.DrawPlan? DrawPlan { get; }
    public int NearDistance => DrawPlan?.NearDistance ?? NativeDrawBufferGuard.MaxNearDistance;
    public SceneDocument Document { get; }
    public LevelDefinition Target => SceneDocument.NativeTarget(Document.Name);
    public byte[] CopyLevelData()=>_body.ToArray();
    public byte[] CopyExecutable()=>_exe.ToArray();
    public byte[] CopyOverlay()=>_overlay.ToArray();

    public static async Task<NativeFreshMainCompilation> CaptureAsync(string originalUSA, SceneDocument document,
        string scratchBaselinePath, CancellationToken token=default, NativeFreeFlightCompositionPlan? flight=null, NativeHubPortals? portals=null)
    {
        ExportArtifactTransaction.RequireSourceSeparation(originalUSA,null,scratchBaselinePath);
        NativeAuthoredSpawn.RequireSupported(document);
        var materials=document.Surfaces.Where(s=>s.Material!=null).Select(s=>s.Material!).Distinct().ToArray();
        // Faces whose far colours were never chosen are drawn far away in their texture's own colour.
        var drawn=NativeAuthoredFarColors.Derive(document,m=>NativeAuthoredFarColors.Average(originalUSA,m));
        byte[] mesh=NativeAuthoredMesh.CompileWithSky(drawn,materials,NativeAuthoredMesh.CaptureSky(originalUSA,document),portals?.Triggers());
        var drawPlan=NativeAuthoredMeshRenderer.LastDrawPlan;
        // Source-issued bootstrap captures the complete player, companion, native audio and zero-actor scene.
        // Its generated surface is discarded in full; final geometry comes only from NativeAuthoredMesh.
        // The throwaway bootstrap floor is fixed; it must not constrain the authored spawn.
        // Its complete surface is replaced below and its canonical neutral scene is rebound separately.
        const int sx=8960,sy=8960,sz=9216;
        int floor=sz-512;
        var baseMaterial=NativeFreshFloorMaterialIntent.BaseFloorColor();
        var selected=materials.Length==0?baseMaterial:NativeFreshFloorMaterialIntent.RetailTerrain("spyro1-usa-retail-mode2-2352-v1",materials[0].SourceWadEntry,materials[0].TextureId);
        var tile=NativeFreshFlatTileIntent.Create(Guid.NewGuid().ToString("N"),sx-128,sy-128,floor,256,256,new(128,96,64),NativeFreshFloorMaterialsIntent.Create(selected,selected));
        var seed=NativeFreshLevelDocumentIntent.CreateTiled(NativeFreshTiledLevelContent.Create([tile],sx,sy,sz,document.Heading,document.SkyColor,document.MusicTrack),[]);
        var issued=materials.Length==0?await NativeId65TiledDocumentCompilation.CaptureAsync(originalUSA,seed,token):await NativeId65TiledDocumentCompilation.CaptureSelectedMaterialsAsync(originalUSA,seed,token);
        byte[] originalExe=await NativeId65AuthoredSurfaceConsumers.ReadOriginalExecutableAsync(originalUSA,token);
        _=NativeId65AuthoredSurfaceConsumers.Capture(originalExe);
        var fresh=issued.FreshResourceAncestor;
        byte[] oldData=fresh.CopyDataComponents();var oldLayout=NativeId65NormalComponentLayout.Decode(oldData);
        var rewardFallbacks=await NativeFreshRewardFallbackResources.CaptureAsync(originalUSA,fresh,token);
        byte[] upload=rewardFallbacks.CopyUploadPayload();
        byte[] metadata=[8,0,0,0,0,0,0,0];
        if(materials.Length>0)
        {
            var sources=materials.Select(m=>NativeStaticTerrainTextureSource.Capture(originalUSA,m.SourceWadEntry,m.TextureId)).ToArray();
            var allocation=NativeStaticTerrainTextureAllocation.CompileSelected(upload,rewardFallbacks.UploadOwners,sources.Select((s,i)=>
                new NativeStaticTerrainTextureInput(i,s.Handle,"source-right-half-vram:wad-"+s.SourceWadEntry,
                    NativeStaticTerrainTextureAllocation.RelocationPolicyId,s.Layout.CopyRecord(),s.CopySourceVram())),0x8000);
            upload=allocation.CopyUploadPayload();metadata=NativeTerrainMaterialTableCodec.Encode(allocation.Records.ToArray());
            // Materials with their own picture: written into the copied texture at every distance.
            Dictionary<int,NativeTerrainTextureImageWriter.Image> images=[];
            for(int i=0;i<materials.Length;i++)
                if(materials[i].Image is {} path)
                {
                    if(!File.Exists(path))throw new InvalidDataException($"A terrain picture of this level is missing: {path}");
                    // The compiled face lists its corners in reverse (NativeAuthoredMeshRenderer.Encode, for the game's
                    // back-face test), so the game draws the texture's top along the face's corners 2-3. The picture is
                    // stored upside down so its bottom row lands on corners 2-3, the bottom edge the scene documents.
                    static Rgba32[] Upright(Rgba32[] p,int side){var r=new Rgba32[p.Length];for(int y=0;y<side;y++)p.AsSpan((side-1-y)*side,side).CopyTo(r.AsSpan(y*side,side));return r;}
                    images[i]=new(Upright(PngRgbaImage.ReadResizedRgba(path,NativeTerrainTextureImageWriter.NormalSide,NativeTerrainTextureImageWriter.NormalSide),NativeTerrainTextureImageWriter.NormalSide),
                        Upright(PngRgbaImage.ReadResizedRgba(path,NativeTerrainTextureImageWriter.CloseSide,NativeTerrainTextureImageWriter.CloseSide),NativeTerrainTextureImageWriter.CloseSide));
                }
            // The upload is the 512 KB VRAM image followed by the sound bank; pictures go into the VRAM image.
            if(images.Count>0)NativeTerrainTextureImageWriter.Write(upload.AsSpan(0,0x80000),metadata,images).CopyTo(upload,0);
        }
        // A rebuilt homeworld's doorways replace the empty portal table (count 0).
        byte[] portalSection=portals?.Section()??oldData.AsSpan(oldLayout.Portal.Offset,oldLayout.Portal.ByteLength).ToArray();
        byte[] data=[..metadata,..mesh,..portalSection,..oldData.AsSpan(oldLayout.Portal.Offset+oldLayout.Portal.ByteLength).ToArray()];
        if(data.Length>NativeAuthoredMesh.MaxSurfaceBytes+0x8000+portalSection.Length)throw new InvalidDataException("Authored native data exceeds the bounded main-editor allocation.");
        var body=new UniversalLevelData(issued.DiscPlan.CopyBody());
        var dataFile=body.Subfiles()[1];int growth=Math.Max(0,UniversalLevelData.Align(data.Length,0x800)-dataFile.Length);
        if(growth>0)body.GrowSubfile(1,growth);
        dataFile=body.Subfiles()[1];body.Bytes.AsSpan(dataFile.Offset,dataFile.Length).Clear();data.CopyTo(body.Bytes,dataFile.Offset);
        var vramFile=body.Subfiles()[0];
        if(vramFile.Length!=upload.Length)throw new InvalidDataException("Fresh native upload allocation changed unexpectedly.");
        upload.CopyTo(body.Bytes,vramFile.Offset);
        rewardFallbacks.InstallModels(body);
        var sceneFile=body.Subfiles()[3];
        _=NativeId65FreshSceneLayout.DecodeCanonical(body.Bytes.AsSpan(sceneFile.Offset,sceneFile.Length));
        BinaryPrimitives.WriteInt32LittleEndian(body.Bytes.AsSpan(sceneFile.Offset),checked((int)MathF.Round(document.Spawn.X*16)));
        BinaryPrimitives.WriteInt32LittleEndian(body.Bytes.AsSpan(sceneFile.Offset+4),checked((int)MathF.Round(document.Spawn.Y*16)));
        BinaryPrimitives.WriteInt32LittleEndian(body.Bytes.AsSpan(sceneFile.Offset+8),checked((int)MathF.Round(document.Spawn.Z*16)));
        body.Bytes[sceneFile.Offset+14]=(byte)document.Heading;
        _=NativeId65FreshSceneLayout.DecodeCanonical(body.Bytes.AsSpan(sceneFile.Offset,sceneFile.Length));
        if (flight != null) body = new UniversalLevelData(flight.MergeLevelData(65, body.Bytes).CopyLevelData());
        // Supercharge ground switches Spyro to animations 25 and 45, which this player stream (Town Square's) lacks.
        NativeSuperchargeAnimations? supercharge=null;
        if(NativeSuperchargeAnimations.Needed(document))
        {
            supercharge=await NativeSuperchargeAnimations.CaptureAsync(originalUSA,token);
            uint copy=new UniversalOverlay(issued.DiscPlan.CopyOverlay(),UniversalExeBindings.Read(issued.DiscPlan.CopyExecutable(),65)).CopyBuffer;
            bool fitted=GameRam.Fits(body,copy);
            body=supercharge.Merge(body);
            if(fitted&&!GameRam.Fits(body,copy))throw new InvalidDataException(NativeSuperchargeAnimations.NoRoomMessage(document.Name,GameRam.Shortfall(body,copy)));
            supercharge.RequireDestination(body.Bytes);
        }
        rewardFallbacks.RequireInstalled(body.Bytes);
        if(body.Scene().RowCount!=0)throw new InvalidDataException("Fresh native bootstrap unexpectedly contains source actors.");
        _=UniversalMobyComposer.RuntimeEnd(body,new UniversalOverlay(issued.DiscPlan.CopyOverlay(),UniversalExeBindings.Read(issued.DiscPlan.CopyExecutable(),65)).CopyBuffer);
        Directory.CreateDirectory(Path.GetDirectoryName(scratchBaselinePath)!);
        string stage=scratchBaselinePath+".pending-"+Guid.NewGuid().ToString("N");
        try
        {
            await using(var source=new FileStream(originalUSA,FileMode.Open,FileAccess.Read,FileShare.Read))
            await using(var output=new FileStream(stage,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None))
                _=NativeId65FreshDiscStreaming.Measure(issued.DiscPlan,source,token,output);
            // Preserve the issuer's actual relocation/bootstrap; replace only the complete authored owners.
            var staged=UniversalDiscImage.Read(stage);
            byte[] wad=staged.RebuildWad(new Dictionary<int,byte[]>{{80,body.Bytes}});
            staged.WriteImage(stage,wad,issued.DiscPlan.CopyExecutable());
            var read=UniversalDiscImage.Read(stage);
            if(!read.EntryBytes(80).AsSpan().SequenceEqual(body.Bytes))throw new InvalidDataException("Fresh native scene readback differs from authored output.");
            ExportArtifactTransaction.RequireSourceSeparation(originalUSA,null,scratchBaselinePath);
            File.Move(stage,scratchBaselinePath,true);
            return new(document,scratchBaselinePath,body.Bytes,issued.DiscPlan.CopyOverlay(),issued.DiscPlan.CopyExecutable(),flight,issued.DiscPlan.Executable.SequentialStages,rewardFallbacks,drawPlan,supercharge);
        }
        finally {if(File.Exists(stage))File.Delete(stage);}
    }

    public UniversalComposeResult Compose(UniversalSourceLibrary donors,IReadOnlyList<UniversalMobyPlacement> placements,
        Func<string,UniversalSpecialAsset?>? special=null)
        =>ComposeWithExecutable(donors,placements,_exe,special);

    private UniversalComposeResult ComposeWithExecutable(UniversalSourceLibrary donors,IReadOnlyList<UniversalMobyPlacement> placements,
        byte[] actualExecutable,Func<string,UniversalSpecialAsset?>? special)
    {
        if(UniversalDragonRows.BuiltLevelRefusal(Document.Name,placements,donors) is {} noDragons)throw new InvalidDataException(noDragons);
        NativeFreshTreasureTarget.RequireZeroBaseline(donors.Disc.Exe, actualExecutable);
        // The canonical zero-actor scene includes only one spare placed row.
        // Size its owned empty arena before imports consume further placed rows;
        // the ordinary composer still checks the final dynamic reserve and RAM.
        int active = placements.Count(p => !p.Retired);
        byte[] prepared = NativeFreshPlacementArena.Prepare(_body, active, UniversalMobyComposer.BuiltLevelDynamicSlots(active));
        var composed=UniversalMobyComposer.Compose(Target,prepared,_overlay,actualExecutable,
            key => string.Equals(key, Target.Key, StringComparison.OrdinalIgnoreCase)
                ? (Target, new UniversalLevelData(prepared), new UniversalOverlay(_overlay, UniversalExeBindings.Read(actualExecutable,65)))
                : donors.Get(key), donors.SharedClasses,
            UniversalMobyExport.DefaultProtectedVram(_baseline,Target).Concat(Supercharge?.ProtectedVram??[]),placements,special,sharedModelPackage: donors.SharedModelPackage, playerAnimationSource: donors.PlayerAnimations,nativeOriginalExecutable:donors.Disc.Exe,
            dependencyClasses:UniversalLiquidSplashes.Dependencies(UniversalLiquidSplashes.Liquids(Document.GroundKinds()),null,k=>donors.Get(k).Data));
        RewardFallbacks.RequireInstalled(composed.LevelData);
        Supercharge?.RequireDestination(composed.LevelData);
        return composed;
    }

    public async Task<string> ExportAsync(UniversalSourceLibrary donors,IReadOnlyList<UniversalMobyPlacement> placements,
        UniversalJapaneseSource? japanese,string outputImage,string identityWorkspace,LevelCatalog nativeCatalog,CancellationToken token=default,Action? requireDocumentCurrent=null,
        string? stagedRetailImage=null,IReadOnlyList<(int LevelIndex,int Row,int Colour)>? retailEggs=null,IReadOnlySet<string>? retailSignLevels=null,
        IReadOnlyList<LevelTextReplacement>? retailNames=null,bool startInAuthoredLevel=true,int? startLevelId=null,PortalRouteDocument? portalRoutes=null,
        IReadOnlyDictionary<int,IReadOnlyList<NativePortalReturnClass>>? retailReturnClasses=null,Spyro.Editor.Core.Crash.CrashWarpedDisc? crash=null,
        Spyro.Editor.Core.Music.YearOfTheDragonDisc? yotd=null)
    {
        if(Path.GetFullPath(outputImage)==Path.GetFullPath(donors.ImagePath))throw new InvalidDataException("Fresh export cannot replace its original source image.");
        if(portalRoutes?.Destinations.Any(d=>d.DestinationLevelId==65)==true)
            NativeAuthoredSpawn.RequirePortalArrivalSupported(Document);
        ExportArtifactTransaction.RequireSourceSeparation(donors.ImagePath,null,outputImage,Path.ChangeExtension(outputImage,".cue"));
        ExportArtifactTransaction.RequireSourceSeparation(stagedRetailImage??_baseline,null,outputImage,Path.ChangeExtension(outputImage,".cue"));
        UniversalNativeLayoutStore.Validate(identityWorkspace,Target.Key,0);
        var baseline=UniversalDiscImage.Read(stagedRetailImage??_baseline);
        byte[] stagedExe=stagedRetailImage==null?_exe.ToArray():MergeAuthoredExecutable(donors.Disc.Exe,baseline.Exe);
        // The built level bounds its terrain's share of the frame's draw buffer (a staged image already has the guard).
        NativeDrawBufferGuard.PatchExe(stagedExe,new Dictionary<int,int>{[65]=NearDistance});
        NativeFreshTreasureTarget.RequireZeroBaseline(donors.Disc.Exe,stagedExe);
        if(startLevelId is {} chosen) NativeClearedRetailBinding.ApplyNewGameStart(donors.Disc.Exe,stagedExe,chosen);
        else NativeFreshNewGameEntry.Apply(donors.Disc.Exe,stagedExe,startInAuthoredLevel);
        bool liquids=UniversalLiquidSplashes.Liquids(Document.GroundKinds()).Count>0;
        var compose=placements.Count == 0 && !liquids ? null : ComposeWithExecutable(donors,placements,stagedExe,UniversalMobyExport.SpecialProvider(japanese,donors,crash,yotd));
        Dictionary<int,IReadOnlyList<NativePortalReturnClass>> returnClasses=retailReturnClasses?.ToDictionary(p=>p.Key,p=>p.Value)??[];
        returnClasses[65]=compose==null?[]:NativePortalReturnClass.Capture(compose.Classes);
        byte[] levelData=compose?.LevelData ?? _body.ToArray(), overlayData=compose?.Overlay ?? _overlay.ToArray();
        List<(int LevelIndex,int Row,int Colour)> eggs=retailEggs?.ToList()??[];
        for(int i=0;i<placements.Count;i++)if(!placements[i].Retired&&placements[i].SpecialId is { } id&&UniversalDragonflyEggs.Variants.Any(v=>v.Id==id))
            eggs.Add((UniversalMobyExport.CollectedMaskIndex(Target),compose!.PlacementSaveRows[i],UniversalDragonflyEggs.VariantIndex(id)));
        byte[] exe=compose?.Executable ?? stagedExe;
        HashSet<string> signLevels=new(retailSignLevels??new HashSet<string>(),StringComparer.OrdinalIgnoreCase);
        if(placements.Any(p=>!p.Retired&&UniversalSpecialCatalog.NeedsRenderHook(p.SpecialId)))signLevels.Add(Target.Key);
        UniversalSpecialBehaviors.PatchExeCave(exe,donors.Disc.Exe,signLevels.Count>0,eggs,eggs.Count>0?japanese?.InventoryEggModel():null);
        if (_flight != null) exe = _flight.ApplyToExecutable(exe);
        var title = new TextTargetEntry(Target.Key, Target.ScriptKey, Document.Name, "LAB", 3, 19, LevelTextTableKind.LevelNames, 35);
        exe = LevelTextPatchExporter.ComposeStagedExecutable(stagedRetailImage??_baseline, exe, [..retailNames??[],new(title, Document.Name)]);
        if (_flight?.IsLevelEnabled(65) == true) _ = _flight.RequireFinalLevelClosure(65, levelData, exe);
        // The level's game RAM in the export's memory mode (GameRam: one 2 MB budget, or two with 8 MB RAM).
        {
            var finalData=new UniversalLevelData(levelData);uint copy=new UniversalOverlay(overlayData,UniversalExeBindings.Read(exe,65)).CopyBuffer;
            if(!GameRam.Fits(finalData,copy))
                throw new InvalidDataException(Supercharge!=null&&GameRam.Shortfall(finalData,copy)<=NativeSuperchargeAnimations.ByteLength
                    ?NativeSuperchargeAnimations.NoRoomMessage(Document.Name,GameRam.Shortfall(finalData,copy))
                    :"The complete authored scene exceeds the native normal-buffer memory budget.");
        }
        Dictionary<int,byte[]> replacements = new() {{79,overlayData},{80,levelData}};
        UniversalEggUnlocks.PatchOverlays(nativeCatalog,donors.Disc.EntryBytes,baseline.EntryBytes,replacements,eggs.Count);
        if(signLevels.Count>0)
            UniversalMobyExport.RequireSignMarkersOnlyIn(exe,new LevelCatalog(nativeCatalog.Levels.Where(l=>l.LevelId!=65).Append(Target)),signLevels,
                entry=>replacements.TryGetValue(entry,out var bytes)?bytes:baseline.EntryBytes(entry));
        if(_flight?.Enabled==true)
            foreach(var level in nativeCatalog.Levels.Where(l=>NativeFreeFlightCompositionPlan.IsNormalLevel(l.LevelId) && _flight.IsLevelEnabled(l.LevelId)))
                _=_flight.RequireFinalLevelClosure(level.LevelId,baseline.EntryBytes(level.SourceWadEntry),exe);
        var freshParticleBinding=compose?.FreshParticleBinding;
        if(portalRoutes?.HasEdits==true)
            NativePortalRouteComposer.Apply(donors,new LevelCatalog(nativeCatalog.Levels.Where(l=>l.LevelId!=65).Append(Target)),
                exe,replacements,baseline.EntryBytes,portalRoutes,includesFreshLevel:true,returnClasses:returnClasses, freshParticleBinding:freshParticleBinding, reportFreshParticleBinding:updated=>freshParticleBinding=updated);
        levelData=replacements[80];overlayData=replacements[79];
        RewardFallbacks.RequireInstalled(levelData);
        Supercharge?.RequireDestination(levelData);
        if(freshParticleBinding!=null)NativePortalRouteFreshBinding.RequireOwnedParticleDestination(donors.Disc.Exe,exe,
            new(overlayData,UniversalExeBindings.Read(exe,65)),freshParticleBinding);
        // Emulator only (8 MB RAM): the scenes load in the extra memory; every level is checked against that layout.
        NativeExtendedRam.ApplyForExport(exe,entry=>replacements.TryGetValue(entry,out var bytes)?bytes:baseline.EntryBytes(entry),
            nativeCatalog.Levels.Where(l=>l.LevelId!=65).Append(Target),donors.Disc);
        byte[] wad=baseline.RebuildWad(replacements);
        string stage=outputImage+".pending-"+Guid.NewGuid().ToString("N"),cue=Path.ChangeExtension(outputImage,".cue"),stagedCue=stage+".cue";
        bool retainStage=false;
        try
        {
            baseline.WriteImage(stage,wad,exe);token.ThrowIfCancellationRequested();
            var read=UniversalDiscImage.Read(stage);
            if(!read.EntryBytes(80).AsSpan().SequenceEqual(levelData)||!read.EntryBytes(79).AsSpan().SequenceEqual(overlayData)||!read.Exe.AsSpan().SequenceEqual(exe))
                throw new InvalidDataException("Fresh native image readback differs from its authored mesh and object composition.");
            if(freshParticleBinding!=null)NativePortalRouteFreshBinding.RequireOwnedParticleDestination(donors.Disc.Exe,read.Exe,
                new(read.EntryBytes(79),UniversalExeBindings.Read(read.Exe,65)),freshParticleBinding);
            if(_flight!=null)_flight.RequireResidentEntry8(read.EntryBytes(8));
            foreach(var(entry,bytes) in replacements)
                if(!read.EntryBytes(entry).AsSpan().SequenceEqual(bytes))throw new InvalidDataException("Fresh native bonus-overlay readback differs from its composition.");
            for(int entry=0;entry<256;entry++)
                if(!replacements.ContainsKey(entry)&&!read.EntryBytes(entry).AsSpan().SequenceEqual(baseline.EntryBytes(entry)))
                    throw new InvalidDataException($"Fresh aggregate changed unrelated staged WAD entry {entry}.");
            await File.WriteAllLinesAsync(stagedCue,["FILE \""+Path.GetFileName(outputImage)+"\" BINARY","  TRACK 01 MODE2/2352","    INDEX 01 00:00:00"],token);
            requireDocumentCurrent?.Invoke();
            token.ThrowIfCancellationRequested();
            ExportArtifactTransaction.RequireSourceSeparation(donors.ImagePath,null,outputImage,cue);
            UniversalNativeLayoutStore.Bind(identityWorkspace,Target.Key,0);
            _=ExportArtifactTransaction.Publish(Path.GetDirectoryName(outputImage)!,[new(outputImage,stage),new(cue,stagedCue)]);
            await using var hashStream = File.OpenRead(outputImage);
            return Convert.ToHexStringLower(await SHA256.HashDataAsync(hashStream,token));
        }
        catch(ExportArtifactRollbackException){retainStage=true;throw;}
        finally {if(!retainStage){if(File.Exists(stage))File.Delete(stage);if(File.Exists(stagedCue))File.Delete(stagedCue);}}
    }

    private byte[] MergeAuthoredExecutable(byte[] original,byte[] staged)
    {
        if(original.Length!=_exe.Length||staged.Length!=_exe.Length)
            throw new InvalidDataException("Fresh aggregate executable extent changed.");
        byte[] merged=staged.ToArray();
        bool[] covered=new bool[original.Length];
        foreach(var window in _bootstrapWindows)
        {
            // The guarded name-pool allocator installs the authored indexed title later.
            // A staged retail rename can legitimately have moved this shared pool.
            if(window.Name=="fixed-four-byte-LAB-title")continue;
            int at=window.FileOffset,length=Convert.FromHexString(window.AfterHex).Length;
            var before=original.AsSpan(at,length);var after=_exe.AsSpan(at,length);
            if(before.SequenceEqual(after))continue;
            var current=merged.AsSpan(at,length);
            if(!current.SequenceEqual(before)&&!current.SequenceEqual(after))
                throw new InvalidDataException($"Fresh aggregate conflicts with staged executable owner {window.Name} at 0x{at:X}.");
            after.CopyTo(current);covered.AsSpan(at,length).Fill(true);
        }
        for(int at=0;at<original.Length;at++)
            if(original[at]!=_exe[at]&&!covered[at]&&at is not (>=0x65D64 and <0x65D68))
                throw new InvalidDataException($"Fresh aggregate has an undeclared executable delta at 0x{at:X}.");
        return merged;
    }
}
