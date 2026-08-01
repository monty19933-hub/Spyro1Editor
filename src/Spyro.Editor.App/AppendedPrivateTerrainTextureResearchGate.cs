using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.App;

/// <summary>
/// Internal promotion gate for appended-private native terrain texture rows.
/// Normal release admits only exact registry profiles with DuckStation evidence
/// and their proven row capacity. The explicit research switch retains the
/// broader static-audit route; even there, the three destinations that do not
/// pass the strict global-packer audit stay blocked.
/// </summary>
internal static class AppendedPrivateTerrainTextureResearchGate
{
    internal const string EnvironmentVariable =
        "SPYRO_EDITOR_ENABLE_APPENDED_PRIVATE_TEXTURES";
    internal const int ResearchMaximumAppendedRecords =
        NativeTerrainTextureRecordAppendBuilder
            .MaximumStaticResearchAppendedRecordCount;

    private static readonly HashSet<string> StrictAuditBlockedLevels =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "loftycastle",
            "jacques",
            "gnorccove"
        };

    // Literal membership from the 2026-07-21 all-level synthetic global-
    // repacker report. Do not infer support as "anything except three": an
    // unknown/future catalog key must earn its own exact packing proof first.
    private static readonly HashSet<string> StrictAuditAllowedLevels =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "alpineridge",
            "artisans",
            "beastmakers",
            "blowhard",
            "clifftown",
            "crystalflight",
            "darkhollow",
            "darkpassage",
            "doctorshemp",
            "dreamweavers",
            "drycanyon",
            "gnastygnorc",
            "gnastysloot",
            "gnastysworld",
            "hauntedtowers",
            "highcaves",
            "icecavern",
            "icyflight",
            "magiccrafters",
            "metalhead",
            "mistybog",
            "nightflight",
            "peacekeepers",
            "stonehill",
            "sunnyflight",
            "terracevillage",
            "toasty",
            "townsquare",
            "treetops",
            "twilightharbor",
            "wildflight",
            "wizardpeak"
        };

    internal static bool IsEnabled
    {
        get
        {
            string value = Environment.GetEnvironmentVariable(EnvironmentVariable)?.Trim() ?? "";
            return value is "1" or "true" or "TRUE" or "yes" or "YES";
        }
    }

    internal static NativeTerrainTextureStructuralGrowthPolicy
        ResolveStructuralGrowthPolicy(
            NativeTerrainTextureRecordAppendSourceBinding sourceBinding,
            int appendedRecordCount)
    {
        ArgumentNullException.ThrowIfNull(sourceBinding);
        return IsEnabled
            ? NativeTerrainTextureStructuralGrowthPolicy.FiftyRowStaticResearch
            : AppendedPrivateTerrainTexturePromotionProfileRegistry
                .ResolveNormalReleaseStructuralGrowthPolicy(
                    sourceBinding,
                    appendedRecordCount);
    }

    internal static NativeTerrainTextureStructuralGrowthPolicy
        ResolveAggregateStructuralGrowthPolicy(
            IEnumerable<NativeTerrainTextureStructuralGrowthPolicy> levelPolicies)
    {
        ArgumentNullException.ThrowIfNull(levelPolicies);
        NativeTerrainTextureStructuralGrowthPolicy[] policies =
            levelPolicies.Distinct().ToArray();
        if (policies.Length == 0)
        {
            throw new InvalidOperationException(
                "A private-texture aggregate requires at least one destination policy.");
        }

        if (IsEnabled)
        {
            if (policies.Any(policy =>
                    policy !=
                    NativeTerrainTextureStructuralGrowthPolicy
                        .FiftyRowStaticResearch))
            {
                throw new InvalidDataException(
                    "The static-research aggregate received a normal-release private-texture plan.");
            }
            return NativeTerrainTextureStructuralGrowthPolicy
                .FiftyRowStaticResearch;
        }

        if (policies.Any(NativeTerrainTextureRecordAppendBuilder
                .IsStaticResearchOnly))
        {
            throw new InvalidDataException(
                "A static-research private-texture plan cannot enter normal multi-level Create BIN.");
        }
        if (policies.Any(policy =>
                policy is not
                    NativeTerrainTextureStructuralGrowthPolicy.NormalChecked and
                    not NativeTerrainTextureStructuralGrowthPolicy
                        .RuntimeProvenPlus2000 and
                    not NativeTerrainTextureStructuralGrowthPolicy
                        .RuntimeProvenExtended))
        {
            throw new InvalidDataException(
                "The private-texture aggregate contains an unknown structural-growth policy.");
        }

        if (policies.Contains(
                NativeTerrainTextureStructuralGrowthPolicy.RuntimeProvenExtended))
        {
            return NativeTerrainTextureStructuralGrowthPolicy
                .RuntimeProvenExtended;
        }
        return policies.Contains(
                NativeTerrainTextureStructuralGrowthPolicy
                    .RuntimeProvenPlus2000)
            ? NativeTerrainTextureStructuralGrowthPolicy
                .RuntimeProvenPlus2000
            : NativeTerrainTextureStructuralGrowthPolicy.NormalChecked;
    }

    internal static bool TryAllow(string levelKey, out string reason)
    {
        string normalized = LevelCatalog.NormalizeKey(levelKey);
        if (!IsEnabled)
        {
            reason =
                $"Selected-section allocation has no exact source/writer proof request and remains guarded. " +
                "Normal release requires a RuntimeProven profile matching the retail disc, level-data preimage, and structural writer. " +
                $"Set {EnvironmentVariable}=1 only for the isolated static-research launcher; its outputs remain runtime-unverified.";
            return false;
        }

        return TryAllowResearch(normalized, out reason);
    }

    /// <summary>
    /// Exact promotion path used once the caller has inspected the retail
    /// source and selected the sole structural writer. Normal release can pass
    /// only through a RuntimeProven registry profile. The explicit environment
    /// switch retains the wider static-audit research behavior.
    /// </summary>
    internal static bool TryAllow(
        NativeTerrainTextureRecordAppendSourceBinding sourceBinding,
        NativeTerrainTexturePrivateImageWriterKind writerKind,
        int appendedRecordCount,
        NativeTerrainTextureStructuralGrowthPolicy structuralGrowthPolicy,
        out string reason)
    {
        ArgumentNullException.ThrowIfNull(sourceBinding);
        bool normalAuthorized = TryAuthorizeNormalCreateBin(
            sourceBinding,
            writerKind,
            appendedRecordCount,
            structuralGrowthPolicy,
            out string promotionReason);
        bool staticResearchOnly =
            NativeTerrainTextureRecordAppendBuilder.IsStaticResearchOnly(
                structuralGrowthPolicy);
        if (normalAuthorized)
        {
            reason = promotionReason;
            return true;
        }

        if (!IsEnabled)
        {
            reason = promotionReason;
            return false;
        }

        if (!TryAllowResearch(
                LevelCatalog.NormalizeKey(sourceBinding.TargetLevelKey),
                out string researchReason))
        {
            reason = researchReason;
            return false;
        }
        if (!TryAllowResearchRecordCount(
                appendedRecordCount,
                out string researchCountReason))
        {
            reason = researchCountReason;
            return false;
        }

        reason =
            $"{researchReason} {researchCountReason} " +
            (staticResearchOnly
                ? $"Structural growth policy {structuralGrowthPolicy} is static-research-only. "
                : "") +
            $"Normal promotion remains unavailable for this exact request: {promotionReason}";
        return true;
    }

    /// <summary>
    /// Release-only authorization used by Build Safety and normal Create BIN.
    /// The explicit research environment switch is intentionally ignored:
    /// extended static-growth policies can produce disposable proof artifacts,
    /// but can never become normal release authorization.
    /// </summary>
    internal static bool TryAuthorizeNormalCreateBin(
        NativeTerrainTextureRecordAppendSourceBinding sourceBinding,
        NativeTerrainTexturePrivateImageWriterKind writerKind,
        int appendedRecordCount,
        NativeTerrainTextureStructuralGrowthPolicy structuralGrowthPolicy,
        out string reason)
    {
        ArgumentNullException.ThrowIfNull(sourceBinding);
        if (NativeTerrainTextureRecordAppendBuilder.IsStaticResearchOnly(
                structuralGrowthPolicy))
        {
            reason =
                $"Structural growth policy {structuralGrowthPolicy} is static-research-only and cannot authorize normal Create BIN.";
            return false;
        }

        return AppendedPrivateTerrainTexturePromotionProfileRegistry
            .TryAuthorizeNormalRelease(
                sourceBinding,
                writerKind,
                appendedRecordCount,
                structuralGrowthPolicy,
                out _,
                out reason);
    }

    /// <summary>
    /// Shared count policy for the editor allocator and its deterministic smoke.
    /// Reusing an existing row preserves the current count, so a project already
    /// at the research maximum can continue assigning that row to more faces.
    /// </summary>
    internal static bool TryPlanResearchEditorAllocation(
        int currentlyStagedAppendedRecords,
        bool reuseExistingRecord,
        out int resultingAppendedRecordCount,
        out string reason)
    {
        if (currentlyStagedAppendedRecords < 0)
        {
            resultingAppendedRecordCount = currentlyStagedAppendedRecords;
            reason = "The staged private-texture row count cannot be negative.";
            return false;
        }

        try
        {
            resultingAppendedRecordCount = reuseExistingRecord
                ? currentlyStagedAppendedRecords
                : checked(currentlyStagedAppendedRecords + 1);
        }
        catch (OverflowException)
        {
            resultingAppendedRecordCount = int.MaxValue;
            reason =
                "The staged private-texture row count overflowed while planning another allocation.";
            return false;
        }

        if (!TryAllowResearchRecordCount(resultingAppendedRecordCount, out reason))
            return false;

        reason = reuseExistingRecord
            ? $"Reusing an existing private texture keeps this destination at " +
              $"{resultingAppendedRecordCount}/{ResearchMaximumAppendedRecords} research rows; no new row is allocated."
            : $"Research allocation admits private texture row " +
              $"{resultingAppendedRecordCount}/{ResearchMaximumAppendedRecords}.";
        return true;
    }

    private static bool TryAllowResearchRecordCount(
        int appendedRecordCount,
        out string reason)
    {
        if (appendedRecordCount <= 0)
        {
            reason =
                "Research allocation requires at least one appended-private texture record.";
            return false;
        }
        if (appendedRecordCount > ResearchMaximumAppendedRecords)
        {
            reason =
                $"Research mode currently allows at most {ResearchMaximumAppendedRecords} " +
                $"appended-private texture records per destination level. This request would " +
                $"require {appendedRecordCount}. Reuse an already staged donor/material row " +
                "or undo one private texture before adding another.";
            return false;
        }

        reason =
            $"Research count policy admits {appendedRecordCount}/" +
            $"{ResearchMaximumAppendedRecords} appended-private texture records.";
        return true;
    }

    private static bool TryAllowResearch(string normalized, out string reason)
    {
        if (StrictAuditBlockedLevels.Contains(normalized))
        {
            reason = normalized switch
            {
                "loftycastle" =>
                    "Lofty Castle is not in the strict 32-level private-texture set because its native texture pages still fail the exact alias-preserving packing proof.",
                "jacques" =>
                    "Jacques is not in the strict 32-level private-texture set because its native texture pages still fail the exact alias-preserving packing proof.",
                "gnorccove" =>
                    "Gnorc Cove is not in the strict 32-level private-texture set because its native texture pages still fail the exact alias-preserving packing proof.",
                _ => "This destination is outside the strict private-texture audit set."
            };
            return false;
        }

        if (!StrictAuditAllowedLevels.Contains(normalized))
        {
            reason = string.IsNullOrWhiteSpace(normalized)
                ? "This destination has no normalized level identity and is outside the strict 32-level private-texture audit set."
                : $"'{normalized}' is not one of the exact 32 destination keys in the private-texture audit set; unknown or future levels remain blocked until they pass the same proof.";
            return false;
        }

        reason =
            $"This destination is in the strict 32-level global-packer audit set; " +
            $"the selected face may use an appended private native texture row, subject to the " +
            $"{ResearchMaximumAppendedRecords}-row research maximum and the exact packer.";
        return true;
    }

    internal static IReadOnlyList<string> StrictBlockedLevelKeys =>
        StrictAuditBlockedLevels.OrderBy(key => key, StringComparer.Ordinal).ToArray();

    internal static IReadOnlyList<string> StrictAllowedLevelKeys =>
        StrictAuditAllowedLevels.OrderBy(key => key, StringComparer.Ordinal).ToArray();

    /// <summary>
    /// Focused non-UI regression hook for the four-record Artisans promotion.
    /// It verifies normal exact-profile admission, capacity, fingerprint, and
    /// writer rejection while preserving the broader research-only allow set.
    /// </summary>
    internal static string RunExactPromotionProfileSmokeForTesting()
    {
        string? previous = Environment.GetEnvironmentVariable(EnvironmentVariable);
        try
        {
            AppendedPrivateTerrainTexturePromotionProfile artisans =
                AppendedPrivateTerrainTexturePromotionProfileRegistry.Profiles.Single(profile =>
                    profile.NormalizedTargetLevelKey == "artisans" &&
                    profile.WriterKind == NativeTerrainTexturePrivateImageWriterKind.FixedTail);
            NativeTerrainTextureRecordAppendSourceBinding exactBinding = new(
                NativeTerrainTextureRecordAppendBuilder.CurrentBindingVersion,
                AppendedPrivateTerrainTexturePromotionProfileRegistry.CleanUsaImageSha256
                    .ToUpperInvariant(),
                "Artisans",
                artisans.Retail.TargetWadEntry,
                artisans.Retail.ExpectedSourceTextureCount,
                artisans.Retail.ExpectedTextureComponentSha256.ToUpperInvariant(),
                artisans.Retail.ExpectedLevelDataSha256.ToUpperInvariant());
            AppendedPrivateTerrainTexturePromotionProfile gnastysWorld =
                AppendedPrivateTerrainTexturePromotionProfileRegistry.Profiles.Single(profile =>
                    profile.NormalizedTargetLevelKey == "gnastysworld" &&
                    profile.WriterKind ==
                        NativeTerrainTexturePrivateImageWriterKind.SectorRelocation);
            NativeTerrainTextureRecordAppendSourceBinding exactGnastyBinding = new(
                NativeTerrainTextureRecordAppendBuilder.CurrentBindingVersion,
                AppendedPrivateTerrainTexturePromotionProfileRegistry.CleanUsaImageSha256
                    .ToUpperInvariant(),
                "gnastysworld",
                gnastysWorld.Retail.TargetWadEntry,
                gnastysWorld.Retail.ExpectedSourceTextureCount,
                gnastysWorld.Retail.ExpectedTextureComponentSha256.ToUpperInvariant(),
                gnastysWorld.Retail.ExpectedLevelDataSha256.ToUpperInvariant());
            AppendedPrivateTerrainTexturePromotionProfile wizardPeak =
                AppendedPrivateTerrainTexturePromotionProfileRegistry.Profiles.Single(profile =>
                    profile.NormalizedTargetLevelKey == "wizardpeak" &&
                    profile.WriterKind ==
                        NativeTerrainTexturePrivateImageWriterKind.SectorRelocation);
            NativeTerrainTextureRecordAppendSourceBinding exactWizardPeakBinding = new(
                NativeTerrainTextureRecordAppendBuilder.CurrentBindingVersion,
                AppendedPrivateTerrainTexturePromotionProfileRegistry.CleanUsaImageSha256
                    .ToUpperInvariant(),
                "Wizard Peak",
                wizardPeak.Retail.TargetWadEntry,
                wizardPeak.Retail.ExpectedSourceTextureCount,
                wizardPeak.Retail.ExpectedTextureComponentSha256.ToUpperInvariant(),
                wizardPeak.Retail.ExpectedLevelDataSha256.ToUpperInvariant());

            if (!artisans.RuntimeProven ||
                artisans.RuntimeProvenMaxAppendedRecords != 4 ||
                !AppendedPrivateTerrainTexturePromotionProfileRegistry.TryFindExact(
                    exactBinding,
                    NativeTerrainTexturePrivateImageWriterKind.FixedTail,
                    out AppendedPrivateTerrainTexturePromotionProfile? found) ||
                !ReferenceEquals(found, artisans))
            {
                throw new InvalidOperationException(
                    "The Artisans fixed-tail profile is not registered as the exact runtime-proven candidate.");
            }

            Environment.SetEnvironmentVariable(EnvironmentVariable, null);
            NativeTerrainTextureStructuralGrowthPolicy artisansResolvedPolicy =
                ResolveStructuralGrowthPolicy(exactBinding, 4);
            NativeTerrainTextureStructuralGrowthPolicy gnastyResolvedPolicy =
                ResolveStructuralGrowthPolicy(exactGnastyBinding, 50);
            NativeTerrainTextureStructuralGrowthPolicy gnastyOverLimitResolvedPolicy =
                ResolveStructuralGrowthPolicy(exactGnastyBinding, 51);
            NativeTerrainTextureStructuralGrowthPolicy wizardPeakResolvedPolicy =
                ResolveStructuralGrowthPolicy(exactWizardPeakBinding, 46);
            NativeTerrainTextureStructuralGrowthPolicy wizardPeakOverLimitResolvedPolicy =
                ResolveStructuralGrowthPolicy(exactWizardPeakBinding, 47);
            if (artisansResolvedPolicy !=
                    NativeTerrainTextureStructuralGrowthPolicy.NormalChecked ||
                gnastyResolvedPolicy !=
                    NativeTerrainTextureStructuralGrowthPolicy
                        .RuntimeProvenExtended ||
                gnastyOverLimitResolvedPolicy !=
                    NativeTerrainTextureStructuralGrowthPolicy.NormalChecked ||
                wizardPeakResolvedPolicy !=
                    NativeTerrainTextureStructuralGrowthPolicy
                        .RuntimeProvenPlus2000 ||
                wizardPeakOverLimitResolvedPolicy !=
                    NativeTerrainTextureStructuralGrowthPolicy.NormalChecked)
            {
                throw new InvalidOperationException(
                    $"Normal app policy resolution did not retain Artisans NormalChecked ({artisansResolvedPolicy}), select Gnasty's World RuntimeProvenExtended through row 50 ({gnastyResolvedPolicy}), select Wizard Peak RuntimeProvenPlus2000 through appended row 46 ({wizardPeakResolvedPolicy}), or fail closed beyond the exact capacities ({gnastyOverLimitResolvedPolicy}, {wizardPeakOverLimitResolvedPolicy}).");
            }
            for (int requestedCount = 1;
                 requestedCount <= artisans.RuntimeProvenMaxAppendedRecords;
                 requestedCount++)
            {
                if (!TryAllow(
                        exactBinding,
                        NativeTerrainTexturePrivateImageWriterKind.FixedTail,
                        appendedRecordCount: requestedCount,
                        structuralGrowthPolicy:
                            NativeTerrainTextureStructuralGrowthPolicy.NormalChecked,
                        out string normalReason) ||
                    !normalReason.Contains("Runtime-proven exact", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Normal release did not admit the exact {requestedCount}-record Artisans fixed-tail profile: {normalReason}");
                }
            }

            if (TryAllow(
                    exactBinding,
                    NativeTerrainTexturePrivateImageWriterKind.FixedTail,
                    appendedRecordCount: 5,
                    structuralGrowthPolicy:
                        NativeTerrainTextureStructuralGrowthPolicy.NormalChecked,
                    out string fiveRecordReason) ||
                !fiveRecordReason.Contains("at most 4", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Normal release admitted five appended Artisans records from a four-record runtime proof.");
            }

            string gnastyReason = "";
            if (!gnastysWorld.RuntimeProven ||
                gnastysWorld.RuntimeProvenMaxAppendedRecords != 50 ||
                !TryAuthorizeNormalCreateBin(
                    exactGnastyBinding,
                    NativeTerrainTexturePrivateImageWriterKind.SectorRelocation,
                    appendedRecordCount: 50,
                    structuralGrowthPolicy:
                        NativeTerrainTextureStructuralGrowthPolicy
                            .RuntimeProvenExtended,
                    out gnastyReason) ||
                !gnastyReason.Contains(
                    "50/50 proven",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Normal release did not admit the exact fifty-row Gnasty's World sector profile: {gnastyReason}");
            }
            if (TryAuthorizeNormalCreateBin(
                    exactGnastyBinding,
                    NativeTerrainTexturePrivateImageWriterKind.SectorRelocation,
                    appendedRecordCount: 51,
                    structuralGrowthPolicy:
                        NativeTerrainTextureStructuralGrowthPolicy
                            .RuntimeProvenExtended,
                    out string gnastyOverLimitReason) ||
                !gnastyOverLimitReason.Contains(
                    "at most 50",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Normal release admitted row 51 for the fifty-row Gnasty's World runtime proof.");
            }
            if (TryAuthorizeNormalCreateBin(
                    exactGnastyBinding,
                    NativeTerrainTexturePrivateImageWriterKind.SectorRelocation,
                    appendedRecordCount: 50,
                    structuralGrowthPolicy:
                        NativeTerrainTextureStructuralGrowthPolicy.NormalChecked,
                    out string gnastyWrongPolicyReason) ||
                !gnastyWrongPolicyReason.Contains(
                    "No exact",
                    StringComparison.OrdinalIgnoreCase) ||
                TryAuthorizeNormalCreateBin(
                    exactGnastyBinding,
                    NativeTerrainTexturePrivateImageWriterKind.SectorRelocation,
                    appendedRecordCount: 50,
                    structuralGrowthPolicy:
                        NativeTerrainTextureStructuralGrowthPolicy
                            .FiftyRowStaticResearch,
                    out string gnastyStaticPolicyReason) ||
                !gnastyStaticPolicyReason.Contains(
                    "static-research-only",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Gnasty's World escaped its exact RuntimeProvenExtended policy.");
            }
            NativeTerrainTextureRecordAppendSourceBinding wrongGnastyDisc =
                exactGnastyBinding with { SourceImageSha256 = new string('0', 64) };
            if (TryAuthorizeNormalCreateBin(
                    wrongGnastyDisc,
                    NativeTerrainTexturePrivateImageWriterKind.SectorRelocation,
                    appendedRecordCount: 50,
                    structuralGrowthPolicy:
                        NativeTerrainTextureStructuralGrowthPolicy
                            .RuntimeProvenExtended,
                    out string wrongGnastyDiscReason) ||
                !wrongGnastyDiscReason.Contains(
                    "No exact",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Normal release admitted the Gnasty's World extended writer on a mismatched disc.");
            }
            string wizardPeakReason = "";
            if (!wizardPeak.RuntimeProven ||
                wizardPeak.RuntimeProvenMaxAppendedRecords != 46 ||
                wizardPeak.StructuralGrowthPolicy !=
                    NativeTerrainTextureStructuralGrowthPolicy
                        .RuntimeProvenPlus2000 ||
                !TryAuthorizeNormalCreateBin(
                    exactWizardPeakBinding,
                    NativeTerrainTexturePrivateImageWriterKind.SectorRelocation,
                    appendedRecordCount: 46,
                    structuralGrowthPolicy:
                        NativeTerrainTextureStructuralGrowthPolicy
                            .RuntimeProvenPlus2000,
                    out wizardPeakReason) ||
                !wizardPeakReason.Contains(
                    "46/46 proven",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Normal release did not admit the exact 46-appended-row Wizard Peak sector profile: {wizardPeakReason}");
            }
            if (TryAuthorizeNormalCreateBin(
                    exactWizardPeakBinding,
                    NativeTerrainTexturePrivateImageWriterKind.SectorRelocation,
                    appendedRecordCount: 47,
                    structuralGrowthPolicy:
                        NativeTerrainTextureStructuralGrowthPolicy
                            .RuntimeProvenPlus2000,
                    out string wizardPeakOverLimitReason) ||
                !wizardPeakOverLimitReason.Contains(
                    "at most 46",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Normal release admitted appended row 47 for the 46-row Wizard Peak runtime proof.");
            }
            if (TryAuthorizeNormalCreateBin(
                    exactWizardPeakBinding,
                    NativeTerrainTexturePrivateImageWriterKind.SectorRelocation,
                    appendedRecordCount: 46,
                    structuralGrowthPolicy:
                        NativeTerrainTextureStructuralGrowthPolicy
                            .RuntimeProvenExtended,
                    out string wizardPeakWrongPolicyReason) ||
                !wizardPeakWrongPolicyReason.Contains(
                    "No exact",
                    StringComparison.OrdinalIgnoreCase) ||
                TryAuthorizeNormalCreateBin(
                    exactWizardPeakBinding,
                    NativeTerrainTexturePrivateImageWriterKind.FixedTail,
                    appendedRecordCount: 46,
                    structuralGrowthPolicy:
                        NativeTerrainTextureStructuralGrowthPolicy
                            .RuntimeProvenPlus2000,
                    out string wizardPeakWrongWriterReason) ||
                !wizardPeakWrongWriterReason.Contains(
                    "No exact",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Wizard Peak escaped its exact RuntimeProvenPlus2000 policy or SectorRelocation writer.");
            }
            NativeTerrainTextureRecordAppendSourceBinding wrongWizardPeakDisc =
                exactWizardPeakBinding with
                {
                    SourceImageSha256 = new string('0', 64)
                };
            if (TryAuthorizeNormalCreateBin(
                    wrongWizardPeakDisc,
                    NativeTerrainTexturePrivateImageWriterKind.SectorRelocation,
                    appendedRecordCount: 46,
                    structuralGrowthPolicy:
                        NativeTerrainTextureStructuralGrowthPolicy
                            .RuntimeProvenPlus2000,
                    out string wrongWizardPeakDiscReason) ||
                !wrongWizardPeakDiscReason.Contains(
                    "No exact",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Normal release admitted the Wizard Peak +0x2000 writer on a mismatched disc.");
            }
            if (ResolveAggregateStructuralGrowthPolicy(
                    [
                        NativeTerrainTextureStructuralGrowthPolicy.NormalChecked,
                        NativeTerrainTextureStructuralGrowthPolicy
                            .RuntimeProvenPlus2000
                    ]) !=
                NativeTerrainTextureStructuralGrowthPolicy
                    .RuntimeProvenPlus2000)
            {
                throw new InvalidOperationException(
                    "A normal Artisans plus exact Wizard Peak aggregate did not select RuntimeProvenPlus2000.");
            }
            if (ResolveAggregateStructuralGrowthPolicy(
                    [
                        NativeTerrainTextureStructuralGrowthPolicy.NormalChecked,
                        NativeTerrainTextureStructuralGrowthPolicy
                            .RuntimeProvenPlus2000,
                        NativeTerrainTextureStructuralGrowthPolicy
                            .RuntimeProvenExtended
                    ]) !=
                NativeTerrainTextureStructuralGrowthPolicy.RuntimeProvenExtended)
            {
                throw new InvalidOperationException(
                    "A normal Artisans plus exact Gnasty's World aggregate did not select RuntimeProvenExtended.");
            }

            NativeTerrainTextureRecordAppendSourceBinding wrongDisc =
                exactBinding with { SourceImageSha256 = new string('0', 64) };
            if (TryAllow(
                    wrongDisc,
                    NativeTerrainTexturePrivateImageWriterKind.FixedTail,
                    appendedRecordCount: 1,
                    structuralGrowthPolicy:
                        NativeTerrainTextureStructuralGrowthPolicy.NormalChecked,
                    out string wrongDiscReason) ||
                !wrongDiscReason.Contains("No exact", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Normal release did not reject a mismatched retail disc fingerprint.");
            }

            if (TryAllow(
                    exactBinding,
                    NativeTerrainTexturePrivateImageWriterKind.SectorRelocation,
                    appendedRecordCount: 1,
                    structuralGrowthPolicy:
                        NativeTerrainTextureStructuralGrowthPolicy.NormalChecked,
                    out string wrongWriterReason) ||
                !wrongWriterReason.Contains("No exact", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Normal release did not reject the wrong structural writer.");
            }

            if (TryAllow(
                    exactBinding,
                    NativeTerrainTexturePrivateImageWriterKind.FixedTail,
                    appendedRecordCount: 1,
                    structuralGrowthPolicy:
                        NativeTerrainTextureStructuralGrowthPolicy.FiftyRowStaticResearch,
                    out string staticPolicyNormalReason) ||
                !staticPolicyNormalReason.Contains(
                    "static-research-only",
                    StringComparison.OrdinalIgnoreCase) ||
                !staticPolicyNormalReason.Contains(
                    "cannot authorize normal Create BIN",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The fifty-row static policy escaped into normal Create BIN authorization.");
            }

            Environment.SetEnvironmentVariable(EnvironmentVariable, "1");
            if (ResolveStructuralGrowthPolicy(exactGnastyBinding, 50) !=
                    NativeTerrainTextureStructuralGrowthPolicy
                        .FiftyRowStaticResearch ||
                ResolveAggregateStructuralGrowthPolicy(
                    [
                        NativeTerrainTextureStructuralGrowthPolicy
                            .FiftyRowStaticResearch
                    ]) !=
                    NativeTerrainTextureStructuralGrowthPolicy
                        .FiftyRowStaticResearch)
            {
                throw new InvalidOperationException(
                    "The explicit research switch did not retain its isolated static policy.");
            }
            if (!TryAllow(
                    exactBinding,
                    NativeTerrainTexturePrivateImageWriterKind.FixedTail,
                    appendedRecordCount: 5,
                    structuralGrowthPolicy:
                        NativeTerrainTextureStructuralGrowthPolicy.NormalChecked,
                    out string researchReason) ||
                !researchReason.Contains("strict 32-level", StringComparison.OrdinalIgnoreCase) ||
                !researchReason.Contains("at most 4", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The explicit research switch no longer admits the statically allowed five-record Artisans research destination.");
            }
            if (!TryAllow(
                    exactBinding,
                    NativeTerrainTexturePrivateImageWriterKind.FixedTail,
                    appendedRecordCount: 1,
                    structuralGrowthPolicy:
                        NativeTerrainTextureStructuralGrowthPolicy.FiftyRowStaticResearch,
                    out string staticResearchReason) ||
                !staticResearchReason.Contains(
                    "static-research-only",
                    StringComparison.OrdinalIgnoreCase) ||
                !staticResearchReason.Contains(
                    "Normal promotion remains unavailable",
                    StringComparison.OrdinalIgnoreCase) ||
                TryAuthorizeNormalCreateBin(
                    exactBinding,
                    NativeTerrainTexturePrivateImageWriterKind.FixedTail,
                    appendedRecordCount: 1,
                    structuralGrowthPolicy:
                        NativeTerrainTextureStructuralGrowthPolicy.FiftyRowStaticResearch,
                    out _))
            {
                throw new InvalidOperationException(
                    "The research switch either stopped admitting the disposable fifty-row policy or incorrectly promoted it to normal Create BIN.");
            }
            for (int requestedCount = 1;
                 requestedCount <= ResearchMaximumAppendedRecords;
                 requestedCount++)
            {
                if (!TryAllow(
                        wrongDisc,
                        NativeTerrainTexturePrivateImageWriterKind.SectorRelocation,
                        requestedCount,
                        NativeTerrainTextureStructuralGrowthPolicy.NormalChecked,
                        out string admittedCountReason))
                {
                    throw new InvalidOperationException(
                        $"Research count {requestedCount}/{ResearchMaximumAppendedRecords} was rejected: {admittedCountReason}");
                }
            }
            if (TryAllow(
                    wrongDisc,
                    NativeTerrainTexturePrivateImageWriterKind.SectorRelocation,
                    ResearchMaximumAppendedRecords + 1,
                    NativeTerrainTextureStructuralGrowthPolicy.NormalChecked,
                    out string overLimitReason) ||
                !overLimitReason.Contains(
                    $"at most {ResearchMaximumAppendedRecords}",
                    StringComparison.OrdinalIgnoreCase) ||
                !overLimitReason.Contains(
                    $"require {ResearchMaximumAppendedRecords + 1}",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Research count policy did not reject row {ResearchMaximumAppendedRecords + 1} " +
                    $"with its exact {ResearchMaximumAppendedRecords}-row limit.");
            }

            for (int currentlyStaged = 0;
                 currentlyStaged < ResearchMaximumAppendedRecords;
                 currentlyStaged++)
            {
                if (!TryPlanResearchEditorAllocation(
                        currentlyStaged,
                        reuseExistingRecord: false,
                        out int resultingCount,
                        out string allocationReason) ||
                    resultingCount != currentlyStaged + 1)
                {
                    throw new InvalidOperationException(
                        $"Editor allocation did not admit row {currentlyStaged + 1}: {allocationReason}");
                }
            }
            if (TryPlanResearchEditorAllocation(
                    ResearchMaximumAppendedRecords,
                    reuseExistingRecord: false,
                    out int rejectedResultingCount,
                    out string rejectedAllocationReason) ||
                rejectedResultingCount != ResearchMaximumAppendedRecords + 1 ||
                !rejectedAllocationReason.Contains(
                    $"at most {ResearchMaximumAppendedRecords}",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Editor allocation policy did not reject a new row before the " +
                    $"{ResearchMaximumAppendedRecords}-row staged set could be mutated.");
            }
            if (!TryPlanResearchEditorAllocation(
                    ResearchMaximumAppendedRecords,
                    reuseExistingRecord: true,
                    out int reusedResultingCount,
                    out string reuseReason) ||
                reusedResultingCount != ResearchMaximumAppendedRecords ||
                !reuseReason.Contains("no new row", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Editor allocation policy did not permit existing-row reuse at the " +
                    $"{ResearchMaximumAppendedRecords}-row maximum.");
            }
            if (TryAllow("loftycastle", out _) ||
                TryAllow("future-secret-level", out _))
            {
                throw new InvalidOperationException(
                    "The research switch admitted a strict-audit-blocked or unknown destination.");
            }

            if (!artisans.AllowsNormalRelease(
                    exactBinding,
                    NativeTerrainTexturePrivateImageWriterKind.FixedTail,
                    appendedRecordCount: 1) ||
                !artisans.AllowsNormalRelease(
                    exactBinding,
                    NativeTerrainTexturePrivateImageWriterKind.FixedTail,
                    appendedRecordCount: 4) ||
                artisans.AllowsNormalRelease(
                    wrongDisc,
                    NativeTerrainTexturePrivateImageWriterKind.FixedTail,
                    appendedRecordCount: 1) ||
                artisans.AllowsNormalRelease(
                    exactBinding,
                    NativeTerrainTexturePrivateImageWriterKind.SectorRelocation,
                    appendedRecordCount: 1) ||
                artisans.AllowsNormalRelease(
                    exactBinding,
                    NativeTerrainTexturePrivateImageWriterKind.FixedTail,
                    appendedRecordCount: 5))
            {
                throw new InvalidOperationException(
                    "The production policy is not exact across proof, retail fingerprint, writer kind, and four-record capacity.");
            }

            return
                "Artisans fixed-tail is runtime-proven for up to four appended private records; " +
                "Gnasty's World sector relocation is runtime-proven only for its exact clean-USA binding, RuntimeProvenExtended policy, and rows 1-50; row 51, wrong policy, and wrong disc were rejected; " +
                "Wizard Peak sector relocation is runtime-proven only for its exact clean-USA binding, RuntimeProvenPlus2000 policy, and appended rows 1-46; row 47, wrong policy, wrong writer, and wrong disc were rejected; " +
                "normal release accepted rows 1-4 and rejected row 5, the wrong disc, and the wrong writer; " +
                $"research admitted rows 1-{ResearchMaximumAppendedRecords}, rejected row " +
                $"{ResearchMaximumAppendedRecords + 1} before editor mutation, permitted existing-row reuse at the maximum, " +
                "retained the strict 32-level allow set, and kept FiftyRowStaticResearch outside normal Create BIN even when research was enabled.";
        }
        finally
        {
            Environment.SetEnvironmentVariable(EnvironmentVariable, previous);
        }
    }
}
