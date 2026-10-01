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
public sealed class NativeFreshMainCompilation
{
    private readonly byte[] _body, _overlay, _exe;
    private readonly string _baseline;
    private readonly NativeFreeFlightCompositionPlan? _flight;
    private readonly IReadOnlyList<NativeId65FreshExecutableWindow> _bootstrapWindows;
    private NativeFreshMainCompilation(SceneDocument document, string baseline, byte[] body, byte[] overlay, byte[] exe, NativeFreeFlightCompositionPlan? flight,
        IReadOnlyList<NativeId65FreshExecutableWindow> bootstrapWindows)
    { Document=document;_baseline=baseline;_body=body;_overlay=overlay;_exe=exe;_flight=flight;_bootstrapWindows=bootstrapWindows; }
    public SceneDocument Document { get; }
    public LevelDefinition Target => SceneDocument.NativeTarget(Document.Name);
    public byte[] CopyLevelData()=>_body.ToArray();
    public byte[] CopyExecutable()=>_exe.ToArray();
    public byte[] CopyOverlay()=>_overlay.ToArray();

    public static async Task<NativeFreshMainCompilation> CaptureAsync(string originalUSA, SceneDocument document,
        string scratchBaselinePath, CancellationToken token=default, NativeFreeFlightCompositionPlan? flight=null)
    {
        document.Validate();
        var materials=document.Surfaces.Where(s=>s.Material!=null).Select(s=>s.Material!).Distinct().ToArray();
        byte[] mesh=NativeAuthoredMesh.Compile(document,materials);
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
        byte[] upload=fresh.CopyUploadPayload();
        byte[] metadata=[8,0,0,0,0,0,0,0];
        if(materials.Length>0)
        {
            var sources=materials.Select(m=>NativeStaticTerrainTextureSource.Capture(originalUSA,m.SourceWadEntry,m.TextureId)).ToArray();
            var allocation=NativeStaticTerrainTextureAllocation.CompileSelected(upload,fresh.UploadOwners,sources.Select((s,i)=>
                new NativeStaticTerrainTextureInput(i,s.Handle,"source-right-half-vram:wad-"+s.SourceWadEntry,
                    NativeStaticTerrainTextureAllocation.RelocationPolicyId,s.Layout.CopyRecord(),s.CopySourceVram())),0x8000);
            upload=allocation.CopyUploadPayload();metadata=NativeTerrainMaterialTableCodec.Encode(allocation.Records.ToArray());
        }
        byte[] data=[..metadata,..mesh,..oldData.AsSpan(oldLayout.Portal.Offset).ToArray()];
        if(data.Length>0x20000)throw new InvalidDataException("Authored native data exceeds the bounded main-editor allocation.");
        var body=new UniversalLevelData(issued.DiscPlan.CopyBody());
        var dataFile=body.Subfiles()[1];int growth=Math.Max(0,UniversalLevelData.Align(data.Length,0x800)-dataFile.Length);
        if(growth>0)body.GrowSubfile(1,growth);
        dataFile=body.Subfiles()[1];body.Bytes.AsSpan(dataFile.Offset,dataFile.Length).Clear();data.CopyTo(body.Bytes,dataFile.Offset);
        var vramFile=body.Subfiles()[0];
        if(vramFile.Length!=upload.Length)throw new InvalidDataException("Fresh native upload allocation changed unexpectedly.");
        upload.CopyTo(body.Bytes,vramFile.Offset);
        var sceneFile=body.Subfiles()[3];
        _=NativeId65FreshSceneLayout.DecodeCanonical(body.Bytes.AsSpan(sceneFile.Offset,sceneFile.Length));
        BinaryPrimitives.WriteInt32LittleEndian(body.Bytes.AsSpan(sceneFile.Offset),checked((int)MathF.Round(document.Spawn.X*16)));
        BinaryPrimitives.WriteInt32LittleEndian(body.Bytes.AsSpan(sceneFile.Offset+4),checked((int)MathF.Round(document.Spawn.Y*16)));
        BinaryPrimitives.WriteInt32LittleEndian(body.Bytes.AsSpan(sceneFile.Offset+8),checked((int)MathF.Round(document.Spawn.Z*16)));
        body.Bytes[sceneFile.Offset+14]=(byte)document.Heading;
        _=NativeId65FreshSceneLayout.DecodeCanonical(body.Bytes.AsSpan(sceneFile.Offset,sceneFile.Length));
        if (flight != null) body = new UniversalLevelData(flight.MergeLevelData(65, body.Bytes).CopyLevelData());
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
            File.Move(stage,scratchBaselinePath,true);
            return new(document,scratchBaselinePath,body.Bytes,issued.DiscPlan.CopyOverlay(),issued.DiscPlan.CopyExecutable(),flight,issued.DiscPlan.Executable.SequentialStages);
        }
        finally {if(File.Exists(stage))File.Delete(stage);}
    }

    public UniversalComposeResult Compose(UniversalSourceLibrary donors,IReadOnlyList<UniversalMobyPlacement> placements,
        Func<string,UniversalSpecialAsset?>? special=null)
        =>ComposeWithExecutable(donors,placements,_exe,special);

    private UniversalComposeResult ComposeWithExecutable(UniversalSourceLibrary donors,IReadOnlyList<UniversalMobyPlacement> placements,
        byte[] actualExecutable,Func<string,UniversalSpecialAsset?>? special)
    {
        NativeFreshTreasureTarget.RequireZeroBaseline(donors.Disc.Exe, actualExecutable);
        // The canonical zero-actor scene includes only one spare placed row.
        // Size its owned empty arena before imports consume further placed rows;
        // the ordinary composer still checks the final dynamic reserve and RAM.
        byte[] prepared = NativeFreshPlacementArena.Prepare(_body, placements.Count(p => !p.Retired));
        return UniversalMobyComposer.Compose(Target,prepared,_overlay,actualExecutable,
            key => string.Equals(key, Target.Key, StringComparison.OrdinalIgnoreCase)
                ? (Target, new UniversalLevelData(prepared), new UniversalOverlay(_overlay, UniversalExeBindings.Read(actualExecutable,65)))
                : donors.Get(key), donors.SharedClasses,
            UniversalMobyExport.DefaultProtectedVram(_baseline,Target),placements,special,sharedModelPackage: donors.SharedModelPackage, playerAnimationSource: donors.PlayerAnimations);
    }

    public async Task<string> ExportAsync(UniversalSourceLibrary donors,IReadOnlyList<UniversalMobyPlacement> placements,
        UniversalJapaneseSource? japanese,string outputImage,string identityWorkspace,LevelCatalog nativeCatalog,CancellationToken token=default,Action? requireDocumentCurrent=null,
        string? stagedRetailImage=null,IReadOnlyList<(int LevelIndex,int Row,int Colour)>? retailEggs=null,IReadOnlySet<string>? retailSignLevels=null,
        IReadOnlyList<LevelTextReplacement>? retailNames=null,bool startInAuthoredLevel=true)
    {
        if(Path.GetFullPath(outputImage)==Path.GetFullPath(donors.ImagePath))throw new InvalidDataException("Fresh export cannot replace its original source image.");
        ExportArtifactTransaction.RequireSourceSeparation(donors.ImagePath,null,outputImage,Path.ChangeExtension(outputImage,".cue"));
        ExportArtifactTransaction.RequireSourceSeparation(stagedRetailImage??_baseline,null,outputImage,Path.ChangeExtension(outputImage,".cue"));
        UniversalNativeLayoutStore.Validate(identityWorkspace,Target.Key,0);
        var baseline=UniversalDiscImage.Read(stagedRetailImage??_baseline);
        byte[] stagedExe=stagedRetailImage==null?_exe.ToArray():MergeAuthoredExecutable(donors.Disc.Exe,baseline.Exe);
        NativeFreshTreasureTarget.RequireZeroBaseline(donors.Disc.Exe,stagedExe);
        NativeFreshNewGameEntry.Apply(donors.Disc.Exe,stagedExe,startInAuthoredLevel);
        var compose=placements.Count == 0 ? null : ComposeWithExecutable(donors,placements,stagedExe,UniversalMobyExport.SpecialProvider(japanese));
        byte[] levelData=compose?.LevelData ?? _body.ToArray(), overlayData=compose?.Overlay ?? _overlay.ToArray();
        List<(int LevelIndex,int Row,int Colour)> eggs=retailEggs?.ToList()??[];
        for(int i=0;i<placements.Count;i++)if(!placements[i].Retired&&placements[i].SpecialId is { } id&&UniversalDragonflyEggs.Variants.Any(v=>v.Id==id))
            eggs.Add((UniversalMobyExport.CollectedMaskIndex(Target),compose!.PlacementSaveRows[i],UniversalDragonflyEggs.VariantIndex(id)));
        byte[] exe=compose?.Executable ?? stagedExe;
        HashSet<string> signLevels=new(retailSignLevels??new HashSet<string>(),StringComparer.OrdinalIgnoreCase);
        if(placements.Any(p=>!p.Retired&&p.SpecialId==UniversalDragonflyEggs.SignId))signLevels.Add(Target.Key);
        UniversalSpecialBehaviors.PatchExeCave(exe,donors.Disc.Exe,signLevels.Count>0,eggs);
        if (_flight != null) exe = _flight.ApplyToExecutable(exe);
        var title = new TextTargetEntry(Target.Key, Target.ScriptKey, Document.Name, "LAB", 3, 19, LevelTextTableKind.LevelNames, 35);
        exe = LevelTextPatchExporter.ComposeStagedExecutable(stagedRetailImage??_baseline, exe, [..retailNames??[],new(title, Document.Name)]);
        if (_flight?.Enabled == true) _ = _flight.RequireFinalLevelClosure(65, levelData, exe);
        uint runtimeEnd = UniversalMobyComposer.RuntimeEnd(new UniversalLevelData(levelData), new UniversalOverlay(overlayData,UniversalExeBindings.Read(exe,65)).CopyBuffer);
        if(runtimeEnd > UniversalMobyComposer.NormalBufferFloor-UniversalMobyComposer.RamSafetyMargin)
            throw new InvalidDataException("The complete authored scene exceeds the native normal-buffer memory budget.");
        Dictionary<int,byte[]> replacements = new() {{79,overlayData},{80,levelData}};
        UniversalEggUnlocks.PatchOverlays(nativeCatalog,donors.Disc.EntryBytes,baseline.EntryBytes,replacements,eggs.Count);
        if(signLevels.Count>0)
            UniversalMobyExport.RequireSignMarkersOnlyIn(exe,new LevelCatalog(nativeCatalog.Levels.Where(l=>l.LevelId!=65).Append(Target)),signLevels,
                entry=>replacements.TryGetValue(entry,out var bytes)?bytes:baseline.EntryBytes(entry));
        if(_flight?.Enabled==true)
            foreach(var level in nativeCatalog.Levels.Where(l=>NativeFreeFlightCompositionPlan.IsNormalLevel(l.LevelId)))
                _=_flight.RequireFinalLevelClosure(level.LevelId,baseline.EntryBytes(level.SourceWadEntry),exe);
        byte[] wad=baseline.RebuildWad(replacements);
        string stage=outputImage+".pending-"+Guid.NewGuid().ToString("N"),cue=Path.ChangeExtension(outputImage,".cue"),stagedCue=stage+".cue";
        bool retainStage=false;
        try
        {
            baseline.WriteImage(stage,wad,exe);token.ThrowIfCancellationRequested();
            var read=UniversalDiscImage.Read(stage);
            if(!read.EntryBytes(80).AsSpan().SequenceEqual(levelData)||!read.EntryBytes(79).AsSpan().SequenceEqual(overlayData)||!read.Exe.AsSpan().SequenceEqual(exe))
                throw new InvalidDataException("Fresh native image readback differs from its authored mesh and object composition.");
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
