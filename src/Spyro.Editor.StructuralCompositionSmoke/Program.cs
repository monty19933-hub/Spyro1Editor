using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

string root = Directory.CreateTempSubdirectory("spyro-structural-composition-").FullName;
int checks = 0;
try
{
    StructuralExportAddressEffect growth = new("private", StructuralAddressChanges.LevelDataComponents |
        StructuralAddressChanges.NativeSceneSubfiles | StructuralAddressChanges.FollowingWadEntries |
        StructuralAddressChanges.ExecutableExtent, 0x800);
    StructuralExportAddressEffect fixedTail = new("fixed", StructuralAddressChanges.LevelDataComponents, 0);
    StructuralSavedWriterScope mobyWriter = new("Later level", false, true, false, false, false, false, false, false);
    // A tiny cooked image represents a preceding WAD entry growing one sector.
    // The later native row moved, but its source catalog still contains 0x1820.
    const long sourceTable = 0x1820;
    const long relocatedTable = sourceTable + 0x800;
    byte[] image = new byte[48 * 2048];
    Span<byte> wad = image.AsSpan(37 * 2048);
    BinaryPrimitives.WriteUInt32LittleEndian(wad, 0x800);
    BinaryPrimitives.WriteUInt32LittleEndian(wad[4..], 0x1800);
    BinaryPrimitives.WriteUInt32LittleEndian(wad[8..], 0x2000);
    BinaryPrimitives.WriteUInt32LittleEndian(wad[12..], 0x800);
    BinaryPrimitives.WriteInt32LittleEndian(wad[(int)(sourceTable + 0x0C)..], 777);
    BinaryPrimitives.WriteInt32LittleEndian(wad[(int)(relocatedTable + 0x0C)..], 100);
    string path = Path.Combine(root, "shifted.bin");
    File.WriteAllBytes(path, image);
    using FileStream stream = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
    DiscLayout layout = new(2048, 0, 0, 0);
    LevelDefinition level = new() { Key = "later", DisplayName = "Later level", LevelId = 11 };
    using JsonDocument edit = JsonDocument.Parse("{\"rawOriginal\":{\"x\":100},\"rawEdited\":{\"x\":116}}");
    List<MobySourcePatch> patches = [];
    bool blocked = false;
    try
    {
        StructuralExportCompositionPolicy.RequireCompatible([growth], [mobyWriter]);
        Invoke("AddCoordinatePatches", stream, layout, level, sourceTable, 0, "Synthetic Moby", edit.RootElement, patches, new HashSet<long>());
        Invoke("ApplySourcePatch", stream, layout, patches.Single());
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains("post-relocation composition contract", StringComparison.Ordinal)) { blocked = true; }
    stream.Flush();
    Check(blocked && patches.Count == 0 && File.ReadAllBytes(path).AsSpan().SequenceEqual(image),
        "Single private growth followed by the real Moby planner/writer modified stale WAD 0x182C (preimage 777) instead of rejecting the unrebased catalog address; relocated row 0x202C remains 100.");

    // A proved fixed-tail edit shifts level-data components, not the separate
    // scene subfile. Exercise the actual coordinate writer at its valid address.
    BinaryPrimitives.WriteInt32LittleEndian(image.AsSpan(37 * 2048 + (int)sourceTable + 0x0C), 100);
    string fixedPath = Path.Combine(root, "fixed-tail.bin");
    File.WriteAllBytes(fixedPath, image);
    using (FileStream fixedStream = File.Open(fixedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
    {
        StructuralExportCompositionPolicy.RequireCompatible([fixedTail], [mobyWriter]);
        Invoke("AddCoordinatePatches", fixedStream, layout, level, sourceTable, 0, "Synthetic Moby", edit.RootElement, patches, new HashSet<long>());
        Invoke("ApplySourcePatch", fixedStream, layout, patches.Single());
        fixedStream.Flush();
    }
    byte[] expectedFixed = image.ToArray();
    BinaryPrimitives.WriteInt32LittleEndian(expectedFixed.AsSpan(37 * 2048 + (int)sourceTable + 0x0C), 116);
    Check(File.ReadAllBytes(fixedPath).AsSpan().SequenceEqual(expectedFixed), "Known fixed-tail scene addresses no longer compose with Moby coordinates exactly.");

    Check(StructuralExportCompositionPolicy.FindBlocker([growth, fixedTail], [mobyWriter]) != null, "Mixed aggregate skipped the same composition boundary.");
    Check(StructuralExportCompositionPolicy.FindBlocker([growth, growth with { LevelKey = "second" }], [mobyWriter]) != null, "Two growing destinations skipped the composition boundary.");
    Check(StructuralExportCompositionPolicy.FindBlocker([fixedTail], [mobyWriter with { HasSkyboxEdit = true, HasLevelMusicEdit = true, HasLevelTextEdit = true }]) == null,
        "Previously supported fixed-tail composition was rejected despite preserved scene/archive extents.");
    StructuralSavedWriterScope folded = new("Private level", true, false, true, false, true, false, false, false);
    Check(StructuralExportCompositionPolicy.FindBlocker([growth], [folded]) == null, "Already composed terrain/native-texture writes were treated as pending unrebased writers.");
    foreach (StructuralSavedWriterScope writer in new[]
    {
        folded with { IsPrivateTextureDestination = false },
        folded with { HasObjectEdits = true }, folded with { HasCustomTerrainTextures = true },
        folded with { HasSkyboxEdit = true }, folded with { HasLevelTextEdit = true }, folded with { HasLevelMusicEdit = true }
    }) Check(StructuralExportCompositionPolicy.FindBlocker([growth], [writer]) != null, "An unverified pending writer escaped growing composition preflight.");
    Check(StructuralExportCompositionPolicy.FindBlocker([], [mobyWriter]) == null, "A nonstructural build was blocked.");
    ExpectInvalid(() => StructuralExportCompositionPolicy.FindBlocker([growth with { WadGrowthBytes = 0 }], []));
    ExpectInvalid(() => StructuralExportCompositionPolicy.FindBlocker([fixedTail with { WadGrowthBytes = 0x800 }], []));
    ExpectInvalid(() => StructuralExportCompositionPolicy.FindBlocker([growth with { WadGrowthBytes = -0x800 }], []));
    ExpectInvalid(() => StructuralExportCompositionPolicy.FindBlocker([growth with { Changes = (StructuralAddressChanges)128 }], []));
    ExpectInvalid(() => StructuralExportCompositionPolicy.FindBlocker([growth, growth], []));

    NativeTerrainTexturePrivateRecordBatchPlan fixedPlan = JsonSerializer.Deserialize<NativeTerrainTexturePrivateRecordBatchPlan>("""
        {"TargetLevelKey":"artisans","SourcePreimagesVerified":true,"ExclusiveImageWriterSelected":true,"WriterKind":0,
         "FixedTailPlan":{"FixedSubfileBoundaryPreserved":true,"Append":{"FixedSubfileBoundaryPreserved":true,
           "ArchiveHeadersRequireNoFixup":true,"ShiftedComponentChainReparsed":true,"Patch":{"SourceByteLength":2048,"OutputByteLength":2048}}}}
        """)!;
    StructuralExportAddressEffect proofEffect = StructuralExportAddressEffect.FromPrivateTexturePlan(fixedPlan);
    Check(proofEffect.Changes == StructuralAddressChanges.LevelDataComponents && proofEffect.WadGrowthBytes == 0,
        "Fixed-tail proof lost the distinction between component movement and native scene movement.");
    ExpectInvalid(() => StructuralExportAddressEffect.FromPrivateTexturePlan(fixedPlan with
        { FixedTailPlan = fixedPlan.FixedTailPlan! with { FixedSubfileBoundaryPreserved = false } }));
    NativeTerrainTexturePrivateRecordBatchPlan sectorPlan = JsonSerializer.Deserialize<NativeTerrainTexturePrivateRecordBatchPlan>("""
        {"TargetLevelKey":"wizardpeak","SourcePreimagesVerified":true,"ExclusiveImageWriterSelected":true,"WriterKind":1,
         "SectorRelocationPlan":{"StructuralSectorGrowthVerified":true,"Structural":{"SectorGrowthBytes":2048,"OriginalWadSize":8192,"ExpandedWadSize":10240}}}
        """)!;
    Check(StructuralExportAddressEffect.FromPrivateTexturePlan(sectorPlan).Changes == growth.Changes, "Sector relocation did not declare all moved address spaces.");
    ExpectInvalid(() => StructuralExportAddressEffect.FromPrivateTexturePlan(sectorPlan with
        { SectorRelocationPlan = sectorPlan.SectorRelocationPlan! with { StructuralSectorGrowthVerified = false } }));

    // Supplemental integration check: the real coordinator uses one preflight
    // before branching, not a tested helper that the single writer never calls.
    string repository = FindRepository();
    string source = File.ReadAllText(Path.Combine(repository, "src/Spyro.Editor.App/Views/MainWindow.cs"));
    int method = source.IndexOf("private async Task<CombinedTestBinResult> CreateAllSavedEditsTestAsync", StringComparison.Ordinal);
    string coordinator = source[method..source.IndexOf("foreach (EditedLevelExportTarget target in targets)", method, StringComparison.Ordinal)];
    int policy = coordinator.IndexOf("StructuralExportCompositionPolicy.RequireCompatible", StringComparison.Ordinal);
    int branch = coordinator.IndexOf("if (appendedPrivateTargets.Length == 1)", StringComparison.Ordinal);
    Check(policy >= 0 && branch > policy && coordinator.LastIndexOf("StructuralExportCompositionPolicy.RequireCompatible", StringComparison.Ordinal) == policy &&
        coordinator.IndexOf("NativeTerrainTexturePrivateRecordBatchCompiler.ExportAsync", StringComparison.Ordinal) > policy &&
        coordinator.IndexOf("NativeTerrainTexturePrivateMultiLevelBatchCompiler.ExportAsync", StringComparison.Ordinal) > policy &&
        !coordinator.Contains("ExportAppendedPrivateTerrainTextureStepAsync", StringComparison.Ordinal),
        "Single/multi private writer integration no longer shares preflight before image mutation.");
    MethodInfo adapter = typeof(MainWindow).GetMethod("FindGrowingPrivateTextureAggregateWriterBlocker", BindingFlags.Static | BindingFlags.NonPublic)!;
    Type scopeType = typeof(MainWindow).GetNestedType("PrivateTextureAggregateSavedWriterScope", BindingFlags.NonPublic)!;
    Array adapterScopes = Array.CreateInstance(scopeType, 1);
    adapterScopes.SetValue(Activator.CreateInstance(scopeType, "Later level", false, true, false, false, false, false, false, false), 0);
    Check(adapter.Invoke(null, [1, 0x800, adapterScopes]) is string singleBlock && singleBlock.Contains("object/path/chest edits", StringComparison.Ordinal),
        "Existing app adapter did not reject a growing single destination plus Moby edits.");
    Check(adapter.Invoke(null, [0, 0, adapterScopes]) == null, "Existing app adapter rejected same-size composition.");
    adapterScopes.SetValue(Activator.CreateInstance(scopeType, "Private level", true, false, true, false, true, false, false, false), 0);
    Check(adapter.Invoke(null, [2, 0x1000, adapterScopes]) == null, "Existing app adapter rejected already-folded aggregate texture writes.");
    Console.WriteLine($"STRUCTURAL_COMPOSITION_SMOKE_OK checks={checks}; real stale-address writer refused, proved fixed-tail scene composition retained, shared single/multi preflight");
}
finally { Directory.Delete(root, recursive: true); }

void Check(bool condition, string message) { checks++; Require(condition, message); }
void ExpectInvalid(Action action)
{
    checks++;
    try { action(); } catch (InvalidDataException) { return; }
    throw new InvalidOperationException("Malformed structural address proof was accepted.");
}

static string FindRepository()
{
    DirectoryInfo? directory = new(AppContext.BaseDirectory);
    while (directory != null && !File.Exists(Path.Combine(directory.FullName, "src/Spyro.Editor.App/Views/MainWindow.cs"))) directory = directory.Parent;
    return directory?.FullName ?? throw new InvalidOperationException("Repository source is required for the coordinator integration check.");
}

static object Invoke(string name, params object[] args)
{
    MethodInfo method = typeof(MobySourcePatchExporter).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!;
    try { return method.Invoke(null, args)!; }
    catch (TargetInvocationException ex) when (ex.InnerException != null) { ExceptionDispatchInfo.Capture(ex.InnerException).Throw(); throw; }
}
static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
