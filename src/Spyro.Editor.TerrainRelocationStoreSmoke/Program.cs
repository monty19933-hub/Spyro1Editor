using System.Text.Json;
using System.Text.Json.Nodes;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Persistence;

string temporaryRoot = Path.Combine(Path.GetTempPath(), $"spyro-terrain-relocation-store-{Guid.NewGuid():N}");

try
{
    Directory.CreateDirectory(temporaryRoot);
    string manifestPath = NativeTerrainTextureRelocationEditStore.ManifestPath(temporaryRoot, "Artisans");
    Assert(
        manifestPath.EndsWith("artisans-native-terrain-texture-relocations.json", StringComparison.Ordinal),
        "The relocation store did not use a dedicated normalized per-destination manifest path.");
    Assert(
        !string.Equals(
            manifestPath,
            CustomTerrainTextureStore.ManifestPath(temporaryRoot, "artisans"),
            StringComparison.OrdinalIgnoreCase),
        "Native relocations were mixed into the custom imported-image manifest.");
    Assert(
        PortableProjectMigration.Classify(Path.GetFileName(manifestPath)) == PortableProjectArtifactKind.ProjectEdit,
        "The dedicated relocation manifest would not survive legacy project migration during an update.");

    IReadOnlyList<NativeTerrainTextureRelocationEdit> one =
        await NativeTerrainTextureRelocationEditStore.AddOrReplaceAsync(
            temporaryRoot,
            destinationLevelKey: "Artisans",
            destinationLevelName: "Artisans",
            targetTextureId: 54,
            donorLevelKey: "Gnasty's World",
            donorLevelName: "Gnasty's World",
            donorWadEntry: 70,
            donorTextureId: 22,
            donorRuntimeKey: "34:3:hp",
            previewImagePath: Path.Combine(temporaryRoot, "previews", "gnasty-22.png"));
    Assert(one.Count == 1, "The first relocation was not persisted.");
    Assert(one[0].DescriptorTier == NativeTerrainTextureRelocationEditStore.CompleteDescriptorTier,
        "The store did not canonicalize the complete descriptor tier.");
    Assert(one[0].PreviewImageName == "gnasty-22.png",
        "The optional preview name was not derived from its path.");
    Assert(one[0].ApplyMode == NativeTerrainTextureRelocationApplyMode.ArtAndNativeSurface &&
           !one[0].PreservesTargetNativeSurface,
        "The legacy AddOrReplace API did not retain full native surface-transfer mode.");
    string originalCreatedAt = one[0].CreatedAt;

    IReadOnlyList<NativeTerrainTextureRelocationEdit> two =
        await NativeTerrainTextureRelocationEditStore.AddOrReplaceAsync(
            temporaryRoot,
            destinationLevelKey: "artisans",
            destinationLevelName: "Artisans",
            targetTextureId: 12,
            donorLevelKey: "stone_hill",
            donorLevelName: "Stone Hill",
            donorWadEntry: 11,
            donorTextureId: 7,
            donorRuntimeKey: "4:8:hp");
    Assert(two.Select(edit => edit.TargetTextureId).SequenceEqual([12, 54]),
        "Relocations were not saved in deterministic target-texture order.");
    Assert(two[0].DonorLevelKey == "stonehill", "Donor level keys were not normalized.");
    Assert(string.IsNullOrEmpty(two[0].PreviewImagePath) && string.IsNullOrEmpty(two[0].PreviewImageName),
        "Optional preview fields were unexpectedly required.");

    string firstSerializedForm = await File.ReadAllTextAsync(manifestPath);
    IReadOnlyList<NativeTerrainTextureRelocationEdit> loaded =
        NativeTerrainTextureRelocationEditStore.Load(temporaryRoot, "artisans");
    Assert(loaded.SequenceEqual(two), "A saved relocation manifest did not round-trip exactly.");
    await NativeTerrainTextureRelocationEditStore.SaveManifestAsync(
        manifestPath,
        "artisans",
        "Artisans",
        loaded);
    Assert(
        string.Equals(firstSerializedForm, await File.ReadAllTextAsync(manifestPath), StringComparison.Ordinal),
        "Saving the same relocation records did not produce deterministic JSON.");

    IReadOnlyList<NativeTerrainTextureRelocationEdit> replaced =
        await NativeTerrainTextureRelocationEditStore.AddOrReplaceAsync(
            temporaryRoot,
            destinationLevelKey: "artisans",
            destinationLevelName: "Artisans",
            targetTextureId: 54,
            donorLevelKey: "wizard_peak",
            donorLevelName: "Wizard Peak",
            donorWadEntry: 40,
            donorTextureId: 9,
            donorRuntimeKey: "12:3:hp");
    NativeTerrainTextureRelocationEdit replacement = replaced.Single(edit => edit.TargetTextureId == 54);
    Assert(replaced.Count == 2, "Replacing a target texture appended a duplicate relocation.");
    Assert(replacement.DonorLevelKey == "wizardpeak" &&
           replacement.DonorLevelName == "Wizard Peak" &&
           replacement.DonorWadEntry == 40 &&
           replacement.DonorTextureId == 9 &&
           replacement.DonorRuntimeKey == "12:3:hp",
        "Replacement did not update all donor provenance fields.");
    Assert(replacement.CreatedAt == originalCreatedAt,
        "Replacing a target texture churned its original createdAt timestamp.");
    Assert(replaced.Single(edit => edit.TargetTextureId == 12) == two.Single(edit => edit.TargetTextureId == 12),
        "Replacing one target texture mutated another relocation.");

    IReadOnlyList<NativeTerrainTextureRelocationEdit> remaining =
        await NativeTerrainTextureRelocationEditStore.RemoveAsync(
            temporaryRoot,
            "artisans",
            "Artisans",
            targetTextureId: 12);
    Assert(remaining.Count == 1 && remaining[0].TargetTextureId == 54,
        "Remove did not delete exactly the requested target relocation.");
    string beforeMissingRemove = await File.ReadAllTextAsync(manifestPath);
    IReadOnlyList<NativeTerrainTextureRelocationEdit> afterMissingRemove =
        await NativeTerrainTextureRelocationEditStore.RemoveAsync(
            temporaryRoot,
            "artisans",
            "Artisans",
            targetTextureId: 999);
    Assert(afterMissingRemove.SequenceEqual(remaining), "Removing an absent target changed the loaded records.");
    Assert(beforeMissingRemove == await File.ReadAllTextAsync(manifestPath),
        "Removing an absent target rewrote otherwise unchanged project data.");

    IReadOnlyList<NativeTerrainTextureRelocationEdit> empty =
        await NativeTerrainTextureRelocationEditStore.RemoveAsync(
            temporaryRoot,
            "artisans",
            "Artisans",
            targetTextureId: 54);
    Assert(empty.Count == 0, "Removing the last relocation did not return an empty edit set.");
    Assert(!File.Exists(manifestPath), "Removing the last relocation left a stale empty manifest behind.");

    await NativeTerrainTextureRelocationEditStore.SaveManifestAsync(
        manifestPath,
        "artisans",
        "Artisans",
        remaining);

    string brokenJsonPath = Path.Combine(temporaryRoot, "broken.json");
    await File.WriteAllTextAsync(brokenJsonPath, "{ definitely not json");
    Assert(NativeTerrainTextureRelocationEditStore.LoadManifest(brokenJsonPath).Count == 0,
        "Syntactically malformed JSON escaped as an active relocation.");

    string mixedManifestPath = Path.Combine(temporaryRoot, "mixed.json");
    await File.WriteAllTextAsync(mixedManifestPath, """
        {
          "destinationLevelKey": "artisans",
          "relocations": [
            {
              "targetTextureId": 54,
              "donorLevelKey": "gnastysworld",
              "donorLevelName": "Gnasty's World",
              "donorWadEntry": 70,
              "donorTextureId": 22,
              "donorRuntimeKey": "native-texture-record:22",
              "descriptorTier": "hqData",
              "createdAt": "2026-07-15T12:00:00Z"
            },
            {
              "targetTextureId": -1,
              "donorLevelKey": "stonehill",
              "donorLevelName": "Stone Hill",
              "donorWadEntry": 11,
              "donorTextureId": 7,
              "donorRuntimeKey": "4:8:hp",
              "descriptorTier": "both",
              "createdAt": "not-a-date"
            },
            {
              "targetTextureId": 54,
              "donorLevelKey": "wizard_peak",
              "donorLevelName": "Wizard Peak",
              "donorWadEntry": 40,
              "donorTextureId": 9,
              "donorRuntimeKey": "12:3:hp",
              "descriptorTier": "BOTH",
              "createdAt": "2026-07-15T12:00:00Z"
            },
            {
              "targetTextureId": 54,
              "donorLevelKey": "blowhard",
              "donorLevelName": "Blowhard",
              "donorWadEntry": 41,
              "donorTextureId": 3,
              "donorRuntimeKey": "2:1:hp",
              "descriptorTier": "both",
              "createdAt": "2026-07-15T12:00:00Z"
            }
          ]
        }
        """);
    IReadOnlyList<NativeTerrainTextureRelocationEdit> safeMixedLoad =
        NativeTerrainTextureRelocationEditStore.LoadManifest(mixedManifestPath, "artisans");
    Assert(safeMixedLoad.Count == 1 && safeMixedLoad[0].DonorLevelKey == "blowhard",
        "Malformed entries were activated or duplicate-target last-valid-wins behavior was not deterministic.");
    Assert(NativeTerrainTextureRelocationEditStore.LoadManifest(mixedManifestPath, "stonehill").Count == 0,
        "A manifest bound to another destination level was accepted.");

    bool partialTierRejected = false;
    try
    {
        await NativeTerrainTextureRelocationEditStore.SaveManifestAsync(
            Path.Combine(temporaryRoot, "partial-tier-must-not-save.json"),
            "artisans",
            "Artisans",
            [replacement with { DescriptorTier = "hqData" }]);
    }
    catch (ArgumentException)
    {
        partialTierRejected = true;
    }
    Assert(partialTierRejected, "The save API accepted a runtime-incomplete descriptor tier.");

    using JsonDocument savedDocument = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath));
    Assert(savedDocument.RootElement.GetProperty("version").GetInt32() == 3,
        "The relocation store did not write the version-3 target-record schema.");
    Assert(savedDocument.RootElement.GetProperty("relocationCount").GetInt32() == 1,
        "Manifest metadata did not match the deterministic relocation list.");

    string artOnlyRoot = Path.Combine(temporaryRoot, "art-only");
    IReadOnlyList<NativeTerrainTextureRelocationEdit> artOnly =
        await NativeTerrainTextureRelocationEditStore.AddOrReplaceArtOnlyAsync(
            artOnlyRoot,
            destinationLevelKey: "Artisans",
            destinationLevelName: "Artisans",
            targetTextureId: 54,
            donorLevelKey: "Beast Makers",
            donorLevelName: "Beast Makers",
            donorWadEntry: 46,
            donorTextureId: 10,
            previewImagePath: Path.Combine(artOnlyRoot, "previews", "beastmakers-10.png"));
    NativeTerrainTextureRelocationEdit artOnlyEdit = artOnly.Single();
    Assert(artOnlyEdit.ApplyMode == NativeTerrainTextureRelocationApplyMode.ArtOnlyPreserveTarget &&
           artOnlyEdit.PreservesTargetNativeSurface,
        "The dedicated art-only API did not persist preserve-target mode.");
    Assert(artOnlyEdit.DonorProvenanceKey == "native-texture-record:beastmakers:10",
        $"Art-only texture-record provenance was not canonical: {artOnlyEdit.DonorProvenanceKey}");
    string artOnlyManifest = NativeTerrainTextureRelocationEditStore.ManifestPath(artOnlyRoot, "artisans");
    Assert(NativeTerrainTextureRelocationEditStore.Load(artOnlyRoot, "artisans").SequenceEqual(artOnly),
        "Art-only apply mode/provenance did not round-trip exactly.");
    using (JsonDocument artOnlyDocument = JsonDocument.Parse(await File.ReadAllTextAsync(artOnlyManifest)))
    {
        JsonElement savedArtOnly = artOnlyDocument.RootElement.GetProperty("relocations").EnumerateArray().Single();
        Assert(artOnlyDocument.RootElement.GetProperty("version").GetInt32() == 3 &&
               savedArtOnly.GetProperty("applyMode").GetString() == NativeTerrainTextureRelocationEditStore.ArtOnlyPreserveTargetMode,
            "The art-only manifest omitted its required version-3 apply mode.");
    }

    string appendedRoot = Path.Combine(temporaryRoot, "appended-private");
    IReadOnlyList<NativeTerrainTextureRelocationEdit> appended =
        await NativeTerrainTextureRelocationEditStore.AddOrReplaceAppendedPrivateArtOnlyAsync(
            appendedRoot,
            destinationLevelKey: "Artisans",
            destinationLevelName: "Artisans",
            targetTextureId: 68,
            donorLevelKey: "Gnasty's World",
            donorLevelName: "Gnasty's World",
            donorWadEntry: 70,
            donorTextureId: 17,
            targetWadEntry: 10,
            sourceImageSha256: new string('C', 64),
            sourceTextureRecordCount: 68,
            sourceTextureComponentSha256: new string('A', 64),
            sourceLevelDataSha256: new string('B', 64),
            materialTemplateTextureId: 55,
            previewImagePath: Path.Combine(appendedRoot, "previews", "gnasty-eye.png"));
    NativeTerrainTextureRelocationEdit appendedEdit = appended.Single();
    Assert(appendedEdit.UsesAppendedPrivateRecord &&
           appendedEdit.TargetRecordKind == NativeTerrainTextureTargetRecordKind.AppendedPrivate &&
           appendedEdit.TargetWadEntry == 10 &&
           appendedEdit.SourceImageSha256 == new string('c', 64) &&
           appendedEdit.SourceTextureRecordCount == 68 &&
           appendedEdit.SourceTextureComponentSha256 == new string('a', 64) &&
           appendedEdit.SourceLevelDataSha256 == new string('b', 64) &&
           appendedEdit.MaterialTemplateTextureId == 55 &&
           appendedEdit.PrivateRecordEditId == "appended-private-terrain-texture:T068",
        "The appended-private record did not retain its source preimage, material template, and stable identity.");
    Assert(
        NativeTerrainTextureRelocationEditStore.Load(appendedRoot, "artisans").SequenceEqual(appended),
        "The appended-private target record did not round-trip exactly.");
    string appendedManifest = NativeTerrainTextureRelocationEditStore.ManifestPath(appendedRoot, "artisans");
    using (JsonDocument appendedDocument = JsonDocument.Parse(await File.ReadAllTextAsync(appendedManifest)))
    {
        JsonElement savedAppended = appendedDocument.RootElement.GetProperty("relocations").EnumerateArray().Single();
        Assert(savedAppended.GetProperty("targetRecordKind").GetString() == NativeTerrainTextureRelocationEditStore.AppendedPrivateTargetRecordKind,
            "The generated record was not explicitly marked appended-private in project data.");
    }

    string damagedAppendedPath = Path.Combine(temporaryRoot, "damaged-appended.json");
    await File.WriteAllTextAsync(
        damagedAppendedPath,
        (await File.ReadAllTextAsync(appendedManifest)).Replace(new string('a', 64), "not-a-source-hash", StringComparison.Ordinal));
    AssertInvalidData(
        () => NativeTerrainTextureRelocationEditStore.LoadManifest(damagedAppendedPath, "artisans"),
        "A malformed version-3 appended-private source-table preimage was silently skipped instead of failing the whole manifest.");

    string duplicateAppendedPath = Path.Combine(temporaryRoot, "duplicate-appended.json");
    JsonNode duplicateAppended = JsonNode.Parse(await File.ReadAllTextAsync(appendedManifest))
        ?? throw new InvalidDataException("Could not parse the appended-private manifest fixture.");
    JsonArray duplicateRows = duplicateAppended["relocations"]?.AsArray()
        ?? throw new InvalidDataException("The appended-private fixture has no relocations array.");
    duplicateRows.Add(duplicateRows[0]?.DeepClone());
    await File.WriteAllTextAsync(
        duplicateAppendedPath,
        duplicateAppended.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    AssertInvalidData(
        () => NativeTerrainTextureRelocationEditStore.LoadManifest(duplicateAppendedPath, "artisans"),
        "A duplicate version-3 appended-private target was silently collapsed instead of failing the whole manifest.");

    await NativeTerrainTextureRelocationEditStore.AddOrReplaceAppendedPrivateArtOnlyAsync(
        appendedRoot,
        "artisans",
        "Artisans",
        69,
        "darkhollow",
        "Dark Hollow",
        12,
        8,
        10,
        new string('c', 64),
        68,
        new string('a', 64),
        new string('b', 64),
        55);
    IReadOnlyList<NativeTerrainTextureRelocationEdit> withDuplicate =
        await NativeTerrainTextureRelocationEditStore.AddOrReplaceAppendedPrivateArtOnlyAsync(
            appendedRoot,
            "artisans",
            "Artisans",
            70,
            "gnastysworld",
            "Gnasty's World",
            70,
            17,
            10,
            new string('c', 64),
            68,
            new string('a', 64),
            new string('b', 64),
            55);
    NativeTerrainTextureAppendedPrivateCompaction compacted =
        NativeTerrainTextureRelocationEditStore.CompactAppendedPrivateRecords(withDuplicate);
    Assert(compacted.Changed &&
           compacted.AppendedRecordCount == 2 &&
           compacted.DuplicateContractCount == 1 &&
           compacted.TextureIdRemap[68] == 68 &&
           compacted.TextureIdRemap[69] == 69 &&
           compacted.TextureIdRemap[70] == 68 &&
           compacted.Edits.Select(edit => edit.TargetTextureId).SequenceEqual([68, 69]),
        $"Appended-private compaction did not deduplicate the donor/material contract and preserve contiguous table ids. " +
        $"changed={compacted.Changed}, appended={compacted.AppendedRecordCount}, duplicates={compacted.DuplicateContractCount}, " +
        $"ids={string.Join(",", compacted.Edits.Select(edit => edit.TargetTextureId))}, " +
        $"remap={string.Join(",", compacted.TextureIdRemap.Select(pair => $"{pair.Key}->{pair.Value}"))}.");
    await NativeTerrainTextureRelocationEditStore.SaveManifestAsync(
        appendedManifest,
        "artisans",
        "Artisans",
        compacted.Edits);
    NativeTerrainTextureAppendedPrivateAllocation reused =
        await NativeTerrainTextureRelocationEditStore.AddOrReuseAppendedPrivateArtOnlyAsync(
            appendedRoot,
            "artisans",
            "Artisans",
            "gnastysworld",
            "Gnasty's World",
            70,
            17,
            10,
            new string('c', 64),
            68,
            new string('a', 64),
            new string('b', 64),
            55);
    Assert(reused.ReusedExistingRecord && !reused.AddedNewRecord && reused.AssignedTextureId == 68 && reused.Edits.Count == 2,
        "An identical donor/material contract allocated a duplicate private record instead of reusing T68.");
    NativeTerrainTextureAppendedPrivateAllocation allocated =
        await NativeTerrainTextureRelocationEditStore.AddOrReuseAppendedPrivateArtOnlyAsync(
            appendedRoot,
            "artisans",
            "Artisans",
            "stonehill",
            "Stone Hill",
            11,
            4,
            10,
            new string('c', 64),
            68,
            new string('a', 64),
            new string('b', 64),
            55);
    Assert(!allocated.ReusedExistingRecord && allocated.AddedNewRecord && allocated.AssignedTextureId == 70 &&
           allocated.Edits.Select(edit => edit.TargetTextureId).SequenceEqual([68, 69, 70]),
        "A distinct donor-level contract was not allocated at the next contiguous native table id.");

    string legacyV1Path = Path.Combine(temporaryRoot, "legacy-v1.json");
    await File.WriteAllTextAsync(legacyV1Path, """
        {
          "version": 1,
          "destinationLevelKey": "artisans",
          "relocations": [{
            "targetTextureId": 54,
            "donorLevelKey": "gnastysworld",
            "donorLevelName": "Gnasty's World",
            "donorWadEntry": 70,
            "donorTextureId": 22,
            "donorRuntimeKey": "34:3:hp",
            "descriptorTier": "both",
            "previewImagePath": "",
            "previewImageName": "",
            "createdAt": "2026-07-15T12:00:00Z"
          }]
        }
        """);
    NativeTerrainTextureRelocationEdit legacyV1 = NativeTerrainTextureRelocationEditStore
        .LoadManifest(legacyV1Path, "artisans")
        .Single();
    Assert(legacyV1.ApplyMode == NativeTerrainTextureRelocationApplyMode.ArtAndNativeSurface,
        "A version-1 manifest without applyMode did not migrate to the existing full-transfer behavior.");

    string legacyV2Path = Path.Combine(temporaryRoot, "legacy-v2.json");
    await File.WriteAllTextAsync(legacyV2Path, $$"""
        {
          "version": 2,
          "destinationLevelKey": "artisans",
          "relocations": [{
            "targetTextureId": 54,
            "donorLevelKey": "gnastysworld",
            "donorLevelName": "Gnasty's World",
            "donorWadEntry": 70,
            "donorTextureId": 17,
            "donorRuntimeKey": "{{NativeTerrainTextureRelocationEditStore.BuildTextureRecordProvenanceKey("gnastysworld", 17)}}",
            "applyMode": "{{NativeTerrainTextureRelocationEditStore.ArtOnlyPreserveTargetMode}}",
            "descriptorTier": "both",
            "previewImagePath": "",
            "previewImageName": "",
            "createdAt": "2026-07-15T12:00:00Z"
          }]
        }
        """);
    NativeTerrainTextureRelocationEdit legacyV2 = NativeTerrainTextureRelocationEditStore
        .LoadManifest(legacyV2Path, "artisans")
        .Single();
    Assert(legacyV2.PreservesTargetNativeSurface &&
           legacyV2.TargetRecordKind == NativeTerrainTextureTargetRecordKind.ExistingNative &&
           !legacyV2.UsesAppendedPrivateRecord,
        "A version-2 art-only manifest did not migrate to an existing-native target record.");

    string legacyV2MixedPath = Path.Combine(temporaryRoot, "legacy-v2-valid-plus-malformed.json");
    JsonNode legacyV2Mixed = JsonNode.Parse(await File.ReadAllTextAsync(legacyV2Path))
        ?? throw new InvalidDataException("Could not parse the version-2 manifest fixture.");
    JsonArray legacyV2Rows = legacyV2Mixed["relocations"]?.AsArray()
        ?? throw new InvalidDataException("The version-2 fixture has no relocations array.");
    JsonObject malformedLegacyRow = legacyV2Rows[0]?.DeepClone().AsObject()
        ?? throw new InvalidDataException("The version-2 fixture row could not be cloned.");
    malformedLegacyRow["targetTextureId"] = -1;
    legacyV2Rows.Add(malformedLegacyRow);
    await File.WriteAllTextAsync(
        legacyV2MixedPath,
        legacyV2Mixed.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    Assert(NativeTerrainTextureRelocationEditStore.LoadManifest(legacyV2MixedPath, "artisans").Count == 1,
        "Version-2 compatibility no longer preserves valid legacy rows while skipping a malformed legacy row.");

    string missingV2ModePath = Path.Combine(temporaryRoot, "missing-v2-mode.json");
    string artOnlyJson = await File.ReadAllTextAsync(artOnlyManifest);
    using (JsonDocument validArtOnly = JsonDocument.Parse(artOnlyJson))
    {
        JsonElement saved = validArtOnly.RootElement.GetProperty("relocations").EnumerateArray().Single();
        string missingMode = artOnlyJson.Replace(
            $"      \"applyMode\": \"{NativeTerrainTextureRelocationEditStore.ArtOnlyPreserveTargetMode}\",{Environment.NewLine}",
            "",
            StringComparison.Ordinal);
        await File.WriteAllTextAsync(missingV2ModePath, missingMode);
        AssertInvalidData(
            () => NativeTerrainTextureRelocationEditStore.LoadManifest(missingV2ModePath, "artisans"),
            "A damaged version-3 art-only entry silently downgraded when applyMode was missing.");

        string wrongProvenancePath = Path.Combine(temporaryRoot, "wrong-art-only-provenance.json");
        await File.WriteAllTextAsync(
            wrongProvenancePath,
            artOnlyJson.Replace("native-texture-record:beastmakers:10", "native-texture-record:beastmakers:11", StringComparison.Ordinal));
        AssertInvalidData(
            () => NativeTerrainTextureRelocationEditStore.LoadManifest(wrongProvenancePath, "artisans"),
            "Art-only provenance that disagrees with donor level/texture identity was silently skipped.");

        string unknownModePath = Path.Combine(temporaryRoot, "unknown-mode.json");
        await File.WriteAllTextAsync(
            unknownModePath,
            artOnlyJson.Replace(NativeTerrainTextureRelocationEditStore.ArtOnlyPreserveTargetMode, "invented-mode", StringComparison.Ordinal));
        AssertInvalidData(
            () => NativeTerrainTextureRelocationEditStore.LoadManifest(unknownModePath, "artisans"),
            "An unknown version-3 relocation apply mode was silently skipped.");

        string unknownVersionPath = Path.Combine(temporaryRoot, "unknown-version.json");
        await File.WriteAllTextAsync(
            unknownVersionPath,
            artOnlyJson.Replace("\"version\": 3", "\"version\": 99", StringComparison.Ordinal));
        Assert(NativeTerrainTextureRelocationEditStore.LoadManifest(unknownVersionPath, "artisans").Count == 0,
            "An unknown relocation manifest version was activated.");
    }

    Console.WriteLine("Native terrain texture relocation edit store smoke: PASSED");
    Console.WriteLine("Verified v3 existing/appended target records, source preimages, donor-contract reuse, middle-row compaction/remap, canonical provenance, v1 migration, deterministic round-trip/order, removal, complete-tier enforcement, and malformed-manifest safety.");
}
finally
{
    if (Directory.Exists(temporaryRoot))
        Directory.Delete(temporaryRoot, recursive: true);
}

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

static void AssertInvalidData(Action action, string message)
{
    bool rejected = false;
    try
    {
        action();
    }
    catch (InvalidDataException)
    {
        rejected = true;
    }
    Assert(rejected, message);
}
