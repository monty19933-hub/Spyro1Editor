using System.Buffers.Binary;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

// Typed test-only composition of EXISTING compilers. No duplicate emitter,
// production hook, native initializer, allocator or serializer.
internal sealed class ExistingDetachedCallables
{
    internal const uint AttemptPc = 0x800E0000, PredicateBase = 0x800F0000, CollectorPc = 0x80100000, QueryPc = 0x80110000, Ledger = 0x801FF600;
    private readonly DetachedCollectibleManifest manifest;
    private readonly NativeJapaneseEggPickupPredicateKernel predicate;
    private readonly NativeCollectibleCollectionKernel collector;
    private readonly NativeJapaneseEggCollectionAttempt attempt;
    private readonly NativeCollectibleStateQueryKernel query;
    private readonly NativeAddressSpace domain = new(NativeAddressKind.LevelData, "scheduling-test-only", NativeAddressRevision.Final, 0x10000);
    internal byte[] AttemptBytes => attempt.CopyBytes();
    internal byte[] PredicateBytes => predicate.CopyBytes();
    internal byte[] CollectorBytes => collector.CopyBytes();
    internal byte[] QueryBytes => query.CopyBytes();
    internal uint PredicatePc => predicate.EntryPc;
    internal int MaximumStackBytes => attempt.MaximumStackByteLength;
    private static Guid Id(int value) => Guid.Parse(value.ToString("x32"));
    private static NativeJapaneseEggCollectibleIdentity[] Mappings() => [new("egg-a", Id(1)), new("egg-b", Id(2))];
    internal ExistingDetachedCallables(NativeJapaneseEggAuthoredClassesPlan authored, NativeRegionalSourceSession jp, NativeRegionalSourceSession usa)
    {
        manifest = DetachedCollectibleManifest.CreateNew(Guid.Parse("12345678-1111-2222-3333-444444444444"), [Id(1), Id(2)]);
        var binding = NativeJapaneseEggCollectibleBinding.Create(authored, manifest, Mappings());
        var source = NativeJapaneseEggPickupPredicateSource.Capture(jp, usa);
        predicate = source.Compile(new(domain, 0x1000, 0x154), PredicateBase);
        collector = CompileCollector(manifest);
        query = NativeCollectibleStateQueryKernel.Compile(manifest, 1,
            new NativeByteExtent(domain, 0x4000, 0x2000), QueryPc, Ledger);
        attempt = NativeJapaneseEggCollectionAttempt.Compile(binding, authored, manifest, predicate, collector,
            new NativeByteExtent(domain, 0, 0x1000), AttemptPc);
        if (!attempt.SourceBound || !predicate.SourceBound) throw new Exception("Lost source ancestry.");
    }
    private NativeCollectibleCollectionKernel CompileCollector(DetachedCollectibleManifest manifest) => NativeCollectibleCollectionKernel.Compile(manifest, 1,
        new NativeByteExtent(domain, 0x2000, 0x2000), CollectorPc, Ledger);

    // Independent RAM fixture, NOT a native initializer or alternate card codec.
    private static byte[] LedgerFixture(DetachedCollectibleManifest manifest, byte earned, byte active)
    {
        byte[] result = new byte[0x200];
        void Word(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(offset), value);
        void Half(int offset, int value) => BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(offset), (ushort)value);
        Word(0x10, 0x524C4353); Half(0x14, 1); result[0x16] = result[0x17] = result[0x18] = 1;
        manifest.SerializeCanonical().AsSpan(0x10, 16).CopyTo(result.AsSpan(0x20));
        Convert.FromHexString(manifest.Sha256).CopyTo(result, 0x30);
        Word(0x70, manifest.Revision); Half(0x74, 2); Half(0x76, 1);
        Half(0x78, System.Numerics.BitOperations.PopCount((uint)active));
        Half(0x7A, System.Numerics.BitOperations.PopCount((uint)(earned & active)));
        result[0x90] = earned; result[0xA0] = active; return result;
    }
    internal byte[] EmptyLedger => LedgerFixture(manifest, 0, 3);
    internal byte[] EarnedFirstLedger => LedgerFixture(manifest, 1, 3);
    internal (byte[] Code, byte[] Ledger) RetiredFirst(NativeJapaneseEggAuthoredClassesPlan authored, Action<bool, string> check)
    {
        var retired = manifest.Advance([], [Id(1)]);
        try { NativeJapaneseEggCollectibleBinding.Create(authored, retired, Mappings()); throw new Exception("Retired mapping accepted."); }
        catch (InvalidDataException e) { check(e.Message.Contains("retired", StringComparison.Ordinal), "retired identity cannot bind a live authored row"); }
        return (CompileCollector(retired).CopyBytes(), LedgerFixture(retired, 1, 2));
    }
}
