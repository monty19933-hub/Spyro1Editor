using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.Core.Scene;

internal enum NativeMobyDependencyKind
{
    Bundle, Instance, InstanceProperties, ActorPackage, Animation, Texture,
    Behavior, NativeRoutine, Effect, Sound, SceneFixup, ObjectIndex,
    RuntimeBuffer, ActorRegistration, RootlessController, SpecialRenderer
}
internal enum NativeMobyDependencyResolution
{
    Aggregate, DestinationResident, GlobalShared, ImportFromSource, Conflict, Unrelocatable
}
internal enum NativeMobyDependencySharing { Immutable, PrivateInstance }

/// <summary>Only an exact source factory may issue these spans; labels and hashes are not write authority.</summary>
internal sealed record NativeMobyDependencyRead(string Id, NativeByteExtent Extent, string ExpectedSha256);
internal sealed record NativeMobyDependencyInstall(string Id, NativeByteExtent Destination);
internal sealed record NativeMobyDependencyPreservation(string ReadId, int ReadOffset, NativeByteExtent Final);
internal sealed record NativeMobyDependencyReference(string Id, int FieldOffset, string TargetNodeId,
    int TargetByteOffset, int ByteWidth, uint? ExpectedBefore = null, uint EncodedBase = 0,
    int AddressUnitBytes = 1);

internal sealed record NativeMobyDependencyNode(string Id, NativeMobyDependencyKind Kind,
    NativeMobyDependencyResolution Resolution, NativeMobyDependencySharing Sharing,
    string EvidenceSha256, IReadOnlyList<string> Dependencies,
    IReadOnlyList<NativeMobyDependencyRead> Reads,
    Func<NativeMobyDependencyBuildContext, NativeMobyDependencyOutput>? Build = null,
    string? OwnerInstanceId = null, string? FailureReason = null,
    IReadOnlyList<NativeMobyDependencyPreservation>? Preserved = null);

internal sealed class NativeMobyDependencyOutput
{
    private readonly byte[] _bytes;
    public NativeMobyDependencyOutput(ReadOnlySpan<byte> bytes,
        IEnumerable<NativeMobyDependencyInstall> installs,
        IEnumerable<NativeMobyDependencyReference> references)
    {
        _bytes = bytes.ToArray();
        Installs = Array.AsReadOnly(installs.ToArray());
        References = Array.AsReadOnly(references.ToArray());
    }
    public IReadOnlyList<NativeMobyDependencyInstall> Installs { get; }
    public IReadOnlyList<NativeMobyDependencyReference> References { get; }
    public byte[] CopyBytes() => _bytes.ToArray();
}

internal sealed class NativeMobyDependencyBuildContext
{
    private readonly IReadOnlyDictionary<string, byte[]> _reads;
    private readonly IReadOnlySet<string> _dependencies;
    private readonly Func<string, byte[]> _payload;
    private readonly Func<string, string, byte[]> _dependencyRead;
    internal NativeMobyDependencyBuildContext(IReadOnlyDictionary<string, byte[]> reads,
        IReadOnlySet<string> dependencies, Func<string, byte[]> payload, Func<string, string, byte[]> dependencyRead)
    { _reads = reads; _dependencies = dependencies; _payload = payload; _dependencyRead = dependencyRead; }
    public byte[] Read(string id) => _reads.TryGetValue(id, out byte[]? bytes)
        ? bytes.ToArray() : throw new InvalidDataException($"Undeclared dependency source read '{id}'.");
    public byte[] DependencyPayload(string id) => _dependencies.Contains(id)
        ? _payload(id) : throw new InvalidDataException($"Undeclared dependency payload '{id}'.");
    public byte[] DependencyRead(string nodeId, string readId) => _dependencies.Contains(nodeId)
        ? _dependencyRead(nodeId, readId) : throw new InvalidDataException($"Undeclared dependency source '{nodeId}'.");
}

internal sealed record NativeMobyDependencySelectedNode(string Id, NativeMobyDependencyKind Kind,
    NativeMobyDependencyResolution Resolution, NativeMobyDependencySharing Sharing,
    string EvidenceSha256, string? OwnerInstanceId, IReadOnlyList<string> Dependencies,
    IReadOnlyList<NativeMobyDependencyRead> Reads,
    IReadOnlyList<NativeMobyDependencyPreservation> Preserved, string? PayloadSha256);
internal sealed record NativeMobyDependencyPlannedInstall(string NodeId, string InstallId,
    NativeByteExtent Destination, string PayloadSha256);
internal sealed record NativeMobyDependencyResolvedReference(string NodeId, string ReferenceId,
    int FieldOffset, string TargetNodeId, NativeByteExtent Target, uint Before, uint After);

internal sealed class NativeMobyDependencyClosurePlan
{
    private readonly IReadOnlyDictionary<string, byte[]> _payloads;
    internal NativeMobyDependencyClosurePlan(IReadOnlyList<NativeMobyDependencySelectedNode> nodes,
        IReadOnlyList<NativeMobyDependencyPlannedInstall> installs,
        IReadOnlyList<NativeMobyDependencyResolvedReference> references,
        IReadOnlyDictionary<string, byte[]> payloads, string fingerprint)
    {
        Nodes = Array.AsReadOnly(nodes.ToArray()); Installs = Array.AsReadOnly(installs.ToArray());
        References = Array.AsReadOnly(references.ToArray()); Fingerprint = fingerprint;
        _payloads = payloads.ToDictionary(p => p.Key, p => p.Value.ToArray(), StringComparer.Ordinal);
    }
    public IReadOnlyList<NativeMobyDependencySelectedNode> Nodes { get; }
    public IReadOnlyList<NativeMobyDependencyPlannedInstall> Installs { get; }
    public IReadOnlyList<NativeMobyDependencyResolvedReference> References { get; }
    public string Fingerprint { get; }
    public byte[] CopyPayload(string nodeId) => _payloads.TryGetValue(nodeId, out byte[]? value)
        ? value.ToArray() : throw new InvalidDataException($"Dependency '{nodeId}' has no materialized payload.");
}

/// <summary>
/// Computes a complete reachable graph before reading, extracts and verifies
/// every selected native preimage, then builds only those assets. Resident and
/// global nodes still require checked source evidence. Cyclic declarations are
/// valid; a cycle demanding unfinished payload bytes fails as unresolved
/// materialization. No byte is written outside this detached plan. Callers must
/// derive schemas, ownership, source spans and destinations from actual native
/// factories and publish through the existing verified transaction.
/// </summary>
internal static class NativeMobyDependencyClosureCompiler
{
    public static NativeMobyDependencyClosurePlan Compile(IEnumerable<NativeMobyDependencyNode> catalog,
        IEnumerable<string> roots, Func<NativeByteExtent, byte[]> readSource)
    {
        ArgumentNullException.ThrowIfNull(catalog); ArgumentNullException.ThrowIfNull(roots);
        ArgumentNullException.ThrowIfNull(readSource);
        Dictionary<string, NativeMobyDependencyNode> nodes = new(StringComparer.Ordinal);
        foreach (NativeMobyDependencyNode node in catalog)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.Id) || !nodes.TryAdd(node.Id, node))
                throw new InvalidDataException("Dependency catalog contains an empty or duplicate node identity.");
            if (node.Dependencies == null || node.Reads == null)
                throw new InvalidDataException($"Dependency '{node.Id}' has no explicit edge/read lists.");
            nodes[node.Id] = node with
            {
                Dependencies = Array.AsReadOnly(node.Dependencies.ToArray()),
                Reads = Array.AsReadOnly(node.Reads.ToArray()),
                Preserved = Array.AsReadOnly(node.Preserved?.ToArray() ?? [])
            };
        }
        string[] requested = roots.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (requested.Length == 0) throw new InvalidDataException("A dependency closure requires at least one root.");
        SortedDictionary<string, NativeMobyDependencyNode> selected = new(StringComparer.Ordinal);
        void Visit(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || !nodes.TryGetValue(id, out NativeMobyDependencyNode? node))
                throw new InvalidDataException($"Missing dependency node '{id}'.");
            if (selected.ContainsKey(id)) return;
            Validate(node);
            selected.Add(id, node); // A checked strongly connected group is allowed.
            foreach (string dependency in node.Dependencies.Order(StringComparer.Ordinal)) Visit(dependency);
        }
        foreach (string root in requested) Visit(root);
        foreach (NativeMobyDependencyNode privateNode in selected.Values.Where(n => n.Sharing == NativeMobyDependencySharing.PrivateInstance))
            if (!selected.TryGetValue(privateNode.OwnerInstanceId!, out NativeMobyDependencyNode? owner) || owner.Kind != NativeMobyDependencyKind.Instance)
                throw new InvalidDataException($"Private dependency '{privateNode.Id}' has no selected native instance owner.");
        HashSet<string> privatelyOwned = new(StringComparer.Ordinal);
        foreach (NativeMobyDependencyNode owner in selected.Values.Where(n => n.Kind == NativeMobyDependencyKind.Instance))
        {
            HashSet<string> visited = new(StringComparer.Ordinal);
            void VisitOwned(string id)
            {
                if (!visited.Add(id)) return;
                NativeMobyDependencyNode node = selected[id];
                if (node.Kind == NativeMobyDependencyKind.Instance && node.Id != owner.Id) return;
                if (node.Sharing == NativeMobyDependencySharing.PrivateInstance)
                {
                    if (node.OwnerInstanceId != owner.Id)
                        throw new InvalidDataException($"Private dependency '{node.Id}' owned by '{node.OwnerInstanceId}' is consumed by another instance '{owner.Id}'.");
                    privatelyOwned.Add(id);
                }
                foreach (string dependency in node.Dependencies) VisitOwned(dependency);
            }
            VisitOwned(owner.Id);
        }
        foreach (NativeMobyDependencyNode node in selected.Values.Where(n => n.Sharing == NativeMobyDependencySharing.PrivateInstance))
            if (!privatelyOwned.Contains(node.Id)) throw new InvalidDataException($"Private dependency '{node.Id}' is not reachable from its declared instance owner.");

        Dictionary<string, IReadOnlyDictionary<string, byte[]>> reads = new(StringComparer.Ordinal);
        foreach ((string id, NativeMobyDependencyNode node) in selected)
        {
            Dictionary<string, byte[]> captured = new(StringComparer.Ordinal);
            foreach (NativeMobyDependencyRead read in node.Reads.OrderBy(r => r.Id, StringComparer.Ordinal))
            {
                byte[] bytes = readSource(read.Extent)?.ToArray() ??
                    throw new InvalidDataException($"Dependency '{id}' source reader returned no bytes.");
                if (bytes.LongLength != read.Extent.ByteLength || Hash(bytes) != read.ExpectedSha256)
                    throw new InvalidDataException($"Dependency '{id}' read '{read.Id}' failed its complete native preimage hash/length.");
                captured.Add(read.Id, bytes);
            }
            reads.Add(id, captured);
        }

        // Residency is a final-byte promise, not just a successful original read.
        // Equal overlapping promises are legitimate shared native resources.
        var preservedRanges = selected.Values.SelectMany(n => n.Preserved!.Select(p => (Node: n, Range: p))).ToArray();
        for (int i = 0; i < preservedRanges.Length; i++)
            for (int j = i + 1; j < preservedRanges.Length; j++)
            {
                var a = preservedRanges[i]; var b = preservedRanges[j];
                if (a.Range.Final.Space != b.Range.Final.Space) continue;
                long start = Math.Max(a.Range.Final.Offset, b.Range.Final.Offset);
                long end = Math.Min(a.Range.Final.EndExclusive, b.Range.Final.EndExclusive);
                if (start >= end) continue;
                ReadOnlySpan<byte> first = reads[a.Node.Id][a.Range.ReadId].AsSpan(
                    checked(a.Range.ReadOffset + (int)(start - a.Range.Final.Offset)), checked((int)(end - start)));
                ReadOnlySpan<byte> second = reads[b.Node.Id][b.Range.ReadId].AsSpan(
                    checked(b.Range.ReadOffset + (int)(start - b.Range.Final.Offset)), checked((int)(end - start)));
                if (!first.SequenceEqual(second))
                    throw new InvalidDataException($"Conflicting preserved resident bytes for '{a.Node.Id}' and '{b.Node.Id}'.");
            }

        Dictionary<string, NativeMobyDependencyOutput> outputs = new(StringComparer.Ordinal);
        Dictionary<string, byte[]> payloads = new(StringComparer.Ordinal);
        HashSet<string> building = new(StringComparer.Ordinal);
        List<NativeMobyDependencyResolvedReference> references = [];
        NativeMobyDependencyOutput Prepare(string id)
        {
            if (outputs.TryGetValue(id, out NativeMobyDependencyOutput? existing)) return existing;
            NativeMobyDependencyNode node = selected[id];
            if (node.Build == null) throw new InvalidDataException($"Dependency '{id}' has no imported payload to consume.");
            if (!building.Add(id)) throw new InvalidDataException($"Unresolved recursive materialization at dependency '{id}'.");
            NativeMobyDependencyBuildContext context = new(reads[id], node.Dependencies.ToHashSet(StringComparer.Ordinal), Materialize,
                (dependencyId, readId) => reads[dependencyId].TryGetValue(readId, out byte[]? value) ? value.ToArray() :
                    throw new InvalidDataException($"Undeclared dependency source read '{dependencyId}:{readId}'."));
            NativeMobyDependencyOutput built = node.Build(context) ??
                throw new InvalidDataException($"Dependency '{id}' materializer returned no output.");
            byte[] bytes = built.CopyBytes();
            if (bytes.Length == 0) throw new InvalidDataException($"Imported dependency '{id}' produced an empty payload.");
            foreach (NativeMobyDependencyInstall install in built.Installs)
                if (install == null || string.IsNullOrWhiteSpace(install.Id) || install.Destination == null ||
                    install.Destination.Space.Revision != NativeAddressRevision.Final || install.Destination.ByteLength != bytes.Length)
                    throw new InvalidDataException($"Dependency '{id}' install has invalid identity, revision or byte length.");
            if (built.Installs.Select(i => i.Id).Distinct(StringComparer.Ordinal).Count() != built.Installs.Count)
                throw new InvalidDataException($"Dependency '{id}' has duplicate install identities.");
            if (node.Sharing == NativeMobyDependencySharing.PrivateInstance && built.Installs.Count != 1)
                throw new InvalidDataException($"Private dependency '{id}' requires exactly one installed owner extent.");
            NativeMobyDependencyOutput detached = new(bytes, built.Installs, built.References);
            outputs.Add(id, detached); building.Remove(id);
            return detached;
        }
        byte[] Materialize(string id)
        {
            if (payloads.TryGetValue(id, out byte[]? existing)) return existing.ToArray();
            NativeMobyDependencyOutput built = Prepare(id);
            NativeMobyDependencyNode node = selected[id];
            byte[] bytes = built.CopyBytes();
            HashSet<int> fields = []; HashSet<string> referenceIds = new(StringComparer.Ordinal);
            if (built.References.Any(r=>r==null)) throw new InvalidDataException($"Dependency '{id}' contains a null reference declaration.");
            foreach (NativeMobyDependencyReference reference in built.References.OrderBy(r => r.FieldOffset))
            {
                if (reference == null || string.IsNullOrWhiteSpace(reference.Id) || !referenceIds.Add(reference.Id) ||
                    reference.ByteWidth != 4 || (reference.FieldOffset & 3) != 0 || reference.FieldOffset < 0 ||
                    reference.FieldOffset > bytes.Length - 4 || !fields.Add(reference.FieldOffset) ||
                    !node.Dependencies.Contains(reference.TargetNodeId, StringComparer.Ordinal) ||
                    built.Installs.Count != 1 || reference.ExpectedBefore == null || reference.AddressUnitBytes <= 0 ||
                    (reference.AddressUnitBytes & (reference.AddressUnitBytes - 1)) != 0)
                    throw new InvalidDataException($"Dependency '{id}' has an undeclared or unsupported reference target/field/encoding.");
                NativeMobyDependencyNode targetNode = selected[reference.TargetNodeId];
                NativeByteExtent targetOwner;
                if (targetNode.Build == null)
                {
                    if (targetNode.Preserved!.Count != 1)
                        throw new InvalidDataException($"Dependency '{id}' resident reference target needs one exact preserved extent.");
                    targetOwner = targetNode.Preserved[0].Final;
                }
                else
                {
                    NativeMobyDependencyOutput targetOutput = Prepare(reference.TargetNodeId);
                    if (targetOutput.Installs.Count != 1)
                        throw new InvalidDataException($"Dependency '{id}' reference target needs one exact installed extent.");
                    targetOwner = targetOutput.Installs[0].Destination;
                }
                NativeByteExtent fieldOwner = built.Installs[0].Destination;
                if (fieldOwner.Space != targetOwner.Space)
                    throw new InvalidDataException($"Dependency '{id}' reference crosses an unbound address space.");
                if (reference.TargetByteOffset < 0 || reference.TargetByteOffset >= targetOwner.ByteLength)
                    throw new InvalidDataException($"Dependency '{id}' reference points outside its declared target.");
                long address = checked(targetOwner.Offset + reference.TargetByteOffset);
                if (address % reference.AddressUnitBytes != 0 || address / reference.AddressUnitBytes + reference.EncodedBase > uint.MaxValue)
                    throw new InvalidDataException($"Dependency '{id}' reference address cannot use its declared encoding.");
                uint before = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(reference.FieldOffset, 4));
                if (before != reference.ExpectedBefore.Value)
                    throw new InvalidDataException($"Dependency '{id}' reference does not match its source preimage.");
                uint after = checked((uint)(address / reference.AddressUnitBytes + reference.EncodedBase));
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(reference.FieldOffset, 4), after);
                references.Add(new(id, reference.Id, reference.FieldOffset, reference.TargetNodeId,
                    new NativeByteExtent(targetOwner.Space, address, 1), before, after));
            }
            payloads.Add(id, bytes);
            return bytes.ToArray();
        }
        foreach (NativeMobyDependencyNode node in selected.Values.Where(n => n.Build != null)) Materialize(node.Id);

        List<(string NodeId, NativeMobyDependencyInstall Install)> installations = outputs
            .SelectMany(p => p.Value.Installs.Select(i => (p.Key, i))).ToList();
        foreach ((string nodeId, NativeMobyDependencyInstall install) in installations)
            foreach (NativeMobyDependencyNode resident in selected.Values)
                foreach (NativeMobyDependencyPreservation preserved in resident.Preserved!)
                    if (install.Destination.Space == preserved.Final.Space &&
                        install.Destination.Offset < preserved.Final.EndExclusive && preserved.Final.Offset < install.Destination.EndExclusive)
                        throw new InvalidDataException($"Dependency install '{nodeId}' overwrites preserved resident dependency '{resident.Id}'.");
        for (int i = 0; i < installations.Count; i++)
            for (int j = i + 1; j < installations.Count; j++)
            {
                NativeByteExtent a = installations[i].Install.Destination, b = installations[j].Install.Destination;
                if (a.Space == b.Space && a.Offset < b.EndExclusive && b.Offset < a.EndExclusive)
                    throw new InvalidDataException($"Dependency installs '{installations[i].NodeId}' and '{installations[j].NodeId}' overlap; sharing must use one graph node.");
            }

        references = references.OrderBy(r => r.NodeId, StringComparer.Ordinal).ThenBy(r => r.FieldOffset).ToList();
        NativeMobyDependencySelectedNode[] resultNodes = selected.Values.Select(n => new NativeMobyDependencySelectedNode(
            n.Id, n.Kind, n.Resolution, n.Sharing, n.EvidenceSha256, n.OwnerInstanceId,
            Array.AsReadOnly(n.Dependencies.Order(StringComparer.Ordinal).ToArray()),
            Array.AsReadOnly(n.Reads.OrderBy(r=>r.Id,StringComparer.Ordinal).ToArray()),
            Array.AsReadOnly(OrderPreserved(n.Preserved!).ToArray()),
            payloads.TryGetValue(n.Id, out byte[]? bytes) ? Hash(bytes) : null)).ToArray();
        NativeMobyDependencyPlannedInstall[] resultInstalls = installations.OrderBy(i => i.NodeId, StringComparer.Ordinal)
            .ThenBy(i => i.Install.Id, StringComparer.Ordinal).Select(i => new NativeMobyDependencyPlannedInstall(
                i.NodeId, i.Install.Id, i.Install.Destination, Hash(payloads[i.NodeId]))).ToArray();
        using MemoryStream fingerprint = new();
        using (BinaryWriter writer = new(fingerprint, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write("native-moby-dependency-closure-v1");
            foreach (NativeMobyDependencySelectedNode node in resultNodes)
            {
                writer.Write(node.Id); writer.Write((int)node.Kind); writer.Write((int)node.Resolution);
                writer.Write((int)node.Sharing); writer.Write(node.EvidenceSha256); writer.Write(node.OwnerInstanceId ?? "");
                writer.Write(node.Dependencies.Count); foreach (string dependency in node.Dependencies) writer.Write(dependency);
                writer.Write(node.Reads.Count);
                foreach (NativeMobyDependencyRead read in node.Reads.OrderBy(r => r.Id, StringComparer.Ordinal))
                { writer.Write(read.Id); WriteExtent(writer, read.Extent); writer.Write(read.ExpectedSha256); }
                writer.Write(node.Preserved.Count);
                foreach (NativeMobyDependencyPreservation preserved in node.Preserved)
                { writer.Write(preserved.ReadId); writer.Write(preserved.ReadOffset); WriteExtent(writer,preserved.Final); }
                writer.Write(node.PayloadSha256 ?? "");
            }
            foreach (NativeMobyDependencyPlannedInstall install in resultInstalls)
            { writer.Write(install.NodeId); writer.Write(install.InstallId); WriteExtent(writer, install.Destination); writer.Write(install.PayloadSha256); }
            foreach (NativeMobyDependencyResolvedReference reference in references)
            { writer.Write(reference.NodeId); writer.Write(reference.ReferenceId); writer.Write(reference.FieldOffset); writer.Write(reference.TargetNodeId); WriteExtent(writer, reference.Target); writer.Write(reference.Before); writer.Write(reference.After); }
        }
        return new(resultNodes, resultInstalls, references, payloads, Hash(fingerprint.ToArray()));
    }

    private static void Validate(NativeMobyDependencyNode node)
    {
        if (!Enum.IsDefined(node.Kind) || !Enum.IsDefined(node.Resolution) || !Enum.IsDefined(node.Sharing))
            throw new InvalidDataException($"Dependency '{node.Id}' has an unknown kind, resolution or sharing policy.");
        if (!Digest(node.EvidenceSha256)) throw new InvalidDataException($"Dependency '{node.Id}' lacks a complete native evidence digest.");
        if (node.Resolution is NativeMobyDependencyResolution.Conflict or NativeMobyDependencyResolution.Unrelocatable)
            throw new InvalidDataException($"Dependency '{node.Id}' is {node.Resolution}: {node.FailureReason ?? "native closure is unavailable"}.");
        if (node.Dependencies.Any(string.IsNullOrWhiteSpace) || node.Dependencies.Distinct(StringComparer.Ordinal).Count() != node.Dependencies.Count)
            throw new InvalidDataException($"Dependency '{node.Id}' has empty or duplicate edge targets.");
        if (node.Reads.Any(r => r == null || string.IsNullOrWhiteSpace(r.Id) || r.Extent == null ||
            r.Extent.Space.Revision != NativeAddressRevision.Original || !Digest(r.ExpectedSha256)) ||
            node.Reads.Select(r => r.Id).Distinct(StringComparer.Ordinal).Count() != node.Reads.Count)
            throw new InvalidDataException($"Dependency '{node.Id}' has invalid or duplicate native read declarations.");
        bool importing = node.Resolution == NativeMobyDependencyResolution.ImportFromSource;
        if (importing != (node.Build != null))
            throw new InvalidDataException($"Dependency '{node.Id}' has a missing import builder or a resident/global/aggregate builder.");
        if (node.Resolution is NativeMobyDependencyResolution.DestinationResident or NativeMobyDependencyResolution.GlobalShared && node.Reads.Count == 0)
            throw new InvalidDataException($"Resident/global dependency '{node.Id}' requires actual native byte checks.");
        if (node.Resolution is NativeMobyDependencyResolution.DestinationResident or NativeMobyDependencyResolution.GlobalShared && node.Preserved!.Count == 0)
            throw new InvalidDataException($"Resident/global dependency '{node.Id}' requires preserved final address bindings.");
        foreach (NativeMobyDependencyPreservation preserved in node.Preserved!)
        {
            NativeMobyDependencyRead? read = preserved == null ? null : node.Reads.SingleOrDefault(r => r.Id == preserved.ReadId);
            if (read == null || preserved!.Final == null || preserved.Final.Space.Revision != NativeAddressRevision.Final ||
                preserved.ReadOffset < 0 || preserved.ReadOffset > read.Extent.ByteLength - preserved.Final.ByteLength ||
                preserved.Final.Space.Kind != read.Extent.Space.Kind)
                throw new InvalidDataException($"Dependency '{node.Id}' has an invalid preserved source/final address binding.");
        }
        if (node.Sharing == NativeMobyDependencySharing.PrivateInstance &&
            (string.IsNullOrWhiteSpace(node.OwnerInstanceId) || !importing))
            throw new InvalidDataException($"Private dependency '{node.Id}' requires an explicit imported instance owner.");
        if (node.Sharing == NativeMobyDependencySharing.Immutable && node.OwnerInstanceId != null)
            throw new InvalidDataException($"Immutable dependency '{node.Id}' cannot claim a private instance owner.");
    }
    private static bool Digest(string value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static IOrderedEnumerable<NativeMobyDependencyPreservation> OrderPreserved(IEnumerable<NativeMobyDependencyPreservation> values) =>
        values.OrderBy(p => p.ReadId, StringComparer.Ordinal).ThenBy(p => p.ReadOffset)
            .ThenBy(p => p.Final.Space.Kind).ThenBy(p => p.Final.Space.OwnerIdentity, StringComparer.Ordinal)
            .ThenBy(p => p.Final.Space.Revision).ThenBy(p => p.Final.Space.ByteLength)
            .ThenBy(p => p.Final.Offset).ThenBy(p => p.Final.ByteLength);
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void WriteExtent(BinaryWriter writer, NativeByteExtent extent)
    {
        writer.Write((int)extent.Space.Kind); writer.Write(extent.Space.OwnerIdentity); writer.Write((int)extent.Space.Revision);
        writer.Write(extent.Space.ByteLength); writer.Write(extent.Offset); writer.Write(extent.ByteLength);
    }
}
