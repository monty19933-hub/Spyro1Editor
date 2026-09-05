using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Spyro.Editor.Core.Scene;

internal enum NativePropertiesReservationKind { OwnedComponentExtent, ExactPassiveTail }

internal sealed record NativePropertiesReservation(
    NativePropertiesReservationKind Kind, int Offset, int ByteLength,
    string BeforeSha256, string OwnershipEvidenceSha256);

internal sealed record NativePropertiesConsumer(string Id, int PointerFieldOffset, bool AppendFixup);

internal sealed record NativePropertiesFixupEnvelope(
    int CountOffset, int CountBefore, int MaximumCount, string ActiveBeforeSha256,
    string AppendCapacityBeforeSha256);

/// <summary>
/// A trusted evidence producer must validate the referenced read-only behavior
/// proof before issuing this certificate. The linker verifies source/payload/
/// reservation/fixup/group/consumer binding; it cannot infer immutability from equal bytes.
/// No authored JSON or UI flag is accepted as sharing authority.
/// </summary>
internal sealed class NativePropertiesImmutableSharingAuthority
{
    private NativePropertiesImmutableSharingAuthority(string proofSha256, string sourceSha256,
        string payloadSha256, NativePropertiesReservation reservation, NativePropertiesFixupEnvelope fixups, string groupId,
        IReadOnlyList<NativePropertiesConsumer> consumers)
    {
        ProofSha256 = proofSha256; SourceSha256 = sourceSha256; PayloadSha256 = payloadSha256;
        Reservation = reservation; Fixups = fixups; GroupId = groupId;
        Consumers = Array.AsReadOnly(consumers.OrderBy(item => item.PointerFieldOffset).ThenBy(item => item.Id, StringComparer.Ordinal).ToArray());
    }
    public string ProofSha256 { get; }
    public string SourceSha256 { get; }
    public string PayloadSha256 { get; }
    public NativePropertiesReservation Reservation { get; }
    public NativePropertiesFixupEnvelope Fixups { get; }
    public string GroupId { get; }
    public IReadOnlyList<NativePropertiesConsumer> Consumers { get; }

    internal static NativePropertiesImmutableSharingAuthority FromValidatedReadOnlyEvidence(
        string proofSha256, string sourceSha256, string payloadSha256,
        NativePropertiesReservation reservation, NativePropertiesFixupEnvelope fixups, string groupId,
        IReadOnlyList<NativePropertiesConsumer> consumers) =>
        new(proofSha256, sourceSha256, payloadSha256, reservation, fixups, groupId, consumers);
}

internal sealed record NativeScenePropertiesLinkRequest(
    ReadOnlyMemory<byte> SourceScene, string SourceSha256, string ReadOnlyEvidenceSha256,
    ReadOnlyMemory<byte> Payload, string PayloadSha256, IReadOnlyList<int> InternalPointerFields,
    NativePropertiesReservation Reservation, NativePropertiesFixupEnvelope Fixups,
    string AtomicGroupId, IReadOnlyList<NativePropertiesConsumer> Consumers,
    NativePropertiesImmutableSharingAuthority? SharingAuthority);

internal sealed record NativePropertiesPointerBinding(string ConsumerId, int FieldOffset, uint Before, uint After);
internal sealed record NativePropertiesSceneWrite(int Offset, string BeforeHex, string AfterHex);

internal sealed class NativeScenePropertiesLinkPlan(
    IReadOnlyList<NativePropertiesPointerBinding> bindings,
    IReadOnlyList<NativePropertiesSceneWrite> writes, int fixupCountAfter)
{
    public IReadOnlyList<NativePropertiesPointerBinding> Bindings { get; } = Array.AsReadOnly(bindings.ToArray());
    public IReadOnlyList<NativePropertiesSceneWrite> Writes { get; } = Array.AsReadOnly(writes.ToArray());
    public int FixupCountAfter { get; } = fixupCountAfter;
}

/// <summary>
/// Pure, pointer-free properties linker for explicit fixed reservations. It
/// does not discover free space, grow components, move opaque data, prove
/// behavior closure or authorize publication. Scene-relative addresses only.
/// </summary>
internal static class NativeScenePropertiesLinker
{
    public static NativeScenePropertiesLinkPlan Compile(NativeScenePropertiesLinkRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        byte[] scene = request.SourceScene.ToArray();
        byte[] payload = request.Payload.ToArray();
        RequireHash(scene, request.SourceSha256, "source scene");
        RequireHash(payload, request.PayloadSha256, "properties payload");
        if (request.InternalPointerFields.Count != 0)
            throw new InvalidDataException("This linker accepts pointer-free properties only; internal pointer schemas require a later linker.");
        NativePropertiesReservation reservation = request.Reservation;
        if (!Enum.IsDefined(reservation.Kind) || payload.Length == 0 || reservation.ByteLength != payload.Length ||
            (reservation.Offset & 3) != 0 || (reservation.ByteLength & 3) != 0)
            throw new InvalidDataException("Properties reservation kind, alignment or exact byte length is invalid.");
        ReadOnlySpan<byte> beforeProperties = Slice(scene, reservation.Offset, reservation.ByteLength);
        RequireHash(beforeProperties, reservation.BeforeSha256, "properties reservation preimage");
        RequireDigest(reservation.OwnershipEvidenceSha256, "reservation ownership evidence");
        if (string.IsNullOrWhiteSpace(request.AtomicGroupId) || request.Consumers.Count == 0)
            throw new InvalidDataException("Properties consumers require a nonempty atomic group.");
        NativePropertiesConsumer[] consumers = request.Consumers.OrderBy(item => item.PointerFieldOffset)
            .ThenBy(item => item.Id, StringComparer.Ordinal).ToArray();
        if (consumers.Any(item => string.IsNullOrWhiteSpace(item.Id)) ||
            consumers.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != consumers.Length ||
            consumers.Select(item => item.PointerFieldOffset).Distinct().Count() != consumers.Length)
            throw new InvalidDataException("Properties consumers have duplicate identities or pointer fields.");

        if (consumers.Length > 1 || reservation.Kind == NativePropertiesReservationKind.ExactPassiveTail)
        {
            NativePropertiesImmutableSharingAuthority? authority = request.SharingAuthority;
            if (authority == null || authority.SourceSha256 != request.SourceSha256 ||
                authority.PayloadSha256 != request.PayloadSha256 || authority.Reservation != reservation ||
                authority.Fixups != request.Fixups ||
                authority.ProofSha256 != request.ReadOnlyEvidenceSha256 || authority.GroupId != request.AtomicGroupId ||
                !authority.Consumers.SequenceEqual(consumers))
                throw new InvalidDataException("Source-bound immutable properties sharing/tail authority does not match this request.");
            RequireDigest(authority.ProofSha256, "immutable sharing authority");
        }

        NativePropertiesFixupEnvelope fixups = request.Fixups;
        if (fixups.CountBefore < 0 || fixups.MaximumCount < fixups.CountBefore || (fixups.CountOffset & 3) != 0)
            throw new InvalidDataException("Fixup count/capacity or alignment is invalid.");
        long activeLength = 4L + 4L * fixups.CountBefore;
        long maximumLength = 4L + 4L * fixups.MaximumCount;
        ReadOnlySpan<byte> fullFixups = Slice(scene, fixups.CountOffset, maximumLength);
        ReadOnlySpan<byte> activeFixups = Slice(scene, fixups.CountOffset, activeLength);
        RequireHash(activeFixups, fixups.ActiveBeforeSha256, "active fixup preimage");
        RequireHash(fullFixups[(int)activeLength..], fixups.AppendCapacityBeforeSha256, "fixup append-capacity preimage");
        if (BinaryPrimitives.ReadUInt32LittleEndian(activeFixups) != fixups.CountBefore)
            throw new InvalidDataException("Native fixup count disagrees with the inspected envelope.");
        if (Overlaps(reservation.Offset, reservation.ByteLength, fixups.CountOffset, maximumLength))
            throw new InvalidDataException("Properties reservation overlaps the owned fixup envelope.");
        HashSet<int> oldFields = [];
        for (int index = 0; index < fixups.CountBefore; index++)
        {
            uint rawField = BinaryPrimitives.ReadUInt32LittleEndian(activeFixups.Slice(4 + index * 4, 4));
            if (rawField > int.MaxValue || (rawField & 3) != 0)
                throw new InvalidDataException("An existing fixup field has an invalid scene-relative range.");
            int field = (int)rawField;
            ReadOnlySpan<byte> pointer = Slice(scene, field, 4);
            if (!oldFields.Add(field))
                throw new InvalidDataException("The existing fixup list contains a duplicate field.");
            if (Overlaps(field, 4, fixups.CountOffset, maximumLength))
                throw new InvalidDataException("An existing pointer field overlaps the owned fixup envelope.");
            if (Overlaps(field, 4, reservation.Offset, reservation.ByteLength))
                throw new InvalidDataException("A claimed pointer-free properties reservation contains an existing pointer fixup.");
            uint target = BinaryPrimitives.ReadUInt32LittleEndian(pointer);
            if (target >= reservation.Offset && target < (long)reservation.Offset + reservation.ByteLength &&
                !consumers.Any(consumer => consumer.PointerFieldOffset == field))
                throw new InvalidDataException("An undeclared existing pointer targets the owned properties reservation.");
        }

        List<NativePropertiesPointerBinding> bindings = [];
        foreach (NativePropertiesConsumer consumer in consumers)
        {
            ReadOnlySpan<byte> beforeField = Slice(scene, consumer.PointerFieldOffset, 4);
            if ((consumer.PointerFieldOffset & 3) != 0 ||
                Overlaps(consumer.PointerFieldOffset, 4, reservation.Offset, reservation.ByteLength) ||
                Overlaps(consumer.PointerFieldOffset, 4, fixups.CountOffset, maximumLength))
                throw new InvalidDataException("A properties consumer pointer field overlaps storage/fixups or is unaligned.");
            bool registered = oldFields.Contains(consumer.PointerFieldOffset);
            if (consumer.AppendFixup && registered)
                throw new InvalidDataException("An appended consumer field is already registered as a native fixup.");
            if (!consumer.AppendFixup && !registered)
                throw new InvalidDataException("An existing consumer has no exact native pointer fixup.");
            bindings.Add(new(consumer.Id, consumer.PointerFieldOffset,
                BinaryPrimitives.ReadUInt32LittleEndian(beforeField), checked((uint)reservation.Offset)));
        }
        NativePropertiesConsumer[] appended = consumers.Where(item => item.AppendFixup).ToArray();
        if (appended.Length > fixups.MaximumCount - fixups.CountBefore)
            throw new InvalidDataException("The explicit fixup append capacity is insufficient.");
        int afterCount = fixups.CountBefore + appended.Length;
        List<NativePropertiesSceneWrite> writes = [];
        if (appended.Length > 0)
        {
            AddWrite(writes, scene, fixups.CountOffset, Word(afterCount));
            for (int index = 0; index < appended.Length; index++)
                AddWrite(writes, scene, checked(fixups.CountOffset + (int)activeLength + index * 4), Word(appended[index].PointerFieldOffset));
        }
        AddWrite(writes, scene, reservation.Offset, payload);
        return new(bindings, writes.OrderBy(item => item.Offset).ToArray(), afterCount);
    }

    private static ReadOnlySpan<byte> Slice(byte[] bytes, int offset, long length)
    {
        if (offset < 0 || length < 0 || length > bytes.Length || offset > bytes.Length - length)
            throw new InvalidDataException("Native properties/fixup range exceeds the exact source scene.");
        return bytes.AsSpan(offset, (int)length);
    }
    private static bool Overlaps(int first, long length, int second, long otherLength) =>
        first < second + otherLength && second < first + length;
    private static byte[] Word(int value) { byte[] bytes = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(bytes, value); return bytes; }
    private static void AddWrite(List<NativePropertiesSceneWrite> writes, byte[] source, int offset, byte[] after) =>
        writes.Add(new(offset, Convert.ToHexString(Slice(source, offset, after.Length)), Convert.ToHexString(after)));
    private static void RequireDigest(string hash, string label)
    {
        if (hash.Length != 64 || !hash.All(Uri.IsHexDigit))
            throw new InvalidDataException($"The {label} SHA-256 evidence is missing or malformed.");
    }
    private static void RequireHash(ReadOnlySpan<byte> bytes, string expected, string label)
    {
        RequireDigest(expected, label);
        if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"The {label} hash does not match its exact preimage.");
    }
}
