using System.Buffers.Binary;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Analysis;

public enum NativeMobyCatalogueView { PlacedRows, Resources, All }
public enum NativeMobyCatalogueEntryKind { PlacedRow, LocalRegistration, PlayerAnimationStream, SharedRegistration }

/// <summary>Detached source metadata. Classification and template matching never grant export authority.</summary>
public sealed class NativeMobyCatalogueEntry
{
    private readonly byte[]? _row;
    internal NativeMobyCatalogueEntry(NativeMobySourceLevel level, NativeMobySourceRow? row,
        NativeMobySourceRegistration? registration)
    {
        SourceLevelKey = level.LevelKey; SourceLevelName = level.DisplayName; SourceLevelId = level.LevelId;
        SourceWadEntry = level.DataWadEntry;
        SourceDataSha256 = level.SourceDataSha256;
        ActorId = row?.ActorId ?? registration!.ActorId;
        TrueIndex = row?.TrueIndex;
        RegistrationSlot = registration?.Slot;
        Kind = row != null ? NativeMobyCatalogueEntryKind.PlacedRow : registration!.Slot == 0
            ? NativeMobyCatalogueEntryKind.PlayerAnimationStream : NativeMobyCatalogueEntryKind.LocalRegistration;
        SourceVariantId = row?.SourceVariantId ??
            $"{NativeMobySourceInventory.OriginalImageSha256}:level-{level.LevelId}:data-{level.SourceDataSha256}:root-{registration!.Slot}:{registration.RootDataOffset:X8}:{registration.ActorId:X4}";
        PropertiesLocation = row?.PropertiesLocation;
        SourceRecordSha256 = row?.RecordSha256;
        _row = row?.CopyRecord();
        if (_row != null)
        {
            Moby value = new()
            {
                Index = row!.TrueIndex, TrueIndex = row.TrueIndex,
                Type = _row[0x50], State = _row[0x51], SourceByte36 = _row[0x36], SourceByte37 = _row[0x37],
                SourceByte4F = _row[0x4F], Flag4A = _row[0x52], Flag4B = _row[0x53],
                Position = new Vector3f(BinaryPrimitives.ReadInt32LittleEndian(_row.AsSpan(0x0C)) / 16f,
                    BinaryPrimitives.ReadInt32LittleEndian(_row.AsSpan(0x10)) / 16f,
                    BinaryPrimitives.ReadInt32LittleEndian(_row.AsSpan(0x14)) / 16f),
                PropertiesPointer = row.PropertiesPointer, SpecialDataPointer = row.AssociatedWord,
                YawByte = _row[0x46]
            };
            if (MobyIdentityClassifier.TryClassify(level.LevelKey, value, out var identity))
            { Label = identity.Label; Category = identity.Kind; ClassificationEvidence = $"{identity.Confidence}: {identity.Evidence}"; }
            else { Label = $"Native class 0x{ActorId:X4}"; Category = "Unclassified placed object"; ClassificationEvidence = "No derived identity; the native row is retained."; }
        }
        else
        {
            Label = IsSpecialPlayerStream ? "Spyro animation stream" : $"Registered resource 0x{ActorId:X4}";
            Category = IsSpecialPlayerStream ? "Special player resource" : "Local model registration";
            ClassificationEvidence = "A registration is not a placed object or an owned importable package schema.";
        }
    }
    internal NativeMobyCatalogueEntry(NativeSharedMobySourceInventory shared, NativeSharedMobySourceRegistration registration)
    {
        SourceLevelKey = NativeMobyCatalogue.SharedSourceKey; SourceLevelName = "Shared PETE models";
        // This source is not a level. Never fabricate a level ID or data entry
        // that a downstream authoring provider could mistake for a donor row.
        SourceLevelId = null; SourceWadEntry = null;
        SourceDataSha256 = shared.RuntimeModelDataSha256;
        ActorId = registration.ActorId; RegistrationSlot = registration.Slot;
        SharedRootPeteOffset = registration.RootPeteOffset;
        Kind = NativeMobyCatalogueEntryKind.SharedRegistration;
        SourceVariantId = $"{NativeMobySourceInventory.OriginalImageSha256}:shared-pete:header-{shared.HeaderSha256}:data-{shared.RuntimeModelDataSha256}:root-{registration.Slot}:{registration.RootPeteOffset:X8}:{registration.ActorId:X4}";
        Label = ActorId switch { 0 => "Shared Spyro model", 0x22 => "Retail dragon egg model", _ => $"Shared resource 0x{ActorId:X4}" };
        Category = "Shared model registration";
        ClassificationEvidence = "Original USA shared PETE registration; not a placed row or portable dependency package. " +
            (ActorId == 0x22 ? "This is the ordinary retail egg, not an identified Japanese dragonfly egg." : "No beta/Japanese identity is inferred.");
    }
    public string SourceVariantId { get; }
    public string SourceLevelKey { get; }
    public string SourceLevelName { get; }
    public int? SourceLevelId { get; }
    public int? SourceWadEntry { get; }
    public uint? SharedRootPeteOffset { get; }
    public string SourceDataSha256 { get; }
    public string? SourceRecordSha256 { get; }
    public ushort ActorId { get; }
    public int? TrueIndex { get; }
    public int? RegistrationSlot { get; }
    public NativeMobyCatalogueEntryKind Kind { get; }
    public string Label { get; }
    public string Category { get; }
    public string ClassificationEvidence { get; }
    public NativeMobyPropertiesLocation? PropertiesLocation { get; }
    public bool IsSpecialPlayerStream => Kind == NativeMobyCatalogueEntryKind.PlayerAnimationStream;
    public bool IsSharedResource => Kind == NativeMobyCatalogueEntryKind.SharedRegistration;
    public bool NativeImportApproved => false;
    public string SourceLabel => TrueIndex is int index ? $"{SourceLevelName} · T{index}" : $"{SourceLevelName} · root {RegistrationSlot}";
    public override string ToString() => $"{Label} · 0x{ActorId:X4} · {SourceLabel}";

    // This maps a selected variant back to an ALREADY approved editor template.
    // It is not a substitute for the existing template gate or source-patch planner.
    public bool MatchesExistingTemplate(string sourceLevelKey, int sourceTrueIndex, int renderRadius,
        int initialState, int classLow, int classHigh, int byte4F, int byte52, int byte53) =>
        _row != null && ActorId != 0 &&
        SourceLevelKey == LevelCatalog.NormalizeKey(sourceLevelKey) && TrueIndex == sourceTrueIndex &&
        _row[0x50] == renderRadius && _row[0x51] == initialState &&
        _row[0x36] == classLow && _row[0x37] == classHigh && _row[0x4F] == byte4F &&
        _row[0x52] == byte52 && _row[0x53] == byte53;
}

public sealed record NativeMobyCatalogueTargetStatus(string TargetLevelKey, bool RetailTargetInspected,
    NativeMobyLocalModelStatus? LocalModelStatus, int? NativePlacedRowCount, int? GenericRegistrationTailSlots)
{
    public bool SharedModelRegistered { get; init; }
    public bool NativeImportApproved => false;
    public bool DependencyClosureEvaluated => false;
    public bool MemoryFitEvaluated => false;
    public string Description => !RetailTargetInspected
        ? "This destination is not certified by the retail inventory. No donor level's residency or capacity is borrowed."
        : $"Local registration: {LocalModelStatus}; {NativePlacedRowCount} native placed row(s) of this class. " +
          $"Shared model registration: {(SharedModelRegistered ? "present" : "not found")}. " +
          $"{GenericRegistrationTailSlots} generic header tail slot(s). " +
          "Behavior, animation, sound/effects, private/shared state and RAM/VRAM/SPU fit are not evaluated by discovery. " +
          "A present root does not prove equivalent dependencies; an absent root may use global or special rendering.";
}

/// <summary>Usable, immutable source-variant catalogue over an exact-source inventory; no scene or file writes.</summary>
public sealed class NativeMobyCatalogue
{
    // A source-filter key only, never an entry in the retail level catalogue.
    public const string SharedSourceKey = "sharedpete";
    private readonly NativeMobySourceInventory _inventory;
    private readonly IReadOnlyList<NativeMobyCatalogueEntry> _entries;
    private readonly Dictionary<string, NativeMobyCatalogueEntry> _byId;
    public NativeMobyCatalogue(NativeMobySourceInventory inventory)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        _inventory = inventory;
        _entries = Array.AsReadOnly(inventory.Levels.SelectMany(DescribeLevel)
            .Concat(inventory.SharedModels.Registrations.Select(root => new NativeMobyCatalogueEntry(inventory.SharedModels, root)))
            .OrderBy(e => e.ActorId).ThenBy(e => e.SourceLevelId).ThenBy(e => e.TrueIndex ?? int.MaxValue)
            .ThenBy(e => e.RegistrationSlot).ToArray());
        _byId = _entries.ToDictionary(e => e.SourceVariantId, StringComparer.Ordinal);
    }
    public IReadOnlyList<NativeMobySourceLevel> Levels => _inventory.Levels;
    public IReadOnlyList<NativeMobyCatalogueEntry> Search(string? query = null, string? sourceLevelKey = null,
        NativeMobyCatalogueView view = NativeMobyCatalogueView.PlacedRows) => SearchEntries(_entries, query, sourceLevelKey, view);

    public NativeMobyCatalogueTargetStatus DescribeTarget(NativeMobyCatalogueEntry entry, string targetLevelKey)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (!_byId.TryGetValue(entry.SourceVariantId, out var owned) || !ReferenceEquals(owned, entry))
            throw new InvalidDataException("Catalogue selection is not owned by this captured source snapshot.");
        string key = LevelCatalog.NormalizeKey(targetLevelKey);
        var target = Levels.SingleOrDefault(l => l.LevelKey == key);
        if (target == null) return new(key, false, null, null, null);
        var found = _inventory.DescribeDestination(entry.ActorId, key);
        return new(key, true, found.LocalModelStatus, found.NativePlacedRowCount,
            63 - target.Registrations.Count(r => r.Slot > 0)) { SharedModelRegistered = found.SharedRootPeteOffset.HasValue };
    }

    internal static IReadOnlyList<NativeMobyCatalogueEntry> DescribeLevel(NativeMobySourceLevel level)
    {
        ArgumentNullException.ThrowIfNull(level);
        return Array.AsReadOnly(level.Rows.Select(r => new NativeMobyCatalogueEntry(level, r, null))
            .Concat(level.Registrations.Select(r => new NativeMobyCatalogueEntry(level, null, r))).ToArray());
    }

    internal static IReadOnlyList<NativeMobyCatalogueEntry> SearchEntries(IReadOnlyList<NativeMobyCatalogueEntry> entries,
        string? query, string? sourceLevelKey, NativeMobyCatalogueView view)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (!Enum.IsDefined(view) || query?.Length > 256)
            throw new InvalidDataException("Catalogue query/view exceeds its bounded metadata scope.");
        string[] terms = (query ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string key = string.IsNullOrWhiteSpace(sourceLevelKey) ? "" : LevelCatalog.NormalizeKey(sourceLevelKey);
        return Array.AsReadOnly(entries.Where(e =>
            (view == NativeMobyCatalogueView.All || (view == NativeMobyCatalogueView.PlacedRows) == (e.Kind == NativeMobyCatalogueEntryKind.PlacedRow)) &&
            (key.Length == 0 || e.SourceLevelKey == key) &&
            terms.All(term => $"{e.Label} {e.Category} {e.SourceLevelName} {e.SourceLevelKey} 0x{e.ActorId:X4} T{e.TrueIndex}"
                .Contains(term, StringComparison.OrdinalIgnoreCase))).ToArray());
    }
}
