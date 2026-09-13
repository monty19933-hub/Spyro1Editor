using System.Collections;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Spyro.Editor.Core.Analysis;
using static Program;

// Tests-first contract for the internal canonical-static resource graph.
// Compile(IEnumerable<NativeStaticTerrainTextureInput>, int maximumConsumers,
//         long maximumPairChecks) is neutral and keeps per-material isolation.
// Records/Resources/Owners/Consumers/ContainedAliases are immutable collections.
// Resource: MaterialId, SourceIdentity, AddressDomainIdentity, RelocationPolicy,
// Kind, BitsPerPixel, SourceOffset, RowByteLength, RowCount.
// Consumer: MaterialId, DescriptorIndex, Resource (the actual graph-owned node).
// Alias: Child, Owner (actual nodes), RelativeByteOffset. PairCheckCount is the
// number of same-material/kind/format candidate comparisons, never all-N pairs.
// This is NOT retail global-repacker coupled-palette or runtime sharing policy.
internal static class ResourceGraphCases
{
    private static Type? graphType;
    private static MethodInfo? compile;

    internal static void Run()
    {
        graphType = typeof(NativeStaticTerrainTextureAllocation).Assembly.GetType(
            "Spyro.Editor.Core.Analysis.NativeStaticTerrainTextureResourceGraph");
        Check(graphType is { IsPublic: false, IsSealed: true },
            "Missing internal canonical static texture resource/consumer graph; neutral labels must not issue a source capability.");
        Check(graphType!.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .All(c => c.IsPrivate) &&
            graphType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static).Length == 0,
            "Resource graph has no public construction, source certificate, destination allocation or runtime/publication API.");
        compile = graphType.GetMethod("Compile", BindingFlags.Static | BindingFlags.NonPublic);
        Check(compile != null && compile.ReturnType == graphType && compile.GetParameters().Select(p => p.ParameterType)
                .SequenceEqual(new[] { typeof(IEnumerable<NativeStaticTerrainTextureInput>), typeof(int), typeof(long) }),
            "Graph accepts bounded neutral records/labels with consumer and comparison-work budgets, not a BIN, target upload or source issuer.");
        CompleteConsumersAndOrder();
        ContainmentAndInterpretation();
        IsolationAndWork();
        BoundedRefusals();
        Console.WriteLine("NATIVE_STATIC_TEXTURE_RESOURCE_GRAPH_OK sourceOpened=false allocationIssued=false perMaterialIsolation=true");
    }

    private static void CompleteConsumersAndOrder()
    {
        var inputs = AllocationCases.Inputs();
        string[] before = inputs.SelectMany(i => new[] { Hash(i.Record), Hash(i.SourceVram) }).ToArray();
        object graph = Compile(inputs, 92, 0);
        object[] resources = Items(graph, "Resources"), owners = Items(graph, "Owners"), consumers = Items(graph, "Consumers");
        var records = Property<IReadOnlyList<NativeStaticTerrainTextureLayout>>(graph, "Records");
        Check(records.Count == 2 && records.All(r => r.Descriptors.Count == 23) && consumers.Length == 92,
            "Every ordered material exposes all23 descriptor identities times pixel and full-palette consumers.");
        var firstSeen = new[]
        {
            (0, "pixel", 4, 0, 16, 32), (0, "palette", 4, 128 * 1024, 32, 16),
            (0, "pixel", 8, 64 * 1024, 32, 32), (0, "palette", 8, 160 * 1024, 512, 1),
            (1, "pixel", 4, 0, 16, 32), (1, "palette", 4, 128 * 1024, 32, 16),
            (1, "pixel", 8, 64 * 1024, 32, 32), (1, "palette", 8, 160 * 1024, 512, 1)
        };
        Check(resources.Select(Node).SequenceEqual(firstSeen),
            "Distinct resource nodes retain first-seen material/descriptor/pixel-then-palette order.");
        int[] ownerOrder = [1, 3, 5, 7, 2, 6, 0, 4];
        Check(owners.Length == 8 && owners.Zip(ownerOrder).All(pair => ReferenceEquals(pair.First, resources[pair.Second])) &&
            Items(graph, "ContainedAliases").Length == 0 && Property<long>(graph, "PairCheckCount") == 0,
            "Owner order is the old palette/descending-area/material/Y/X/Bpp order; independent singleton buckets do no pair work.");
        bool complete = true;
        for (int material = 0; material < 2; material++)
            for (int descriptor = 0; descriptor < 23; descriptor++)
                for (int kind = 0; kind < 2; kind++)
                {
                    object consumer = consumers[material * 46 + descriptor * 2 + kind];
                    object resource = Property<object>(consumer, "Resource");
                    int node = material * 4 + (descriptor < 3 ? 0 : 2) + kind;
                    complete &= Property<int>(consumer, "MaterialId") == material &&
                        Property<int>(consumer, "DescriptorIndex") == descriptor && ReferenceEquals(resource, resources[node]);
                    complete &= Property<string>(resource, "SourceIdentity") == inputs[material].SourceIdentity &&
                        Property<string>(resource, "AddressDomainIdentity") == inputs[material].AddressDomainIdentity &&
                        Property<string>(resource, "RelocationPolicy") == inputs[material].RelocationPolicy;
                }
        Check(complete, "All92 consumers point to their actual graph-owned pixel/palette node and retain complete independent source labels.");
        Check(inputs.SelectMany(i => new[] { Hash(i.Record), Hash(i.SourceVram) }).SequenceEqual(before),
            "Graph construction does not alter any caller record or source GPU byte.");
        string frozen = Describe(graph);
        inputs[0].Record[0] ^= 1; inputs[0].SourceVram[0] ^= 1;
        inputs[1] = inputs[0];
        byte[] copy = records[0].CopyRecord(); copy[0] ^= 1;
        Check(Describe(graph) == frozen && records[0].RecordSha256 == before[0],
            "Caller array replacement, raw record/GPU mutation and returned record copies cannot mutate the graph.");
        foreach (string name in new[] { "Records", "Resources", "Owners", "Consumers", "ContainedAliases" })
        {
            object collection = Property<object>(graph, name);
            Check(collection is IList { IsReadOnly: true }, "Graph exposes an immutable collection: " + name);
        }
        Check(Describe(Compile(AllocationCases.Inputs(), 92, 0)) == frozen,
            "Fresh independent reconstruction preserves graph node order, consumer mapping and record bytes.");
    }

    private static void ContainmentAndInterpretation()
    {
        var one = AllocationCases.Inputs()[0];
        byte[] record = one.Record.ToArray();
        Close16(record, 7, 16, 80); Close16(record, 8, 16, 80); Close16(record, 9, 0, 64);
        object graph = Compile([one with { Record = record }], 46, 20);
        long used = Property<long>(graph, "PairCheckCount");
        Check(used > 0 && Describe(Compile([one with { Record = record }], 46, used)) == Describe(graph),
            "The exact positive comparison-work budget admits the same complete graph.");
        Reject(() => Compile([one with { Record = record }], 46, used - 1),
            "One fewer comparison than the complete graph requires is refused; positive limits are enforced exactly.");
        object[] resources = Items(graph, "Resources"), owners = Items(graph, "Owners"), aliases = Items(graph, "ContainedAliases");
        Check(resources.Length == 6 && owners.Length == 4 && aliases.Length == 2,
            "Repeated close16 consumers collapse exact aliases; distinct contained footprints remain nodes under the complete32px owner.");
        Check(ReferenceEquals(Property<object>(aliases[0], "Child"), resources[5]) &&
            ReferenceEquals(Property<object>(aliases[1], "Child"), resources[4]) &&
            aliases.All(a => ReferenceEquals(Property<object>(a, "Owner"), resources[2])) &&
            aliases.Select(a => Property<int>(a, "RelativeByteOffset")).SequenceEqual(new[] { 0, 16 * 1024 + 16 }),
            "Containment keeps old child material/kind/Y/X/Bpp order and source-row-stride offsets, including same-origin smaller rectangles.");
        object[] consumers = Items(graph, "Consumers");
        Check(ReferenceEquals(Property<object>(consumers[14], "Resource"), Property<object>(consumers[16], "Resource")) &&
            !ReferenceEquals(Property<object>(consumers[18], "Resource"), Property<object>(consumers[6], "Resource")),
            "Descriptor identity survives exact sharing, while same-origin footprints with different dimensions stay distinct.");
        Reject(() => Compile([one with { Record = record }], 46, 0),
            "An exhausted same-domain comparison budget refuses before an incomplete graph can escape.");

        byte[] mixed = one.Record.ToArray();
        // Put an HQ512-byte CLUT across the first row of the LQ32x16 CLUT.
        // Fresh intentionally keeps these interpretations independent. The retail
        // global packer's coupled palette component semantics do not apply here.
        for (int descriptor = 3; descriptor < 23; descriptor++) mixed[descriptor * 8 + 3] = 0x20;
        object separate = Compile([one with { Record = mixed }], 46, 0);
        Check(Items(separate, "Owners").Length == 4 && Items(separate, "ContainedAliases").Length == 0,
            "Overlapping4bpp distance-palette and8bpp CLUT footprints retain the current separate interpretation domains.");
        byte[] pixelPalette = one.Record.ToArray();
        for (int descriptor = 0; descriptor < 3; descriptor++) pixelPalette[descriptor * 8 + 3] = 0;
        Check(Items(Compile([one with { Record = pixelPalette }], 46, 0), "Owners").Length == 4,
            "Physically overlapping pixel/palette footprints do not merge merely because their Bpp and source coordinates agree.");
    }

    private static void IsolationAndWork()
    {
        var sample = AllocationCases.Inputs()[0];
        // Sharing even the actual caller byte arrays and every label still does
        // not authorize cross-material allocation sharing in this graph profile.
        var repeated = Enumerable.Range(0, 31).Select(id => sample with { MaterialId = id }).ToArray();
        long before = GC.GetAllocatedBytesForCurrentThread();
        object graph = Compile(repeated, 31 * 46, 0);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(Items(graph, "Consumers").Length == 31 * 46 && Items(graph, "Resources").Length == 31 * 4 &&
            Items(graph, "Owners").Length == 31 * 4 && Property<long>(graph, "PairCheckCount") == 0,
            "Many repeated bindings remain isolated without an arbitrary material cap or cross-material pair comparison work.");
        Check(allocated < 31L * 0x80000 / 2,
            "Metadata graph does not materialize a complete source-VRAM clone per binding; allocation remains the GPU payload owner.");
        foreach (var other in new[]
        {
            sample with { MaterialId = 1 },
            sample with { MaterialId = 1, AddressDomainIdentity = "different-address-domain" },
            sample with { MaterialId = 1, SourceIdentity = "different-display-or-hash-label" }
        })
        {
            object two = Compile([sample, other], 92, 0);
            Check(Items(two, "Owners").Length == 8 && Items(two, "ContainedAliases").Length == 0,
                "Equal source labels, equal bytes, or equal address-domain labels never promote independent material bindings to shared source authority.");
        }
    }

    private static void BoundedRefusals()
    {
        var inputs = AllocationCases.Inputs();
        var stream = new ObservedInputs(inputs[0], malformedRecord: true);
        Reject(() => Compile(stream, 92, 1000), "Consumer budget bounds enumeration even when reported Count lies.");
        Check(stream.Visited == 3 && stream.Disposed == 1,
            "Bounded input collection stops at the first excess record and disposes once before decoding any earlier malformed descriptor.");
        foreach (var budget in new[] { (0, 0L), (45, 0L), (92, -1L) })
        {
            var never = new ObservedInputs(inputs[0]);
            Reject(() => Compile(never, budget.Item1, budget.Item2), "Invalid graph budget refuses before enumeration or graph materialization.");
            Check(never.Visited == 0 && never.Disposed == 0, "Invalid budgets do not acquire an input enumerator.");
        }
        Reject(() => Compile(inputs, 91, 1000), "A partial46-consumer budget cannot truncate the second complete record.");
        Reject(() => Compile([], 92, 1000), "An empty graph is outside the existing nonempty canonical allocator input grammar.");
        foreach (var bad in new[]
        {
            inputs.Reverse().ToArray(), new[] { inputs[0], null! },
            new[] { inputs[0] with { MaterialId = -1 } },
            new[] { inputs[0] with { SourceIdentity = "" } },
            new[] { inputs[0] with { AddressDomainIdentity = "" } },
            new[] { inputs[0] with { AddressDomainIdentity = "domain\nother" } },
            new[] { inputs[0] with { SourceIdentity = new string('x', 1025) } },
            new[] { inputs[0] with { RelocationPolicy = "caller-promoted-sharing" } },
            new[] { inputs[0] with { Record = new byte[183] } },
            new[] { inputs[0] with { SourceVram = new byte[0x80001] } }
        }) Reject(() => Compile(bad, 92, 1000), "Malformed neutral input retains the old ordered/label/record/GPU-shape contract.");
        byte[] pixels = inputs[0].Record.ToArray(); pixels[32] = 16; pixels[36] = 47;
        byte[] palettes = inputs[0].Record.ToArray(); palettes[3 * 8 + 2] = 0x21;
        foreach (byte[] bad in new[] { pixels, palettes })
        {
            try { Compile([inputs[0] with { Record = bad }], 46, 1000); }
            catch (InvalidDataException error)
            {
                Check(error.Message == "Partially overlapping source rectangles lack an admitted alias policy.",
                    "Pixel and full-CLUT partial overlaps retain the exact existing fail-closed refusal.");
                continue;
            }
            throw new InvalidDataException("Expected partial source-resource overlap refusal.");
        }
        var primary = new IOException("actual resource-graph enumeration failure");
        IEnumerable<NativeStaticTerrainTextureInput> Broken()
        { yield return inputs[0]; throw primary; }
        try { Compile(Broken(), 92, 1000); }
        catch (IOException error) { Check(ReferenceEquals(error, primary), "Actual enumeration failure preserves exception identity."); return; }
        throw new InvalidDataException("Expected original enumeration exception.");
    }

    private static void Close16(byte[] record, int descriptor, byte x, byte y)
    {
        int at = descriptor * 8; record[at] = x; record[at + 1] = y;
        record[at + 4] = checked((byte)(x + 15)); record[at + 5] = y;
    }
    private static object Compile(IEnumerable<NativeStaticTerrainTextureInput> inputs, int consumers, long pairs)
    {
        try { return compile!.Invoke(null, new object[] { inputs, consumers, pairs })!; }
        catch (TargetInvocationException error) when (error.InnerException != null)
        { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
    }
    private static T Property<T>(object value, string name) => (T)(value.GetType().GetProperty(name,
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(value)
        ?? throw new InvalidDataException("Missing resource graph property: " + name));
    private static object[] Items(object graph, string name) => ((IEnumerable)Property<object>(graph, name)).Cast<object>().ToArray();
    private static (int Material, string Kind, int Bpp, int Offset, int Width, int Rows) Node(object value) =>
        (Property<int>(value, "MaterialId"), Property<string>(value, "Kind"), Property<int>(value, "BitsPerPixel"),
            Property<int>(value, "SourceOffset"), Property<int>(value, "RowByteLength"), Property<int>(value, "RowCount"));
    private static string Describe(object graph) =>
        string.Join("|", Property<IReadOnlyList<NativeStaticTerrainTextureLayout>>(graph, "Records").Select(r => Hash(r.CopyRecord()))) + ";" +
        string.Join("|", Items(graph, "Resources").Select(r => Node(r).ToString())) + ";" +
        string.Join("|", Items(graph, "Owners").Select(r => Node(r).ToString())) + ";" +
        string.Join("|", Items(graph, "Consumers").Select(c => $"{Property<int>(c, "MaterialId")}:{Property<int>(c, "DescriptorIndex")}:{Node(Property<object>(c, "Resource"))}"));

    private sealed class ObservedInputs(NativeStaticTerrainTextureInput input, bool malformedRecord = false)
        : IReadOnlyCollection<NativeStaticTerrainTextureInput>
    {
        internal int Visited, Disposed;
        public int Count => 0;
        public IEnumerator<NativeStaticTerrainTextureInput> GetEnumerator()
        {
            try
            {
                for (int material = 0; ; material++)
                {
                    Visited++;
                    byte[] record = input.Record.ToArray();
                    if (malformedRecord) record[8] ^= 2;
                    yield return input with { MaterialId = material, Record = record };
                }
            }
            finally { Disposed++; }
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
