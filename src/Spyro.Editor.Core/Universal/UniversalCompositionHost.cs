using System.Globalization;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Universal;

/// <summary>An exact prepared destination (including terrain textures), with unchanged donor sources.
/// Ownership is enumerated against the same prepared image, never the raw retail texture upload.</summary>
public sealed class UniversalCompositionHost
{
    private readonly byte[] _data, _overlay, _exe, _sharedEntry8;
    private readonly string _imagePath;
    private readonly byte[]? _preparedAsset;
    public LevelDefinition Destination { get; }
    private readonly IReadOnlyList<(long Offset, int Length)> _protected;
    private readonly Dictionary<string, IReadOnlyList<(long Offset, int Length)>> _afterDrop = [];
    public UniversalLevelData DestinationData => new(_data.ToArray());
    public UniversalOverlay DestinationOverlay => new(_overlay.ToArray(), UniversalExeBindings.Read(_exe, Destination.LevelId));
    public byte[] Executable => _exe.ToArray();

    private UniversalCompositionHost(string imagePath, LevelDefinition destination, UniversalDiscImage image, bool prepared = false)
        : this(imagePath,destination,image.EntryBytes(destination.SourceWadEntry),image.EntryBytes(destination.SourceWadEntry-1),image.Exe,prepared,image.EntryBytes(8))
    { }

    private UniversalCompositionHost(string imagePath,LevelDefinition destination,byte[] data,byte[] overlay,byte[] exe,bool prepared,byte[] sharedEntry8)
    {
        _imagePath = imagePath; Destination = destination;
        _data = data.ToArray(); _overlay = overlay.ToArray(); _exe = exe.ToArray(); _sharedEntry8=sharedEntry8.ToArray();
        _preparedAsset = prepared ? _data : null;
        _protected = Protected(new HashSet<int>());
    }

    public static UniversalCompositionHost Capture(string preparedImagePath, LevelDefinition destination) =>
        new(preparedImagePath,destination,UniversalDiscImage.Read(preparedImagePath));

    public static UniversalCompositionHost FromPreparedImage(string unchangedSourceImagePath, string preparedImagePath, LevelDefinition destination)
    {
        var source = UniversalDiscImage.Read(unchangedSourceImagePath);
        var prepared = UniversalDiscImage.Read(preparedImagePath);
        if (!source.EntryBytes(8).AsSpan().SequenceEqual(prepared.EntryBytes(8)))
            throw new InvalidDataException("Prepared fit host changed the shared PETE model owner.");
        return new(unchangedSourceImagePath,destination,prepared,prepared:true);
    }

    private IReadOnlyList<(long Offset, int Length)> Protected(IReadOnlySet<int> dropped) =>
        NativeTexturePageOwnershipScanner.ScanExcludingResidentClasses(_imagePath,Destination,dropped,_preparedAsset)
            .ProtectedRanges.Select(r => (r.Offset,r.Length)).ToArray();

    /// <summary>Applies the real terrain compiler's exact guarded patches to a private in-memory WAD.
    /// The original image, project and prepared source arrays are never modified.</summary>
    public static UniversalCompositionHost FromTerrainPlan(string sourceImagePath, LevelDefinition destination, TerrainPatchPlan plan)
    {
        if (!string.Equals(Path.GetFullPath(plan.SourceImagePath),Path.GetFullPath(sourceImagePath),StringComparison.Ordinal) ||
            !string.Equals(plan.LevelKey,destination.Key,StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Terrain fit host does not match the source image and destination.");
        var image = UniversalDiscImage.Read(sourceImagePath);
        if(plan.NativeLevelReplacement is { } replacement)
        {
            if(plan.Patches.Count!=0||replacement.WadEntry!=destination.SourceWadEntry||replacement.LevelId!=destination.LevelId||
                !image.EntryBytes(destination.SourceWadEntry).AsSpan().SequenceEqual(replacement.Before))
                throw new InvalidDataException("Growing terrain fit host requires its exact complete source body without duplicate granular patches.");
            uint copy=UniversalExeBindings.Read(image.Exe,destination.LevelId)[UniversalExeBindings.CopyBufferGlobal].Value;
            uint original=UniversalMobyComposer.RuntimeEnd(new UniversalLevelData(replacement.Before),copy);
            uint final=UniversalMobyComposer.RuntimeEnd(new UniversalLevelData(replacement.After),copy);
            if(original!=replacement.OriginalRuntimeEnd||final!=replacement.FinalRuntimeEnd||
                GameRam.Exceeds(new UniversalLevelData(replacement.After),copy,new UniversalLevelData(replacement.Before),copy))
                throw new InvalidDataException("Growing terrain fit host failed its source-authenticated native RAM guard.");
            return new(sourceImagePath,destination,replacement.After,image.EntryBytes(destination.SourceWadEntry-1),image.Exe,prepared:true,sharedEntry8:image.EntryBytes(8));
        }
        foreach (var patch in plan.Patches)
        {
            string offsetText = patch.WadRelativeOffset;
            int offset = checked((int)(offsetText.StartsWith("0x",StringComparison.OrdinalIgnoreCase)
                ? long.Parse(offsetText[2..],NumberStyles.HexNumber,CultureInfo.InvariantCulture)
                : long.Parse(offsetText,CultureInfo.InvariantCulture)));
            byte[] before = Convert.FromHexString(patch.BeforeHexPreview.Replace(" ",""));
            byte[] after = Convert.FromHexString(patch.AfterHexPreview.Replace(" ",""));
            if (after.Length != patch.ByteLength || before.Length != after.Length || offset < 0 || (long)offset + after.Length > image.Wad.Length ||
                !image.Wad.AsSpan(offset,before.Length).SequenceEqual(before))
                throw new InvalidDataException("Terrain fit host patch no longer matches its exact source bytes.");
            after.CopyTo(image.Wad.AsSpan(offset));
        }
        return new(sourceImagePath,destination,image,prepared:true);
    }

    public static UniversalCompositionHost FromPrivateTerrainPlan(string sourceImagePath, LevelDefinition destination,
        NativeTerrainTexturePrivateRecordBatchPlan plan,string? preparedSourceImagePath=null)
    {
        string input=preparedSourceImagePath??sourceImagePath;
        if (!string.Equals(Path.GetFullPath(plan.SourceImagePath),Path.GetFullPath(input),StringComparison.Ordinal) ||
            !string.Equals(plan.TargetLevelKey,destination.Key,StringComparison.OrdinalIgnoreCase) || !plan.SourcePreimagesVerified)
            throw new InvalidDataException("Private terrain fit host does not match its verified source and destination.");
        if (plan.FixedTailPlan == null)
            throw new InvalidDataException("A growing private terrain fit host must be captured from its verified staged image.");
        var image = UniversalDiscImage.Read(input);
        if(preparedSourceImagePath!=null&&!image.EntryBytes(8).AsSpan().SequenceEqual(UniversalDiscImage.Read(sourceImagePath).EntryBytes(8)))
            throw new InvalidDataException("Growing private terrain fit host changed shared PETE ownership.");
        foreach (var patch in plan.FixedTailPlan.CombinedPatches)
        {
            int offset = checked((int)patch.WadOffset);
            if (offset < 0 || (long)offset + patch.After.Length > image.Wad.Length ||
                patch.After.Length != patch.ByteLength || patch.Before.Length != patch.After.Length ||
                !image.Wad.AsSpan(offset,patch.Before.Length).SequenceEqual(patch.Before))
                throw new InvalidDataException("Private terrain fit host no longer matches its exact source bytes.");
            patch.After.CopyTo(image.Wad.AsSpan(offset));
        }
        return new(sourceImagePath,destination,image,prepared:true);
    }

    public (UniversalComposeResult? Result, string? Error) DryRun(UniversalSourceLibrary sources,
        IReadOnlyList<UniversalMobyPlacement> placements, Func<string,UniversalSpecialAsset?>? special = null,
        IReadOnlyCollection<int>? removedRows = null)
    {
        try
        {
            return (UniversalMobyComposer.Compose(Destination,_data,_overlay,_exe,sources.Get,sources.SharedClasses,_protected,
                placements,special,removedRows,dropped =>
                {
                    string key = string.Join(',',dropped.Order());
                    lock (_afterDrop)
                    {
                        if (!_afterDrop.TryGetValue(key,out var ranges))
                            _afterDrop[key] = ranges = Protected(dropped);
                        return ranges;
                    }
                },sources.SharedModelPackage, sources.PlayerAnimations,nativeSharedEntry8:_sharedEntry8),null);
        }
        catch (Exception e) when (e is IOException or InvalidDataException) { return (null,e.Message); }
    }
}
