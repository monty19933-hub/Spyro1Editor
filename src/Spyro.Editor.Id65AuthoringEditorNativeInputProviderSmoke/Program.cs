using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

const string SmokeRootPrefix =
    "Spyro.Editor.Id65AuthoringEditorNativeInputProviderSmoke-";

string repositoryRoot = FindRepositoryRoot(args.ElementAtOrDefault(0));
string sourceImagePath = Path.Combine(
    repositoryRoot,
    "_local",
    "v5-stone-hill-level-replacement",
    "unused-level-65-display-name",
    "Unused-Level-65-Town-Square-independent-storage-with-Town-Square-display-name-RUNTIME-CANDIDATE.bin");
string sourceCuePath = Path.ChangeExtension(sourceImagePath, ".cue");
Require(File.Exists(sourceImagePath) && File.Exists(sourceCuePath),
    "The provider smoke is missing the exact locked-base BIN/CUE fixture.");
FileState sourceImageBefore = SnapshotFile(sourceImagePath);
FileState sourceCueBefore = SnapshotFile(sourceCuePath);

string smokeRoot = Path.Combine(
    Path.GetTempPath(),
    SmokeRootPrefix + Guid.NewGuid().ToString("N"));
string workspaceContainer = Path.Combine(smokeRoot, "workspace-container");
Directory.CreateDirectory(smokeRoot);

int positiveLoads = 0;
int failClosedRejects = 0;

try
{
    UnusedLevel65BlankLevelLabWorkspacePaths paths =
        UnusedLevel65BlankLevelLabProfileRegistry.CreateWorkspacePaths(
            workspaceContainer);
    Directory.CreateDirectory(paths.LockedBaseDirectoryPath);
    Directory.CreateDirectory(paths.AuthoredEditsDirectoryPath);
    File.Copy(sourceImagePath, paths.LockedBaseImagePath, overwrite: false);
    string cue = DiscImage.BuildCueText(
        sourceCuePath,
        Path.GetFileName(paths.LockedBaseImagePath));
    await File.WriteAllTextAsync(paths.LockedBaseCuePath, cue, Encoding.ASCII);
    UnusedLevel65BlankLevelLabManifest manifest =
        UnusedLevel65BlankLevelLabProfileRegistry.CreateManifest(paths);
    await File.WriteAllTextAsync(
        paths.ManifestPath,
        JsonSerializer.Serialize(manifest, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        }) + "\n",
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    Require(
        await UnusedLevel65BlankLevelLabBootstrapper
            .ValidatePublishedWorkspaceReadOnlyAsync(paths) == manifest,
        "The provider smoke workspace did not validate exactly.");

    FileState labImageBefore = SnapshotFile(paths.LockedBaseImagePath);
    FileState labCueBefore = SnapshotFile(paths.LockedBaseCuePath);
    FileState labManifestBefore = SnapshotFile(paths.ManifestPath);
    IReadOnlyList<string> labEntriesBefore = SnapshotDirectory(paths.RootPath);

    Id65AuthoringEditorNativeInputProvider.ResetCacheForSmoke();
    Task<Id65AuthoringEditorNativeInputProvider.InputSet> firstTask =
        Id65AuthoringEditorNativeInputProvider.LoadAsync(paths, manifest);
    Task<Id65AuthoringEditorNativeInputProvider.InputSet> secondTask =
        Id65AuthoringEditorNativeInputProvider.LoadAsync(paths, manifest);
    Id65AuthoringEditorNativeInputProvider.InputSet[] concurrent =
        await Task.WhenAll(firstTask, secondTask);
    positiveLoads += concurrent.Length;
    Id65AuthoringEditorNativeInputProvider.InputSet first = concurrent[0];
    Id65AuthoringEditorNativeInputProvider.InputSet second = concurrent[1];
    Require(
        first.CanonicalInputSetSha256 == second.CanonicalInputSetSha256 &&
        first.CanonicalInputSetSha256 ==
            Id65AuthoringEditorNativeInputProvider.ExpectedCanonicalInputSetSha256 &&
        first.LockedBaseImageSha256 == Id65RewardCensusContract.LockedSourceImageSha256 &&
        first.LockedRow80Sha256 == Id65AuthoringCompositeCompiler.ExpectedLockedRow80Sha256 &&
        first.PrivateTextureWitnessSha256 ==
            Id65V2NativeTextureCompositionCompiler.ExpectedWitnessSha256 &&
        first.GrassBundleSha256 ==
            Id65AuthoringMobyDependencyBundleCompiler.ExpectedArtisansGrassBundleSha256,
        "Concurrent provider loads lost an exact source, witness, bundle, or aggregate identity.");
    Id65AuthoringEditorNativeInputProvider.ProviderCacheSnapshot concurrentCache =
        Id65AuthoringEditorNativeInputProvider.CaptureCacheSnapshotForSmoke();
    Require(
        concurrentCache.EntryCount == 1 &&
        concurrentCache.DerivedPairBuildCount == 1 &&
        concurrentCache.CacheKeys.Count == 1,
        "Concurrent provider loads did not fan in to one bounded native-pair build.");

    Id65AuthoringEditorProjection projection =
        Id65AuthoringEditorDocumentProjector.Project(new(
            Id65AuthoringEditorDocument.CreateExact(),
            first.LockedSource,
            first.PrivateTextureWitness,
            first.GrassBundle));
    Require(
        projection.DocumentIdentityVerified &&
        projection.SemanticReadbackVerified &&
        projection.Compiled.OutputRow80Sha256 ==
            Id65AuthoringCompositeCompiler.ExpectedCombinedOutputRow80Sha256 &&
        projection.Compiled.DeterministicPlanSha256 ==
            Id65AuthoringCompositeCompiler.ExpectedCombinedDeterministicPlanSha256,
        "The project-derived native input set did not reproduce the exact full document projection.");

    using (CancellationTokenSource canceled = new())
    {
        canceled.Cancel();
        await ExpectCanceledAsync(
            "pre-cancelled provider load",
            async () => _ = await Id65AuthoringEditorNativeInputProvider.LoadAsync(
                paths,
                manifest,
                canceled.Token));
    }

    Id65AuthoringEditorNativeInputProvider.ResetCacheForSmoke();
    using (CancellationTokenSource abandonedWaiter = new())
    using (ManualResetEventSlim buildEntered = new(initialState: false))
    using (ManualResetEventSlim releaseBuild = new(initialState: false))
    {
        TaskCompletionSource<bool> failedEntryEvicted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        Task<Id65AuthoringEditorNativeInputProvider.InputSet> abandoned =
            Id65AuthoringEditorNativeInputProvider.LoadForSmokeAsync(
                paths,
                manifest,
                stage =>
                {
                    if (stage == "before-native-input-derivation")
                    {
                        buildEntered.Set();
                        if (!releaseBuild.Wait(TimeSpan.FromSeconds(20)))
                            throw new TimeoutException("The injected provider build was not released.");
                        throw new InvalidDataException("Injected provider derivation failure after waiter cancellation.");
                    }
                    if (stage == "after-failed-native-input-cache-eviction")
                        failedEntryEvicted.TrySetResult(true);
                },
                abandonedWaiter.Token);
        try
        {
            Require(buildEntered.Wait(TimeSpan.FromSeconds(20)),
                "The provider smoke did not reach its in-flight native derivation.");
            Id65AuthoringEditorNativeInputProvider.ProviderCacheSnapshot inFlight =
                Id65AuthoringEditorNativeInputProvider.CaptureCacheSnapshotForSmoke();
            Require(inFlight.EntryCount == 1 && inFlight.DerivedPairBuildCount == 1,
                "The injected in-flight derivation was not represented by one cache entry.");
            abandonedWaiter.Cancel();
            await ExpectCanceledAsync("in-flight sole provider waiter", async () => _ = await abandoned);
            releaseBuild.Set();
            await failedEntryEvicted.Task.WaitAsync(TimeSpan.FromSeconds(20));
            Require(
                Id65AuthoringEditorNativeInputProvider.CaptureCacheSnapshotForSmoke().EntryCount == 0,
                "A later-faulted derivation survived after its sole waiter was canceled.");
            failClosedRejects++;
        }
        finally
        {
            releaseBuild.Set();
        }
    }

    byte[] validManifest = await File.ReadAllBytesAsync(paths.ManifestPath);
    string forgedManifestText = Encoding.UTF8.GetString(validManifest)
        .Replace("\"profileVersion\": 1", "\"profileVersion\": 999", StringComparison.Ordinal);
    Require(!forgedManifestText.SequenceEqual(Encoding.UTF8.GetString(validManifest)),
        "The provider smoke could not forge its manifest profile version.");
    await File.WriteAllTextAsync(
        paths.ManifestPath,
        forgedManifestText,
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    await ExpectRejectedAsync(
        "forged manifest",
        async () => _ = await Id65AuthoringEditorNativeInputProvider.LoadAsync(paths, manifest),
        "manifest");
    failClosedRejects++;
    await File.WriteAllBytesAsync(paths.ManifestPath, validManifest);
    File.SetLastWriteTimeUtc(paths.ManifestPath, labManifestBefore.LastWriteUtc);

    string imageBackup = paths.LockedBaseImagePath + ".smoke-backup";
    File.Move(paths.LockedBaseImagePath, imageBackup);
    try
    {
        File.CreateSymbolicLink(paths.LockedBaseImagePath, imageBackup);
        await ExpectRejectedAsync(
            "locked BIN symlink",
            async () => _ = await Id65AuthoringEditorNativeInputProvider.LoadAsync(paths, manifest),
            "symbolic link");
        failClosedRejects++;
    }
    finally
    {
        if (File.Exists(paths.LockedBaseImagePath) ||
            new FileInfo(paths.LockedBaseImagePath).LinkTarget != null)
        {
            File.Delete(paths.LockedBaseImagePath);
        }
        File.Move(imageBackup, paths.LockedBaseImagePath);
    }

    Id65AuthoringEditorNativeInputProvider.ResetCacheForSmoke();
    DateTime imageWriteBeforeFault = File.GetLastWriteTimeUtc(paths.LockedBaseImagePath);
    byte originalByte = ReadByte(paths.LockedBaseImagePath, 0x1000);
    try
    {
        await ExpectRejectedAsync(
            "in-call locked BIN mutation",
            async () => _ = await Id65AuthoringEditorNativeInputProvider.LoadForSmokeAsync(
                paths,
                manifest,
                stage =>
                {
                    if (stage == "after-initial-workspace-capture")
                        WriteByte(paths.LockedBaseImagePath, 0x1000, (byte)(originalByte ^ 0x01));
                }),
            "SHA-256");
        failClosedRejects++;
    }
    finally
    {
        WriteByte(paths.LockedBaseImagePath, 0x1000, originalByte);
        File.SetLastWriteTimeUtc(paths.LockedBaseImagePath, imageWriteBeforeFault);
    }
    Require(
        Id65AuthoringEditorNativeInputProvider.CaptureCacheSnapshotForSmoke().EntryCount == 0,
        "A failed locked-source derivation poisoned the process-local pair cache.");

    Id65AuthoringEditorNativeInputProvider.InputSet recovered =
        await Id65AuthoringEditorNativeInputProvider.LoadAsync(paths, manifest);
    positiveLoads++;
    Id65AuthoringEditorNativeInputProvider.ProviderCacheSnapshot recoveredCache =
        Id65AuthoringEditorNativeInputProvider.CaptureCacheSnapshotForSmoke();
    Require(
        recovered.CanonicalInputSetSha256 == first.CanonicalInputSetSha256 &&
        recoveredCache.EntryCount == 1 &&
        recoveredCache.DerivedPairBuildCount == 2,
        "The provider did not recover deterministically after evicting a failed derivation.");

    AssertTopology(repositoryRoot);
    Require(
        SnapshotFile(paths.LockedBaseImagePath) == labImageBefore &&
        SnapshotFile(paths.LockedBaseCuePath) == labCueBefore &&
        SnapshotFile(paths.ManifestPath) == labManifestBefore &&
        SnapshotDirectory(paths.RootPath).SequenceEqual(labEntriesBefore),
        "The read-only provider changed a lab BIN, CUE, manifest, or workspace entry.");
    Require(
        SnapshotFile(sourceImagePath) == sourceImageBefore &&
        SnapshotFile(sourceCuePath) == sourceCueBefore,
        "The provider changed its external locked-base fixture.");

    Console.WriteLine(
        $"PASS Id65AuthoringEditorNativeInputProviderSmoke: source={recovered.LockedBaseImageSha256}, " +
        $"witness={recovered.PrivateTextureWitnessSha256}, bundle={recovered.GrassBundleSha256}, " +
        $"inputSet={recovered.CanonicalInputSetSha256}, positiveLoads={positiveLoads}, " +
        $"failClosedRejects={failClosedRejects}, cacheBuilds={recoveredCache.DerivedPairBuildCount}. " +
        "Exact locked-project derivation, concurrent fan-in, failed-build eviction/recovery, semantic projection, path-free topology, immutable workspace, and App/Create-BIN/runtime/release false boundaries passed without a retired foundation path.");
}
finally
{
    Id65AuthoringEditorNativeInputProvider.ResetCacheForSmoke();
    if (Directory.Exists(smokeRoot) &&
        Path.GetFileName(smokeRoot).StartsWith(SmokeRootPrefix, StringComparison.Ordinal))
    {
        Directory.Delete(smokeRoot, recursive: true);
    }
    Require(!Directory.Exists(smokeRoot),
        "The provider smoke retained temporary game data.");
}

static void AssertTopology(string repositoryRoot)
{
    Type provider = typeof(Id65AuthoringEditorNativeInputProvider);
    Type result = typeof(Id65AuthoringEditorNativeInputProvider.InputSet);
    Type implementation = provider.GetNestedType("ProviderInputSet", BindingFlags.NonPublic)
        ?? throw new InvalidOperationException(
            "The private provider-owned native-input implementation is missing.");
    Require(!provider.IsPublic && result.IsNestedAssembly && result.IsAbstract &&
            implementation.IsNestedPrivate && implementation.IsSealed &&
            implementation.BaseType == result,
        "The native-input provider/result escaped the internal Core boundary.");
    ConstructorInfo[] constructors = result.GetConstructors(
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    ConstructorInfo[] implementationConstructors = implementation.GetConstructors(
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    Require(constructors.Length == 1 && constructors[0].IsFamilyAndAssembly &&
            implementationConstructors.Length == 1 && implementationConstructors[0].IsAssembly &&
            !result.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.DeclaredOnly).Any(method => method.ReturnType == result),
        "The native-input shell can be directly constructed or exposes a forgeable factory.");
    MethodInfo[] loads = provider.GetMethods(
            BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly)
        .Where(method => method.Name == "LoadAsync")
        .ToArray();
    Require(loads.Length == 1 && loads[0].GetParameters().All(parameter =>
            parameter.ParameterType != typeof(string) &&
            parameter.ParameterType != typeof(byte[]) &&
            !typeof(Stream).IsAssignableFrom(parameter.ParameterType) &&
            !typeof(Delegate).IsAssignableFrom(parameter.ParameterType)),
        "The production provider exposes an arbitrary path, byte, stream, delegate, or duplicate load surface.");
    Require(result.GetProperties(BindingFlags.Instance | BindingFlags.Public).All(property =>
            !(property.PropertyType == typeof(string) &&
              property.Name.Contains("Path", StringComparison.OrdinalIgnoreCase)) &&
            property.PropertyType != typeof(byte[]) &&
            !typeof(Stream).IsAssignableFrom(property.PropertyType)),
        "The native-input result exposes a path or native byte payload.");
    PropertyInfo[] materializerRequest =
        typeof(Id65AuthoringEditorAggregateCandidateMaterializationRequest)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public);
    Require(materializerRequest.All(property =>
            property.PropertyType != typeof(Id65V2NativeTextureWitness) &&
            property.PropertyType != typeof(Id65MobyDependencyBundleDescriptor)),
        "The materializer request still permits independently mixed witness/bundle generations.");

    Require(
        HasExactPublicProperties(
            typeof(Id65AuthoringEditorDocumentProjectionInputs),
            ("NativeInputs", result),
            ("Limits", typeof(Id65AuthoringCompositeCompilerLimits))) &&
        HasExactPublicProperties(
            typeof(Id65AuthoringEditorAggregatePlanRequest),
            ("StoredDocument", typeof(Id65AuthoringEditorDocumentStoreResult)),
            ("LabManifest", typeof(UnusedLevel65BlankLevelLabManifest)),
            ("NativeInputs", result)),
        "A document-store or aggregate request regained independently mixed native inputs.");

    string coreRoot = Path.Combine(repositoryRoot, "src", "Spyro.Editor.Core");
    string appRoot = Path.Combine(repositoryRoot, "src", "Spyro.Editor.App");
    string[] productionFiles = new[] { coreRoot, appRoot }
        .SelectMany(root => Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        .Where(path => !HasPathSegment(path, "bin") && !HasPathSegment(path, "obj"))
        .ToArray();
    string[] providerOwners = productionFiles
        .Where(path => File.ReadAllText(path).Contains(
            "Id65AuthoringEditorNativeInputProvider",
            StringComparison.Ordinal))
        .Select(path => Path.GetRelativePath(repositoryRoot, path))
        .Order(StringComparer.Ordinal)
        .ToArray();
    string[] expectedProviderOwners =
    [
        Path.Combine("src", "Spyro.Editor.Core", "Persistence",
            "Id65AuthoringEditorAggregateCandidateMaterializer.cs"),
        Path.Combine("src", "Spyro.Editor.Core", "Persistence",
            "Id65AuthoringEditorAggregatePlanProjector.cs"),
        Path.Combine("src", "Spyro.Editor.Core", "Persistence",
            "Id65AuthoringEditorDocumentStore.cs"),
        Path.Combine("src", "Spyro.Editor.Core", "Persistence",
            "Id65AuthoringEditorDocumentWorkspaceWriter.cs"),
        Path.Combine("src", "Spyro.Editor.Core", "Persistence",
            "Id65AuthoringEditorNativeInputProvider.cs")
    ];
    Regex loadCall = new(
        @"\bId65AuthoringEditorNativeInputProvider\s*\.\s*LoadAsync\s*\(",
        RegexOptions.CultureInvariant);
    string[] productionLoadOwners = productionFiles
        .Where(path => loadCall.IsMatch(File.ReadAllText(path)))
        .Select(path => Path.GetRelativePath(repositoryRoot, path))
        .Order(StringComparer.Ordinal)
        .ToArray();
    Require(
        providerOwners.SequenceEqual(expectedProviderOwners.Order(StringComparer.Ordinal)) &&
        productionLoadOwners.SequenceEqual(
        [
            Path.Combine("src", "Spyro.Editor.Core", "Persistence",
                "Id65AuthoringEditorAggregateCandidateMaterializer.cs"),
            Path.Combine("src", "Spyro.Editor.Core", "Persistence",
                "Id65AuthoringEditorDocumentWorkspaceWriter.cs")
        ]) &&
        !providerOwners.Any(path => HasPathSegment(path, "Spyro.Editor.App")),
        "The provider callsite topology changed or the cheap App inspector became a native-input loader.");

}

static bool HasExactPublicProperties(Type type, params (string Name, Type Type)[] expected)
{
    PropertyInfo[] actual = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
    return actual.Length == expected.Length && expected.All(item =>
        actual.Count(property =>
            property.Name == item.Name && property.PropertyType == item.Type) == 1);
}

static bool HasPathSegment(string path, string segment) =>
    path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        .Contains(segment, StringComparer.OrdinalIgnoreCase);

static byte ReadByte(string path, long offset)
{
    using FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    stream.Position = offset;
    int value = stream.ReadByte();
    if (value < 0)
        throw new EndOfStreamException("The provider smoke mutation offset escaped the BIN.");
    return (byte)value;
}

static void WriteByte(string path, long offset, byte value)
{
    using FileStream stream = File.Open(path, FileMode.Open, FileAccess.Write, FileShare.None);
    stream.Position = offset;
    stream.WriteByte(value);
    stream.Flush(flushToDisk: true);
}

static async Task ExpectRejectedAsync(
    string label,
    Func<Task> action,
    string? requiredText = null)
{
    try
    {
        await action();
        throw new InvalidOperationException($"Expected {label} to be rejected.");
    }
    catch (Exception exception) when (exception is not InvalidOperationException ||
                                      !exception.Message.StartsWith("Expected ", StringComparison.Ordinal))
    {
        if (requiredText != null &&
            !exception.ToString().Contains(requiredText, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The {label} rejection did not mention '{requiredText}': {exception}");
        }
    }
}

static async Task ExpectCanceledAsync(string label, Func<Task> action)
{
    try
    {
        await action();
        throw new InvalidOperationException($"Expected {label} to be canceled.");
    }
    catch (OperationCanceledException)
    {
    }
}

static IReadOnlyList<string> SnapshotDirectory(string root) =>
    Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories)
        .Select(path => Path.GetRelativePath(root, path))
        .Order(StringComparer.Ordinal)
        .ToArray();

static FileState SnapshotFile(string path)
{
    FileInfo file = new(path);
    using FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    return new(file.Length, file.LastWriteTimeUtc, Convert.ToHexStringLower(SHA256.HashData(stream)));
}

static string FindRepositoryRoot(string? explicitRoot)
{
    if (!string.IsNullOrWhiteSpace(explicitRoot))
        return Path.GetFullPath(explicitRoot);
    DirectoryInfo? current = new(AppContext.BaseDirectory);
    while (current != null)
    {
        if (File.Exists(Path.Combine(current.FullName, "src", "Spyro.Editor.Core", "Spyro.Editor.Core.csproj")))
            return current.FullName;
        current = current.Parent;
    }
    throw new DirectoryNotFoundException("Could not locate the Spyro editor repository root.");
}

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

internal readonly record struct FileState(long ByteLength, DateTime LastWriteUtc, string Sha256);
