using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Spyro.Editor.Core.Exporting;

internal enum NativeLevelSlotIntent { ReplaceExisting, AddIndependent }
internal enum NativeLevelSlotResourceKind
{
    Overlay, LevelData, InitialMusicRow, LateMusicRow, DragonTarget, TreasureTarget, NamePointer
}

internal sealed record NativeLevelSlotRequest(string OwnerId, int LevelId,
    NativeLevelSlotIntent Intent, IReadOnlyList<int> RetirementTrueIndexes, bool RequiresEggPersistence = false);
internal sealed record NativeLevelSlotResource(NativeLevelSlotResourceKind Kind, NativeByteExtent Extent);
internal sealed record NativeLevelSaveRow(string Kind, int SaveOffset, int ByteLength);
internal sealed record NativeLevelSlotBinding(int LevelId, int ContinuousIndex, int OverlayDirectoryIndex,
    int DataDirectoryIndex, IReadOnlyList<NativeLevelSlotResource> Resources, IReadOnlyList<NativeLevelSaveRow> SaveRows);
internal sealed record NativeObservedPortalBinding(int SourceLevelId, int SurfaceIndex, int DestinationLevelId,
    NativeByteExtent Descriptor, string DescriptorSha256);
internal sealed record NativeLevelSlotReservation(string OwnerId, NativeLevelSlotBinding Slot,
    IReadOnlyList<int> RetirementTrueIndexes, bool RequiresEggPersistence);
internal sealed record NativeLevelSlotDeclaredWrite(string Id, string OwnerId, NativeByteExtent Extent);
internal sealed record NativeLevelSlotPortalIntent(NativeByteExtent ExistingDescriptor, string DestinationOwnerId);

/// <summary>
/// Geometry/capacity only; this kernel does not establish source authenticity.
/// Production callers obtain it through NativeLevelSlotOwnershipSource. A
/// reservation permits neither publication nor arbitrary bytes in its spans.
/// </summary>
internal sealed class NativeLevelSlotOwnershipLayout
{
    internal const int RetailSlotCount = 35;
    internal const int SaveSlotCount = 36;
    internal const int EggSaveSlotCount = 18;
    internal const int RetirementBitsPerSlot = 256;
    internal const int InitialMusicRowCount = 48;
    internal const int LateMusicRowCount = 35;
    internal const int MaximumWrites = 4096;
    internal const string IndependentLevelBlocker =
        "Independent native levels are unsupported: reserved ID65 has no source WAD resources (entries79/80), " +
        "no bounded late-music row35 (it overlaps PETEXA LBAs), and no observed normal portal binding; " +
        "the retail portal guard excludes65 and the loader classifies it as a flight. " +
        "36 save rows do not establish complete level capacity, and egg persistence has only18 rows. " +
        "Existing ID65 laboratory candidates are not authority to add an independent level.";

    private readonly IReadOnlyList<NativeLevelSlotBinding> _slots;
    internal NativeAddressSpace Wad { get; }
    internal NativeAddressSpace Executable { get; }
    internal IReadOnlyList<NativeLevelSlotBinding> Slots => _slots;
    internal IReadOnlyList<NativeObservedPortalBinding> ObservedPortals { get; }

    internal NativeLevelSlotOwnershipLayout(ReadOnlySpan<byte> wadHeader, NativeAddressSpace wad,
        NativeAddressSpace executable, IReadOnlyList<NativeObservedPortalBinding> observedPortals)
    {
        ArgumentNullException.ThrowIfNull(wad);
        ArgumentNullException.ThrowIfNull(executable);
        if (wad.Kind != NativeAddressKind.WadFile || executable.Kind != NativeAddressKind.ExecutableFile ||
            wad.Revision != NativeAddressRevision.Original || executable.Revision != NativeAddressRevision.Original ||
            wadHeader.Length != 0x800 || Word(wadHeader, 0) != 0x800 || executable.ByteLength < 0x60080)
            throw new InvalidDataException("Level-slot ownership requires bounded original WAD/EXE domains and a complete native header.");
        Wad = wad; Executable = executable;
        byte[] header = wadHeader.ToArray();
        ArgumentNullException.ThrowIfNull(observedPortals);
        if (observedPortals.Count > 63 * RetailSlotCount || observedPortals.Any(p => p == null))
            throw new InvalidDataException("The observed native portal inventory is missing or exceeds its bounded capacity.");

        // Every occupied top-level resource is checked, not just selected pairs:
        // otherwise a new owner could alias credits or another shared file.
        List<NativeByteExtent> occupied = [];
        for (int entry = 0; entry < wadHeader.Length / 8; entry++)
        {
            uint offset = Word(header, entry * 8), length = Word(header, entry * 8 + 4);
            if (offset == 0 && length == 0) continue;
            if (offset < 0x800 || length == 0 || offset % 0x800 != 0 || length % 0x800 != 0)
                throw new InvalidDataException($"WAD entry{entry} has no sector-aligned, occupied resource owner.");
            occupied.Add(new(wad, offset, length));
        }
        RequireDisjoint(occupied, "native WAD resource owners");
        if (wadHeader.Slice(79 * 8, 16).IndexOfAnyExcept((byte)0) >= 0 ||
            Word(wadHeader, 81 * 8) == 0 || Word(wadHeader, 81 * 8 + 4) == 0)
            throw new InvalidDataException("The reserved ID65 pair or adjacent non-level resource boundary changed.");

        List<NativeLevelSlotBinding> slots = [];
        for (int index = 0; index < RetailSlotCount; index++)
        {
            int levelId = (index / 6 + 1) * 10 + index % 6;
            int overlay = 9 + index * 2, data = overlay + 1;
            List<NativeLevelSlotResource> resources =
            [
                new(NativeLevelSlotResourceKind.Overlay, Entry(overlay)),
                new(NativeLevelSlotResourceKind.LevelData, Entry(data)),
                new(NativeLevelSlotResourceKind.InitialMusicRow, new(executable, 0x5F79C + index * 4, 4)),
                new(NativeLevelSlotResourceKind.LateMusicRow, new(executable, 0x5F85C + index * 12, 12)),
                new(NativeLevelSlotResourceKind.DragonTarget, new(executable, 0x5FC14 + index, 1)),
                new(NativeLevelSlotResourceKind.TreasureTarget, new(executable, 0x5FC38 + index * 2, 2)),
                new(NativeLevelSlotResourceKind.NamePointer, new(executable, 0x5FFF0 + index * 4, 4))
            ];
            List<NativeLevelSaveRow> save =
            [
                new("visited", 0x40 + index, 1),
                new("opaque-second-byte-preserve-semantics", 0x64 + index, 1),
                new("dragons", 0x88 + index, 1),
                new("treasure", 0xAC + index * 2, 2),
                new("object-retirement", 0x10C + index * 0x20, 0x20)
            ];
            if (index < EggSaveSlotCount) save.Add(new("eggs", 0xF4 + index, 1));
            slots.Add(new(levelId, index, overlay, data, resources.AsReadOnly(), save.AsReadOnly()));
        }
        _slots = slots.AsReadOnly();
        RequireDisjoint(slots.SelectMany(s => s.Resources).Select(r => r.Extent), "slot resource rows");
        var saveSpans = slots.SelectMany(s => s.SaveRows).OrderBy(s => s.SaveOffset).ToArray();
        for (int i = 1; i < saveSpans.Length; i++)
            if (saveSpans[i].SaveOffset < saveSpans[i - 1].SaveOffset + saveSpans[i - 1].ByteLength)
                throw new InvalidDataException("Native slot save identities overlap.");

        var portals = observedPortals.OrderBy(p => p.SourceLevelId).ThenBy(p => p.SurfaceIndex).ToArray();
        if (portals.Select(p => (p.SourceLevelId, p.SurfaceIndex)).Distinct().Count() != portals.Length)
            throw new InvalidDataException("A native portal descriptor identity is duplicated.");
        foreach (NativeObservedPortalBinding portal in portals)
        {
            NativeLevelSlotBinding source = RequireSlot(portal.SourceLevelId);
            _ = RequireSlot(portal.DestinationLevelId);
            NativeByteExtent owner = source.Resources.Single(r => r.Kind == NativeLevelSlotResourceKind.LevelData).Extent;
            if (portal.SurfaceIndex is < 0 or >= 63 || portal.Descriptor == null || portal.Descriptor.Space != Wad ||
                portal.Descriptor.ByteLength < 12 || !Contains(owner, portal.Descriptor) ||
                !IsSha256(portal.DescriptorSha256))
                throw new InvalidDataException("An observed portal lacks its exact source-owned descriptor.");
        }
        RequireDisjoint(portals.Select(p => p.Descriptor), "observed portal descriptors");
        ObservedPortals = Array.AsReadOnly(portals);

        NativeByteExtent Entry(int entry)
        {
            uint offset = Word(header, entry * 8), length = Word(header, entry * 8 + 4);
            if (offset == 0 || length == 0)
                throw new InvalidDataException($"Retail level resource entry{entry} is absent.");
            return new(wad, offset, length);
        }
    }

    internal NativeLevelSlotBinding RequireSlot(int levelId)
    {
        if (levelId == 65) throw new InvalidDataException(IndependentLevelBlocker);
        // Validate the decimal domain BEFORE the native arithmetic. ID16 would
        // otherwise map to20's save index6; IDs9/19/etc can alias other slots too.
        if (levelId < 10 || levelId > 64 || levelId % 10 > 5)
            throw new InvalidDataException($"Level ID{levelId} is not a retail native identity; applying decimal index arithmetic could alias another slot.");
        return _slots[checked((levelId / 10 - 1) * 6 + levelId % 10)];
    }

    internal NativeLevelSlotOwnershipPlan Reserve(IReadOnlyList<NativeLevelSlotRequest> requests,
        IReadOnlyList<NativeLevelSlotDeclaredWrite>? writes = null,
        IReadOnlyList<NativeLevelSlotPortalIntent>? portalIntents = null)
    {
        ArgumentNullException.ThrowIfNull(requests);
        writes ??= []; portalIntents ??= [];
        if (requests.Count is < 1 or > RetailSlotCount || requests.Any(r => r == null) ||
            writes.Count > MaximumWrites || writes.Any(w => w == null) || portalIntents.Count > 0)
            throw new InvalidDataException("Reservations must be bounded and complete; portal retarget intent is not proof of a patched native consumer. Retargeting requires a separate source-bound closure.");
        List<NativeLevelSlotReservation> reservations = [];
        HashSet<string> owners = new(StringComparer.Ordinal);
        HashSet<int> levels = [];
        foreach (NativeLevelSlotRequest request in requests.OrderBy(r => r.OwnerId, StringComparer.Ordinal))
        {
            if (!ValidId(request.OwnerId) || !owners.Add(request.OwnerId) || !Enum.IsDefined(request.Intent))
                throw new InvalidDataException("Reservations require unique bounded owner IDs and a known intent.");
            if (request.Intent == NativeLevelSlotIntent.AddIndependent)
                throw new InvalidDataException(IndependentLevelBlocker);
            NativeLevelSlotBinding slot = RequireSlot(request.LevelId);
            if (!levels.Add(slot.LevelId))
                throw new InvalidDataException($"Native level{slot.LevelId}, its portal destination and save index{slot.ContinuousIndex} already have another owner.");
            if (request.RetirementTrueIndexes == null || request.RetirementTrueIndexes.Count > RetirementBitsPerSlot)
                throw new InvalidDataException("A native level has exactly256 object-retirement bits, not unbounded object persistence.");
            int[] indexes = request.RetirementTrueIndexes.Order().ToArray();
            if (indexes.Any(i => i is < 0 or >= RetirementBitsPerSlot) || indexes.Distinct().Count() != indexes.Length)
                throw new InvalidDataException("Persistent object owners must be unique true indexes in0..255 within each level.");
            if (request.RequiresEggPersistence && slot.ContinuousIndex >= EggSaveSlotCount)
                throw new InvalidDataException($"Level{slot.LevelId} has no bounded egg-save row; only indexes0..17 deserialize eggs.");
            reservations.Add(new(request.OwnerId, slot, Array.AsReadOnly(indexes), request.RequiresEggPersistence));
        }
        // Check the composed set, not just each reservation independently.
        RequireDisjoint(reservations.SelectMany(r => r.Slot.Resources).Select(r => r.Extent), "reserved native resources");
        var byOwner = reservations.ToDictionary(r => r.OwnerId, StringComparer.Ordinal);
        NativeLevelSlotDeclaredWrite[] orderedWrites = writes.OrderBy(w => w.Id, StringComparer.Ordinal).ToArray();
        HashSet<string> writeIds = new(StringComparer.Ordinal);
        foreach (NativeLevelSlotDeclaredWrite write in orderedWrites)
        {
            if (!ValidId(write.Id) || !writeIds.Add(write.Id) || write.OwnerId == null ||
                !byOwner.TryGetValue(write.OwnerId, out NativeLevelSlotReservation? owner) || write.Extent == null)
                throw new InvalidDataException("A declared write has no unique ID and reserved native owner.");
            if (!owner.Slot.Resources.Any(r => Contains(r.Extent, write.Extent)))
                throw new InvalidDataException($"Write{write.Id} leaves its exact reserved resource/address space; shared code, directory or other-slot bytes need their own typed producer.");
            if (ObservedPortals.Any(p => Overlaps(p.Descriptor, write.Extent)))
                throw new InvalidDataException($"Write{write.Id} enters an observed native portal descriptor. Portal identity preservation/retargeting is not implied by level-data ownership.");
        }
        RequireDisjoint(orderedWrites.Select(w => w.Extent), "declared composition writes");
        return new(reservations.AsReadOnly(), Array.AsReadOnly(orderedWrites), ObservedPortals);
    }

    internal static bool Contains(NativeByteExtent outer, NativeByteExtent inner) => outer.Space == inner.Space &&
        outer.Offset <= inner.Offset && inner.EndExclusive <= outer.EndExclusive;
    private static bool Overlaps(NativeByteExtent a, NativeByteExtent b) => a.Space == b.Space &&
        a.Offset < b.EndExclusive && b.Offset < a.EndExclusive;
    private static bool ValidId(string? value) => value != null && value.Length is >= 1 and <= 128 &&
        value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or ':');
    private static bool IsSha256(string value) => value != null && value.Length == 64 && value.All(Uri.IsHexDigit);
    private static uint Word(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
    private static void RequireDisjoint(IEnumerable<NativeByteExtent> extents, string what)
    {
        foreach (var group in extents.GroupBy(e => e.Space))
        {
            var ordered = group.OrderBy(e => e.Offset).ThenBy(e => e.ByteLength).ToArray();
            for (int i = 1; i < ordered.Length; i++)
                if (ordered[i].Offset < ordered[i - 1].EndExclusive)
                    throw new InvalidDataException($"Overlapping {what}; shared-resource aliasing is not authorized.");
        }
    }
}

internal sealed class NativeLevelSlotOwnershipPlan
{
    internal IReadOnlyList<NativeLevelSlotReservation> Reservations { get; }
    internal IReadOnlyList<NativeLevelSlotDeclaredWrite> DeclaredWrites { get; }
    // These source descriptor spans cannot be declared writable. This does not
    // certify that native pointers, geometry or code still reach them.
    internal IReadOnlyList<NativeObservedPortalBinding> ProtectedSourcePortalDescriptors { get; }
    internal string GeometrySha256 { get; }
    internal NativeLevelSlotOwnershipPlan(IReadOnlyList<NativeLevelSlotReservation> reservations,
        IReadOnlyList<NativeLevelSlotDeclaredWrite> writes, IReadOnlyList<NativeObservedPortalBinding> portals)
    {
        Reservations = reservations; DeclaredWrites = writes; ProtectedSourcePortalDescriptors = portals;
        // IDs have a restricted grammar; integers and delimiters are invariant.
        using MemoryStream bytes = new();
        using (BinaryWriter writer = new(bytes, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write("native-level-slot-ownership-geometry-v1");
            writer.Write(reservations.Count);
            foreach (var r in reservations)
            {
                writer.Write(r.OwnerId); writer.Write(r.Slot.LevelId); writer.Write(r.Slot.ContinuousIndex);
                writer.Write(r.RequiresEggPersistence); writer.Write(r.RetirementTrueIndexes.Count);
                foreach (int i in r.RetirementTrueIndexes) writer.Write(i);
                foreach (var resource in r.Slot.Resources)
                { writer.Write((int)resource.Kind); WriteExtent(writer, resource.Extent); }
                foreach (var save in r.Slot.SaveRows)
                { writer.Write(save.Kind); writer.Write(save.SaveOffset); writer.Write(save.ByteLength); }
            }
            writer.Write(writes.Count);
            foreach (var w in writes) { writer.Write(w.Id); writer.Write(w.OwnerId); WriteExtent(writer, w.Extent); }
            writer.Write(portals.Count);
            foreach (var p in portals)
            {
                writer.Write(p.SourceLevelId); writer.Write(p.SurfaceIndex); writer.Write(p.DestinationLevelId);
                WriteExtent(writer, p.Descriptor); writer.Write(p.DescriptorSha256);
            }
        }
        GeometrySha256 = Convert.ToHexString(SHA256.HashData(bytes.ToArray())).ToLowerInvariant();
    }
    private static void WriteExtent(BinaryWriter writer, NativeByteExtent extent)
    {
        writer.Write((int)extent.Space.Kind); writer.Write((int)extent.Space.Revision);
        writer.Write(extent.Space.ByteLength); writer.Write(extent.Offset); writer.Write(extent.ByteLength);
    }
}
