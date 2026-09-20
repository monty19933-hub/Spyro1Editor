using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Persistence;
using static Cases;
using P = SavedLevelPublicationCases;

// PRIVATE tests-first, tiny owned paths only. No source or physical mode invoked.
internal static class SavedLevelPublicationArgumentCases
{
    internal static async Task Free()
    {
        string[] originalModes = ["--publish-saved-level-reviewed", "--verify-saved-level-reviewed",
            "--partial-saved-level-reviewed", "--stale-saved-level-reviewed", "--unconfirmed-saved-level-reviewed",
            "--guard-release-saved-level-reviewed", "--artifact-saved-level-reviewed",
            "--artifact-notify-saved-level-reviewed", "--cancel-saved-level-reviewed"];
        string[] familyModes=["--publish-family-saved-level-reviewed","--verify-family-saved-level-reviewed","--stale-family-saved-level-reviewed"];
        string[] tiledModes=["--publish-tiled-saved-level-reviewed","--verify-tiled-saved-level-reviewed","--stale-tiled-saved-level-reviewed"];
        string[] tiledMaterialModes=["--publish-tiled-material-saved-level-reviewed","--verify-tiled-material-saved-level-reviewed","--stale-tiled-material-saved-level-reviewed"];
        string[] selectedMaterialModes=["--publish-selected-tiled-material-saved-level-reviewed","--verify-selected-tiled-material-saved-level-reviewed","--stale-selected-tiled-material-saved-level-reviewed"];
        string[] lockedChestModes = originalModes.Select(mode => mode.Replace("-saved-level-", "-locked-chest-saved-level-", StringComparison.Ordinal)).ToArray();
        Check(P.Modes.Take(9).SequenceEqual(originalModes) && P.Modes.Skip(9).Take(3).SequenceEqual(new[] {
            "--publish-mixed-saved-level-reviewed", "--verify-mixed-saved-level-reviewed", "--stale-mixed-saved-level-reviewed" }),
            "Existing twelve reviewed modes retain their exact order and meaning.");
        Check(P.Modes.Skip(12).Take(3).SequenceEqual(familyModes)&&P.ExpectedFamilyImage==
            "408dfdfc577f991e635e4c0b636344333ca4b2654d89afabd88af88cc1a55c98",
            "Exactly three family modes use the observed full-source regression reference, not an independent oracle.");
        Check(P.Modes.Skip(15).Take(3).SequenceEqual(tiledModes) && P.ExpectedTiledImage == TiledSavedCases.ExpectedImage &&
            P.ExpectedTiledImage == "7ba0b29b68a1bee415901382c3a3b5345ccb01ca84872502131f68361afdc305",
            "Exactly three tiled modes use the separately frozen independent full-image reference.");
        Check(P.Modes.Skip(18).Take(3).SequenceEqual(tiledMaterialModes) && P.ExpectedTiledMaterialImage == TiledMaterialSavedCases.ExpectedImage &&
            P.ExpectedTiledMaterialImage == "462c992f80f512f0b6be781b1a79e707e37a0e18f5e5e47356cd332ef61b2ba8",
            "Exactly three appended material-tile modes use the independently frozen full-image reference without renumbering old modes.");
        Check(P.Modes.Skip(21).Take(3).SequenceEqual(selectedMaterialModes) && P.ExpectedSelectedOneImage ==
            "14e5b3606f652c250df9cdd6c94de1d393186d1b74f1ab64decd5dd35130a77f" && P.ExpectedSelectedThreeImage ==
            "f1c137476833d1c046a78df812b40a47f8dc6558051c823437d7c5b3f86d8e32",
            "One appended selected-material mode family uses the two independent complete-image references; no modes per source count.");
        Check(P.Modes.Skip(24).SequenceEqual(lockedChestModes) && P.ExpectedLockedChestImage ==
            "a5b00725388f73e7e6f7b9ed356c4a0d0f55b1c869d6a65f38e97ef2641d6c50" &&
            P.Modes.Take(24).All(mode => !P.IsLockedChestMode(mode)) &&
            lockedChestModes.Select(P.OperationMode).SequenceEqual(originalModes),
            "Nine exact chest modes reuse every existing physical outcome gate without renumbering the earlier24 modes.");
        string[] valid = [P.Modes[0], "original.bin", "original.cue", "/private/tmp/test-project", "test-project", "/private/tmp/test-output"];
        foreach (string mode in P.Modes)
        {
            var args = valid.ToArray(); args[0] = mode;
            Check(P.Parse(args).Mode == mode && P.IsMode(args), "Exact six-token supported mode.");
        }
        Check(originalModes.All(mode => !P.IsMixedMode(mode) && P.OperationMode(mode) == mode) &&
            P.Modes.Skip(9).Take(3).All(P.IsMixedMode) && P.OperationMode(P.Modes[9]) == P.Modes[0] &&
            P.OperationMode(P.Modes[10]) == P.Modes[1] && P.OperationMode(P.Modes[11]) == P.Modes[3],
            "Mixed modes reuse only publish, verify and stale implementations; existing fault selections are unchanged.");
        Check(P.Modes.Take(12).All(mode=>!P.IsFamilyMode(mode))&&familyModes.All(mode=>P.IsFamilyMode(mode)&&!P.IsMixedMode(mode))&&
            P.OperationMode(familyModes[0])==P.Modes[0]&&P.OperationMode(familyModes[1])==P.Modes[1]&&
            P.OperationMode(familyModes[2])==P.Modes[3],"Family modes map to the same publisher, verifier and stale-transaction test.");
        Check(P.Modes.Take(15).All(mode => !P.IsTiledMode(mode)) &&
            tiledModes.All(mode => P.IsTiledMode(mode) && !P.IsMixedMode(mode) && !P.IsFamilyMode(mode)) &&
            P.OperationMode(tiledModes[0]) == P.Modes[0] && P.OperationMode(tiledModes[1]) == P.Modes[1] &&
            P.OperationMode(tiledModes[2]) == P.Modes[3],
            "Tiled modes map only to the existing publisher, verifier and stale fault without changing legacy modes.");
        Check(P.Modes.Take(18).All(mode => !P.IsTiledMaterialMode(mode)) &&
            tiledMaterialModes.All(mode => P.IsTiledMaterialMode(mode) && !P.IsTiledMode(mode) && !P.IsMixedMode(mode) && !P.IsFamilyMode(mode)) &&
            P.OperationMode(tiledMaterialModes[0]) == P.Modes[0] && P.OperationMode(tiledMaterialModes[1]) == P.Modes[1] &&
            P.OperationMode(tiledMaterialModes[2]) == P.Modes[3],
            "Material-tile modes reuse only the existing publisher, verifier and stale fault; no alternate writer.");
        Check(P.Modes.Take(21).All(mode => !P.IsSelectedTiledMaterialMode(mode)) &&
            selectedMaterialModes.All(mode => P.IsSelectedTiledMaterialMode(mode) && !P.IsTiledMaterialMode(mode) &&
                !P.IsTiledMode(mode) && !P.IsMixedMode(mode) && !P.IsFamilyMode(mode)) &&
            P.OperationMode(selectedMaterialModes[0]) == P.Modes[0] && P.OperationMode(selectedMaterialModes[1]) == P.Modes[1] &&
            P.OperationMode(selectedMaterialModes[2]) == P.Modes[3],
            "Selected N1/N3 modes reuse the exact existing publisher/verifier/stale workflow; all old21 mode indices remain unchanged.");
        byte[] materialDocument = NativeFreshLevelDocumentCodec.SerializeCanonical(Document(14, 1, 70, 1, 128, 20));
        byte[] mixedDocument = NativeFreshLevelDocumentCodec.SerializeCanonical(MixedSavedCases.Document());
        byte[] familyDocument=NativeFreshLevelDocumentCodec.SerializeCanonical(FamilySavedCases.Document());
        byte[] lockedChestDocument = NativeFreshLevelDocumentCodec.SerializeCanonical(SavedLockedChestCompilationCases.Document());
        byte[] tiledDocument=NativeFreshLevelDocumentCodec.SerializeCanonical(SavedTiledDocumentFixture.Document());
        byte[] tiledMaterialDocument=NativeFreshLevelDocumentCodec.SerializeCanonical(SavedTiledMaterialDocumentFixture.Document());
        var selectedOne = SelectedTiledMaterialSavedCases.Document([0, 0, 0, 0, 0, 0]);
        var selectedThree = SelectedTiledMaterialSavedCases.Document([0, 1, 2, 0, 1, 2]);
        byte[] selectedOneDocument = NativeFreshLevelDocumentCodec.SerializeCanonical(selectedOne);
        byte[] selectedThreeDocument = NativeFreshLevelDocumentCodec.SerializeCanonical(selectedThree);
        Check(P.RequireSelectedFixtureDocument(selectedOneDocument) == (P.ExpectedSelectedOneImage, 1) &&
            P.RequireSelectedFixtureDocument(selectedThreeDocument) == (P.ExpectedSelectedThreeImage, 3),
            "Only complete exact canonical bytes choose independent N1/N3 references and expected actual source counts.");
        // No reviewed mode may borrow the other arm's canonical fixture.
        foreach (string mode in P.Modes)
        {
            bool selected = P.IsSelectedTiledMaterialMode(mode);
            byte[] actual=P.IsLockedChestMode(mode)?lockedChestDocument:selected?selectedOneDocument:P.IsTiledMaterialMode(mode)?tiledMaterialDocument:P.IsTiledMode(mode)?tiledDocument:P.IsFamilyMode(mode)?familyDocument:P.IsMixedMode(mode)?mixedDocument:materialDocument;
            P.RequireFixtureDocument(mode,actual);
            if (selected) P.RequireFixtureDocument(mode, selectedThreeDocument);
            foreach(byte[] foreign in new[]{materialDocument,mixedDocument,familyDocument,tiledDocument,tiledMaterialDocument,selectedOneDocument,selectedThreeDocument,lockedChestDocument}
                .Where(value=>!ReferenceEquals(value,actual) && !(selected && ReferenceEquals(value,selectedThreeDocument))))
                await Throws<InvalidDataException>(()=>{P.RequireFixtureDocument(mode,foreign);return Task.CompletedTask;});
        }
        foreach(string mode in familyModes.Concat(tiledModes).Concat(tiledMaterialModes).Concat(selectedMaterialModes).Concat(lockedChestModes))
        {
            foreach(string altered in new[]{mode+"-extra",mode[..^1],mode.ToUpperInvariant()})
            {
                var args=valid.ToArray();args[0]=altered;
                Check(!P.IsMode(args),"Family/tiled command prefixes/case changes never select an explicit reviewed action.");
                await Throws<ArgumentException>(()=>{P.Parse(args);return Task.CompletedTask;});
            }
            var wrong=NativeFreshLevelDocumentCodec.SerializeCanonical(P.IsSelectedTiledMaterialMode(mode)
                ? ChangeFirstId(selectedThree) : P.IsTiledMaterialMode(mode)
                ? SavedTiledMaterialDocumentFixture.Document(firstId:4.ToString("x32")) : P.IsTiledMode(mode)
                ? SavedTiledDocumentFixture.Document(firstId:4.ToString("x32")) : FamilySavedCases.Document(x:9153));
            await Throws<InvalidDataException>(()=>{P.RequireFixtureDocument(mode,wrong);return Task.CompletedTask;});
        }
        var tiled = SavedTiledDocumentFixture.Document(); var content = tiled.TiledContent!;
        foreach (var alternate in new[] {
            tiled.WithTiledContent(content.WithTiles(content.Tiles.Reverse())),
            tiled.WithTiledContent(content.WithSettings(8976, content.SpawnY, content.SpawnZ, content.Heading, content.SkyColor, content.MusicTrackId)),
            tiled.WithTiledContent(content.WithSettings(content.SpawnX, content.SpawnY, content.SpawnZ, content.Heading, content.SkyColor, 20)),
            SavedTiledDocumentFixture.BoundaryDocument(46) }.Concat(SavedTiledDocumentFixture.Unsupported().Select(item => item.Document)))
            foreach (string mode in tiledModes)
                await Throws<InvalidDataException>(() => { P.RequireFixtureDocument(mode, NativeFreshLevelDocumentCodec.SerializeCanonical(alternate)); return Task.CompletedTask; });
        var selectedContent = selectedThree.TiledContent!;
        var thirdDescriptor = selectedThree.WithTiledContent(selectedContent.WithTiles(selectedContent.Tiles.Select(tile =>
            NativeFreshFlatTileIntent.Create(tile.EditorId, tile.X, tile.Y, tile.Z, tile.Width, tile.Depth, tile.FloorColor,
                NativeFreshFloorMaterialsIntent.Create(ChangeThirdDescriptor(tile.Materials.Triangle0), ChangeThirdDescriptor(tile.Materials.Triangle1))))));
        foreach (var alternate in new[]
        {
            SelectedTiledMaterialSavedCases.Document([0, 0, 0, 0, 0, 255]), // Only the last face changes.
            SelectedTiledMaterialSavedCases.Document([0, 1, 2, 0, 1, 0]),
            SelectedTiledMaterialSavedCases.Document([0, 1, 255, 2, 0, 255]),
            thirdDescriptor, ChangeFirstId(selectedOne), ChangeFirstId(selectedThree),
            SavedTiledDocumentFixture.Document(), SavedTiledMaterialDocumentFixture.Document(),
            selectedThree.WithTiledContent(selectedContent.WithTiles(selectedContent.Tiles.Reverse())),
            selectedThree.WithPlacements([NativeFreshWizardRouteIntent.Create(new string('a', 32), 13056, 8960, 8704, 13568, 8960)]),
            selectedOne.WithPlacements([NativeFreshMobyPlacementIntent.Create(new string('b', 32),
                NativeFreshRetailMobyReference.Create(Cases.Variant, 10, 121, 501), 13056, 8960, 8704, 64)])
        })
        {
            byte[] changed = NativeFreshLevelDocumentCodec.SerializeCanonical(alternate);
            await Throws<InvalidDataException>(() => { P.RequireSelectedFixtureDocument(changed); return Task.CompletedTask; });
            foreach (string mode in selectedMaterialModes)
                await Throws<InvalidDataException>(() => { P.RequireFixtureDocument(mode, changed); return Task.CompletedTask; });
        }
        var tiledMaterial = SavedTiledMaterialDocumentFixture.Document(); var materialContent = tiledMaterial.TiledContent!;
        foreach (var alternate in new[] {
            tiledMaterial.WithTiledContent(materialContent.WithTiles(materialContent.Tiles.Reverse())),
            tiledMaterial.WithTiledContent(materialContent.WithSettings(8976, materialContent.SpawnY, materialContent.SpawnZ,
                materialContent.Heading, materialContent.SkyColor, materialContent.MusicTrackId)),
            tiledMaterial.WithTiledContent(materialContent.WithSettings(materialContent.SpawnX, materialContent.SpawnY, materialContent.SpawnZ,
                materialContent.Heading, materialContent.SkyColor, 20)),
            SavedTiledMaterialDocumentFixture.Document([0, 1, 255, 1, 0, 255]),
            SavedTiledMaterialDocumentFixture.Document([0, 1, 0, 1, 1, 0]),
            SavedTiledMaterialDocumentFixture.Document([255, 255, 0, 1, 0, 1]),
            SavedTiledMaterialDocumentFixture.BoundaryDocument(58) }.Concat(SavedTiledMaterialDocumentFixture.Unsupported().Select(item => item.Document)))
            foreach (string mode in tiledMaterialModes)
                await Throws<InvalidDataException>(() => { P.RequireFixtureDocument(mode, NativeFreshLevelDocumentCodec.SerializeCanonical(alternate)); return Task.CompletedTask; });
        await Throws<ArgumentException>(() => { P.OperationMode("--unknown"); return Task.CompletedTask; });
        foreach (var args in new[] { Array.Empty<string>(), valid[..5], valid.Concat(new[] { "extra" }).ToArray(),
            new[] { "--unknown", "b", "c", "/p", "id", "/o" }, new[] { "--source", "b", "c" } })
            await Throws<ArgumentException>(() => { P.Parse(args); return Task.CompletedTask; });
        foreach (int slot in Enumerable.Range(1, 5))
            foreach (string invalid in new[] { "", " ", "\0", "prefix\0suffix", "--source", P.Modes[1] })
            {
                var args = valid.ToArray(); args[slot] = invalid;
                await Throws<ArgumentException>(() => { P.Parse(args); return Task.CompletedTask; });
            }
        foreach (int slot in new[] { 3, 5 })
        { var args = valid.ToArray(); args[slot] = "relative"; await Throws<ArgumentException>(() => { P.Parse(args); return Task.CompletedTask; }); }
        foreach (string id in new[] { "UPPER", "bad space", "../other", new string('a', 65) })
        { var args = valid.ToArray(); args[4] = id; await Throws<ArgumentException>(() => { P.Parse(args); return Task.CompletedTask; }); }
        foreach (string mode in selectedMaterialModes)
        {
            var args = valid.ToArray(); args[0] = mode;
            // Malformed arguments must stop the actual entry point before path
            // location checks or acquisition, even with no real source/project.
            await Throws<ArgumentException>(() => P.RunAsync(args[..5]));
            await Throws<ArgumentException>(() => P.RunAsync(args.Concat(new[] { "extra" }).ToArray()));
            foreach (int slot in Enumerable.Range(1, 5))
                foreach (string invalid in new[] { "", " ", "\0", "prefix\0suffix", "--source", P.Modes[1] })
                {
                    var invalidArgs = args.ToArray(); invalidArgs[slot] = invalid;
                    await Throws<ArgumentException>(() => P.RunAsync(invalidArgs));
                }
        }

        // Resolve only actually observed temp-ancestor links; do not weaken CLI.
        string root = Path.GetFullPath(Root);
        for (var parent = Directory.GetParent(root); parent != null; parent = parent.Parent)
            if (parent.LinkTarget != null)
                root = Path.Combine(parent.ResolveLinkTarget(true)!.FullName, Path.GetRelativePath(parent.FullName, root));
        string project = Path.Combine(root, "physical-argument-project"), inputs = Path.Combine(root, "physical-argument-inputs");
        Directory.CreateDirectory(project); Directory.CreateDirectory(inputs);
        string bin = Path.Combine(inputs, "tiny.bin"), cue = Path.Combine(inputs, "tiny.cue"), output = Path.Combine(root, "absent-physical-args-output");
        File.WriteAllBytes(bin, [1]); File.WriteAllBytes(cue, [2]);
        Check(P.RequireLocation(output, bin, cue, project, false) == output && !Directory.Exists(output), "Preflight is non-writing.");
        foreach (string blocked in new[] { project, Path.Combine(project, "inside"), inputs, bin, cue })
            await Throws<InvalidOperationException>(() => { P.RequireLocation(blocked, bin, cue, project, false); return Task.CompletedTask; });
        await Throws<InvalidDataException>(() => { P.RequireLocation(Path.Combine(root, "missing-parent", "out"), bin, cue, project, false); return Task.CompletedTask; });
        string occupied = Path.Combine(root, "occupied-physical-args-output"); Directory.CreateDirectory(occupied);
        await Throws<InvalidDataException>(() => { P.RequireLocation(occupied, bin, cue, project, false); return Task.CompletedTask; });
        string dangling = Path.Combine(root, "dangling-physical-args-output");
        File.CreateSymbolicLink(dangling, Path.Combine(root, "missing-physical-target"));
        await Throws<InvalidDataException>(() => { P.RequireLocation(dangling, bin, cue, project, false); return Task.CompletedTask; });
        await Throws<InvalidDataException>(() => { P.RequireLocation(output, bin, cue, project, true); return Task.CompletedTask; });
        // Exercise THIS CLI's acquired-owner failure path, not just the shared
        // SourceOwners component. Invalid tiny CUE refuses before any saved
        // capture/native Candidate. Final checks must still inspect both names
        // and held descriptors, then close every acquired owner.
        var early = await Throws<IOException>(() => P.RunAsync(
            [P.Modes[0], bin, cue, project, "tiny-project", output]));
        var errors = Leaves(early);
        Check(errors.Count == 5 && errors.All(e => e is InvalidDataException) &&
            errors[0].Message == errors[3].Message && errors[0].Message == errors[4].Message &&
            errors[1].Message == errors[2].Message && errors[0].Message != errors[1].Message,
            "Actual CUE primary, held/current BIN failures, then held/current CUE failures, once each.");
        var mixedEarly = await Throws<IOException>(() => P.RunAsync(
            [P.Modes[9], bin, cue, project, "tiny-project", output]));
        Check(Leaves(mixedEarly).Select(e => (e.GetType(), e.Message)).SequenceEqual(
            errors.Select(e => (e.GetType(), e.Message))),
            "Mixed public-factory mode keeps the same tiny-source primary and independent final-owner failure ordering before capture.");
        string verifyOutput=Path.Combine(root,"family-verify-argument-output");Directory.CreateDirectory(verifyOutput);
        foreach(string name in Spyro.Editor.Core.Exporting.NativeFreshSavedLevelResearchCandidatePlan.OutputNames)
            P.WriteNewSmall(Path.Combine(verifyOutput,name),[3]);
        foreach(string mode in familyModes.Concat(tiledModes).Concat(tiledMaterialModes).Concat(selectedMaterialModes).Concat(lockedChestModes))
        {
            bool verify=P.OperationMode(mode)==P.Modes[1];string target=verify?verifyOutput:output;
            var args=valid.ToArray();args[0]=mode;
            await Throws<ArgumentException>(()=>{P.Parse(args[..5]);return Task.CompletedTask;});
            args[5]="relative";await Throws<ArgumentException>(()=>{P.Parse(args);return Task.CompletedTask;});
            var familyEarly=await Throws<IOException>(()=>P.RunAsync([mode,bin,cue,project,"tiny-project",target]));
            Check(Leaves(familyEarly).Select(e=>(e.GetType(),e.Message)).SequenceEqual(errors.Select(e=>(e.GetType(),e.Message))),
                "Actual family/tiled "+mode+" reaches safe tiny original-owner refusal before Candidate capture or publication.");
        }
        foreach (var (path, expected) in new[] { (bin, (byte)1), (cue, (byte)2) })
        {
            using var reopened = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
            Check(reopened.Length == 1 && reopened.ReadByte() == expected && reopened.ReadByte() == -1 &&
                reopened.Position == 1 && reopened.Length == 1, "Every tiny held owner closed and exact original fixture bytes unchanged.");
        }
        Check(!Directory.EnumerateFileSystemEntries(project).Any(), "Early refusal never captured/saved a project.");
        Check(!Directory.Exists(output), "Argument-only controls never create an output.");
        Check(Directory.GetParent(verifyOutput)!.FullName==root&&!verifyOutput.StartsWith(project+Path.DirectorySeparatorChar,StringComparison.Ordinal)&&
            Directory.EnumerateFileSystemEntries(verifyOutput).Count()==3&&Directory.EnumerateFiles(verifyOutput).All(path=>
                P.ReadSmall(path,1).SequenceEqual(new byte[]{3})),
            "Three explicitly test-owned tiny verification companions remain unchanged outside the project; no native image was emitted.");
        Console.WriteLine("SAVED_LEVEL_PHYSICAL_ARGUMENTS_OK originalOpened=false tinyInputsOpened=true candidateCaptureReached=false nativeOutput=false");
    }

    private static NativeFreshLevelDocumentIntent ChangeFirstId(NativeFreshLevelDocumentIntent document) =>
        document.WithTiledContent(document.TiledContent!.WithTiles(document.TiledContent.Tiles.Select((tile, index) => index == 0
            ? NativeFreshFlatTileIntent.Create(4.ToString("x32"), tile.X, tile.Y, tile.Z, tile.Width, tile.Depth, tile.FloorColor, tile.Materials) : tile)));
    private static NativeFreshFloorMaterialIntent ChangeThirdDescriptor(NativeFreshFloorMaterialIntent material) =>
        material is NativeFreshRetailTerrainMaterialIntent { SourceWadEntry: 14, TextureId: 1 }
            ? NativeFreshFloorMaterialIntent.RetailTerrain(Cases.Variant, 14, 2) : material;
}
