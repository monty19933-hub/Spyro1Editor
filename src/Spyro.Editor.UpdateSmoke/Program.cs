using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Updates;

Assert(EditorBetaReleaseVersion.TryParseDisplayName("Spyro Editor Beta V2", out EditorBetaReleaseVersion beta2), "Could not parse public Beta V2 name.");
Assert(EditorBetaReleaseVersion.TryParseReleaseTag("beta-v3", out EditorBetaReleaseVersion beta3), "Could not parse public beta-v3 tag.");
Assert(EditorBetaReleaseVersion.TryParseDisplayName("Spyro Editor Beta V3.1", out EditorBetaReleaseVersion beta31), "Could not parse public Beta V3.1 name.");
Assert(EditorBetaReleaseVersion.TryParseReleaseTag("beta-v3.2", out EditorBetaReleaseVersion beta32), "Could not parse public beta-v3.2 tag.");
Assert(EditorBetaReleaseVersion.TryParse("4.1", out EditorBetaReleaseVersion beta41), "Could not parse canonical public version 4.1.");
Assert(beta3.CompareTo(beta2) > 0, "Beta V3 did not compare above Beta V2.");
Assert(beta31.CompareTo(beta3) > 0 && beta32.CompareTo(beta31) > 0 && beta41.CompareTo(beta32) > 0,
    "Incremental public beta versions did not compare canonically.");
Assert(EditorBetaReleaseVersion.TryParseDisplayName("Spyro Editor Beta V10", out EditorBetaReleaseVersion beta10) && beta10.Number == 10, "Two-digit beta version did not parse numerically.");
Assert(!EditorBetaReleaseVersion.TryParseDisplayName("Spyro Editor Beta V02", out _), "Leading-zero public beta was accepted.");
Assert(!EditorBetaReleaseVersion.TryParse("3.0", out _), "Non-canonical .0 public beta was accepted.");
Assert(!EditorBetaReleaseVersion.TryParse("3.01", out _), "Leading-zero incremental public beta was accepted.");
Assert(!EditorBetaReleaseVersion.TryParse("3.1.0", out _), "Three-component public beta was accepted.");
Assert(!EditorBetaReleaseVersion.TryParse("03.1", out _), "Leading-zero public beta major was accepted.");
Assert(!EditorBetaReleaseVersion.TryParseDisplayName(" Spyro Editor Beta V3.1", out _), "A non-exact public display name was accepted.");
Assert(!EditorBetaReleaseVersion.TryParseReleaseTag("beta-v3.1 ", out _), "A non-exact public release tag was accepted.");
Assert(!EditorBetaReleaseVersion.TryParseReleaseTag("v3", out _), "Non-beta release tag was accepted.");
Assert(EditorSemanticVersion.TryParse("1.2.3-beta.45", out _), "Internal semantic-version parser fixture is malformed.");
Assert(EditorUpdateNotificationPolicy.ShouldShow(2, 3, 0), "Beta V3 did not trigger the first V2 notification.");
Assert(!EditorUpdateNotificationPolicy.ShouldShow(2, 3, 3), "Beta V3 notification repeated after it was presented.");
Assert(EditorUpdateNotificationPolicy.ShouldShow(2, 4, 3), "Beta V4 did not trigger after Beta V3 was presented.");
Assert(!EditorUpdateNotificationPolicy.ShouldShow(3, 2, 0), "An older beta triggered an update notification.");
Assert(EditorUpdateNotificationPolicy.ShouldShow(beta3, beta31, beta3), "Beta V3.1 did not trigger after Beta V3.");
Assert(!EditorUpdateNotificationPolicy.ShouldShow(beta3, beta31, beta31), "Beta V3.1 notification repeated after it was presented.");
Assert(EditorUpdateNotificationPolicy.ShouldShow(beta31, beta32, beta31), "Beta V3.2 did not trigger after Beta V3.1.");
DateTimeOffset updateCheckNow = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
Assert(
    EditorUpdateNotificationPolicy.IsRecentCheck(updateCheckNow, updateCheckNow - TimeSpan.FromHours(1), TimeSpan.FromHours(24)),
    "A normal one-hour-old update check was not treated as recent.");
Assert(
    !EditorUpdateNotificationPolicy.IsRecentCheck(updateCheckNow, updateCheckNow - TimeSpan.FromHours(24), TimeSpan.FromHours(24)),
    "A check at the maximum age was incorrectly treated as recent.");
Assert(
    !EditorUpdateNotificationPolicy.IsRecentCheck(updateCheckNow, updateCheckNow + TimeSpan.FromMinutes(1), TimeSpan.FromHours(24)),
    "A future update-check timestamp was incorrectly treated as recent.");

(string platform, string appDllRelativePath) = CurrentPlatform();
byte[] validAppDll = ReadFixtureAppDll();
byte[] validIncrementalAppDll = ReadIncrementalFixtureAppDll();
string candidateInternalVersion;
int candidateBeta;
EditorBetaReleaseVersion candidateVersion;
using (MemoryStream identityInput = new(validAppDll, writable: false))
{
    EditorAssemblyReleaseIdentity identity = EditorAssemblyReleaseIdentityReader.Read(identityInput, "update smoke application fixture");
    Assert(identity.AssemblyName == EditorAssemblyReleaseIdentityReader.ExpectedAssemblyName, "Fixture app DLL has the wrong assembly name.");
    Assert(identity.PublicBetaVersion >= 2, "The numbered updater smoke requires Beta V2 or newer app metadata.");
    Assert(identity.HasExplicitPublicVersion, "The release app fixture is missing full public-version metadata.");
    Assert(identity.HasExplicitReleaseManifestSchema, "The release app fixture is missing release-manifest schema metadata.");
    Assert(identity.ReleaseManifestSchema == 1 && !identity.PublicVersion.IsIncremental,
        "The V4 bridge fixture must retain whole-number schema-1 identity for legacy-client compatibility.");
    Assert(EditorSemanticVersion.TryParse(identity.InternalVersion, out _), "Fixture app DLL informational version is not a semantic version.");
    candidateBeta = identity.PublicBetaVersion;
    candidateVersion = identity.PublicVersion;
    candidateInternalVersion = identity.InternalVersion;
}
string incrementalInternalVersion;
using (MemoryStream identityInput = new(validIncrementalAppDll, writable: false))
{
    EditorAssemblyReleaseIdentity identity = EditorAssemblyReleaseIdentityReader.Read(
        identityInput,
        "incremental update smoke application fixture");
    Assert(identity.AssemblyName == EditorAssemblyReleaseIdentityReader.ExpectedAssemblyName,
        "Incremental fixture app DLL has the wrong assembly name.");
    Assert(identity.PublicBetaVersion == candidateBeta,
        "Incremental fixture app DLL does not retain the current legacy beta major.");
    Assert(identity.PublicVersion == new EditorBetaReleaseVersion(candidateBeta, 1) &&
        identity.HasExplicitPublicVersion &&
        identity.ReleaseManifestSchema == 2 &&
        identity.HasExplicitReleaseManifestSchema,
        "Incremental fixture app DLL does not carry canonical V4.1/schema-2 identity.");
    Assert(EditorSemanticVersion.TryParse(identity.InternalVersion, out _),
        "Incremental fixture app DLL informational version is not semantic.");
    incrementalInternalVersion = identity.InternalVersion;
}
int currentBeta = candidateBeta - 1;
string candidateNotes = $"# {candidateVersion.DisplayName}\n\n- Keeps external projects untouched.\n- Adds the next bundled editor improvements.";
string candidateAssetName = AssetName(candidateBeta, platform);
byte[] validPayload = CreatePackage(candidateBeta, platform, appDllRelativePath, candidateNotes, candidateInternalVersion, validAppDll, includeMacMetadata: true);
string validHash = Hash(validPayload);

object validAsset = Asset(candidateAssetName, validPayload.LongLength, validHash);
string releasesJson = JsonSerializer.Serialize(new object[]
{
    Release(13, new[]
    {
        Asset(AssetName(13, platform), validPayload.LongLength, validHash,
            downloadUrl: $"https://github.com/example/another-repository/releases/download/beta-v13/{AssetName(13, platform)}")
    }),
    Release(12, new[] { Asset(AssetName(12, platform), validPayload.LongLength, validHash) },
        htmlUrl: "https://github.com/example/another-repository/releases/tag/beta-v12"),
    Release(11, new[] { Asset(AssetName(11, platform), validPayload.LongLength, validHash) }, prerelease: false),
    Release(10, new[] { Asset(AssetName(10, platform), GitHubReleaseUpdateClient.MaxUpdatePackageBytes + 1, validHash) }),
    Release(9, new[] { Asset(AssetName(9, platform), 0, validHash) }),
    Release(8, new[]
    {
        Asset(AssetName(8, platform), validPayload.LongLength, validHash),
        Asset(AssetName(8, platform), validPayload.LongLength, validHash)
    }),
    Release(7, new[] { Asset(AssetName(7, platform), validPayload.LongLength, validHash) }, htmlUrl: "https://example.com/not-github"),
    Release(6, new[]
    {
        Asset(AssetName(6, platform), validPayload.LongLength, validHash, downloadUrl: "https://example.com/not-github.zip")
    }),
    Release(5, new[] { Asset(AssetName(5, platform), validPayload.LongLength, validHash) }, tagOverride: "beta-v4"),
    Release(4, new[] { Asset(AssetName(4, platform), validPayload.LongLength, validHash) }, body: ""),
    Release(candidateBeta, new[]
    {
        Asset($"Legacy-{candidateAssetName}", validPayload.LongLength, validHash),
        Asset(AssetName(99, platform), validPayload.LongLength, validHash),
        validAsset
    }, body: candidateNotes),
    Release(currentBeta, Array.Empty<object>())
});

using FixtureHandler discoveryHandler = new(releasesJson, validPayload);
using HttpClient http = new(discoveryHandler);
GitHubReleaseUpdateClient client = new(http);
EditorUpdateInfo update = await client.CheckAsync(currentBeta)
    ?? throw new InvalidOperationException("Expected numbered beta update was not found.");
Assert(update.BetaVersion == candidateBeta, $"Unexpected public beta selected: {update.BetaVersion}");
Assert(update.PublicVersion == candidateVersion.CanonicalVersion, $"Unexpected canonical public version selected: {update.PublicVersion}");
Assert(update.ReleaseTag == candidateVersion.ReleaseTag, "Updater did not retain the exact public release tag.");
Assert(update.DisplayName == candidateVersion.DisplayName, "Updater did not retain the friendly release name.");
Assert(update.ReleaseNotes == candidateNotes, "Updater did not retain the GitHub changelog.");
Assert(update.Asset.Name == candidateAssetName, "Updater did not select the exact public-beta platform asset name.");
Assert(update.Asset.Sha256 == validHash, "GitHub asset SHA-256 was not normalized.");
Assert(update.Asset.Size == validPayload.LongLength, "GitHub asset size was not retained.");
Assert(await client.CheckAsync(candidateBeta) == null, "Current public beta was offered as an update.");

EditorBetaReleaseVersion incrementalOne = new(candidateBeta, 1);
EditorBetaReleaseVersion incrementalTwo = new(candidateBeta, 2);
string incrementalJson = JsonSerializer.Serialize(new object[]
{
    IncrementalRelease(incrementalOne, new[]
    {
        Asset(AssetNameForVersion(incrementalOne, platform), validPayload.LongLength, validHash)
    }),
    IncrementalRelease(incrementalTwo, new[]
    {
        Asset(AssetNameForVersion(incrementalTwo, platform), validPayload.LongLength, validHash)
    }),
    IncrementalRelease(candidateVersion, Array.Empty<object>())
});
using (FixtureHandler incrementalHandler = new(incrementalJson, validPayload))
using (HttpClient incrementalHttp = new(incrementalHandler))
{
    GitHubReleaseUpdateClient incrementalClient = new(incrementalHttp);
    EditorUpdateInfo fromWhole = await incrementalClient.CheckAsync(candidateVersion)
        ?? throw new InvalidOperationException("The current whole-number beta did not discover a same-major incremental update.");
    Assert(fromWhole.PublicVersion == incrementalTwo.CanonicalVersion,
        "The current whole-number beta did not choose the newest available same-major incremental release.");
    EditorUpdateInfo fromFirstIncrement = await incrementalClient.CheckAsync(incrementalOne)
        ?? throw new InvalidOperationException("The first current-major increment did not discover the second increment.");
    Assert(fromFirstIncrement.PublicVersion == incrementalTwo.CanonicalVersion,
        "The first current-major increment did not select the second increment.");
    Assert(await incrementalClient.CheckAsync(incrementalTwo) == null,
        "The current incremental public beta was offered as an update.");
}

string temporaryRoot = Path.Combine(Path.GetTempPath(), $"spyro-editor-update-{Guid.NewGuid():N}");
try
{
    string protectedProjectFile = Path.Combine(temporaryRoot, "protected-project", "stonehill-native-edits.json");
    Directory.CreateDirectory(Path.GetDirectoryName(protectedProjectFile)!);
    byte[] protectedProjectBytes = Encoding.UTF8.GetBytes("{\"savedEdit\":\"must remain byte-identical\"}\n");
    File.WriteAllBytes(protectedProjectFile, protectedProjectBytes);

    string downloaded = await client.DownloadVerifiedAsync(update, Path.Combine(temporaryRoot, "valid"));
    Assert(File.ReadAllBytes(downloaded).AsSpan().SequenceEqual(validPayload), "Verified update download differs from its fixture payload.");
    Assert(discoveryHandler.DownloadRequests == 1, "Valid update was not downloaded exactly once.");
    string reused = await client.DownloadVerifiedAsync(update, Path.Combine(temporaryRoot, "valid"));
    Assert(reused == downloaded, "Existing verified update was not reused.");
    Assert(discoveryHandler.DownloadRequests == 1, "Reusing a verified update unexpectedly downloaded it again.");

    string changelogPath = await GitHubReleaseUpdateClient.WriteChangelogAsync(update, Path.Combine(temporaryRoot, "valid"));
    string savedChangelog = File.ReadAllText(changelogPath);
    Assert(savedChangelog.Contains(candidateVersion.DisplayName, StringComparison.Ordinal) &&
        savedChangelog.Contains(candidateNotes, StringComparison.Ordinal), "Downloaded update changelog was not preserved beside the package.");
    Assert(File.ReadAllBytes(protectedProjectFile).AsSpan().SequenceEqual(protectedProjectBytes),
        "Downloading an update changed a saved project edit outside the Updates folder.");

    await ExpectInvalidDataAsync(
        () => client.DownloadVerifiedAsync(
            update with { Asset = update.Asset with { Name = $"Copy-{candidateAssetName}" } },
            Path.Combine(temporaryRoot, "wrong-name")),
        "A package with a non-exact asset name was accepted.");

    await ExpectInvalidDataAsync(
        () => client.DownloadVerifiedAsync(
            update with
            {
                Asset = update.Asset with
                {
                    DownloadUri = new Uri($"https://github.com/example/another-repository/releases/download/{candidateVersion.ReleaseTag}/{candidateAssetName}")
                }
            },
            Path.Combine(temporaryRoot, "wrong-repository")),
        "A package URL from a different GitHub repository was accepted.");

    await ExpectInvalidDataAsync(
        () => client.DownloadVerifiedAsync(
            update with { DisplayName = $"Spyro Editor Beta V{candidateBeta + 1}" },
            Path.Combine(temporaryRoot, "wrong-display")),
        "An update with a mismatched public display name was accepted.");

    await ExpectInvalidDataAsync(
        () => client.DownloadVerifiedAsync(
            update with { ReleaseTag = $"beta-v{candidateBeta + 1}" },
            Path.Combine(temporaryRoot, "wrong-tag")),
        "An update with a mismatched public tag was accepted.");

    await ExpectInvalidDataAsync(
        () => client.DownloadVerifiedAsync(
            update with { ReleaseNotes = "" },
            Path.Combine(temporaryRoot, "missing-notes")),
        "An update without a changelog was accepted.");

    await ExpectInvalidDataAsync(
        () => client.DownloadVerifiedAsync(
            update with { Asset = update.Asset with { Size = GitHubReleaseUpdateClient.MaxUpdatePackageBytes + 1 } },
            Path.Combine(temporaryRoot, "oversized-metadata")),
        "Oversized package metadata was accepted.");

    await ExpectInvalidDataAsync(
        () => client.DownloadVerifiedAsync(
            update with { Asset = update.Asset with { Size = 0 } },
            Path.Combine(temporaryRoot, "zero-metadata")),
        "Zero-byte package metadata was accepted.");

    await AssertRejectedPayloadAsync(
        update with { Asset = update.Asset with { Size = validPayload.LongLength - 1, Sha256 = validHash } },
        validPayload,
        Path.Combine(temporaryRoot, "oversized-body"),
        "A response body larger than its declared size was accepted.",
        includeContentLength: false);

    await AssertRejectedPayloadAsync(
        update with { Asset = update.Asset with { Sha256 = new string('0', 64) } },
        validPayload,
        Path.Combine(temporaryRoot, "tampered"),
        "A package with a mismatched digest was accepted.");

    byte[] invalidZip = Encoding.UTF8.GetBytes("this is not a ZIP archive");
    await AssertRejectedPayloadAsync(UpdateForPayload(update, invalidZip), invalidZip, Path.Combine(temporaryRoot, "invalid-zip"), "A non-ZIP update package was accepted.");

    byte[] wrongRoot = CreatePackage(candidateBeta, platform, appDllRelativePath, candidateNotes, candidateInternalVersion, validAppDll, rootOverride: "WrongRoot");
    await AssertRejectedPayloadAsync(UpdateForPayload(update, wrongRoot), wrongRoot, Path.Combine(temporaryRoot, "wrong-root"), "An update ZIP with the wrong root was accepted.");

    byte[] wrongReadme = CreatePackage(candidateBeta, platform, appDllRelativePath, candidateNotes, candidateInternalVersion, validAppDll, readmeFirstLine: $"Spyro Editor Beta V{candidateBeta + 1}");
    await AssertRejectedPayloadAsync(UpdateForPayload(update, wrongReadme), wrongReadme, Path.Combine(temporaryRoot, "wrong-readme"), "An update ZIP with the wrong README first line was accepted.");

    byte[] missingAppDll = CreatePackage(candidateBeta, platform, appDllRelativePath, candidateNotes, candidateInternalVersion, validAppDll, includeAppDll: false);
    await AssertRejectedPayloadAsync(UpdateForPayload(update, missingAppDll), missingAppDll, Path.Combine(temporaryRoot, "missing-app-dll"), "An update ZIP without the required platform app DLL was accepted.");

    byte[] nonManagedApp = CreatePackage(
        candidateBeta,
        platform,
        appDllRelativePath,
        candidateNotes,
        candidateInternalVersion,
        Encoding.UTF8.GetBytes("not a managed application assembly"));
    await AssertRejectedPayloadAsync(
        UpdateForPayload(update, nonManagedApp),
        nonManagedApp,
        Path.Combine(temporaryRoot, "non-managed-app-dll"),
        "An update ZIP whose application DLL is not a managed assembly was accepted.");

    byte[] wrongPublicBetaDll = ReplaceAssemblyAttributeBlob(
        validAppDll,
        [EditorAssemblyReleaseIdentityReader.BetaReleaseMetadataKey, candidateBeta.ToString()],
        [EditorAssemblyReleaseIdentityReader.BetaReleaseMetadataKey, DifferentSameLengthValue(candidateBeta.ToString())]);
    byte[] wrongPublicBetaPackage = CreatePackage(
        candidateBeta,
        platform,
        appDllRelativePath,
        candidateNotes,
        candidateInternalVersion,
        wrongPublicBetaDll);
    await AssertRejectedPayloadAsync(
        UpdateForPayload(update, wrongPublicBetaPackage),
        wrongPublicBetaPackage,
        Path.Combine(temporaryRoot, "wrong-assembly-public-beta"),
        "An update ZIP whose app assembly public beta differs from its manifest was accepted.");

    byte[] wrongManifestSchemaDll = ReplaceAssemblyAttributeBlob(
        validAppDll,
        [EditorAssemblyReleaseIdentityReader.ReleaseManifestSchemaMetadataKey, "1"],
        [EditorAssemblyReleaseIdentityReader.ReleaseManifestSchemaMetadataKey, "2"]);
    byte[] wrongManifestSchemaPackage = CreatePackage(
        candidateBeta,
        platform,
        appDllRelativePath,
        candidateNotes,
        candidateInternalVersion,
        wrongManifestSchemaDll);
    await AssertRejectedPayloadAsync(
        UpdateForPayload(update, wrongManifestSchemaPackage),
        wrongManifestSchemaPackage,
        Path.Combine(temporaryRoot, "wrong-assembly-manifest-schema"),
        "An update ZIP whose app assembly manifest schema differs from its public version was accepted.");

    string wrongInternalVersion = DifferentSameLengthValue(candidateInternalVersion);
    byte[] wrongInternalVersionDll = ReplaceAssemblyAttributeBlob(
        validAppDll,
        [candidateInternalVersion],
        [wrongInternalVersion]);
    byte[] wrongInternalVersionPackage = CreatePackage(
        candidateBeta,
        platform,
        appDllRelativePath,
        candidateNotes,
        candidateInternalVersion,
        wrongInternalVersionDll);
    await AssertRejectedPayloadAsync(
        UpdateForPayload(update, wrongInternalVersionPackage),
        wrongInternalVersionPackage,
        Path.Combine(temporaryRoot, "wrong-assembly-internal-version"),
        "An update ZIP whose app assembly informational version differs from its manifest was accepted.");

    byte[] missingManifest = CreatePackage(candidateBeta, platform, appDllRelativePath, candidateNotes, candidateInternalVersion, validAppDll, includeManifest: false);
    await AssertRejectedPayloadAsync(UpdateForPayload(update, missingManifest), missingManifest, Path.Combine(temporaryRoot, "missing-manifest"), "An update ZIP without its release manifest was accepted.");

    byte[] malformedManifest = CreatePackage(candidateBeta, platform, appDllRelativePath, candidateNotes, candidateInternalVersion, validAppDll, manifestPayloadOverride: "not-json");
    await AssertRejectedPayloadAsync(UpdateForPayload(update, malformedManifest), malformedManifest, Path.Combine(temporaryRoot, "malformed-manifest"), "An update ZIP with a malformed release manifest was accepted.");

    byte[] wrongManifestBeta = CreatePackage(candidateBeta, platform, appDllRelativePath, candidateNotes, candidateInternalVersion, validAppDll, manifestBetaOverride: candidateBeta + 1);
    await AssertRejectedPayloadAsync(UpdateForPayload(update, wrongManifestBeta), wrongManifestBeta, Path.Combine(temporaryRoot, "wrong-manifest-beta"), "An update ZIP with a mismatched manifest beta was accepted.");

    byte[] wrongManifestPlatform = CreatePackage(candidateBeta, platform, appDllRelativePath, candidateNotes, candidateInternalVersion, validAppDll, manifestPlatformOverride: "wrong-platform");
    await AssertRejectedPayloadAsync(UpdateForPayload(update, wrongManifestPlatform), wrongManifestPlatform, Path.Combine(temporaryRoot, "wrong-manifest-platform"), "An update ZIP with a mismatched manifest platform was accepted.");

    string incrementalNotes = $"# {incrementalOne.DisplayName}\n\n- Exercises canonical incremental update validation.";
    EditorUpdateInfo incrementalTemplate = new(
        incrementalOne.Major,
        incrementalOne.ReleaseTag,
        incrementalOne.DisplayName,
        incrementalNotes,
        new Uri($"https://github.com/monty19933-hub/Spyro1Editor/releases/tag/{incrementalOne.ReleaseTag}"),
        true,
        new EditorUpdateAsset(
            AssetNameForVersion(incrementalOne, platform),
            new Uri($"https://github.com/monty19933-hub/Spyro1Editor/releases/download/{incrementalOne.ReleaseTag}/{AssetNameForVersion(incrementalOne, platform)}"),
            1,
            new string('0', 64)),
        incrementalOne.CanonicalVersion);
    byte[] validIncrementalPackage = CreateIncrementalPackage(
        incrementalOne,
        platform,
        appDllRelativePath,
        incrementalNotes,
        incrementalInternalVersion,
        validIncrementalAppDll);
    EditorUpdateInfo validIncrementalUpdate = UpdateForPayload(
        incrementalTemplate,
        validIncrementalPackage);
    using (FixtureHandler incrementalDownloadHandler = new("[]", validIncrementalPackage))
    using (HttpClient incrementalDownloadHttp = new(incrementalDownloadHandler))
    {
        GitHubReleaseUpdateClient incrementalDownloadClient = new(incrementalDownloadHttp);
        string incrementalDownload = await incrementalDownloadClient.DownloadVerifiedAsync(
            validIncrementalUpdate,
            Path.Combine(temporaryRoot, "valid-incremental"));
        Assert(File.ReadAllBytes(incrementalDownload).AsSpan().SequenceEqual(validIncrementalPackage),
            "Verified incremental update differs from its fixture payload.");
        Assert(incrementalDownloadHandler.DownloadRequests == 1,
            "Valid incremental update was not downloaded exactly once.");
    }

    byte[] dottedSchemaOne = CreateIncrementalPackage(
        incrementalOne,
        platform,
        appDllRelativePath,
        incrementalNotes,
        candidateInternalVersion,
        validAppDll,
        manifestSchema: 1);
    await AssertRejectedPayloadAsync(
        UpdateForPayload(incrementalTemplate, dottedSchemaOne),
        dottedSchemaOne,
        Path.Combine(temporaryRoot, "dotted-schema-one"),
        "A dotted public release using legacy manifest schema 1 was accepted.");

    byte[] dottedSchemaTwoWithWholeAssembly = CreateIncrementalPackage(
        incrementalOne,
        platform,
        appDllRelativePath,
        incrementalNotes,
        candidateInternalVersion,
        validAppDll);
    await AssertRejectedPayloadAsync(
        UpdateForPayload(incrementalTemplate, dottedSchemaTwoWithWholeAssembly),
        dottedSchemaTwoWithWholeAssembly,
        Path.Combine(temporaryRoot, "dotted-whole-assembly"),
        "A schema-2 dotted package whose app assembly retained whole-number identity was accepted.");

    byte[] wrongDottedManifestVersion = CreateIncrementalPackage(
        incrementalOne,
        platform,
        appDllRelativePath,
        incrementalNotes,
        candidateInternalVersion,
        validAppDll,
        manifestPublicVersionOverride: incrementalTwo.CanonicalVersion);
    await AssertRejectedPayloadAsync(
        UpdateForPayload(incrementalTemplate, wrongDottedManifestVersion),
        wrongDottedManifestVersion,
        Path.Combine(temporaryRoot, "wrong-dotted-manifest-version"),
        "A schema-2 package with a mismatched canonical public version was accepted.");

    await ExpectInvalidDataAsync(
        () => client.DownloadVerifiedAsync(
            incrementalTemplate with { BetaVersion = incrementalOne.Major + 1 },
            Path.Combine(temporaryRoot, "dotted-legacy-major-mismatch")),
        "A dotted update whose legacy beta major did not match its public version was accepted.");

    await ExpectInvalidDataAsync(
        () => client.DownloadVerifiedAsync(
            incrementalTemplate with { PublicVersion = $" {incrementalOne.CanonicalVersion}" },
            Path.Combine(temporaryRoot, "noncanonical-dotted-version")),
        "A dotted update with non-canonical public-version text was accepted.");

    byte[] missingChangelog = CreatePackage(candidateBeta, platform, appDllRelativePath, candidateNotes, candidateInternalVersion, validAppDll, includeChangelog: false);
    await AssertRejectedPayloadAsync(UpdateForPayload(update, missingChangelog), missingChangelog, Path.Combine(temporaryRoot, "missing-changelog"), "An update ZIP without its changelog was accepted.");

    byte[] wrongChangelog = CreatePackage(candidateBeta, platform, appDllRelativePath, "different packaged notes", candidateInternalVersion, validAppDll);
    await AssertRejectedPayloadAsync(UpdateForPayload(update, wrongChangelog), wrongChangelog, Path.Combine(temporaryRoot, "wrong-changelog"), "An update ZIP whose changelog differs from GitHub was accepted.");

    byte[] rogueMetadata = CreatePackage(candidateBeta, platform, appDllRelativePath, candidateNotes, candidateInternalVersion, validAppDll, includeMacMetadata: true, metadataRootOverride: "WrongRoot");
    await AssertRejectedPayloadAsync(UpdateForPayload(update, rogueMetadata), rogueMetadata, Path.Combine(temporaryRoot, "rogue-metadata"), "An update ZIP with __MACOSX metadata outside the package root was accepted.");

    byte[] symbolicLink = CreatePackage(candidateBeta, platform, appDllRelativePath, candidateNotes, candidateInternalVersion, validAppDll, includeSymbolicLink: true);
    await AssertRejectedPayloadAsync(UpdateForPayload(update, symbolicLink), symbolicLink, Path.Combine(temporaryRoot, "symbolic-link"), "An update ZIP containing a symbolic link was accepted.");

    byte[] tooManyEntries = CreatePackage(
        candidateBeta,
        platform,
        appDllRelativePath,
        candidateNotes,
        candidateInternalVersion,
        validAppDll,
        extraEntryCount: GitHubReleaseUpdateClient.MaxArchiveEntries);
    await AssertRejectedPayloadAsync(UpdateForPayload(update, tooManyEntries), tooManyEntries, Path.Combine(temporaryRoot, "too-many-entries"), "An update ZIP with too many entries was accepted.");
}
finally
{
    if (Directory.Exists(temporaryRoot))
        Directory.Delete(temporaryRoot, recursive: true);
}

Console.WriteLine("PASS: whole-number and incremental prerelease discovery, current bridge-to-incremental notification compatibility, historical V2/V3 notification coverage, one-time notification policy, exact repository/tag/title/asset identity, visible changelog preservation, protected-project isolation, trusted URLs, bounded streaming, SHA-256 verification, bounded non-link archive validation, schema-aware package manifest/changelog/app-assembly validation, macOS metadata handling, reuse, and malformed-package rejection.");

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

static async Task ExpectInvalidDataAsync(Func<Task<string>> action, string message)
{
    try
    {
        await action();
    }
    catch (InvalidDataException)
    {
        return;
    }
    throw new InvalidOperationException(message);
}

static async Task AssertRejectedPayloadAsync(EditorUpdateInfo update, byte[] payload, string destination, string message, bool includeContentLength = true)
{
    using FixtureHandler handler = new("[]", payload, includeContentLength);
    using HttpClient http = new(handler);
    GitHubReleaseUpdateClient client = new(http);
    await ExpectInvalidDataAsync(() => client.DownloadVerifiedAsync(update, destination), message);
    Assert(!Directory.Exists(destination) || !Directory.EnumerateFiles(destination).Any(), "Rejected update left an accepted or temporary package behind.");
}

static EditorUpdateInfo UpdateForPayload(EditorUpdateInfo template, byte[] payload) =>
    template with { Asset = template.Asset with { Size = payload.LongLength, Sha256 = Hash(payload) } };

static string Hash(byte[] payload) => Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();

static (string Platform, string AppDllRelativePath) CurrentPlatform()
{
    if (OperatingSystem.IsMacOS() && System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.Arm64)
        return ("osx-arm64", "Spyro Editor.app/Contents/MacOS/Spyro.Editor.App.dll");
    if (OperatingSystem.IsWindows() && System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.X64)
        return ("win-x64", "support/app/Spyro.Editor.App.dll");
    throw new PlatformNotSupportedException("Update smoke requires a supported Spyro Editor release platform.");
}

static string AssetName(int beta, string platform) => $"SpyroEditor-Beta-V{beta}-{platform}.zip";
static string AssetNameForVersion(EditorBetaReleaseVersion version, string platform) => $"{version.PackageStem}-{platform}.zip";

static object Asset(string name, long size, string hash, string? downloadUrl = null) => new
{
    name,
    browser_download_url = downloadUrl ?? $"https://github.com/monty19933-hub/Spyro1Editor/releases/download/fixture/{name}",
    size,
    digest = "sha256:" + hash
};

static object Release(
    int beta,
    object[] assets,
    string? htmlUrl = null,
    string? tagOverride = null,
    string? nameOverride = null,
    string? body = "Safe project storage and update checks.",
    bool prerelease = true) => new
{
    tag_name = tagOverride ?? $"beta-v{beta}",
    name = nameOverride ?? $"Spyro Editor Beta V{beta}",
    body,
    draft = false,
    prerelease,
    html_url = htmlUrl ?? $"https://github.com/monty19933-hub/Spyro1Editor/releases/tag/beta-v{beta}",
    assets
};

static object IncrementalRelease(
    EditorBetaReleaseVersion version,
    object[] assets,
    string? body = "Safe project storage and update checks.",
    bool prerelease = true) => new
{
    tag_name = version.ReleaseTag,
    name = version.DisplayName,
    body,
    draft = false,
    prerelease,
    html_url = $"https://github.com/monty19933-hub/Spyro1Editor/releases/tag/{version.ReleaseTag}",
    assets
};

static byte[] ReadFixtureAppDll()
{
    string path = typeof(Spyro.Editor.App.App).Assembly.Location;
    if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        throw new FileNotFoundException("The update smoke could not locate its built Spyro.Editor.App fixture assembly.", path);
    return File.ReadAllBytes(path);
}

static byte[] ReadIncrementalFixtureAppDll()
{
    string path = Path.Combine(AppContext.BaseDirectory, "Spyro.Editor.App.IncrementalFixture.dll");
    if (!File.Exists(path))
        throw new FileNotFoundException("The update smoke could not locate its built incremental app fixture assembly.", path);
    return File.ReadAllBytes(path);
}

static byte[] ReplaceAssemblyAttributeBlob(
    byte[] assembly,
    IReadOnlyList<string> oldArguments,
    IReadOnlyList<string> newArguments)
{
    byte[] oldBlob = BuildStringAttributeBlob(oldArguments);
    byte[] newBlob = BuildStringAttributeBlob(newArguments);
    if (oldBlob.Length != newBlob.Length)
        throw new InvalidOperationException("Update smoke metadata replacements must retain the original blob length.");

    int match = FindUniqueSequence(assembly, oldBlob);
    byte[] replaced = assembly.ToArray();
    newBlob.CopyTo(replaced, match);
    return replaced;
}

static byte[] BuildStringAttributeBlob(IReadOnlyList<string> arguments)
{
    using MemoryStream output = new();
    output.WriteByte(1);
    output.WriteByte(0);
    foreach (string argument in arguments)
    {
        byte[] encoded = Encoding.UTF8.GetBytes(argument);
        if (encoded.Length >= 0x80)
            throw new InvalidOperationException("Update smoke metadata fixture strings must fit a one-byte serialized-string length.");
        output.WriteByte((byte)encoded.Length);
        output.Write(encoded);
    }
    output.WriteByte(0);
    output.WriteByte(0);
    return output.ToArray();
}

static int FindUniqueSequence(byte[] haystack, byte[] needle)
{
    int match = -1;
    for (int start = 0; start <= haystack.Length - needle.Length; start++)
    {
        if (!haystack.AsSpan(start, needle.Length).SequenceEqual(needle))
            continue;
        if (match >= 0)
            throw new InvalidOperationException("Update smoke assembly metadata fixture was not unique.");
        match = start;
    }
    if (match < 0)
        throw new InvalidOperationException("Update smoke could not locate the expected assembly metadata fixture.");
    return match;
}

static string DifferentSameLengthValue(string value)
{
    if (string.IsNullOrEmpty(value))
        throw new InvalidOperationException("Update smoke needs a non-empty source assembly version fixture.");
    char replacement = value[^1] == '0' ? '1' : '0';
    return value[..^1] + replacement;
}

static byte[] CreatePackage(
    int beta,
    string platform,
    string appDllRelativePath,
    string changelog,
    string internalVersion,
    byte[] appDllPayload,
    string? rootOverride = null,
    string? readmeFirstLine = null,
    bool includeAppDll = true,
    bool includeManifest = true,
    string? manifestPayloadOverride = null,
    int? manifestBetaOverride = null,
    string? manifestPlatformOverride = null,
    bool includeChangelog = true,
    bool includeMacMetadata = false,
    string? metadataRootOverride = null,
    bool includeSymbolicLink = false,
    int extraEntryCount = 0)
{
    string root = rootOverride ?? $"SpyroEditor-Beta-V{beta}-{platform}";
    using MemoryStream output = new();
    using (ZipArchive archive = new(output, ZipArchiveMode.Create, leaveOpen: true))
    {
        archive.CreateEntry(root + "/");
        WriteEntry(archive, root + "/README.txt", Encoding.UTF8.GetBytes((readmeFirstLine ?? $"Spyro Editor Beta V{beta}") + "\nFixture package.\n"));
        if (includeManifest)
        {
            string manifest = manifestPayloadOverride ?? JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                channel = "beta",
                publicBeta = manifestBetaOverride ?? beta,
                displayName = $"Spyro Editor Beta V{beta}",
                internalVersion,
                platform = manifestPlatformOverride ?? platform
            });
            WriteEntry(archive, root + "/release-manifest.json", Encoding.UTF8.GetBytes(manifest));
        }
        if (includeChangelog)
            WriteEntry(archive, root + "/CHANGELOG.md", Encoding.UTF8.GetBytes(changelog + "\n"));
        if (includeAppDll)
            WriteEntry(archive, root + "/" + appDllRelativePath, appDllPayload);
        WriteEntry(archive, root + "/support/fixture.txt", Encoding.UTF8.GetBytes("support"));
        for (int index = 0; index < extraEntryCount; index++)
            archive.CreateEntry($"{root}/support/extra-{index:D5}.txt");

        if (includeSymbolicLink)
        {
            ZipArchiveEntry link = archive.CreateEntry(root + "/support/unsafe-link");
            link.ExternalAttributes = unchecked((int)0xA1FF0000u);
        }

        if (includeMacMetadata)
        {
            string metadataRoot = metadataRootOverride ?? root;
            archive.CreateEntry("__MACOSX/");
            archive.CreateEntry($"__MACOSX/{metadataRoot}/");
            WriteEntry(archive, $"__MACOSX/{metadataRoot}/._README.txt", new byte[] { 0, 5, 22, 7 });
        }
    }
    return output.ToArray();
}

static byte[] CreateIncrementalPackage(
    EditorBetaReleaseVersion version,
    string platform,
    string appDllRelativePath,
    string changelog,
    string internalVersion,
    byte[] appDllPayload,
    int manifestSchema = 2,
    string? manifestPublicVersionOverride = null)
{
    string root = $"{version.PackageStem}-{platform}";
    using MemoryStream output = new();
    using (ZipArchive archive = new(output, ZipArchiveMode.Create, leaveOpen: true))
    {
        archive.CreateEntry(root + "/");
        WriteEntry(archive, root + "/README.txt", Encoding.UTF8.GetBytes(version.DisplayName + "\nFixture package.\n"));
        string manifest = JsonSerializer.Serialize(new
        {
            schemaVersion = manifestSchema,
            channel = "beta",
            publicBeta = version.Major,
            publicVersion = manifestPublicVersionOverride ?? version.CanonicalVersion,
            displayName = version.DisplayName,
            internalVersion,
            platform
        });
        WriteEntry(archive, root + "/release-manifest.json", Encoding.UTF8.GetBytes(manifest));
        WriteEntry(archive, root + "/CHANGELOG.md", Encoding.UTF8.GetBytes(changelog + "\n"));
        WriteEntry(archive, root + "/" + appDllRelativePath, appDllPayload);
        WriteEntry(archive, root + "/support/fixture.txt", Encoding.UTF8.GetBytes("support"));
    }
    return output.ToArray();
}

static void WriteEntry(ZipArchive archive, string path, byte[] payload)
{
    ZipArchiveEntry entry = archive.CreateEntry(path, CompressionLevel.Optimal);
    using Stream output = entry.Open();
    output.Write(payload);
}

sealed class FixtureHandler(string releasesJson, byte[] payload, bool includeContentLength = true) : HttpMessageHandler
{
    public int DownloadRequests { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        if (request.RequestUri?.Host == "api.github.com")
        {
            response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(releasesJson, Encoding.UTF8, "application/json")
            };
        }
        else
        {
            DownloadRequests++;
            HttpContent content = includeContentLength ? new ByteArrayContent(payload) : new UnknownLengthContent(payload);
            if (includeContentLength)
                content.Headers.ContentLength = payload.LongLength;
            response = new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        }
        return Task.FromResult(response);
    }
}

sealed class UnknownLengthContent(byte[] payload) : HttpContent
{
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(payload).AsTask();

    protected override bool TryComputeLength(out long length)
    {
        length = 0;
        return false;
    }
}
