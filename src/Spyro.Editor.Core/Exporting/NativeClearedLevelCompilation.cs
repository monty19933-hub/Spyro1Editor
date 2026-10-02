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
    private NativeClearedLevelCompilation(SceneDocument document, LevelDefinition target, string baseline,
        byte[] body, byte[] overlay, byte[] exe)
    { Document=document; Target=target; _baseline=baseline; _body=body; _overlay=overlay; _exe=exe; }
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
        string kernelPath=scratchBaselinePath+".kernel-"+Guid.NewGuid().ToString("N");
        string stage=scratchBaselinePath+".pending-"+Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(scratchBaselinePath))!);
        try
        {
            // Reuse only the authenticated neutral engine/resources and compiled authored body.
            // The ID65 bootstrap and its WAD slots are never installed into this retail image.
            var kernel=await NativeFreshMainCompilation.CaptureAsync(originalUSA,
                document with{TargetBinding=null,Objects=[],Ledger=UniversalPlacementLedger.Empty},kernelPath,token);
            byte[] body=kernel.CopyLevelData(),overlay=kernel.CopyOverlay();
            var replacements=new Dictionary<int,byte[]>{{target.SourceWadEntry,body},{target.SourceWadEntry-1,overlay}};
            original.WriteImage(stage,original.RebuildWad(replacements),exe);
            token.ThrowIfCancellationRequested();
            var read=UniversalDiscImage.Read(stage);
            if(!read.Exe.AsSpan().SequenceEqual(exe)||!read.EntryBytes(target.SourceWadEntry).AsSpan().SequenceEqual(body)||
                !read.EntryBytes(target.SourceWadEntry-1).AsSpan().SequenceEqual(overlay))
                throw new InvalidDataException("The rebuilt retail level failed independent disc readback.");
            ExportArtifactTransaction.RequireSourceSeparation(originalUSA,null,scratchBaselinePath);
            File.Move(stage,scratchBaselinePath,true);
            return new(document,target,scratchBaselinePath,body,overlay,exe);
        }
        finally {if(File.Exists(stage))File.Delete(stage);if(File.Exists(kernelPath))File.Delete(kernelPath);}
    }

    public UniversalComposeResult Compose(UniversalSourceLibrary donors,IReadOnlyList<UniversalMobyPlacement> placements,
        Func<string,UniversalSpecialAsset?>? special=null) => ComposeWithExecutable(donors,placements,_exe,special);

    public UniversalComposeResult ComposeWithExecutable(UniversalSourceLibrary donors,IReadOnlyList<UniversalMobyPlacement> placements,
        byte[] executable,Func<string,UniversalSpecialAsset?>? special=null)
    {
        byte[] prepared=NativeFreshPlacementArena.Prepare(_body,placements.Count(p=>!p.Retired));
        var nativeOwner=new UniversalOverlay(_overlay,UniversalExeBindings.Read(executable,Target.LevelId));
        return UniversalMobyComposer.Compose(Target,prepared,_overlay,executable,donors.Get,donors.SharedClasses,
            UniversalMobyExport.DefaultProtectedVram(_baseline,Target),placements,special,
            sharedModelPackage:donors.SharedModelPackage,playerAnimationSource:donors.PlayerAnimations,
            persistentNativePrefixRows:Document.TargetBinding!.NativePrefixRows,nativeDestinationOwner:nativeOwner);
    }
}
