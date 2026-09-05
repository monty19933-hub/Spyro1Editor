using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using Spyro.Editor.Core.Scene;

internal static class NativeMutableScenePropertiesLinkerCases
{
    public static void Run()
    {
        NativeMutableScenePropertiesLinkRequest request = Fixture();
        NativeScenePropertiesLinkPlan plan = NativeScenePropertiesLinker.CompileMutable(request);
        Require(plan.Bindings.Count == 2 && plan.Bindings[0].After == 0x180 && plan.Bindings[1].After == 0x1C0,
            "Two equal mutable payloads were not assigned independently owned extents.");
        Require(plan.FixupCountAfter == 6 && plan.Writes.Count == 3, "Mutable fixup replacement/removal count changed.");
        byte[] output = Apply(request.SourceScene.ToArray(), plan, inverse: false);
        Require(Word(output, 0x180) == 0x190 && Word(output, 0x1C0) == 0x1D0,
            "The installed payloads did not receive distinct scene-relative internal pointers.");
        Require(new uint[] { 0x10, 0x20, 0x80, 0x100, 0x180, 0x1C0 }.SequenceEqual(
            Enumerable.Range(0, 6).Select(index => Word(output, 0x244 + index * 4))),
            "The native fixup list was not rewritten in its original order.");
        Require(Word(output, 0x25C) == 0 && output.AsSpan(0x1A0, 0x10).SequenceEqual(request.SourceScene.Span.Slice(0x1A0, 0x10)) &&
                output.AsSpan(0x1E0, 0x10).SequenceEqual(request.SourceScene.Span.Slice(0x1E0, 0x10)),
            "Removed fixup capacity or preserved owned tails changed.");
        Require(Apply(output.ToArray(), plan, inverse: true).AsSpan().SequenceEqual(request.SourceScene.Span), "Mutable plan inverse was not byte exact.");
        Require(request.SourceScene.Span.SequenceEqual(Fixture().SourceScene.Span), "Mutable linkage mutated the input source.");
        Require(Describe(plan) == Describe(NativeScenePropertiesLinker.CompileMutable(request with { Instances = request.Instances.Reverse().ToArray() })),
            "Caller order changed independent mutable linkage.");

        Reject(request with { SourceSha256 = Hash([1]) }, "source");
        Reject(Change(request, 0, instance => instance with { OwnershipAuthority = null }), "authority");
        Reject(Change(request, 0, instance => instance with { Id = "other" }), "authority");
        Reject(Change(request, 0, instance => instance with { Payload = new byte[0x20] }), "payload");
        Reject(Change(request, 0, instance => instance with { PayloadSha256 = Hash([2]) }), "payload");
        Reject(Change(request, 0, instance => instance with { Consumer = instance.Consumer with { PointerFieldOffset = 0x30 } }), "authority");
        Reject(Change(request, 0, instance => instance with { InternalPointers = [] }), "authority");
        Reject(Change(request, 0, instance => instance with { FixupRewrites = [] }), "authority");
        Reject(Change(request, 0, instance => instance with { Reservation = instance.Reservation with { BeforeSha256 = Hash([3]) } }), "preimage");
        Reject(Resign(Change(request, 0, instance => instance with { Reservation = instance.Reservation with { Kind = NativePropertiesReservationKind.ExactPassiveTail } })), "owned");
        Reject(Resign(Change(request, 0, instance => instance with { Consumer = instance.Consumer with { AppendFixup = true } })), "existing");
        Reject(Resign(Change(request, 0, instance => instance with { InternalPointers = [new(0, 0x20)] })), "internal");
        Reject(Resign(Change(request, 0, instance => instance with { InternalPointers = [new(2, 0x10)] })), "internal");
        Reject(Resign(Change(request, 0, instance => instance with { InternalPointers = [new(0, 0x10), new(0, 0x10)] })), "duplicate");
        Reject(Resign(Change(request, 0, instance => instance with { FixupRewrites = [] })), "coverage");
        Reject(Resign(Change(request, 0, instance => instance with { FixupRewrites = [new(0x18C, null)] })), "coverage");
        Reject(Resign(Change(request, 0, instance => instance with { FixupRewrites = [new(0x18C, 4)] })), "internal");
        Reject(Resign(Change(request, 1, instance => instance with
        {
            Reservation = request.Instances[0].Reservation,
            FixupRewrites = request.Instances[0].FixupRewrites
        })), "overlap");
        Reject(Resign(Change(request, 1, instance => instance with { Consumer = request.Instances[0].Consumer })), "consumer");
        foreach (uint target in new uint[] { 0x180, 0x184, 0x1AF })
            Reject(MutateSource(request, scene => Put(scene, 0x80, target)), "undeclared");
        Reject(MutateSource(request, scene => Put(scene, 0x10, 0x184)), "owner");
        Reject(MutateSource(request, scene => Put(scene, 0x244, 0x240)), "envelope");
        Reject(MutateSource(request, scene => Put(scene, 0x248, 0x10)), "duplicate");
        Reject(MutateSource(request, scene => Put(scene, 0x100, 0x114)), "internal");
        Reject(request with { SourceScene = output, SourceSha256 = Hash(output) }, "preimage");
        NativeScenePropertiesLinkRequest grass = new(request.SourceScene, request.SourceSha256, Hash([4]),
            new byte[4], Hash(new byte[4]), [0], request.Instances[0].Reservation, request.Fixups, "unchanged-grass", [], null);
        try { NativeScenePropertiesLinker.Compile(grass); throw new InvalidOperationException("Pointer-free Grass entry point unexpectedly accepts mutable schema."); }
        catch (InvalidDataException error) when (error.Message.Contains("pointer-free", StringComparison.Ordinal)) { }
        Console.WriteLine("NATIVE_MUTABLE_SCENE_PROPERTIES_LINKER_SMOKE_OK two private instances, exact inverse, source-bound ownership, complete internal fixups, no sharing");
    }

    public static void RunIndependentDonor()
    {
        NativeMutableScenePropertiesLinkRequest local = Fixture();
        byte[] donorScene = new byte[0x100];
        local.Instances[0].Payload.Span.CopyTo(donorScene.AsSpan(0x20));
        Put(donorScene, 0x20, 0x30);
        Put(donorScene, 0x80, 1); Put(donorScene, 0x84, 0x20);
        NativePropertiesDonorScene donor = new(donorScene, Hash(donorScene),
            new(0x80, 1, 1, Hash(donorScene.AsSpan(0x80, 8)), Hash([])));
        NativeMutableScenePropertiesLinkRequest request = Resign(local with
        {
            Instances = local.Instances.Select(instance => instance with
            {
                DonorScene = donor, DonorPayloadSceneOffset = 0x20,
                DonorPayloadSha256 = Hash(donorScene.AsSpan(0x20, 0x20)),
                Payload = donorScene.AsMemory(0x20, 0x20).ToArray(), PayloadSha256 = Hash(donorScene.AsSpan(0x20, 0x20))
            }).ToArray()
        });
        NativeScenePropertiesLinkPlan plan = NativeScenePropertiesLinker.CompileMutable(request);
        byte[] output = Apply(request.SourceScene.ToArray(), plan, inverse: false);
        Require(Word(output, 0x180) == 0x190 && Word(output, 0x1C0) == 0x1D0 &&
                plan.Bindings.Count == 2 && plan.FixupCountAfter == 6,
            "A detached donor address space was not linked into two private destination instances.");
        Require(Apply(output.ToArray(), plan, inverse: true).AsSpan().SequenceEqual(request.SourceScene.Span),
            "Detached-donor plan inverse did not preserve the whole destination scene.");
        byte[] staleDonor = donorScene.ToArray(); staleDonor[^1] ^= 1;
        Reject(Change(request, 0, instance => instance with { DonorScene = donor with { Scene = staleDonor } }), "donor scene");
        Reject(Change(request, 0, instance => instance with { DonorScene = donor with { Scene = staleDonor, SourceSha256 = Hash(staleDonor) } }), "authority");
        Reject(Change(request, 0, instance => instance with { DonorScene = null }), "payload");
        byte[] missingFixup = donorScene.ToArray(); Put(missingFixup, 0x84, 0x24);
        NativePropertiesDonorScene unregistered = donor with
        {
            Scene = missingFixup, SourceSha256 = Hash(missingFixup),
            Fixups = donor.Fixups with { ActiveBeforeSha256 = Hash(missingFixup.AsSpan(0x80, 8)) }
        };
        Reject(Resign(Change(request, 0, instance => instance with { DonorScene = unregistered })), "internal");
        Require(Hash(donorScene) == donor.SourceSha256 && request.SourceScene.Span.SequenceEqual(local.SourceScene.Span),
            "Cross-scene linkage mutated its independently owned donor or destination.");
        Console.WriteLine("NATIVE_CROSS_SCENE_PROPERTIES_LINKER_SMOKE_OK independent donor/destination hashes and address spaces; two private instances; inverse and stale-donor guards");
    }

    private static NativeMutableScenePropertiesLinkRequest Fixture()
    {
        byte[] scene = new byte[0x300];
        Put(scene, 0x10, 0x180); Put(scene, 0x20, 0x1C0); Put(scene, 0x80, 0x110);
        Put(scene, 0x100, 0x110); Put(scene, 0x110, 2); Put(scene, 0x114, 123);
        Put(scene, 0x18C, 0x1A0); Put(scene, 0x1C0, 0x1D0); Put(scene, 0x1CC, 0x1E0);
        scene.AsSpan(0x1A0, 0x10).Fill(0xA5); scene.AsSpan(0x1E0, 0x10).Fill(0x5A);
        // Register the donor pointer as well; it is a distinct owner, never a mutable target.
        uint[] fields = [0x10, 0x20, 0x80, 0x100, 0x18C, 0x1C0, 0x1CC];
        Put(scene, 0x240, (uint)fields.Length);
        for (int index = 0; index < fields.Length; index++) Put(scene, 0x244 + index * 4, fields[index]);
        NativePropertiesFixupEnvelope fixups = new(0x240, 7, 7, Hash(scene.AsSpan(0x240, 32)), Hash([]));
        NativeMutablePropertiesInstance First(string id, int target, int consumer, params NativePropertiesFixupRewrite[] rewrites) => new(
            id, 0x100, Hash(scene.AsSpan(0x100, 0x20)), scene.AsMemory(0x100, 0x20).ToArray(), Hash(scene.AsSpan(0x100, 0x20)),
            new(NativePropertiesReservationKind.OwnedComponentExtent, target, 0x30, Hash(scene.AsSpan(target, 0x30)), Hash([42])),
            new(id, consumer, false), [new(0, 0x10)], rewrites, null);
        return Resign(new(scene, Hash(scene), fixups,
            [First("one", 0x180, 0x10, new NativePropertiesFixupRewrite(0x18C, 0)),
             First("two", 0x1C0, 0x20, new NativePropertiesFixupRewrite(0x1C0, 0), new NativePropertiesFixupRewrite(0x1CC, null))]));
    }

    // Synthetic certificates exist only in this test through private-constructor
    // reflection. Production evidence is issued by the exact native adapter.
    private static NativeMutableScenePropertiesLinkRequest Resign(NativeMutableScenePropertiesLinkRequest request) => request with
    {
        Instances = request.Instances.Select(instance => instance with
        {
            OwnershipAuthority = (NativePropertiesMutableOwnershipAuthority)typeof(NativePropertiesMutableOwnershipAuthority)
                .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single().Invoke([
                    instance.Reservation.OwnershipEvidenceSha256, request.SourceSha256, request.Fixups,
                    NativePropertiesMutableOwnershipAuthority.Fingerprint(instance)])
        }).ToArray()
    };
    private static NativeMutableScenePropertiesLinkRequest Change(NativeMutableScenePropertiesLinkRequest request, int index,
        Func<NativeMutablePropertiesInstance, NativeMutablePropertiesInstance> edit) => request with
        { Instances = request.Instances.Select((instance, current) => current == index ? edit(instance) : instance).ToArray() };
    private static NativeMutableScenePropertiesLinkRequest MutateSource(NativeMutableScenePropertiesLinkRequest request, Action<byte[]> edit)
    {
        byte[] scene = request.SourceScene.ToArray(); edit(scene);
        return Resign(request with
        {
            SourceScene = scene, SourceSha256 = Hash(scene),
            Fixups = request.Fixups with { ActiveBeforeSha256 = Hash(scene.AsSpan(0x240, 32)) },
            Instances = request.Instances.Select(instance => instance with
            {
                DonorPayloadSha256 = Hash(scene.AsSpan(instance.DonorPayloadSceneOffset, instance.Payload.Length)),
                Reservation = instance.Reservation with { BeforeSha256 = Hash(scene.AsSpan(instance.Reservation.Offset, instance.Reservation.ByteLength)) }
            }).ToArray()
        });
    }
    private static byte[] Apply(byte[] scene, NativeScenePropertiesLinkPlan plan, bool inverse)
    {
        foreach (NativePropertiesSceneWrite write in plan.Writes)
        {
            byte[] before = Convert.FromHexString(inverse ? write.AfterHex : write.BeforeHex);
            byte[] after = Convert.FromHexString(inverse ? write.BeforeHex : write.AfterHex);
            Require(scene.AsSpan(write.Offset, before.Length).SequenceEqual(before), "Mutable write preimage mismatch.");
            after.CopyTo(scene, write.Offset);
        }
        return scene;
    }
    private static string Describe(NativeScenePropertiesLinkPlan plan) => string.Join("|", plan.Writes.Select(write => $"{write.Offset}:{write.BeforeHex}:{write.AfterHex}"));
    private static void Reject(NativeMutableScenePropertiesLinkRequest request, string reason)
    {
        try { NativeScenePropertiesLinker.CompileMutable(request); }
        catch (InvalidDataException error) when (error.Message.Contains(reason, StringComparison.OrdinalIgnoreCase)) { return; }
        throw new InvalidOperationException($"Expected mutable linker rejection containing '{reason}'.");
    }
    private static uint Word(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset));
    private static void Put(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static void Require(bool value, string text) { if (!value) throw new InvalidOperationException(text); }
}
