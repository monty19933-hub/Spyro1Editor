using Spyro.Editor.Core.Exporting;
using static Cases;

if (SavedLevelPublicationCases.IsMode(args)) { await SavedLevelPublicationCases.RunAsync(args); return; }
if (args.Length != 0 && args[0] is ("--tiled-family-candidate-free" or "--tiled-family-candidate-source" or "--tiled-family-candidate-physical"))
{
    bool source = args[0] != "--tiled-family-candidate-free";
    bool physical = args[0] == "--tiled-family-candidate-physical";
    if (args.Length != (physical ? 4 : source ? 3 : 1) || args.Skip(1).Any(v =>
        string.IsNullOrWhiteSpace(v) || v.StartsWith("--", StringComparison.Ordinal) || v.Contains('\0')))
        throw new ArgumentException("Usage: --tiled-family-candidate-free, or --tiled-family-candidate-source ORIGINAL_BIN ORIGINAL_CUE (read-only native), or --tiled-family-candidate-physical ORIGINAL_BIN ORIGINAL_CUE ABSOLUTE_NEW_ROOT (new isolated scratch artifacts only).");
    if (physical)
    {
        if (!Path.IsPathFullyQualified(args[3])) throw new ArgumentException("Physical evidence root must be absolute and new.");
        Root = Path.GetFullPath(args[3]);
        SavedLevelPublicationCases.Unlinked(Root); SavedLevelPublicationCases.Absent(Root);
        foreach (string sourcePath in new[] { args[1], args[2] })
            Spyro.Editor.Core.Persistence.PersistencePathSafety.EnsureTreesDoNotOverlap(
                Path.GetFullPath(sourcePath), Root, "Physical evidence root overlaps a source owner.");
        Directory.CreateDirectory(Root);
    }
    else
    {
        Root = Directory.CreateTempSubdirectory("spyro-saved-tiled-family-candidate-").FullName;
        // Resolve only observed links in our newly created temporary owner;
        // the physical publication helper's unlinked-path guard stays strict.
        for (var parent = Directory.GetParent(Root); parent != null; parent = parent.Parent)
            if (parent.LinkTarget != null)
                Root = Path.Combine(parent.ResolveLinkTarget(true)!.FullName, Path.GetRelativePath(parent.FullName, Root));
    }
    try
    {
        await TiledFamilySavedCandidateCases.FreeAsync();
        if (source)
        {
            var owners = new NativeFreshResearchSourceOwners(Path.GetFullPath(args[1]), Path.GetFullPath(args[2]));
            Exception? primary = null;
            try
            {
                owners.Open(CancellationToken.None);
                await NativeId65FreshDiscSource.RequireWholeOriginalAsync(owners.Bin!, CancellationToken.None);
                if (physical) await TiledFamilySavedCandidateCases.PhysicalAsync(owners.ImagePath, owners.CuePath);
                else await TiledFamilySavedCandidateCases.SourceAsync(owners.ImagePath, owners.CuePath);
            }
            catch (Exception error) { primary = error; }
            owners.Complete(primary);
        }
        Console.WriteLine($"NATIVE_FRESH_SAVED_TILED_FAMILY_CANDIDATE_FINAL_OK checks={Checks} refusals={Refusals} source={source} physical={physical} nativeImageWritten={physical}");
    }
    finally { Console.Error.WriteLine("Retained new tiny tiled Candidate fixtures: " + Root); }
    return;
}
if (args.Length != 0 && args[0] is ("--tiled-family-compilation-free" or "--tiled-family-compilation-source"))
{
    bool source = args[0] == "--tiled-family-compilation-source";
    if (args.Length != (source ? 3 : 1) || args.Skip(1).Any(v =>
        string.IsNullOrWhiteSpace(v) || v.StartsWith("--", StringComparison.Ordinal) || v.Contains('\0')))
        throw new ArgumentException("Usage: --tiled-family-compilation-free, or --tiled-family-compilation-source ORIGINAL_BIN ORIGINAL_CUE; no native outputs.");
    Root = Directory.CreateTempSubdirectory("spyro-saved-tiled-family-source-").FullName;
    try
    {
        await SavedTiledFamilyCompilationCases.FreeAsync();
        if (source)
        {
            var owners = new NativeFreshResearchSourceOwners(Path.GetFullPath(args[1]), Path.GetFullPath(args[2]));
            Exception? primary = null;
            try
            {
                owners.Open(CancellationToken.None);
                await NativeId65FreshDiscSource.RequireWholeOriginalAsync(owners.Bin!, CancellationToken.None);
                await SavedTiledFamilyCompilationCases.SourceAsync(owners.ImagePath, owners.CuePath);
            }
            catch (Exception error) { primary = error; }
            owners.Complete(primary);
        }
        Console.WriteLine($"NATIVE_FRESH_SAVED_TILED_FAMILY_COMPILATION_FINAL_OK checks={Checks} refusals={Refusals} source={source} nativeImageWritten=false");
    }
    finally { Console.Error.WriteLine("Retained new tiny tiled family fixtures: " + Root); }
    return;
}
if (args.Length != 0 && args[0] == "--locked-chest-source")
{
    if (args.Length is not (3 or 4) || args.Skip(1).Any(v =>
        string.IsNullOrWhiteSpace(v) || v.StartsWith("--", StringComparison.Ordinal) || v.Contains('\0')))
        throw new ArgumentException("Usage: --locked-chest-source ORIGINAL_BIN ORIGINAL_CUE [EXISTING_EXACT_CANDIDATE_BIN]; read-only native outputs.");
    Root = Directory.CreateTempSubdirectory("spyro-saved-chest-source-").FullName;
    var owners = new NativeFreshResearchSourceOwners(Path.GetFullPath(args[1]), Path.GetFullPath(args[2]));
    Exception? primary = null;
    try
    {
        await SavedLockedChestCompilationCases.FreeAsync();
        await LockedChestSavedCandidateCases.FreeAsync();
        await SavedLockedChestInverseCases.FreeAsync();
        owners.Open(CancellationToken.None);
        await NativeId65FreshDiscSource.RequireWholeOriginalAsync(owners.Bin!, CancellationToken.None);
        await LockedChestSavedCandidateCases.SourceAsync(owners.ImagePath, owners.CuePath, args.Length == 4 ? args[3] : null);
    }
    catch (Exception error) { primary = error; }
    finally { Console.Error.WriteLine("Retained new tiny chest source fixtures: " + Root); }
    owners.Complete(primary);
    Console.WriteLine($"NATIVE_FRESH_SAVED_CHEST_SOURCE_FINAL_OK checks={Checks} refusals={Refusals} originalFinal=true nativeImageWritten=false");
    return;
}
if (args.Length != 0 && args[0] is ("--selected-tiled-material-saved-free" or "--selected-tiled-material-saved-source"))
{
    bool source = args[0] == "--selected-tiled-material-saved-source";
    if (args.Length != (source ? 3 : 1) || args.Skip(1).Any(v =>
        string.IsNullOrWhiteSpace(v) || v.StartsWith("--", StringComparison.Ordinal) || v.Contains('\0')))
        throw new ArgumentException("Usage: --selected-tiled-material-saved-free, or --selected-tiled-material-saved-source original.bin original.cue");
    Root = Directory.CreateTempSubdirectory("spyro-selected-tiled-saved-smoke-").FullName;
    try
    {
        await SelectedTiledMaterialSavedCases.Free();
        if (source) await SelectedTiledMaterialSavedCases.Source(args[1], args[2]);
    }
    finally { Console.Error.WriteLine("Retained new test-owned tiny selected fixtures: " + Root); }
    return;
}
if (args.Length != 0 && (args.Length != 3 || args[0] is not ("--source" or "--mixed-source" or "--family-source" or "--tiled-source" or "--tiled-material-source") || args.Skip(1).Any(v =>
    string.IsNullOrWhiteSpace(v) || v.StartsWith("--", StringComparison.Ordinal) || v.Contains('\0'))))
    throw new ArgumentException("Usage: NativeFreshSavedLevelResearchSmoke [--source|--mixed-source|--family-source|--tiled-source|--tiled-material-source original.bin original.cue], or an explicit reviewed saved-level mode with BIN CUE ABSOLUTE_PROJECT_ROOT PROJECT_ID ABSOLUTE_OUTPUT (six tokens including mode).");
Root = Directory.CreateTempSubdirectory("spyro-saved-level-research-smoke-").FullName;
try
{
    await CandidateCases.Free(); await SourceOwnerCases.Free();
    await SavedLevelPhysicalApiCases.Free(); await SavedLevelPublicationArgumentCases.Free();
    await LeaseOutcomeCases.Free(); await ActualAttemptCases.Free();
    await MixedSavedCases.Free(); await FamilySavedCases.Free(); await TiledSavedCases.Free(); await TiledMaterialSavedCases.Free();
    await NeutralFamilyAdmissionCases.Free();
    await SavedLockedChestCompilationCases.FreeAsync();
    await SavedTiledFamilyCompilationCases.FreeAsync();
    await TiledFamilySavedCandidateCases.FreeAsync();
    await LockedChestSavedCandidateCases.FreeAsync(); await SavedLockedChestInverseCases.FreeAsync();
    Console.WriteLine($"NATIVE_FRESH_SAVED_LEVEL_RESEARCH_FREE_OK checks={Checks} refusals={Refusals} originalOpened=false nativeImageWritten=false tinyTransactionInvoked=true");
    if (args.Length != 0)
    {
        var owners = new NativeFreshResearchSourceOwners(Path.GetFullPath(args[1]), Path.GetFullPath(args[2]));
        Exception? primary = null;
        try
        {
            owners.Open(CancellationToken.None);
            await NativeId65FreshDiscSource.RequireWholeOriginalAsync(owners.Bin!, CancellationToken.None);
            if (args[0] == "--tiled-material-source") await TiledMaterialSavedCases.Source(owners.ImagePath, owners.CuePath);
            else if (args[0] == "--tiled-source") await TiledSavedCases.Source(owners.ImagePath, owners.CuePath);
            else if (args[0] == "--family-source") await FamilySavedCases.Source(owners.ImagePath, owners.CuePath);
            else if (args[0] == "--mixed-source") await MixedSavedCases.Source(owners.ImagePath, owners.CuePath);
            else await CandidateCases.Source(owners.ImagePath, owners.CuePath);
        }
        catch (Exception error) { primary = error; }
        owners.Complete(primary); // Both held/current BIN+CUE, EOF and all acquired closes.
        Console.WriteLine($"NATIVE_FRESH_SAVED_LEVEL_RESEARCH_SOURCE_OK checks={Checks} refusals={Refusals} originalFinal=true nativeImageWritten=false");
    }
}
finally { Console.Error.WriteLine("Retained new test-owned tiny fixtures: " + Root); }
