using System.IO.Compression;
using System.Text.Json.Nodes;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;
using Spyro.Editor.Core.Workspace;

string temporaryRoot = Path.Combine(Path.GetTempPath(), $"spyro-editor-persistence-{Guid.NewGuid():N}");
string portableRoot = Path.Combine(temporaryRoot, "SpyroEditor-beta.25");
string dataRoot = Path.Combine(temporaryRoot, "user-data");
string? previousWorkspace = Environment.GetEnvironmentVariable(ReleaseProjectBootstrap.WorkspaceEnvironmentVariable);
string? previousInstall = Environment.GetEnvironmentVariable(ReleaseProjectBootstrap.InstallRootEnvironmentVariable);
string? previousRelease = Environment.GetEnvironmentVariable(ReleaseProjectBootstrap.ReleaseEnvironmentVariable);
string? previousResearchProjectBridge = Environment.GetEnvironmentVariable(
    ResearchProjectWorkspaceBridge.UseCurrentReleaseProjectEnvironmentVariable);

try
{
    Directory.CreateDirectory(portableRoot);
    string importedSky = Path.Combine(portableRoot, "custom-skyboxes", "artisans", "sky.sky");
    string importedTexture = Path.Combine(portableRoot, "_local", "custom-textures", "artisans-12.png");
    Write(importedSky, "sky bytes");
    Write(importedTexture, "texture bytes");
    Write(Path.Combine(portableRoot, "artisans-native-edits.json"), "{\"editCount\":1,\"edits\":[{}]}");
    Write(Path.Combine(portableRoot, "stonehill-native-level-replacement.json"), "{\"format\":\"spyro-editor-native-level-replacement\",\"version\":1}");
    Write(Path.Combine(portableRoot, "artisans-skybox-edit-plan.json"), $$"""
        {"mode":"custom-sky-import","importedSkyPath":{{Json(importedSky)}}}
        """);
    Write(Path.Combine(portableRoot, "artisans-custom-terrain-textures.json"), $$"""
        {"textures":[{"textureId":12,"sourceImagePath":{{Json(importedTexture)}}}]}
        """);
    Write(Path.Combine(portableRoot, "artisans-native-terrain-texture-relocations.json"), $$"""
        {"destinationLevelKey":"artisans","relocations":[{"targetTextureId":54,"donorLevelKey":"gnastysworld","donorLevelName":"Gnasty's World","donorWadEntry":70,"donorTextureId":22,"donorRuntimeKey":"34:3:hp","descriptorTier":"both","previewImagePath":{{Json(importedTexture)}},"createdAt":"2026-07-15T00:00:00Z"}]}
        """);
    Write(Path.Combine(portableRoot, "_local", "settings", "source-disc.json"), "{\"imagePath\":\"/Games/Spyro.bin\"}");
    Write(Path.Combine(portableRoot, "_local", "identity-review", "answers.tsv"), "key\tanswer");
    Write(Path.Combine(portableRoot, "output", "My Build.cue"), "FILE \"My Build.bin\" BINARY");
    Write(Path.Combine(portableRoot, "output", "My Build.bin"), "small test output");
    Write(Path.Combine(portableRoot, "editor-cache", "artisans-mobys.json"), "{}");
    Write(Path.Combine(portableRoot, "spyro-wad-analysis.json"), "{}");
    Write(Path.Combine(portableRoot, "support", "spyro-level-catalog.json"), "{}");
    Write(Path.Combine(portableRoot, "support", "spyro-object-templates.json"), "{}");
    Write(Path.Combine(portableRoot, "support", "docs", "release-user-guide.md"), "guide");
    Write(Path.Combine(portableRoot, "support", "docs", "retired-in-next-release.md"), "retired guide");
    Write(Path.Combine(portableRoot, "support", "app", "must-not-copy.dll"), "application binary");
    Write(Path.Combine(portableRoot, "Spyro Editor.app", "Contents", "MacOS", "Spyro.Editor.App"), "app");
    Write(Path.Combine(portableRoot, "Launch Spyro Editor.command"), "#!/bin/zsh\n");
    Write(Path.Combine(portableRoot, "unowned.bin"), "game image must not migrate");

    EditorUserDataLayout userData = new(dataRoot);
    VerifyResearchWorkspaceBridge(temporaryRoot, portableRoot);
    Assert(userData.RootPath == Path.GetFullPath(dataRoot), "Explicit data root was not honored.");
    Assert(EditorUserDataLayout.NormalizeProjectId("default-project") == "default-project", "Safe project ID changed unexpectedly.");
    Assert(EditorUserDataLayout.NormalizeProjectId("My / Project") != EditorUserDataLayout.NormalizeProjectId("My Project"), "Lossy project IDs collided.");
    Assert(EditorUserDataLayout.NormalizeProjectId("测试") != EditorUserDataLayout.NormalizeProjectId("デモ"), "Unicode project IDs collided.");
    Assert(
        EditorUserDataLayout.NormalizeProjectId(new string('a', 70) + "x") != EditorUserDataLayout.NormalizeProjectId(new string('a', 70) + "y"),
        "Truncated project IDs collided.");
    Assert(!new PortableProjectMigrationOptions().IncludeGeneratedOutputs, "Large generated outputs must remain opt-in during legacy migration.");
    Assert(
        PortableProjectMigration.Classify("_local/research/proof.json") == PortableProjectArtifactKind.UserResearch,
        "General local research was not classified as user research.");
    Assert(
        PortableProjectMigration.Classify("_local/skybox-research/proof.json") == PortableProjectArtifactKind.UserResearch,
        "Skybox research was not classified as user research.");
    Assert(
        PortableProjectMigration.Classify("_local/release-qa/proof.json") == PortableProjectArtifactKind.UserResearch,
        "Release QA evidence was not classified as user research.");
    EditorProjectLayout identityCheck = await userData.EnsureProjectAsync("manifest-check", "Manifest Check");
    JsonNode identityManifest = JsonNode.Parse(File.ReadAllText(identityCheck.ManifestPath))!;
    identityManifest["projectId"] = "wrong-project";
    Write(identityCheck.ManifestPath, identityManifest.ToJsonString());
    bool identityMismatchRejected = false;
    try
    {
        await userData.EnsureProjectAsync("manifest-check", "Manifest Check");
    }
    catch (InvalidDataException)
    {
        identityMismatchRejected = true;
    }
    Assert(identityMismatchRejected, "An existing project manifest identity mismatch was silently reused.");
    EditorProjectLayout project = await userData.EnsureProjectAsync("my-project", "My Spyro Project", portableRoot);
    Assert(File.Exists(project.ManifestPath), "Project manifest was not created.");
    Assert(!project.RootPath.StartsWith(Path.GetFullPath(portableRoot), StringComparison.OrdinalIgnoreCase), "Project remained inside the application folder.");

    PortableProjectMigrationOptions preserveOutputs = new(IncludeGeneratedOutputs: true);
    PortableProjectMigrationResult first = await PortableProjectMigration.MigrateAsync(portableRoot, project, preserveOutputs);
    Assert(first.CopiedCount == 11, $"Expected 11 migrated files, got {first.CopiedCount}.");
    Assert(first.ConflictCount == 0, "Unexpected first-run migration conflict.");
    Assert(File.Exists(Path.Combine(project.RootPath, "artisans-native-edits.json")), "Object edits were not migrated.");
    Assert(File.Exists(Path.Combine(project.RootPath, "stonehill-native-level-replacement.json")), "Native level-replacement research was not migrated.");
    Assert(File.Exists(Path.Combine(project.RootPath, "output", "My Build.bin")), "Generated output was not preserved.");
    Assert(!File.Exists(Path.Combine(project.RootPath, "editor-cache", "artisans-mobys.json")), "Rebuildable cache migrated by default.");
    Assert(!File.Exists(Path.Combine(project.RootPath, "spyro-wad-analysis.json")), "Source-dependent WAD analysis migrated by default.");
    Assert(!File.Exists(Path.Combine(project.RootPath, "support", "spyro-level-catalog.json")), "Application support files migrated into user data.");
    Assert(!File.Exists(Path.Combine(project.RootPath, "unowned.bin")), "A game image migrated into user data.");

    JsonNode skyPlan = JsonNode.Parse(File.ReadAllText(Path.Combine(project.RootPath, "artisans-skybox-edit-plan.json")))!;
    string rebasedSky = skyPlan["importedSkyPath"]!.GetValue<string>();
    Assert(rebasedSky == Path.Combine(project.RootPath, "custom-skyboxes", "artisans", "sky.sky"), "Imported sky path was not rebased.");
    JsonNode texturePlan = JsonNode.Parse(File.ReadAllText(Path.Combine(project.RootPath, "artisans-custom-terrain-textures.json")))!;
    string rebasedTexture = texturePlan["textures"]![0]!["sourceImagePath"]!.GetValue<string>();
    Assert(rebasedTexture == Path.Combine(project.RootPath, "_local", "custom-textures", "artisans-12.png"), "Imported texture path was not rebased.");
    JsonNode relocationPlan = JsonNode.Parse(File.ReadAllText(Path.Combine(project.RootPath, "artisans-native-terrain-texture-relocations.json")))!;
    string rebasedRelocationPreview = relocationPlan["relocations"]![0]!["previewImagePath"]!.GetValue<string>();
    Assert(
        rebasedRelocationPreview == Path.Combine(project.RootPath, "_local", "custom-textures", "artisans-12.png"),
        "Native terrain relocation preview path was not rebased.");

    File.Copy(
        Path.Combine(portableRoot, "artisans-skybox-edit-plan.json"),
        Path.Combine(project.RootPath, "artisans-skybox-edit-plan.json"),
        overwrite: true);
    Assert(
        File.ReadAllBytes(Path.Combine(portableRoot, "artisans-skybox-edit-plan.json"))
            .AsSpan()
            .SequenceEqual(File.ReadAllBytes(Path.Combine(project.RootPath, "artisans-skybox-edit-plan.json"))),
        "Interrupted-retry fixture was not copied byte-for-byte.");
    PortableProjectMigrationResult second = await PortableProjectMigration.MigrateAsync(portableRoot, project, preserveOutputs);
    if (second.ConflictCount > 0)
        Console.WriteLine($"Retry conflicts: {string.Join(", ", second.ConflictPaths)}");
    Assert(second.ExistingCount == 11, $"Expected 11 identical existing files, got {second.ExistingCount}; copied={second.CopiedCount}, conflicts={second.ConflictCount}, skipped={second.SkippedCount}.");
    Assert(second.ConflictCount == 0, "Idempotent migration created a conflict.");
    JsonNode recoveredSkyPlan = JsonNode.Parse(File.ReadAllText(Path.Combine(project.RootPath, "artisans-skybox-edit-plan.json")))!;
    Assert(recoveredSkyPlan["importedSkyPath"]!.GetValue<string>() == rebasedSky, "An interrupted path-rebase was not recovered on retry.");

    Write(Path.Combine(portableRoot, "artisans-native-edits.json"), "{\"editCount\":2,\"edits\":[{},{}]}");
    JsonNode changedLegacySkyPlan = JsonNode.Parse(File.ReadAllText(Path.Combine(portableRoot, "artisans-skybox-edit-plan.json")))!;
    changedLegacySkyPlan["legacyVariant"] = "conflicting plan";
    Write(Path.Combine(portableRoot, "artisans-skybox-edit-plan.json"), changedLegacySkyPlan.ToJsonString());
    PortableProjectMigrationResult third = await PortableProjectMigration.MigrateAsync(portableRoot, project, preserveOutputs);
    Assert(third.ConflictCount == 2, $"Expected two preserved conflicts, got {third.ConflictCount}.");
    Assert(third.ConflictPaths.Count == 2 && third.ConflictPaths.All(File.Exists), "Conflict copies were not preserved.");
    Assert(File.ReadAllText(Path.Combine(project.RootPath, "artisans-native-edits.json")).Contains("\"editCount\":1", StringComparison.Ordinal), "Migration overwrote newer destination data.");
    string skyConflictPath = third.ConflictPaths.Single(path => path.EndsWith("artisans-skybox-edit-plan.json", StringComparison.OrdinalIgnoreCase));
    JsonNode skyConflict = JsonNode.Parse(File.ReadAllText(skyConflictPath))!;
    string conflictSkyAsset = skyConflict["importedSkyPath"]!.GetValue<string>();
    Assert(File.Exists(conflictSkyAsset), "Conflicting imported-sky plan was not preserved with a self-contained asset copy.");
    Assert(
        conflictSkyAsset.StartsWith(Path.GetDirectoryName(skyConflictPath)!, StringComparison.OrdinalIgnoreCase),
        "Conflicting imported-sky plan still points back into the replaceable installation.");

    string atomicSourceRoot = Path.Combine(temporaryRoot, "atomic-plan-source");
    string atomicSourceAsset = Path.Combine(atomicSourceRoot, "custom-skyboxes", "artisans", "sky.sky");
    Write(atomicSourceAsset, "incoming legacy sky bytes");
    Write(Path.Combine(atomicSourceRoot, "artisans-skybox-edit-plan.json"), $$"""
        {"mode":"custom-sky-import","importedSkyPath":{{Json(atomicSourceAsset)}}}
        """);
    EditorProjectLayout atomicProject = await userData.EnsureProjectAsync("atomic-plan-project", "Atomic Plan Project");
    string atomicDestinationAsset = Path.Combine(atomicProject.RootPath, "custom-skyboxes", "artisans", "sky.sky");
    Write(atomicDestinationAsset, "existing project sky bytes");
    PortableProjectMigrationResult atomicResult = await PortableProjectMigration.MigrateAsync(
        atomicSourceRoot,
        atomicProject);
    Assert(
        !File.Exists(Path.Combine(atomicProject.RootPath, "artisans-skybox-edit-plan.json")),
        "A legacy plan was activated even though its referenced asset conflicted with the current project.");
    Assert(
        File.ReadAllText(atomicDestinationAsset) == "existing project sky bytes",
        "A referenced-asset conflict overwrote the current project's asset.");
    string atomicConflictPlanPath = atomicResult.ConflictPaths.Single(path =>
        path.EndsWith("artisans-skybox-edit-plan.json", StringComparison.OrdinalIgnoreCase));
    JsonNode atomicConflictPlan = JsonNode.Parse(File.ReadAllText(atomicConflictPlanPath))!;
    string atomicConflictAsset = atomicConflictPlan["importedSkyPath"]!.GetValue<string>();
    Assert(
        File.Exists(atomicConflictAsset) && File.ReadAllText(atomicConflictAsset) == "incoming legacy sky bytes",
        "A plan blocked by a referenced-asset conflict was not preserved with its matching incoming asset.");
    Assert(
        atomicConflictAsset.StartsWith(Path.GetDirectoryName(atomicConflictPlanPath)!, StringComparison.OrdinalIgnoreCase),
        "A plan blocked by a referenced-asset conflict was not rebased into its conflict bundle.");

    string relocationAtomicSourceRoot = Path.Combine(temporaryRoot, "atomic-relocation-plan-source");
    string relocationAtomicSourceAsset = Path.Combine(
        relocationAtomicSourceRoot,
        "_local",
        "custom-textures",
        "generated",
        "artisans-54.png");
    Write(relocationAtomicSourceAsset, "incoming native relocation preview bytes");
    Write(Path.Combine(relocationAtomicSourceRoot, "artisans-native-terrain-texture-relocations.json"), $$"""
        {"destinationLevelKey":"artisans","relocations":[{"targetTextureId":54,"donorLevelKey":"gnastysworld","donorLevelName":"Gnasty's World","donorWadEntry":70,"donorTextureId":22,"donorRuntimeKey":"34:3:hp","descriptorTier":"both","previewImagePath":{{Json(relocationAtomicSourceAsset)}},"createdAt":"2026-07-15T00:00:00Z"}]}
        """);
    EditorProjectLayout relocationAtomicProject = await userData.EnsureProjectAsync(
        "atomic-relocation-plan-project",
        "Atomic Relocation Plan Project");
    string relocationAtomicDestinationAsset = Path.Combine(
        relocationAtomicProject.RootPath,
        "_local",
        "custom-textures",
        "generated",
        "artisans-54.png");
    Write(relocationAtomicDestinationAsset, "existing project preview bytes");
    PortableProjectMigrationResult relocationAtomicResult = await PortableProjectMigration.MigrateAsync(
        relocationAtomicSourceRoot,
        relocationAtomicProject);
    Assert(
        !File.Exists(Path.Combine(relocationAtomicProject.RootPath, "artisans-native-terrain-texture-relocations.json")),
        "A native relocation plan was activated even though its preview asset conflicted with the current project.");
    Assert(
        File.ReadAllText(relocationAtomicDestinationAsset) == "existing project preview bytes",
        "A native relocation preview conflict overwrote the current project's asset.");
    string relocationAtomicConflictPlanPath = relocationAtomicResult.ConflictPaths.Single(path =>
        path.EndsWith("artisans-native-terrain-texture-relocations.json", StringComparison.OrdinalIgnoreCase));
    JsonNode relocationAtomicConflictPlan = JsonNode.Parse(File.ReadAllText(relocationAtomicConflictPlanPath))!;
    string relocationAtomicConflictAsset = relocationAtomicConflictPlan["relocations"]![0]!["previewImagePath"]!.GetValue<string>();
    Assert(
        File.Exists(relocationAtomicConflictAsset) &&
        File.ReadAllText(relocationAtomicConflictAsset) == "incoming native relocation preview bytes",
        "A native relocation plan blocked by a preview conflict was not preserved with its matching incoming preview.");
    Assert(
        relocationAtomicConflictAsset.StartsWith(Path.GetDirectoryName(relocationAtomicConflictPlanPath)!, StringComparison.OrdinalIgnoreCase),
        "A native relocation plan blocked by a preview conflict was not rebased into its conflict bundle.");

    VerifyReleaseArtifactIsolation(Path.Combine(temporaryRoot, "locator-isolation"));

    bool nestedRejected = false;
    try
    {
        EditorProjectLayout invalid = new(Path.Combine(portableRoot, "Projects", "bad"), "bad");
        await PortableProjectMigration.MigrateAsync(portableRoot, invalid);
    }
    catch (InvalidOperationException)
    {
        nestedRejected = true;
    }
    Assert(nestedRejected, "A project destination inside the application folder was accepted.");

    bool reverseOverlapRejected = false;
    string overlappingDestinationRoot = Path.Combine(temporaryRoot, "overlapping-destination");
    string nestedSourceRoot = Path.Combine(overlappingDestinationRoot, "legacy-source");
    Write(Path.Combine(nestedSourceRoot, "artisans-native-edits.json"), "{}");
    try
    {
        await PortableProjectMigration.MigrateAsync(
            nestedSourceRoot,
            new EditorProjectLayout(overlappingDestinationRoot, "overlapping-destination"));
    }
    catch (InvalidOperationException)
    {
        reverseOverlapRejected = true;
    }
    Assert(reverseOverlapRejected, "A migration source inside its destination project was accepted.");

    string nestedMacAppRoot = Path.Combine(portableRoot, "Spyro Editor.app", "Contents", "MacOS");
    Write(Path.Combine(nestedMacAppRoot, "support", "spyro-level-catalog.json"), "{}");
    Write(Path.Combine(nestedMacAppRoot, "support", "spyro-object-templates.json"), "{}");
    Assert(
        ReleaseProjectBootstrap.FindInstallRoot(appBaseDirectory: nestedMacAppRoot) == Path.GetFullPath(portableRoot),
        "Direct Mac app launch did not prefer the surrounding portable installation root.");
    string standaloneMacAppRoot = Path.Combine(temporaryRoot, "Standalone Spyro Editor.app", "Contents", "MacOS");
    Write(Path.Combine(standaloneMacAppRoot, "support", "spyro-level-catalog.json"), "{}");
    Write(Path.Combine(standaloneMacAppRoot, "support", "spyro-object-templates.json"), "{}");
    Assert(
        ReleaseProjectBootstrap.FindInstallRoot(appBaseDirectory: standaloneMacAppRoot) == Path.GetFullPath(standaloneMacAppRoot),
        "A standalone Mac app could not identify its internal release support root.");

    Environment.SetEnvironmentVariable(ReleaseProjectBootstrap.WorkspaceEnvironmentVariable, portableRoot);
    string rejectedBootstrapDataRoot = Path.Combine(temporaryRoot, "rejected-bootstrap-data");
    await AssertInvalidOperationAsync(
        async () => await ReleaseProjectBootstrap.PrepareAsync(
            appVersion: "0.1.0-beta.26",
            appBaseDirectory: portableRoot,
            userData: new EditorUserDataLayout(rejectedBootstrapDataRoot),
            explicitInstallRoot: portableRoot,
            forceReleaseMode: true),
        "PrepareAsync accepted a workspace equal to the replaceable installation.");

    string insideInstallWorkspace = Path.Combine(portableRoot, "Projects", "inside-install");
    Environment.SetEnvironmentVariable(ReleaseProjectBootstrap.WorkspaceEnvironmentVariable, insideInstallWorkspace);
    await AssertInvalidOperationAsync(
        async () => await ReleaseProjectBootstrap.PrepareAsync(
            appVersion: "0.1.0-beta.26",
            appBaseDirectory: portableRoot,
            userData: new EditorUserDataLayout(Path.Combine(temporaryRoot, "rejected-descendant-data")),
            explicitInstallRoot: portableRoot,
            forceReleaseMode: true),
        "PrepareAsync accepted a workspace inside the replaceable installation.");
    Assert(!Directory.Exists(insideInstallWorkspace), "Rejected in-install project storage was created before validation.");

    string unsafeDataRoot = Path.Combine(portableRoot, "unsafe-user-data");
    Environment.SetEnvironmentVariable(ReleaseProjectBootstrap.WorkspaceEnvironmentVariable, "");
    await AssertInvalidOperationAsync(
        async () => await ReleaseProjectBootstrap.PrepareAsync(
            appVersion: "0.1.0-beta.26",
            appBaseDirectory: portableRoot,
            userData: new EditorUserDataLayout(
                unsafeDataRoot,
                Path.Combine(temporaryRoot, "safe-projects-for-unsafe-data")),
            explicitInstallRoot: portableRoot,
            forceReleaseMode: true),
        "PrepareAsync created user-data storage inside the replaceable installation.");
    Assert(!Directory.Exists(unsafeDataRoot), "Unsafe user-data storage was created before validation.");

    string unsafeProjectsRoot = Path.Combine(portableRoot, "unsafe-projects-root");
    await AssertInvalidOperationAsync(
        async () => await ReleaseProjectBootstrap.PrepareAsync(
            appVersion: "0.1.0-beta.26",
            appBaseDirectory: portableRoot,
            userData: new EditorUserDataLayout(
                Path.Combine(temporaryRoot, "safe-data-for-unsafe-projects"),
                unsafeProjectsRoot),
            explicitInstallRoot: portableRoot,
            forceReleaseMode: true),
        "PrepareAsync created the projects root inside the replaceable installation.");
    Assert(!Directory.Exists(unsafeProjectsRoot), "Unsafe projects storage was created before validation.");

    string registeredOverlapDataRoot = Path.Combine(temporaryRoot, "registered-overlap-data");
    EditorUserDataLayout registeredOverlapUserData = new(registeredOverlapDataRoot);
    Write(
        Path.Combine(registeredOverlapUserData.SettingsPath, "current-project.json"),
        System.Text.Json.JsonSerializer.Serialize(new CurrentProjectSettings(
            Version: 1,
            ProjectId: "unsafe-registered-project",
            ProjectRoot: registeredOverlapUserData.BackupsPath,
            SavedAtUtc: DateTimeOffset.UtcNow)));
    Environment.SetEnvironmentVariable(ReleaseProjectBootstrap.WorkspaceEnvironmentVariable, "");
    await AssertInvalidOperationAsync(
        async () => await ReleaseProjectBootstrap.PrepareAsync(
            appVersion: "0.1.0-beta.26",
            appBaseDirectory: portableRoot,
            userData: registeredOverlapUserData,
            explicitInstallRoot: portableRoot,
            forceReleaseMode: true),
        "PrepareAsync accepted a registered project overlapping safety backups.");
    Assert(
        !File.Exists(Path.Combine(registeredOverlapUserData.BackupsPath, "spyro-project.json")),
        "A rejected registered project modified the safety-backup tree.");

    string fallbackDataRoot = Path.Combine(temporaryRoot, "fallback-user-data");
    string blockedDefaultProjectsRoot = Path.Combine(temporaryRoot, "blocked-documents-projects");
    Write(blockedDefaultProjectsRoot, "this file simulates an unavailable default Projects directory");
    EditorUserDataLayout fallbackRequestedUserData = new(
        fallbackDataRoot,
        blockedDefaultProjectsRoot,
        allowProjectsFallback: true);
    ReleaseProjectContext fallbackBootstrap = await ReleaseProjectBootstrap.PrepareAsync(
        appVersion: "0.1.0-beta.26",
        appBaseDirectory: portableRoot,
        userData: fallbackRequestedUserData,
        explicitInstallRoot: portableRoot,
        forceReleaseMode: true) ?? throw new InvalidOperationException("Fallback release project bootstrap did not run.");
    string expectedFallbackProjectsPath = Path.Combine(fallbackDataRoot, "Projects");
    string expectedFallbackProjectRoot = Path.Combine(expectedFallbackProjectsPath, "default-project");
    Assert(
        fallbackBootstrap.UserData.ProjectsPath == Path.GetFullPath(expectedFallbackProjectsPath),
        "The fallback context did not report its actual projects root.");
    Assert(
        fallbackBootstrap.Project.RootPath == Path.GetFullPath(expectedFallbackProjectRoot),
        "The default project was not created under the user-data fallback root.");
    Assert(File.Exists(fallbackBootstrap.Project.ManifestPath), "The fallback project manifest was not created.");
    Assert(
        fallbackBootstrap.Project.RootPath != fallbackBootstrap.UserData.SettingsPath &&
        fallbackBootstrap.Project.RootPath != fallbackBootstrap.UserData.UpdatesPath &&
        fallbackBootstrap.Project.RootPath != fallbackBootstrap.UserData.BackupsPath,
        "Fallback project storage was mixed with an editor service tree.");

    Environment.SetEnvironmentVariable(ReleaseProjectBootstrap.WorkspaceEnvironmentVariable, "");
    ReleaseProjectContext bootstrap = await ReleaseProjectBootstrap.PrepareAsync(
        appVersion: "0.1.0-beta.26",
        appBaseDirectory: portableRoot,
        userData: userData,
        explicitInstallRoot: portableRoot,
        forceReleaseMode: true) ?? throw new InvalidOperationException("Release project bootstrap did not run.");
    Assert(!bootstrap.Project.RootPath.StartsWith(Path.GetFullPath(portableRoot), StringComparison.OrdinalIgnoreCase), "Release workspace remained inside the replaceable installation.");
    Assert(Environment.GetEnvironmentVariable(ReleaseProjectBootstrap.WorkspaceEnvironmentVariable) == bootstrap.Project.RootPath, "Release workspace environment was not redirected.");
    Assert(File.Exists(Path.Combine(bootstrap.Project.RootPath, "support", "spyro-level-catalog.json")), "Versioned support files were not synchronized into the external project.");
    string retiredSupportPath = Path.Combine(bootstrap.Project.RootPath, "support", "docs", "retired-in-next-release.md");
    Assert(File.Exists(retiredSupportPath), "Initial app-owned support file was not synchronized.");
    Assert(!File.Exists(Path.Combine(bootstrap.Project.RootPath, "support", "app", "must-not-copy.dll")), "Application binaries leaked into the project support copy.");
    Assert(File.Exists(Path.Combine(bootstrap.Project.RootPath, "artisans-native-edits.json")), "Portable edits were not automatically migrated from an in-place legacy beta.");
    Assert(!File.Exists(Path.Combine(bootstrap.Project.RootPath, "output", "My Build.bin")), "Multi-gigabyte generated output should be opt-in during automatic migration.");
    Assert(File.Exists(Path.Combine(portableRoot, "artisans-native-edits.json")), "Automatic migration deleted legacy project data.");
    Assert(File.Exists(Path.Combine(userData.SettingsPath, "current-project.json")), "Current external project was not remembered outside the installation.");

    await AssertInvalidOperationAsync(
        async () => await ReleaseProjectBootstrap.PrepareAndRememberProjectAsync(portableRoot),
        "PrepareAndRememberProjectAsync accepted the replaceable installation as a project.");
    string rejectedProjectDescendant = Path.Combine(portableRoot, "Projects", "rejected-by-open-project");
    await AssertInvalidOperationAsync(
        async () => await ReleaseProjectBootstrap.PrepareAndRememberProjectAsync(rejectedProjectDescendant),
        "PrepareAndRememberProjectAsync accepted a project inside the replaceable installation.");
    Assert(!Directory.Exists(rejectedProjectDescendant), "Rejected project descendant was created before validation.");

    foreach ((string serviceName, string servicePath) in new[]
    {
        ("settings", userData.SettingsPath),
        ("updates", userData.UpdatesPath),
        ("backups", userData.BackupsPath)
    })
    {
        await AssertInvalidOperationAsync(
            async () => await ReleaseProjectBootstrap.PrepareAndRememberProjectAsync(servicePath),
            $"PrepareAndRememberProjectAsync accepted a project overlapping {serviceName} storage.");
        Assert(
            !File.Exists(Path.Combine(servicePath, "spyro-project.json")),
            $"A rejected project modified {serviceName} storage.");
    }

    string invalidImportWithoutLauncher = Path.Combine(temporaryRoot, "invalid-import-without-launcher");
    Write(Path.Combine(invalidImportWithoutLauncher, "support", "spyro-level-catalog.json"), "{}");
    Write(Path.Combine(invalidImportWithoutLauncher, "support", "spyro-object-templates.json"), "{}");
    Write(Path.Combine(invalidImportWithoutLauncher, "townsquare-native-edits.json"), "{}");
    await AssertInvalidDataAsync(
        async () => await ReleaseProjectBootstrap.ImportPortableProjectAsync(
            invalidImportWithoutLauncher,
            includeGeneratedOutputs: false),
        "Manual import accepted a folder without a known Spyro Editor launcher.");
    Assert(
        !File.Exists(Path.Combine(bootstrap.Project.RootPath, "townsquare-native-edits.json")),
        "A rejected non-beta folder copied data into the active project.");

    string invalidImportWithoutCatalogs = Path.Combine(temporaryRoot, "invalid-import-without-catalogs");
    Write(Path.Combine(invalidImportWithoutCatalogs, "Launch Spyro Editor.command"), "#!/bin/zsh\n");
    Write(Path.Combine(invalidImportWithoutCatalogs, "darkhollow-native-edits.json"), "{}");
    await AssertInvalidDataAsync(
        async () => await ReleaseProjectBootstrap.ImportPortableProjectAsync(
            invalidImportWithoutCatalogs,
            includeGeneratedOutputs: false),
        "Manual import accepted a folder without the Spyro Editor support catalogs.");
    Assert(
        !File.Exists(Path.Combine(bootstrap.Project.RootPath, "darkhollow-native-edits.json")),
        "A rejected folder without catalogs copied data into the active project.");

    string validManualImportRoot = Path.Combine(temporaryRoot, "valid-manual-beta-import");
    Write(Path.Combine(validManualImportRoot, "Launch Spyro Editor.bat"), "@echo off\n");
    Write(Path.Combine(validManualImportRoot, "support", "spyro-level-catalog.json"), "{}");
    Write(Path.Combine(validManualImportRoot, "support", "spyro-object-templates.json"), "{}");
    Write(Path.Combine(validManualImportRoot, "magiccrafters-native-edits.json"), "{}");
    Write(Path.Combine(validManualImportRoot, "_local", "research", "proof.json"), "{}");
    Write(Path.Combine(validManualImportRoot, "_local", "skybox-research", "proof.json"), "{}");
    Write(Path.Combine(validManualImportRoot, "_local", "release-qa", "proof.json"), "{}");
    PortableProjectMigrationResult manualImport = await ReleaseProjectBootstrap.ImportPortableProjectAsync(
        validManualImportRoot,
        includeGeneratedOutputs: false,
        includeUserResearch: true);
    Assert(manualImport.CopiedCount == 4, $"Expected four manual-import files, got {manualImport.CopiedCount}.");
    Assert(
        File.Exists(Path.Combine(bootstrap.Project.RootPath, "magiccrafters-native-edits.json")) &&
        File.Exists(Path.Combine(bootstrap.Project.RootPath, "_local", "research", "proof.json")) &&
        File.Exists(Path.Combine(bootstrap.Project.RootPath, "_local", "skybox-research", "proof.json")) &&
        File.Exists(Path.Combine(bootstrap.Project.RootPath, "_local", "release-qa", "proof.json")),
        "A validated previous-beta import omitted project edits or user research.");

    Write(Path.Combine(bootstrap.Project.RootPath, "support", "user-notes.txt"), "user owned");
    File.Delete(Path.Combine(portableRoot, "support", "docs", "retired-in-next-release.md"));
    Write(Path.Combine(portableRoot, "support", "docs", "replacement-guide.md"), "replacement guide");
    await ReleaseProjectBootstrap.PrepareAndRememberProjectAsync(bootstrap.Project.RootPath);
    Assert(!File.Exists(retiredSupportPath), "A retired app-owned support file survived a successful synchronization.");
    Assert(File.Exists(Path.Combine(bootstrap.Project.RootPath, "support", "docs", "replacement-guide.md")), "New support files were not copied before pruning.");
    Assert(File.Exists(Path.Combine(bootstrap.Project.RootPath, "support", "user-notes.txt")), "Support pruning removed a file the app did not own.");

    if (OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
        await VerifySymlinkEscapeProtectionAsync(temporaryRoot, portableRoot, bootstrap.Project.RootPath);

    ReleaseProjectContext secondBootstrap = await ReleaseProjectBootstrap.PrepareAsync(
        appVersion: "0.1.0-beta.26",
        appBaseDirectory: portableRoot,
        userData: userData,
        explicitInstallRoot: portableRoot,
        forceReleaseMode: true) ?? throw new InvalidOperationException("Second release project bootstrap did not run.");
    Assert(secondBootstrap.AutomaticMigration == null, "A completed legacy migration repeated on the next launch.");
    Assert(
        Directory.EnumerateFiles(Path.Combine(bootstrap.Project.MigrationPath, "automatic-sources"), "*.json").Count() == 1,
        "The completed automatic migration was not recorded exactly once.");
    string renamedProjectRoot = Path.Combine(temporaryRoot, "renamed-project-folder");
    Write(
        Path.Combine(renamedProjectRoot, "spyro-project.json"),
        System.Text.Json.JsonSerializer.Serialize(new EditorProjectManifest(
            SchemaVersion: 1,
            ProjectId: "stable-project-id",
            DisplayName: "Renamed Project",
            CreatedAtUtc: DateTimeOffset.UtcNow,
            LastOpenedAtUtc: DateTimeOffset.UtcNow,
            LastEditorVersion: "0.1.0-beta.25",
            MigratedFrom: "")));
    EditorProjectLayout renamedProject = await ReleaseProjectBootstrap.PrepareAndRememberProjectAsync(renamedProjectRoot);
    Assert(renamedProject.ProjectId == "stable-project-id", "Renaming a valid project folder changed its stable manifest identity.");

    string unsafeProjectRoot = Path.Combine(temporaryRoot, "unsafe-project-manifest");
    Write(
        Path.Combine(unsafeProjectRoot, "spyro-project.json"),
        System.Text.Json.JsonSerializer.Serialize(new EditorProjectManifest(
            SchemaVersion: 1,
            ProjectId: "../../escape",
            DisplayName: "Unsafe Project",
            CreatedAtUtc: DateTimeOffset.UtcNow,
            LastOpenedAtUtc: DateTimeOffset.UtcNow,
            LastEditorVersion: "0.1.0-beta.25",
            MigratedFrom: "")));
    bool unsafeManifestRejected = false;
    try
    {
        await ReleaseProjectBootstrap.PrepareAndRememberProjectAsync(unsafeProjectRoot);
    }
    catch (InvalidDataException)
    {
        unsafeManifestRejected = true;
    }
    Assert(unsafeManifestRejected, "An unsafe manifest project ID could escape the backup destination.");
    await ReleaseProjectBootstrap.PrepareAndRememberProjectAsync(bootstrap.Project.RootPath);
    string futureProjectFile = Path.Combine(bootstrap.Project.RootPath, "future-editor-state.projectdata");
    string preservedConflictAsset = Path.Combine(
        bootstrap.Project.MigrationPath,
        "conflicts",
        "manual-proof",
        "custom-skyboxes",
        "artisans",
        "preserved.sky");
    string excludedWad = Path.Combine(bootstrap.Project.RootPath, "WAD.WAD");
    Write(futureProjectFile, "future project state");
    Write(preservedConflictAsset, "preserved conflict asset");
    Write(excludedWad, "game data fixture");
    string overlappingBackupRoot = Path.Combine(bootstrap.Project.RootPath, "unsafe-backups");
    await AssertInvalidOperationAsync(
        async () => await ProjectSnapshotService.CreateAsync(
            bootstrap.Project,
            overlappingBackupRoot,
            "must-reject"),
        "ProjectSnapshotService accepted backup storage inside the project.");
    Assert(!Directory.Exists(overlappingBackupRoot), "Rejected in-project backup storage was created.");
    ProjectSnapshotResult snapshot = await ProjectSnapshotService.CreateAsync(
        bootstrap.Project,
        Path.Combine(userData.RootPath, "Backups"),
        "pre-update-beta-27");
    Assert(File.Exists(snapshot.SnapshotPath) && File.Exists(snapshot.Sha256Path), "Verified project safety snapshot was not created.");
    using (ZipArchive snapshotArchive = ZipFile.OpenRead(snapshot.SnapshotPath))
    {
        Assert(snapshotArchive.GetEntry("artisans-native-edits.json") != null, "Project snapshot omitted saved object edits.");
        Assert(snapshotArchive.GetEntry("future-editor-state.projectdata") != null, "Project snapshot omitted an unclassified future project file.");
        Assert(snapshotArchive.GetEntry("_migration/conflicts/manual-proof/custom-skyboxes/artisans/preserved.sky") != null, "Project snapshot omitted a preserved migration conflict asset.");
        Assert(snapshotArchive.GetEntry("WAD.WAD") == null, "Project snapshot included source game data with an unclassified WAD path.");
        Assert(snapshotArchive.GetEntry("_local/.app/support-ownership.json") == null, "Project snapshot included replaceable application bookkeeping.");
        Assert(snapshotArchive.Entries.All(entry => !entry.FullName.StartsWith("output/", StringComparison.OrdinalIgnoreCase)), "Project snapshot bundled generated output.");
        Assert(snapshotArchive.Entries.All(entry => !entry.FullName.StartsWith("support/", StringComparison.OrdinalIgnoreCase)), "Project snapshot bundled replaceable application support files.");
    }
    string snapshotHash = Convert.ToHexString(
        System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(snapshot.SnapshotPath)))
        .ToLowerInvariant();
    Assert(snapshotHash == snapshot.Sha256, "Project snapshot result did not report the ZIP's actual SHA-256.");
    Assert(File.ReadAllText(snapshot.Sha256Path).StartsWith(snapshot.Sha256, StringComparison.Ordinal), "Project snapshot SHA-256 sidecar did not match the verified ZIP.");

    Console.WriteLine("PASS: external project layout, inventory, non-destructive migration, path rebasing, output preservation, cache exclusion, and conflict preservation.");
    Console.WriteLine("PASS: release bootstrap redirects writes, synchronizes support resources, and copy-migrates legacy edits without moving application files or generated BINs.");
    Console.WriteLine("PASS: install/project overlap, physical symlink escapes, nested write symlinks, and reverse migration overlap are rejected before unsafe writes.");
    Console.WriteLine("PASS: support synchronization prunes retired app-owned files while preserving unowned project support files.");
    Console.WriteLine("PASS: pre-update snapshot preserves project-authored data while excluding game output, cache, and application files.");
    Console.WriteLine("PASS: release data locators remain project-local while research discovery can inspect sibling workspaces.");
    Console.WriteLine("PASS: research mode safely opens the registered protected release project and falls back without mutation when registration is invalid.");
    Console.WriteLine($"Project: {project.RootPath}");
    Console.WriteLine($"Inventory: {first.Inventory.Count} files; migrated: {first.CopiedCount}; skipped: {first.SkippedCount}");
}
finally
{
    Environment.SetEnvironmentVariable(ReleaseProjectBootstrap.WorkspaceEnvironmentVariable, previousWorkspace);
    Environment.SetEnvironmentVariable(ReleaseProjectBootstrap.InstallRootEnvironmentVariable, previousInstall);
    Environment.SetEnvironmentVariable(ReleaseProjectBootstrap.ReleaseEnvironmentVariable, previousRelease);
    Environment.SetEnvironmentVariable(
        ResearchProjectWorkspaceBridge.UseCurrentReleaseProjectEnvironmentVariable,
        previousResearchProjectBridge);
    if (Directory.Exists(temporaryRoot))
        Directory.Delete(temporaryRoot, recursive: true);
}

static void Write(string path, string contents)
{
    Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
    File.WriteAllText(path, contents);
}

static string Json(string value)
{
    return System.Text.Json.JsonSerializer.Serialize(value);
}

static async Task AssertInvalidOperationAsync(Func<Task> action, string message)
{
    bool rejected = false;
    try
    {
        await action();
    }
    catch (InvalidOperationException)
    {
        rejected = true;
    }
    Assert(rejected, message);
}

static async Task AssertInvalidDataAsync(Func<Task> action, string message)
{
    bool rejected = false;
    try
    {
        await action();
    }
    catch (InvalidDataException)
    {
        rejected = true;
    }
    Assert(rejected, message);
}

static void VerifyResearchWorkspaceBridge(string temporaryRoot, string installRoot)
{
    string? previousWorkspace = Environment.GetEnvironmentVariable(
        ReleaseProjectBootstrap.WorkspaceEnvironmentVariable);
    string? previousRelease = Environment.GetEnvironmentVariable(
        ReleaseProjectBootstrap.ReleaseEnvironmentVariable);
    string? previousBridge = Environment.GetEnvironmentVariable(
        ResearchProjectWorkspaceBridge.UseCurrentReleaseProjectEnvironmentVariable);
    string bridgeDataRoot = Path.Combine(temporaryRoot, "research-bridge-user-data");
    EditorUserDataLayout bridgeUserData = new(
        bridgeDataRoot,
        Path.Combine(temporaryRoot, "research-bridge-projects"));
    string projectRoot = Path.Combine(temporaryRoot, "Protected Research Project With Spaces");
    string manifestPath = Path.Combine(projectRoot, "spyro-project.json");
    string sentinelPath = Path.Combine(projectRoot, "artisans-native-edits.json");
    string settingsPath = Path.Combine(bridgeUserData.SettingsPath, "current-project.json");

    try
    {
        Write(
            manifestPath,
            System.Text.Json.JsonSerializer.Serialize(new EditorProjectManifest(
                SchemaVersion: 1,
                ProjectId: "protected-research-project",
                DisplayName: "Protected Research Project",
                CreatedAtUtc: DateTimeOffset.UtcNow,
                LastOpenedAtUtc: DateTimeOffset.UtcNow,
                LastEditorVersion: "Spyro Editor Beta V4",
                MigratedFrom: "")));
        Write(sentinelPath, "{\"savedReleaseEdit\":true}");
        Write(Path.Combine(projectRoot, "support", "spyro-level-catalog.json"), "{}");
        Write(Path.Combine(projectRoot, "support", "spyro-object-templates.json"), "{}");
        Write(
            settingsPath,
            System.Text.Json.JsonSerializer.Serialize(new CurrentProjectSettings(
                Version: 1,
                ProjectId: "protected-research-project",
                ProjectRoot: projectRoot,
                SavedAtUtc: DateTimeOffset.UtcNow)));

        Environment.SetEnvironmentVariable(
            ResearchProjectWorkspaceBridge.UseCurrentReleaseProjectEnvironmentVariable,
            "1");
        Environment.SetEnvironmentVariable(
            ReleaseProjectBootstrap.ReleaseEnvironmentVariable,
            "0");
        Environment.SetEnvironmentVariable(
            ReleaseProjectBootstrap.WorkspaceEnvironmentVariable,
            installRoot);
        byte[] manifestBefore = File.ReadAllBytes(manifestPath);
        byte[] sentinelBefore = File.ReadAllBytes(sentinelPath);
        ResearchProjectWorkspaceBridgeResult accepted = ResearchProjectWorkspaceBridge.TryActivate(
            appBaseDirectory: installRoot,
            userData: bridgeUserData,
            explicitInstallRoot: installRoot);
        Assert(accepted.Enabled && accepted.Activated, $"A valid protected release project was not bridged: {accepted.Reason}");
        Assert(
            Environment.GetEnvironmentVariable(ReleaseProjectBootstrap.WorkspaceEnvironmentVariable) ==
            Path.GetFullPath(projectRoot),
            "Research mode did not select the protected release project.");
        Assert(
            Environment.GetEnvironmentVariable(ReleaseProjectBootstrap.ReleaseEnvironmentVariable) == "0",
            "Selecting the protected project changed research mode into release mode.");
        Assert(
            manifestBefore.AsSpan().SequenceEqual(File.ReadAllBytes(manifestPath)) &&
            sentinelBefore.AsSpan().SequenceEqual(File.ReadAllBytes(sentinelPath)),
            "The read-only research bridge modified protected project data.");

        static void ResetFallback(string installRoot) =>
            Environment.SetEnvironmentVariable(
                ReleaseProjectBootstrap.WorkspaceEnvironmentVariable,
                installRoot);

        static void AssertFallback(
            ResearchProjectWorkspaceBridgeResult result,
            string installRoot,
            string message)
        {
            Assert(result.Enabled && !result.Activated, message);
            Assert(
                Environment.GetEnvironmentVariable(ReleaseProjectBootstrap.WorkspaceEnvironmentVariable) ==
                installRoot,
                "A rejected research project changed the package-workspace fallback.");
        }

        Write(settingsPath, "{not json");
        ResetFallback(installRoot);
        AssertFallback(
            ResearchProjectWorkspaceBridge.TryActivate(installRoot, bridgeUserData, installRoot),
            installRoot,
            "Malformed current-project settings were accepted.");

        Write(
            settingsPath,
            System.Text.Json.JsonSerializer.Serialize(new CurrentProjectSettings(
                Version: 1,
                ProjectId: "protected-research-project",
                ProjectRoot: "relative-project",
                SavedAtUtc: DateTimeOffset.UtcNow)));
        ResetFallback(installRoot);
        AssertFallback(
            ResearchProjectWorkspaceBridge.TryActivate(installRoot, bridgeUserData, installRoot),
            installRoot,
            "A relative registered project path was accepted.");

        Write(
            settingsPath,
            System.Text.Json.JsonSerializer.Serialize(new CurrentProjectSettings(
                Version: 1,
                ProjectId: "protected-research-project",
                ProjectRoot: projectRoot,
                SavedAtUtc: DateTimeOffset.UtcNow)));
        Write(
            manifestPath,
            System.Text.Json.JsonSerializer.Serialize(new EditorProjectManifest(
                SchemaVersion: EditorUserDataLayout.CurrentProjectSchemaVersion + 1,
                ProjectId: "protected-research-project",
                DisplayName: "Future Project",
                CreatedAtUtc: DateTimeOffset.UtcNow,
                LastOpenedAtUtc: DateTimeOffset.UtcNow,
                LastEditorVersion: "future",
                MigratedFrom: "")));
        ResetFallback(installRoot);
        AssertFallback(
            ResearchProjectWorkspaceBridge.TryActivate(installRoot, bridgeUserData, installRoot),
            installRoot,
            "A future project schema was accepted by the research bridge.");

        Write(
            manifestPath,
            System.Text.Json.JsonSerializer.Serialize(new EditorProjectManifest(
                SchemaVersion: 1,
                ProjectId: "different-project-id",
                DisplayName: "Wrong Identity",
                CreatedAtUtc: DateTimeOffset.UtcNow,
                LastOpenedAtUtc: DateTimeOffset.UtcNow,
                LastEditorVersion: "Spyro Editor Beta V4",
                MigratedFrom: "")));
        ResetFallback(installRoot);
        AssertFallback(
            ResearchProjectWorkspaceBridge.TryActivate(installRoot, bridgeUserData, installRoot),
            installRoot,
            "A mismatched project identity was accepted by the research bridge.");

        File.Delete(Path.Combine(projectRoot, "support", "spyro-object-templates.json"));
        File.WriteAllBytes(manifestPath, manifestBefore);
        ResetFallback(installRoot);
        AssertFallback(
            ResearchProjectWorkspaceBridge.TryActivate(installRoot, bridgeUserData, installRoot),
            installRoot,
            "A project missing required support files was accepted.");

        string overlappingProject = Path.Combine(installRoot, "unsafe-research-bridge-project");
        Write(
            Path.Combine(overlappingProject, "spyro-project.json"),
            System.Text.Json.JsonSerializer.Serialize(new EditorProjectManifest(
                SchemaVersion: 1,
                ProjectId: "unsafe-research-bridge-project",
                DisplayName: "Unsafe Project",
                CreatedAtUtc: DateTimeOffset.UtcNow,
                LastOpenedAtUtc: DateTimeOffset.UtcNow,
                LastEditorVersion: "Spyro Editor Beta V4",
                MigratedFrom: "")));
        Write(Path.Combine(overlappingProject, "support", "spyro-level-catalog.json"), "{}");
        Write(Path.Combine(overlappingProject, "support", "spyro-object-templates.json"), "{}");
        Write(
            settingsPath,
            System.Text.Json.JsonSerializer.Serialize(new CurrentProjectSettings(
                Version: 1,
                ProjectId: "unsafe-research-bridge-project",
                ProjectRoot: overlappingProject,
                SavedAtUtc: DateTimeOffset.UtcNow)));
        ResetFallback(installRoot);
        AssertFallback(
            ResearchProjectWorkspaceBridge.TryActivate(installRoot, bridgeUserData, installRoot),
            installRoot,
            "A project overlapping the replaceable research installation was accepted.");

        File.Delete(settingsPath);
        ResetFallback(installRoot);
        AssertFallback(
            ResearchProjectWorkspaceBridge.TryActivate(installRoot, bridgeUserData, installRoot),
            installRoot,
            "Missing current-project settings did not retain the package fallback.");

        Environment.SetEnvironmentVariable(
            ReleaseProjectBootstrap.ReleaseEnvironmentVariable,
            "1");
        ResetFallback(installRoot);
        AssertFallback(
            ResearchProjectWorkspaceBridge.TryActivate(installRoot, bridgeUserData, installRoot),
            installRoot,
            "The research bridge activated while the editor was in release mode.");
    }
    finally
    {
        Environment.SetEnvironmentVariable(
            ReleaseProjectBootstrap.WorkspaceEnvironmentVariable,
            previousWorkspace);
        Environment.SetEnvironmentVariable(
            ReleaseProjectBootstrap.ReleaseEnvironmentVariable,
            previousRelease);
        Environment.SetEnvironmentVariable(
            ResearchProjectWorkspaceBridge.UseCurrentReleaseProjectEnvironmentVariable,
            previousBridge);
    }
}

static async Task VerifySymlinkEscapeProtectionAsync(
    string temporaryRoot,
    string installRoot,
    string activeProjectRoot)
{
    string physicalProjectInsideInstall = Path.Combine(installRoot, "physical-project-inside-install");
    Directory.CreateDirectory(physicalProjectInsideInstall);
    string linkedProjectPath = Path.Combine(temporaryRoot, "linked-project-inside-install");
    Directory.CreateSymbolicLink(linkedProjectPath, physicalProjectInsideInstall);
    string? workspaceBeforeLinkedProjectTest = Environment.GetEnvironmentVariable(ReleaseProjectBootstrap.WorkspaceEnvironmentVariable);
    try
    {
        Environment.SetEnvironmentVariable(ReleaseProjectBootstrap.WorkspaceEnvironmentVariable, linkedProjectPath);
        await AssertInvalidOperationAsync(
            async () => await ReleaseProjectBootstrap.PrepareAsync(
                appVersion: "0.1.0-beta.26",
                appBaseDirectory: installRoot,
                userData: new EditorUserDataLayout(Path.Combine(temporaryRoot, "linked-project-bootstrap-data")),
                explicitInstallRoot: installRoot,
                forceReleaseMode: true),
            "PrepareAsync accepted a project symlink physically resolving inside the replaceable installation.");
        await AssertInvalidOperationAsync(
            async () => await ReleaseProjectBootstrap.PrepareAndRememberProjectAsync(linkedProjectPath),
            "A project symlink physically resolving inside the replaceable installation was accepted.");
        Assert(!File.Exists(Path.Combine(physicalProjectInsideInstall, "spyro-project.json")), "Rejected symlinked project storage was modified.");
    }
    finally
    {
        Environment.SetEnvironmentVariable(
            ReleaseProjectBootstrap.WorkspaceEnvironmentVariable,
            workspaceBeforeLinkedProjectTest ?? activeProjectRoot);
        Directory.Delete(linkedProjectPath);
    }

    string migrationSourceRoot = Path.Combine(temporaryRoot, "symlink-migration-source");
    Write(Path.Combine(migrationSourceRoot, "custom-skyboxes", "artisans", "escape.sky"), "sky bytes");
    string migrationProjectRoot = Path.Combine(temporaryRoot, "symlink-migration-project");
    Directory.CreateDirectory(migrationProjectRoot);
    string escapedMigrationTarget = Path.Combine(temporaryRoot, "escaped-migration-target");
    Directory.CreateDirectory(escapedMigrationTarget);
    string linkedAssetDirectory = Path.Combine(migrationProjectRoot, "custom-skyboxes");
    Directory.CreateSymbolicLink(linkedAssetDirectory, escapedMigrationTarget);
    try
    {
        await AssertInvalidOperationAsync(
            async () => await PortableProjectMigration.MigrateAsync(
                migrationSourceRoot,
                new EditorProjectLayout(migrationProjectRoot, "symlink-migration-project")),
            "Migration followed a nested project symlink outside the project.");
        Assert(!File.Exists(Path.Combine(escapedMigrationTarget, "artisans", "escape.sky")), "Migration wrote through a nested project symlink.");
    }
    finally
    {
        Directory.Delete(linkedAssetDirectory);
    }

    string migrationMetadataProjectRoot = Path.Combine(temporaryRoot, "symlink-migration-metadata-project");
    Directory.CreateDirectory(migrationMetadataProjectRoot);
    string escapedMigrationMetadata = Path.Combine(temporaryRoot, "escaped-migration-metadata");
    Directory.CreateDirectory(escapedMigrationMetadata);
    string linkedMigrationDirectory = Path.Combine(migrationMetadataProjectRoot, "_migration");
    Directory.CreateSymbolicLink(linkedMigrationDirectory, escapedMigrationMetadata);
    try
    {
        await AssertInvalidOperationAsync(
            async () => await PortableProjectMigration.MigrateAsync(
                migrationSourceRoot,
                new EditorProjectLayout(migrationMetadataProjectRoot, "symlink-migration-metadata-project")),
            "Migration wrote reports or conflicts through a nested _migration symlink.");
        Assert(!Directory.EnumerateFileSystemEntries(escapedMigrationMetadata).Any(), "Migration metadata escaped the project root.");
    }
    finally
    {
        Directory.Delete(linkedMigrationDirectory);
    }

    string supportProjectRoot = Path.Combine(temporaryRoot, "symlink-support-project");
    string supportRoot = Path.Combine(supportProjectRoot, "support");
    Directory.CreateDirectory(supportRoot);
    string escapedSupportTarget = Path.Combine(temporaryRoot, "escaped-support-target");
    Directory.CreateDirectory(escapedSupportTarget);
    string linkedDocsDirectory = Path.Combine(supportRoot, "docs");
    Directory.CreateSymbolicLink(linkedDocsDirectory, escapedSupportTarget);
    try
    {
        await AssertInvalidOperationAsync(
            async () => await ReleaseProjectBootstrap.PrepareAndRememberProjectAsync(supportProjectRoot),
            "Support synchronization followed a nested project symlink outside the project.");
        Assert(!File.Exists(Path.Combine(escapedSupportTarget, "release-user-guide.md")), "Support synchronization wrote outside the project.");
    }
    finally
    {
        Directory.Delete(linkedDocsDirectory);
    }

    string outsideManifestPath = Path.Combine(temporaryRoot, "outside-project-manifest.json");
    string outsideManifestContents = System.Text.Json.JsonSerializer.Serialize(new EditorProjectManifest(
        SchemaVersion: 1,
        ProjectId: "linked-manifest-project",
        DisplayName: "Linked Manifest Project",
        CreatedAtUtc: DateTimeOffset.UtcNow,
        LastOpenedAtUtc: DateTimeOffset.UtcNow,
        LastEditorVersion: "0.1.0-beta.25",
        MigratedFrom: ""));
    Write(outsideManifestPath, outsideManifestContents);
    string linkedManifestProjectRoot = Path.Combine(temporaryRoot, "linked-manifest-project");
    Directory.CreateDirectory(linkedManifestProjectRoot);
    string linkedManifestPath = Path.Combine(linkedManifestProjectRoot, "spyro-project.json");
    File.CreateSymbolicLink(linkedManifestPath, outsideManifestPath);
    try
    {
        await AssertInvalidOperationAsync(
            async () => await ReleaseProjectBootstrap.PrepareAndRememberProjectAsync(linkedManifestProjectRoot),
            "Project preparation followed a manifest symlink outside the project.");
        Assert(File.ReadAllText(outsideManifestPath) == outsideManifestContents, "Project preparation modified an external manifest target.");
    }
    finally
    {
        File.Delete(linkedManifestPath);
    }

    string automaticMarkerProjectRoot = Path.Combine(temporaryRoot, "automatic-marker-symlink-project");
    Write(
        Path.Combine(automaticMarkerProjectRoot, "spyro-project.json"),
        System.Text.Json.JsonSerializer.Serialize(new EditorProjectManifest(
            SchemaVersion: 1,
            ProjectId: "automatic-marker-symlink-project",
            DisplayName: "Automatic Marker Symlink Project",
            CreatedAtUtc: DateTimeOffset.UtcNow,
            LastOpenedAtUtc: DateTimeOffset.UtcNow,
            LastEditorVersion: "0.1.0-beta.25",
            MigratedFrom: "")));
    string escapedMarkerTarget = Path.Combine(temporaryRoot, "escaped-automatic-marker");
    Directory.CreateDirectory(escapedMarkerTarget);
    string linkedAutomaticMigrationDirectory = Path.Combine(automaticMarkerProjectRoot, "_migration");
    Directory.CreateSymbolicLink(linkedAutomaticMigrationDirectory, escapedMarkerTarget);
    string? previousWorkspace = Environment.GetEnvironmentVariable(ReleaseProjectBootstrap.WorkspaceEnvironmentVariable);
    try
    {
        Environment.SetEnvironmentVariable(ReleaseProjectBootstrap.WorkspaceEnvironmentVariable, automaticMarkerProjectRoot);
        await AssertInvalidOperationAsync(
            async () => await ReleaseProjectBootstrap.PrepareAsync(
                appVersion: "0.1.0-beta.26",
                appBaseDirectory: installRoot,
                userData: new EditorUserDataLayout(Path.Combine(temporaryRoot, "automatic-marker-user-data")),
                explicitInstallRoot: installRoot,
                forceReleaseMode: true),
            "Automatic migration followed a nested _migration symlink outside the project.");
        Assert(!Directory.EnumerateFileSystemEntries(escapedMarkerTarget).Any(), "Automatic migration marker escaped the project root.");
    }
    finally
    {
        Environment.SetEnvironmentVariable(ReleaseProjectBootstrap.WorkspaceEnvironmentVariable, previousWorkspace ?? activeProjectRoot);
        Directory.Delete(linkedAutomaticMigrationDirectory);
    }
}

static void VerifyReleaseArtifactIsolation(string root)
{
    string? previousReleaseMode = Environment.GetEnvironmentVariable(WorkspaceArtifactSearchPolicy.ReleaseEnvironmentVariable);
    try
    {
        string projectRoot = Path.Combine(root, "Projects", "Alpha");
        string siblingRoot = Path.Combine(root, "Projects", "Beta");
        Directory.CreateDirectory(projectRoot);
        Directory.CreateDirectory(siblingRoot);
        string siblingWad = Path.Combine(siblingRoot, "spyro-wad-analysis.json");
        string siblingDisc = Path.Combine(siblingRoot, "Spyro the Dragon (USA).bin");
        string siblingRam = Path.Combine(siblingRoot, "artisans-before-clean.bin");
        string siblingTerrain = Path.Combine(siblingRoot, "artisans-runtime-terrain-source-search.json");
        Write(siblingWad, "{}");
        Write(siblingDisc, "disc fixture");
        Write(siblingRam, "ram fixture");
        Write(siblingTerrain, "{}");
        EditorWorkspace workspace = new(projectRoot);

        Environment.SetEnvironmentVariable(WorkspaceArtifactSearchPolicy.ReleaseEnvironmentVariable, "0");
        Assert(WadAnalysisLocator.Find(workspace) == siblingWad, "Research WAD discovery no longer finds sibling workspaces.");
        Assert(DiscImageLocator.FindImage(workspace) == siblingDisc, "Research disc discovery no longer finds sibling workspaces.");
        Assert(TerrainPatchDataLocator.FindRamDump(workspace, "artisans") == siblingRam, "Research RAM discovery no longer finds sibling workspaces.");
        Assert(TerrainPatchDataLocator.FindSourceSearch(workspace, "artisans") == siblingTerrain, "Research terrain discovery no longer finds sibling workspaces.");
        Assert(
            WorkspaceArtifactSearchPolicy.EnumerateCandidateRoots(workspace).Any(path => Path.GetFullPath(path) == Path.GetFullPath(siblingRoot)),
            "Research candidate roots omitted the sibling workspace used by cross-level analysis.");

        Environment.SetEnvironmentVariable(WorkspaceArtifactSearchPolicy.ReleaseEnvironmentVariable, "1");
        Assert(WadAnalysisLocator.Find(workspace) == Path.Combine(projectRoot, "spyro-wad-analysis.json"), "Release WAD lookup escaped the active project.");
        Assert(DiscImageLocator.FindImage(workspace) == Path.Combine(projectRoot, "Spyro the Dragon (USA).bin"), "Release disc lookup escaped the active project.");
        Assert(TerrainPatchDataLocator.FindRamDump(workspace, "artisans") == Path.Combine(projectRoot, "artisans-before-clean.bin"), "Release RAM lookup escaped the active project.");
        Assert(TerrainPatchDataLocator.FindSourceSearch(workspace, "artisans") == Path.Combine(projectRoot, "artisans-runtime-terrain-source-search.json"), "Release terrain lookup escaped the active project.");
        Assert(WorkspaceArtifactSearchPolicy.EnumerateCandidateRoots(workspace).SequenceEqual([workspace.RootPath]), "Release candidate roots included another project.");

        DiscImageSelection explicitlySelected = DiscImageLocator.ConfigureImage(workspace, siblingDisc);
        Assert(explicitlySelected.ImageExists && DiscImageLocator.FindImage(workspace) == siblingDisc, "Release mode rejected the user's explicitly selected external disc.");
    }
    finally
    {
        Environment.SetEnvironmentVariable(WorkspaceArtifactSearchPolicy.ReleaseEnvironmentVariable, previousReleaseMode);
    }
}

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
