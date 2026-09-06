using System.Buffers.Binary;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Scene;

internal static class NativeResourceCompositionCases
{
    internal static void Run(NativeMobyModelRegistrationSource source, byte[] original, NativeSkyRelocationPayload[] inputs,
        Func<NativeByteExtent, byte[]> read, Action<bool, string> check, Action<Action, string> reject)
    {
        NativeLevelResourceCompositionLayout Build(IReadOnlyList<NativeSkyRelocationPayload>? payloads = null, IReadOnlyList<NativeResourceOriginalBytePatch>? patches = null) =>
            new(source, 0, 0, payloads ?? inputs, patches ?? [], read);
        var layout = Build(); byte[] final = layout.CopyFinalBytes();
        check(layout.MapOriginalRange(0x1810, 4).Offset == 0x2910 && layout.PayloadExtent(3).Offset == 0x3100, "original roots and append start map through common domain");
        check(layout.Subfiles[2].Growth == 0x800 && layout.Subfiles.Sum(s => s.Growth) == 0x1800, "per-owner rounding exactly once");
        foreach (var extent in layout.Preservations)
            check(original.AsSpan(extent.OriginalDataOffset, extent.ByteLength).SequenceEqual(final.AsSpan(extent.FinalDataOffset, extent.ByteLength)), "full original complement exact");
        final[0x800] ^= 1; reject(() => layout.RequireFinalBody(final), "changed protected final byte");
        check(layout.CopyFinalBytes()[0x800] != final[0x800], "final body copies detached");
        NativeResourceOriginalBytePatch P(int offset, byte after, string id = "field") => new(id, offset, original.AsSpan(offset, 1), [after]);
        var edited = Build(patches: [P(0x1000, 1)]);
        check(edited.CopyFinalBytes()[(int)edited.MapOriginalRange(0x1000, 1).Offset] == 1, "fixed field mapped across earlier subfile growth");
        check(Build(patches: [P(0x1000, 1, "z"), P(0x1000, 1, "a")]).CopyFinalBytes().AsSpan().SequenceEqual(edited.CopyFinalBytes()), "exact equivalent scalar claims deterministic");
        reject(() => Build(patches: [P(0x1000, 1), P(0x1000, 2)]), "contradictory scalar owners");
        reject(() => Build(patches: [new("header", 0x50, original.AsSpan(0x50, 4), new byte[4])]), "header ownership cannot be claimed by scalar stage");
        reject(() => Build(patches: [P(0x1810, 1)]), "old generic package bytes cannot be overwritten");
        reject(() => Build(patches: [new("stale", 0x1000, [0], [1])]), "stale original preimage");
        reject(() => Build(patches: [new("split", 0x103C, original.AsSpan(0x103C, 8), new byte[8])]), "insertion splits scalar owner");
        var replacement = inputs.Concat([new NativeSkyRelocationPayload(4, 37, 0x2000, 0, 0, 0x800, new byte[0x800], 3, false)]).ToArray();
        check(Build(replacement).CopyFinalBytes().AsSpan(0x3800, 0x800).ContainsAnyExcept((byte)0) == false, "complete fixed-size scene stage composed");
        reject(() => Build(replacement, [P(0x2010, 1)]), "scalar overlaps scene replacement");
        reject(() => Build(inputs.Select(p => p.PatchIndex == 2 ? p with { ModelBlockOffset = 0, OriginalWadOffset = 0x1800 } : p).ToArray()), "root0 start cannot be stolen by generic boundary rule");
        reject(() => Build(inputs.Select(p => p.PatchIndex == 2 ? p with { ModelBlockOffset = 0x20, OriginalWadOffset = 0x1820 } : p).ToArray()), "old generic model cannot be split");
        reject(() => Build(inputs.Select(p => p.PatchIndex == 3 ? p with { OriginalWadOffset = long.MaxValue } : p).ToArray()), "detached WAD address");
        reject(() => new NativeLevelResourceCompositionLayout(source, 0, long.MaxValue, inputs, [], read), "WAD offset overflow");
        reject(() => layout.MapOriginalRange(int.MaxValue, 4), "mapped offset overflow");
        reject(() => layout.MapOriginalRange(0x7FF, 2), "header-crossing range");
        reject(() => layout.MapOriginalRange(0xFFC, 8), "subfile-crossing range");
        reject(() => Build([..inputs, inputs[0]]), "duplicate payload identity");
        reject(() => Build(inputs.Select(p => p.PatchIndex == 3 ? p with { OriginalLength = 4, ModelBlockOffset = 0x7FC, OriginalWadOffset = 0x1FFC } : p).ToArray()), "original model replacement forbidden");
        reject(() => new NativeLevelResourceCompositionLayout(source, 0, 0, inputs, [], e => new byte[(int)e.ByteLength]), "wrong complete source bytes");
        byte[] sharedLarge = new byte[0x20000];
        var byteBudget = Enumerable.Range(0, 65).Select(i => new NativeSkyRelocationPayload(i, 37, 0x1000 + i * 4, 0, i * 4, 0, sharedLarge, 1, false)).ToArray();
        ExpectMessage(() => Build(byteBudget), "payload byte budget", "aggregate payload pre-copy refusal");
        var manyInserts = Enumerable.Range(0, 512).Select(i => new NativeSkyRelocationPayload(i + 10, 37, 0x1000 + i * 4, 0, i * 4, 0, new byte[4], 1, false)).ToArray();
        var manyPatches = Enumerable.Range(0, 1000).Select(i => P(0x800 + i, (byte)(original[0x800 + i] ^ 1), "patch" + i)).ToArray();
        ExpectMessage(() => Build(manyInserts, manyPatches), "work budget", "bounded conservative total preservation-work refusal");
        int reads = 0;
        ExpectMessage(() => new NativeLevelResourceCompositionLayout(source, 0, 0, inputs, [], e =>
            ++reads == 1 ? read(e) : read(e).Concat(new byte[4]).ToArray()), "source read length", "second source owner length checked before copy");
        reject(() => Build(new FiniteList<NativeSkyRelocationPayload>(1, 1025, inputs[0], true)), "payload declaration cannot hide a longer finite enumeration");
        reject(() => Build(new FiniteList<NativeSkyRelocationPayload>(1025, 0, inputs[0])), "over-budget payload declaration refused without enumeration");
        reject(() => Build(patches: new FiniteList<NativeResourceOriginalBytePatch>(0, 1, P(0x800, 1))), "patch declaration cannot hide a finite extra owner");
        reject(() => Build(patches: new FiniteList<NativeResourceOriginalBytePatch>(-1, 0, P(0x800, 1))), "negative patch declaration refused");
        reject(() => Build(patches: [new("width", 0x1000, [original[0x1000]], [1, 2])]), "changed scalar byte width refused");
        reject(() => Build(patches: [new("partial-a", 0x1000, original.AsSpan(0x1000, 2), [1, 2]), P(0x1001, 2, "partial-b")]), "equivalent partial overlap is not a shared owner");
        reject(() => Build(inputs.Select(p => p.PatchIndex == 0 ? p with { Bytes = [1, 2, 3] } : p).ToArray()), "unaligned structural insertion refused");
        reject(() => Build(inputs.Select(p => p.PatchIndex == 0 ? p with { SubfileIndex = 10 } : p).ToArray()), "out-of-schema subfile refused");
        var mutable = inputs.Select(p => p with { Bytes = p.Bytes.ToArray() }).ToArray();
        var snapshotted = Build(mutable); string snapshotHash = NativeMobyModelLoaderCertificate.Hash(snapshotted.CopyFinalBytes());
        mutable[0].Bytes[0] = 255;
        check(NativeMobyModelLoaderCertificate.Hash(snapshotted.CopyFinalBytes()) == snapshotHash, "input structural payload snapshot is immutable");
        Registration(source, original, inputs, read, check, reject);
        void ExpectMessage(Action action, string fragment, string label)
        {
            try { action(); } catch (InvalidDataException ex) { check(ex.Message.Contains(fragment, StringComparison.Ordinal), label); return; }
            throw new InvalidOperationException("Expected bounded refusal: " + label);
        }
    }

    private static void Registration(NativeMobyModelRegistrationSource source, byte[] original, NativeSkyRelocationPayload[] inputs,
        Func<NativeByteExtent, byte[]> targetRead, Action<bool, string> check, Action<Action, string> reject)
    {
        byte[] donor = original.ToArray(); Put(donor, 0x58, 0x1900); donor[0x152] = 2; donor[0x154] = 3;
        var donorDomain = new NativeAddressSpace(NativeAddressKind.LevelData, "donor-fixture", NativeAddressRevision.Original, donor.Length);
        byte[] Read(NativeByteExtent extent) => extent.Space == donorDomain ? donor.AsSpan((int)extent.Offset, (int)extent.ByteLength).ToArray() : targetRead(extent);
        var donorSource = NativeMobyModelRegistrationSource.DecodeFixture(donorDomain, donor);
        byte[] packages = donor.AsSpan(0x1810, 0x7F0).ToArray();
        var payloads = inputs.Select(p => p.PatchIndex == 3 ? p with { Bytes = packages } : p).ToArray();
        var layout = new NativeLevelResourceCompositionLayout(source, 0, 0, payloads, [], Read);
        var append = layout.PayloadExtent(3); List<NativeMobyDependencyNode> nodes = [];
        Add("package2", 0x1810, 0xF0, append.Offset); Add("package3", 0x1900, 0x700, append.Offset + 0xF0);
        var graph = NativeMobyDependencyClosureCompiler.Compile(nodes, nodes.Select(n => n.Id), Read);
        var bindings = new[]
        {
            NativeMobyModelPackageBinding.Capture(donorSource, 2, graph, "package2", "package", "fixture", Read),
            NativeMobyModelPackageBinding.Capture(donorSource, 3, graph, "package3", "package", "fixture", Read)
        };
        var loader = NativeMobyModelLoaderCertificate.DecodeFixture([]);
        var registration = NativeComposedMobyModelRegistrationPlan.Build(source, loader, layout, 3, bindings.Reverse(), Read);
        nodes.Add(registration.HeaderNode()); var finalGraph = NativeMobyDependencyClosureCompiler.Compile(nodes, nodes.Select(n => n.Id), Read);
        byte[] final = layout.CopyFinalBytes(); finalGraph.CopyPayload("resource.header").CopyTo(final, 0);
        registration.RequireFinal(final, finalGraph);
        check(registration.GenericCountBefore == 1 && registration.GenericCountAfter == 3, "append retains every original registration");
        check(registration.Packages.Select(p => p.ActorId).SequenceEqual(new ushort[] { 2, 3 }) &&
            Word(final, 0x58) == append.Offset && Word(final, 0x5C) == append.Offset + 0xF0, "new typed roots target deterministic final package extents");
        check(finalGraph.Installs.Count(i => i.Destination.Offset == 0 && i.Destination.Space == layout.Final) == 1 && finalGraph.References.Count == 2, "one final header owner with two typed package pointers");
        check(NativeComposedMobyModelRegistrationPlan.Build(source, loader, layout, 3, [..bindings, bindings[0]], Read).CopyHeader().AsSpan().SequenceEqual(registration.CopyHeader()), "same package request shares exact resource");
        reject(() => NativeComposedMobyModelRegistrationPlan.Build(source, NativeMobyModelLoaderCertificate.DecodeFixture([2]), layout, 3, bindings, Read), "shared PETE class collision");
        reject(() => NativeComposedMobyModelRegistrationPlan.Build(source, loader, layout, 2, bindings, Read), "registration cannot claim player insertion");
        var detached = new NativeLevelResourceCompositionLayout(source, 0, 0, payloads, [], Read);
        reject(() => NativeComposedMobyModelRegistrationPlan.Build(source, loader, detached, 3, bindings, Read), "equal-label detached final domain");
        reject(() => NativeComposedMobyModelRegistrationPlan.Build(NativeMobyModelRegistrationSource.DecodeFixture(source.Original, original), loader, layout, 3, bindings, Read), "detached original source issuer");
        byte[] bad = final.ToArray(); bad[0x58] ^= 4; reject(() => registration.RequireFinal(bad, finalGraph), "corrupted final root");
        bad = final.ToArray(); bad[(int)append.Offset] ^= 1; reject(() => registration.RequireFinal(bad, finalGraph), "corrupted final model payload");
        reject(() => NativeComposedMobyModelRegistrationPlan.Build(source, loader, layout, 3, [], Read), "empty model request refused");
        reject(() => NativeComposedMobyModelRegistrationPlan.Build(source, loader, layout, 3,
            new FiniteList<NativeMobyModelPackageBinding>(1, NativeMobyModelRegistrationPlan.MaximumRequestDeclarations + 1, bindings[0], true), Read), "model enumeration stops at finite request budget");
        var capacity = AtCapacity(61);
        check(capacity.GenericCountAfter == 63 && capacity.FreeSlotsAfter == 0 && capacity.Packages[^1].Slot == 63, "all63 generic slots supported without stealing special root0");
        reject(() => AtCapacity(62), "one more full registered package exceeds63 generic slots");
        reject(() => AtCapacity(1, true), "existing unplaced class cannot be replaced by imported package");
        NativeComposedMobyModelRegistrationPlan AtCapacity(int genericCount, bool collidingClass = false)
        {
            byte[] crowded = original.ToArray(); crowded.AsSpan(0x54, 63 * 4).Clear(); crowded.AsSpan(0x152, 63 * 2).Clear();
            for (int slot = 1; slot <= genericCount; slot++)
            {
                Put(crowded, 0x50 + slot * 4, (uint)(0x1810 + (slot - 1) * 4));
                BinaryPrimitives.WriteUInt16LittleEndian(crowded.AsSpan(0x150 + slot * 2), (ushort)(collidingClass ? 2 : 100 + slot));
            }
            var domain = new NativeAddressSpace(NativeAddressKind.LevelData, "capacity-" + genericCount, NativeAddressRevision.Original, crowded.Length);
            byte[] OwnerRead(NativeByteExtent e) => e.Space == domain ? crowded.AsSpan((int)e.Offset, (int)e.ByteLength).ToArray() : Read(e);
            var issuer = NativeMobyModelRegistrationSource.DecodeFixture(domain, crowded);
            var common = new NativeLevelResourceCompositionLayout(issuer, 0, 0, payloads, [], OwnerRead);
            var end = common.PayloadExtent(3); List<NativeMobyDependencyNode> owned = [];
            int cursor = 0;
            foreach (var (actor, offset, length) in new[] { (2, 0x1810, 0xF0), (3, 0x1900, 0x700) })
            {
                var extent = new NativeByteExtent(donorDomain, offset, length);
                var readOwner = new NativeMobyDependencyRead("package", extent, NativeMobyModelLoaderCertificate.Hash(OwnerRead(extent)));
                var install = new NativeMobyDependencyInstall("package", new(common.Final, end.Offset + cursor, length));
                owned.Add(new("capacity-package-" + actor, NativeMobyDependencyKind.ActorPackage, NativeMobyDependencyResolution.ImportFromSource,
                    NativeMobyDependencySharing.Immutable, readOwner.ExpectedSha256, [], [readOwner], c => new(c.Read("package"), [install], [])));
                cursor += length;
            }
            var catalog = NativeMobyDependencyClosureCompiler.Compile(owned, owned.Select(n => n.Id), OwnerRead);
            var bound = new[] { 2, 3 }.Select(actor => NativeMobyModelPackageBinding.Capture(donorSource, (ushort)actor, catalog, "capacity-package-" + actor, "package", "fixture", OwnerRead)).ToArray();
            var result = NativeComposedMobyModelRegistrationPlan.Build(issuer, loader, common, 3, bound, OwnerRead);
            owned.Add(result.HeaderNode()); var complete = NativeMobyDependencyClosureCompiler.Compile(owned, owned.Select(n => n.Id), OwnerRead);
            byte[] bytes = common.CopyFinalBytes(); complete.CopyPayload("resource.header").CopyTo(bytes, 0); result.RequireFinal(bytes, complete);
            foreach (var root in issuer.Registrations.Skip(1))
                check(Word(bytes, 0x50 + root.Slot * 4) == common.MapOriginalRange((int)root.RootDataOffset, 4).Offset, "every unplaced root preserved at capacity");
            return result;
        }
        void Add(string id, int offset, int length, long destination)
        {
            var extent = new NativeByteExtent(donorDomain, offset, length); var bytes = Read(extent);
            var r = new NativeMobyDependencyRead("package", extent, NativeMobyModelLoaderCertificate.Hash(bytes));
            nodes.Add(new(id, NativeMobyDependencyKind.ActorPackage, NativeMobyDependencyResolution.ImportFromSource, NativeMobyDependencySharing.Immutable,
                r.ExpectedSha256, [], [r], c => new(c.Read("package"), [new(id, new(layout.Final, destination, length))], [])));
        }
    }
    private static void Put(byte[] b, int o, uint v) => BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(o), v);
    private static uint Word(byte[] b, int o) => BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(o));

    // Finite tripwire: tests fail if a producer asks for more than its allowed
    // sentinel element. No infinite enumerator, stress allocation or timeout.
    private sealed class FiniteList<T>(int declared, int yielded, T value, bool tripwire = false) : IReadOnlyList<T>
    {
        public int Count => declared;
        public T this[int index] => throw new InvalidOperationException("Unexpected indexer access.");
        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < yielded; i++) yield return value;
            if (tripwire) throw new InvalidOperationException("Finite request enumeration exceeded its sentinel bound.");
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
