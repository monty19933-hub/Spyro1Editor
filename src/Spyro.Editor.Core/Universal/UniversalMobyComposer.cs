using System.Buffers.Binary;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Universal;

/// <summary>One Moby placed in a destination level from any source level's native row.</summary>
public sealed record UniversalMobyPlacement(string SourceLevelKey, int SourceRowIndex, int X, int Y, int Z,
    int? Yaw = null, IReadOnlyDictionary<int, byte>? RowByteOverrides = null, string? EditorId = null, string? SpecialId = null,
    IReadOnlyList<string>? Text = null, int? PersistentSlot = null, bool Retired = false)
{
    public string? LinkGroupId { get; init; }
    public Spyro.Editor.Core.Primitives.ColorRgba? DragonColor { get; init; }
    /// <summary>Words of the object's own properties (offsets in its packed properties, as captured) set to new values:
    /// a moved homeworld doorway's path nodes.</summary>
    public IReadOnlyDictionary<int, int>? PropertyWordOverrides { get; init; }
    /// <summary>An explicit save identity (0-255). Below a rebuilt level's original prefix it must be one of
    /// <see cref="UniversalSaveIdentities.Reusable"/>.</summary>
    public int? SaveIdentity { get; init; }
}

public sealed record UniversalClassImport(int ClassId, string SourceLevelKey, bool ModelImported, bool BehaviorImported,
    int ModelBytes, UniversalTextureReport? Textures, UniversalSoundReport? Sounds, bool DependencyOnly)
{
    /// <summary>ClassId remains the donor identity; this is the registration/row ID in the output.</summary>
    public int DestinationClassId { get; init; } = ClassId;
    public string ModelOrigin { get; init; } = "source-local-or-resident";
}

public sealed record UniversalRamBudget(uint OriginalEnd, uint FinalEnd, uint Limit, int OriginalHeadroom, int FinalHeadroom,
    int OverlayGrowth, int ModelGrowth, int SceneGrowth, int DataGrowth);

public sealed record UniversalComposeResult(
    string DestinationKey,
    IReadOnlyList<UniversalClassImport> Classes,
    IReadOnlyList<int> NewRowIndices,
    UniversalRamBudget Ram,
    int ArenaSlotsRemaining,
    int VramBytesUsed,
    IReadOnlyList<UniversalBehaviorSourceReport> Behavior,
    IReadOnlyList<string> Warnings,
    int TreasureDelta,
    byte[] Overlay,
    byte[] LevelData,
    byte[] Executable)
{
    /// <summary>Spare object slots added to an edited retail level's arena to keep its own retail reserve.</summary>
    public int ArenaSlotsAdded { get; init; }
    /// <summary>Native models dropped because every placed copy was removed; with the bytes freed.</summary>
    public IReadOnlyList<int> DroppedModelClasses { get; init; } = [];
    public int DroppedModelBytes { get; init; }
    public int ReclaimedVramBytes { get; init; }
    public IReadOnlyList<NativeTexturePageOwnedRange> ImportedVramRanges { get; init; } = [];
    public IReadOnlyList<UniversalParticleDependency> ParticleDependencies { get; init; } = [];
    public NativeFreshParticleBinding? FreshParticleBinding { get; init; }
    public IReadOnlyList<UniversalCollectionStarCertificate> CanonicalGemCollectionProofs { get; init; } = [];
    public IReadOnlyList<UniversalTerraceLineParticleProof> NativeLineParticleProofs {get;init;}=[];
    public IReadOnlyList<UniversalFlightChestParticleProof> FlightChestParticleProofs { get; init; } = [];
    public IReadOnlyList<UniversalFlightGateParticleProof> FlightGateParticleProofs { get; init; } = [];
    public int RestoredObjectLinks { get; init; }
    public int RestoredIndexedObjectLinks { get; init; }
    public int UnresolvedObjectLinks { get; init; }
    public IReadOnlyList<int> PlacementSaveRows { get; init; } = [];
    public IReadOnlyList<int> PhysicalToLogicalSaveRows { get; init; } = [];
    public IReadOnlyList<int> UnoccupiedPhysicalRows { get; init; } = [];
    public int NativeRowsBefore { get; init; }
    public int NativeRowsAfter { get; init; }
    public int ReusedPhysicalRows { get; init; }
    public int DroppedPropertyBytes { get; init; }
    public IReadOnlyList<UniversalPlayerAnimationImport> PlayerAnimationImports { get; init; } = [];
    public IReadOnlyList<UniversalWhirlwindSoundBinding> WhirlwindSoundImports { get; init; } = [];
    public IReadOnlyList<UniversalMetalTextureBinding> MetalTextureBindings { get; init; } = [];
    public IReadOnlyList<UniversalDestinationGemCall> DestinationGemCollectionCalls { get; init; } = [];
    public int DestinationGemCollectionExtraBytes { get; init; }
    public IReadOnlyList<UniversalDestinationGemColourLoad> DestinationGemColourLoads { get; init; } = [];
    public int DestinationGemColourExtraBytes { get; init; }
    public IReadOnlyList<UniversalNativeParticleNoOpProof> NativeParticleNoOpProofs { get; init; } = [];
}

/// <summary>
/// Composes universal Moby imports into one destination level: models (with textures and sounds),
/// placed rows with private properties, and relinked source behavior chained onto the destination's
/// native update/init callbacks. The source image is never modified.
/// </summary>
public static class UniversalMobyComposer
{
    public const uint NormalBufferFloor = 0x80187BB0; // lowest normal reserved buffer (see runtime memory ownership)
    public const int RamSafetyMargin = 0x400;
    public const int MinimumDynamicSlots = 16;
    /// <summary>Spare object slots every retail level reserves (91; a few flight levels more). The allocator
    /// (0x800524C4) takes the next free row upward and a 0x18-byte record downward with no bounds check, so each
    /// live drop, butterfly or projectile needs one: past the reserve they overwrite level data.</summary>
    public const int RetailDynamicSlots = 91;
    /// <summary>The spare slots a built level reserves: the retail 91 where the 256-row range allows.</summary>
    public static int BuiltLevelDynamicSlots(int activePlacements) =>
        Math.Max(MinimumDynamicSlots, Math.Min(RetailDynamicSlots, 256 - activePlacements));
    /// <summary>Spare slots an edited retail level keeps beyond one per object that can drop something
    /// (projectiles and effects).</summary>
    public const int DropSlotMargin = MinimumDynamicSlots;

    /// <summary>Placed rows that can put an object into the spare slots when defeated or broken: every row with a
    /// reward byte (+0x53: a gem, a butterfly or another reward), except visible gems and removed rows.</summary>
    public static int DropCapableRows(UniversalLevelData d)
    {
        var sc = d.Scene(); int rows = d.Subfiles()[3].Offset + sc.RowsOffset, count = 0;
        for (int i = 0; i < sc.RowCount; i++)
        {
            var row = d.Bytes.AsSpan(rows + i * UniversalLevelData.RowStride, UniversalLevelData.RowStride);
            int c = BinaryPrimitives.ReadUInt16LittleEndian(row[0x36..]);
            if (row[0x53] != 0xFF && c != InertClass && !GemValue.TryFromIdByte(c, out _) && UniversalHighValueGems.ValueForClass(c) == 0)
                count++;
        }
        return count;
    }

    /// <summary>The spare slots an edited retail level must keep: one per object that can drop something plus
    /// <see cref="DropSlotMargin"/>, never more than the level's own retail reserve.</summary>
    public static int RequiredDynamicSlots(int retailSlots, int dropCapable) =>
        Math.Min(retailSlots, Math.Max(MinimumDynamicSlots, dropCapable + DropSlotMargin));

    /// <summary>Grows an edited level's spare object slots back towards <paramref name="targetSlots"/>, as many as
    /// <paramref name="ramRoom"/> bytes of game RAM allow: the scene is loaded last, so only its sector-rounded length
    /// moves the level's RAM end. Returns the level data to use and the slots added.</summary>
    public static (UniversalLevelData Data, int Added) RestoreDynamicSlots(UniversalLevelData data, int targetSlots, long ramRoom)
    {
        int want = targetSlots - ArenaSlots(data), length = data.Subfiles()[3].Length;
        if (want <= 0 || ramRoom < 0) return (data, 0);
        long longest = length + ramRoom / 0x800 * 0x800;
        for (long slots = Math.Min(want, (longest - data.Scene().UsedEnd) / UniversalLevelData.ArenaSlotBytes); slots > 0; slots--)
        {
            var grown = new UniversalLevelData(data.Bytes.ToArray());
            grown.GrowArena((int)slots);
            if (grown.Subfiles()[3].Length - length <= ramRoom) return (grown, (int)slots);
        }
        return (data, 0);
    }

    public const uint TreasureTableAddress = 0x8006F438;

    /// <summary>A delete-only operation may retain the verified native resident end when it adds
    /// no actors or overlay bytes. New content and physical save maps keep the full import margin.</summary>
    public static bool PreservesNativeRemovalBudget(int removedCount, int activeIncoming, int overlayGrowth,
        uint originalEnd, uint verifiedSourceEnd, uint finalEnd) =>
        removedCount > 0 && activeIncoming == 0 && overlayGrowth == 0 && originalEnd <= verifiedSourceEnd &&
        finalEnd <= originalEnd && finalEnd <= NormalBufferFloor;

    /// <summary>Gem value a native row contributes: a visible gem (type 0x18) or its reward gem ID (+0x53).</summary>
    public static int TreasureValue(byte[] row) => UniversalTreasureAccounting.KnownValue(row);

    public static uint RuntimeEnd(UniversalLevelData d, uint copyBuffer)
    {
        var snd = new NativeLevelSoundLayout(d.Bytes);
        var sf = d.Subfiles();
        return (uint)(copyBuffer + (snd.SoundEnd - snd.DataOffset) + sf[2].Length + sf[3].Length);
    }

    public static int ArenaSlots(UniversalLevelData d)
    {
        var sc = d.Scene();
        return (sc.ArenaEnd - (sc.RowsOffset + sc.RowCount * UniversalLevelData.RowStride)) / 112;
    }

    /// <param name="sources">Original-image source level data and overlays keyed by level key.</param>
    /// <param name="shared">Classes resident in the shared PETE model table.</param>
    /// <param name="removedTreasureAlreadyApplied">True only when the ordinary source-row writer
    /// already applied removedRows treasure to this executable. Direct composition owns it by default.</param>
    public static UniversalComposeResult Compose(
        LevelDefinition destination,
        byte[] destinationData,
        byte[] destinationOverlay,
        byte[] executable,
        Func<string, (LevelDefinition Level, UniversalLevelData Data, UniversalOverlay Overlay)> source,
        IReadOnlySet<int> shared,
        IEnumerable<(long Offset, int Length)> protectedVram,
        IReadOnlyList<UniversalMobyPlacement> placements,
        Func<string, UniversalSpecialAsset?>? special = null,
        IReadOnlyCollection<int>? removedRows = null,
        Func<IReadOnlySet<int>, IEnumerable<(long Offset, int Length)>>? protectedVramAfterDrop = null,
        Func<int, byte[]>? sharedModelPackage = null,
        Func<NativePlayerAnimationSource>? playerAnimationSource = null,
        bool removedTreasureAlreadyApplied = false,
        int? persistentNativePrefixRows = null,
        UniversalOverlay? nativeDestinationOwner = null, int magicPairBundlePaddingBytes = 0, byte[]? nativeOriginalExecutable = null, byte[]? nativeSharedEntry8 = null,
        IReadOnlyList<(int Class, string SourceKey)>? dependencyClasses = null, IReadOnlySet<int>? releasedNativeClasses = null)
    {
        if (placements.Count == 0 && removedRows is not { Count: > 0 } && dependencyClasses is not { Count: > 0 }) throw new ArgumentException("No placements or permanent removals were supplied.");
        if(UniversalAlpineArmInstaller.Requested(placements))return UniversalAlpineArmInstaller.Compose(destination,destinationData,destinationOverlay,executable,source,shared,protectedVram,placements,removedRows,playerAnimationSource,nativeSharedEntry8,persistentNativePrefixRows,magicPairBundlePaddingBytes,protectedVramAfterDrop,removedTreasureAlreadyApplied);
        UniversalHauntedSuperflameFairy.RequirePlacements(destination.Key,placements,source);
        bool hauntedFairy=destination.Key=="darkhollow"&&UniversalHauntedSuperflameFairy.Requested(placements);
        UniversalMagicPairCertificate? magicPair=null;
        UniversalMagicDruidChainCertificate? magicChain=null;
        UniversalMagicDruidOriginalCertificate? magicOriginal=null;
        if(placements.Any(p=>!p.Retired&&p.SourceLevelKey=="magiccrafters"&&p.SourceRowIndex is 1 or 11 or 16 or 25 or 26 or 27 or 32 or 33))
        {var donor=source("magiccrafters");magicOriginal=UniversalMagicDruidOriginalCertificate.Capture(destination.Key,destinationData,destinationOverlay,executable,donor.Data,donor.Overlay,placements,removedRows);}
        if(placements.Any(p=>!p.Retired&&p.SourceLevelKey=="magiccrafters"&&p.SourceRowIndex is >=2 and <=5))
        {var donor=source("magiccrafters");magicChain=UniversalMagicDruidChainCertificate.Capture(destination.Key,destinationData,destinationOverlay,executable,donor.Data,donor.Overlay,placements,removedRows);}
        if(placements.Any(p=>!p.Retired&&p.SourceLevelKey=="magiccrafters"&&p.SourceRowIndex is 12 or 13))
        { var donor=source("magiccrafters"); magicPair=UniversalMagicPairCertificate.Capture(destination.Key,destinationData,destinationOverlay,executable,donor.Data,donor.Overlay,placements,removedRows,removedTreasureAlreadyApplied); }
        if(magicPairBundlePaddingBytes!=0&&magicPair==null&&magicChain==null&&magicOriginal==null)throw new InvalidDataException("Magic bundle padding requires the scoped original pair owner.");
        if(magicPairBundlePaddingBytes<0||magicPairBundlePaddingBytes%8!=0)throw new InvalidDataException("Magic bundle padding must be a nonnegative aligned allocation.");
        int incomingCount = placements.Count;
        int[] incomingOrder = Enumerable.Range(0, placements.Count).ToArray();
        if (placements.Any(p => p.PersistentSlot != null))
        {
            // Objects outside the ledger (a homeworld's doorway objects) carry an explicit save identity. A ledger object
            // may carry one too (a rebuilt homeworld's own dragon keeps its original one); its slot stays reserved.
            var ledger = placements.Where(p => p.PersistentSlot != null).ToArray();
            if (placements.Any(p => p.PersistentSlot == null && p.SaveIdentity == null) || placements.Any(p => string.IsNullOrWhiteSpace(p.EditorId)) ||
                placements.Select(p => p.EditorId).Distinct(StringComparer.Ordinal).Count() != placements.Count ||
                !ledger.Select(p => p.PersistentSlot!.Value).Order().SequenceEqual(Enumerable.Range(0, ledger.Length)))
                throw new InvalidDataException("Persistent placement ledger requires unique IDs and contiguous lifetime slots starting at zero.");
            incomingOrder = incomingOrder.OrderBy(i => placements[i].PersistentSlot == null ? 1 : 0).ThenBy(i => placements[i].PersistentSlot ?? i).ToArray();
            placements = incomingOrder.Select(i => placements[i]).ToArray();
        }
        // Lifetime ledger entries reserve save identities, never physical actor rows or properties.
        var activeOrder = Enumerable.Range(0, placements.Count).Where(i => !placements[i].Retired).ToArray();
        incomingOrder = activeOrder.Select(i => incomingOrder[i]).ToArray();
        placements = activeOrder.Select(i => placements[i]).ToArray();
        UniversalLevelData dest = new(destinationData.ToArray());
        int nativeRowsBefore = dest.Scene().RowCount;
        var permanentlyRemoved = UniversalRowRemoval.Normalize(nativeRowsBefore, removedRows);
        // Capture before dropped classes become inert and packages/rows move.
        int removedKnownTreasure = permanentlyRemoved.Sum(row => TreasureValue(dest.Row(row)));
        int savePrefix = persistentNativePrefixRows ?? nativeRowsBefore;
        if (persistentNativePrefixRows != null &&
            (nativeRowsBefore != 0 || savePrefix is < 0 or > 256 ||
             savePrefix != source(destination.Key).Data.Scene().RowCount))
            throw new InvalidDataException("A rebuilt empty level must retain its exact original native save prefix.");
        // Without a ledger, objects take the identities after the prefix in order, skipping those with their own.
        int[] rank = new int[placements.Count];
        for (int i = 0, next = 0; i < placements.Count; i++) rank[i] = placements[i].SaveIdentity == null ? next++ : -1;
        int[] saveRows = placements.Select((p, i) => p.SaveIdentity ?? checked(savePrefix + (p.PersistentSlot ?? rank[i]))).ToArray();
        if (placements.Any(p => p.SaveIdentity is int id && id < savePrefix))
        {
            // Below the prefix: an identity no original object could have set, or the object's own (an original object
            // put back in its rebuilt level).
            var reusable = UniversalSaveIdentities.Reusable(source(destination.Key), shared).ToHashSet();
            bool Own(UniversalMobyPlacement p, int id) => id == p.SourceRowIndex && string.Equals(p.SourceLevelKey, destination.Key, StringComparison.OrdinalIgnoreCase);
            if (placements.Any(p => p.SaveIdentity is int id && (id < 0 || id < savePrefix && !reusable.Contains(id) && !Own(p, id))))
                throw new InvalidDataException("An object reuses a save identity that the original level's objects could have set.");
        }
        if (saveRows.Distinct().Count() != saveRows.Length) throw new InvalidDataException("Two objects share a save identity.");
        if (saveRows.Any(i => i > 255)) throw new InvalidDataException("An active placement's lifetime save identity exceeds the native 256-bit level bitmap; retired identities cannot be reused on existing cards.");
        byte[] exe = executable.ToArray();
        var bindings = UniversalExeBindings.Read(exe, destination.LevelId);
        UniversalOverlay destOverlay = new(destinationOverlay.ToArray(), bindings);
        if (nativeDestinationOwner != null &&
            (!nativeDestinationOwner.Bytes.SequenceEqual(destinationOverlay) ||
             nativeDestinationOwner.Update != destOverlay.Update || nativeDestinationOwner.Init != destOverlay.Init ||
             nativeDestinationOwner.CopyBuffer != destOverlay.CopyBuffer))
            throw new InvalidDataException("The rebuilt level's native behavior owner differs from its actual prepared host.");
        uint originalEnd = RuntimeEnd(dest, destOverlay.CopyBuffer);
        var destSwitch = UniversalDispatchAnalysis.FindUpdateSwitch(destOverlay);
        // Released classes (a rebuilt level's own dragons) keep their numbers and run their own imported code; the
        // destination's switch passes them by (UniversalNativeRelease).
        bool DestHandles(int c) => releasedNativeClasses?.Contains(c) != true && UniversalDispatchAnalysis.Handles(destOverlay, destSwitch, c);
        HashSet<int> destRegistered = [.. dest.Registrations().Select(r => r.ClassId)];
        List<string> warnings = [];

        var greenDonor=destination.Key!="alpineridge"&&placements.Any(p=>p.SourceLevelKey=="alpineridge"&&UniversalGreenDruidWorldRows.EnrolledMembers.Contains(p.SourceRowIndex))?source("alpineridge"):default;
        var greenRequest=greenDonor.Data==null?null:UniversalGreenDruidWorldInstallation.RequireRequest(destination.Key,dest,destOverlay,exe,nativeOriginalExecutable,greenDonor.Data!,greenDonor.Overlay,placements,permanentlyRemoved,persistentNativePrefixRows);
        bool linkedRequested=destination.Key!="alpineridge"&&UniversalGreenDruidLinkedWorldInstallation.IsRequested(placements);
        var linkedDonor=linkedRequested?source("alpineridge"):default;
        if(linkedRequested)UniversalGreenDruidLinkedWorldInstallation.RequireRequest(destination.Key,dest,destOverlay,exe,nativeOriginalExecutable,linkedDonor.Data!,linkedDonor.Overlay,placements,permanentlyRemoved,persistentNativePrefixRows);
        // 1. Resolve classes and dependency closure (spawned child classes come from the same source level).
        Dictionary<(string Source, int Class), (int DestinationClass, bool Dependency)> classes = [];
        Dictionary<string, UniversalDryCanyonBirdCertificate> dryCanyonBirdBranches = new(StringComparer.OrdinalIgnoreCase);
        HashSet<int> reservedClasses = [.. shared, .. destRegistered, .. UniversalDragonflyEggs.Variants.Select(v => v.Class), UniversalDragonflyEggs.SignClass, InertClass];
        for (int row = 0; row < dest.Scene().RowCount; row++) reservedClasses.Add(BinaryPrimitives.ReadUInt16LittleEndian(dest.Row(row).AsSpan(0x36)));
        HashSet<string> reservedSources = new(StringComparer.OrdinalIgnoreCase);
        for (int c = 1; c < 0x200; c++) if (DestHandles(c)) reservedClasses.Add(c);
        foreach (byte[] bytes in new[] { exe, destinationOverlay })
            for (int offset = 0; offset + 4 <= bytes.Length; offset += 4)
            {
                uint word = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset));
                if (UniversalMips.Op(word) is 9 or 10 or 11 or 12 or 13 or 14 && UniversalMips.Imm(word) is > 0 and < 0x200)
                    reservedClasses.Add(UniversalMips.Imm(word));
            }
        Dictionary<int, UniversalSpecialAsset> specialClasses = [];
        int AllocateClass(string sk, int c)
        {
            for (int candidate = 0x1FD; candidate > 0; candidate--)
                if (!reservedClasses.Contains(candidate) && !specialClasses.ContainsKey(candidate))
                { reservedClasses.Add(candidate); return candidate; }
            throw new UniversalSourceCollisionException($"No proven unused class ID remains for {sk}:0x{c:X3}.");
        }
        var originalDestination = source(destination.Key);
        var flightGate = UniversalFlightGateOwnership.Capture(destination.Key,dest,destOverlay,originalDestination.Data,originalDestination.Overlay,placements,permanentlyRemoved,source,exe);
        var flightGates = new Dictionary<string,UniversalFlightGateCertificate>(StringComparer.OrdinalIgnoreCase);
        if(flightGate!=null)flightGates[flightGate.SourceKey]=flightGate;
        var flightChest = UniversalFlightChestOwnership.Capture(destination.Key,dest,destOverlay,originalDestination.Data,originalDestination.Overlay,placements,permanentlyRemoved,source,exe);
        var flightChests = new Dictionary<string,UniversalFlightChestCertificate>(StringComparer.OrdinalIgnoreCase);
        if(flightChest!=null)flightChests[flightChest.SourceKey]=flightChest;
        var darkLanternContexts=new Dictionary<string,UniversalDarkPassageLanternContext>(StringComparer.OrdinalIgnoreCase);
        if(placements.Any(p=>p.SpecialId==null&&!p.Retired&&string.Equals(p.SourceLevelKey,"darkpassage",StringComparison.OrdinalIgnoreCase)))
        { var lanternSource=source("darkpassage");var context=UniversalDarkPassageLanternGroups.Issue(destination.Key,dest,destOverlay,lanternSource.Data,lanternSource.Overlay,placements,exe);if(context!=null) { darkLanternContexts.Add("darkpassage",context);warnings.Add(context.BehaviorNote); } }
        bool originalDestinationAssets = (flightChest != null || flightGate != null || destinationData.AsSpan().SequenceEqual(originalDestination.Data.Bytes)) &&
            destinationOverlay.AsSpan().SequenceEqual(originalDestination.Overlay.Bytes);
        bool EquivalentDestination(int c, string sk, out string reason)
        {
            if(c==0x7A&&darkLanternContexts.TryGetValue(sk,out var lanternContext)&&!lanternContext.UsesSourceTerrainLighting) { reason="Imported lantern actor lighting owns a separate source namespace.";return false; }
            if (originalDestinationAssets)
                return EquivalentSources(c, destination.Key, sk, out reason);
            var donor = source(sk);
            return UniversalSourceIdentity.Equivalent(dest, destOverlay, donor.Data, donor.Overlay, c, shared, out reason);
        }
        bool EquivalentSources(int c, string a, string b, out string reason)
        {
            if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase)) { reason = "same source level"; return true; }
            var sa = source(a); var sb = source(b);
            return UniversalSourceIdentity.Equivalent(sa.Data, sa.Overlay, sb.Data, sb.Overlay, c, shared, out reason);
        }
        Queue<(int Class, string Source, bool Dependency)> work = new();
        List<UniversalRowTemplate> templates = [];
        HashSet<(int SourceRow,string? Group)> darkRetainedNativeDogTargets=[];
        Dictionary<(string Source, int Type), UniversalParticleDependency> particleDependencies = [];
        UniversalTerraceLineParticleCertificate? nativeLine77=null;
        Dictionary<(string Source, int Type), UniversalFlightChestParticleProof> flightParticleProofs = [];
        Dictionary<(string Source, int Type), UniversalFlightGateParticleProof> gateParticleProofs = [];
        foreach (var p in placements)
        {
            if (p.SpecialId != null)
            {
                var asset = special?.Invoke(p.SpecialId) ?? throw new InvalidDataException(
                    $"'{p.SpecialId}' needs its original source disc. Select that disc in the editor settings.");
                byte[] row = asset.RowTemplate.ToArray();
                BinaryPrimitives.WriteUInt16LittleEndian(row.AsSpan(0x36), (ushort)asset.ClassId);
                if (asset.Behavior is not (UniversalSpecialBehavior.CrashBox or UniversalSpecialBehavior.CrashEnemy))
                    row[0x53] = 0xFF; // no gem reward; Crash crates and enemies keep their native reward (a gem or a butterfly)
                byte[] props = asset.Behavior == UniversalSpecialBehavior.Sign
                    ? UniversalSpecialBehaviors.EncodeSignText(p.Text ?? [])
                    : new byte[16];
                templates.Add(new UniversalRowTemplate(asset.SourceKey, -1, row, props, [], 0));
                specialClasses[asset.ClassId] = asset;
                foreach (var companion in asset.Companions) specialClasses[companion.ClassId] = companion;
                continue;
            }
            var s = source(p.SourceLevelKey);
            var t = UniversalIndexedRows.Capture(s.Data, s.Overlay, s.Data.CaptureRow(p.SourceLevelKey, p.SourceRowIndex));
            // A rebuilt homeworld's own dragons keep naming each other's rows (statue, dragon, pedestal).
            if (persistentNativePrefixRows != null && string.Equals(p.SourceLevelKey, destination.Key, StringComparison.OrdinalIgnoreCase))
                t = UniversalDragonRows.Capture(s.Data, t);
            if(destination.Key=="alpineridge"&&UniversalGreenDruidWorldRows.Applies(t))t=UniversalGreenDruidNativeReferences.Capture(dest,s.Data,s.Overlay,t,permanentlyRemoved);
            if(string.Equals(p.SourceLevelKey,"darkpassage",StringComparison.OrdinalIgnoreCase)&&string.Equals(destination.Key,"darkpassage",StringComparison.OrdinalIgnoreCase)&&t.ClassId==0x35&&t.SourceRowIndices.Count==1&&
                destinationData.AsSpan().SequenceEqual(originalDestination.Data.Bytes)&&destinationOverlay.AsSpan().SequenceEqual(originalDestination.Overlay.Bytes))
            {
                var nativeLink=t.SourceRowIndices.Single();
                bool copiedTarget=placements.Any(a=>a.SpecialId==null&&!a.Retired&&string.Equals(a.SourceLevelKey,p.SourceLevelKey,StringComparison.OrdinalIgnoreCase)&&a.SourceRowIndex==nativeLink.SourceRow&&(a.LinkGroupId??"")==(p.LinkGroupId??""));
                if(!copiedTarget&&!permanentlyRemoved.Contains(nativeLink.SourceRow))
                { darkRetainedNativeDogTargets.Add((t.SourceRowIndex,p.LinkGroupId));t=t with{SourceRowIndices=[]}; }
            }
            if(flightGates.TryGetValue(p.SourceLevelKey,out var gateTemplateProof))t=gateTemplateProof.PrepareTemplate(t);
            if(magicPair!=null&&UniversalMagicPairCertificate.IsPair(t))t=magicPair.Rebase(t,p);
            if(magicChain!=null&&UniversalMagicDruidChainResources.IsChain(t))t=magicChain.Rebase(t,p);
            if(magicOriginal!=null&&UniversalMagicDruidOriginalRows.IsSupported(t))t=magicOriginal.Rebase(t,p);
            templates.Add(t with { LinkGroupId = p.LinkGroupId });
            work.Enqueue((t.ClassId, p.SourceLevelKey, false));
        }
        // Copies have independent group identities. A source pointer may only select the
        // unique counterpart in this group, including the legacy ungrouped namespace.
        for (int i = 0; i < templates.Count; i++)
        {
            var t = templates[i];
            templates[i] = UniversalObjectRowLinks.Resolve(UniversalIndexedRows.Resolve(t, templates), templates);
            if (templates[i].DroppedExternalPointers > 0)
                warnings.Add($"{t.SourceLevelKey} row {t.SourceRowIndex}: {templates[i].DroppedExternalPointers} unresolved source-object link(s) were cleared; each target must be present once in the same linked group.");
        }
        // Models the level needs without placing them (a built level's liquid splashes, UniversalLiquidSplashes). The
        // executable spawns them by their fixed class, so they keep it; a level that already has the model needs nothing.
        HashSet<(string, int)> fixedDependencies = [];
        foreach (var (c, sk) in dependencyClasses ?? [])
        {
            if (destRegistered.Contains(c)) continue;
            fixedDependencies.Add((sk.ToLowerInvariant(), c));
            work.Enqueue((c, sk, true));
        }
        while (work.Count > 0)
        {
            var (c, sk, dep) = work.Dequeue();
            if (c <= 0 || c >= 0x200)
                throw new InvalidDataException($"Class 0x{c:X3} from {sk} is outside the supported placement range.");
            if(linkedRequested&&c==0x10E&&sk=="alpineridge"){ /* The exact complete request is validated above; installed capability is required below. */ }
            else if(greenRequest!=null&&c==0x10E&&sk=="alpineridge")greenRequest.Require(destination.Key,sk,c,dest,source(sk).Data);
            else UniversalGreenDruidWorldOwnership.Require(destination.Key, sk, c, dest, source(sk).Data, permanentlyRemoved);
            if (reservedSources.Add(sk))
            {
                var sd = source(sk).Data;
                reservedClasses.UnionWith(sd.Registrations().Select(r => r.ClassId));
                for (int row = 0; row < sd.Scene().RowCount; row++) reservedClasses.Add(BinaryPrimitives.ReadUInt16LittleEndian(sd.Row(row).AsSpan(0x36)));
            }
            var identity = (sk.ToLowerInvariant(), c);
            if (classes.TryGetValue(identity, out var existing))
            {
                if (existing.Dependency && !dep) classes[identity] = (existing.DestinationClass, false);
                continue;
            }
            int mapped = c;
            var other = classes.FirstOrDefault(k => k.Key.Class == c);
            bool collision = classes.Any(k => k.Value.DestinationClass == c && k.Key.Class != c);
            // Class-specific byte equivalence cannot establish identical child namespaces.
            // Co-imports share IDs only for an identical complete source package.
            collision |= other.Key.Source != null &&
                (!source(other.Key.Source).Data.Bytes.AsSpan().SequenceEqual(source(sk).Data.Bytes) ||
                 !source(other.Key.Source).Overlay.Bytes.SequenceEqual(source(sk).Overlay.Bytes));
            collision |= (DestHandles(c) || destRegistered.Contains(c)) && !EquivalentDestination(c, sk, out _);
            if (fixedDependencies.Contains(identity))
            {
                if (classes.Any(k => k.Value.DestinationClass == c && k.Key != identity))
                    throw new UniversalSourceCollisionException($"Class 0x{c:X3}, which the game spawns by number, is already taken in {destination.Key}.");
                collision = false;
            }
            if (collision)
            {
                if (shared.Contains(c) && !source(sk).Data.Registrations().Any(r => r.ClassId == c) && sharedModelPackage == null)
                    throw new UniversalSourceCollisionException($"Shared-model class {sk}:0x{c:X3} needs a local shared package alias before its behavior can be remapped.");
                mapped = AllocateClass(sk, c);
            }
            else if (other.Key.Source != null) mapped = other.Value.DestinationClass;
            reservedClasses.Add(mapped);
            classes[identity] = (mapped, dep);
            var s = source(sk);
            var sw = UniversalDispatchAnalysis.FindUpdateSwitch(s.Overlay);
            if (!UniversalDispatchAnalysis.Handles(s.Overlay, sw, c))
            {
                if (!s.Data.Registrations().Any(r => r.ClassId == c) && !shared.Contains(c))
                    throw new InvalidDataException($"Class 0x{c:X3} from {sk} has neither a model nor an extractable handler; its dependency is unresolved.");
                continue;
            }
            if (mapped != c || !DestHandles(mapped))
            {
                if(flightChests.TryGetValue(sk,out var proof))proof.RequireSource(s.Overlay);
                else if(flightGates.TryGetValue(sk,out var gateProof))gateProof.RequireSource(s.Overlay);
                else UniversalFlightCopiedRootOwnership.RequireCopiedSourceDependencyOwnership(sk, s.Overlay, [c]);
            }
            var tr = UniversalDispatchAnalysis.Trace(s.Overlay, sw.TreeStart, c, sw.Seeds, sw.ClassRegister);
            UniversalDryCanyonBirdCertificate? birdProof = null;
            if (c == 0xB8 && string.Equals(sk, "drycanyon", StringComparison.OrdinalIgnoreCase))
            {
                birdProof = UniversalDryCanyonBirdBranches.Capture(sk, s.Data, s.Overlay, exe)
                    ?? throw new InvalidDataException("Dry Canyon B8 branch authority is unavailable.");
                dryCanyonBirdBranches[sk] = birdProof;
            }
            var (closure, _) = UniversalDispatchAnalysis.Closure(s.Overlay, sw.Function, [tr.Stop], new HashSet<uint> { sw.TreeStart }, birdProof, sk=="magiccrafters"?magicPair:null,sk=="magiccrafters"?magicChain:null,sk=="magiccrafters"?magicOriginal:null);
            var (spawns, particles) = UniversalDispatchAnalysis.Dependencies(s.Overlay, closure);
            try
            {
                var initDependencies = UniversalDispatchAnalysis.Dependencies(s.Overlay, UniversalSpecializer.Specialize(s.Overlay, s.Overlay.Init, 4, [c]).Instructions);
                spawns.UnionWith(initDependencies.SpawnedClasses); particles.UnionWith(initDependencies.ParticleTypes);
            }
            catch (InvalidDataException) when (mapped == c) { /* Existing whole-init fallback remains guarded by bundle extraction. */ }
            if(flightChests.TryGetValue(sk,out var chestProof))chestProof.AddDependencies(s.Overlay,c,spawns);
            if(flightGates.TryGetValue(sk,out var gateDependencies))gateDependencies.AddDependencies(s.Overlay,c,spawns);
            UniversalDarkPassageShapeshifterOwnership.AddDependencies(sk,s.Overlay,c,spawns);
            foreach (int particle in particles)
            {
                var key = (sk, particle);
                if (!particleDependencies.ContainsKey(key))
                {
                    if(flightChests.TryGetValue(sk,out var flightParticleCertificate))
                    {
                        var issued=UniversalFlightChestParticles.Inspect(flightParticleCertificate,particle,s.Overlay,destOverlay,s.Data,dest,exe);
                        particleDependencies[key]=issued.Dependency;flightParticleProofs[key]=issued.Proof;
                    }
                    else if(flightGates.TryGetValue(sk,out var gateParticleCertificate))
                    {
                        var issued=UniversalFlightGateParticles.Inspect(gateParticleCertificate,particle,s.Overlay,destOverlay,s.Data,dest,exe);
                        particleDependencies[key]=issued.Dependency;gateParticleProofs[key]=issued.Proof;
                    }
                    else if(sk=="twilightharbor" && destination.Key=="darkhollow" && particle is 1 or 13 or 70 or 79)
                    {
                        var issued=UniversalTwilightTrooperParticles.Capture(s.Data,s.Overlay,destOverlay,exe,particle==13);
                        //13 still uses the existing exact texture import path, then
                        //the owned dispatcher and readback issue its final status.
                        particleDependencies[key]=particle==13?UniversalParticleDependencies.Inspect(sk,particle,s.Data,dest,false):UniversalTwilightTrooperParticles.Inspect(issued,particle);
                    }
                    else if(sk=="terracevillage" && destination.Key=="darkhollow" && particle==77)
                    {
                        nativeLine77=UniversalTerraceLineParticle77.Capture(sk,destination.Key,s.Overlay,destOverlay,exe);
                        particleDependencies[key]=UniversalTerraceLineParticle77.Inspect(nativeLine77);
                    }
                    else particleDependencies[key] = UniversalParticleDependencies.Inspect(sk, particle, s.Data, dest,
                        string.Equals(sk, destination.Key, StringComparison.OrdinalIgnoreCase));
                }
            }
            foreach (int k in spawns) if (!classes.ContainsKey((sk.ToLowerInvariant(), k))) work.Enqueue((k, sk, true));
        }

        // An otherwise equivalent native parent cannot call a source-mapped child through
        // its unchanged resident handler. Give the imported namespace its own parent IDs,
        // so every imported handler uses the same proved child-spawn map.
        foreach (string sk in classes.Keys.Select(k => k.Source).Distinct().ToArray())
            if (classes.Any(k => k.Key.Source == sk && k.Key.Class != k.Value.DestinationClass))
                foreach (var key in classes.Keys.Where(k => k.Source == sk).ToArray())
                    if (classes[key].DestinationClass == key.Class && !fixedDependencies.Contains(key) && (DestHandles(key.Class) || (key.Class==0x176 && flightGates.ContainsKey(sk))))
                        classes[key] = (AllocateClass(sk, key.Class), classes[key].Dependency);

        foreach (int c in specialClasses.Keys)
            if (classes.Values.Any(v => v.DestinationClass == c) || destRegistered.Contains(c) || shared.Contains(c) || DestHandles(c))
                throw new UniversalSourceCollisionException($"Special class 0x{c:X3} collides with a native or imported class in {destination.Key}.");
        foreach (int c in releasedNativeClasses ?? new HashSet<int>())
            if (!classes.TryGetValue((destination.Key.ToLowerInvariant(), c), out var released) || released.DestinationClass != c ||
                classes.Any(k => k.Value.DestinationClass == c && k.Key.Source != destination.Key.ToLowerInvariant()))
                throw new InvalidDataException($"Released class 0x{c:X3} must be placed from {destination.DisplayName}'s own objects under its own number.");

        // 1b. Free the models of native object types the user removed completely (see DroppableClasses).
        var (dropped, inertRows) = DroppableClasses(dest, destOverlay, exe, shared, removedRows,
            classes.Values.Select(v => v.DestinationClass).Concat(specialClasses.Keys).ToHashSet());
        int droppedBytes = 0;
        if (dropped.Count > 0)
        {
            foreach (int r in inertRows) dest.SetRowClass(r, InertClass);
            droppedBytes = dest.RemoveModels(dropped);
            foreach (int c in dropped) destRegistered.Remove(c);
        }

        // 2. Models, textures and sounds.
        UniversalVramAllocator baselineVram = new(protectedVram);
        UniversalVramAllocator vram = dropped.Count > 0 && protectedVramAfterDrop != null
            ? new(protectedVramAfterDrop(dropped.ToHashSet())) : baselineVram;
        int reclaimedVram = vram.FreeBytes - baselineVram.FreeBytes;
        if (reclaimedVram < 0) throw new InvalidDataException("Dropped-model ownership scan unexpectedly reduced free texture space.");
        int freeBefore = vram.FreeBytes;
        var greenWorld=greenRequest==null?null:UniversalGreenDruidWorldInstallation.Capture(destination.Key,dest,destOverlay,exe,nativeOriginalExecutable,greenDonor.Data!,greenDonor.Overlay,placements,vram,permanentlyRemoved,persistentNativePrefixRows);
        if(greenWorld!=null){dest=new(greenWorld.Storage.CopyLevelData());warnings.Add(greenWorld.BehaviorNote);}
        var linkedWorld=linkedRequested?UniversalGreenDruidLinkedWorldInstallation.Capture(destination.Key,dest,destOverlay,exe,nativeOriginalExecutable,linkedDonor.Data!,linkedDonor.Overlay,placements,vram,permanentlyRemoved,persistentNativePrefixRows):null;
        if(linkedWorld!=null){dest=new(linkedWorld.Storage.CopyLevelData());linkedWorld.BindNamespace(classes.Where(k=>k.Key.Source=="alpineridge").ToDictionary(k=>k.Key.Class,k=>k.Value.DestinationClass),exe);warnings.Add(linkedWorld.BehaviorNote);}
        var worldDonor=placements.Any(p=>p.SourceLevelKey=="highcaves"&&UniversalHighCavesWorldRows.Members.Contains(p.SourceRowIndex))?source("highcaves"):default;
        var highCavesWorld=worldDonor.Data==null?null:UniversalHighCavesWorldInstallation.Capture(destination.Key,dest,destOverlay,exe,nativeOriginalExecutable,worldDonor.Data,worldDonor.Overlay,placements,vram,permanentlyRemoved,persistentNativePrefixRows);
        if(highCavesWorld!=null){dest=new(highCavesWorld.Storage.CopyLevelData());warnings.Add(highCavesWorld.BehaviorNote);}

        // Metal faces use a native generated-UV material rather than ordinary model
        // texture descriptors. Reserve its entire pixel/fog-palette domain before
        // any regular allocation, even when the destination already has this model.
        Dictionary<(string Source, int Class), UniversalMetalModelCertificate> metalModels = [];
        List<UniversalMetalTextureBinding> metalBindings = [];
        foreach (var key in classes.Keys.Where(k => k.Class == 0xE2 && k.Source == "clifftown"))
        {
            var donor = source(key.Source);
            var certificate = UniversalCliffMetalTextures.Capture(key.Source, donor.Data, donor.Overlay, donor.Data.Package(key.Class), exe)
                ?? throw new InvalidDataException("The Cliff Town metal model has no native shader-field authority.");
            metalModels.Add(key, certificate);
            metalBindings.Add(UniversalCliffMetalTextures.BindDestination(certificate, donor.Data, dest, vram, exe));
        }
        foreach(var key in classes.Keys.Where(k=>k.Class==0xCB && k.Source=="hauntedtowers"))
        {
            var donor=source(key.Source);
            var certificate=UniversalHauntedArmorMetalTextures.Capture(donor.Data,donor.Overlay,exe);
            metalModels.Add(key,certificate);
            metalBindings.Add(UniversalHauntedArmorMetalTextures.BindDestination(certificate,donor.Data,donor.Overlay,dest,vram,exe));
        }
        var superflameScene=hauntedFairy?UniversalHauntedSuperflameScene.Bind(source("hauntedtowers").Data,source("hauntedtowers").Overlay,dest,vram,exe):null;
        List<UniversalClassImport> imports = [];
        int modelGrowth = 0;
        var ordered = classes.OrderBy(k => k.Value.DestinationClass).Select(k => (Class: k.Key.Class, Source: k.Key.Source, DestinationClass: k.Value.DestinationClass, Dependency: k.Value.Dependency)).ToList();
        var modelJobs = ordered.Where(o => !destRegistered.Contains(o.DestinationClass) &&
                (source(o.Source).Data.Registrations().Any(r => r.ClassId == o.Class) && !shared.Contains(o.DestinationClass) || o.DestinationClass != o.Class && shared.Contains(o.Class)))
            .Select(o =>
            {
                var data = source(o.Source).Data;
                byte[] package = data.Registrations().Any(r => r.ClassId == o.Class) ? data.Package(o.Class)
                    : sharedModelPackage?.Invoke(o.Class) ?? throw new InvalidDataException($"Shared class 0x{o.Class:X3} requires its verified alias package provider.");
                return (Class: o.DestinationClass, o.Source, o.Dependency, Pkg: package, Vram: data.Vram().ToArray(), Data: data);
            }).GroupBy(j => j.Class).Select(g => g.First()).ToList();
        foreach (var (c, asset) in specialClasses.OrderBy(k => k.Key))
            if (!destRegistered.Contains(c))
                modelJobs.Add((c, asset.SourceKey, false, asset.Package.ToArray(), asset.SourceVram, asset.SourceData));
        var particleJobs = particleDependencies.Values.Where(p => p.Status == "texture-import-required")
            .GroupBy(p => p.TypeId).Select(g =>
            {
                var first = g.First(); var donor = source(first.SourceLevelKey).Data;
                byte[] tile = UniversalParticleDependencies.Records(donor)[first.TypeId];
                foreach (var other in g.Skip(1))
                {
                    var od = source(other.SourceLevelKey).Data;
                    if (!UniversalParticleDependencies.EquivalentTexture(tile, donor.Vram(), UniversalParticleDependencies.Records(od)[other.TypeId], od.Vram()))
                        throw new InvalidDataException($"Particle dependency 0x{first.TypeId:X2} conflicts between {first.SourceLevelKey} and {other.SourceLevelKey}; particle ID remapping is required.");
                }
                return (Type: first.TypeId, Source: first.SourceLevelKey, Package: UniversalParticleDependencies.TexturePackage(tile), Vram: donor.Vram().ToArray());
            }).ToList();
        var textured = modelJobs.Where(j => BinaryPrimitives.ReadInt32LittleEndian(j.Pkg) >= 0).ToList();
        Dictionary<int, UniversalMetalModelCertificate> metalTextureJobs = [];
        for (int i = 0; i < textured.Count; i++)
            foreach (var (key, certificate) in metalModels)
                if (textured[i].Source == key.Source && textured[i].Class == classes[key].DestinationClass)
                    metalTextureJobs.Add(i, certificate);
        // A rebuilt homeworld's own dragon rescue scenes (subfiles 4 on) carry models textured from the original
        // level's VRAM: their regions come along like an imported model's (NativeDragonRescueCamera.Scenes).
        var rescueScenes = persistentNativePrefixRows != null ? NativeDragonRescueCamera.Scenes(dest) : [];
        byte[] rescueVram = rescueScenes.Count > 0 ? originalDestination.Data.Vram().ToArray() : [];
        var texReports = UniversalAssetImport.ImportTexturesBatch(textured.Select(j => (j.Pkg, j.Vram,
            $"class 0x{j.Class:X3} from {j.Source}{(j.Dependency ? ", a helper object it spawns" : "")}"))
            .Concat(particleJobs.Select(j => (j.Package, j.Vram, $"particle 0x{j.Type:X2} from {j.Source}")))
            .Concat(rescueScenes.Select(r => (r.Package, rescueVram, "a dragon rescue scene"))).ToList(), dest.Vram(), vram, metalTextureJobs);
        foreach (var r in rescueScenes) r.Package.CopyTo(dest.Bytes, r.Offset);
        foreach (var particle in particleJobs)
            UniversalParticleDependencies.AppendTexture(dest, particle.Type, particle.Package);
        foreach (var key in particleDependencies.Keys.ToArray())
            if (particleDependencies[key].Status == "texture-import-required")
            {
                particleDependencies[key] = particleDependencies[key] with { Status = "dependency-unverified",
                    Detail = "Missing source texture record/pixels/palette imported; per-ID spawn/update semantics remain unverified." };
            }
        List<(int, byte[])> pendingModels = [];
        foreach (var (c, asset) in specialClasses.OrderBy(k => k.Key))
        {
            var job = modelJobs.FirstOrDefault(j => j.Class == c);
            UniversalTextureReport? tex = null; UniversalSoundReport? snd = null;
            if (job.Pkg != null)
            {
                int ti = textured.FindIndex(j => j.Class == c);
                if (ti >= 0) { tex = texReports[ti]; snd = UniversalAssetImport.ImportSounds(job.Pkg, asset.SourceData, dest, exe); }
                pendingModels.Add((c, job.Pkg)); destRegistered.Add(c);
            }
            imports.Add(new(c, asset.SourceKey, job.Pkg != null, true, job.Pkg?.Length ?? 0, tex, snd, false));
        }
        foreach (var (c, sk, mapped, dep) in ordered)
        {
            var s = source(sk);
            var sourceSwitch = UniversalDispatchAnalysis.FindUpdateSwitch(s.Overlay);
            bool needBehavior = mapped != c || !DestHandles(mapped) && UniversalDispatchAnalysis.Handles(s.Overlay, sourceSwitch, c);
            var job = modelJobs.FirstOrDefault(j => j.Class == mapped);
            bool needModel = job.Pkg != null && !destRegistered.Contains(mapped);
            if (!needModel && !destRegistered.Contains(mapped) && !shared.Contains(mapped) && !dep)
                warnings.Add($"Class 0x{c:X3} has no model registration in {sk}; it is imported as behavior only.");
            UniversalTextureReport? tex = null; UniversalSoundReport? snd = null; int size = 0;
            if (needModel)
            {
                byte[] pkg = job.Pkg!;
                int ti = textured.FindIndex(j => j.Class == mapped);
                if (ti >= 0) { tex = texReports[ti]; snd = UniversalAssetImport.ImportSounds(pkg, s.Data, dest, exe); }
                size = pkg.Length;
                pendingModels.Add((mapped, pkg));
                destRegistered.Add(mapped);
            }
            imports.Add(new(c, sk, needModel, needBehavior, size, tex, snd, dep) { DestinationClassId = mapped, ModelOrigin = mapped != c && shared.Contains(c) && !s.Data.Registrations().Any(r => r.ClassId == c) ? "verified-shared-pete-simple-untextured-alias" : "source-local-or-resident" });
        }

        Dictionary<string, UniversalWhirlwindSoundBinding> whirlwindSounds = new(StringComparer.OrdinalIgnoreCase);
        foreach (var imported in imports.Where(i => i.ClassId == 0x12C && i.BehaviorImported))
        {
            var donor = source(imported.SourceLevelKey);
            whirlwindSounds.Add(imported.SourceLevelKey, UniversalWhirlwindSoundDependency.Import(
                imported.SourceLevelKey, donor.Data, donor.Overlay, dest, exe));
        }

        if (pendingModels.Count > 0)
        {
            int before = dest.Subfiles()[2].Length;
            if(linkedWorld!=null)linkedWorld.Actors.AppendModels(dest,pendingModels);else dest.AppendModels(pendingModels);
            modelGrowth = dest.Subfiles()[2].Length - before;
        }

        // 3. Placed rows.
        List<(byte[], UniversalRowTemplate)> rows = [];
        // Direct composition owns removal accounting. A caller that completed
        // the ordinary source-row writer must explicitly retain its prior delta.
        int treasure = removedTreasureAlreadyApplied ? 0 : -removedKnownTreasure;
        for (int i = 0; i < placements.Count; i++)
        {
            var p = placements[i]; var t = templates[i];
            if (p.PropertyWordOverrides != null)
            {
                byte[] props = t.Properties.ToArray();
                foreach (var (o, v) in p.PropertyWordOverrides)
                {
                    if (o < 0 || (o & 3) != 0 || o + 4 > props.Length || t.InternalPointers.Any(x => x.Offset == o))
                        throw new InvalidDataException("A property override lies outside the object's own data.");
                    BinaryPrimitives.WriteInt32LittleEndian(props.AsSpan(o), v);
                }
                t = t with { Properties = props };
            }
            if (t.ClassId == NativeDragonRescueCamera.StatueClass && p.SpecialId == null)
                t = t with { Properties = NativeDragonRescueCamera.Move(t.Properties.ToArray(), source(p.SourceLevelKey).Data.Row(p.SourceRowIndex), p.X, p.Y, p.Z, p.Yaw ?? t.Row[0x46]) };
            byte[] r = t.Row.ToArray();
            BinaryPrimitives.WriteInt32LittleEndian(r.AsSpan(0x0C), p.X);
            BinaryPrimitives.WriteInt32LittleEndian(r.AsSpan(0x10), p.Y);
            BinaryPrimitives.WriteInt32LittleEndian(r.AsSpan(0x14), p.Z);
            if (p.Yaw is int yaw) r[0x46] = (byte)yaw;
            r[0x43] = 0xFF; // detach from the source level's pod/group activation table
            if (p.RowByteOverrides != null)
                foreach (var (o, b) in p.RowByteOverrides)
                {
                    if (o is 0x36 or 0x37 && b != r[o])
                        throw new InvalidDataException("Row overrides cannot change the imported class identity.");
                    if (o is > 3 and < 0x58) r[o] = b;
                }
            // A donor sector belongs to its original terrain visibility graph.
            // Even an identical-level clone can be placed in rebuilt terrain.
            // Use the native unrestricted sentinel (also used by every retail
            // Town Square row); stale overrides must not hide an imported actor.
            // Existing destination rows retain their own sector assignments.
            r[0x4A] = 0xFF;
            if (p.DragonColor is { } dragonColor)
            {
                NativeDragonStatueColors.RequirePalette(source(p.SourceLevelKey).Data.Package(0xFA));
                DragonStatueColor.Apply(r, dragonColor);
            }
            if (metalModels.TryGetValue((p.SourceLevelKey.ToLowerInvariant(), t.ClassId), out var metalModel))
                UniversalCliffMetalTextures.RequireRow(metalModel, r);
            if (t.ClassId == 0xB8 && string.Equals(t.SourceLevelKey, "drycanyon", StringComparison.OrdinalIgnoreCase))
                UniversalDryCanyonBirdBranches.RequireInitialRow(r);
            if(UniversalDarkPassageLanternGroups.Applies(t)&&!darkRetainedNativeDogTargets.Contains((t.SourceRowIndex,t.LinkGroupId)))
            { t=darkLanternContexts[t.SourceLevelKey].Prepare(t,r); }
            else if (UniversalDarkPassageShapeshifterOwnership.Applies(t))
            {
                var dogSource = source(p.SourceLevelKey);
                UniversalDarkPassageShapeshifterOwnership.RequirePlacement(destination.Key,dogSource.Data,dogSource.Overlay,t,r,dest);
            }
            if(flightChests.TryGetValue(p.SourceLevelKey,out var chestRowProof))chestRowProof.RequireRow(r,t.Properties,t.ClassId);
            if(flightGates.TryGetValue(p.SourceLevelKey,out var gateRowProof))gateRowProof.RequireRow(r,t.Properties,t.ClassId);
            if(UniversalGreenDruidLinkedRows.Applies(t)&&destination.Key!="alpineridge"){if(linkedWorld==null)throw new InvalidDataException("Linked Druid world installation is missing.");t=linkedWorld.Prepare(t,r);}
            if(UniversalGreenDruidWorldRows.Applies(t)&&destination.Key!="alpineridge"){if(greenWorld==null)throw new InvalidDataException("Green Druid world installation is missing.");t=greenWorld.Prepare(t,r);}
            if(UniversalHighCavesWorldRows.Applies(t)){if(highCavesWorld==null)throw new InvalidDataException("Moving terrain installation is missing.");t=highCavesWorld.Prepare(t,r);}
            if(UniversalHighCavesTornadoPlacement.Applies(t)) { var tornadoSource=source(p.SourceLevelKey);t=UniversalHighCavesTornadoPlacement.Rebase(tornadoSource.Data,tornadoSource.Overlay,t,r); }
            if (UniversalHauntedWizardPlacement.Applies(t))
            {
                UniversalHauntedWizardPlacement.RequireInitialRow(t, r);
                var hauntedSource = source(p.SourceLevelKey);
                t = UniversalHauntedWizardPlacement.Rebase(hauntedSource.Data, hauntedSource.Overlay, t, r);
            }
            if(UniversalHauntedSuperflameFairy.Applies(t)){var fairySource=source(p.SourceLevelKey);t=UniversalHauntedSuperflameFairy.Rebase(fairySource.Data,fairySource.Overlay,t,r);}
            if(UniversalTwilightTrooperPlacement.Applies(t)||t.ClassId is 0x93 or 0x94&&UniversalTwilightTrooperOwnership.SourceFamily(source(p.SourceLevelKey).Overlay))
            { var donor=source(p.SourceLevelKey);UniversalTwilightTrooperPlacement.RequireSourceLabel(p.SourceLevelKey);UniversalTwilightTrooperPlacement.RequireInitialRow(t,r);t=UniversalTwilightTrooperPlacement.Rebase(donor.Data,donor.Overlay,t,r); }
            if(UniversalBeastMistyBoarPlacement.Applies(t))
            { var boarSource=source(p.SourceLevelKey);t=UniversalBeastMistyBoarPlacement.Rebase(boarSource.Data,boarSource.Overlay,t,r); }
            if (p.SpecialId == null)
            {
                // Count the actual authored bytes while class still identifies
                // the donor's native gem. Namespace aliases are renderer/behavior
                // identities, not treasure-value encodings.
                treasure = checked(treasure + TreasureValue(r));
                BinaryPrimitives.WriteUInt16LittleEndian(r.AsSpan(0x36), (ushort)classes[(p.SourceLevelKey.ToLowerInvariant(), t.ClassId)].DestinationClass);
            }
            else if (UniversalHighValueGems.IsId(p.SpecialId))
                treasure = checked(treasure + UniversalHighValueGems.ValueForClass(t.ClassId));
            else if (UniversalCrashBoxes.IsId(p.SpecialId))
                treasure = checked(treasure + UniversalCrashBoxes.TreasureValue(t.ClassId));
            else if (UniversalCrashEnemies.IsId(p.SpecialId))
                treasure = checked(treasure + UniversalCrashEnemies.TreasureValue(t.ClassId));
            rows.Add((r, t));
        }
        foreach(var context in darkLanternContexts.Values)context.RequireGroups(rows);
        UniversalHighCavesTornadoPlacement.RequireGroups(rows);
        UniversalTreasureAccounting.ApplyDelta(exe, destination.LevelId, treasure);
        int sceneBefore = dest.Subfiles()[3].Length;
        UniversalIndexedRows.RequireNativeRemoval(destination.Key, dest, originalDestination.Data, originalDestination.Overlay, permanentlyRemoved);
        var allocation = UniversalRowRemoval.Allocate(dest, permanentlyRemoved, rows.Count);
        for (int i = 0; i < rows.Count; i++)
            if (rows[i].Item2.SourceLevelKey == "hauntedtowers" && rows[i].Item2.ClassId == 0xCC)
                UniversalHauntedWizardParticles.RequirePhysicalWizardRow(allocation.NewRows[i]);
        if(linkedWorld!=null){linkedWorld.RequireAllocation(allocation.NewRows);linkedWorld.Actors.RequireTranslatedRows(rows,linkedWorld.Storage.DeltaRaw,allocation.NewRows);}
        greenWorld?.RequireAllocation(allocation.NewRows);
        highCavesWorld?.RequireAllocation(allocation.NewRows);
        int droppedProperties = UniversalRowRemoval.RemoveOwnedPropertiesAndClearRows(dest, permanentlyRemoved);
        var (_, sceneGrowth, newRows) = dest.AppendRows(rows, permanentlyRemoved);
        if(magicPair!=null){magicPair.AppendPod(dest,newRows);sceneGrowth=dest.Subfiles()[3].Length-sceneBefore;}
        if(magicChain!=null){magicChain.AppendPod(dest,rows,newRows);sceneGrowth=dest.Subfiles()[3].Length-sceneBefore;}
        if(magicOriginal!=null){magicOriginal.AppendPairPod(dest,newRows);sceneGrowth=dest.Subfiles()[3].Length-sceneBefore;}
        int[] physicalSaveRows = Enumerable.Range(0, allocation.FinalRowCount).Select(i => permanentlyRemoved.Contains(i) ? -1 : i).ToArray();
        for (int i = 0; i < newRows.Count; i++) physicalSaveRows[newRows[i]] = saveRows[i];
        bool remapSaveRows = physicalSaveRows.Where((logical, physical) => logical != physical).Any();

        List<UniversalOwnedParticleCertificate> ownedParticles=[];
        if(greenWorld!=null){UniversalGreenDruidWorldSpellParticles.RequireTexture(greenDonor.Data!,dest);ownedParticles.Add(greenWorld.Dispatcher());particleDependencies[("alpineridge",8)]=new("alpineridge",8,"source-certified-copied",greenWorld.HasSpellTarget?"Original live-model vertex spell and physical target; native gameplay pending.":"Original no-target druid; complete copied particle owner retained, native target−1 skips emission; native gameplay pending.");}
        if(linkedWorld!=null){linkedWorld.Actors.RequireParticles(dest);ownedParticles.Add(linkedWorld.Dispatcher());foreach(int type in new[]{5,8})particleDependencies[("alpineridge",type)]=new("alpineridge",type,"source-certified-copied","Complete original linked Druid effects and actual physical targets; native gameplay pending.");}
        Dictionary<string,UniversalHighCavesSpellParticleCertificate> highCavesSpellParticles=new(StringComparer.OrdinalIgnoreCase);
        if(imports.Any(i=>i.SourceLevelKey=="highcaves"&&i.ClassId==0x127&&i.BehaviorImported)){
            var donor=source("highcaves");var proof=UniversalHighCavesSpellParticles.Capture("highcaves",destination.Key,destination.LevelId,donor.Data,donor.Overlay,dest,destOverlay,exe);
            UniversalHighCavesSpellParticles.RequireTextures(proof,donor.Data,dest);highCavesSpellParticles.Add("highcaves",proof);if(highCavesWorld!=null){ownedParticles.Add(highCavesWorld.Dispatcher(proof));UniversalHighCavesWorldSpellParticles.RequireTexture(donor.Data,dest);particleDependencies[("highcaves",8)]=new("highcaves",8,"source-certified-copied","Original live-model vertex spell, physical scenery target and65-tick/orbit/drawn lifetime. Native gameplay remains pending.");}else if(proof.OwnedDispatcher is {} dispatch)ownedParticles.Add(dispatch);particleDependencies[("highcaves",5)]=UniversalHighCavesSpellParticles.Inspect(proof);
        }

        if(destination.Key=="darkhollow" && imports.Any(i=>i.SourceLevelKey=="twilightharbor"&&i.ClassId is 0x93 or 0x94))
        {
            var donor=source("twilightharbor");var issued=UniversalTwilightTrooperParticles.Capture(donor.Data,donor.Overlay,destOverlay,exe,imports.Any(i=>i.SourceLevelKey=="twilightharbor"&&i.ClassId==0xB0));
            if(issued.OwnedDispatcher is {} dispatcher)ownedParticles.Add(dispatcher);
            UniversalTwilightTrooperParticles.RequireTextures(issued,donor.Data,dest);
        }
        if(destination.Key is "peacekeepers" or "darkhollow" && placements.Any(p=>p.SourceLevelKey=="hauntedtowers" && source(p.SourceLevelKey).Data.CaptureRow(p.SourceLevelKey,p.SourceRowIndex).ClassId==0xCC))
        {
            var donor=source("hauntedtowers");
            ownedParticles.Add(hauntedFairy?UniversalHauntedSuperflameEffects.Capture(donor.Data,donor.Overlay,destOverlay,exe):UniversalHauntedWizardParticles.Capture(donor.Data,donor.Overlay,destOverlay,exe,destination.Key));
            UniversalHauntedWizardParticles.RequireDestinationTexture(donor.Data,dest);
            if(destination.Key=="darkhollow"){UniversalHauntedWizardDarkHollowEffects.RequireTextures(donor.Data,dest);foreach(int type in new[]{1,70})particleDependencies[("hauntedtowers",type)]=UniversalHauntedWizardDarkHollowEffects.Inspect(type);}
        }
        if(hauntedFairy){UniversalHauntedSuperflameEffects.RequireTextures(source("hauntedtowers").Data,dest);particleDependencies[("hauntedtowers",66)]=new("hauntedtowers",66,"source-certified-native-equivalent","Original native untextured sparkle, movement, RNG and expiry; native gameplay remains pending.");}
        Dictionary<string,UniversalDarkPassageProjectileParticleCertificate> darkProjectileParticles=new(StringComparer.OrdinalIgnoreCase);
        if(imports.Any(i=>i.SourceLevelKey=="darkpassage" && i.ClassId==0x9B && i.BehaviorImported))
        {
            var donor=source("darkpassage");var certificate=UniversalDarkPassageProjectileParticles.Capture("darkpassage",destination.Key,donor.Data,donor.Overlay,destOverlay,exe);
            UniversalDarkPassageProjectileParticles.RequireTextures(certificate,donor.Data,dest);darkProjectileParticles.Add("darkpassage",certificate);
            if(certificate.OwnedDispatcher is {} dispatch)ownedParticles.Add(dispatch);
            foreach(int type in new[]{1,13})particleDependencies[("darkpassage",type)]=UniversalDarkPassageProjectileParticles.Inspect(certificate,type);
        }
        // Canonical native gem rewards retain the destination star manager. This does
        // not certify unrelated copied callbacks that also request particle 12.
        var collectionStars = classes.Keys.Where(k => k.Class is >= 0x53 and <= 0x57)
            .Select(k => k.Source)
            // A Crash crate's gem is spawned by the destination's own reward path, like its native enemies' gems.
            .Concat(rows.Where(r => r.Item1[0x53] is >= 0x53 and <= 0x57 && r.Item2.SourceLevelKey != UniversalCrashBoxes.SourceKey)
                .Select(r => r.Item2.SourceLevelKey))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(key =>
            {
                var donor = source(key);
                return UniversalCollectionStarParticles.Capture(key, donor.Data, donor.Overlay,
                    dest, destOverlay, exe, destination.LevelId);
            }).ToArray();
        UniversalCollectionStarContinuation? collectionStarContinuation = null;
        NativeFreshParticleBinding? freshParticleBinding = null;
        // 4. Behavior bundle chained onto the destination's own callbacks.
        List<UniversalBehaviorSourceReport> behavior = [];
        IReadOnlyList<UniversalDestinationGemCall> destinationGemCalls = [];
        int destinationGemBytes = 0;
        IReadOnlyList<UniversalDestinationGemColourLoad> destinationColourLoads = [];
        int destinationColourBytes = 0;
        IReadOnlyList<UniversalNativeParticleNoOpProof> particleNoOpProofs = [];
        byte[] overlay = destinationOverlay.ToArray();
        uint copyBuffer = destOverlay.CopyBuffer;
        foreach(var context in darkLanternContexts.Values)context.RequireDestinationData(dest);
        var byBehaviorSource = imports.Where(i => i.BehaviorImported && !specialClasses.ContainsKey(i.DestinationClassId)).GroupBy(i => i.SourceLevelKey)
            .Select(g => (g.Key, source(g.Key).Overlay, (IReadOnlyList<int>)g.Select(i => i.ClassId).Order().ToList())).ToList();
        var eggClasses = specialClasses.Values.Where(a => a.Behavior == UniversalSpecialBehavior.DragonflyEgg).Select(a => a.ClassId).Order().ToList();
        var signClasses = specialClasses.Values.Where(a => a.Behavior == UniversalSpecialBehavior.Sign).Select(a => a.ClassId).Order().ToList();
        var highValueGemClasses = specialClasses.Values.Where(a => a.Behavior == UniversalSpecialBehavior.HighValueGem).Select(a => a.ClassId).Order().ToList();
        var crashBoxClasses = specialClasses.Values.Where(a => a.Behavior == UniversalSpecialBehavior.CrashBox).Select(a => a.ClassId).Order().ToList();
        var crashEnemyClasses = specialClasses.Values.Where(a => a.Behavior == UniversalSpecialBehavior.CrashEnemy).Select(a => a.ClassId).Order().ToList();
        var crashPlayerClasses = specialClasses.Values.Where(a => a.Behavior == UniversalSpecialBehavior.CrashPlayer).Select(a => a.ClassId).Order().ToList();
        // Crash enemies call the same engine routines and particles as the boxes.
        if (crashBoxClasses.Count > 0 || crashEnemyClasses.Count > 0) UniversalCrashBoxes.RequireDestination(destOverlay, exe);
        if (highValueGemClasses.Count > 0)
        {
            UniversalHighValueGems.RequireNativeOwner(exe);
            UniversalHighValueGems.RequireDestinationParticleOwner(destOverlay);
        }
        if (releasedNativeClasses is { Count: > 0 } && !releasedNativeClasses.All(c => imports.Any(i => i.BehaviorImported && i.DestinationClassId == c &&
                string.Equals(i.SourceLevelKey, destination.Key, StringComparison.OrdinalIgnoreCase))))
            throw new InvalidDataException("A released class has no imported behavior to run it.");
        if (byBehaviorSource.Count > 0 || eggClasses.Count > 0 || signClasses.Count > 0 || highValueGemClasses.Count > 0 || crashBoxClasses.Count > 0 || crashEnemyClasses.Count > 0 || crashPlayerClasses.Count > 0)
        {
            uint baseAddress = checked(((copyBuffer + 7) & ~7u) + (uint)magicPairBundlePaddingBytes);
            var classMaps = classes.GroupBy(k => k.Key.Source).ToDictionary(g => g.Key,
                g => (IReadOnlyDictionary<int, int>)g.ToDictionary(k => k.Key.Class, k => k.Value.DestinationClass));
            var bundle = UniversalBehaviorBundleBuilder.Build(destOverlay, byBehaviorSource, baseAddress, eggClasses, signClasses, classMaps, remapSaveRows, nativeExecutable: exe,
                nativeDestinationOwner: nativeDestinationOwner ?? originalDestination.Overlay, highValueGemClasses: highValueGemClasses, whirlwindSounds: whirlwindSounds, dryCanyonBirdBranches: dryCanyonBirdBranches,flightChests:flightChests,flightGates:flightGates,ownedParticles:ownedParticles,magicPair:magicPair,darkLanternContexts:darkLanternContexts,darkProjectileParticles:darkProjectileParticles,magicChain:magicChain,highCavesSpellParticles:highCavesSpellParticles,magicOriginal:magicOriginal,highCavesWorld:highCavesWorld,greenDruidWorld:greenWorld,greenDruidLinkedWorld:linkedWorld,crashBoxClasses:crashBoxClasses,crashEnemyClasses:crashEnemyClasses,crashPlayerClasses:crashPlayerClasses);
            int releaseBytes = releasedNativeClasses is { Count: > 0 } ? UniversalNativeRelease.Bytes(releasedNativeClasses.Count) : 0;
            uint releaseStub = (uint)((baseAddress + bundle.Code.Length + 3) & ~3);
            uint newCopy = (uint)((releaseStub + releaseBytes + 15) & ~15);
            int keep = (int)(copyBuffer - UniversalOverlay.Base);
            byte[] ov = new byte[UniversalLevelData.Align((int)(newCopy - UniversalOverlay.Base), 0x800)];
            destinationOverlay.AsSpan(0, keep).CopyTo(ov);
            bundle.Code.CopyTo(ov, (int)(baseAddress - UniversalOverlay.Base));
            if (releaseBytes > 0) UniversalNativeRelease.Install(ov, destOverlay, releasedNativeClasses!, releaseStub);
            UniversalDestinationGemCollectionAdapter.Apply(ov, bundle.DestinationGemCollectionCalls);
            UniversalDestinationGemColourAdapter.Apply(ov, bundle.DestinationGemColourLoads);
            destinationColourLoads = bundle.DestinationGemColourLoads;
            destinationColourBytes = bundle.DestinationGemColourExtraBytes;
            particleNoOpProofs = bundle.NativeParticleNoOpProofs;
            collectionStarContinuation = bundle.CollectionStarContinuation;
            destinationGemCalls = bundle.DestinationGemCollectionCalls;
            destinationGemBytes = bundle.DestinationGemCollectionExtraBytes;
            overlay = ov;
            if (bundle.OwnedParticleUpdateWrapper is uint particleUpdate)
            {
                if(destination.LevelId==65)
                {
                    if(nativeOriginalExecutable==null||collectionStarContinuation==null)throw new InvalidDataException("Fresh copied particles need their original executable and issued updater chain.");
                    freshParticleBinding=NativePortalRouteFreshBinding.RebindOwnedParticleContinuation(nativeOriginalExecutable,exe,
                        new UniversalOverlay(overlay,bindings),collectionStarContinuation,bundle.UpdateWrapper,bundle.InitWrapper,newCopy);
                }
                else UniversalExeBindings.Patch(exe, bindings[UniversalExeBindings.ParticleUpdateGlobal], particleUpdate);
                foreach (var certificate in ownedParticles)
                    foreach (int type in certificate.TypeIds)
                        particleDependencies[(certificate.SourceLevelKey, type)] = certificate.SourceLevelKey=="darkpassage" && darkProjectileParticles.TryGetValue(certificate.SourceLevelKey,out var projectileEffect)
                            ? UniversalDarkPassageProjectileParticles.Inspect(projectileEffect,type)
                            : new(certificate.SourceLevelKey, type,"source-certified-copied", "Uses the authenticated source effect, translated actor links and relocated texture; the destination particle manager runs once.");
            }
            if(freshParticleBinding==null)
            {
                UniversalExeBindings.Patch(exe, bindings[UniversalExeBindings.UpdateGlobal], bundle.UpdateWrapper);
                UniversalExeBindings.Patch(exe, bindings[UniversalExeBindings.InitGlobal], bundle.InitWrapper);
                UniversalExeBindings.Patch(exe, bindings[UniversalExeBindings.CopyBufferGlobal], newCopy);
            }
            copyBuffer = newCopy;
            behavior.AddRange(bundle.Sources);
        }

        if (remapSaveRows)
        {
            var mappedBindings = UniversalExeBindings.Read(exe, destination.LevelId);
            uint update = mappedBindings[UniversalExeBindings.UpdateGlobal].Value;
            var mapped = NativeMobySaveMap.Append(destOverlay, overlay, update, copyBuffer, physicalSaveRows);
            overlay = mapped.Overlay; copyBuffer = mapped.CopyBuffer;
            if(freshParticleBinding!=null)
                freshParticleBinding=NativePortalRouteFreshBinding.RebindOwnedParticleContinuation(nativeOriginalExecutable!,exe,
                    new UniversalOverlay(overlay,mappedBindings),collectionStarContinuation!,mapped.UpdatePointer,mappedBindings[UniversalExeBindings.InitGlobal].Value,copyBuffer);
            else
            {
                UniversalExeBindings.Patch(exe, mappedBindings[UniversalExeBindings.UpdateGlobal], mapped.UpdatePointer);
                UniversalExeBindings.Patch(exe, mappedBindings[UniversalExeBindings.CopyBufferGlobal], copyBuffer);
            }
            NativeMobySaveMap.PatchExe(exe);
        }

        // Enemy collision masks can select level-local Spyro hurt animations. They are
        // separate from the enemy model and must travel with the dependency closure.
        List<UniversalPlayerAnimationImport> playerAnimationImports = [];
        foreach (var donorGroup in ordered.Where(o => nativeDestinationOwner != null || !string.Equals(o.Source, destination.Key, StringComparison.OrdinalIgnoreCase))
            .GroupBy(o => o.Source))
        {
            var donor = source(donorGroup.Key);
            var registered = donor.Data.Registrations().Select(r => r.ClassId).ToHashSet();
            int[] required = donorGroup.Where(o => registered.Contains(o.Class))
                .SelectMany(o => UniversalPlayerDamageDependencies.Analyze(donor.Data.Package(o.Class), exe).AnimationIds)
                .Concat(UniversalPlayerDamageDependencies.MapDamageMask(behavior.Where(b => b.SourceLevelKey == donorGroup.Key).Aggregate(0u, (mask, b) => mask | b.PlayerDamageMask), exe))
                .Distinct().Order().ToArray();
            if (required.Length == 0) continue;
            var animations = playerAnimationSource?.Invoke() ?? throw new InvalidDataException(
                "These objects need additional Spyro animations. Select the unchanged original USA disc to include them.");
            var localIds = animations.Levels.Single(l => l.LevelId == donor.Level.LevelId)
                .Stream.Records.Select(r => r.AnimationId).ToHashSet();
            // A few original enemy reaction masks reference a player record absent
            // from their own level. Resolve only independently certified native
            // donors, retaining the actual donor identity in the export report.
            var reactionDonors = required.Select(id => (Id: id, LevelId:
                localIds.Contains(id) || animations.SharedAnimationIds.Contains(id)
                    ? donor.Level.LevelId
                    : UniversalPlayerReactionDonors.ResolveMissingDonor(donorGroup.Key, donor.Level.LevelId, id, animations)
                        ?? donor.Level.LevelId));
            foreach (var reactionGroup in reactionDonors.GroupBy(r => r.LevelId))
            {
                string reactionSource = reactionGroup.Key == donor.Level.LevelId ? donorGroup.Key :
                    reactionGroup.Key == 12 ? "darkhollow" : throw new InvalidDataException("Unknown certified Spyro reaction donor.");
                var merged = UniversalPlayerAnimationImporter.Merge(dest, reactionSource, reactionGroup.Key,
                    reactionGroup.Select(r => r.Id).ToArray(), animations, exe);
                dest = merged.Data;
                modelGrowth += merged.Report.ModelGrowthBytes;
                playerAnimationImports.Add(merged.Report);
            }
        }

        if(rows.Any(r=>UniversalHighCavesTornadoPlacement.Applies(r.Item2))) { var tornadoSource=source(UniversalHighCavesTornadoPlacement.Key);UniversalHighCavesTornadoPlacement.RequireDestination(tornadoSource.Data,tornadoSource.Overlay,dest,rows,newRows); }
        foreach(var proof in playerAnimationImports.SelectMany(p=>p.NativeHeaderProofs))UniversalHighCavesTornadoReaction.RequireDestination(proof,dest,exe);
        UniversalNativeParticleNoOpOwnership.RequireFinalDestination(particleNoOpProofs,
            new UniversalOverlay(overlay, UniversalExeBindings.Read(exe, destination.LevelId)), exe);
        if (highValueGemClasses.Count > 0)
            UniversalHighValueGems.RequireDestinationParticleOwner(new UniversalOverlay(overlay, UniversalExeBindings.Read(exe, destination.LevelId)));
        magicChain?.RequireFinalRows(dest,newRows);magicOriginal?.RequireFinalRows(dest,newRows);
        magicChain?.RequireFinalOverlay(overlay);magicOriginal?.RequireFinalOverlay(overlay);
        magicChain?.RequireFinalExecutable(exe);magicOriginal?.RequireFinalExecutable(exe);
        magicPair?.PrepareArtisansRescueModels(dest);
        magicPair?.RequireFinalRows(dest,newRows);
        magicPair?.RequireFinalOverlay(overlay);
        magicPair?.RequireFinalExecutable(exe);
        foreach (var binding in whirlwindSounds.Values) UniversalWhirlwindSoundDependency.RequireDestination(binding, dest);
        foreach (var binding in metalBindings) UniversalCliffMetalTextures.RequireDestination(binding, dest);
        superflameScene?.Require(dest);
        foreach (var key in classes.Keys.Where(k => k.Class == 0xB8 && k.Source == "drycanyon"))
            UniversalDryCanyonBirdBranches.RequireAnimationFrames(dest.Package(classes[key].DestinationClass));
        foreach (var (key, certificate) in metalModels)
            certificate.RequireShaderFields(dest.Package(classes[key].DestinationClass));
        if(flightGate!=null)flightGate.RequireFinal(dest,overlay,newRows.Single(),classes[(flightGate.SourceKey,0x161)].DestinationClass);
        if(flightChest!=null)flightChest.RequireFinal(dest,overlay,newRows.Single(),classes[(flightChest.SourceKey,0x12B)].DestinationClass);
        linkedWorld?.RequireFinal(dest,new(overlay,UniversalExeBindings.Read(exe,destination.LevelId)),exe,rows,newRows);
        greenWorld?.RequireFinal(dest,new(overlay,UniversalExeBindings.Read(exe,destination.LevelId)),exe,rows,newRows);
        highCavesWorld?.RequireFinal(dest,new(overlay,UniversalExeBindings.Read(exe,destination.LevelId)),exe,rows,newRows);
        // A dragon rescue streams into the model area below the statue. When removed native models leave too little
        // room there, imported models move below the statue too; the game reloads that area after every rescue.
        // Compositions whose model layout is certified exactly above keep it and are refused instead.
        bool certifiedLayout = magicChain != null || magicOriginal != null || magicPair != null || linkedWorld != null ||
            greenWorld != null || highCavesWorld != null || flightGate != null || flightChest != null;
        if (!certifiedLayout &&
            UniversalNativeDragonRescuePrefix.Measure(destination.Key, destination.DisplayName, originalDestination.Data, dest, exe) is { Deficit: > 0 })
        {
            var original = originalDestination.Data.Registrations();
            int originalStatue = original.Single(r => r.ClassId == 250).Root, statue = dest.Registrations().Single(r => r.ClassId == 250).Root;
            var nativeTail = original.Where(r => r.Root > originalStatue).Select(r => r.ClassId).ToHashSet();
            modelGrowth += dest.MoveModelsBefore(250, dest.Registrations().Where(r => r.Root > statue && !nativeTail.Contains(r.ClassId)).Select(r => r.ClassId).ToArray());
            // A rebuilt homeworld may hold too few models to fill the area: the rest is reserved as empty room there.
            if (persistentNativePrefixRows != null &&
                UniversalNativeDragonRescuePrefix.Measure(destination.Key, destination.DisplayName, originalDestination.Data, dest, exe) is { Deficit: > 0 } still)
                modelGrowth += dest.ReserveBefore(250, UniversalLevelData.Align(still.Deficit, 0x800));
        }
        UniversalNativeDragonRescuePrefix.RequireSafe(destination.Key,destination.DisplayName,originalDestination.Data,dest,exe);
        uint limit = NormalBufferFloor - RamSafetyMargin;
        // Objects added to a retail level take their rows out of its spare object slots, which every gem or butterfly
        // an enemy or crate drops and every projectile needs while it exists (the allocator checks no end). Grow the
        // arena back to the level's own retail reserve where game RAM allows. Built levels prepare their arena before
        // composition, and layouts certified exactly above keep theirs.
        bool retailEdit = persistentNativePrefixRows == null && nativeRowsBefore > 0 && !certifiedLayout;
        int retailSlots = retailEdit ? ArenaSlots(originalDestination.Data) : 0, slotsAdded = 0;
        if (retailEdit && placements.Count > 0)
            (dest, slotsAdded) = RestoreDynamicSlots(dest, retailSlots, (long)limit - RuntimeEnd(dest, copyBuffer));
        uint finalEnd = RuntimeEnd(dest, copyBuffer);
        var ram = new UniversalRamBudget(originalEnd, finalEnd, NormalBufferFloor,
            (int)(NormalBufferFloor - originalEnd), (int)((long)NormalBufferFloor - finalEnd),
            (int)(copyBuffer - destOverlay.CopyBuffer), modelGrowth, dest.Subfiles()[3].Length - sceneBefore,
            (int)(finalEnd - originalEnd) - (int)(copyBuffer - destOverlay.CopyBuffer) - modelGrowth - (dest.Subfiles()[3].Length - sceneBefore));
        bool nativeRemovalBudget = PreservesNativeRemovalBudget(permanentlyRemoved.Count, placements.Count, ram.OverlayGrowth,
            originalEnd, RuntimeEnd(originalDestination.Data, originalDestination.Overlay.CopyBuffer), finalEnd);
        if (finalEnd > limit && !nativeRemovalBudget)
            throw new UniversalBudgetException(
                $"{destination.DisplayName} does not have enough game RAM for these objects: they need {(finalEnd - originalEnd) / 1024.0:0.0} KB " +
                $"but the level has {Math.Max(0, (int)(limit - originalEnd)) / 1024.0:0.0} KB available." +
                (playerAnimationImports.Sum(p => p.ModelGrowthBytes) > 0
                    ? $" This includes {playerAnimationImports.Sum(p => p.ModelGrowthBytes) / 1024.0:0.0} KB for Spyro's reactions to these enemies." : "") +
                (specialClasses.Values.Any(a => a.Behavior == UniversalSpecialBehavior.CrashPlayer)
                    ? $" Playing it as Crash takes about {specialClasses.Values.Where(a => a.Behavior == UniversalSpecialBehavior.CrashPlayer).Sum(a => UniversalLevelData.Align(a.Package.Length, 0x800)) / 1024.0:0} KB of that: untick it under Play as Crash, or clear the level with Start Blank to make room." : ""), ram);
        int arena = ArenaSlots(dest);
        if (arena < MinimumDynamicSlots)
            throw new InvalidDataException($"Only {arena} dynamic object slots would remain in {destination.DisplayName}; at least {MinimumDynamicSlots} are required.");
        if (retailEdit && arena < retailSlots)
        {
            int drops = DropCapableRows(dest), required = RequiredDynamicSlots(retailSlots, drops);
            if (arena < required)
                throw new InvalidDataException(
                    $"Only {arena} spare object slots would remain in {destination.DisplayName}, and its {drops} enemies and crates that " +
                    $"drop something need at least {required}. The game puts every dropped gem or butterfly and every projectile in " +
                    $"one of these slots and does not check for the last one, so more would crash it. There is not enough game " +
                    $"memory left in this level for more slots: remove some of the objects added to {destination.DisplayName}.");
            warnings.Add($"{destination.DisplayName} keeps {arena} of its {retailSlots} spare object slots (game memory is short); " +
                $"its {drops} enemies and crates that drop something need {required}.");
        }
        int[] mappedRows = Enumerable.Repeat(-1, incomingCount).ToArray(), mappedSaveRows = Enumerable.Repeat(-1, incomingCount).ToArray();
        for (int i = 0; i < newRows.Count; i++) { mappedRows[incomingOrder[i]] = newRows[i]; mappedSaveRows[incomingOrder[i]] = saveRows[i]; }
        if (placements.Any(p => p.DragonColor != null)) NativeDragonStatueColors.Install(exe);
        nativeLine77?.RequireDestination(new UniversalOverlay(overlay,UniversalExeBindings.Read(exe,destination.LevelId)),exe);
        if(freshParticleBinding!=null)NativePortalRouteFreshBinding.RequireOwnedParticleDestination(nativeOriginalExecutable!,exe,
            new UniversalOverlay(overlay,UniversalExeBindings.Read(exe,65)),freshParticleBinding);
        foreach(var proof in highCavesSpellParticles.Values){particleDependencies[("highcaves",5)]=UniversalHighCavesSpellParticles.Inspect(proof);UniversalHighCavesSpellParticles.RequireFinal(proof,source("highcaves").Data,dest,new UniversalOverlay(overlay,UniversalExeBindings.Read(exe,destination.LevelId)),exe,collectionStarContinuation);}
        foreach (var certificate in collectionStars)
            UniversalCollectionStarParticles.RequireFinalDestination(certificate, dest,
                new UniversalOverlay(overlay, UniversalExeBindings.Read(exe, destination.LevelId)),
                exe, collectionStarContinuation);
        foreach (var particle in particleDependencies.Values.Where(p => p.Status == "dependency-unverified"))
            warnings.Add($"Particle 0x{particle.TypeId:X2} from {particle.SourceLevelKey}: {particle.Detail}");
        // Playable Crash: Spyro's drawing routine is hooked so he stays hidden in cutscenes too (inert elsewhere).
        if (specialClasses.Values.Any(a => a.Behavior == UniversalSpecialBehavior.CrashPlayer)) UniversalCrashPlayer.PatchExe(exe);
        return new(destination.Key, imports, mappedRows, ram, arena, freeBefore - vram.FreeBytes, behavior, warnings,
            treasure, overlay, dest.Bytes, exe) { ArenaSlotsAdded = slotsAdded, DroppedModelClasses = dropped.Order().ToList(), DroppedModelBytes = droppedBytes, ReclaimedVramBytes = reclaimedVram, ImportedVramRanges = vram.ImportedReservations, ParticleDependencies = particleDependencies.Values.ToList(), CanonicalGemCollectionProofs = collectionStars, FreshParticleBinding = freshParticleBinding, NativeLineParticleProofs=nativeLine77==null?[]:[nativeLine77.Proof], FlightChestParticleProofs = flightParticleProofs.Values.ToArray(), FlightGateParticleProofs = gateParticleProofs.Values.ToArray(), RestoredObjectLinks = templates.Sum(t => t.ImportedRowPointers.Count), RestoredIndexedObjectLinks = templates.Sum(t => t.ImportedRowIndices.Count), UnresolvedObjectLinks = templates.Sum(t => t.DroppedExternalPointers), PlacementSaveRows = mappedSaveRows, PhysicalToLogicalSaveRows = physicalSaveRows, UnoccupiedPhysicalRows = allocation.UnoccupiedRows, NativeRowsBefore = nativeRowsBefore, NativeRowsAfter = allocation.FinalRowCount, ReusedPhysicalRows = allocation.ReusedRows, DroppedPropertyBytes = droppedProperties, PlayerAnimationImports = playerAnimationImports, WhirlwindSoundImports = whirlwindSounds.Values.ToArray(), MetalTextureBindings = metalBindings, DestinationGemCollectionCalls = destinationGemCalls, DestinationGemCollectionExtraBytes = destinationGemBytes, DestinationGemColourLoads = destinationColourLoads, DestinationGemColourExtraBytes = destinationColourBytes, NativeParticleNoOpProofs = particleNoOpProofs };
    }

    /// <summary>Class a fully removed native row is switched to: no model, no behavior in any level, never
    /// spawned. Unfilled interior holes keep their native physical index and carry save-map sentinel −1.</summary>
    public const ushort InertClass = 0x1FE;

    /// <summary>Locally registered classes whose every placed row was removed by the user and that nothing can
    /// still create: not shared, not imported/placed/special, not spawned by any code in the destination
    /// overlay or the executable, and not named in any remaining row's properties (spawner data).</summary>
    public static (List<int> Classes, List<int> InertRows) DroppableClasses(UniversalLevelData dest, UniversalOverlay destOverlay,
        byte[] exe, IReadOnlySet<int> shared, IReadOnlyCollection<int>? removedRows, IReadOnlySet<int> needed)
    {
        if (removedRows is not { Count: > 0 }) return ([], []);
        var sc = dest.Scene();
        var removed = removedRows.Where(r => r >= 0 && r < sc.RowCount).ToHashSet();
        Dictionary<int, List<int>> rowsByClass = [];
        for (int i = 0; i < sc.RowCount; i++)
        {
            int c = BinaryPrimitives.ReadUInt16LittleEndian(dest.Row(i).AsSpan(0x36));
            if (!rowsByClass.TryGetValue(c, out var l)) rowsByClass[c] = l = [];
            l.Add(i);
        }
        var registered = dest.Registrations().Select(r => r.ClassId).ToHashSet();
        if (registered.Contains(InertClass) || shared.Contains(InertClass) || rowsByClass.ContainsKey(InertClass)) return ([], []);
        var candidates = rowsByClass.Where(k => registered.Contains(k.Key) && !shared.Contains(k.Key) && !needed.Contains(k.Key) &&
            k.Value.All(removed.Contains)).Select(k => k.Key).ToHashSet();
        if (candidates.Count == 0) return ([], []);
        // Spawned by code: the whole destination overlay and the executable (spawns go through the init global).
        SortedSet<uint> all = [];
        for (uint a = destOverlay.CodeStart; a + 4 <= UniversalOverlay.Base + (uint)destOverlay.Bytes.Length; a += 4) all.Add(a);
        candidates.ExceptWith(UniversalDispatchAnalysis.Dependencies(destOverlay, all).SpawnedClasses);
        candidates.ExceptWith(ExecutableSpawnedClasses(exe));
        // Named by a remaining row's properties (generators/spawners keep their data there).
        for (int i = 0; i < sc.RowCount && candidates.Count > 0; i++)
        {
            if (removed.Contains(i)) continue;
            byte[] row = dest.Row(i);
            int props = BinaryPrimitives.ReadInt32LittleEndian(row);
            if (props <= 0) continue;
            int start = dest.Subfiles()[3].Offset + props, end = Math.Min(start + 0x80, dest.Bytes.Length - 2); // scene-relative; blocks are small
            for (int p = start; p < end; p += 2) candidates.Remove(BinaryPrimitives.ReadUInt16LittleEndian(dest.Bytes.AsSpan(p)));
        }
        var inert = candidates.SelectMany(c => rowsByClass[c]).Order().ToList();
        return (candidates.ToList(), inert);
    }

    private static readonly Dictionary<string, HashSet<int>> ExeSpawnCache = [];

    /// <summary>Class constants passed in a0 to calls through the init/spawn global (0x800758CC) in the EXE.</summary>
    public static HashSet<int> ExecutableSpawnedClasses(byte[] exe)
    {
        string key = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(exe));
        lock (ExeSpawnCache) if (ExeSpawnCache.TryGetValue(key, out var hit)) return hit;
        HashSet<int> result = [];
        int n = exe.Length / 4;
        uint W(int i) => BinaryPrimitives.ReadUInt32LittleEndian(exe.AsSpan(4 * i));
        for (int i = 0x800 / 4; i < n; i++)
        {
            uint w = W(i);
            if (!UniversalMips.IsJalr(w)) continue;
            int rs = UniversalMips.Rs(w);
            bool viaInit = false;
            for (int b = i - 1; b > i - 8 && b > 0; b--)
            {
                uint w2 = W(b);
                if (UniversalMips.Op(w2) == 35 && UniversalMips.Rt(w2) == rs)
                {
                    viaInit = UniversalMips.SImm(w2) == unchecked((short)(UniversalExeBindings.InitGlobal & 0xFFFF));
                    break;
                }
            }
            if (!viaInit) continue;
            for (int b = i + 1; b > i - 14 && b > 0; b--)
            {
                uint w3 = W(b);
                if (UniversalMips.Rt(w3) == 4 && UniversalMips.Rs(w3) == 0 && UniversalMips.Op(w3) is 9 or 13) { result.Add(UniversalMips.Imm(w3)); break; }
            }
        }
        lock (ExeSpawnCache) ExeSpawnCache[key] = result;
        return result;
    }
}

public sealed class UniversalBudgetException(string message, UniversalRamBudget budget) : IOException(message)
{
    public UniversalRamBudget Budget { get; } = budget;
}
