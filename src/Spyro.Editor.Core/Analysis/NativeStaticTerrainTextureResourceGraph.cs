namespace Spyro.Editor.Core.Analysis;

/// <summary>
/// Neutral canonical-static descriptor consumers and complete source rectangles.
/// Each material binding remains isolated, even when labels or bytes agree.
/// This is not retail coupled-palette sharing, source authority or allocation.
/// No source GPU buffer is copied or retained by the graph.
/// </summary>
internal sealed class NativeStaticTerrainTextureResourceGraph
{
    internal const int ConsumersPerRecord = 23 * 2;
    private const string InputShape = "GPU allocation requires a nonempty ordered array of bounded neutral material inputs.";

    private NativeStaticTerrainTextureResourceGraph(NativeStaticTerrainTextureLayout[] records,
        Resource[] resources, Resource[] owners, Consumer[] consumers, ContainedAlias[] aliases, long pairCheckCount)
    {
        Records = Array.AsReadOnly(records); Resources = Array.AsReadOnly(resources);
        Owners = Array.AsReadOnly(owners); Consumers = Array.AsReadOnly(consumers);
        ContainedAliases = Array.AsReadOnly(aliases); PairCheckCount = pairCheckCount;
    }

    internal IReadOnlyList<NativeStaticTerrainTextureLayout> Records { get; }
    internal IReadOnlyList<Resource> Resources { get; }
    internal IReadOnlyList<Resource> Owners { get; }
    internal IReadOnlyList<Consumer> Consumers { get; }
    internal IReadOnlyList<ContainedAlias> ContainedAliases { get; }
    internal long PairCheckCount { get; }

    internal static NativeStaticTerrainTextureResourceGraph Compile(
        IEnumerable<NativeStaticTerrainTextureInput> inputs, int maximumConsumers, long maximumPairChecks)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        if (maximumConsumers < ConsumersPerRecord || maximumPairChecks < 0)
            throw new InvalidDataException("Resource graph requires a complete-record consumer budget and nonnegative comparison-work budget.");

        // Do not trust Count or decode an early record before bounded enumeration
        // completes. foreach disposes the enumerator, including on excess/error.
        var selected = new List<NativeStaticTerrainTextureInput>();
        int maximumRecords = maximumConsumers / ConsumersPerRecord;
        foreach (var input in inputs)
        {
            if (selected.Count == maximumRecords)
                throw new InvalidDataException("Resource graph complete descriptor consumers exceed the declared budget.");
            selected.Add(input);
        }
        if (selected.Count == 0 || selected.Where((input, index) => input is null ||
            input.MaterialId != index || !Label(input.SourceIdentity) || !Label(input.AddressDomainIdentity) ||
            input.RelocationPolicy != NativeStaticTerrainTextureAllocation.RelocationPolicyId ||
            input.Record is null || input.Record.Length != 184 ||
            input.SourceVram is null || input.SourceVram.Length != 0x80000).Any())
            throw new InvalidDataException(InputShape);

        var records = selected.Select(input => NativeStaticTerrainTextureLayout.Decode(input.Record.ToArray())).ToArray();
        var resources = new List<Resource>();
        var consumers = new Consumer[selected.Count * ConsumersPerRecord];
        var distinct = new Dictionary<(int Material, string Kind, int Bpp, int Offset, int Width, int Rows), Resource>();
        var buckets = new Dictionary<(int Material, string Kind, int Bpp), List<Resource>>();
        for (int material = 0; material < records.Length; material++)
            for (int descriptor = 0; descriptor < 23; descriptor++)
            {
                var field = records[material].Descriptors[descriptor];
                Resource pixel = Intern(material, "pixel", field.BitsPerPixel, field.PixelByteOffset,
                    field.PixelRowByteLength, field.PixelRowCount);
                Resource palette = Intern(material, "palette", field.BitsPerPixel, field.PaletteByteOffset,
                    field.PaletteRowByteLength, field.PaletteRowCount);
                int at = material * ConsumersPerRecord + descriptor * 2;
                consumers[at] = Consumer.Create(material, descriptor, pixel);
                consumers[at + 1] = Consumer.Create(material, descriptor, palette);
            }

        long pairChecks = 0;
        // Bucketing precedes all pair scans. Neither equal labels nor unrelated
        // materials/interpretations contribute comparisons or shared owners.
        foreach (var bucket in buckets.Values)
            for (int i = 0; i < bucket.Count; i++)
                for (int j = 0; j < i; j++)
                {
                    ChargePair();
                    if (Overlaps(bucket[i], bucket[j]) && !Contains(bucket[i], bucket[j]) && !Contains(bucket[j], bucket[i]))
                        throw new InvalidDataException("Partially overlapping source rectangles lack an admitted alias policy.");
                }

        var owners = new List<Resource>();
        var aliases = new List<ContainedAlias>();
        var ownerBuckets = new Dictionary<(int Material, string Kind, int Bpp), List<Resource>>();
        // Preserve the old global stable sort and first containing owner, but
        // only visit candidate owners in this exact material/interpretation.
        foreach (var resource in resources.OrderByDescending(r => r.RowByteLength * r.RowCount).ThenBy(r => r.MaterialId)
            .ThenBy(r => r.Kind, StringComparer.Ordinal).ThenBy(r => r.Y).ThenBy(r => r.X).ThenBy(r => r.BitsPerPixel))
        {
            var domain = (resource.MaterialId, resource.Kind, resource.BitsPerPixel);
            if (!ownerBuckets.TryGetValue(domain, out var candidates))
                ownerBuckets.Add(domain, candidates = []);
            Resource? parent = null;
            foreach (var candidate in candidates)
            {
                ChargePair();
                if (Contains(candidate, resource)) { parent = candidate; break; }
            }
            if (parent is null) { owners.Add(resource); candidates.Add(resource); }
            else aliases.Add(ContainedAlias.Create(resource, parent));
        }

        return new(records, resources.ToArray(),
            owners.OrderBy(r => r.Kind != "palette").ThenByDescending(r => r.RowByteLength * r.RowCount)
                .ThenBy(r => r.MaterialId).ThenBy(r => r.Y).ThenBy(r => r.X).ThenBy(r => r.BitsPerPixel).ToArray(),
            consumers, aliases.OrderBy(a => a.Child.MaterialId).ThenBy(a => a.Child.Kind, StringComparer.Ordinal)
                .ThenBy(a => a.Child.Y).ThenBy(a => a.Child.X).ThenBy(a => a.Child.BitsPerPixel).ToArray(), pairChecks);

        Resource Intern(int material, string kind, int bpp, int offset, int width, int rows)
        {
            var key = (material, kind, bpp, offset, width, rows);
            if (distinct.TryGetValue(key, out var existing)) return existing;
            var resource = Resource.Create(selected[material], kind, bpp, offset, width, rows);
            distinct.Add(key, resource); resources.Add(resource);
            var domain = (material, kind, bpp);
            if (!buckets.TryGetValue(domain, out var bucket)) buckets.Add(domain, bucket = []);
            bucket.Add(resource);
            return resource;
        }

        void ChargePair()
        {
            if (pairChecks == maximumPairChecks)
                throw new InvalidDataException("Resource graph source-rectangle comparison work exceeds the declared budget.");
            pairChecks++;
        }
    }

    // Callers above have already selected an identical domain bucket.
    private static bool Contains(Resource a, Resource b) => a.X <= b.X && a.Y <= b.Y &&
        a.X + a.RowByteLength >= b.X + b.RowByteLength && a.Y + a.RowCount >= b.Y + b.RowCount;
    private static bool Overlaps(Resource a, Resource b) => a.X < b.X + b.RowByteLength && b.X < a.X + a.RowByteLength &&
        a.Y < b.Y + b.RowCount && b.Y < a.Y + a.RowCount;
    private static bool Label(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 1024 && !value.Any(char.IsControl);

    internal sealed class Resource
    {
        private Resource(NativeStaticTerrainTextureInput input, string kind, int bpp, int offset, int width, int rows)
        {
            MaterialId = input.MaterialId; SourceIdentity = input.SourceIdentity;
            AddressDomainIdentity = input.AddressDomainIdentity; RelocationPolicy = input.RelocationPolicy;
            Kind = kind; BitsPerPixel = bpp; SourceOffset = offset; RowByteLength = width; RowCount = rows;
        }
        internal static Resource Create(NativeStaticTerrainTextureInput input, string kind, int bpp, int offset, int width, int rows) =>
            new(input, kind, bpp, offset, width, rows);
        internal int MaterialId { get; }
        internal string SourceIdentity { get; }
        internal string AddressDomainIdentity { get; }
        internal string RelocationPolicy { get; }
        internal string Kind { get; }
        internal int BitsPerPixel { get; }
        internal int SourceOffset { get; }
        internal int RowByteLength { get; }
        internal int RowCount { get; }
        internal int X => SourceOffset % 1024;
        internal int Y => SourceOffset / 1024;
    }

    internal sealed class Consumer
    {
        private Consumer(int materialId, int descriptorIndex, Resource resource)
        { MaterialId = materialId; DescriptorIndex = descriptorIndex; Resource = resource; }
        internal static Consumer Create(int materialId, int descriptorIndex, Resource resource) => new(materialId, descriptorIndex, resource);
        internal int MaterialId { get; }
        internal int DescriptorIndex { get; }
        internal Resource Resource { get; }
    }

    internal sealed class ContainedAlias
    {
        private ContainedAlias(Resource child, Resource owner) { Child = child; Owner = owner; }
        internal static ContainedAlias Create(Resource child, Resource owner) => new(child, owner);
        internal Resource Child { get; }
        internal Resource Owner { get; }
        internal int RelativeByteOffset => Child.SourceOffset - Owner.SourceOffset;
    }
}
