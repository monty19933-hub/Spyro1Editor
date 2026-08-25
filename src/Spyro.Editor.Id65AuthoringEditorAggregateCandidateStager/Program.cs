using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

return await AggregateCandidateStager.RunAsync(args);

internal static class AggregateCandidateStager
{
    private const string ExpectedDocumentCanonicalSha256 =
        "8f27bfc3f97238402c0b7dd59a081335ae19df1acfaefa9575d9ca7110dce808";
    private const string ExpectedAggregatePlanSha256 =
        "4955c54b3eb7ea5c1871205e559f5d5dda0766613e55aa0de988b92e896d5c21";
    private const string ExpectedOutputRow80Sha256 =
        "dad25853f92e4f8300036edd85dae374672d265ba17c7b2682c6b524661b75f3";
    private const string ExpectedOutputImageSha256 =
        "083dd5b7e258b59010e87c3262274e4da9cb86b1a6a15e7730a8b5eeac001647";
    private const string ExpectedCandidateCueSha256 =
        "91c12af236642fc8ad601995cea86d22c414eb1b692a4957cc3f9fa3aedd6de8";
    private const string ExpectedReceiptContentSha256 =
        "d6c14861cde023e9e100fe2828227da8252c9de37c36e84d33ba4bfbd3b0516f";
    private const int ExpectedChangedRawSectorCount = 558;
    private const long ExpectedChangedPhysicalImageBytes = 1_075_276;
    private const string SuccessMarker =
        "ID65_AUTHORING_EDITOR_AGGREGATE_CANDIDATE_STAGED";
    private const string RefusalMarker =
        "ID65_AUTHORING_EDITOR_AGGREGATE_CANDIDATE_STAGER_REFUSED";

    internal static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 1 &&
            string.Equals(args[0], "--help", StringComparison.Ordinal))
        {
            PrintUsage(Console.Out);
            return 0;
        }
        if (!TryParse(args, out Options? options, out string usageError) ||
            options == null)
        {
            Console.Error.WriteLine($"{RefusalMarker} kind=usage message={usageError}");
            PrintUsage(Console.Error);
            return 2;
        }

        using CancellationTokenSource cancellation = new();
        ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };
        Console.CancelKeyPress += cancelHandler;
        try
        {
            await StageAsync(options, cancellation.Token);
            return 0;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine(
                $"{RefusalMarker} kind=cancelled message=staging was cancelled");
            return 130;
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidDataException or
                IOException or UnauthorizedAccessException or
                JsonException or PlatformNotSupportedException or
                NotSupportedException)
        {
            Console.Error.WriteLine(
                $"{RefusalMarker} kind=safety message={OneLine(exception.Message)}");
            return 3;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                $"{RefusalMarker} kind=unexpected type={exception.GetType().Name} " +
                $"message={OneLine(exception.Message)}");
            return 1;
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
        }
    }

    private static async Task StageAsync(
        Options options,
        CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsMacOS())
        {
            throw new PlatformNotSupportedException(
                "Exact aggregate candidate staging is currently macOS-only because atomic directory publication is Darwin-anchored.");
        }

        Id65AuthoringEditorControlState defaultState =
            Id65AuthoringEditorControlState.CreateDefault();
        Id65AuthoringEditorControlStateCompilation compiledDefault =
            Id65AuthoringEditorControlStateCompiler.Compile(defaultState);
        Require(
            compiledDefault.DocumentCanonicalSha256 ==
                ExpectedDocumentCanonicalSha256,
            "The frozen exact default authoring document changed.");

        string sourceImagePath = RequireAbsoluteExistingFile(
            options.SourceImagePath,
            "source BIN");
        string sourceCuePath = RequireAbsoluteExistingFile(
            options.SourceCuePath,
            "source CUE");
        string workspaceContainerPath = RequireSafeWorkspaceContainer(
            options.WorkspaceContainerPath,
            sourceImagePath,
            sourceCuePath);
        UnusedLevel65BlankLevelLabWorkspacePaths plannedPaths =
            UnusedLevel65BlankLevelLabProfileRegistry.CreateWorkspacePaths(
                workspaceContainerPath);
        RequireSafeWorkspaceTree(workspaceContainerPath, plannedPaths);
        bool bootstrapRecoveryMutationPerformedThisCall =
            Directory.Exists(plannedPaths.OperationsDirectoryPath);
        cancellationToken.ThrowIfCancellationRequested();

        UnusedLevel65BlankLevelLabBootstrapResult bootstrap =
            await UnusedLevel65BlankLevelLabBootstrapper.BootstrapAsync(
                new(
                    sourceImagePath,
                    sourceCuePath,
                    workspaceContainerPath,
                    ReplaceInvalidExistingWorkspace: false),
                cancellationToken);
        Require(
            bootstrap.SourceImageSha256 ==
                UnusedLevel65BlankLevelLabProfileRegistry.Profile
                    .CleanUsaImageSha256 &&
            bootstrap.LockedBaseImageSha256 ==
                UnusedLevel65BlankLevelLabProfileRegistry.Profile
                    .LockedBaseImageSha256 &&
            bootstrap.SourceImagePreserved &&
            bootstrap.AtomicPublicationCompleted &&
            bootstrap.OwnedTemporaryIntermediatesRemoved,
            "The exact ID65 workspace bootstrap lost a source, hash, atomicity, or cleanup boundary.");

        UnusedLevel65BlankLevelLabWorkspacePaths paths = bootstrap.Paths;
        RequireExactWorkspaceLayout(workspaceContainerPath, paths);
        bool bootstrapPermissionsHardenedThisCall = false;
        if (!bootstrap.ReusedExistingLockedBase)
        {
            bootstrapPermissionsHardenedThisCall =
                HardenNewBootstrapFilesForStaging(paths);
        }
        string postBootstrapContainerPath = RequireSafeWorkspaceContainer(
            workspaceContainerPath,
            sourceImagePath,
            sourceCuePath);
        Require(
            PathsEqual(postBootstrapContainerPath, workspaceContainerPath) &&
            PathsEqual(paths.ContainerRootPath, workspaceContainerPath),
            "The workspace-container identity changed during bootstrap.");
        RequireSafeWorkspaceTree(workspaceContainerPath, paths);
        UnusedLevel65BlankLevelLabManifest manifest =
            await UnusedLevel65BlankLevelLabBootstrapper
                .ValidatePublishedWorkspaceReadOnlyAsync(
                    paths,
                    cancellationToken);
        Require(
            manifest == bootstrap.Manifest,
            "The published ID65 workspace manifest changed after bootstrap.");

        Id65AuthoringEditorDocumentReadModel? existing =
            await Id65AuthoringEditorDocumentWorkspaceInspector.InspectAsync(
                paths,
                manifest,
                cancellationToken);
        bool documentCreatedThisCall = false;
        if (existing == null)
        {
            Id65AuthoringEditorDocumentWriteResult written =
                await Id65AuthoringEditorDocumentWorkspaceWriter.SaveExactAsync(
                    paths,
                    manifest,
                    defaultState,
                    expectedPriorEnvelopeCanonicalSha256: null,
                    cancellationToken);
            Require(
                written.CreatedNewDocument &&
                written.DocumentCanonicalSha256 ==
                    ExpectedDocumentCanonicalSha256 &&
                written.AtomicPublicationCompleted &&
                written.PersistencePerformed &&
                !written.CarriesAppOrUiAuthority &&
                !written.AggregateMaterializationEnabled &&
                !written.CreateBinEnabled &&
                !written.NormalCreateBinEnabled &&
                !written.RuntimeCandidateAuthorized &&
                !written.RuntimeProven &&
                !written.ReleaseIntegrated &&
                !written.PromotionAuthorized &&
                !written.Publishable,
                "The canonical default document writer crossed an exact or offline boundary.");
            documentCreatedThisCall = true;
        }
        else
        {
            RequireExactDefaultDocument(existing, compiledDefault);
        }

        Id65AuthoringEditorDocumentReadModel document =
            await Id65AuthoringEditorDocumentWorkspaceInspector.InspectAsync(
                paths,
                manifest,
                cancellationToken) ?? throw new InvalidDataException(
                    "The canonical ID65 Full Authoring Document disappeared before materialization.");
        RequireExactDefaultDocument(document, compiledDefault);
        RequireSafeWorkspaceTree(workspaceContainerPath, paths);

        Id65AuthoringEditorAggregateCandidateWorkspaceResult candidate =
            await Id65AuthoringEditorAggregateCandidateWorkspaceMaterializer
                .MaterializeExactAsync(
                    paths,
                    manifest,
                    cancellationToken);
        RequireSafeWorkspaceTree(workspaceContainerPath, paths);
        RequireExactCandidate(candidate, paths, document);

        Id65AuthoringEditorDocumentReadModel currentDocument =
            await Id65AuthoringEditorDocumentWorkspaceInspector.InspectAsync(
                paths,
                manifest,
                cancellationToken) ?? throw new InvalidDataException(
                    "The canonical ID65 Full Authoring Document disappeared after materialization.");
        Require(
            currentDocument.EnvelopeCanonicalSha256 ==
                candidate.DocumentEnvelopeSha256 &&
            currentDocument.DocumentCanonicalSha256 ==
                candidate.DocumentCanonicalSha256,
            "The canonical ID65 Full Authoring Document changed after materialization.");
        RequireSafeWorkspaceTree(workspaceContainerPath, paths);

        string currentImageSha256 = await HashFileAsync(
            candidate.CandidateImagePath,
            cancellationToken);
        string currentCueSha256 = await HashFileAsync(
            candidate.CandidateCuePath,
            cancellationToken);
        Require(
            currentImageSha256 == candidate.OutputImageSha256 &&
            currentCueSha256 == candidate.CandidateCueSha256,
            "The staged aggregate BIN or CUE changed before handoff.");
        string finalContainerPath = RequireSafeWorkspaceContainer(
            workspaceContainerPath,
            sourceImagePath,
            sourceCuePath);
        Require(
            PathsEqual(finalContainerPath, workspaceContainerPath),
            "The workspace-container identity changed before handoff.");
        RequireSafeWorkspaceTree(workspaceContainerPath, paths);

        var report = new
        {
            format = "spyro-editor-id65-authoring-aggregate-stager-result",
            schemaVersion = 1,
            workspaceContainerPath,
            workspaceRootPath = paths.RootPath,
            documentPath = Path.Combine(
                paths.AuthoredEditsDirectoryPath,
                Id65AuthoringEditorDocumentWorkspaceInspector.DocumentFileName),
            candidateDirectoryPath = candidate.CandidateDirectoryPath,
            candidateImagePath = candidate.CandidateImagePath,
            candidateCuePath = candidate.CandidateCuePath,
            candidateReceiptPath = candidate.CandidateReceiptPath,
            sourceImageSha256 = bootstrap.SourceImageSha256,
            lockedBaseImageSha256 = bootstrap.LockedBaseImageSha256,
            documentEnvelopeSha256 = candidate.DocumentEnvelopeSha256,
            documentCanonicalSha256 = candidate.DocumentCanonicalSha256,
            aggregatePlanSha256 = candidate.AggregatePlanSha256,
            outputRow80Sha256 = candidate.OutputRow80Sha256,
            outputImageSha256 = candidate.OutputImageSha256,
            candidateCueSha256 = candidate.CandidateCueSha256,
            receiptContentSha256 = candidate.ReceiptContentSha256,
            candidate.ChangedRawSectorCount,
            candidate.ChangedPhysicalImageBytes,
            documentCreatedThisCall,
            bootstrapPermissionsHardenedThisCall,
            bootstrapRecoveryMutationPerformedThisCall,
            bootstrap.ReusedExistingLockedBase,
            candidate.ReusedExistingCandidate,
            candidate.CandidateArtifactsWrittenThisCall,
            fileSystemMutationPerformed =
                candidate.FileSystemMutationPerformed ||
                bootstrapPermissionsHardenedThisCall ||
                bootstrapRecoveryMutationPerformedThisCall ||
                documentCreatedThisCall,
            requiresFocusedRuntimeValidation =
                candidate.RequiresFocusedRuntimeValidation,
            runtimeCandidateAuthorized = candidate.RuntimeCandidateAuthorized,
            runtimeProven = candidate.RuntimeProven,
            carriesCreateBinAuthority = candidate.CarriesCreateBinAuthority,
            normalRetailCreateBinComposed = candidate.NormalRetailCreateBinComposed,
            releaseIntegrated = candidate.ReleaseIntegrated,
            promotionAuthorized = candidate.PromotionAuthorized,
            publishable = candidate.Publishable,
            currentOnDiskFreshnessVerified =
                candidate.CurrentOnDiskFreshnessVerified,
            hostileFilesystemRaceSafe = candidate.HostileFilesystemRaceSafe,
            requiresTrustedExclusiveWorkspaceDirectory =
                candidate.RequiresTrustedExclusiveWorkspaceDirectory,
            commonWorkspaceLeaseOwned = candidate.CommonWorkspaceLeaseOwned,
            existingWorkspaceTreeValidatedThisCall = true
        };
        Console.WriteLine(JsonSerializer.Serialize(
            report,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            }));
        Console.Error.WriteLine(
            $"{SuccessMarker} image={candidate.OutputImageSha256} " +
            $"cue={candidate.CandidateCueSha256} " +
            $"receipt={candidate.ReceiptContentSha256} " +
            "focusedDuckStation=PENDING createBinAuthority=false " +
            "runtimeAuthority=false releaseAuthority=false publishable=false");
    }

    private static void RequireExactDefaultDocument(
        Id65AuthoringEditorDocumentReadModel document,
        Id65AuthoringEditorControlStateCompilation compiledDefault)
    {
        Id65AuthoringEditorControlState recovered =
            Id65AuthoringEditorControlStateCompiler.RecoverControlState(document);
        Require(
            recovered.IncludeStructuralTerrain &&
            recovered.IncludeGrassAtT88 &&
            recovered.IncludeGrassAtT107 &&
            document.DocumentCanonicalSha256 ==
                compiledDefault.DocumentCanonicalSha256 &&
            document.DocumentContentSha256 ==
                compiledDefault.DocumentContentSha256,
            "The existing canonical document is not the exact full default. This stager never replaces authored intent.");
    }

    private static void RequireExactCandidate(
        Id65AuthoringEditorAggregateCandidateWorkspaceResult candidate,
        UnusedLevel65BlankLevelLabWorkspacePaths paths,
        Id65AuthoringEditorDocumentReadModel document)
    {
        string candidateRoot = Path.GetFullPath(Path.Combine(
            paths.RootPath,
            "materialized-candidates"));
        string candidateDirectory = Path.GetFullPath(
            candidate.CandidateDirectoryPath);
        Require(
            PathsEqual(
                Path.GetDirectoryName(candidateDirectory) ?? "",
                candidateRoot) &&
            PathsEqual(
                Path.GetDirectoryName(candidate.CandidateImagePath) ?? "",
                candidateDirectory) &&
            PathsEqual(
                Path.GetDirectoryName(candidate.CandidateCuePath) ?? "",
                candidateDirectory) &&
            PathsEqual(
                Path.GetDirectoryName(candidate.CandidateReceiptPath) ?? "",
                candidateDirectory) &&
            new[]
            {
                Path.GetFullPath(candidate.CandidateImagePath),
                Path.GetFullPath(candidate.CandidateCuePath),
                Path.GetFullPath(candidate.CandidateReceiptPath)
            }.Distinct(PathComparer).Count() == 3 &&
            File.Exists(candidate.CandidateImagePath) &&
            File.Exists(candidate.CandidateCuePath) &&
            File.Exists(candidate.CandidateReceiptPath) &&
            candidate.DocumentEnvelopeSha256 ==
                document.EnvelopeCanonicalSha256 &&
            candidate.DocumentCanonicalSha256 ==
                document.DocumentCanonicalSha256 &&
            candidate.AggregatePlanSha256 == ExpectedAggregatePlanSha256 &&
            candidate.OutputRow80Sha256 == ExpectedOutputRow80Sha256 &&
            candidate.OutputImageSha256 == ExpectedOutputImageSha256 &&
            candidate.CandidateCueSha256 == ExpectedCandidateCueSha256 &&
            candidate.ReceiptContentSha256 == ExpectedReceiptContentSha256 &&
            candidate.ChangedRawSectorCount == ExpectedChangedRawSectorCount &&
            candidate.ChangedPhysicalImageBytes ==
                ExpectedChangedPhysicalImageBytes &&
            candidate.ContainsFilePaths &&
            candidate.ContainsFullDiscImage &&
            candidate.ContainsCue &&
            candidate.ExactStoreReloadVerified &&
            candidate.ExactLockedBaseRecaptured &&
            candidate.ExactRow80ReadbackVerified &&
            candidate.ExactInverseReadbackVerified &&
            candidate.ProtectedComplementVerified &&
            candidate.RetailTownSquarePreserved &&
            candidate.TargetOverlayPreserved &&
            candidate.ExecutablePreserved &&
            candidate.RawSectorIntegrityVerified &&
            candidate.PhysicalComplementVerified &&
            candidate.AtomicNoReplacePublicationVerified &&
            candidate.OwnedTemporaryResidueRemoved &&
            candidate.RequiresExactFullDefaultDocument &&
            candidate.RequiresFocusedRuntimeValidation &&
            !candidate.CarriesAppOrUiAuthority &&
            !candidate.CarriesCreateBinAuthority &&
            !candidate.NormalRetailCreateBinComposed &&
            !candidate.RuntimeCandidateAuthorized &&
            !candidate.RuntimeProven &&
            !candidate.ReleaseIntegrated &&
            !candidate.PromotionAuthorized &&
            !candidate.Publishable &&
            !candidate.CurrentOnDiskFreshnessVerified &&
            !candidate.HostileFilesystemRaceSafe &&
            candidate.RequiresTrustedExclusiveWorkspaceDirectory &&
            !candidate.CommonWorkspaceLeaseOwned,
            "The staged aggregate candidate lost an exact identity, readback, or offline-authority boundary.");
    }

    private static string RequireAbsoluteExistingFile(string value, string label)
    {
        if (!Path.IsPathFullyQualified(value))
            throw new ArgumentException($"The {label} path must be absolute.");
        string fullPath = Path.GetFullPath(value);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"The {label} does not exist.", fullPath);
        return fullPath;
    }

    private static string RequireSafeWorkspaceContainer(
        string value,
        string sourceImagePath,
        string sourceCuePath)
    {
        if (!Path.IsPathFullyQualified(value))
            throw new ArgumentException(
                "The workspace-container path must be absolute.");
        string fullPath = NormalizeDirectoryPath(value);
        string root = NormalizeDirectoryPath(
            Path.GetPathRoot(fullPath) ?? throw new ArgumentException(
                "The workspace-container path has no filesystem root."));
        string userProfile = NormalizeDirectoryPath(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        string physicalSourceImagePath = ResolvePhysicalPath(sourceImagePath);
        string physicalSourceCuePath = ResolvePhysicalPath(sourceCuePath);
        if (PathsEqual(fullPath, root) ||
            PathsEqual(fullPath, userProfile) ||
            !IsWithin(userProfile, fullPath) ||
            IsWithin(fullPath, sourceImagePath) ||
            IsWithin(fullPath, sourceCuePath) ||
            IsWithin(fullPath, physicalSourceImagePath) ||
            IsWithin(fullPath, physicalSourceCuePath))
        {
            throw new ArgumentException(
                "The workspace-container path is too broad or contains a source fixture.");
        }

        string current = fullPath;
        bool foundExistingDirectory = false;
        while (true)
        {
            DirectoryInfo directory = new(current);
            directory.Refresh();
            FileInfo file = new(current);
            file.Refresh();
            if (file.Exists || file.LinkTarget != null ||
                directory.LinkTarget != null ||
                (directory.Exists &&
                    (directory.Attributes & FileAttributes.ReparsePoint) != 0))
            {
                throw new InvalidDataException(
                    "The workspace-container path and every existing ancestor under the user profile must be real directories, never files, links, or reparse points.");
            }
            if (directory.Exists)
            {
                foundExistingDirectory = true;
                if (OperatingSystem.IsMacOS())
                {
                    UnixFileMode mode = File.GetUnixFileMode(current);
                    if ((mode & (UnixFileMode.GroupWrite |
                            UnixFileMode.OtherWrite)) != 0)
                    {
                        throw new InvalidDataException(
                            "The workspace-container path and its existing ancestors must not be group- or other-writable.");
                    }
                }
            }
            if (PathsEqual(current, userProfile))
                break;
            DirectoryInfo? parent = Directory.GetParent(current);
            if (parent == null ||
                (!PathsEqual(parent.FullName, userProfile) &&
                    !IsWithin(userProfile, parent.FullName)))
            {
                throw new InvalidDataException(
                    "The workspace-container ancestor chain escaped the user profile.");
            }
            current = NormalizeDirectoryPath(parent.FullName);
        }
        Require(
            foundExistingDirectory,
            "The workspace-container path has no trusted existing ancestor.");
        return fullPath;
    }

    private static void RequireSafeWorkspaceTree(
        string workspaceContainerPath,
        UnusedLevel65BlankLevelLabWorkspacePaths paths)
    {
        string container = NormalizeDirectoryPath(workspaceContainerPath);
        RequireExactWorkspaceLayout(container, paths);

        string root = NormalizeDirectoryPath(paths.RootPath);
        Require(
            IsWithin(container, root),
            "The planned ID65 workspace root escapes its admitted container.");
        string[] fixedPaths =
        [
            paths.ManifestPath,
            paths.LockedBaseDirectoryPath,
            paths.LockedBaseImagePath,
            paths.LockedBaseCuePath,
            paths.AuthoredEditsDirectoryPath,
            paths.OperationsDirectoryPath
        ];
        Require(
            fixedPaths.All(path => IsWithin(root, path)),
            "A fixed ID65 workspace path escapes its versioned root.");
        string[] fixedDirectoryPaths =
        [
            root,
            paths.LockedBaseDirectoryPath,
            paths.AuthoredEditsDirectoryPath,
            paths.OperationsDirectoryPath,
            Path.Combine(root, "materialized-candidates"),
            Path.Combine(root, ".aggregate-candidate-operations")
        ];
        foreach (string fixedDirectoryPath in fixedDirectoryPaths)
        {
            RequireSafeDirectoryEntry(
                fixedDirectoryPath,
                "fixed workspace directory");
        }

        string key = UnusedLevel65BlankLevelLabProfileRegistry.Key;
        string[] reservedLegacyPaths =
        [
            Path.Combine(container, $"{key}-native-edits.json"),
            Path.Combine(container, $"{key}-native-moby-path-edits.json"),
            Path.Combine(container, $"{key}-custom-terrain-textures.json"),
            Path.Combine(
                container,
                $"{key}-native-terrain-texture-relocations.json"),
            Path.Combine(container, $"{key}-terrain-material-overrides.json"),
            Path.Combine(container, $"{key}-skybox-edit-plan.json"),
            Path.Combine(container, $"{key}-level-music-edit-plan.json")
        ];
        foreach (string reservedPath in reservedLegacyPaths)
            RequireAbsentEntry(reservedPath, "reserved legacy authoring path");

        foreach (string component in DirectoryComponents(container, root))
            RequireSafeDirectoryEntry(component, "fixed workspace path");

        if (!Directory.Exists(root))
            return;

        Stack<string> pending = new();
        pending.Push(root);
        while (pending.Count > 0)
        {
            string directoryPath = pending.Pop();
            RequireSafeDirectoryEntry(
                directoryPath,
                "existing workspace tree");
            foreach (string entry in Directory.EnumerateFileSystemEntries(
                         directoryPath))
            {
                DirectoryInfo directory = new(entry);
                FileInfo file = new(entry);
                directory.Refresh();
                file.Refresh();
                if (directory.LinkTarget != null || file.LinkTarget != null ||
                    (directory.Exists &&
                        (directory.Attributes & FileAttributes.ReparsePoint) != 0) ||
                    (file.Exists &&
                        (file.Attributes & FileAttributes.ReparsePoint) != 0))
                {
                    throw new InvalidDataException(
                        "The existing ID65 workspace tree must not contain symbolic links or reparse points.");
                }
                if (directory.Exists)
                {
                    RequireSafeDirectoryEntry(
                        entry,
                        "existing workspace tree");
                    pending.Push(entry);
                }
                else if (!file.Exists)
                {
                    throw new InvalidDataException(
                        "The existing ID65 workspace tree contains an unsupported filesystem entry.");
                }
                else
                {
                    RequireSafeRegularFileEntry(
                        entry,
                        "existing workspace tree");
                }
            }
        }
    }

    private static void RequireExactWorkspaceLayout(
        string workspaceContainerPath,
        UnusedLevel65BlankLevelLabWorkspacePaths paths)
    {
        string container = NormalizeDirectoryPath(workspaceContainerPath);
        UnusedLevel65BlankLevelLabWorkspacePaths expected =
            UnusedLevel65BlankLevelLabProfileRegistry.CreateWorkspacePaths(
                container);
        Require(
            PathsEqual(paths.ContainerRootPath, expected.ContainerRootPath) &&
            PathsEqual(paths.RootPath, expected.RootPath) &&
            PathsEqual(paths.ManifestPath, expected.ManifestPath) &&
            PathsEqual(
                paths.LockedBaseDirectoryPath,
                expected.LockedBaseDirectoryPath) &&
            PathsEqual(paths.LockedBaseImagePath, expected.LockedBaseImagePath) &&
            PathsEqual(paths.LockedBaseCuePath, expected.LockedBaseCuePath) &&
            PathsEqual(
                paths.AuthoredEditsDirectoryPath,
                expected.AuthoredEditsDirectoryPath) &&
            PathsEqual(
                paths.OperationsDirectoryPath,
                expected.OperationsDirectoryPath),
            "The planned ID65 workspace layout changed identity.");
    }

    internal static bool HardenNewBootstrapFilesForStaging(
        UnusedLevel65BlankLevelLabWorkspacePaths paths)
    {
        if (!OperatingSystem.IsMacOS())
        {
            throw new PlatformNotSupportedException(
                "Bootstrap permission hardening requires macOS Unix file modes.");
        }
        _ = UnusedLevel65BlankLevelLabProfileRegistry.CreateManifest(paths);
        string[] files =
        [
            paths.ManifestPath,
            paths.LockedBaseImagePath,
            paths.LockedBaseCuePath
        ];
        bool changed = false;
        foreach (string path in files)
        {
            FileInfo file = new(path);
            DirectoryInfo directory = new(path);
            file.Refresh();
            directory.Refresh();
            if (!file.Exists || directory.Exists || file.LinkTarget != null ||
                directory.LinkTarget != null ||
                (file.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException(
                    "A newly bootstrapped ID65 fixed file is missing or is not one regular file.");
            }
            UnixFileMode before = File.GetUnixFileMode(path);
            UnixFileMode after = before &
                ~(UnixFileMode.GroupWrite | UnixFileMode.OtherWrite);
            if (after == before)
                continue;
            File.SetUnixFileMode(path, after);
            if (File.GetUnixFileMode(path) != after)
            {
                throw new IOException(
                    "A newly bootstrapped ID65 fixed file did not retain its hardened Unix mode.");
            }
            changed = true;
        }
        return changed;
    }

    private static IEnumerable<string> DirectoryComponents(
        string container,
        string descendant)
    {
        string current = NormalizeDirectoryPath(descendant);
        Stack<string> components = new();
        while (!PathsEqual(current, container))
        {
            Require(
                IsWithin(container, current),
                "A fixed ID65 workspace directory escaped its admitted container.");
            components.Push(current);
            current = NormalizeDirectoryPath(
                Directory.GetParent(current)?.FullName ?? throw new InvalidDataException(
                    "A fixed ID65 workspace directory has no admitted parent."));
        }
        components.Push(container);
        return components;
    }

    private static void RequireSafeDirectoryEntry(string path, string label)
    {
        DirectoryInfo directory = new(path);
        FileInfo file = new(path);
        directory.Refresh();
        file.Refresh();
        if (file.Exists || file.LinkTarget != null ||
            directory.LinkTarget != null ||
            (directory.Exists &&
                (directory.Attributes & FileAttributes.ReparsePoint) != 0))
        {
            throw new InvalidDataException(
                $"The {label} must contain only real directories, never files, links, or reparse points.");
        }
        if (!directory.Exists)
            return;
        if (OperatingSystem.IsMacOS())
        {
            UnixFileMode mode = File.GetUnixFileMode(path);
            if ((mode & (UnixFileMode.GroupWrite | UnixFileMode.OtherWrite)) != 0)
            {
                throw new InvalidDataException(
                    $"The {label} directories must not be group- or other-writable.");
            }
        }
    }

    private static void RequireAbsentEntry(string path, string label)
    {
        DirectoryInfo directory = new(path);
        FileInfo file = new(path);
        directory.Refresh();
        file.Refresh();
        if (directory.Exists || directory.LinkTarget != null ||
            file.Exists || file.LinkTarget != null)
        {
            throw new InvalidDataException(
                $"The {label} '{Path.GetFileName(path)}' must be absent before exact aggregate staging.");
        }
    }

    private static void RequireSafeRegularFileEntry(string path, string label)
    {
        FileInfo file = new(path);
        DirectoryInfo directory = new(path);
        file.Refresh();
        directory.Refresh();
        if (!file.Exists || directory.Exists || file.LinkTarget != null ||
            directory.LinkTarget != null ||
            (file.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException(
                $"The {label} files must be regular files, never directories, links, or reparse points.");
        }
        if (OperatingSystem.IsMacOS())
        {
            UnixFileMode mode = File.GetUnixFileMode(path);
            if ((mode & (UnixFileMode.GroupWrite | UnixFileMode.OtherWrite)) != 0)
            {
                throw new InvalidDataException(
                    $"The {label} file '{path}' must not be group- or other-writable.");
            }
        }
    }

    private static string ResolvePhysicalPath(string path, int linkDepth = 0)
    {
        if (linkDepth > 64)
        {
            throw new InvalidDataException(
                $"A source path contains too many symbolic-link hops: {path}");
        }
        string fullPath = Path.GetFullPath(path);
        string root = Path.GetPathRoot(fullPath) ?? throw new InvalidDataException(
            $"A rooted source path is required: {path}");
        string relative = Path.GetRelativePath(root, fullPath);
        if (relative == ".")
            return Path.GetFullPath(root);

        string current = Path.GetFullPath(root);
        foreach (string segment in relative.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = Path.GetFullPath(Path.Combine(current, segment));
            FileSystemInfo? entry = FindExistingEntry(candidate);
            if (entry == null || string.IsNullOrWhiteSpace(entry.LinkTarget))
            {
                current = candidate;
                continue;
            }

            FileSystemInfo resolved;
            try
            {
                resolved = entry.ResolveLinkTarget(returnFinalTarget: true)
                    ?? throw new IOException(
                        $"The source symbolic link has no resolvable target: {candidate}");
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                throw new InvalidDataException(
                    $"A source path contains an unreadable symbolic link: {candidate}",
                    exception);
            }
            current = ResolvePhysicalPath(resolved.FullName, linkDepth + 1);
        }

        return Path.TrimEndingDirectorySeparator(current);
    }

    private static FileSystemInfo? FindExistingEntry(string path)
    {
        DirectoryInfo directory = new(path);
        try
        {
            directory.Refresh();
            if (directory.Exists ||
                !string.IsNullOrWhiteSpace(directory.LinkTarget))
            {
                return directory;
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException(
                $"A source path component cannot be inspected: {path}",
                exception);
        }

        FileInfo file = new(path);
        try
        {
            file.Refresh();
            if (file.Exists || !string.IsNullOrWhiteSpace(file.LinkTarget))
                return file;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException(
                $"A source path component cannot be inspected: {path}",
                exception);
        }

        return null;
    }

    private static bool IsWithin(string root, string candidate)
    {
        string fullRoot = NormalizeDirectoryPath(root);
        string fullCandidate = Path.GetFullPath(candidate);
        string prefix = fullRoot.EndsWith(Path.DirectorySeparatorChar)
            ? fullRoot
            : fullRoot + Path.DirectorySeparatorChar;
        return fullCandidate.StartsWith(prefix, PathComparison);
    }

    private static string NormalizeDirectoryPath(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    private static bool PathsEqual(string left, string right) =>
        string.Equals(
            NormalizeDirectoryPath(left),
            NormalizeDirectoryPath(right),
            PathComparison);

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    private static async Task<string> HashFileAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using FileStream stream = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 1024 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexStringLower(hash);
    }

    private static bool TryParse(
        string[] args,
        out Options? options,
        out string error)
    {
        options = null;
        error = "";
        if (args.Length != 7 ||
            !string.Equals(args[0], "stage", StringComparison.Ordinal))
        {
            error = "Expected exactly the stage command and three named absolute paths.";
            return false;
        }

        Dictionary<string, string> values = new(StringComparer.Ordinal);
        for (int index = 1; index < args.Length; index += 2)
        {
            string name = args[index];
            if (name is not ("--source-bin" or "--source-cue" or
                    "--workspace-container"))
            {
                error = $"Unknown option '{name}'.";
                return false;
            }
            if (!values.TryAdd(name, args[index + 1]))
            {
                error = $"Duplicate option '{name}'.";
                return false;
            }
        }
        if (!values.TryGetValue("--source-bin", out string? sourceImagePath) ||
            !values.TryGetValue("--source-cue", out string? sourceCuePath) ||
            !values.TryGetValue(
                "--workspace-container",
                out string? workspaceContainerPath))
        {
            error = "All three named paths are required.";
            return false;
        }
        options = new(sourceImagePath, sourceCuePath, workspaceContainerPath);
        return true;
    }

    private static void PrintUsage(TextWriter writer) => writer.WriteLine(
        "Usage: Spyro.Editor.Id65AuthoringEditorAggregateCandidateStager " +
        "stage --source-bin <absolute-path> --source-cue <absolute-path> " +
        "--workspace-container <absolute-path>");

    private static string OneLine(string value) =>
        value.Replace('\r', ' ').Replace('\n', ' ');

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }

    private sealed record Options(
        string SourceImagePath,
        string SourceCuePath,
        string WorkspaceContainerPath);
}
