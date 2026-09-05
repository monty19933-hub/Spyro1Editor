using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;

namespace Spyro.Editor.Core.Scene;

internal sealed record NativePropertiesInternalPointer(int PayloadFieldOffset, int PayloadTargetOffset);
internal sealed record NativePropertiesFixupRewrite(int ExistingFieldOffset, int? ReplacementPayloadFieldOffset);
internal sealed record NativePropertiesDonorScene(
    ReadOnlyMemory<byte> Scene, string SourceSha256, NativePropertiesFixupEnvelope Fixups);

internal sealed record NativeMutablePropertiesInstance(
    string Id, int DonorPayloadSceneOffset, string DonorPayloadSha256,
    ReadOnlyMemory<byte> Payload, string PayloadSha256,
    NativePropertiesReservation Reservation, NativePropertiesConsumer Consumer,
    IReadOnlyList<NativePropertiesInternalPointer> InternalPointers,
    IReadOnlyList<NativePropertiesFixupRewrite> FixupRewrites,
    NativePropertiesMutableOwnershipAuthority? OwnershipAuthority)
{
    // Null preserves the existing same-scene native route. A cross-level donor
    // has its own scene-relative address space and complete native fixup proof.
    public NativePropertiesDonorScene? DonorScene { get; init; }
}

internal sealed record NativeMutableScenePropertiesLinkRequest(
    ReadOnlyMemory<byte> SourceScene, string SourceSha256,
    NativePropertiesFixupEnvelope Fixups, IReadOnlyList<NativeMutablePropertiesInstance> Instances);

/// <summary>
/// Issued by code-owned evidence validation, never authored JSON. The producer
/// must establish the native owner's extent and the donor's complete schema;
/// the linker independently checks aliases, fixup coverage and source binding.
/// Mutable instances cannot acquire the Grass immutable-sharing authority.
/// </summary>
internal sealed class NativePropertiesMutableOwnershipAuthority
{
    private NativePropertiesMutableOwnershipAuthority(string proofSha256, string sourceSha256,
        NativePropertiesFixupEnvelope fixups, string instanceFingerprint)
    {
        ProofSha256 = proofSha256; SourceSha256 = sourceSha256;
        Fixups = fixups; InstanceFingerprint = instanceFingerprint;
    }
    public string ProofSha256 { get; }
    public string SourceSha256 { get; }
    public NativePropertiesFixupEnvelope Fixups { get; }
    public string InstanceFingerprint { get; }

    internal static NativePropertiesMutableOwnershipAuthority FromValidatedOwnedInstanceEvidence(
        string proofSha256, string sourceSha256, NativePropertiesFixupEnvelope fixups,
        NativeMutablePropertiesInstance instance) => new(proofSha256, sourceSha256, fixups, Fingerprint(instance));

    internal static string Fingerprint(NativeMutablePropertiesInstance instance) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            instance.Id, instance.DonorPayloadSceneOffset, instance.DonorPayloadSha256,
            instance.PayloadSha256, instance.Reservation, instance.Consumer,
            DonorScene = instance.DonorScene is { } donor ? new { donor.SourceSha256, donor.Fixups } : null,
            InternalPointers = instance.InternalPointers.OrderBy(item => item.PayloadFieldOffset).ThenBy(item => item.PayloadTargetOffset).ToArray(),
            FixupRewrites = instance.FixupRewrites.OrderBy(item => item.ExistingFieldOffset).ThenBy(item => item.ReplacementPayloadFieldOffset).ToArray()
        }))).ToLowerInvariant();
}

internal static partial class NativeScenePropertiesLinker
{
    /// <summary>
    /// Links private mutable instances into explicit existing extents. Internal
    /// pointers must remain inside the typed payload. Existing native fixups may
    /// be replaced or removed, never guessed or silently shared. No scene growth.
    /// </summary>
    public static NativeScenePropertiesLinkPlan CompileMutable(NativeMutableScenePropertiesLinkRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        byte[] scene = request.SourceScene.ToArray();
        RequireHash(scene, request.SourceSha256, "mutable source scene");
        NativePropertiesFixupEnvelope envelope = request.Fixups;
        if (envelope.CountBefore < 0 || envelope.MaximumCount != envelope.CountBefore || (envelope.CountOffset & 3) != 0)
            throw new InvalidDataException("Mutable linkage requires an exact existing fixup envelope, without append capacity.");
        long footprintLength = 4L + 4L * envelope.CountBefore;
        ReadOnlySpan<byte> footprint = Slice(scene, envelope.CountOffset, footprintLength);
        RequireHash(footprint, envelope.ActiveBeforeSha256, "mutable fixup preimage");
        RequireHash([], envelope.AppendCapacityBeforeSha256, "mutable fixup capacity preimage");
        if (BinaryPrimitives.ReadUInt32LittleEndian(footprint) != envelope.CountBefore)
            throw new InvalidDataException("Mutable fixup count disagrees with the exact native envelope.");
        List<int> oldFields = [];
        HashSet<int> distinctOldFields = [];
        for (int index = 0; index < envelope.CountBefore; index++)
        {
            uint rawField = BinaryPrimitives.ReadUInt32LittleEndian(footprint.Slice(4 + index * 4, 4));
            if (rawField > int.MaxValue || (rawField & 3) != 0)
                throw new InvalidDataException("An existing mutable-scene fixup field has an invalid range/alignment.");
            int field = (int)rawField;
            _ = Slice(scene, field, 4);
            if (!distinctOldFields.Add(field))
                throw new InvalidDataException("The existing mutable-scene fixup list contains a duplicate field.");
            if (Overlaps(field, 4, envelope.CountOffset, footprintLength))
                throw new InvalidDataException("An existing pointer field overlaps the mutable fixup envelope.");
            oldFields.Add(field);
        }
        NativeMutablePropertiesInstance[] instances = request.Instances.OrderBy(instance => instance.Reservation.Offset)
            .ThenBy(instance => instance.Id, StringComparer.Ordinal).ToArray();
        if (instances.Length == 0 || instances.Any(instance => string.IsNullOrWhiteSpace(instance.Id)) ||
            instances.Select(instance => instance.Id).Distinct(StringComparer.Ordinal).Count() != instances.Length)
            throw new InvalidDataException("Mutable instances require unique nonempty identities.");
        if (instances.Select(instance => instance.Consumer.PointerFieldOffset).Distinct().Count() != instances.Length ||
            instances.Select(instance => instance.Consumer.Id).Distinct(StringComparer.Ordinal).Count() != instances.Length)
            throw new InvalidDataException("Mutable instances require distinct private consumers; deduplicated sharing is forbidden.");
        for (int first = 0; first < instances.Length; first++)
        for (int second = first + 1; second < instances.Length; second++)
            if (Overlaps(instances[first].Reservation.Offset, instances[first].Reservation.ByteLength,
                    instances[second].Reservation.Offset, instances[second].Reservation.ByteLength))
                throw new InvalidDataException("Mutable instance owned extents overlap; equal payloads cannot share storage.");

        Dictionary<int, int?> replacements = [];
        List<NativePropertiesPointerBinding> bindings = [];
        List<NativePropertiesSceneWrite> writes = [];
        foreach (NativeMutablePropertiesInstance instance in instances)
        {
            NativePropertiesReservation reservation = instance.Reservation;
            byte[] payload = instance.Payload.ToArray();
            if (reservation.Kind != NativePropertiesReservationKind.OwnedComponentExtent ||
                reservation.ByteLength < payload.Length || payload.Length == 0 ||
                (reservation.Offset & 3) != 0 || (reservation.ByteLength & 3) != 0 || (payload.Length & 3) != 0)
                throw new InvalidDataException("Mutable properties require an aligned, sufficiently sized owned component extent.");
            RequireHash(Slice(scene, reservation.Offset, reservation.ByteLength), reservation.BeforeSha256, "mutable owned extent preimage");
            RequireHash(payload, instance.PayloadSha256, "mutable payload");
            byte[] donorScene = instance.DonorScene?.Scene.ToArray() ?? scene;
            IReadOnlyList<int> donorNativeFields = instance.DonorScene is { } donorEvidence
                ? ReadCertifiedDonorFixupFields(donorScene, donorEvidence) : oldFields;
            RequireHash(Slice(donorScene, instance.DonorPayloadSceneOffset, payload.Length), instance.DonorPayloadSha256, "native donor payload");
            RequireDigest(reservation.OwnershipEvidenceSha256, "mutable ownership evidence");
            NativePropertiesMutableOwnershipAuthority? authority = instance.OwnershipAuthority;
            if (authority == null || authority.SourceSha256 != request.SourceSha256 || authority.Fixups != envelope ||
                authority.ProofSha256 != reservation.OwnershipEvidenceSha256 ||
                authority.InstanceFingerprint != NativePropertiesMutableOwnershipAuthority.Fingerprint(instance))
                throw new InvalidDataException("The source-bound mutable ownership authority does not match this instance.");
            RequireDigest(authority.ProofSha256, "mutable ownership authority");
            if (Overlaps(reservation.Offset, reservation.ByteLength, envelope.CountOffset, footprintLength))
                throw new InvalidDataException("The mutable owned extent overlaps its fixup envelope.");
            bool sameAddressSpace = instance.DonorScene == null ||
                instance.DonorScene.SourceSha256.Equals(request.SourceSha256, StringComparison.OrdinalIgnoreCase);
            if (sameAddressSpace && instances.Any(other => Overlaps(instance.DonorPayloadSceneOffset, payload.Length,
                    other.Reservation.Offset, other.Reservation.ByteLength)))
                throw new InvalidDataException("Mutable target storage overlaps a still-owned donor payload.");
            NativePropertiesConsumer consumer = instance.Consumer;
            if (consumer.AppendFixup || !distinctOldFields.Contains(consumer.PointerFieldOffset))
                throw new InvalidDataException("A mutable existing consumer requires its exact registered native fixup.");
            if (string.IsNullOrWhiteSpace(consumer.Id) || (consumer.PointerFieldOffset & 3) != 0 ||
                instances.Any(other => Overlaps(consumer.PointerFieldOffset, 4, other.Reservation.Offset, other.Reservation.ByteLength)))
                throw new InvalidDataException("A private mutable consumer overlaps owned storage or has an invalid identity/alignment.");
            uint ownerPointer = BinaryPrimitives.ReadUInt32LittleEndian(Slice(scene, consumer.PointerFieldOffset, 4));
            if (ownerPointer != reservation.Offset)
                throw new InvalidDataException("The declared native owner does not point to the exact mutable extent base.");

            NativePropertiesInternalPointer[] pointers = instance.InternalPointers.OrderBy(pointer => pointer.PayloadFieldOffset).ToArray();
            if (pointers.Select(pointer => pointer.PayloadFieldOffset).Distinct().Count() != pointers.Length)
                throw new InvalidDataException("The mutable internal pointer schema contains duplicate fields.");
            foreach (NativePropertiesInternalPointer pointer in pointers)
            {
                if (pointer.PayloadFieldOffset < 0 || pointer.PayloadFieldOffset > payload.Length - 4 ||
                    (pointer.PayloadFieldOffset & 3) != 0 || pointer.PayloadTargetOffset < 0 ||
                    pointer.PayloadTargetOffset >= payload.Length || (pointer.PayloadTargetOffset & 3) != 0)
                    throw new InvalidDataException("The mutable internal pointer schema exceeds its exact payload or alignment.");
                uint expectedDonorTarget = checked((uint)(instance.DonorPayloadSceneOffset + pointer.PayloadTargetOffset));
                if (BinaryPrimitives.ReadUInt32LittleEndian(Slice(donorScene, checked(instance.DonorPayloadSceneOffset + pointer.PayloadFieldOffset), 4)) != expectedDonorTarget ||
                    BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(pointer.PayloadFieldOffset, 4)) != expectedDonorTarget)
                    throw new InvalidDataException("A mutable internal pointer does not match the declared native donor-relative target.");
            }
            int[] donorFields = donorNativeFields.Where(field => field >= instance.DonorPayloadSceneOffset &&
                field < (long)instance.DonorPayloadSceneOffset + payload.Length).Order().ToArray();
            if (!donorFields.SequenceEqual(pointers.Select(pointer => checked(instance.DonorPayloadSceneOffset + pointer.PayloadFieldOffset))))
                throw new InvalidDataException("The typed internal pointer schema does not cover every native donor payload fixup.");
            NativePropertiesFixupRewrite[] rewrites = instance.FixupRewrites.OrderBy(rewrite => rewrite.ExistingFieldOffset).ToArray();
            int[] ownedFields = oldFields.Where(field => field >= reservation.Offset &&
                field < (long)reservation.Offset + reservation.ByteLength).Order().ToArray();
            if (!ownedFields.SequenceEqual(rewrites.Select(rewrite => rewrite.ExistingFieldOffset)))
                throw new InvalidDataException("Mutable fixup rewrite coverage does not exactly match the old owned extent.");
            int[] newFields = rewrites.Where(rewrite => rewrite.ReplacementPayloadFieldOffset.HasValue)
                .Select(rewrite => rewrite.ReplacementPayloadFieldOffset!.Value).Order().ToArray();
            if (newFields.Any(field => !pointers.Any(pointer => pointer.PayloadFieldOffset == field)))
                throw new InvalidDataException("A replacement fixup refers to an undeclared internal pointer field.");
            if (!newFields.SequenceEqual(pointers.Select(pointer => pointer.PayloadFieldOffset)))
                throw new InvalidDataException("Mutable fixup rewrite coverage does not register every internal pointer exactly once.");
            foreach (int field in oldFields)
            {
                uint target = BinaryPrimitives.ReadUInt32LittleEndian(Slice(scene, field, 4));
                if (target >= reservation.Offset && target < (long)reservation.Offset + reservation.ByteLength &&
                    field != consumer.PointerFieldOffset && !ownedFields.Contains(field))
                    throw new InvalidDataException("An undeclared native pointer aliases the private mutable extent.");
            }
            foreach (NativePropertiesFixupRewrite rewrite in rewrites)
                replacements.Add(rewrite.ExistingFieldOffset, rewrite.ReplacementPayloadFieldOffset is int relative
                    ? checked(reservation.Offset + relative) : null);
            foreach (NativePropertiesInternalPointer pointer in pointers)
                BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(pointer.PayloadFieldOffset, 4), checked((uint)(reservation.Offset + pointer.PayloadTargetOffset)));
            bindings.Add(new(consumer.Id, consumer.PointerFieldOffset, ownerPointer, checked((uint)reservation.Offset)));
            AddWrite(writes, scene, reservation.Offset, payload);
        }
        List<int> finalFields = [];
        foreach (int field in oldFields)
        {
            if (!replacements.TryGetValue(field, out int? replacement)) finalFields.Add(field);
            else if (replacement.HasValue) finalFields.Add(replacement.Value);
        }
        if (finalFields.Distinct().Count() != finalFields.Count)
            throw new InvalidDataException("The final mutable fixup list contains duplicate pointer fields.");
        byte[] finalFixups = new byte[(int)footprintLength];
        BinaryPrimitives.WriteInt32LittleEndian(finalFixups, finalFields.Count);
        for (int index = 0; index < finalFields.Count; index++)
            BinaryPrimitives.WriteInt32LittleEndian(finalFixups.AsSpan(4 + index * 4), finalFields[index]);
        AddWrite(writes, scene, envelope.CountOffset, finalFixups);
        return new(bindings, writes.OrderBy(write => write.Offset).ToArray(), finalFields.Count);
    }

    private static IReadOnlyList<int> ReadCertifiedDonorFixupFields(byte[] scene, NativePropertiesDonorScene donor)
    {
        RequireHash(scene, donor.SourceSha256, "independent donor scene");
        NativePropertiesFixupEnvelope fixups = donor.Fixups;
        if (fixups.CountBefore < 0 || fixups.MaximumCount != fixups.CountBefore || (fixups.CountOffset & 3) != 0)
            throw new InvalidDataException("The independent donor fixup envelope has an invalid native count/alignment.");
        long length = 4L + 4L * fixups.CountBefore;
        ReadOnlySpan<byte> footprint = Slice(scene, fixups.CountOffset, length);
        RequireHash(footprint, fixups.ActiveBeforeSha256, "independent donor fixup preimage");
        RequireHash([], fixups.AppendCapacityBeforeSha256, "independent donor fixup capacity");
        if (BinaryPrimitives.ReadUInt32LittleEndian(footprint) != fixups.CountBefore)
            throw new InvalidDataException("The independent donor's native fixup count differs from its certificate.");
        HashSet<int> fields = [];
        for (int index = 0; index < fixups.CountBefore; index++)
        {
            uint field = BinaryPrimitives.ReadUInt32LittleEndian(footprint.Slice(4 + index * 4, 4));
            if (field > int.MaxValue || (field & 3) != 0)
                throw new InvalidDataException("An independent donor internal fixup has an invalid scene-relative range/alignment.");
            _ = Slice(scene, (int)field, 4);
            if (!fields.Add((int)field))
                throw new InvalidDataException("The independent donor native fixup list contains a duplicate field.");
            if (Overlaps((int)field, 4, fixups.CountOffset, length))
                throw new InvalidDataException("An independent donor pointer field overlaps its native fixup envelope.");
        }
        return fields.Order().ToArray();
    }
}
