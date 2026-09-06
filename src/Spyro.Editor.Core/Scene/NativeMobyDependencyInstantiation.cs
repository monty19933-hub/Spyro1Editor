using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.Core.Scene;

internal enum NativeMobyDependencyActivation { ConcurrentLevel, SequentialUnproved }
internal sealed record NativeMobyDependencyOwnerField(int PropertyByteOffset, int OwnerRoleIndex);
internal sealed record NativeMobyDependencyPrivateSchema(string Role, int SourceInstanceIndex, ushort NativeClass,
    NativeMobyDependencyRead Row, NativeMobyDependencyRead Properties, long PropertiesPointerBaseOffset,
    IReadOnlyList<NativeMobyDependencyOwnerField> OwnerFields, bool RequiresBytePersistentOwner);
internal sealed record NativeMobyDependencyMutableGlobal(string Role, NativeAddressSpace ExecutableDomain,
    uint RuntimeAddress, int ByteLength, IReadOnlyList<NativeMobyDependencyRead> Consumers);
internal sealed record NativeMobyDependencyInstantiationRequest(string Id, NativeMobyDependencyInstanceTemplate Template,
    NativeMobyDependencyActivation Activation = NativeMobyDependencyActivation.ConcurrentLevel);

/// <summary>
/// Detached native schema captured by a code-owned factory. Full private/code preimages and a
/// closed immutable graph are required; a catalog name or caller-authored hash is not sufficient.
/// Capture checks byte ownership, not the truth of an arbitrary behavior schema. Only validated
/// native factories may choose class semantics, owner fields and mutable-global consumers.
/// </summary>
internal sealed class NativeMobyDependencyInstanceTemplate
{
    private NativeMobyDependencyInstanceTemplate(string policy, NativeMobyDependencyPrivateSchema[] rows,
        NativeMobyDependencyMutableGlobal[] globals, NativeMobyDependencyClosurePlan shared)
    { Policy = policy; Rows = Array.AsReadOnly(rows); Globals = Array.AsReadOnly(globals); Shared = shared; }
    public string Policy { get; }
    public IReadOnlyList<NativeMobyDependencyPrivateSchema> Rows { get; }
    public IReadOnlyList<NativeMobyDependencyMutableGlobal> Globals { get; }
    public NativeMobyDependencyClosurePlan Shared { get; }

    public static NativeMobyDependencyInstanceTemplate Capture(string policy,
        IEnumerable<NativeMobyDependencyPrivateSchema> schemas,
        IEnumerable<NativeMobyDependencyMutableGlobal> mutableGlobals,
        NativeMobyDependencyClosurePlan shared, Func<NativeByteExtent, byte[]> readSource)
    {
        if (string.IsNullOrWhiteSpace(policy)) throw new InvalidDataException("Native instantiation requires a code-owned relocation policy.");
        ArgumentNullException.ThrowIfNull(schemas); ArgumentNullException.ThrowIfNull(mutableGlobals);
        ArgumentNullException.ThrowIfNull(shared); ArgumentNullException.ThrowIfNull(readSource);
        NativeMobyDependencyPrivateSchema[] rows = schemas.ToArray();
        if (rows.Length == 0 || rows.Any(r => r == null || string.IsNullOrWhiteSpace(r.Role) || r.OwnerFields == null || r.Row == null || r.Properties == null) ||
            rows.Select(r => r.Role).Distinct(StringComparer.Ordinal).Count() != rows.Length ||
            rows.Select(r => r.SourceInstanceIndex).Distinct().Count() != rows.Length)
            throw new InvalidDataException("Private native roles/source instances must be nonempty and unique.");
        rows = rows.Select(r => r with { OwnerFields = Array.AsReadOnly(r.OwnerFields.ToArray()) }).ToArray();
        byte[] Read(NativeMobyDependencyRead r)
        {
            if (r == null || r.Extent == null || r.Extent.Space.Revision != NativeAddressRevision.Original)
                throw new InvalidDataException("Native instance source requires an exact original address domain.");
            byte[] bytes = readSource(r.Extent)?.ToArray() ?? throw new InvalidDataException("Native instance source returned no bytes.");
            if (bytes.LongLength != r.Extent.ByteLength || Hash(bytes) != r.ExpectedSha256)
                throw new InvalidDataException("Native instance full source preimage changed.");
            return bytes;
        }
        foreach (NativeMobyDependencyPrivateSchema row in rows)
        {
            byte[] nativeRow = Read(row.Row), props = Read(row.Properties);
            if (row.SourceInstanceIndex < 0 || row.RequiresBytePersistentOwner && row.SourceInstanceIndex >= 256 ||
                nativeRow.Length != 0x58 || props.Length < 4 || props.Length % 4 != 0 ||
                row.Row.Extent.Space != row.Properties.Extent.Space || row.PropertiesPointerBaseOffset < 0 ||
                row.Properties.Extent.Offset < row.PropertiesPointerBaseOffset ||
                row.Properties.Extent.Offset - row.PropertiesPointerBaseOffset > uint.MaxValue ||
                BinaryPrimitives.ReadUInt32LittleEndian(nativeRow) != row.Properties.Extent.Offset - row.PropertiesPointerBaseOffset ||
                BinaryPrimitives.ReadUInt16LittleEndian(nativeRow.AsSpan(0x36)) != row.NativeClass)
                throw new InvalidDataException("Native private row/class/properties address schema changed.");
            if (row.Row.Extent.Offset < row.Properties.Extent.EndExclusive && row.Properties.Extent.Offset < row.Row.Extent.EndExclusive)
                throw new InvalidDataException("Native row and properties source extents overlap.");
            HashSet<int> fields = [];
            foreach (NativeMobyDependencyOwnerField field in row.OwnerFields)
            {
                if (field == null || field.PropertyByteOffset < 0 || field.PropertyByteOffset % 4 != 0 ||
                    field.PropertyByteOffset > props.Length - 4 || !fields.Add(field.PropertyByteOffset) ||
                    field.OwnerRoleIndex < 0 || field.OwnerRoleIndex >= rows.Length ||
                    BinaryPrimitives.ReadUInt32LittleEndian(props.AsSpan(field.PropertyByteOffset)) != rows[field.OwnerRoleIndex].SourceInstanceIndex)
                    throw new InvalidDataException("Native private owner-index field lacks its actual source instance preimage.");
            }
        }
        if (shared.Nodes.Count == 0 || shared.Nodes.Any(n => n.Sharing != NativeMobyDependencySharing.Immutable ||
            n.Kind is NativeMobyDependencyKind.Instance or NativeMobyDependencyKind.InstanceProperties))
            throw new InvalidDataException("Shared native closure contains private instance storage.");
        NativeMobyDependencyMutableGlobal[] globals = mutableGlobals.ToArray();
        if (globals.Any(g => g == null || string.IsNullOrWhiteSpace(g.Role) || g.Consumers == null || g.Consumers.Count == 0 ||
            g.ExecutableDomain == null || g.ExecutableDomain.Kind != NativeAddressKind.ExecutableFile || g.ExecutableDomain.Revision != NativeAddressRevision.Original ||
            g.RuntimeAddress < 0x80000000 || g.ByteLength <= 0 || (ulong)g.RuntimeAddress + (ulong)g.ByteLength > 0x80200000))
            throw new InvalidDataException("Mutable global requires bounded native RAM and certified executable/consumer domains.");
        globals = globals.Select(g => g with { Consumers = Array.AsReadOnly(g.Consumers.ToArray()) }).ToArray();
        foreach (var global in globals) foreach (var consumer in global.Consumers) _ = Read(consumer);
        return new(policy, rows, globals, shared);
    }

    internal bool SameBinding(NativeMobyDependencyInstanceTemplate other) => Policy == other.Policy &&
        Rows.Count == other.Rows.Count && Rows.Zip(other.Rows).All(p =>
            p.First.Role == p.Second.Role && p.First.SourceInstanceIndex == p.Second.SourceInstanceIndex &&
            p.First.NativeClass == p.Second.NativeClass && p.First.Row == p.Second.Row && p.First.Properties == p.Second.Properties &&
            p.First.PropertiesPointerBaseOffset == p.Second.PropertiesPointerBaseOffset &&
            p.First.RequiresBytePersistentOwner == p.Second.RequiresBytePersistentOwner && p.First.OwnerFields.SequenceEqual(p.Second.OwnerFields)) &&
        Globals.Count == other.Globals.Count && Globals.Zip(other.Globals).All(p => p.First.Role == p.Second.Role &&
            p.First.ExecutableDomain == p.Second.ExecutableDomain && p.First.RuntimeAddress == p.Second.RuntimeAddress &&
            p.First.ByteLength == p.Second.ByteLength && p.First.Consumers.SequenceEqual(p.Second.Consumers)) &&
        NativeMobyDependencyInstantiationPlan.SameSharedBinding(Shared, other.Shared);

    internal static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}

internal sealed class NativeMobyDependencyAllocatedRow
{
    internal NativeMobyDependencyAllocatedRow(NativeMobyDependencyPrivateSchema source, int index,
        NativeByteExtent row, NativeByteExtent properties, IReadOnlyList<int> familyIndices)
    { Source = source; Index = index; Row = row; Properties = properties; FamilyIndices = Array.AsReadOnly(familyIndices.ToArray()); }
    public NativeMobyDependencyPrivateSchema Source { get; }
    public int Index { get; }
    public string OwnerId => "instance." + Index;
    public NativeByteExtent Row { get; }
    public NativeByteExtent Properties { get; }
    private IReadOnlyList<int> FamilyIndices { get; }
    public NativeMobyDependencyReference PropertiesReference => new("properties", 0, OwnerId + ".props", 0, 4,
        checked((uint)(Source.Properties.Extent.Offset - Source.PropertiesPointerBaseOffset)));
    public byte[] RemapPrivateOwnerIndices(ReadOnlySpan<byte> captured)
    {
        if (captured.Length != Source.Properties.Extent.ByteLength || NativeMobyDependencyInstanceTemplate.Hash(captured) != Source.Properties.ExpectedSha256)
            throw new InvalidDataException("Native private remap requires its complete source preimage.");
        byte[] bytes = captured.ToArray();
        foreach (var field in Source.OwnerFields)
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(field.PropertyByteOffset), checked((uint)FamilyIndices[field.OwnerRoleIndex]));
        return bytes;
    }
}
internal sealed record NativeMobyDependencyAllocatedFamily(string Id, NativeMobyDependencyInstanceTemplate Template,
    IReadOnlyList<NativeMobyDependencyAllocatedRow> Rows);

/// <summary>
/// Immutable append-only allocation ledger. A failed batch cannot reserve bytes, consume owner
/// indices, or cache a partial resource. Repeating an identical request reuses its exact binding;
/// adding a new identity receives new private extents. It does not prove spatial placement or
/// runtime independence. Unproved sequential scheduling is never used to bypass mutable globals.
/// The source factory must additionally prove destination-native users of these mutable globals
/// absent (as Town's exact factory does), or account for them in a separate certified reservation
/// layer before calling this allocator. This ledger cannot infer original actors' global uses.
/// </summary>
internal sealed class NativeMobyDependencyInstantiationPlan
{
    private readonly NativeByteExtent _rows, _properties;
    private readonly int _firstIndex;
    private readonly IReadOnlyList<NativeAddressSpace> _finalDomains;
    private readonly NativeAddressSpace _runtimeDomain;
    private NativeMobyDependencyInstantiationPlan(NativeByteExtent rows, NativeByteExtent properties, int firstIndex,
        IReadOnlyList<NativeAddressSpace> finalDomains, NativeAddressSpace runtimeDomain,
        IReadOnlyList<NativeMobyDependencyAllocatedFamily> families)
    { _rows = rows; _properties = properties; _firstIndex = firstIndex; _finalDomains = finalDomains; _runtimeDomain = runtimeDomain;
      Families = Array.AsReadOnly(families.ToArray()); }
    public IReadOnlyList<NativeMobyDependencyAllocatedFamily> Families { get; }
    public IReadOnlyList<NativeMobyDependencyAllocatedRow> Rows => Array.AsReadOnly(Families.SelectMany(f => f.Rows).ToArray());
    public int FinalRowCount => checked(_firstIndex + Rows.Count);
    public long PropertyBytesAllocated => Rows.Sum(r => r.Properties.ByteLength);
    public int SharedNodeCount => Families.SelectMany(f => f.Template.Shared.Nodes).Select(n => n.Id).Distinct(StringComparer.Ordinal).Count();

    public static NativeMobyDependencyInstantiationPlan Create(NativeByteExtent availableRows, NativeByteExtent availableProperties,
        long rowTableOffset, int firstInstanceIndex, IEnumerable<NativeAddressSpace> finalDomains, NativeAddressSpace runtimeExecutableDomain)
    {
        ArgumentNullException.ThrowIfNull(availableRows); ArgumentNullException.ThrowIfNull(availableProperties);
        ArgumentNullException.ThrowIfNull(finalDomains); ArgumentNullException.ThrowIfNull(runtimeExecutableDomain);
        NativeAddressSpace[] domains = finalDomains.ToArray();
        if (firstInstanceIndex < 0 || rowTableOffset < 0 || rowTableOffset > availableRows.Offset ||
            availableRows.Offset - rowTableOffset != (long)firstInstanceIndex * 0x58 || availableRows.Space.Kind != NativeAddressKind.Scene ||
            availableRows.Space.Revision != NativeAddressRevision.Final || availableProperties.Space != availableRows.Space ||
            availableRows.ByteLength % 0x58 != 0 || availableRows.Offset % 4 != 0 ||
            availableProperties.ByteLength % 4 != 0 || availableProperties.Offset % 4 != 0 || Overlap(availableRows, availableProperties) ||
            domains.Length == 0 || domains.Any(d => d == null || d.Revision != NativeAddressRevision.Final) ||
            domains.Distinct().Count() != domains.Length || !domains.Contains(availableRows.Space) ||
            runtimeExecutableDomain.Kind != NativeAddressKind.ExecutableFile || runtimeExecutableDomain.Revision != NativeAddressRevision.Original)
            throw new InvalidDataException("Native instance arenas require disjoint aligned canonical final Scene/executable address domains.");
        return new(availableRows, availableProperties, firstInstanceIndex, Array.AsReadOnly(domains), runtimeExecutableDomain, []);
    }

    public NativeMobyDependencyInstantiationPlan Allocate(IEnumerable<NativeMobyDependencyInstantiationRequest> requests)
    {
        ArgumentNullException.ThrowIfNull(requests);
        var batch = requests.ToArray();
        if (batch.Any(r => r == null || string.IsNullOrWhiteSpace(r.Id) || r.Template == null) ||
            batch.Select(r => r.Id).Distinct(StringComparer.Ordinal).Count() != batch.Length)
            throw new InvalidDataException("Native instance requests need unique nonempty identities.");
        if (batch.Any(r => r.Activation != NativeMobyDependencyActivation.ConcurrentLevel))
            throw new InvalidDataException("Sequential native-family activation/reset semantics are unproved.");
        List<NativeMobyDependencyAllocatedFamily> families = [.. Families];
        int count = Rows.Count;
        long propertyCursor = checked(_properties.Offset + PropertyBytesAllocated);
        foreach (var request in batch.OrderBy(r => r.Id, StringComparer.Ordinal))
        {
            var existing = families.SingleOrDefault(f => f.Id == request.Id);
            if (existing != null)
            {
                if (!existing.Template.SameBinding(request.Template)) throw new InvalidDataException("Repeated native identity changed its source/payload/address/relocation binding.");
                continue;
            }
            var template = request.Template;
            foreach (var global in template.Globals)
            {
                if (global.ExecutableDomain != _runtimeDomain) throw new InvalidDataException("Mutable native global belongs to another exact executable address domain.");
                foreach (var other in families.SelectMany(f => f.Template.Globals))
                    if ((ulong)global.RuntimeAddress < (ulong)other.RuntimeAddress + (ulong)other.ByteLength &&
                        (ulong)other.RuntimeAddress < (ulong)global.RuntimeAddress + (ulong)global.ByteLength)
                        throw new InvalidDataException($"Mutable native global conflict at 0x{global.RuntimeAddress:X8}; two live families are not independent.");
            }
            foreach (var node in template.Shared.Nodes)
            {
                foreach (var extent in SharedExtents(template.Shared, node.Id))
                {
                    if (!_finalDomains.Contains(extent.Space)) throw new InvalidDataException("Shared native resource has an unbound final address domain.");
                    if (Overlap(extent, _rows) || Overlap(extent, _properties)) throw new InvalidDataException("Shared native resource overlaps the private instance arenas.");
                }
                foreach (var family in families)
                {
                    var prior = family.Template.Shared.Nodes.SingleOrDefault(n => n.Id == node.Id);
                    if (prior != null && (family.Template.Policy != template.Policy || !SameNodeBinding(family.Template.Shared, template.Shared, node.Id)))
                        throw new InvalidDataException("Shared native identity conflicts in payload, source, address domain or relocation policy.");
                    foreach (var other in family.Template.Shared.Nodes.Where(n => n.Id != node.Id))
                        if (SharedExtents(template.Shared, node.Id).Any(a => SharedExtents(family.Template.Shared, other.Id).Any(b => Overlap(a, b))) &&
                            !SameNodeBinding(family.Template.Shared, template.Shared, other.Id))
                            throw new InvalidDataException("Distinct shared native identities have an unproved overlapping resource binding.");
                }
            }
            int first = checked(_firstIndex + count);
            if (first > int.MaxValue - template.Rows.Count) throw new InvalidDataException("Native instance index range overflows.");
            int[] indices = Enumerable.Range(first, template.Rows.Count).ToArray();
            if (template.Rows.Where((r, i) => r.RequiresBytePersistentOwner && indices[i] >= 256).Any())
                throw new InvalidDataException("Native persistent reward owner must fit the actual byte index (<256).");
            long rowBytes = checked((long)(count + template.Rows.Count) * 0x58);
            long propertyBytes = template.Rows.Sum(r => r.Properties.Extent.ByteLength);
            if (rowBytes > _rows.ByteLength || propertyBytes > _properties.EndExclusive - propertyCursor)
                throw new InvalidDataException("Native instance row/properties arena capacity is exhausted.");
            List<NativeMobyDependencyAllocatedRow> allocated = [];
            foreach (var schema in template.Rows)
            {
                var row = new NativeByteExtent(_rows.Space, checked(_rows.Offset + (long)count * 0x58), 0x58);
                var properties = new NativeByteExtent(_properties.Space, propertyCursor, schema.Properties.Extent.ByteLength);
                allocated.Add(new(schema, checked(_firstIndex + count), row, properties, indices));
                count++; propertyCursor = properties.EndExclusive;
            }
            families.Add(new(request.Id, template, Array.AsReadOnly(allocated.ToArray())));
        }
        return new(_rows, _properties, _firstIndex, _finalDomains, _runtimeDomain, families);
    }

    public void RequireSharedBinding(NativeMobyDependencyClosurePlan finalGraph)
    {
        foreach (var family in Families) foreach (var node in family.Template.Shared.Nodes)
            if (!SameNodeBinding(family.Template.Shared, finalGraph, node.Id))
                throw new InvalidDataException("Final graph changed a source-bound shared instantiation resource.");
    }
    private static bool Overlap(NativeByteExtent a, NativeByteExtent b) => a.Space == b.Space && a.Offset < b.EndExclusive && b.Offset < a.EndExclusive;
    private static IEnumerable<NativeByteExtent> SharedExtents(NativeMobyDependencyClosurePlan graph, string id) =>
        graph.Installs.Where(i => i.NodeId == id).Select(i => i.Destination).Concat(graph.Nodes.Single(n => n.Id == id).Preserved.Select(p => p.Final));
    internal static bool SameSharedBinding(NativeMobyDependencyClosurePlan a, NativeMobyDependencyClosurePlan b) =>
        a.Nodes.Count == b.Nodes.Count && a.Nodes.All(n => SameNodeBinding(a, b, n.Id));
    private static bool SameNodeBinding(NativeMobyDependencyClosurePlan a, NativeMobyDependencyClosurePlan b, string id)
    {
        var x = a.Nodes.SingleOrDefault(n => n.Id == id); var y = b.Nodes.SingleOrDefault(n => n.Id == id);
        return x != null && y != null && x.Kind == y.Kind && x.Resolution == y.Resolution && x.Sharing == y.Sharing &&
            x.EvidenceSha256 == y.EvidenceSha256 && x.OwnerInstanceId == y.OwnerInstanceId && x.PayloadSha256 == y.PayloadSha256 &&
            x.Dependencies.SequenceEqual(y.Dependencies) && x.Reads.SequenceEqual(y.Reads) && x.Preserved.SequenceEqual(y.Preserved) &&
            a.Installs.Where(i => i.NodeId == id).SequenceEqual(b.Installs.Where(i => i.NodeId == id)) &&
            a.References.Where(r => r.NodeId == id).SequenceEqual(b.References.Where(r => r.NodeId == id));
    }
}
