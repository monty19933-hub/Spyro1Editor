using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using System.Reflection;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

const int WadStart = 37 * 2048;
const int TargetBase = 0x1000;
const int SourceBase = 0x300000;
const int TableRelative = 0x200000;
const string RecipeId = "stonehill.peacekeepers.springChest.package.native0149T70Over000ENoControllerNoExeHelper.v19";
string temporary = Path.Combine(Path.GetTempPath(), "spyro-moby-plan-transaction-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temporary);
try
{
    string imagePath = Path.Combine(temporary, "synthetic.iso");
    File.WriteAllBytes(imagePath, CreateSyntheticImage());
    File.WriteAllText(Path.Combine(temporary, "spyro-level-catalog.json"), JsonSerializer.Serialize(new
    {
        levels = new[]
        {
            new { key = "stonehill", displayName = "Stone Hill", levelId = 11, sourceWadEntry = 12,
                sourceTableWadOffset = $"0x{TargetBase + TableRelative:X}", sourceTableRelativeOffset = $"0x{TableRelative:X}", sourceRecordCount = 2 },
            new { key = "peacekeepers", displayName = "Peace Keepers", levelId = 20, sourceWadEntry = 22,
                sourceTableWadOffset = $"0x{SourceBase + TableRelative:X}", sourceTableRelativeOffset = $"0x{TableRelative:X}", sourceRecordCount = 2 }
        }
    }));
    LevelDefinition target = LevelCatalog.Load(temporary).FindByKey("stonehill")!;
    MobyActorPackageImportPreview preview = MobySourcePatchExporter.InspectActorPackageImport(
        imagePath, temporary, target, "peacekeepers", "springChest", RecipeId);
    Require(preview.CopySegments.Count == 1 && preview.RootEntries.Count == 1,
        "Synthetic image did not exercise a validated native actor package/root recipe.");
    // Synthetic evidence admits only this generated fixture into the normal
    // planner, avoiding the real-ROM-only emulator shortcut and any image write.
    string evidenceDirectory = Path.Combine(temporary, "_local", "objects");
    Directory.CreateDirectory(evidenceDirectory);
    File.WriteAllText(Path.Combine(evidenceDirectory, "synthetic.candidate-result.json"), JsonSerializer.Serialize(new
    {
        candidate = new { levelKey = target.Key, recipes = new[] { new { recipeId = RecipeId,
            recipeFingerprint = CrossLevelCandidateRecipeFingerprint.Create(preview) } } },
        result = new { status = "passed", bootedLevel = true, objectAppeared = true,
            behaviorCorrect = true, noNearbyRegression = true }
    }));

    MobySourcePatchPlan rejected = Plan(Edit("rejected", 99));
    Require(rejected.SkippedEdits.Any(reason => reason.Contains("donor source record is not mapped", StringComparison.Ordinal)),
        "The invalid donor was not rejected by the actual BuildPlan path.");
    Require(rejected.Patches.Count == 0,
        $"Rejected Moby retained {rejected.Patches.Count} orphan patches: {string.Join(", ", rejected.Patches.Select(patch => patch.Kind))}.");
    Require(Plan(Edit("rejected-properties", 1)).Patches.Count == 0,
        "Rejected appended-instance properties left an installed package.");
    Require(Plan(Edit("rejected-render-type", 0, type: 0x0A)).Patches.Count == 0,
        "Rejected appended-instance type left an installed package.");
    Require(Plan(Edit("rejected-transform-properties", 1, added: false)).Patches.Count == 0,
        "Rejected existing-slot transform properties left an installed package.");
    Require(Plan(Edit("rejected-transform-donor", 99, added: false)).Patches.Count == 0,
        "An unmapped transform donor was treated as an accepted instance and installed its package.");

    MobySourcePatchPlan baseline = Plan(Edit("accepted", 0));
    Require(baseline.Patches.Any(patch => patch.Kind == "actor-package-copy") &&
            baseline.Patches.Any(patch => patch.Kind == "moby-record-append"),
        "The valid fixture did not exercise shared package installation and Moby append.");
    Require(Plan(Edit("missing-class-high-byte", 0, actorId: 0x249)).Patches.Count == 0,
        "A source-bound actor package pass admitted an instance class not supplied by the package or destination roots.");
    Require(Plan(Edit("missing-class-raw-byte", 0, rawActorHighByte: 2)).Patches.Count == 0,
        "A final sourceByteEdits class override bypassed actor dependency validation.");
    Require(Plan(Edit("unrelated-resident-class", 0, actorId: 0x108)).Patches.Count == 0,
        "An unrelated resident root allowed an undeclared donor class substitution.");
    Require(Plan(Edit("valid-existing-transform", 0, added: false)).Patches.Any(patch => patch.Kind == "actor-package-copy"),
        "An exact native existing-slot transform lost its dependency package.");
    Require(Plan(Edit("missing-class-existing-transform", 0, added: false, actorId: 0x249)).Patches.Count == 0,
        "Existing-slot transforms bypassed final native class/root dependency validation.");
    MobySourcePatchPlan rejectedThenAccepted = Plan(Edit("rejected", 99), Edit("accepted", 0));
    MobySourcePatchPlan acceptedThenRejected = Plan(Edit("accepted", 0), Edit("rejected", 99));
    Require(Describe(baseline) == Describe(rejectedThenAccepted),
        "A rejected earlier object consumed a shared package reservation or append slot.");
    Require(Describe(baseline) == Describe(acceptedThenRejected),
        "Rolling back a later rejected object removed the successful prior object's shared package.");
    Require(rejectedThenAccepted.PackageImportPreviews.Count(preview => preview.CanWriteImage) == 1 &&
            acceptedThenRejected.PackageImportPreviews.Count(preview => preview.CanWriteImage) == 1,
        "Rejected object previews still activate package-dependent helpers or runtime evidence.");
    Require(rejectedThenAccepted.EditOutcomes!.Single(outcome => outcome.MobyLabel == "rejected").PatchKinds.Count == 0,
        "Rejected edit outcome still claims package writes.");
    MobyActorPackageImportPreview diagnostic = rejected.PackageImportPreviews.Single();
    MobyActorPackageImportPreview multiRootAlias = baseline.PackageImportPreviews.Single() with
    {
        RecipeId = "stonehill.townsquare.springChest.package.fullControllerAlias01FEZeroGap26928.v14",
        SourceLevelKey = "townsquare", RecipeMode = "RegisterCompanionRootAlias01FEFullDependencyRebase",
        RootEntries = new[] { 0x1FE, 0x186, 0x149 }.Select((actor, index) =>
            baseline.PackageImportPreviews.Single().RootEntries.Single() with
            { RootIndex = index, TargetActorId = $"0x{actor:X4}" }).ToArray()
    };
    HashSet<ushort> controllerRemaps = InvokeArgs<HashSet<ushort>>("GetDeclaredInstanceActorRemaps",
        new[] { multiRootAlias }, 30, (ushort)0xC2);
    Require(controllerRemaps.SetEquals(new ushort[] { 0x1FE }),
        "Imported helper/shell roots were incorrectly treated as controller class-remap authority.");
    Require(InvokeArgs<HashSet<ushort>>("GetDeclaredInstanceActorRemaps", new[] { multiRootAlias }, 30, (ushort)0x149).Count == 0 &&
            InvokeArgs<HashSet<ushort>>("GetDeclaredInstanceActorRemaps", new[] { multiRootAlias }, 31, (ushort)0xC2).Count == 0,
        "An instance alias escaped its exact donor class and source row.");
    Require(!Invoke<bool>("UsesSpringChestControllerAlias", new[] { diagnostic with { RecipeMode = "Alias01FE" } }),
        "A rolled-back package diagnostic still selected a controller actor alias.");
    Require(Invoke<int>("ResolveStoneHillSpringChestRuntimeVariantId", new[]
        {
            diagnostic with { RecipeMode = "T71" },
            baseline.PackageImportPreviews.Single() with { RecipeMode = "T70" }
        }) == 0x01A6,
        "A rolled-back package diagnostic still selected the runtime variant before a committed package.");
    Require(!Directory.EnumerateFiles(temporary, "*.bin").Any() &&
            File.ReadAllBytes(imagePath).AsSpan().SequenceEqual(CreateSyntheticImage()),
        "Plan-only transaction checks changed the source or published a BIN.");
    Console.WriteLine("MOBY_PLAN_TRANSACTION_SMOKE_OK synthetic-BuildPlan; rejected-only, rejected-before-success, rejected-after-success, exact shared package preservation");

    MobySourcePatchPlan Plan(params object[] edits)
    {
        string editsPath = Path.Combine(temporary, "edits.json");
        File.WriteAllText(editsPath, JsonSerializer.Serialize(new { edits }));
        return MobySourcePatchExporter.BuildPlan(imagePath, "", Path.Combine(temporary, "never-written.bin"),
            Path.Combine(temporary, "never-written.cue"), target, editsPath);
    }
}
finally { Directory.Delete(temporary, recursive: true); }

static object Edit(string label, int sourceTrueIndex, bool added = true, int type = 0x20, int actorId = 0x149, int? rawActorHighByte = null) => new
{
    label, trueIndex = added ? 2 : 0, added, editKind = added ? "add" : "update", typeHex = $"0x{type:X2}", stateHex = "0x00",
    sourceByte36Hex = $"0x{actorId & 255:X2}", sourceByte37Hex = $"0x{actorId >> 8:X2}", sourceByte4FHex = "0x00", flag4AHex = "0x10", flag4BHex = "0xFF",
    sourceByte36OriginalHex = added ? null : "0x00", sourceByte36EditedHex = $"0x{actorId & 255:X2}",
    sourceByte37OriginalHex = added ? null : "0x00", sourceByte37EditedHex = $"0x{actorId >> 8:X2}",
    sourceByteEdits = new[]
    {
        new { offset = 0x36, valueHex = $"0x{actorId & 255:X2}" },
        new { offset = 0x37, valueHex = $"0x{rawActorHighByte ?? (actorId >> 8):X2}" }
    },
    crossLevelTemplate = new { id = "synthetic.spring", sourceLevelKey = "peacekeepers", sourceLevelName = "Peace Keepers",
        sourceTrueIndex, family = "springChest", recipeId = RecipeId, requiredExporterFeature = "ActorPackageImportAndPointerRebase" }
};

static string Describe(MobySourcePatchPlan plan) => string.Join('\n', plan.Patches.Select(patch =>
    $"{patch.Kind}|{patch.WadRelativeOffset}|{patch.BeforeHexPreview}|{patch.AfterHexPreview}"));

static byte[] CreateSyntheticImage()
{
    byte[] image = new byte[WadStart + SourceBase + TableRelative + 0x800];
    Span<byte> pvd = image.AsSpan(16 * 2048, 2048);
    pvd[0] = 1;
    Encoding.ASCII.GetBytes("CD001").CopyTo(pvd[1..]);
    pvd[6] = 1;
    BinaryPrimitives.WriteUInt16LittleEndian(pvd[128..], 2048);
    BinaryPrimitives.WriteUInt16BigEndian(pvd[130..], 2048);
    pvd[156] = 34;
    pvd[188] = 1;
    pvd[189] = 0;
    BinaryPrimitives.WriteInt32LittleEndian(pvd[158..], 18);
    BinaryPrimitives.WriteInt32BigEndian(pvd[162..], 18);
    BinaryPrimitives.WriteInt32LittleEndian(pvd[166..], 2048);
    BinaryPrimitives.WriteInt32BigEndian(pvd[170..], 2048);
    Span<byte> target = image.AsSpan(WadStart + TargetBase);
    BinaryPrimitives.WriteInt32LittleEndian(target, 0x800);
    BinaryPrimitives.WriteInt32LittleEndian(target[4..], 0x1E0000 - 0x800);
    for (int index = 0; index <= 21; index++)
    {
        BinaryPrimitives.WriteInt32LittleEndian(target[(0x50 + index * 4)..], index == 21 ? 0x1D5CC8 : 0x1000 + index * 0x100);
        BinaryPrimitives.WriteUInt16LittleEndian(target[(0x150 + index * 2)..], index == 21 ? (ushort)0xE : (ushort)(0x100 + index));
    }
    BinaryPrimitives.WriteInt32LittleEndian(target[(TableRelative - 4)..], 2);
    Span<byte> source = image.AsSpan(WadStart + SourceBase);
    source.Slice(0x1B3450, 0x528).Fill(0x42);
    Span<byte> donor = source.Slice(TableRelative, 0x58);
    donor[0x36] = 0x49;
    donor[0x37] = 1;
    donor[0x50] = 0x20;
    donor[0x52] = 0x10;
    donor[0x53] = 0xFF;
    donor.CopyTo(source.Slice(TableRelative + 0x58, 0x58));
    BinaryPrimitives.WriteUInt32LittleEndian(source[(TableRelative + 0x58)..], uint.MaxValue);
    return image;
}

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static T Invoke<T>(string name, object previews) => InvokeArgs<T>(name, previews);

static T InvokeArgs<T>(string name, params object[] arguments) => (T)(typeof(MobySourcePatchExporter)
    .GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!
    .Invoke(null, arguments) ?? throw new InvalidOperationException($"No result from {name}."));
