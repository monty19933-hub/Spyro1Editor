using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Analysis;

/// <summary>
/// The six independent gates that must all be backed by exact, hashed evidence
/// before an UnknownNonZero range may be called candidate-dead storage even in
/// research. This is deliberately not a production/export safety proof.
/// </summary>
internal enum NativeTextureDeadStoragePromotionCondition
{
    ExactSourceAndPreimageBinding,
    GpuProducerAndCopySourceEnumeration,
    ReachableSourceValueClosure,
    ArchiveProvenanceAndLifetimeClosure,
    RuntimeMutationAndTransitionClosure,
    ImmutableKnownOwnedReadbackTraceAndDuckStationProof
}

internal sealed record NativeTextureDeadStorageEvidenceArtifact(
    string Kind,
    string Identity,
    string Sha256);

internal sealed record NativeTextureDeadStorageConditionEvidence(
    NativeTextureDeadStoragePromotionCondition Condition,
    bool Complete,
    string Summary,
    IReadOnlyList<NativeTextureDeadStorageEvidenceArtifact> Artifacts);

internal sealed record NativeTextureDeadStoragePromotionAttestation(
    string ExpectedRetailDiscSha256,
    string ExpectedAddressableTexturePagesSha256,
    string ExpectedUnknownRangeLayoutSha256,
    string ExpectedSelectedRangeLayoutSha256,
    string ExpectedSelectedPreimageSha256,
    IReadOnlyList<NativeTextureDeadStorageConditionEvidence> Conditions);

internal sealed record NativeTextureDeadStorageCandidateRange(
    int TexturePageOffset,
    long TexturePagesWadOffset,
    int Length,
    string PreimageSha256,
    string DeterministicPoisonSha256);

/// <summary>
/// Read-only range and hash plan. It intentionally contains neither patch
/// payloads nor a BIN/CUE writer, and it is not visible outside Core except to
/// the single friend smoke assembly declared by Spyro.Editor.Core.csproj.
/// </summary>
internal sealed record NativeTextureDeadStorageCandidatePlan(
    string LevelKey,
    string LevelName,
    int LevelId,
    int WadEntry,
    string RetailDiscSha256,
    long RetailDiscByteLength,
    DateTime RetailDiscLastWriteTimeUtc,
    long TexturePagesWadOffset,
    string AddressableTexturePagesSha256,
    int KnownOwnedByteCount,
    int UnknownNonZeroByteCount,
    int UnknownNonZeroRegionCount,
    string UnknownNonZeroRangeLayoutSha256,
    string UnknownNonZeroPreimageSha256,
    int RequestedUnknownNonZeroByteCount,
    int SelectedUnknownNonZeroByteCount,
    int SelectedRangeCount,
    string SelectedRangeLayoutSha256,
    string SelectedPreimageSha256,
    string SelectedDeterministicPoisonSha256,
    string PoisonAlgorithm,
    IReadOnlyList<NativeTextureDeadStorageCandidateRange> SelectedRanges,
    bool MachineDecodedDescriptorRootsClosed,
    bool HumanAuditedOtherRuntimeClosureOnly,
    bool MachineProvenEveryRuntimeReadClosed,
    bool WritesRetailOrCandidateImage,
    bool NormalExporterEligible,
    IReadOnlyList<string> Notes);

internal sealed record NativeTextureDeadStorageCandidateProof(
    NativeTextureDeadStorageCandidatePlan Plan,
    string PromotionEvidenceSha256,
    bool ResearchOnly,
    bool NormalExporterEligible);

internal sealed record NativeTextureDeadStorageAuthorizationResult(
    NativeTextureDeadStorageCandidatePlan Plan,
    NativeTextureDeadStorageCandidateProof? Proof,
    IReadOnlyList<string> Blockers)
{
    public bool Authorized => Proof != null && Blockers.Count == 0;
}

/// <summary>
/// Research-only dead-unreferenced storage boundary. There is no write method,
/// no public type, and no App/Create BIN reference. Planning identifies exact
/// retail preimages; authorization remains impossible until all six evidence
/// classes are complete and hash-bound to this exact plan.
/// </summary>
internal static class NativeTextureDeadUnreferencedStorageResearch
{
    private const int WadLba = 37;
    private const string PoisonAlgorithm =
        "byte ^= ((absoluteTexturePageOffset & 1) == 0 ? 0xA5 : 0x5A); every selected byte changes";

    private static readonly IReadOnlyDictionary<NativeTextureDeadStoragePromotionCondition, string[]> RequiredArtifactKinds =
        new Dictionary<NativeTextureDeadStoragePromotionCondition, string[]>
        {
            [NativeTextureDeadStoragePromotionCondition.ExactSourceAndPreimageBinding] =
                ["retail-disc", "texture-pages", "unknown-range-layout", "selected-range-layout", "selected-preimage", "executable-overlay-hash-inventory"],
            [NativeTextureDeadStoragePromotionCondition.GpuProducerAndCopySourceEnumeration] =
                ["gpu-producer-inventory"],
            [NativeTextureDeadStoragePromotionCondition.ReachableSourceValueClosure] =
                ["source-value-closure"],
            [NativeTextureDeadStoragePromotionCondition.ArchiveProvenanceAndLifetimeClosure] =
                ["archive-lifetime-proof"],
            [NativeTextureDeadStoragePromotionCondition.RuntimeMutationAndTransitionClosure] =
                ["runtime-mutation-closure"],
            [NativeTextureDeadStoragePromotionCondition.ImmutableKnownOwnedReadbackTraceAndDuckStationProof] =
                ["exact-readback", "vram-trace", "duckstation-run"]
        };

    public static NativeTextureDeadStorageCandidatePlan BuildReadOnlyCandidatePlan(
        string sourceImagePath,
        LevelDefinition level,
        int requiredUnknownNonZeroByteCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceImagePath);
        ArgumentNullException.ThrowIfNull(level);
        if (requiredUnknownNonZeroByteCount <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(requiredUnknownNonZeroByteCount),
                "A positive UnknownNonZero deficit is required.");
        if (!File.Exists(sourceImagePath))
            throw new FileNotFoundException("Missing retail source disc image.", sourceImagePath);

        NativeTexturePageOwnershipReport ownership = NativeTexturePageOwnershipScanner.Scan(sourceImagePath, level);
        if (!ownership.AllConsumerClosureComplete)
        {
            throw new InvalidDataException(
                $"{level.DisplayName} decoded-consumer closure is incomplete: " +
                (ownership.SafetyBlockers.FirstOrDefault() ?? "unknown ownership blocker"));
        }
        if (ownership.UnknownNonZeroRanges.Count != ownership.UnknownNonZeroRegionCount ||
            ownership.UnknownNonZeroRanges.Sum(range => range.Length) != ownership.UnknownNonZeroByteCount)
        {
            throw new InvalidDataException("The exact UnknownNonZero range list does not reconcile with the ownership report.");
        }
        if (requiredUnknownNonZeroByteCount > ownership.UnknownNonZeroByteCount)
        {
            throw new InvalidDataException(
                $"The request needs {requiredUnknownNonZeroByteCount:N0} UnknownNonZero bytes, but " +
                $"{level.DisplayName} exposes only {ownership.UnknownNonZeroByteCount:N0} research candidates.");
        }

        DiscLayout layout = DiscImage.DetectLayout(sourceImagePath);
        string retailDiscSha256;
        long retailDiscByteLength;
        byte[] texturePages;
        using (FileStream stream = File.OpenRead(sourceImagePath))
        {
            retailDiscByteLength = stream.Length;
            retailDiscSha256 = Convert.ToHexString(SHA256.HashData(stream));
            texturePages = DiscImage.ReadFileBytes(
                stream,
                layout,
                WadLba,
                ownership.TexturePagesWadOffset,
                ownership.AddressableTexturePageByteLength);
        }

        string pagesSha256 = Sha256(texturePages);
        RequireHashEqual(
            ownership.AddressableTexturePagesSha256,
            pagesSha256,
            "addressable texture-pages preimage");
        string unknownLayoutSha256 = HashRangeLayout(ownership.UnknownNonZeroRanges);
        RequireHashEqual(
            ownership.UnknownNonZeroRangesSha256,
            unknownLayoutSha256,
            "UnknownNonZero range layout");

        ValidateUnknownRanges(texturePages, ownership.UnknownNonZeroRanges);
        byte[] unknownPreimage = ConcatenateRanges(texturePages, ownership.UnknownNonZeroRanges);

        List<NativeTextureUnknownRangeSample> selectedSourceRanges = [];
        int selectedByteCount = 0;
        foreach (NativeTextureUnknownRangeSample range in ownership.UnknownNonZeroRanges
                     .OrderByDescending(range => range.Length)
                     .ThenBy(range => range.Offset))
        {
            selectedSourceRanges.Add(range);
            selectedByteCount = checked(selectedByteCount + range.Length);
            if (selectedByteCount >= requiredUnknownNonZeroByteCount)
                break;
        }
        selectedSourceRanges.Sort((left, right) => left.Offset.CompareTo(right.Offset));

        List<NativeTextureDeadStorageCandidateRange> selectedRanges = [];
        using IncrementalHash selectedPreimageHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using IncrementalHash selectedPoisonHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (NativeTextureUnknownRangeSample range in selectedSourceRanges)
        {
            ReadOnlySpan<byte> preimage = texturePages.AsSpan(range.Offset, range.Length);
            byte[] poison = BuildPoison(preimage, range.Offset);
            selectedPreimageHash.AppendData(preimage);
            selectedPoisonHash.AppendData(poison);
            selectedRanges.Add(new NativeTextureDeadStorageCandidateRange(
                range.Offset,
                checked(ownership.TexturePagesWadOffset + range.Offset),
                range.Length,
                Sha256(preimage),
                Sha256(poison)));
        }

        return new NativeTextureDeadStorageCandidatePlan(
            LevelKey: level.Key,
            LevelName: level.DisplayName,
            LevelId: level.LevelId,
            WadEntry: level.SourceWadEntry,
            RetailDiscSha256: retailDiscSha256,
            RetailDiscByteLength: retailDiscByteLength,
            RetailDiscLastWriteTimeUtc: File.GetLastWriteTimeUtc(sourceImagePath),
            TexturePagesWadOffset: ownership.TexturePagesWadOffset,
            AddressableTexturePagesSha256: pagesSha256,
            KnownOwnedByteCount: ownership.KnownOwnedByteCount,
            UnknownNonZeroByteCount: ownership.UnknownNonZeroByteCount,
            UnknownNonZeroRegionCount: ownership.UnknownNonZeroRegionCount,
            UnknownNonZeroRangeLayoutSha256: unknownLayoutSha256,
            UnknownNonZeroPreimageSha256: Sha256(unknownPreimage),
            RequestedUnknownNonZeroByteCount: requiredUnknownNonZeroByteCount,
            SelectedUnknownNonZeroByteCount: selectedByteCount,
            SelectedRangeCount: selectedRanges.Count,
            SelectedRangeLayoutSha256: HashRangeLayout(selectedSourceRanges),
            SelectedPreimageSha256: Convert.ToHexString(selectedPreimageHash.GetHashAndReset()),
            SelectedDeterministicPoisonSha256: Convert.ToHexString(selectedPoisonHash.GetHashAndReset()),
            PoisonAlgorithm: PoisonAlgorithm,
            SelectedRanges: selectedRanges,
            MachineDecodedDescriptorRootsClosed: true,
            HumanAuditedOtherRuntimeClosureOnly: true,
            MachineProvenEveryRuntimeReadClosed: false,
            WritesRetailOrCandidateImage: false,
            NormalExporterEligible: false,
            Notes:
            [
                "UnknownNonZero still means protected unclassified nonzero storage; this plan does not call it dead.",
                "Decoded descriptor roots are machine-enumerated, but the other-runtime scope still contains a source-audit assertion; this is not machine-proven closure over every runtime read.",
                "Whole native unknown runs are selected largest-first, then reported in physical offset order; the selected total may exceed the requested deficit.",
                "Only offsets, lengths, and hashes are returned. No before/after payload and no image writer exist in this API.",
                "Authorization requires all six producer, provenance, lifetime, transition, readback, trace, and DuckStation evidence gates."
            ]);
    }

    public static NativeTextureDeadStorageAuthorizationResult TryAuthorizeCandidateDeadStorage(
        NativeTextureDeadStorageCandidatePlan plan,
        NativeTextureDeadStoragePromotionAttestation attestation)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(attestation);
        List<string> blockers = [];

        CompareAttestedHash(attestation.ExpectedRetailDiscSha256, plan.RetailDiscSha256, "retail disc", blockers);
        CompareAttestedHash(attestation.ExpectedAddressableTexturePagesSha256, plan.AddressableTexturePagesSha256, "texture pages", blockers);
        CompareAttestedHash(attestation.ExpectedUnknownRangeLayoutSha256, plan.UnknownNonZeroRangeLayoutSha256, "UnknownNonZero range layout", blockers);
        CompareAttestedHash(attestation.ExpectedSelectedRangeLayoutSha256, plan.SelectedRangeLayoutSha256, "selected range layout", blockers);
        CompareAttestedHash(attestation.ExpectedSelectedPreimageSha256, plan.SelectedPreimageSha256, "selected range preimage", blockers);

        NativeTextureDeadStoragePromotionCondition[] requiredConditions =
            Enum.GetValues<NativeTextureDeadStoragePromotionCondition>();
        foreach (IGrouping<NativeTextureDeadStoragePromotionCondition, NativeTextureDeadStorageConditionEvidence> duplicate in
                 attestation.Conditions.GroupBy(item => item.Condition).Where(group => group.Count() != 1))
        {
            blockers.Add($"Promotion condition {duplicate.Key} must appear exactly once; found {duplicate.Count()} entries.");
        }
        foreach (NativeTextureDeadStoragePromotionCondition condition in requiredConditions)
        {
            NativeTextureDeadStorageConditionEvidence[] matchingEvidence = attestation.Conditions
                .Where(item => item.Condition == condition)
                .ToArray();
            if (matchingEvidence.Length == 0)
            {
                blockers.Add($"Promotion condition {condition} is missing.");
                continue;
            }
            if (matchingEvidence.Length != 1)
                continue;
            NativeTextureDeadStorageConditionEvidence evidence = matchingEvidence[0];
            if (!evidence.Complete)
                blockers.Add($"Promotion condition {condition} is not complete.");
            if (string.IsNullOrWhiteSpace(evidence.Summary))
                blockers.Add($"Promotion condition {condition} has no evidence summary.");

            foreach (string requiredKind in RequiredArtifactKinds[condition])
            {
                NativeTextureDeadStorageEvidenceArtifact[] matchingArtifacts = evidence.Artifacts
                    .Where(item => string.Equals(item.Kind, requiredKind, StringComparison.Ordinal))
                    .ToArray();
                if (matchingArtifacts.Length == 0)
                {
                    blockers.Add($"Promotion condition {condition} is missing hashed artifact '{requiredKind}'.");
                    continue;
                }
                if (matchingArtifacts.Length != 1)
                {
                    blockers.Add($"Promotion condition {condition} must contain exactly one hashed artifact '{requiredKind}'.");
                    continue;
                }
                NativeTextureDeadStorageEvidenceArtifact artifact = matchingArtifacts[0];
                if (string.IsNullOrWhiteSpace(artifact.Identity) || !IsSha256(artifact.Sha256))
                    blockers.Add($"Promotion condition {condition} artifact '{requiredKind}' is not identity/hash complete.");
            }
        }

        NativeTextureDeadStorageConditionEvidence[] sourceBindings = attestation.Conditions
            .Where(item => item.Condition == NativeTextureDeadStoragePromotionCondition.ExactSourceAndPreimageBinding)
            .ToArray();
        if (sourceBindings.Length == 1)
        {
            NativeTextureDeadStorageConditionEvidence sourceBinding = sourceBindings[0];
            CompareArtifactHash(sourceBinding, "retail-disc", plan.RetailDiscSha256, blockers);
            CompareArtifactHash(sourceBinding, "texture-pages", plan.AddressableTexturePagesSha256, blockers);
            CompareArtifactHash(sourceBinding, "unknown-range-layout", plan.UnknownNonZeroRangeLayoutSha256, blockers);
            CompareArtifactHash(sourceBinding, "selected-range-layout", plan.SelectedRangeLayoutSha256, blockers);
            CompareArtifactHash(sourceBinding, "selected-preimage", plan.SelectedPreimageSha256, blockers);
        }

        blockers = blockers.Distinct(StringComparer.Ordinal).ToList();
        if (blockers.Count != 0)
            return new NativeTextureDeadStorageAuthorizationResult(plan, null, blockers);

        string promotionEvidenceSha256 = HashPromotionEvidence(attestation);
        NativeTextureDeadStorageCandidateProof proof = new(
            plan,
            promotionEvidenceSha256,
            ResearchOnly: true,
            NormalExporterEligible: false);
        return new NativeTextureDeadStorageAuthorizationResult(plan, proof, Array.Empty<string>());
    }

    private static byte[] BuildPoison(ReadOnlySpan<byte> preimage, int absoluteOffset)
    {
        byte[] poison = new byte[preimage.Length];
        for (int index = 0; index < poison.Length; index++)
        {
            byte mask = ((absoluteOffset + index) & 1) == 0 ? (byte)0xA5 : (byte)0x5A;
            poison[index] = (byte)(preimage[index] ^ mask);
            if (poison[index] == preimage[index])
                throw new InvalidDataException("The deterministic poison failed to change a selected byte.");
        }
        return poison;
    }

    private static byte[] ConcatenateRanges(
        byte[] texturePages,
        IReadOnlyList<NativeTextureUnknownRangeSample> ranges)
    {
        byte[] result = new byte[ranges.Sum(range => range.Length)];
        int destination = 0;
        foreach (NativeTextureUnknownRangeSample range in ranges.OrderBy(range => range.Offset))
        {
            texturePages.AsSpan(range.Offset, range.Length).CopyTo(result.AsSpan(destination));
            destination += range.Length;
        }
        return result;
    }

    private static void ValidateUnknownRanges(
        byte[] texturePages,
        IReadOnlyList<NativeTextureUnknownRangeSample> ranges)
    {
        int previousEnd = 0;
        foreach (NativeTextureUnknownRangeSample range in ranges.OrderBy(range => range.Offset))
        {
            if (range.Offset < previousEnd || range.Length <= 0 ||
                (long)range.Offset + range.Length > texturePages.Length)
            {
                throw new InvalidDataException("UnknownNonZero ranges overlap or exceed the addressable texture pages.");
            }
            if (texturePages.AsSpan(range.Offset, range.Length).Contains((byte)0))
                throw new InvalidDataException("An UnknownNonZero range contains a zero byte.");
            previousEnd = checked(range.Offset + range.Length);
        }
    }

    private static string HashRangeLayout(IReadOnlyList<NativeTextureUnknownRangeSample> ranges)
    {
        byte[] layout = new byte[ranges.Count * 8];
        for (int index = 0; index < ranges.Count; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(layout.AsSpan(index * 8, 4), ranges[index].Offset);
            BinaryPrimitives.WriteInt32LittleEndian(layout.AsSpan(index * 8 + 4, 4), ranges[index].Length);
        }
        return Sha256(layout);
    }

    private static string HashPromotionEvidence(NativeTextureDeadStoragePromotionAttestation attestation)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendUtf8(hash, attestation.ExpectedRetailDiscSha256);
        AppendUtf8(hash, attestation.ExpectedAddressableTexturePagesSha256);
        AppendUtf8(hash, attestation.ExpectedUnknownRangeLayoutSha256);
        AppendUtf8(hash, attestation.ExpectedSelectedRangeLayoutSha256);
        AppendUtf8(hash, attestation.ExpectedSelectedPreimageSha256);
        foreach (NativeTextureDeadStorageConditionEvidence evidence in attestation.Conditions.OrderBy(item => item.Condition))
        {
            AppendUtf8(hash, evidence.Condition.ToString());
            AppendUtf8(hash, evidence.Complete.ToString());
            AppendUtf8(hash, evidence.Summary);
            foreach (NativeTextureDeadStorageEvidenceArtifact artifact in evidence.Artifacts
                         .OrderBy(item => item.Kind, StringComparer.Ordinal)
                         .ThenBy(item => item.Identity, StringComparer.Ordinal))
            {
                AppendUtf8(hash, artifact.Kind);
                AppendUtf8(hash, artifact.Identity);
                AppendUtf8(hash, artifact.Sha256);
            }
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static void AppendUtf8(IncrementalHash hash, string value) =>
        hash.AppendData(System.Text.Encoding.UTF8.GetBytes(value + "\n"));

    private static void CompareAttestedHash(string supplied, string expected, string label, List<string> blockers)
    {
        if (!IsSha256(supplied) || !string.Equals(supplied, expected, StringComparison.OrdinalIgnoreCase))
            blockers.Add($"The attested {label} hash is missing or stale.");
    }

    private static void CompareArtifactHash(
        NativeTextureDeadStorageConditionEvidence evidence,
        string kind,
        string expected,
        List<string> blockers)
    {
        NativeTextureDeadStorageEvidenceArtifact? artifact = evidence.Artifacts
            .FirstOrDefault(item => string.Equals(item.Kind, kind, StringComparison.Ordinal));
        if (artifact != null && !string.Equals(artifact.Sha256, expected, StringComparison.OrdinalIgnoreCase))
            blockers.Add($"The source-binding artifact '{kind}' does not match the planned preimage.");
    }

    private static bool IsSha256(string value) =>
        value.Length == 64 && value.All(character => Uri.IsHexDigit(character));

    private static void RequireHashEqual(string expected, string actual, string label)
    {
        if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"The {label} hash changed during the read-only research scan.");
    }

    private static string Sha256(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes));
}
