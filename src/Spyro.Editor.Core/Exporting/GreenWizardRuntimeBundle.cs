using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// The immutable, donor-side closure required by actor 0x011B.  Target-level
/// addresses and per-instance storage deliberately live in separate records.
/// </summary>
public sealed record GreenWizardRuntimeBundleManifest(
    int SchemaVersion,
    string BundleId,
    string DisplayName,
    string DonorLevelKey,
    int DonorOverlayEntry,
    int DonorDataEntry,
    uint OverlayLoadAddress,
    ushort GreenWizardActorId,
    ushort LightningActorId,
    IReadOnlyList<int> ParticleTypes,
    IReadOnlyList<RuntimeBundleDonorComponent> Components,
    GreenWizardRuntimePatchContract PatchContract,
    GreenWizardInstanceSchema InstanceSchema,
    string ProvenRecipeId,
    string ProvenOutputSha256)
{
    public string Fingerprint => RuntimeBundleFingerprint.Create(this);

    public RuntimeBundleDonorComponent Component(string id) =>
        Components.Single(component => string.Equals(component.Id, id, StringComparison.OrdinalIgnoreCase));

    public void Validate()
    {
        RuntimeBundleValidation.Require(SchemaVersion == 1, "The Green Wizard runtime-bundle schema must be version 1.");
        RuntimeBundleValidation.RequireText(BundleId, nameof(BundleId));
        RuntimeBundleValidation.RequireText(DisplayName, nameof(DisplayName));
        RuntimeBundleValidation.RequireText(DonorLevelKey, nameof(DonorLevelKey));
        RuntimeBundleValidation.Require(DonorOverlayEntry >= 0 && DonorDataEntry >= 0, "Donor WAD entries must be non-negative.");
        RuntimeBundleValidation.Require(OverlayLoadAddress >= 0x80000000, "The overlay load address must be a KSEG0 address.");
        RuntimeBundleValidation.Require(GreenWizardActorId == 0x011B, "The proven Green Wizard actor id must remain 0x011B.");
        RuntimeBundleValidation.Require(LightningActorId == 0x0026, "The proven lightning actor id must remain 0x0026.");
        RuntimeBundleValidation.Require(
            ParticleTypes.OrderBy(value => value).SequenceEqual([0x07, 0x41]),
            "The proven particle closure must contain exactly types 0x07 and 0x41.");
        RuntimeBundleValidation.Require(Components.Count > 0, "The donor manifest must contain components.");
        RuntimeBundleValidation.Require(
            Components.Select(component => component.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() == Components.Count,
            "Runtime-bundle component ids must be unique.");
        foreach (RuntimeBundleDonorComponent component in Components)
            component.Validate();

        string[] requiredComponents =
        [
            "green-wizard-actor-package",
            "compact-overlay-prefix",
            "lightning-actor-package",
            "particle-handler-bundle",
            "wizard-properties-template",
            "texture-dependency-aggregate",
            "lightning-trail-descriptor-template"
        ];
        foreach (string componentId in requiredComponents)
        {
            RuntimeBundleValidation.Require(
                Components.Any(component => string.Equals(component.Id, componentId, StringComparison.OrdinalIgnoreCase)),
                $"The donor manifest is missing required component '{componentId}'.");
        }

        RuntimeBundleValidation.RequireText(ProvenRecipeId, nameof(ProvenRecipeId));
        RuntimeBundleValidation.RequireSha256(ProvenOutputSha256, nameof(ProvenOutputSha256));
        PatchContract.Validate();
        InstanceSchema.Validate();
    }
}

public sealed record GreenWizardRuntimePatchContract(
    int DispatchShimBytes,
    int ParticleDescriptorBytes,
    string InstalledParticleDescriptorSha256,
    uint OriginalLightningDamageFlags,
    uint PatchedLightningDamageFlags,
    uint PatchedSpyroHurtState,
    uint PatchedSpyroAnimation,
    int SuppressedImpactChildCount,
    int RelocatedJumpCount,
    int PreservedBranchCount,
    int RelocatedPointerCount,
    int ExcludedTailPointerCount,
    int RelocatedHiLoPairCount)
{
    public void Validate()
    {
        RuntimeBundleValidation.Require(DispatchShimBytes == 0xA0, "The v11 dispatch shim must remain 0xA0 bytes.");
        RuntimeBundleValidation.Require(ParticleDescriptorBytes == 0x0C, "The v11 particle descriptor must remain 12 bytes.");
        RuntimeBundleValidation.RequireSha256(InstalledParticleDescriptorSha256, "installed particle descriptor");
        RuntimeBundleValidation.Require(OriginalLightningDamageFlags == 0x00020020, "The guarded native lightning damage flags changed.");
        RuntimeBundleValidation.Require(PatchedLightningDamageFlags == 1, "The transplanted hit must use native generic-damage flag 1.");
        RuntimeBundleValidation.Require(PatchedSpyroHurtState == 0x0E && PatchedSpyroAnimation == 0x0F, "The generic-hurt state/animation contract changed.");
        RuntimeBundleValidation.Require(SuppressedImpactChildCount == 4, "The impact quarantine must suppress exactly four unsafe child actors.");
        RuntimeBundleValidation.Require(
            RelocatedJumpCount == 357 && PreservedBranchCount == 1029 && RelocatedPointerCount == 675 &&
            ExcludedTailPointerCount == 178 && RelocatedHiLoPairCount == 9,
            "The compact-overlay relocation census changed.");
    }
}

public sealed record RuntimeBundleDonorComponent(
    string Id,
    RuntimeBundleStorageKind Storage,
    int SourceOffset,
    int ByteLength,
    string Sha256,
    RuntimeBundleRelocationKind Relocation,
    string Note)
{
    public void Validate()
    {
        RuntimeBundleValidation.RequireText(Id, nameof(Id));
        RuntimeBundleValidation.Require(SourceOffset >= 0, $"Component '{Id}' has a negative source offset.");
        RuntimeBundleValidation.Require(ByteLength > 0, $"Component '{Id}' must contain at least one byte.");
        RuntimeBundleValidation.RequireSha256(Sha256, $"{Id} SHA-256");
        RuntimeBundleValidation.RequireText(Note, $"{Id} note");
    }
}

public enum RuntimeBundleStorageKind
{
    Overlay,
    LevelData,
    Scene,
    TextureData,
    Executable
}

public enum RuntimeBundleRelocationKind
{
    RelocateOverlayCode,
    CopyActorPackage,
    CloneAndRebaseInstanceProperties,
    AllocateTargetTextureStorage,
    RebuildTargetDescriptor
}

/// <summary>
/// Donor-template fields from the runtime-proven Wizard Peak T6 instance.
/// Bundle installation is level-scoped; target-owned row policy and the
/// destination properties layout live in each level profile.
/// </summary>
public sealed record GreenWizardInstanceSchema(
    int SourceRecordBytes,
    int PropertiesBytes,
    int ActorIdLowOffset,
    int ActorIdHighOffset,
    int PodOrGroupOffset,
    byte PodOrGroupValue,
    int YawOffset,
    int CullingSectorOffset,
    int RendererDistanceOffset,
    byte RendererDistanceValue,
    int RewardOffset,
    int InternalRoutePointerOffset,
    int InternalRouteAnchorOffset,
    int FirstRoutePointOffset,
    int RoutePointStride,
    int RoutePointCount,
    bool PreserveTargetYaw,
    bool RecomputeTargetCullingSector,
    bool PreserveTargetReward)
{
    public void Validate()
    {
        RuntimeBundleValidation.Require(SourceRecordBytes == 0x58, "Green Wizard source records must use the 0x58-byte level-Moby stride.");
        RuntimeBundleValidation.Require(PropertiesBytes == 0x50, "Green Wizard properties must use the proven 0x50-byte T6 block.");
        RuntimeBundleValidation.Require(ActorIdLowOffset == 0x36 && ActorIdHighOffset == 0x37, "Actor-id fields must remain at +0x36/+0x37.");
        RuntimeBundleValidation.Require(PodOrGroupOffset == 0x43 && PodOrGroupValue == 0xFF, "The proven Wizard Peak T6 pod/group template must remain +0x43=0xFF.");
        RuntimeBundleValidation.Require(YawOffset == 0x46 && PreserveTargetYaw, "The target yaw must be preserved at +0x46.");
        RuntimeBundleValidation.Require(CullingSectorOffset == 0x4A && RecomputeTargetCullingSector, "The target culling sector must be recomputed at +0x4A.");
        RuntimeBundleValidation.Require(RendererDistanceOffset == 0x4B && RendererDistanceValue == 0x09, "The native Wizard renderer/distance byte must remain +0x4B=0x09.");
        RuntimeBundleValidation.Require(RewardOffset == 0x53 && PreserveTargetReward, "The replaced target's reward must be preserved at +0x53.");
        RuntimeBundleValidation.Require(InternalRoutePointerOffset == 0, "The properties-internal route pointer must remain at +0x00.");
        RuntimeBundleValidation.Require(InternalRouteAnchorOffset == 0x28, "The properties-internal route anchor must remain at +0x28.");
        RuntimeBundleValidation.Require(FirstRoutePointOffset == 0x30, "The first route point must remain at properties +0x30.");
        RuntimeBundleValidation.Require(RoutePointStride == 0x10 && RoutePointCount == 2, "The proven Wizard route must contain two 0x10-byte point slots.");
        int lastPointEnd = checked(FirstRoutePointOffset + ((RoutePointCount - 1) * RoutePointStride) + (3 * sizeof(int)));
        RuntimeBundleValidation.Require(lastPointEnd <= PropertiesBytes, "The Wizard route points exceed the properties block.");
    }
}

public enum RuntimeBundleDeploymentKind
{
    Native,
    ResidentActor,
    Transplanted
}

public enum RuntimeBundleEvidenceKind
{
    NativeGame,
    StaticResidentDetection,
    RuntimeProven
}

public sealed record RuntimeBundleInstanceLayout(
    string PropertiesDonorLevelKey,
    int PropertiesDonorTrueIndex,
    int PropertiesBytes,
    int InternalRouteAnchorOffset,
    int FirstRoutePointOffset,
    int RoutePointStride,
    int RoutePointCount,
    int AddPointerFixupDelta,
    int SwapPointerFixupDelta,
    bool PreserveTargetPodOrGroup,
    bool TranslateDonorRouteFromSpawn,
    string Note)
{
    public int PointerFixupDelta(GreenWizardPlacementMode mode) =>
        mode == GreenWizardPlacementMode.AppendSourceRecord ? AddPointerFixupDelta : SwapPointerFixupDelta;

    public void Validate()
    {
        RuntimeBundleValidation.RequireText(PropertiesDonorLevelKey, nameof(PropertiesDonorLevelKey));
        RuntimeBundleValidation.Require(PropertiesDonorTrueIndex >= 0, "The properties donor true index must be non-negative.");
        RuntimeBundleValidation.Require(PropertiesBytes > 0, "An instance properties layout must contain bytes.");
        RuntimeBundleValidation.Require(InternalRouteAnchorOffset >= 0 && InternalRouteAnchorOffset < PropertiesBytes, "The route anchor is outside the properties layout.");
        RuntimeBundleValidation.Require(FirstRoutePointOffset >= InternalRouteAnchorOffset, "The first route point precedes the route anchor.");
        RuntimeBundleValidation.Require(RoutePointStride > 0 && RoutePointCount > 0, "The route layout must contain at least one positive-stride point.");
        int lastPointEnd = checked(FirstRoutePointOffset + ((RoutePointCount - 1) * RoutePointStride) + (3 * sizeof(int)));
        RuntimeBundleValidation.Require(lastPointEnd <= PropertiesBytes, "The route points exceed the target properties layout.");
        RuntimeBundleValidation.Require(AddPointerFixupDelta is >= -1 and <= 1, "The add pointer-fixup delta must be -1, 0, or +1.");
        RuntimeBundleValidation.Require(SwapPointerFixupDelta is >= -1 and <= 1, "The swap pointer-fixup delta must be -1, 0, or +1.");
        RuntimeBundleValidation.Require(TranslateDonorRouteFromSpawn, "Green Wizard routes must preserve the donor route shape relative to the destination spawn.");
        RuntimeBundleValidation.RequireText(Note, "instance layout note");
    }
}

/// <summary>
/// All target-level addresses and capacity facts needed to install or reuse a
/// bundle.  Instance properties and routes are not stored here.
/// </summary>
public sealed record LevelRuntimeBundleProfile(
    string BundleId,
    string LevelKey,
    RuntimeBundleDeploymentKind Deployment,
    RuntimeBundleEvidenceKind Evidence,
    IReadOnlyList<ushort> ResidentActorIds,
    RuntimeBundleInstanceLayout InstanceLayout,
    RuntimeBundleResidentDetection? ResidentDetection,
    RuntimeBundleOverlayTarget? Overlay,
    RuntimeBundleDataTarget? Data,
    RuntimeBundleHookTarget? Hooks,
    IReadOnlyList<RuntimeBundleTextureTarget> Textures,
    IReadOnlyList<RuntimeBundleTargetGuard> Guards,
    string EvidenceNote,
    string RuntimeProofRecipeId = "",
    string RuntimeProofOutputSha256 = "")
{
    public string Fingerprint => RuntimeBundleFingerprint.Create(this);

    public bool HasResidentGreenWizard => ResidentActorIds.Contains((ushort)0x011B);

    public void Validate(GreenWizardRuntimeBundleManifest manifest)
    {
        RuntimeBundleValidation.Require(
            string.Equals(BundleId, manifest.BundleId, StringComparison.Ordinal),
            $"Profile '{LevelKey}' targets bundle '{BundleId}', not '{manifest.BundleId}'.");
        RuntimeBundleValidation.RequireText(LevelKey, nameof(LevelKey));
        RuntimeBundleValidation.RequireText(EvidenceNote, $"{LevelKey} evidence note");
        RuntimeBundleValidation.Require(
            ResidentActorIds.Distinct().Count() == ResidentActorIds.Count,
            $"Profile '{LevelKey}' contains duplicate resident actor ids.");
        InstanceLayout.Validate();
        ResidentDetection?.Validate(ResidentActorIds);

        if (Deployment == RuntimeBundleDeploymentKind.Native)
        {
            RuntimeBundleValidation.Require(
                Evidence is RuntimeBundleEvidenceKind.NativeGame or RuntimeBundleEvidenceKind.RuntimeProven,
                "A native bundle profile must use native-game or runtime-proven evidence.");
            RuntimeBundleValidation.Require(HasResidentGreenWizard, "A native Green Wizard profile must contain actor 0x011B.");
        }
        else if (Deployment == RuntimeBundleDeploymentKind.ResidentActor)
        {
            RuntimeBundleValidation.Require(Evidence != RuntimeBundleEvidenceKind.NativeGame, "A resident-actor profile must not claim native-donor evidence.");
            RuntimeBundleValidation.Require(HasResidentGreenWizard, "A resident Green Wizard profile must contain actor 0x011B.");
            RuntimeBundleValidation.Require(ResidentDetection != null, "A resident-actor profile must record the roots and dispatch comparisons which support that classification.");
        }
        else
        {
            RuntimeBundleValidation.Require(Overlay != null && Data != null && Hooks != null, $"Transplant profile '{LevelKey}' is missing its target map.");
            RuntimeBundleValidation.Require(Textures.Count > 0, $"Transplant profile '{LevelKey}' is missing target texture allocations.");
            RuntimeBundleValidation.Require(Guards.Count > 0, $"Transplant profile '{LevelKey}' is missing preimage guards.");
        }

        Overlay?.Validate(manifest);
        Data?.Validate(manifest);
        Hooks?.Validate();
        RuntimeBundleValidation.Require(
            Textures.Select(texture => texture.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() == Textures.Count,
            $"Profile '{LevelKey}' contains duplicate texture target ids.");
        foreach (RuntimeBundleTextureTarget texture in Textures)
            texture.Validate();
        RuntimeBundleValidation.Require(
            Guards.Select(guard => guard.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() == Guards.Count,
            $"Profile '{LevelKey}' contains duplicate target guard ids.");
        foreach (RuntimeBundleTargetGuard guard in Guards)
            guard.Validate();

        if (Evidence == RuntimeBundleEvidenceKind.RuntimeProven)
        {
            RuntimeBundleValidation.RequireText(RuntimeProofRecipeId, $"{LevelKey} runtime-proof recipe");
            RuntimeBundleValidation.RequireSha256(RuntimeProofOutputSha256, $"{LevelKey} runtime-proof output");
        }
        else
        {
            RuntimeBundleValidation.Require(
                string.IsNullOrWhiteSpace(RuntimeProofRecipeId) && string.IsNullOrWhiteSpace(RuntimeProofOutputSha256),
                $"Profile '{LevelKey}' supplies a runtime-proof artifact without runtime-proven evidence.");
        }
    }
}

public sealed record RuntimeBundleResidentDetection(
    int OverlayEntry,
    int DataEntry,
    int SceneBaseOffset,
    int SceneBytes,
    IReadOnlyList<RuntimeBundleResidentActorRoot> ActorRoots,
    IReadOnlyList<RuntimeBundleResidentDispatch> Dispatches,
    RuntimeBundleResidentPropertiesLayout? PropertiesLayout,
    string EvidenceNote)
{
    public void Validate(IReadOnlyList<ushort> declaredActorIds)
    {
        RuntimeBundleValidation.Require(OverlayEntry >= 0 && DataEntry >= 0, "Resident detection WAD entries must be non-negative.");
        RuntimeBundleValidation.Require(SceneBaseOffset >= 0 && SceneBytes > 0, "Resident detection scene range is invalid.");
        RuntimeBundleValidation.RequireText(EvidenceNote, "resident detection evidence note");
        RuntimeBundleValidation.Require(ActorRoots.Count > 0 && Dispatches.Count > 0, "Resident detection requires both actor roots and overlay dispatch comparisons.");
        RuntimeBundleValidation.Require(ActorRoots.Select(root => root.ActorId).Distinct().Count() == ActorRoots.Count, "Resident actor-root ids must be unique.");
        RuntimeBundleValidation.Require(Dispatches.Select(dispatch => dispatch.ActorId).Distinct().Count() == Dispatches.Count, "Resident dispatch actor ids must be unique.");
        foreach (RuntimeBundleResidentActorRoot root in ActorRoots)
            root.Validate();
        foreach (RuntimeBundleResidentDispatch dispatch in Dispatches)
            dispatch.Validate();
        foreach (ushort actorId in declaredActorIds)
        {
            RuntimeBundleValidation.Require(ActorRoots.Any(root => root.ActorId == actorId), $"Resident actor 0x{actorId:X4} is missing root evidence.");
            RuntimeBundleValidation.Require(Dispatches.Any(dispatch => dispatch.ActorId == actorId), $"Resident actor 0x{actorId:X4} is missing dispatch evidence.");
        }
        PropertiesLayout?.Validate(SceneBytes);
    }
}

public sealed record RuntimeBundleResidentActorRoot(ushort ActorId, int RootSlotOffset, int RootOffset)
{
    public void Validate()
    {
        RuntimeBundleValidation.Require(RootSlotOffset >= 0 && (RootSlotOffset & 3) == 0, $"Resident actor 0x{ActorId:X4} has an invalid root slot.");
        RuntimeBundleValidation.Require(RootOffset > 0 && (RootOffset & 3) == 0, $"Resident actor 0x{ActorId:X4} has an invalid root offset.");
    }
}

public sealed record RuntimeBundleResidentDispatch(ushort ActorId, uint CompareAddress)
{
    public void Validate() =>
        RuntimeBundleValidation.Require(CompareAddress >= 0x80000000 && (CompareAddress & 3) == 0, $"Resident actor 0x{ActorId:X4} has an invalid dispatch address.");
}

public sealed record RuntimeBundleResidentPropertiesLayout(
    int ExampleTrueIndex,
    int PropertiesSceneOffset,
    int PropertiesBytes,
    int InternalRoutePointer,
    int RoutePointCount,
    int PropertiesComponentHeaderSceneOffset,
    int PropertiesComponentBytes,
    int PointerFixupListSceneOffset,
    int PointerFixupCount,
    int TrailingScenePaddingBytes)
{
    public void Validate(int sceneBytes)
    {
        RuntimeBundleValidation.Require(ExampleTrueIndex >= 0, "Resident properties evidence requires a non-negative example index.");
        RuntimeBundleValidation.Require(PropertiesSceneOffset >= 0 && PropertiesBytes > 0 && PropertiesSceneOffset <= sceneBytes - PropertiesBytes, "Resident properties range exceeds the scene.");
        RuntimeBundleValidation.Require(InternalRoutePointer >= PropertiesSceneOffset && InternalRoutePointer < PropertiesSceneOffset + PropertiesBytes, "Resident route pointer is outside its properties block.");
        RuntimeBundleValidation.Require(RoutePointCount > 0, "Resident properties evidence must include a route point.");
        RuntimeBundleValidation.Require(PropertiesComponentHeaderSceneOffset >= 0 && PropertiesComponentBytes > sizeof(uint), "Resident properties component metadata is invalid.");
        RuntimeBundleValidation.Require(PropertiesComponentHeaderSceneOffset + PropertiesComponentBytes <= sceneBytes, "Resident properties component exceeds the scene.");
        RuntimeBundleValidation.Require(PropertiesSceneOffset >= PropertiesComponentHeaderSceneOffset + sizeof(uint) && PropertiesSceneOffset + PropertiesBytes <= PropertiesComponentHeaderSceneOffset + PropertiesComponentBytes, "Resident example properties are outside the declared properties component.");
        RuntimeBundleValidation.Require(PointerFixupListSceneOffset >= 0 && PointerFixupListSceneOffset < sceneBytes, "Resident pointer-fixup list is outside the scene.");
        RuntimeBundleValidation.Require(PointerFixupCount > 0 && TrailingScenePaddingBytes >= 0, "Resident pointer-fixup evidence is invalid.");
        int pointerFixupEnd = checked(PointerFixupListSceneOffset + sizeof(uint) + (PointerFixupCount * sizeof(uint)));
        RuntimeBundleValidation.Require(pointerFixupEnd + TrailingScenePaddingBytes == sceneBytes, "Resident trailing scene padding does not begin immediately after the pointer-fixup list.");
    }
}

public sealed record RuntimeBundleOverlayTarget(
    int OverlayEntry,
    int NativeOverlayBytes,
    int RequiredExpandedOverlayBytes,
    int MaximumExpandedOverlayBytes,
    int RequiredWadGrowthBytes,
    int AvailableIsoGrowthBytes,
    uint OverlayLoadAddress,
    uint OriginalCopyBufferAddress,
    uint RelocatedCopyBufferAddress,
    uint CopyBufferCeilingAddress,
    int PostCopyBufferFootprintBytes,
    uint LowerPolygonBufferAddress,
    int MinimumPolygonBufferMarginBytes,
    int ActualPolygonBufferMarginBytes,
    int ExecutableCopyBufferHiOffset,
    int ExecutableCopyBufferLoOffset)
{
    public int RequiredAppendBytes => checked(RequiredExpandedOverlayBytes - NativeOverlayBytes);

    public void Validate(GreenWizardRuntimeBundleManifest manifest)
    {
        RuntimeBundleValidation.Require(OverlayEntry >= 0, "The target overlay entry must be non-negative.");
        RuntimeBundleValidation.Require(NativeOverlayBytes > 0, "The native target overlay must be non-empty.");
        RuntimeBundleValidation.Require(RequiredExpandedOverlayBytes >= NativeOverlayBytes, "The expanded overlay cannot be smaller than the native overlay.");
        RuntimeBundleValidation.Require(MaximumExpandedOverlayBytes >= RequiredExpandedOverlayBytes, "The target overlay capacity is smaller than the required bundle.");
        RuntimeBundleValidation.Require(RequiredWadGrowthBytes == RequiredAppendBytes, "The checked WAD growth must equal the overlay append size.");
        RuntimeBundleValidation.Require(AvailableIsoGrowthBytes >= RequiredWadGrowthBytes, "The disc has insufficient ISO extent headroom.");
        RuntimeBundleValidation.Require(OverlayLoadAddress == manifest.OverlayLoadAddress, "The target and donor overlays must share the checked load address.");
        RuntimeBundleValidation.Require(RelocatedCopyBufferAddress == checked(OverlayLoadAddress + (uint)RequiredExpandedOverlayBytes), "The relocated copy buffer must begin immediately after the expanded overlay.");
        RuntimeBundleValidation.Require(RelocatedCopyBufferAddress <= CopyBufferCeilingAddress, "The relocated copy buffer exceeds its checked ceiling.");
        uint sceneEnd = checked(RelocatedCopyBufferAddress + (uint)PostCopyBufferFootprintBytes);
        RuntimeBundleValidation.Require(sceneEnd <= LowerPolygonBufferAddress, "The relocated scene footprint overlaps the lower polygon buffer.");
        RuntimeBundleValidation.Require(ActualPolygonBufferMarginBytes == checked((int)(LowerPolygonBufferAddress - sceneEnd)), "The recorded polygon-buffer margin does not match the target addresses.");
        RuntimeBundleValidation.Require(ActualPolygonBufferMarginBytes >= MinimumPolygonBufferMarginBytes, "The target does not retain the minimum polygon-buffer margin.");
        RuntimeBundleValidation.Require(ExecutableCopyBufferHiOffset >= 0 && ExecutableCopyBufferLoOffset == ExecutableCopyBufferHiOffset + 4, "The executable copy-buffer HI16/LO16 offsets are invalid.");
    }
}

public sealed record RuntimeBundleDataTarget(
    int DataEntry,
    int SceneBaseOffset,
    int SceneBytes,
    int GreenWizardPackageOffset,
    int GreenWizardPackageBytes,
    int GreenWizardRootSlotOffset,
    ushort ReplacedActorId,
    int LightningPackageOffset,
    int LightningPackageBytes,
    int LightningRootSlotOffset,
    int ActorIdTableOffset)
{
    public void Validate(GreenWizardRuntimeBundleManifest manifest)
    {
        RuntimeBundleValidation.Require(DataEntry >= 0, "The target data entry must be non-negative.");
        RuntimeBundleValidation.Require(SceneBaseOffset >= 0 && SceneBytes > 0, "The target scene range is invalid.");
        RuntimeBundleValidation.Require(GreenWizardPackageOffset >= 0 && GreenWizardPackageBytes == manifest.Component("green-wizard-actor-package").ByteLength, "The target Green Wizard package size does not match the donor manifest.");
        RuntimeBundleValidation.Require(GreenWizardRootSlotOffset >= 0 && (GreenWizardRootSlotOffset & 3) == 0, "The Green Wizard root slot must be word-aligned.");
        RuntimeBundleValidation.Require(LightningPackageOffset >= 0 && LightningPackageBytes == manifest.Component("lightning-actor-package").ByteLength, "The target lightning package size does not match the donor manifest.");
        RuntimeBundleValidation.Require(LightningRootSlotOffset >= 0 && (LightningRootSlotOffset & 3) == 0, "The lightning root slot must be word-aligned.");
        RuntimeBundleValidation.Require(ActorIdTableOffset >= 0 && (ActorIdTableOffset & 1) == 0, "The actor-id table offset must be halfword-aligned.");
        RuntimeBundleValidation.Require(GreenWizardPackageOffset != LightningPackageOffset, "Wizard and lightning packages cannot share a target offset.");
    }
}

public sealed record RuntimeBundleHookTarget(
    uint DispatchHookAddress,
    uint DispatchContinueAddress,
    uint NextMobyAddress,
    uint DispatchShimAddress,
    uint GreenWizardHandlerAddress,
    uint LightningHandlerAddress,
    uint LightningDamagePatchAddress,
    uint LightningImpactBurstPatchAddress,
    uint LightningImpactSafeExitAddress,
    uint WizardSpawnInitializerAddress,
    uint ParticleSpawnTableAddress,
    uint ParticleUpdateTableAddress,
    uint ParticleSpawnType7Address,
    uint ParticleSpawnType41Address,
    uint ParticleUpdateType7Address,
    uint ParticleUpdateType41Address,
    uint ParticleTexturePointerSlotAddress,
    uint ParticleDescriptorAddress)
{
    public void Validate()
    {
        uint[] addresses =
        [
            DispatchHookAddress,
            DispatchContinueAddress,
            NextMobyAddress,
            DispatchShimAddress,
            GreenWizardHandlerAddress,
            LightningHandlerAddress,
            LightningDamagePatchAddress,
            LightningImpactBurstPatchAddress,
            LightningImpactSafeExitAddress,
            WizardSpawnInitializerAddress,
            ParticleSpawnTableAddress,
            ParticleUpdateTableAddress,
            ParticleSpawnType7Address,
            ParticleSpawnType41Address,
            ParticleUpdateType7Address,
            ParticleUpdateType41Address,
            ParticleTexturePointerSlotAddress,
            ParticleDescriptorAddress
        ];
        RuntimeBundleValidation.Require(addresses.All(address => address >= 0x80000000 && (address & 3) == 0), "All runtime-bundle hook addresses must be word-aligned KSEG0 addresses.");
        RuntimeBundleValidation.Require(ParticleSpawnType7Address != ParticleSpawnType41Address, "Particle spawn handlers must be distinct.");
        RuntimeBundleValidation.Require(ParticleUpdateType7Address != ParticleUpdateType41Address, "Particle update handlers must be distinct.");
    }
}

public sealed record RuntimeBundleTextureTarget(
    string Id,
    int DonorPixelOffset,
    int TargetPixelOffset,
    int PixelRows,
    int PixelRowBytes,
    int PixelRowStrideBytes,
    int DonorClutOffset,
    int TargetClutOffset,
    int ClutBytes,
    string DonorPixelsSha256,
    string TargetPixelsPreimageSha256,
    string DonorClutSha256,
    string TargetClutPreimageSha256)
{
    public int PixelBytes => checked(PixelRows * PixelRowBytes);

    public void Validate()
    {
        RuntimeBundleValidation.RequireText(Id, nameof(Id));
        RuntimeBundleValidation.Require(DonorPixelOffset >= 0 && TargetPixelOffset >= 0, $"Texture '{Id}' has a negative pixel offset.");
        RuntimeBundleValidation.Require(PixelRows > 0 && PixelRowBytes > 0 && PixelRowStrideBytes >= PixelRowBytes, $"Texture '{Id}' has invalid row geometry.");
        RuntimeBundleValidation.Require(DonorClutOffset >= 0 && TargetClutOffset >= 0 && ClutBytes > 0, $"Texture '{Id}' has an invalid CLUT range.");
        RuntimeBundleValidation.RequireSha256(DonorPixelsSha256, $"{Id} donor pixels");
        RuntimeBundleValidation.RequireSha256(TargetPixelsPreimageSha256, $"{Id} target pixel preimage");
        RuntimeBundleValidation.RequireSha256(DonorClutSha256, $"{Id} donor CLUT");
        RuntimeBundleValidation.RequireSha256(TargetClutPreimageSha256, $"{Id} target CLUT preimage");
    }
}

public enum RuntimeBundleGuardKind
{
    Sha256,
    UInt32
}

public sealed record RuntimeBundleTargetGuard(
    string Id,
    RuntimeBundleStorageKind Storage,
    int Offset,
    int ByteLength,
    RuntimeBundleGuardKind Kind,
    string Expected,
    string Note)
{
    public void Validate()
    {
        RuntimeBundleValidation.RequireText(Id, nameof(Id));
        RuntimeBundleValidation.Require(Offset >= 0 && ByteLength > 0, $"Guard '{Id}' has an invalid range.");
        RuntimeBundleValidation.RequireText(Note, $"{Id} note");
        if (Kind == RuntimeBundleGuardKind.Sha256)
        {
            RuntimeBundleValidation.RequireSha256(Expected, $"{Id} expected SHA-256");
        }
        else
        {
            RuntimeBundleValidation.Require(ByteLength == sizeof(uint), $"UInt32 guard '{Id}' must cover four bytes.");
            RuntimeBundleValidation.Require(
                Expected.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
                uint.TryParse(Expected.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _),
                $"UInt32 guard '{Id}' must use a hexadecimal 0x value.");
        }
    }
}

public enum GreenWizardPlacementMode
{
    ReplaceExistingSlot,
    AppendSourceRecord
}

public sealed record GreenWizardRoutePoint(int X, int Y, int Z);

/// <summary>
/// A single resolved object placement.  This is intentionally not part of the
/// level profile so one installed bundle can serve any number of instances.
/// </summary>
public sealed record GreenWizardInstancePlan(
    string Id,
    string TargetLevelKey,
    GreenWizardPlacementMode Mode,
    int TargetTrueIndex,
    int PropertiesPointerFieldSceneOffset,
    uint PropertiesPointerValue,
    int PropertiesSceneOffset,
    int PropertiesBytes,
    uint RebasedInternalRoutePointer,
    int PointerFixupListSceneOffset,
    int PointerFixupCountBefore,
    int PointerFixupCountAfter,
    uint RemovedPointerFixup,
    int RewardValue,
    IReadOnlyList<GreenWizardRoutePoint> RoutePoints,
    string PropertiesTargetPreimageSha256,
    string PropertiesInstalledSha256,
    string PointerFixupsTargetPreimageSha256,
    string PointerFixupsInstalledSha256)
{
    public string Fingerprint => RuntimeBundleFingerprint.Create(this);

    public void Validate(GreenWizardRuntimeBundleManifest manifest, LevelRuntimeBundleProfile profile)
    {
        RuntimeBundleValidation.RequireText(Id, nameof(Id));
        RuntimeBundleValidation.Require(
            string.Equals(TargetLevelKey, profile.LevelKey, StringComparison.OrdinalIgnoreCase),
            $"Instance '{Id}' targets '{TargetLevelKey}', not profile '{profile.LevelKey}'.");
        RuntimeBundleValidation.Require(TargetTrueIndex >= 0, $"Instance '{Id}' has a negative source index.");
        RuntimeBundleValidation.Require(PropertiesPointerFieldSceneOffset >= 0 && PropertiesSceneOffset >= 0, $"Instance '{Id}' has invalid scene offsets.");
        RuntimeBundleValidation.Require(PropertiesBytes == profile.InstanceLayout.PropertiesBytes, $"Instance '{Id}' properties size does not match the target-level layout.");
        RuntimeBundleValidation.Require(RebasedInternalRoutePointer == checked((uint)(PropertiesSceneOffset + profile.InstanceLayout.InternalRouteAnchorOffset)), $"Instance '{Id}' has an inconsistent rebased route pointer.");
        RuntimeBundleValidation.Require(PointerFixupListSceneOffset >= 0, $"Instance '{Id}' has an invalid fixup-list offset.");
        int expectedFixupDelta = profile.InstanceLayout.PointerFixupDelta(Mode);
        RuntimeBundleValidation.Require(PointerFixupCountBefore > 0 && PointerFixupCountAfter == PointerFixupCountBefore + expectedFixupDelta, $"Instance '{Id}' pointer-fixup delta must be {expectedFixupDelta:+#;-#;0} for this target and placement mode.");
        RuntimeBundleValidation.Require(RewardValue is >= 0 and <= byte.MaxValue, $"Instance '{Id}' reward does not fit the source record.");
        RuntimeBundleValidation.Require(RoutePoints.Count == profile.InstanceLayout.RoutePointCount, $"Instance '{Id}' must contain exactly {profile.InstanceLayout.RoutePointCount} route points.");
        RuntimeBundleValidation.RequireSha256(PropertiesTargetPreimageSha256, $"{Id} properties target preimage");
        RuntimeBundleValidation.RequireSha256(PropertiesInstalledSha256, $"{Id} installed properties");
        RuntimeBundleValidation.RequireSha256(PointerFixupsTargetPreimageSha256, $"{Id} fixup target preimage");
        RuntimeBundleValidation.RequireSha256(PointerFixupsInstalledSha256, $"{Id} installed fixups");
        int? targetSceneBytes = profile.Data?.SceneBytes ?? profile.ResidentDetection?.SceneBytes;
        RuntimeBundleValidation.Require(targetSceneBytes != null, $"Instance '{Id}' requires a target scene map.");
        int sceneBytes = targetSceneBytes!.Value;
        RuntimeBundleValidation.Require(PropertiesSceneOffset <= sceneBytes - PropertiesBytes, $"Instance '{Id}' properties exceed the target scene.");
        RuntimeBundleValidation.Require(PropertiesPointerFieldSceneOffset <= sceneBytes - sizeof(uint), $"Instance '{Id}' properties pointer exceeds the target scene.");
    }
}

public enum RuntimeBundleCompatibilityStatus
{
    Ready,
    Candidate,
    NeedsTargetProfile,
    Blocked
}

public sealed record RuntimeBundleCompatibilityResult(
    string BundleId,
    string BundleFingerprint,
    string TargetLevelKey,
    RuntimeBundleCompatibilityStatus Status,
    RuntimeBundleDeploymentKind? Deployment,
    bool CanStageInstance,
    bool NormalCreateBinReady,
    bool NeedsLevelBundleInstall,
    bool RequiresRuntimeSmoke,
    string ProfileFingerprint,
    IReadOnlyList<string> Findings,
    IReadOnlyList<string> RequiredWork);

internal static class RuntimeBundleFingerprint
{
    public static string Create(GreenWizardRuntimeBundleManifest manifest)
    {
        StringBuilder builder = Begin("green-wizard-manifest-v1");
        Add(builder, manifest.SchemaVersion);
        Add(builder, manifest.BundleId);
        Add(builder, manifest.DisplayName);
        Add(builder, manifest.DonorLevelKey);
        Add(builder, manifest.DonorOverlayEntry);
        Add(builder, manifest.DonorDataEntry);
        Add(builder, manifest.OverlayLoadAddress);
        Add(builder, manifest.GreenWizardActorId);
        Add(builder, manifest.LightningActorId);
        foreach (int particleType in manifest.ParticleTypes.OrderBy(value => value))
            Add(builder, particleType);
        foreach (RuntimeBundleDonorComponent component in manifest.Components.OrderBy(component => component.Id, StringComparer.OrdinalIgnoreCase))
        {
            Add(builder, component.Id);
            Add(builder, component.Storage);
            Add(builder, component.SourceOffset);
            Add(builder, component.ByteLength);
            Add(builder, component.Sha256);
            Add(builder, component.Relocation);
        }
        Add(builder, manifest.PatchContract.DispatchShimBytes);
        Add(builder, manifest.PatchContract.ParticleDescriptorBytes);
        Add(builder, manifest.PatchContract.InstalledParticleDescriptorSha256);
        Add(builder, manifest.PatchContract.OriginalLightningDamageFlags);
        Add(builder, manifest.PatchContract.PatchedLightningDamageFlags);
        Add(builder, manifest.PatchContract.PatchedSpyroHurtState);
        Add(builder, manifest.PatchContract.PatchedSpyroAnimation);
        Add(builder, manifest.PatchContract.SuppressedImpactChildCount);
        Add(builder, manifest.PatchContract.RelocatedJumpCount);
        Add(builder, manifest.PatchContract.PreservedBranchCount);
        Add(builder, manifest.PatchContract.RelocatedPointerCount);
        Add(builder, manifest.PatchContract.ExcludedTailPointerCount);
        Add(builder, manifest.PatchContract.RelocatedHiLoPairCount);
        AddInstanceSchema(builder, manifest.InstanceSchema);
        Add(builder, manifest.ProvenRecipeId);
        Add(builder, manifest.ProvenOutputSha256);
        return Finish(builder);
    }

    public static string Create(LevelRuntimeBundleProfile profile)
    {
        StringBuilder builder = Begin("level-runtime-bundle-profile-v2");
        Add(builder, profile.BundleId);
        Add(builder, profile.LevelKey);
        Add(builder, profile.Deployment);
        Add(builder, profile.Evidence);
        foreach (ushort actorId in profile.ResidentActorIds.OrderBy(value => value))
            Add(builder, actorId);
        Add(builder, profile.InstanceLayout.PropertiesDonorLevelKey);
        Add(builder, profile.InstanceLayout.PropertiesDonorTrueIndex);
        Add(builder, profile.InstanceLayout.PropertiesBytes);
        Add(builder, profile.InstanceLayout.InternalRouteAnchorOffset);
        Add(builder, profile.InstanceLayout.FirstRoutePointOffset);
        Add(builder, profile.InstanceLayout.RoutePointStride);
        Add(builder, profile.InstanceLayout.RoutePointCount);
        Add(builder, profile.InstanceLayout.AddPointerFixupDelta);
        Add(builder, profile.InstanceLayout.SwapPointerFixupDelta);
        Add(builder, profile.InstanceLayout.PreserveTargetPodOrGroup);
        Add(builder, profile.InstanceLayout.TranslateDonorRouteFromSpawn);
        if (profile.ResidentDetection != null)
        {
            Add(builder, profile.ResidentDetection.OverlayEntry);
            Add(builder, profile.ResidentDetection.DataEntry);
            Add(builder, profile.ResidentDetection.SceneBaseOffset);
            Add(builder, profile.ResidentDetection.SceneBytes);
            foreach (RuntimeBundleResidentActorRoot root in profile.ResidentDetection.ActorRoots.OrderBy(root => root.ActorId))
            {
                Add(builder, root.ActorId);
                Add(builder, root.RootSlotOffset);
                Add(builder, root.RootOffset);
            }
            foreach (RuntimeBundleResidentDispatch dispatch in profile.ResidentDetection.Dispatches.OrderBy(dispatch => dispatch.ActorId))
            {
                Add(builder, dispatch.ActorId);
                Add(builder, dispatch.CompareAddress);
            }
            if (profile.ResidentDetection.PropertiesLayout != null)
            {
                RuntimeBundleResidentPropertiesLayout layout = profile.ResidentDetection.PropertiesLayout;
                Add(builder, layout.ExampleTrueIndex);
                Add(builder, layout.PropertiesSceneOffset);
                Add(builder, layout.PropertiesBytes);
                Add(builder, layout.InternalRoutePointer);
                Add(builder, layout.RoutePointCount);
                Add(builder, layout.PropertiesComponentHeaderSceneOffset);
                Add(builder, layout.PropertiesComponentBytes);
                Add(builder, layout.PointerFixupListSceneOffset);
                Add(builder, layout.PointerFixupCount);
                Add(builder, layout.TrailingScenePaddingBytes);
            }
        }
        if (profile.Overlay != null)
        {
            Add(builder, profile.Overlay.OverlayEntry);
            Add(builder, profile.Overlay.NativeOverlayBytes);
            Add(builder, profile.Overlay.RequiredExpandedOverlayBytes);
            Add(builder, profile.Overlay.MaximumExpandedOverlayBytes);
            Add(builder, profile.Overlay.RequiredWadGrowthBytes);
            Add(builder, profile.Overlay.AvailableIsoGrowthBytes);
            Add(builder, profile.Overlay.OverlayLoadAddress);
            Add(builder, profile.Overlay.OriginalCopyBufferAddress);
            Add(builder, profile.Overlay.RelocatedCopyBufferAddress);
            Add(builder, profile.Overlay.CopyBufferCeilingAddress);
            Add(builder, profile.Overlay.PostCopyBufferFootprintBytes);
            Add(builder, profile.Overlay.LowerPolygonBufferAddress);
            Add(builder, profile.Overlay.MinimumPolygonBufferMarginBytes);
            Add(builder, profile.Overlay.ActualPolygonBufferMarginBytes);
            Add(builder, profile.Overlay.ExecutableCopyBufferHiOffset);
            Add(builder, profile.Overlay.ExecutableCopyBufferLoOffset);
        }
        if (profile.Data != null)
        {
            Add(builder, profile.Data.DataEntry);
            Add(builder, profile.Data.SceneBaseOffset);
            Add(builder, profile.Data.SceneBytes);
            Add(builder, profile.Data.GreenWizardPackageOffset);
            Add(builder, profile.Data.GreenWizardPackageBytes);
            Add(builder, profile.Data.GreenWizardRootSlotOffset);
            Add(builder, profile.Data.ReplacedActorId);
            Add(builder, profile.Data.LightningPackageOffset);
            Add(builder, profile.Data.LightningPackageBytes);
            Add(builder, profile.Data.LightningRootSlotOffset);
            Add(builder, profile.Data.ActorIdTableOffset);
        }
        if (profile.Hooks != null)
        {
            Add(builder, profile.Hooks.DispatchHookAddress);
            Add(builder, profile.Hooks.DispatchContinueAddress);
            Add(builder, profile.Hooks.NextMobyAddress);
            Add(builder, profile.Hooks.DispatchShimAddress);
            Add(builder, profile.Hooks.GreenWizardHandlerAddress);
            Add(builder, profile.Hooks.LightningHandlerAddress);
            Add(builder, profile.Hooks.LightningDamagePatchAddress);
            Add(builder, profile.Hooks.LightningImpactBurstPatchAddress);
            Add(builder, profile.Hooks.LightningImpactSafeExitAddress);
            Add(builder, profile.Hooks.WizardSpawnInitializerAddress);
            Add(builder, profile.Hooks.ParticleSpawnTableAddress);
            Add(builder, profile.Hooks.ParticleUpdateTableAddress);
            Add(builder, profile.Hooks.ParticleSpawnType7Address);
            Add(builder, profile.Hooks.ParticleSpawnType41Address);
            Add(builder, profile.Hooks.ParticleUpdateType7Address);
            Add(builder, profile.Hooks.ParticleUpdateType41Address);
            Add(builder, profile.Hooks.ParticleTexturePointerSlotAddress);
            Add(builder, profile.Hooks.ParticleDescriptorAddress);
        }
        foreach (RuntimeBundleTextureTarget texture in profile.Textures.OrderBy(texture => texture.Id, StringComparer.OrdinalIgnoreCase))
        {
            Add(builder, texture.Id);
            Add(builder, texture.DonorPixelOffset);
            Add(builder, texture.TargetPixelOffset);
            Add(builder, texture.PixelRows);
            Add(builder, texture.PixelRowBytes);
            Add(builder, texture.PixelRowStrideBytes);
            Add(builder, texture.DonorClutOffset);
            Add(builder, texture.TargetClutOffset);
            Add(builder, texture.ClutBytes);
            Add(builder, texture.DonorPixelsSha256);
            Add(builder, texture.TargetPixelsPreimageSha256);
            Add(builder, texture.DonorClutSha256);
            Add(builder, texture.TargetClutPreimageSha256);
        }
        foreach (RuntimeBundleTargetGuard guard in profile.Guards.OrderBy(guard => guard.Id, StringComparer.OrdinalIgnoreCase))
        {
            Add(builder, guard.Id);
            Add(builder, guard.Storage);
            Add(builder, guard.Offset);
            Add(builder, guard.ByteLength);
            Add(builder, guard.Kind);
            Add(builder, guard.Expected);
        }
        Add(builder, profile.RuntimeProofRecipeId);
        Add(builder, profile.RuntimeProofOutputSha256);
        return Finish(builder);
    }

    public static string Create(GreenWizardInstancePlan plan)
    {
        StringBuilder builder = Begin("green-wizard-instance-v1");
        Add(builder, plan.Id);
        Add(builder, plan.TargetLevelKey);
        Add(builder, plan.Mode);
        Add(builder, plan.TargetTrueIndex);
        Add(builder, plan.PropertiesPointerFieldSceneOffset);
        Add(builder, plan.PropertiesPointerValue);
        Add(builder, plan.PropertiesSceneOffset);
        Add(builder, plan.PropertiesBytes);
        Add(builder, plan.RebasedInternalRoutePointer);
        Add(builder, plan.PointerFixupListSceneOffset);
        Add(builder, plan.PointerFixupCountBefore);
        Add(builder, plan.PointerFixupCountAfter);
        Add(builder, plan.RemovedPointerFixup);
        Add(builder, plan.RewardValue);
        foreach (GreenWizardRoutePoint point in plan.RoutePoints)
        {
            Add(builder, point.X);
            Add(builder, point.Y);
            Add(builder, point.Z);
        }
        Add(builder, plan.PropertiesTargetPreimageSha256);
        Add(builder, plan.PropertiesInstalledSha256);
        Add(builder, plan.PointerFixupsTargetPreimageSha256);
        Add(builder, plan.PointerFixupsInstalledSha256);
        return Finish(builder);
    }

    private static void AddInstanceSchema(StringBuilder builder, GreenWizardInstanceSchema schema)
    {
        Add(builder, schema.SourceRecordBytes);
        Add(builder, schema.PropertiesBytes);
        Add(builder, schema.ActorIdLowOffset);
        Add(builder, schema.ActorIdHighOffset);
        Add(builder, schema.PodOrGroupOffset);
        Add(builder, schema.PodOrGroupValue);
        Add(builder, schema.YawOffset);
        Add(builder, schema.CullingSectorOffset);
        Add(builder, schema.RendererDistanceOffset);
        Add(builder, schema.RendererDistanceValue);
        Add(builder, schema.RewardOffset);
        Add(builder, schema.InternalRoutePointerOffset);
        Add(builder, schema.InternalRouteAnchorOffset);
        Add(builder, schema.FirstRoutePointOffset);
        Add(builder, schema.RoutePointStride);
        Add(builder, schema.RoutePointCount);
        Add(builder, schema.PreserveTargetYaw);
        Add(builder, schema.RecomputeTargetCullingSector);
        Add(builder, schema.PreserveTargetReward);
    }

    private static StringBuilder Begin(string format) => new(format);

    private static void Add<T>(StringBuilder builder, T value)
    {
        string text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        builder.Append('|');
        builder.Append(text.Length.ToString(CultureInfo.InvariantCulture));
        builder.Append(':');
        builder.Append(text.Trim().ToLowerInvariant());
    }

    private static string Finish(StringBuilder builder) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()))).ToLowerInvariant();
}

internal static class RuntimeBundleValidation
{
    public static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }

    public static void RequireText(string value, string label) =>
        Require(!string.IsNullOrWhiteSpace(value), $"{label} must not be empty.");

    public static void RequireSha256(string value, string label)
    {
        Require(
            value.Length == 64 && value.All(character => Uri.IsHexDigit(character)),
            $"{label} must be a 64-character SHA-256 value.");
    }
}
