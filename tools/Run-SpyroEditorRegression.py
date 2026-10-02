#!/usr/bin/env python3
"""Repeatable source-free regression gate; never launches the editor or emulator."""

import argparse
import json
import os
from pathlib import Path
import signal
import subprocess
import sys
import tempfile
import time


ROOT = Path(__file__).resolve().parent.parent
# Mandatory offline fake-RAM/transport suites, before any .NET build. These
# commands have no source image, emulator connection or input mode. Explicit
# discovery plus its frozen test-count marker must not accept an empty suite.
OBSERVER_PYTHON_CASES = (
    ("python-fresh-chest-observer", ("tools/Observe-SpyroFreshChest.py", "--self-test"),
     "FRESH_CHEST_OBSERVER_OFFLINE_OK tests=13 connections=0 romReads=0 runtime=false"),
    ("python-tiled-family-chest-observer",
     ("-m", "unittest", "discover", "-s", "tools/tests", "-p", "test_tiled_family_chest_observer.py", "-v"),
     "Ran 9 tests"),
    ("python-japanese-object-contracts", ("tools/Test-Inspect-SpyroJapaneseObjects.py", "-v"),
     "Ran 9 tests"),
    ("python-japanese-collectible-contracts", ("tools/test_japanese_collectibles.py", "-v"),
     "Ran 12 tests"),
    ("python-japanese-font-contracts", ("tools/test_japanese_font.py", "-v"),
     "Ran 7 tests"),
    ("python-japanese-sign-reference-verifier", ("tools/Test-Verify-SpyroJapaneseSignReference.py", "-v"),
     "Ran 12 tests"),
    ("python-usa-modal-texture-lifetime", ("tools/test_usa_modal_texture_lifetime.py", "-v"),
     "Ran 9 tests"),
    ("python-usa-save-buffer-ownership", ("tools/test_usa_save_buffer_ownership.py", "-v"),
     "Ran 10 tests"),
    ("python-usa-selected-save-admission", ("tools/test_usa_selected_save_admission.py", "-v"),
     "Ran 12 tests"),
    ("python-usa-card-read-completion", ("tools/test_usa_card_read_completion.py", "-v"),
     "Ran 19 tests"),
    ("python-usa-pete-lifetime", ("tools/test_usa_pete_lifetime.py", "-v"),
     "Ran 11 tests"),
    ("python-japanese-egg-observer", ("-m", "unittest", "discover", "-s", "tools/tests", "-p", "test_japanese_egg_observer.py", "-v"),
     "Ran 10 tests"),
    ("python-japanese-egg-authored-observer", ("-m", "unittest", "discover", "-s", "tools/tests", "-p", "test_japanese_egg_authored_observer.py", "-v"),
     "Ran 16 tests"),
    ("python-collectible-startup-observer", ("-m", "unittest", "discover", "-s", "tools/tests", "-p", "test_collectible_startup_observer.py", "-v"),
     "Ran 8 tests"),
    ("python-collectible-startup-physical", ("-m", "unittest", "discover", "-s", "tools/tests", "-p", "test_collectible_startup_physical.py", "-v"),
     "Ran 14 tests"),
    ("python-collectible-card-fixture", ("-m", "unittest", "discover", "-s", "tools/tests", "-p", "test_collectible_card_fixture.py", "-v"),
     "Ran 8 tests"),
    ("python-collectible-card-load-physical", ("-m", "unittest", "discover", "-s", "tools/tests", "-p", "test_collectible_card_load_physical.py", "-v"),
     "Ran 20 tests"),
    ("python-collectible-card-load-observer", ("-m", "unittest", "discover", "-s", "tools/tests", "-p", "test_collectible_card_load_observer.py", "-v"),
     "Ran 9 tests"),
    ("python-collectible-card-admission-physical", ("-m", "unittest", "discover", "-s", "tools/tests", "-p", "test_collectible_card_admission_physical.py", "-v"),
     "Ran 17 tests"),
    ("python-collectible-card-admission-observer", ("-m", "unittest", "discover", "-s", "tools/tests", "-p", "test_collectible_card_admission_observer.py", "-v"),
     "Ran 9 tests"),
    ("python-admitted-egg-lifecycle-physical", ("-m", "unittest", "discover", "-s", "tools/tests", "-p", "test_japanese_egg_admitted_lifecycle_physical.py", "-v"),
     "Ran 14 tests"),
    ("python-admitted-egg-lifecycle-observer", ("-m", "unittest", "discover", "-s", "tools/tests", "-p", "test_japanese_egg_admitted_lifecycle_observer.py", "-v"),
     "Ran 12 tests"),
    ("python-japanese-egg-physical-verifier", ("-m", "unittest", "discover", "-s", "tools/tests", "-p", "test_japanese_egg_research.py", "-v"),
     "Ran 29 tests"),
)
# Each entry was inspected for independence from disc images and local caches.
# Source-disc tests remain opt-in below; the existing broader source suite is
# Run-SpyroEditorOfflineQa.sh. Neither route mode launches the emulator.
CORE_CASES = (
    ("DiscImageWorkingCopySmoke", "Disc-image working-copy smoke passed."),
    ("DiscImageWriteIntegritySmoke", "DISC_IMAGE_WRITE_INTEGRITY_SMOKE_OK"),
    ("DiscImageReadBoundsSmoke", "DISC_IMAGE_READ_BOUNDS_SMOKE_OK"),
    ("DiscCueSheetSmoke", "DISC_CUE_SHEET_SMOKE_OK"),
    ("MobyTreasureExportSmoke", "MOBY_TREASURE_EXPORT_SMOKE_OK"),
    ("MobyWadReaderSmoke", "MOBY_WAD_READER_SMOKE_OK"),
    ("MobyPlanTransactionSmoke", "MOBY_PLAN_TRANSACTION_SMOKE_OK"),
    ("NativeMobyDependencyFactsSmoke", "NATIVE_MOBY_DEPENDENCY_FACTS_SMOKE_OK"),
    ("NativeMobyDependencyClosureSmoke", "NATIVE_MOBY_DEPENDENCY_CLOSURE_SMOKE_OK"),
    ("NativeMobyInventorySmoke", "NATIVE_MOBY_PORTABILITY_MATRIX_SMOKE_OK"),
    ("NativeRegionalSourceSmoke", "NATIVE_REGIONAL_SOURCE_FREE_OK"),
    ("JapaneseSignMessageSmoke", "JAPANESE_SIGN_MESSAGE_SOURCE_FREE_OK"),
    ("NativeSimpleMobyGeometrySmoke", "NATIVE_SIMPLE_MOBY_GEOMETRY_SOURCE_FREE_OK"),
    ("NativeJapaneseEggModelAssemblySmoke", "JP_EGG_MODEL_ASSEMBLY_SOURCE_FREE_OK"),
    ("DetachedCollectibleSaveSmoke", "DETACHED_COLLECTIBLE_SAVE_SOURCE_FREE_OK"),
    ("DetachedCollectibleCompletionSmoke", "DETACHED_COLLECTIBLE_COMPLETION_SOURCE_FREE_OK"),
    ("NativeCollectibleSelectedSlotResidenceSmoke", "COLLECTIBLE_SELECTED_SLOT_RESIDENCE_SOURCE_FREE_OK"),
    ("NativeCollectibleCollectionKernelSmoke", "NATIVE_COLLECTIBLE_STATE_QUERY_OK"),
    ("DetachedCollectibleLedgerImageSmoke", "DETACHED_COLLECTIBLE_LEDGER_IMAGE_OK"),
    ("NativeSha256Smoke", "NATIVE_SHA256_OK"),
    ("NativeCollectibleEnvelopeLoadSmoke", "NATIVE_COLLECTIBLE_ENVELOPE_LOAD_OK"),
    ("NativeCollectibleCardLoadSmoke", "NATIVE_COLLECTIBLE_CARD_LOAD_OK"),
    ("NativeCollectibleCardLoadResearchSmoke", "COLLECTIBLE_CARD_LOAD_RESEARCH_SOURCE_FREE_OK"),
    ("NativeCollectibleLiveAdmissionSmoke", "NATIVE_COLLECTIBLE_LIVE_ADMISSION_OK"),
    ("NativeCollectibleCardAdmissionResearchSmoke", "COLLECTIBLE_CARD_ADMISSION_RESEARCH_SOURCE_FREE_OK"),
    ("NativeCollectibleSaveTailSmoke", "NATIVE_COLLECTIBLE_SAVE_TAIL_OK"),
    ("NativeCollectibleSaveControllerSmoke", "NATIVE_COLLECTIBLE_SAVE_CONTROLLER_OK"),
    ("NativeCollectibleSaveWorkspaceSmoke", "COLLECTIBLE_SAVE_WORKSPACE_SOURCE_FREE_OK"),
    ("NativeJapaneseEggWorkspaceLifecycleSmoke", "NATIVE_JAPANESE_EGG_WORKSPACE_LIFECYCLE_OK"),
    ("NativeCollectibleSaveSchedulerSmoke", "NATIVE_COLLECTIBLE_SAVE_SCHEDULER_OK"),
    ("NativeJapaneseEggSchedulingSmoke", "JP_EGG_SCHEDULING_SOURCE_FREE_OK"),
    ("NativeCollectibleReservationMechanicsSmoke", "COLLECTIBLE_RESERVATION_MECHANICS_SOURCE_FREE_OK"),
    ("NativeCollectibleColdBootBootstrapSmoke", "COLLECTIBLE_COLD_BOOT_BOOTSTRAP_SOURCE_FREE_OK"),
    ("NativeCollectibleStartupResearchSmoke", "COLLECTIBLE_STARTUP_RESEARCH_SOURCE_FREE_OK"),
    ("NativeJapaneseEggCollectionAttemptSmoke", "JP_EGG_COLLECTION_ATTEMPT_SOURCE_FREE_OK"),
    ("NativeJapaneseEggLifecycleSmoke", "JP_EGG_LIFECYCLE_SOURCE_FREE_OK"),
    ("NativeJapaneseEggLifecycleOverlaySmoke", "JP_EGG_LIFECYCLE_OVERLAY_SOURCE_FREE_OK"),
    ("NativeJapaneseEggAdmittedLifecycleSmoke", "JP_EGG_ADMITTED_LIFECYCLE_SOURCE_FREE_OK"),
    ("NativeJapaneseEggAdmittedLifecycleResearchSmoke", "JP_EGG_ADMITTED_RESEARCH_SOURCE_FREE_OK"),
    ("NativeJapaneseEggVisualBehaviorSmoke", "JP_EGG_VISUAL_BEHAVIOR_SOURCE_FREE_OK"),
    ("NativeJapaneseEggArtisansIntegrationSmoke", "JP_EGG_ARTISANS_INTEGRATION_SOURCE_FREE_OK"),
    ("NativeJapaneseEggMemorySmoke", "JP_EGG_MEMORY_SOURCE_FREE_OK"),
    ("NativeJapaneseEggRenderAdmissionSmoke", "JP_EGG_RENDER_ADMISSION_SOURCE_FREE_OK"),
    ("NativeJapaneseEggRgbEditSmoke", "JP_EGG_RGB_EDIT_SOURCE_FREE_OK"),
    ("NativeJapaneseEggInstanceRootSmoke", "JP_EGG_INSTANCE_ROOT_SOURCE_FREE_OK"),
    ("NativeJapaneseEggRgbVariantTailSmoke", "JP_EGG_RGB_VARIANT_TAIL_SOURCE_FREE_OK"),
    ("NativeJapaneseEggPickupPredicateSmoke", "JP_EGG_PICKUP_PREDICATE_SOURCE_FREE_OK"),
    ("NativeJapaneseEggAuthoredClassesSmoke", "JP_EGG_AUTHORED_CLASSES_SOURCE_FREE_OK"),
    ("NativeJapaneseEggAuthoredMemorySmoke", "JP_EGG_AUTHORED_MEMORY_SOURCE_FREE_OK"),
    ("NativeJapaneseEggPlacementSmoke", "JP_EGG_PLACEMENT_SOURCE_FREE_OK"),
    ("NativeJapaneseSignFontResidentSmoke", "JP_SIGN_FONT_RESIDENT_SOURCE_FREE_OK"),
    ("NativeJapaneseSignModalHookSmoke", "JAPANESE_SIGN_MODAL_HOOK_SOURCE_FREE_OK"),
    ("NativeJapaneseEggResearchSmoke", "NATIVE_JP_EGG_RESEARCH_SOURCE_FREE_OK"),
    ("NativeJapaneseEggAuthoredResearchSmoke", "NATIVE_JP_EGG_AUTHORED_RESEARCH_SOURCE_FREE_OK"),
    ("NativeMobyPersistentOwnerSmoke", "NATIVE_MOBY_PERSISTENT_OWNER_SOURCE_FREE_OK"),
    ("NativeWizardAppendSmoke", "NATIVE_WIZARD_APPEND_SOURCE_FREE_OK"),
    ("NativeWizardAppendPlacementSmoke", "NATIVE_WIZARD_APPEND_PLACEMENT_FREE_OK"),
    ("NativeWizardCompositionSmoke", "NATIVE_WIZARD_COMPOSITION_FREE_OK"),
    ("NativeWizardFamilyAssetSmoke", "NATIVE_WIZARD_FAMILY_ASSET_FREE_OK"),
    ("NativeId65FreshWizardResourceSmoke", "FRESH_WIZARD_RESOURCE_FREE_OK"),
    ("NativeId65FreshWizardSceneSmoke", "FRESH_WIZARD_SCENE_FREE_OK"),
    ("NativeId65FreshWizardStagingSmoke", "FRESH_WIZARD_STAGING_FREE_OK"),
    ("NativeId65FreshWizardFallbackResourceSmoke", "FRESH_WIZARD_FALLBACK_RESOURCE_FREE_OK"),
    ("NativeId65FreshWizardHostSmoke", "FRESH_WIZARD_HOST_FREE_OK"),
    ("NativeId65FreshWizardBodySmoke", "FRESH_WIZARD_BODY_FREE_OK"),
    ("NativeId65FreshWizardExecutableSmoke", "FRESH_WIZARD_EXECUTABLE_FREE_OK"),
    ("NativeId65FreshWizardDiscSmoke", "FRESH_WIZARD_DISC_FREE_OK"),
    ("NativeId65FreshWizardResearchSmoke", "FRESH_WIZARD_RESEARCH_FREE_OK"),
    ("NativeFreshInitialSpawnClearanceSmoke", "INITIAL_SPAWN_CLEARANCE_FREE_OK"),
    ("NativeFreshWizardAuthoringSmoke", "FRESH_WIZARD_AUTHORING_FREE_OK"),
    ("NativePlayerAnimationSmoke", "NATIVE_PLAYER_ANIMATION_SOURCE_FREE_OK"),
    ("NativeGlobalSoundSmoke", "NATIVE_GLOBAL_SOUND_SOURCE_FREE_OK"),
    ("NativeId65EngineProfileSmoke", "NATIVE_ID65_ENGINE_PROFILE_SOURCE_FREE_OK"),
    ("NativeId65CompanionAssetSmoke", "NATIVE_ID65_COMPANION_ASSET_SOURCE_FREE_OK"),
    ("NativeId65AuthoredSurfaceSmoke", "NATIVE_ID65_AUTHORED_SURFACE_SMOKE_OK"),
    ("NativeId65FreshDocumentSmoke", "NATIVE_ID65_FRESH_DOCUMENT_SMOKE_OK"),
    ("NativeFreshLevelDocumentSmoke", "NATIVE_FRESH_LEVEL_DOCUMENT_SMOKE_OK"),
    ("NativeFreshPlacementArenaSmoke", "NATIVE_FRESH_PLACEMENT_ARENA_OK checks=2067"),
    ("UniversalTreasureSmoke", "UNIVERSAL_TREASURE_SOURCE_FREE_OK checks=2592"),
    ("UiSmoke", "FRESH_NATIVE_DOCUMENT_UI_OK"),
    ("NativeId65NormalResourceSmoke", "NATIVE_ID65_NORMAL_RESOURCE_SMOKE_OK"),
    ("NativeId65NormalBodySmoke", "NATIVE_ID65_NORMAL_BODY_SMOKE_OK"),
    ("NativeId65StaticTerrainTextureSmoke", "NATIVE_ID65_STATIC_TERRAIN_TEXTURE_FREE_OK"),
    ("NativeId65FreshTerrainTextureSmoke", "NATIVE_ID65_FRESH_TERRAIN_TEXTURE_FREE_OK"),
    ("NativeId65TexturedBodySmoke", "NATIVE_ID65_TEXTURED_BODY_FREE_OK"),
    ("NativeId65TexturedDiscSmoke", "NATIVE_ID65_TEXTURED_DISC_FREE_OK"),
    ("NativeId65TexturedResearchSmoke", "NATIVE_ID65_TEXTURED_RESEARCH_BASELINE_FREE_OK"),
    ("NativeFreshSavedMaterialSmoke", "NATIVE_FRESH_SAVED_MATERIAL_FREE_OK"),
    ("NativeFreshSavedLevelResearchSmoke", "NATIVE_FRESH_SAVED_LEVEL_RESEARCH_FREE_OK"),
    ("NativeFreshSavedLevelPublicSmoke", "SAVED_LEVEL_PUBLIC_FREE_OK"),
    ("NativeId65FreshDiscSmoke", "NATIVE_ID65_FRESH_DISC_SMOKE_OK"),
    ("NativeId65FreshDiscStreamSmoke", "NATIVE_ID65_FRESH_DISC_STREAM_SMOKE_OK"),
    ("NativeId65FreshDiscResearchSmoke", "NATIVE_ID65_FRESH_DISC_RESEARCH_SMOKE_OK"),
    ("NativeId65FreshEditedDocumentResearchSmoke", "NATIVE_ID65_FRESH_EDITED_DOCUMENT_RESEARCH_SMOKE_OK"),
    ("NativeId65FreshDocumentResearchSmoke", "NATIVE_ID65_FRESH_DOCUMENT_RESEARCH_SMOKE_OK"),
    ("NativeId65FreshDocumentPublicationSmoke", "NATIVE_ID65_FRESH_DOCUMENT_PUBLICATION_SMOKE_OK"),
    ("NativeWizardPlacementSmoke", "NATIVE_WIZARD_PLACEMENT_SMOKE_OK"),
    ("NativeWizardResearchSmoke", "NATIVE_WIZARD_RESEARCH_SOURCE_FREE_OK"),
    ("NativeTwoWizardResearchSmoke", "NATIVE_TWO_WIZARD_RESEARCH_FREE_OK"),
    ("NativeResourceCompositionSmoke", "NATIVE_RESOURCE_COMPOSITION_SOURCE_FREE_OK"),
    ("NativeFreeFlightSmoke", "NATIVE_FREE_FLIGHT_SOURCE_FREE_OK"),
    ("NativeFreeFlightResearchSmoke", "NATIVE_FREE_FLIGHT_RESEARCH_SOURCE_FREE_OK"),
    ("FreeFlightSettingsSmoke", "FREE_FLIGHT_SETTINGS_SMOKE_OK"),
    ("NativeGreenWizardMoverSmoke", "NATIVE_GREEN_WIZARD_MOVER_SMOKE_OK"),
    ("NativeLevelSlotOwnershipSmoke", "NATIVE_LEVEL_SLOT_OWNERSHIP_SMOKE_OK"),
    ("NativeCollisionIndexSmoke", "NATIVE_COLLISION_INDEX_SMOKE_OK"),
    ("NativeRelocationMapSmoke", "NATIVE_RELOCATION_MAP_SMOKE_OK"),
    ("WadGrowthGapSmoke", "WAD_GROWTH_GAP_SMOKE_OK"),
    ("FixedTailPrivateRecordSmoke", "FIXED_TAIL_IMAGE_PROOF_CASES_OK"),
    ("Id65AuthoringMobyDependencyBundleCompilerSmoke", "NATIVE_SCENE_PROPERTIES_LINKER_SMOKE_OK"),
    ("Id65AuthoringRewardStagingRuntimeCandidatePublisherSmoke", "V3_FIXTURE_LOOKUP_SOURCE_FREE_OK"),
    ("CombinedExportPublicationSmoke", "COMBINED_EXPORT_PUBLICATION_SMOKE_OK"),
    ("DiagnosticsBundleSmoke", "DIAGNOSTICS_BUNDLE_SMOKE_OK"),
    ("UnusedLevel65MusicOwnershipClosureSmoke", "ID65_MUSIC_SELECTION_COMPILER_SMOKE_OK"),
    ("Id65AuthoringMusicBuildInputSmoke", "ID65_AUTHORING_MUSIC_BUILD_INPUT_SMOKE_OK"),
    ("Id65AuthoredCollisionMusicSmoke", "ID65_AUTHORED_COLLISION_MUSIC_SMOKE_OK"),
    ("StructuralCompositionSmoke", "STRUCTURAL_COMPOSITION_SMOKE_OK"),
    ("CrossLevelCandidateFingerprintSmoke", "CROSS_LEVEL_CANDIDATE_FINGERPRINT_SMOKE_OK"),
    ("TextureHistoryWorkspaceSmoke", "TEXTURE_HISTORY_WORKSPACE_SMOKE_OK"),
    ("FarLodTextureCompanionSmoke", "Far-LOD texture companion smoke passed."),
    ("TerrainRelocationStoreSmoke", "Native terrain texture relocation edit store smoke: PASSED"),
    ("PersistenceSmoke", "PASS: external project layout"),
    ("GteProjectionSmoke", "PS1 GTE RTPS/RTPT smoke passed."),
    ("NativeTerrainBaseProjectionSmoke", "Native terrain base-projection smoke passed."),
    ("UpdateSmoke", "PASS: whole-number and incremental prerelease discovery"),
    ("UniversalMobySmoke", "UNIVERSAL_MOBY_SOURCE_FREE_OK"),
    ("UniversalDependencySmoke", "UNIVERSAL_DEPENDENCY_CHECKS_OK"),
    ("UniversalSpawnProducerSmoke", "UNIVERSAL_SPAWN_PRODUCER_CHECKS_OK"),
    ("UniversalGemCollectionSmoke", "UNIVERSAL_GEM_ADAPTER_FREE_OK"),
    ("UniversalHighValueGemSmoke", "HIGH_VALUE_GEM_FREE_OK"),
    ("UniversalClassOwnershipSmoke", "UNIVERSAL_CLASS_OWNERSHIP_FREE_OK"),
    ("UniversalModelTailSmoke", "UNIVERSAL_MODEL_TAIL_FREE_OK"),
    ("UniversalParticleNoOpSmoke", "UNIVERSAL_PARTICLE_NOOP_SOURCE_FREE_OK"),
    ("UniversalPlayerDamageSmoke", "UNIVERSAL_PLAYER_DAMAGE_SMOKE_OK"),
    ("UniversalPlayerDamageWritesSmoke", "UNIVERSAL_PLAYER_DAMAGE_WRITES_OK"),
    ("UniversalRowRemovalSmoke", "UNIVERSAL_ROW_REMOVAL_CHECKS_OK"),
    ("UniversalCapacityPlanSmoke", "UNIVERSAL_CAPACITY_PLAN_OK"),
    ("UniversalIdentitySmoke", "UNIVERSAL_IDENTITY_OK"),
    ("NativeMobySaveMapSmoke", "NATIVE_MOBY_SAVE_MAP_SOURCE_FREE_OK"),
    ("UniversalJumpModelOwnershipSmoke", "UNIVERSAL_JUMP_MODEL_FREE_OK"),
    ("NativePortalRouteRuntimeSmoke", "NATIVE_PORTAL_ROUTE_FREE_OK"),
    ("StartBlankSmoke", "START_BLANK_SOURCE_FREE_OK checks=32"),
    ("NativeClearedRetailBindingSmoke", "NATIVE_CLEARED_RETAIL_BINDING_SMOKE_OK"),
    ("NativeClearedTitleDemosSmoke", "NATIVE_CLEARED_TITLE_DEMOS_SMOKE_OK"),
    ("NativeAuthoredAggregateOwnershipSmoke", "NATIVE_AUTHORED_AGGREGATE_OWNERSHIP_CHECKS_OK checks=32"),
    ("NativeClearedLevelCompositionSmoke", "NATIVE_CLEARED_LEVEL_COMPOSITION_CHECKS_OK checks="),
)
# Some source-aware executables expose an explicitly isolated source-free mode.
# Never execute their fixture/materializer path as part of the default gate.
CASE_ARGUMENTS = {
    # This exact mode dispatches before workspace/cache/source initialization.
    # Do not invoke the broad, source-aware UI fixture as a default regression.
    "UiSmoke": ["--fresh-native-document-only"],
    "FixedTailPrivateRecordSmoke": ["--source-free-image-proof"],
    "Id65AuthoringMobyDependencyBundleCompilerSmoke": ["--source-free-properties-linker"],
    # Bind the compiled fixture only. Never invoke the legacy publisher's
    # default mode, which constructs fixtures and mutates retained inputs.
    "Id65AuthoringRewardStagingRuntimeCandidatePublisherSmoke": ["--source-free-fixture-lookup"],
    "UnusedLevel65MusicOwnershipClosureSmoke": ["--source-free-music-selection"],
}
# Additional modes retain their own arguments/markers without replacing the
# established mode of the same executable. Project builds remain deduplicated.
ADDITIONAL_CORE_CASES = (
    ("UiSmoke", "HIGH_VALUE_GEM_UI_OK checks=", ["--high-value-gems-ui-only"]),
    ("UiSmoke", "PORTAL_ROUTE_UI_OK checks=18",
     ["--portal-routes-ui-only"]),
    ("UiSmoke", "START_BLANK_UI_OK checks=27",
     ["--start-blank-ui-only"]),
    ("TownSquareLockedChestSmoke", "LOCKED_CHEST_BEHAVIOR_FREE_OK",
     ["--behavior-free"]),
    ("UiSmoke", "FREE_FLIGHT_UI_OK",
     ["--free-flight-ui-only"]),
    ("UiSmoke", "LEVEL_PICKER_AUTOMATION_OK",
     ["--level-picker-automation-only"]),
    ("UiSmoke", "AUTHORED_MESH_CHECKS_OK",
     ["--authored-mesh-only"]),
    ("Id65AuthoringMobyDependencyBundleCompilerSmoke", "NATIVE_ROUTE_SURFACE_SMOKE_OK",
     ["--source-free-route-surfaces"]),
    ("NativeId65FreshDocumentSmoke", "NATIVE_ID65_VARIABLE_DATA_FREE_OK",
     ["--variable-data-free"]),
    ("NativeId65FreshDocumentSmoke", "NATIVE_ID65_TILED_RESOURCE_FREE_OK",
     ["--tiled-resource-free"]),
    ("NativeId65FreshDocumentSmoke", "NATIVE_ID65_TILED_COMPILATION_FREE_OK",
     ["--tiled-compilation-free"]),
    ("NativeId65FreshDocumentSmoke", "NATIVE_ID65_TILED_MATERIAL_FREE_OK",
     ["--tiled-material-free"]),
    ("NativeId65FreshDocumentSmoke", "NATIVE_ID65_TILED_MATERIAL_COMPILATION_FREE_OK",
     ["--tiled-material-compilation-free"]),
    ("NativeId65FreshDocumentSmoke", "NATIVE_ID65_SELECTED_TILED_MATERIAL_FREE_OK",
     ["--selected-tiled-material-free"]),
    ("NativeFreshSavedLevelResearchSmoke", "SELECTED_TILED_MATERIAL_SAVED_FREE_OK",
     ["--selected-tiled-material-saved-free"]),
    ("UiSmoke", "FRESH_LEVEL_DOCUMENT_SESSION_OK",
     ["--fresh-native-level-session-only"]),
    ("UiSmoke", "FRESH_NATIVE_LEVEL_UI_OK",
     ["--fresh-native-level-ui-only"]),
    ("UiSmoke", "FRESH_MATERIAL_PICKER_UI_OK",
     ["--fresh-material-picker-only"]),
    ("UiSmoke", "FRESH_MATERIAL_INSPECTION_WORKER_FREE_OK",
     ["--fresh-material-inspection-worker-only"]),
    ("UiSmoke", "FRESH_MATERIAL_INSPECTION_UI_OK",
     ["--fresh-material-inspection-only"]),
    ("UiSmoke", "FRESH_SAVED_RESEARCH_UI_OK",
     ["--fresh-saved-research-ui"]),
)
SOURCE_QA_CASES = (
    ("NativeCollectibleCardLoadResearchSmoke", "COLLECTIBLE_CARD_LOAD_RESEARCH_SOURCE_OK", ["--source"]),
    ("NativeCollectibleCardAdmissionResearchSmoke", "COLLECTIBLE_CARD_ADMISSION_RESEARCH_SOURCE_OK", ["--source"]),
    ("NativeCollectibleSaveTailSmoke", "NATIVE_COLLECTIBLE_SAVE_TAIL_SOURCE_OK", ["--source"]),
    ("NativeCollectibleSaveControllerSmoke", "NATIVE_COLLECTIBLE_SAVE_CONTROLLER_SOURCE_OK", ["--source"]),
    ("NativeCollectibleStartupResearchSmoke", "COLLECTIBLE_STARTUP_RESEARCH_SOURCE_OK", ["--source"]),
    ("NativeCollectibleColdBootBootstrapSmoke", "COLLECTIBLE_COLD_BOOT_BOOTSTRAP_SOURCE_OK", ["--source"]),
    ("NativeCollectibleReservationMechanicsSmoke", "COLLECTIBLE_RESERVATION_MECHANICS_SOURCE_OK", ["--source"]),
    ("NativeCollectibleSelectedSlotResidenceSmoke", "COLLECTIBLE_SELECTED_SLOT_RESIDENCE_SOURCE_OK", ["--source"]),
    ("NativeSimpleMobyGeometrySmoke", "NATIVE_SIMPLE_MOBY_GEOMETRY_DISC_OK", ["--source"]),
    # This read-only mode checks both preserved V2 and V3 native closure goldens;
    # publication/fault modes must never be substituted here.
    ("NativeMobyDependencyClosureSmoke", "NATIVE_TOWNSQUARE_V3_OCCUPIED_SITE_REFUSED_AND_COORDINATE_ONLY_SOURCE_OK",
     ["--source-v3"]),
    ("Id65AuthoringMobyDependencyBundleCompilerSmoke", "NATIVE_GREEN_WIZARD_ROUTE_SOURCE_SMOKE_OK",
     ["--source-backed-route-surfaces"]),
    ("NativeMobyInventorySmoke", "NATIVE_MOBY_PORTABILITY_MATRIX_SOURCE_OK",
     ["--source"]),
    ("NativeMobyPersistentOwnerSmoke", "NATIVE_MOBY_PERSISTENT_OWNER_SOURCE_OK", ["--source"]),
    ("NativeWizardAppendSmoke", "NATIVE_WIZARD_APPEND_SOURCE_OK", ["--source"]),
    ("NativeWizardAppendPlacementSmoke", "NATIVE_WIZARD_APPEND_PLACEMENT_SOURCE_OK", ["--source"]),
    ("NativeWizardCompositionSmoke", "NATIVE_WIZARD_COMPOSITION_SOURCE_OK", ["--source"]),
    ("NativeWizardFamilyAssetSmoke", "NATIVE_WIZARD_FAMILY_ASSET_SOURCE_OK", ["--source"]),
    ("NativeId65FreshWizardResourceSmoke", "FRESH_WIZARD_RESOURCE_SOURCE_OK", ["--source"]),
    ("NativeId65FreshWizardSceneSmoke", "FRESH_WIZARD_SCENE_SOURCE_OK", ["--source"]),
    ("NativeId65FreshWizardStagingSmoke", "FRESH_WIZARD_STAGING_SOURCE_OK", ["--source"]),
    ("NativeId65FreshWizardFallbackResourceSmoke", "FRESH_WIZARD_FALLBACK_RESOURCE_SOURCE_OK", ["--source"]),
    ("NativeId65FreshWizardHostSmoke", "FRESH_WIZARD_HOST_SOURCE_OK", ["--source"]),
    ("NativeId65FreshWizardBodySmoke", "FRESH_WIZARD_BODY_SOURCE_OK", ["--source"]),
    ("NativeId65FreshWizardExecutableSmoke", "FRESH_WIZARD_EXECUTABLE_SOURCE_OK", ["--source"]),
    ("NativeId65FreshWizardDiscSmoke", "FRESH_WIZARD_DISC_SOURCE_OK", ["--source"]),
    ("NativeId65FreshWizardResearchSmoke", "FRESH_WIZARD_RESEARCH_SOURCE_OK", ["--source"]),
    ("NativeFreshInitialSpawnClearanceSmoke", "INITIAL_SPAWN_CLEARANCE_SOURCE_OK", ["--source"]),
    ("NativeFreshWizardAuthoringSmoke", "FRESH_WIZARD_AUTHORING_SOURCE_OK", ["--source"]),
    ("NativeGreenWizardMoverSmoke", "NATIVE_GREEN_WIZARD_MOVER_SOURCE_SMOKE_OK", []),
    ("NativeLevelSlotOwnershipSmoke", "NATIVE_LEVEL_SLOT_OWNERSHIP_SOURCE_OK", ["--source"]),
    ("NativePlayerAnimationSmoke", "NATIVE_PLAYER_ANIMATION_SOURCE_OK", ["--source"]),
    ("NativeGlobalSoundSmoke", "NATIVE_GLOBAL_SOUND_SOURCE_OK", ["--source"]),
    ("NativeId65EngineProfileSmoke", "NATIVE_ID65_ENGINE_PROFILE_SOURCE_OK", []),
    ("NativeId65CompanionAssetSmoke", "NATIVE_ID65_COMPANION_ASSET_SOURCE_OK", []),
    ("NativeId65AuthoredSurfaceSmoke", "NATIVE_ID65_AUTHORED_SURFACE_SOURCE_OK", []),
    ("NativeId65NormalResourceSmoke", "NATIVE_ID65_NORMAL_RESOURCE_SOURCE_OK", []),
    ("NativeId65NormalBodySmoke", "NATIVE_ID65_NORMAL_BODY_SOURCE_OK", []),
    ("NativeId65StaticTerrainTextureSmoke", "NATIVE_ID65_STATIC_TERRAIN_TEXTURE_SOURCE_OK", ["--source"]),
    # Explicit read-only App worker; no Window, cache, project or publication.
    ("UiSmoke", "FRESH_MATERIAL_INSPECTION_WORKER_SOURCE_OK", ["--fresh-material-inspection-worker-source"]),
    # Explicit headless UI uses retained tiny projects, never a native window or output.
    ("UiSmoke", "FRESH_MATERIAL_INSPECTION_SOURCE_UI_OK", ["--fresh-material-inspection-source"]),
    ("NativeId65FreshTerrainTextureSmoke", "NATIVE_ID65_FRESH_TERRAIN_TEXTURE_SOURCE_OK", ["--source"]),
    ("NativeId65TexturedBodySmoke", "NATIVE_ID65_TEXTURED_BODY_SOURCE_OK", ["--source"]),
    ("NativeId65TexturedDiscSmoke", "NATIVE_ID65_TEXTURED_DISC_SOURCE_OK", ["--source"]),
    ("NativeId65TexturedResearchSmoke", "NATIVE_ID65_TEXTURED_RESEARCH_BASELINE_SOURCE_OK", ["--source"]),
    ("NativeFreshSavedMaterialSmoke", "NATIVE_FRESH_SAVED_MATERIAL_SOURCE_OK", ["--source"]),
    ("NativeFreshSavedLevelResearchSmoke", "NATIVE_FRESH_SAVED_LEVEL_RESEARCH_SOURCE_OK", ["--source"]),
    # Complete saved-family source/receipt checks only; no physical/fault mode.
    ("NativeFreshSavedLevelResearchSmoke", "NATIVE_FRESH_SAVED_CHEST_SOURCE_FINAL_OK", ["--locked-chest-source"]),
    ("NativeFreshSavedLevelResearchSmoke", "NATIVE_FRESH_SAVED_TILED_FAMILY_CANDIDATE_FINAL_OK", ["--tiled-family-candidate-source"]),
    ("NativeFreshSavedLevelPublicSmoke", "SAVED_LEVEL_PUBLIC_SOURCE_OK", ["--source"]),
    ("NativeId65FreshDiscSmoke", "NATIVE_ID65_FRESH_DISC_SOURCE_OK", []),
    ("NativeId65FreshDiscStreamSmoke", "NATIVE_ID65_FRESH_DISC_STREAM_SOURCE_OK", []),
    # Capture checks only; never stage faults, exercise publication or verify outputs here.
    ("NativeId65FreshDiscResearchSmoke", "NATIVE_ID65_FRESH_DISC_RESEARCH_SOURCE_OK", ["--source"]),
    ("NativeId65FreshEditedDocumentResearchSmoke", "NATIVE_ID65_FRESH_EDITED_DOCUMENT_RESEARCH_SOURCE_OK", ["--source"]),
    ("NativeId65FreshDocumentResearchSmoke", "NATIVE_ID65_FRESH_DOCUMENT_RESEARCH_SOURCE_OK", ["--source"]),
    ("NativeId65FreshDocumentPublicationSmoke", "NATIVE_ID65_FRESH_DOCUMENT_PUBLICATION_SOURCE_OK", ["--source"]),
    ("NativeWizardPlacementSmoke", "NATIVE_WIZARD_PLACEMENT_SOURCE_OK", []),
    # Capture/readback only; never stage, publish or open an emulator here.
    ("NativeWizardResearchSmoke", "NATIVE_WIZARD_RESEARCH_SOURCE_OK", []),
    # Exact two-owner plan/source checks only; never --exercise-staging or publication.
    ("NativeTwoWizardResearchSmoke", "NATIVE_TWO_WIZARD_RESEARCH_SOURCE_OK", ["--source"]),
    ("NativeResourceCompositionSmoke", "NATIVE_RESOURCE_COMPOSITION_SOURCE_OK", ["--source"]),
    ("NativeFreeFlightSmoke", "NATIVE_FREE_FLIGHT_SOURCE_OK", []),
    # Plan capture only: never --exercise-staging, --verify or publication.
    ("NativeFreeFlightResearchSmoke", "NATIVE_FREE_FLIGHT_RESEARCH_SOURCE_OK", []),
)
# These source checks additionally verify the current workspace's catalog against
# native ownership. Resolve it only in the opted-in, deadline-bound command.
SOURCE_QA_WORKSPACE_CASES = frozenset({"NativeMobyInventorySmoke", "NativeMobyPersistentOwnerSmoke", "NativeWizardAppendSmoke", "NativeWizardAppendPlacementSmoke", "NativeWizardCompositionSmoke", "NativeFreeFlightResearchSmoke", "NativeWizardResearchSmoke", "NativeTwoWizardResearchSmoke"})
SOURCE_QA_CUE_CASES = frozenset({"NativeCollectibleCardAdmissionResearchSmoke", "NativeCollectibleCardLoadResearchSmoke", "NativeCollectibleStartupResearchSmoke", "NativeFreeFlightResearchSmoke", "NativeWizardResearchSmoke", "NativeId65FreshDiscResearchSmoke", "NativeId65FreshEditedDocumentResearchSmoke", "NativeId65FreshDocumentResearchSmoke", "NativeId65FreshDocumentPublicationSmoke", "NativeTwoWizardResearchSmoke", "NativeId65FreshWizardResearchSmoke", "NativeFreshWizardAuthoringSmoke", "NativeId65TexturedResearchSmoke", "NativeFreshSavedLevelResearchSmoke", "NativeFreshSavedLevelPublicSmoke", "UiSmoke"})
# These raw-source checks have no project/build or default invocation. Each
# accepts only the selected original BIN and shares the source-suite deadline.
SOURCE_PYTHON_CASES = (
    ("fresh-wizard-audio-audit", "src/Spyro.Editor.NativeWizardFamilyAssetSmoke/source_fresh_wizard_audio_audit.py",
     "FRESH_WIZARD_GLOBAL1F_SOURCE_AUDIT_OK"),
    ("fresh-wizard-deferred-damage", "src/Spyro.Editor.NativeWizardFamilyAssetSmoke/source_fresh_wizard_deferred_damage_oracle.py",
     "FRESH_WIZARD_DEFERRED_DAMAGE_ORACLE_OK"),
    ("fresh-wizard-collider-lifetime", "src/Spyro.Editor.NativeWizardFamilyAssetSmoke/source_fresh_wizard_collider_lifetime_oracle.py",
     "NATIVE_COLLIDER_LIFETIME_SLICES_OK"),
    ("fresh-wizard-p1-scene", "src/Spyro.Editor.NativeId65FreshWizardSceneSmoke/source_p1_scene_oracle.py",
     "FRESH_P1_SCENE_NATIVE_SLICES_OK"),
    ("fresh-wizard-staging", "src/Spyro.Editor.NativeId65FreshWizardStagingSmoke/source_fresh_wizard_staging_oracle.py",
     "FRESH_WIZARD_STAGING_RAW_ORACLE_OK"),
    ("fresh-wizard-fallback-resources", "src/Spyro.Editor.NativeId65FreshWizardFallbackResourceSmoke/source_fallback_resource_oracle.py",
     "SOURCE_TOWN_FALLBACK_ASSETS_OK"),
    ("fresh-textured-disc", "src/Spyro.Editor.NativeId65TexturedDiscSmoke/source_textured_disc_oracle.py",
     "NATIVE_ID65_TEXTURED_DISC_RAW_ORACLE_OK"),
)


def configured_source_image():
    """Read the same local source selection as offline QA, only when opted in."""
    return configured_source_path("imagePath", "BIN")


def configured_source_cue():
    """Use the explicitly selected CUE; never infer a sibling or track layout."""
    return configured_source_path("cuePath", "CUE")


def configured_source_path(key, label):
    settings = ROOT / "_local/settings/source-disc.json"
    data = json.loads(settings.read_text(encoding="utf-8"))
    image = data.get(key) if isinstance(data, dict) else None
    if not isinstance(image, str) or not image.strip():
        raise ValueError(f"{settings} does not contain a non-empty {key}.")
    path = Path(image)
    if not path.is_absolute():
        path = ROOT / path
    if not path.is_file():
        raise FileNotFoundError(f"Configured source {label} is missing: {path}")
    return str(path)


def run_owned_command(command, *, cwd, stdout, timeout, termination_grace=1.0):
    """Run one POSIX command in an owned, timeout-controlled process group.

    A timeout remains a failure even if TERM allows a clean child exit. Forced
    termination cannot promise .NET finally blocks or shell cleanup completed;
    interrupted exports/backup directories may require manual recovery. This
    helper never deletes output artifacts. Windows tree termination is not yet
    implemented or tested, and is rejected explicitly rather than orphaning work.
    """
    if os.name != "posix":
        raise OSError("The regression runner requires POSIX process-group termination; Windows is not supported yet.")
    if timeout <= 0 or termination_grace < 0:
        raise ValueError("Timeout must be positive and termination grace nonnegative.")
    process = subprocess.Popen(command, cwd=cwd, stdout=stdout, stderr=subprocess.STDOUT,
                               start_new_session=True)
    try:
        returncode = process.wait(timeout=timeout)
    except subprocess.TimeoutExpired as expired:
        try:
            _stop_owned_process_group(process, termination_grace)
        except OSError as cleanup:
            # A denied final signal can otherwise hide the deadline failure and
            # accidentally imply that all descendants stopped. Retain both facts.
            raise OSError(f"timeout={timeout}s; owned process group {process.pid} termination unverified: "
                          f"{cleanup}; retain interrupted artifacts and inspect the process group") from expired
        raise
    except KeyboardInterrupt as interrupted:
        if process.returncode is None:
            try:
                _stop_owned_process_group(process, termination_grace)
            except OSError as cleanup:
                interrupted.add_note(f"Owned process group {process.pid} termination unverified: {cleanup}")
        raise
    return subprocess.CompletedProcess(command, returncode)


def _stop_owned_process_group(process, termination_grace):
    # Do not reap the group leader before the final signal. Its reserved PID
    # prevents accidental reuse of this owned PGID while descendants shut down.
    failures = []

    def signal_group(value):
        try:
            os.killpg(process.pid, value)
        except ProcessLookupError:
            pass
        except OSError as error:
            failures.append(f"{value.name}: {error}")

    try:
        signal_group(signal.SIGTERM)
        time.sleep(termination_grace)
    finally:
        signal_group(signal.SIGKILL)
        try:
            # Never wait forever when signaling was denied. The still-reserved
            # leader PID protects the final signal from PGID reuse as before.
            process.wait(timeout=max(1.0, termination_grace))
        except subprocess.TimeoutExpired:
            failures.append("leader did not exit within the bounded cleanup wait")
        if failures:
            raise OSError("; ".join(failures))


def parse_options(arguments=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--build-all", action="store_true",
                        help="Build every project, including research tools, before regressions.")
    parser.add_argument("--with-source-qa", action="store_true",
                        help="Also run source-aware offline QA and native discovery/route audits using this checkout's configured disc/cache; offline QA creates disposable readbacks and local reports.")
    parser.add_argument("--configuration", choices=("Debug", "Release"), default="Release")
    parser.add_argument("--timeout", type=int, default=600,
                        help="Maximum seconds for each build or source-free test (default: 600).")
    parser.add_argument("--source-qa-timeout", type=int, default=1800,
                        help="Maximum seconds for the entire optional source-aware QA suite, not each nested test (default: 1800).")
    parser.add_argument("--list", action="store_true", help="List selected cases without building.")
    options = parser.parse_args(arguments)
    if options.timeout <= 0:
        parser.error("--timeout must be positive")
    if options.source_qa_timeout <= 0:
        parser.error("--source-qa-timeout must be positive")
    return options


def main():
    options = parse_options()
    cases = [(ROOT / "src" / ("Spyro.Editor." + name) /
              ("Spyro.Editor." + name + ".csproj"), marker, CASE_ARGUMENTS.get(name, []))
             for name, marker in CORE_CASES]
    cases.extend((ROOT / "src" / ("Spyro.Editor." + name) /
                  ("Spyro.Editor." + name + ".csproj"), marker, arguments)
                 for name, marker, arguments in ADDITIONAL_CORE_CASES)
    if options.list:
        for project, marker, arguments in cases:
            print(f"{project.relative_to(ROOT)} {' '.join(arguments)} | {marker}")
        return 0

    # Per-run logs are preserved outside the checkout, including on a failure.
    output = Path(tempfile.mkdtemp(prefix="spyro-regression-"))
    print(f"Regression logs: {output}", flush=True)
    results = []

    def execute(name, command, marker=None, timeout=None, deadline=None):
        command_timeout = options.timeout if timeout is None else timeout
        started = time.monotonic()
        log = output / f"{len(results):03d}-{name}.log"
        passed = False
        detail = ""
        resolved_command = None
        with log.open("w", encoding="utf-8") as stream:
            try:
                # Deferred source arguments are resolved inside the logged
                # failure boundary, never during a default or --list run.
                if deadline is not None and time.monotonic() >= deadline:
                    raise TimeoutError("Source QA deadline exhausted; no command was started.")
                resolved_command = command() if callable(command) else command
                stream.write(json.dumps(resolved_command) + "\n")
                stream.flush()
                if deadline is not None:
                    command_timeout = min(command_timeout, max(0, deadline - time.monotonic()))
                if command_timeout <= 0:
                    raise TimeoutError("Source QA deadline exhausted; no command was started.")
                process = run_owned_command(resolved_command, cwd=ROOT, stdout=stream,
                                            timeout=command_timeout)
                passed = process.returncode == 0
                detail = f"exit={process.returncode}"
            except subprocess.TimeoutExpired:
                detail = f"timeout={command_timeout}s; owned process group stopped; interrupted output cleanup may require recovery"
            except (OSError, ValueError) as error:
                detail = str(error)
                stream.write(detail + "\n")
        contents = log.read_text(encoding="utf-8", errors="replace")
        if passed and marker is not None and marker not in contents:
            passed = False
            detail = "missing success marker"
        elapsed = round(time.monotonic() - started, 2)
        results.append(dict(name=name, passed=passed, detail=detail, seconds=elapsed,
                            log=str(log), command=resolved_command))
        print(f"{'PASS' if passed else 'FAIL'} {name} ({elapsed}s; {detail})", flush=True)
        if not passed:
            print("\n".join(contents.splitlines()[-20:]), flush=True)
        (output / "summary.json").write_text(
            json.dumps(results, indent=2) + "\n", encoding="utf-8")
        return passed

    if not execute("python-runner-tests",
                   [sys.executable, "-m", "unittest", "discover", "-s",
                    str(ROOT / "tools/tests"), "-p", "test_spyro_regression_runner.py", "-v"]):
        print("Process-runner regression failed; no project builds were started.", flush=True)
        return 1

    for name, arguments, marker in OBSERVER_PYTHON_CASES:
        if not execute(name, [sys.executable, "-B", *arguments], marker):
            print("Offline observer regression failed; no project builds were started.", flush=True)
            return 1

    app = ROOT / "src/Spyro.Editor.App/Spyro.Editor.App.csproj"
    required_projects = [app] + [project for project, _, _ in cases]
    if options.with_source_qa:
        required_projects.extend(ROOT / "src" / ("Spyro.Editor." + name) /
                                 ("Spyro.Editor." + name + ".csproj")
                                 for name, _, _ in SOURCE_QA_CASES)
    # Registered regressions remain mandatory when their project was deleted or
    # renamed. A filesystem-only glob would silently skip them and report green.
    projects = (sorted(set(ROOT.glob("src/**/*.csproj")) | set(required_projects))
                if options.build_all else required_projects)
    # Never race MSBuild writers against the shared Core/App output directories.
    build_results = {}
    for project in dict.fromkeys(projects):
        build_results[project] = execute(
            "build-" + project.stem,
            ["dotnet", "build", str(project), "--configuration", options.configuration,
             "--nologo", "--verbosity", "minimal"])
    for project, marker, arguments in cases:
        if not build_results.get(project, False):
            print(f"SKIP {project.stem}: build failed", flush=True)
            continue
        suffix = {
            ("--behavior-free",): "-behavior",
            ("--portal-routes-ui-only",): "-portal-routes-ui",
            ("--high-value-gems-ui-only",): "-high-value-gems-ui",
            ("--start-blank-ui-only",): "-start-blank-ui",
            ("--free-flight-ui-only",): "-free-flight-ui",
            ("--level-picker-automation-only",): "-level-picker-automation",
            ("--authored-mesh-only",): "-authored-mesh",
            ("--source-free-route-surfaces",): "-route-surfaces",
            ("--variable-data-free",): "-variable-data",
            ("--tiled-resource-free",): "-tiled-resource",
            ("--tiled-compilation-free",): "-tiled-compilation",
            ("--tiled-material-free",): "-tiled-material",
            ("--tiled-material-compilation-free",): "-tiled-material-compilation",
            ("--selected-tiled-material-free",): "-selected-tiled-material",
            ("--selected-tiled-material-saved-free",): "-selected-tiled-material-saved",
            ("--fresh-native-level-session-only",): "-fresh-level-session",
            ("--fresh-native-level-ui-only",): "-fresh-level-ui",
            ("--fresh-material-picker-only",): "-fresh-material-picker",
            ("--fresh-material-inspection-worker-only",): "-fresh-material-inspection-worker",
            ("--fresh-material-inspection-only",): "-fresh-material-inspection",
            ("--fresh-saved-research-ui",): "-fresh-saved-research",
        }.get(tuple(arguments), "")
        execute("run-" + project.stem + suffix,
                ["dotnet", "run", "--project", str(project), "--configuration",
                 options.configuration, "--no-build", "--no-restore"] +
                (["--"] + arguments if arguments else []), marker)
    identity_project = ROOT / "src/Spyro.Editor.UniversalIdentitySmoke/Spyro.Editor.UniversalIdentitySmoke.csproj"
    if identity_project in build_results:
        if build_results[identity_project]:
            execute("python-required-egg-native-words", [sys.executable, "-B",
                str(ROOT / "tools/Test-UniversalEggProgress.py"), "--dll",
                str(identity_project.parent / "bin" / options.configuration / "net10.0" / "Spyro.Editor.UniversalIdentitySmoke.dll")],
                "NATIVE_REQUIRED_EGG_PERCENT_SCALAR_OK")

    if build_results.get(identity_project, False):
        execute("universal-egg-update-scalar", [sys.executable,
                str(ROOT / "tools/Test-UniversalEggUpdate.py"), "--dll",
                str(ROOT / "src/Spyro.Editor.UniversalIdentitySmoke/bin" / options.configuration /
                    "net10.0/Spyro.Editor.UniversalIdentitySmoke.dll")],
                "UNIVERSAL_EGG_UPDATE_SCALAR_OK")

    map_project = ROOT / "src/Spyro.Editor.NativeMobySaveMapSmoke/Spyro.Editor.NativeMobySaveMapSmoke.csproj"
    if build_results.get(map_project, False):
        execute("native-moby-save-map-scalar", [sys.executable, "-B",
                str(ROOT / "tools/Test-NativeMobySaveMap.py"), "--dll",
                str(map_project.parent / "bin" / options.configuration / "net10.0" /
                    "Spyro.Editor.NativeMobySaveMapSmoke.dll")],
                "NATIVE_MOBY_SAVE_MAP_SCALAR_OK")

    flight_project = ROOT / "src/Spyro.Editor.NativeFreeFlightSmoke/Spyro.Editor.NativeFreeFlightSmoke.csproj"
    if any(project == flight_project for project, _, _ in cases) and build_results.get(flight_project, False):
        execute("native-free-flight-loader-scalar", [sys.executable, "-B",
                str(ROOT / "tools/Test-NativeFreeFlightLoader.py"), "--source-free", "--dll",
                str(flight_project.parent / "bin" / options.configuration / "net10.0" /
                    "Spyro.Editor.NativeFreeFlightSmoke.dll")],
                "NATIVE_FREE_FLIGHT_LOADER_SOURCE_FREE_OK")

    def source_image_before(deadline):
        image = configured_source_image()
        if time.monotonic() >= deadline:
            raise TimeoutError("Source QA deadline exhausted; no command was started.")
        return image

    if options.with_source_qa and all(result["passed"] for result in results):
        # All opted-in source checks share the existing aggregate deadline.
        source_deadline = time.monotonic() + options.source_qa_timeout
        offline_passed = execute("source-offline-qa",
                ["env", "CONFIGURATION=" + options.configuration, "bash",
                 str(ROOT / "tools/Run-SpyroEditorOfflineQa.sh")], "Offline QA passed.",
                timeout=options.source_qa_timeout)
        if offline_passed:
            for name, marker, arguments in SOURCE_QA_CASES:
                project = ROOT / "src" / ("Spyro.Editor." + name) / ("Spyro.Editor." + name + ".csproj")
                suffix = {
                    ("--source-backed-route-surfaces",): "-route-surfaces",
                    ("--fresh-material-inspection-source",): "-fresh-material-inspection",
                    ("--locked-chest-source",): "-locked-chest",
                    ("--tiled-family-candidate-source",): "-tiled-family-candidate",
                }.get(tuple(arguments), "")
                if not execute("source-" + name + suffix,
                        lambda project=project, arguments=arguments, name=name: [
                            "dotnet", "run", "--project", str(project), "--configuration",
                            options.configuration, "--no-build", "--no-restore", "--",
                            *arguments, source_image_before(source_deadline),
                            *([configured_source_cue()] if name in SOURCE_QA_CUE_CASES else []),
                            *([str(ROOT)] if name in SOURCE_QA_WORKSPACE_CASES else [])], marker,
                        timeout=options.source_qa_timeout, deadline=source_deadline):
                    break
            if all(result["passed"] for result in results):
                for name, relative_path, marker in SOURCE_PYTHON_CASES:
                    if not execute("source-" + name,
                            lambda relative_path=relative_path: [sys.executable, "-B",
                                str(ROOT / relative_path), source_image_before(source_deadline)], marker,
                            timeout=options.source_qa_timeout, deadline=source_deadline):
                        break
    failed = sum(not result["passed"] for result in results)
    print(f"{len(results)} checks; {failed} failed. Report: {output / 'summary.json'}", flush=True)
    return 1 if failed else 0


if __name__ == "__main__":
    raise SystemExit(main())
