using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

string workspaceRoot = args.Length > 0 ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();
string sourceImagePath = Path.Combine(workspaceRoot, "Spyro the Dragon (USA).bin");
LevelCatalog catalog = LevelCatalog.Load(workspaceRoot);
LevelDefinition artisans = catalog.FindByKey("artisans")
    ?? throw new InvalidOperationException("Artisans is missing from the level catalog.");

NativeTerrainTextureInPlaceTransplantRequest request = new(
    TargetTextureId: 54,
    DonorWadEntry: 70,
    DonorTextureId: 22);

string sourceShaBefore = Sha256File(sourceImagePath);
long sourceLengthBefore = new FileInfo(sourceImagePath).Length;
NativeTerrainTextureInPlaceTransplantSourceProof proof =
    NativeTerrainTextureInPlaceTransplantBuilder.InspectSourceProof(
        sourceImagePath,
        artisans,
        request);

Assert(proof.TargetWadEntry == artisans.SourceWadEntry &&
       proof.TargetTextureId == 54 &&
       proof.DonorWadEntry == 70 &&
       proof.DonorTextureId == 22,
    "The source proof is not bound to Artisans texture 54 <- Gnasty's World WAD 70 texture 22.");
Assert(proof.TargetTexturePagesSha256.Length == 64 &&
       proof.TargetDescriptorTableSha256.Length == 64 &&
       proof.DonorTexturePagesSha256.Length == 64 &&
       proof.DonorDescriptorTableSha256.Length == 64,
    "The source proof omitted a full target/donor texture-page or descriptor-table SHA-256 binding.");
Assert(proof.TargetStorageIsolation.TargetOwnedByteCount == 5_632 &&
       proof.TargetStorageIsolation.TargetExclusiveByteCount == 5_632 &&
       proof.TargetStorageIsolation.AnyDecodedConsumerOverlapByteCount == 0 &&
       proof.TargetStorageIsolation.AllConsumerClosureComplete &&
       proof.TargetStorageIsolation.DecodedConsumerExclusive,
    "Artisans texture 54 did not retain its complete 5,632-byte all-consumer-exclusive target proof.");
Assert(proof.TargetRuntimeControlAudit.Complete &&
       proof.TargetRuntimeControlAudit.IsRuntimePersistentTarget(54),
    "Artisans texture 54 is not clear of runtime animation/scrolling control.");

bool built = NativeTerrainTextureInPlaceTransplantBuilder.TryBuild(
    sourceImagePath,
    artisans,
    request,
    proof,
    out NativeTerrainTextureInPlaceTransplantPlan? plan,
    out string failureReason);
Assert(built && plan != null, $"Focused in-place transplant did not build: {failureReason}");
NativeTerrainTextureInPlaceTransplantPlan complete = plan!;

Assert(complete.CompleteDescriptorCount == 23 &&
       complete.LowDetailDescriptorCount == 2 &&
       complete.LeadingLowDetailAliasDescriptorCount == 1 &&
       complete.NormalHighDetailDescriptorCount == 4 &&
       complete.CloseHighDetailDescriptorCount == 16,
    "The in-place plan did not cover the complete 2 LQ + leading alias + 4 normal + 16 close descriptor record.");
Assert(complete.TargetOwnedByteCount == 5_632 &&
       complete.TargetExclusiveByteCount == 5_632 &&
       complete.PlannedPhysicalByteCount == 5_632,
    "The plan did not stay inside the expected 5,632-byte target-owned footprint.");
Assert(complete.ChangedByteCount == 5_181,
    $"The focused transplant changed {complete.ChangedByteCount:N0} bytes instead of the source-locked 5,181 bytes.");
Assert(complete.PhysicalAliasConflictCount == 0 &&
       complete.OutOfOwnershipWriteCount == 0 &&
       complete.ConsistentPhysicalAliasAssignmentCount > 0,
    "The plan reported a physical alias conflict, an out-of-ownership write, or failed to observe the expected LQ aliases.");
Assert(complete.SourceBindingVerified &&
       complete.RuntimeControlClearanceVerified &&
       complete.CompleteOwnershipClosureVerified &&
       complete.DecodedAndExternalExclusivityVerified &&
       complete.ExactDonorIndexedPixelsVerified &&
       complete.ExactDonorPalettesVerified &&
       complete.LowDetailAliasPreserved &&
       complete.TargetDescriptorTableUnchanged &&
       complete.TargetTextureIdPreserved &&
       complete.LogicalReadbackVerified,
    "The complete transplant plan omitted a required safety or exact-readback proof.");
Assert(complete.Patches.Count > 0 &&
       complete.Patches.Sum(patch => patch.ByteLength) == 5_181 &&
       complete.Patches.All(patch =>
           patch.TargetWadEntry == artisans.SourceWadEntry &&
           patch.TargetTextureId == 54 &&
           patch.WadOffset >= 0 &&
           patch.TexturePagesRelativeOffset >= 0 &&
           patch.ByteLength == patch.Before.Length &&
           patch.ByteLength == patch.After.Length &&
           patch.Before.Where((value, index) => value != patch.After[index]).Count() == patch.ByteLength &&
           string.Equals(patch.ExpectedTargetTexturePagesSha256, proof.TargetTexturePagesSha256, StringComparison.OrdinalIgnoreCase) &&
           string.Equals(patch.ExpectedTargetDescriptorTableSha256, proof.TargetDescriptorTableSha256, StringComparison.OrdinalIgnoreCase)),
    "The plan did not emit source-bound WAD-relative before/after patches for every changed byte.");

NativeTerrainTextureInPlaceTransplantSourceProof incompleteOwnership = proof with
{
    TargetStorageIsolation = proof.TargetStorageIsolation with { AllConsumerClosureComplete = false }
};
AssertGuardRejects(incompleteOwnership, "ownership closure", "ownership");

NativeTerrainTextureInPlaceTransplantSourceProof overlappingOwnership = proof with
{
    TargetStorageIsolation = proof.TargetStorageIsolation with { OtherTerrainOverlapByteCount = 1 }
};
AssertGuardRejects(overlappingOwnership, "decoded overlap", "overlap");

AssertGuardRejects(
    proof with { TargetTexturePagesSha256 = new string('0', 64) },
    "target texture-page hash",
    "target");
AssertGuardRejects(
    proof with { TargetDescriptorTableSha256 = new string('1', 64) },
    "target descriptor-table hash",
    "target");
AssertGuardRejects(
    proof with { DonorTexturePagesSha256 = new string('2', 64) },
    "donor texture-page hash",
    "donor");
AssertGuardRejects(
    proof with { DonorDescriptorTableSha256 = new string('3', 64) },
    "donor descriptor-table hash",
    "donor");

NativeTerrainTextureInPlaceTransplantSourceProof incompleteRuntime = proof with
{
    TargetRuntimeControlAudit = proof.TargetRuntimeControlAudit with { Complete = false }
};
AssertGuardRejects(incompleteRuntime, "incomplete runtime-control audit", "runtime");

NativeTerrainTextureRuntimeControl injectedControl = new(
    TextureId: 54,
    Kind: NativeTerrainTextureRuntimeControlKind.FullRecordAnimation,
    PointerIndex: 999,
    SceneRelativeStructureOffset: 0,
    Description: "Negative smoke: injected runtime overwrite of texture 54.");
NativeTerrainTextureInPlaceTransplantSourceProof controlledRuntime = proof with
{
    TargetRuntimeControlAudit = proof.TargetRuntimeControlAudit with
    {
        Controls = proof.TargetRuntimeControlAudit.Controls.Append(injectedControl).ToArray()
    }
};
AssertGuardRejects(controlledRuntime, "runtime-controlled target", "runtime");

string sourceShaAfter = Sha256File(sourceImagePath);
long sourceLengthAfter = new FileInfo(sourceImagePath).Length;
Assert(sourceLengthAfter == sourceLengthBefore &&
       string.Equals(sourceShaAfter, sourceShaBefore, StringComparison.OrdinalIgnoreCase),
    "The read-only proof/build smoke modified the retail source image.");

Console.WriteLine("Native terrain texture in-place transplant smoke passed.");
Console.WriteLine("Pair: Artisans texture 54 <- Gnasty's World WAD 70 texture 22.");
Console.WriteLine($"Descriptor coverage: {complete.CompleteDescriptorCount} (2 LQ + 1 alias + 4 normal + 16 close).");
Console.WriteLine($"Target-exclusive footprint: {complete.TargetExclusiveByteCount:N0} bytes.");
Console.WriteLine($"Changed bytes: {complete.ChangedByteCount:N0}; WAD-relative patches: {complete.Patches.Count:N0}.");
Console.WriteLine($"Consistent physical alias assignments: {complete.ConsistentPhysicalAliasAssignmentCount:N0}; conflicts: {complete.PhysicalAliasConflictCount}.");
Console.WriteLine("Negative guards: ownership, overlap, target/donor page hashes, target/donor table hashes, runtime completeness, and runtime control all rejected.");
Console.WriteLine($"Source SHA-256 unchanged: {sourceShaAfter}.");

void AssertGuardRejects(
    NativeTerrainTextureInPlaceTransplantSourceProof guardedProof,
    string label,
    string expectedFailureFragment)
{
    bool unsafeBuilt = NativeTerrainTextureInPlaceTransplantBuilder.TryBuild(
        sourceImagePath,
        artisans,
        request,
        guardedProof,
        out NativeTerrainTextureInPlaceTransplantPlan? unsafePlan,
        out string guardFailure);
    Assert(!unsafeBuilt && unsafePlan == null,
        $"The {label} negative guard incorrectly emitted a plan.");
    Assert(guardFailure.Contains(expectedFailureFragment, StringComparison.OrdinalIgnoreCase),
        $"The {label} negative guard failed for an unexpected reason: {guardFailure}");
}

static string Sha256File(string path)
{
    using FileStream stream = File.OpenRead(path);
    return Convert.ToHexString(SHA256.HashData(stream));
}

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
