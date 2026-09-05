using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Scene;

internal static class NativeScenePropertiesLinkerCases
{
    public static void Run()
    {
        NativeScenePropertiesLinkRequest request = Fixture();
        NativeScenePropertiesLinkPlan plan = NativeScenePropertiesLinker.Compile(request);
        Require(plan.Bindings.Count == 2 && plan.Bindings.All(binding => binding.After == 0xC0), "Shared fixed binding changed.");
        Require(plan.FixupCountAfter == 2 && plan.Writes.Count == 3, "Expected one payload, count and appended-fixup write.");
        Require(plan.Writes.Single(write => write.Offset == 0x80).AfterHex == "20000000", "Wrong scene-relative appended pointer field.");
        Require(plan.Writes.Single(write => write.Offset == 0x78).AfterHex == "02000000", "Wrong fixup count.");
        NativeScenePropertiesLinkPlan reordered = NativeScenePropertiesLinker.Compile(request with { Consumers = request.Consumers.Reverse().ToArray() });
        Require(plan.Bindings.SequenceEqual(reordered.Bindings) && plan.Writes.SequenceEqual(reordered.Writes), "Caller order changed linkage.");
        Require(request.SourceScene.Span.SequenceEqual(Fixture().SourceScene.Span), "Pure linker changed its source.");
        Reject(request with { SharingAuthority = null }, "authority");
        Reject(request with { AtomicGroupId = "other" }, "authority");
        Reject(request with { ReadOnlyEvidenceSha256 = Hash([99]) }, "authority");
        Reject(request with { Payload = new byte[8] }, "payload");
        Reject(request with { SourceSha256 = Hash([98]) }, "source");
        Reject(request with { InternalPointerFields = [0] }, "pointer-free");
        Reject(request with { Reservation = request.Reservation with { Offset = 0xD0 } }, "authority");
        Reject(request with { Reservation = request.Reservation with { BeforeSha256 = Hash([97]) } }, "preimage");
        Reject(request with { Reservation = request.Reservation with { OwnershipEvidenceSha256 = Hash([96]) } }, "authority");
        Reject(request with { Reservation = request.Reservation with { Offset = int.MaxValue - 3 } }, "range");
        Reject(request with { Consumers = [request.Consumers[0], request.Consumers[0]] }, "duplicate");
        Reject(PrivateConsumer(request, new("bad", 0x10, true)), "already");
        Reject(PrivateConsumer(request, new("bad", 0x20, false)), "existing");
        Reject(PrivateConsumer(request, new("bad", 0xC0, false)), "overlap");
        Reject(request with { Consumers = [new("redirected", 0x30, true)] }, "authority");
        Reject(request with { Fixups = request.Fixups with { MaximumCount = 1 } }, "authority");
        NativeScenePropertiesLinkRequest privateAppend = PrivateConsumer(request, request.Consumers[1]);
        Reject(privateAppend with { Fixups = privateAppend.Fixups with { MaximumCount = 1,
            AppendCapacityBeforeSha256 = Hash([]) } }, "capacity");
        Reject(privateAppend with { Fixups = privateAppend.Fixups with { ActiveBeforeSha256 = Hash([95]) } }, "fixup");
        NativeScenePropertiesLinkRequest privateOne = request with
        {
            Reservation = request.Reservation with { Kind = NativePropertiesReservationKind.OwnedComponentExtent },
            Consumers = [request.Consumers[0]], SharingAuthority = null
        };
        Require(NativeScenePropertiesLinker.Compile(privateOne).Bindings.Count == 1, "Private owned extent rejected.");
        Reject(privateOne with { Consumers = request.Consumers }, "authority");
        foreach (uint target in new uint[] { 0xC0, 0xC4 })
            Reject(WithSecondActiveFixup(privateOne, 0x30, target), "undeclared");
        NativeScenePropertiesLinkRequest adjacentTarget = WithSecondActiveFixup(privateOne, 0x30, 0xC8);
        Require(NativeScenePropertiesLinker.Compile(adjacentTarget).Bindings.Count == 1,
            "An unrelated reference beyond the exact reservation was rejected.");
        Reject(WithSecondActiveFixup(privateAppend, 0x84, 0), "envelope");
        Console.WriteLine("NATIVE_SCENE_PROPERTIES_LINKER_SMOKE_OK");
    }

    private static NativeScenePropertiesLinkRequest Fixture()
    {
        byte[] scene = new byte[0x100];
        BinaryPrimitives.WriteUInt32LittleEndian(scene.AsSpan(0x10), 0xB0);
        BinaryPrimitives.WriteUInt32LittleEndian(scene.AsSpan(0x78), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(scene.AsSpan(0x7C), 0x10);
        // Preserve an opaque old block even when its former row is rebound.
        scene[0xB0] = 0xAA;
        byte[] payload = [4, 0, 0, 0, 0x8A, 0, 0, 0];
        string proof = Hash([1, 2, 3]);
        NativePropertiesReservation reservation = new(NativePropertiesReservationKind.ExactPassiveTail,
            0xC0, 8, Hash(scene.AsSpan(0xC0, 8)), proof);
        NativePropertiesConsumer[] consumers = [new("existing", 0x10, false), new("append", 0x20, true)];
        NativePropertiesFixupEnvelope fixups = new(0x78, 1, 3, Hash(scene.AsSpan(0x78, 8)), Hash(scene.AsSpan(0x80, 8)));
        return new(scene, Hash(scene), proof, payload, Hash(payload), [], reservation,
            fixups, "atomic.fixture",
            consumers,
            NativePropertiesImmutableSharingAuthority.FromValidatedReadOnlyEvidence(proof, Hash(scene), Hash(payload), reservation, fixups, "atomic.fixture", consumers));
    }

    public static void VerifyIntegratedRequest(NativeScenePropertiesLinkRequest request)
    {
        NativeScenePropertiesLinkPlan plan = NativeScenePropertiesLinker.Compile(request);
        Require(plan.Bindings.Count == request.Consumers.Count && plan.Bindings.All(binding => binding.After == 0x8138),
            "Integrated Grass adapter did not use the native scene linker.");
        Reject(request with { SharingAuthority = null }, "authority");
        Reject(request with { InternalPointerFields = [0] }, "pointer-free");
        Reject(request with { ReadOnlyEvidenceSha256 = Hash([93]) }, "authority");
        Reject(request with { Fixups = request.Fixups with { MaximumCount = request.Fixups.MaximumCount + 1 } }, "authority");
        byte[] changedPayload = request.Payload.ToArray(); changedPayload[^1] ^= 1;
        Reject(request with { Payload = changedPayload, PayloadSha256 = Hash(changedPayload) }, "authority");
        byte[] changedSource = request.SourceScene.ToArray(); changedSource[^1] ^= 1;
        Reject(request with { SourceScene = changedSource, SourceSha256 = Hash(changedSource) }, "authority");
        NativePropertiesReservation redirected = request.Reservation with { Offset = request.Reservation.Offset + 4 };
        redirected = redirected with { BeforeSha256 = Hash(request.SourceScene.Span.Slice(redirected.Offset, redirected.ByteLength)) };
        Reject(request with { Reservation = redirected }, "authority");
        Reject(request with { Consumers = [request.Consumers[0] with { Id = "forged.consumer" }] }, "authority");
        NativeScenePropertiesLinkPlan repeat = NativeScenePropertiesLinker.Compile(request);
        Require(plan.Bindings.SequenceEqual(repeat.Bindings) && plan.Writes.SequenceEqual(repeat.Writes),
            "A rejected integrated request mutated a later valid plan.");
    }

    private static NativeScenePropertiesLinkRequest PrivateConsumer(NativeScenePropertiesLinkRequest request, NativePropertiesConsumer consumer) => request with
    {
        Consumers = [consumer], SharingAuthority = null,
        Reservation = request.Reservation with { Kind = NativePropertiesReservationKind.OwnedComponentExtent }
    };

    private static NativeScenePropertiesLinkRequest WithSecondActiveFixup(
        NativeScenePropertiesLinkRequest request, int field, uint target)
    {
        byte[] scene = request.SourceScene.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(scene.AsSpan(0x78), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(scene.AsSpan(0x80), checked((uint)field));
        BinaryPrimitives.WriteUInt32LittleEndian(scene.AsSpan(field), target);
        return request with
        {
            SourceScene = scene, SourceSha256 = Hash(scene),
            Fixups = request.Fixups with
            {
                CountBefore = 2,
                ActiveBeforeSha256 = Hash(scene.AsSpan(0x78, 12)),
                AppendCapacityBeforeSha256 = Hash(scene.AsSpan(0x84, 4))
            }
        };
    }

    private static void Reject(NativeScenePropertiesLinkRequest request, string text)
    {
        try { NativeScenePropertiesLinker.Compile(request); }
        catch (InvalidDataException exception) when (exception.Message.Contains(text, StringComparison.OrdinalIgnoreCase)) { return; }
        throw new InvalidOperationException($"Expected linker rejection containing '{text}'.");
    }
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static void Require(bool value, string text) { if (!value) throw new InvalidOperationException(text); }
}
