using System.Text;
using System.Text.Json;

namespace Spyro.Editor.Core.Scene;

internal sealed record NativeMobyOwnerSourceBinding(string SourceImageSha256, int LevelId, int SaveSlot,
    int OriginalRowCount, string OriginalRowsSha256);
internal sealed record NativeMobyOwnerRoleIdentity(string RoleId, int DonorLevelId, int DonorTrueIndex,
    ushort NativeClass, string SourceVariantId);

/// <summary>Source-row selection intent, never a caller-issued native certificate.</summary>
public sealed record NativeMobyPersistentOwnerRoleSelection(string RoleId, int DonorLevelId, int DonorTrueIndex);
public sealed record NativeMobyPersistentOwnerRequest(string Id, NativeMobyPersistentOwnerFamily Family);

/// <summary>
/// Immutable source identity grouping only. This does not prove dependency-family
/// completeness, private owner relationships, simultaneous activation or behavior.
/// </summary>
public sealed class NativeMobyPersistentOwnerFamily
{
    internal string PolicyId { get; }
    internal IReadOnlyList<NativeMobyOwnerRoleIdentity> Roles { get; }
    internal NativeMobyPersistentOwnerSource? Source { get; }

    internal NativeMobyPersistentOwnerFamily(string policyId, IEnumerable<NativeMobyOwnerRoleIdentity> roles,
        NativeMobyPersistentOwnerSource? source = null)
    {
        NativeMobyPersistentOwnerLayout.RequireId(policyId);
        var snapshot = NativeMobyPersistentOwnerLayout.Snapshot(roles, "family roles");
        if (snapshot.Length == 0 || snapshot.Any(r => r == null) ||
            snapshot.Select(r => r.RoleId).Distinct(StringComparer.Ordinal).Count() != snapshot.Length)
            throw new InvalidDataException("Persistent families need nonempty unique stable role IDs.");
        foreach (var role in snapshot)
        {
            NativeMobyPersistentOwnerLayout.RequireId(role.RoleId);
            NativeMobyPersistentOwnerLayout.RequireId(role.SourceVariantId, 512);
            NativeMobyPersistentOwnerLayout.RequireLevel(role.DonorLevelId);
            if (role.DonorTrueIndex is < 0 or >= 256)
                throw new InvalidDataException("Source role identity must use an ordinary native row index0..255.");
        }
        PolicyId = policyId; Roles = Array.AsReadOnly(snapshot.OrderBy(r => r.RoleId, StringComparer.Ordinal).ToArray()); Source = source;
    }

    internal bool SameIdentity(NativeMobyPersistentOwnerFamily other) =>
        PolicyId == other.PolicyId && Roles.SequenceEqual(other.Roles);
}

/// <summary>A reserved native ordinal. Retired entries are not native tombstones.</summary>
public sealed class NativeMobyPersistentOwnerEntry
{
    public string FamilyId { get; }
    public string RoleId => Role.RoleId;
    public string SourceVariantId => Role.SourceVariantId;
    public ushort NativeClass => Role.NativeClass;
    public int NativeIndex { get; }
    public bool Retired { get; }
    internal string PolicyId { get; }
    internal NativeMobyOwnerRoleIdentity Role { get; }
    internal NativeMobyPersistentOwnerEntry(string familyId, string policyId, NativeMobyOwnerRoleIdentity role, int index, bool retired)
    { FamilyId = familyId; PolicyId = policyId; Role = role; NativeIndex = index; Retired = retired; }
    internal NativeMobyPersistentOwnerEntry WithRetired(bool retired) => new(FamilyId, PolicyId, Role, NativeIndex, retired);
}

/// <summary>
/// Structural fixture kernel. Only the overload requiring a private source-issued
/// object establishes SourceCertified; fixture metadata/hash strings never do.
/// </summary>
internal sealed class NativeMobyPersistentOwnerLayout
{
    internal const int Capacity = 256;
    internal const int MaximumDocumentBytes = 1024 * 1024;
    internal NativeMobyOwnerSourceBinding Binding { get; }
    internal NativeMobyPersistentOwnerSource? Source { get; }
    private readonly IReadOnlyList<NativeMobyPersistentOwnerFamily> _families;

    internal NativeMobyPersistentOwnerLayout(NativeMobyOwnerSourceBinding binding,
        IEnumerable<NativeMobyPersistentOwnerFamily> families)
    {
        ArgumentNullException.ThrowIfNull(binding);
        RequireLevel(binding.LevelId);
        if (binding.SaveSlot != (binding.LevelId / 10 - 1) * 6 + binding.LevelId % 10 ||
            binding.OriginalRowCount is < 0 or > Capacity || !Hash(binding.SourceImageSha256) || !Hash(binding.OriginalRowsSha256))
            throw new InvalidDataException("Persistent owners require exact native level/save identity and original placed high-water<=256.");
        Binding = binding;
        var snapshot = Snapshot(families, "available family identities");
        if (snapshot.Any(f => f == null)) throw new InvalidDataException("Missing available family identity.");
        _families = Array.AsReadOnly(snapshot);
    }

    internal NativeMobyPersistentOwnerLayout(NativeMobyPersistentOwnerSource source) : this(source.Binding, []) => Source = source;
    internal NativeMobyPersistentOwnerLedger Empty() => new(this, [], []);

    internal NativeMobyPersistentOwnerFamily RequireFamily(NativeMobyPersistentOwnerFamily family)
    {
        if (family == null) throw new InvalidDataException("Missing source-bound family identity.");
        if (Source != null)
        {
            if (family.Source == null || family.Source.Binding != Binding)
                throw new InvalidDataException("A family must be issued for the exact captured native level/source identity.");
            return Source.Rebind(family.PolicyId, family.Roles);
        }
        return _families.FirstOrDefault(f => f.SameIdentity(family)) ??
            throw new InvalidDataException("Family role/source identity is not bound to this fixture layout.");
    }

    internal NativeMobyPersistentOwnerFamily RestoreFamily(string policyId, IReadOnlyList<NativeMobyOwnerRoleIdentity> roles)
    {
        if (Source != null) return Source.Rebind(policyId, roles);
        return RequireFamily(new(policyId, roles));
    }

    internal NativeMobyPersistentOwnerLedger Restore(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length is < 2 or > MaximumDocumentBytes) throw new InvalidDataException("Persistent owner JSON byte budget exceeded or document missing.");
        try
        {
            using JsonDocument document = JsonDocument.Parse(bytes.ToArray(), new JsonDocumentOptions { MaxDepth = 16 });
            var root = document.RootElement;
            Fields(root, "schemaVersion", "sourceImageSha256", "levelId", "saveSlot", "originalRowCount", "originalRowsSha256", "highWater", "entries");
            var binding = new NativeMobyOwnerSourceBinding(Text(root, "sourceImageSha256", 64), Number(root, "levelId"),
                Number(root, "saveSlot"), Number(root, "originalRowCount"), Text(root, "originalRowsSha256", 64));
            if (Number(root, "schemaVersion") != 1 || binding != Binding)
                throw new InvalidDataException("Persistent owner document is not the exact version/source/level/original-row binding.");
            JsonElement array = root.GetProperty("entries");
            if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() > Capacity)
                throw new InvalidDataException("Persistent entry array exceeds the native256-bit capacity.");
            List<NativeMobyPersistentOwnerEntry> entries = [];
            foreach (JsonElement item in array.EnumerateArray())
            {
                Fields(item, "familyId", "roleId", "policyId", "donorLevelId", "donorTrueIndex", "nativeClass", "sourceVariantId", "nativeIndex", "retired");
                string family = Text(item, "familyId"), policy = Text(item, "policyId"), role = Text(item, "roleId");
                RequireId(family); RequireId(policy); RequireId(role);
                int nativeClass = Number(item, "nativeClass");
                if (nativeClass is < 0 or > ushort.MaxValue) throw new InvalidDataException("Native class is not a halfword.");
                var identity = new NativeMobyOwnerRoleIdentity(role, Number(item, "donorLevelId"), Number(item, "donorTrueIndex"),
                    (ushort)nativeClass, Text(item, "sourceVariantId", 512));
                int index = Number(item, "nativeIndex");
                if (index != Binding.OriginalRowCount + entries.Count || index >= Capacity)
                    throw new InvalidDataException("Persistent entries must retain contiguous unique source-suffix ordinals below256.");
                JsonElement retired = item.GetProperty("retired");
                if (retired.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                    throw new InvalidDataException("Retirement status must be explicit boolean data.");
                entries.Add(new(family, policy, identity, index, retired.GetBoolean()));
            }
            if (Number(root, "highWater") != Binding.OriginalRowCount + entries.Count)
                throw new InvalidDataException("Persistent high-water cannot roll back, alias or invent reservation gaps.");
            List<KeyValuePair<string, NativeMobyPersistentOwnerFamily>> families = [];
            int cursor = 0;
            foreach (var group in entries.GroupBy(e => e.FamilyId, StringComparer.Ordinal))
            {
                var rows = group.ToArray();
                if (rows.Any(r => r.PolicyId != rows[0].PolicyId || r.Retired != rows[0].Retired) ||
                    !rows.Select(r => r.NativeIndex).SequenceEqual(Enumerable.Range(Binding.OriginalRowCount + cursor, rows.Length)) ||
                    !rows.Select(r => r.RoleId).SequenceEqual(rows.Select(r => r.RoleId).OrderBy(r => r, StringComparer.Ordinal)))
                    throw new InvalidDataException("Persistent families must be atomic contiguous canonical role reservations.");
                families.Add(new(group.Key, RestoreFamily(rows[0].PolicyId, rows.Select(r => r.Role).ToArray())));
                cursor += rows.Length;
            }
            return new(this, entries, families);
        }
        catch (JsonException e) { throw new InvalidDataException("Malformed/beyond-depth persistent owner JSON.", e); }
    }

    internal static T[] Snapshot<T>(IEnumerable<T> values, string label)
    {
        if (values == null) throw new InvalidDataException("Missing " + label + ".");
        List<T> result = [];
        using var enumerator = values.GetEnumerator();
        while (enumerator.MoveNext())
        {
            if (result.Count == Capacity) throw new InvalidDataException("Native256 owner budget exceeded for " + label + ".");
            result.Add(enumerator.Current);
        }
        return result.ToArray();
    }
    internal static void RequireLevel(int level)
    {
        if (level < 10 || level > 64 || level % 10 > 5)
            throw new InvalidDataException("Persistent owner requires an existing retail native level, not an aliased decimal ID or independent ID65.");
    }
    internal static void RequireId(string value, int maximum = 128)
    {
        if (value == null || value.Length < 1 || value.Length > maximum ||
            value.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_' or '.' or ':')))
            throw new InvalidDataException("Persistent identities must be bounded stable ASCII IDs.");
    }
    private static bool Hash(string value) => value != null && value.Length == 64 && value.All(c => char.IsAsciiDigit(c) || c is >= 'a' and <= 'f');
    private static void Fields(JsonElement item, params string[] names)
    {
        if (item.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Persistent JSON owner is not an object.");
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (var property in item.EnumerateObject())
            if (!names.Contains(property.Name, StringComparer.Ordinal) || !seen.Add(property.Name))
                throw new InvalidDataException("Duplicate/unknown persistent JSON property is not authority.");
        if (seen.Count != names.Length) throw new InvalidDataException("Persistent JSON fields are incomplete.");
    }
    private static string Text(JsonElement item, string name, int maximum = 128)
    {
        JsonElement value = item.GetProperty(name);
        if (value.ValueKind != JsonValueKind.String || value.GetString() is not string text || text.Length is < 1 || text.Length > maximum)
            throw new InvalidDataException("Missing or over-budget persistent JSON text: " + name);
        return text;
    }
    private static int Number(JsonElement item, string name)
    {
        JsonElement value = item.GetProperty(name);
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int number))
            throw new InvalidDataException("Persistent JSON integer is not bounded: " + name);
        return number;
    }
}

/// <summary>
/// Append-only logical identity bookkeeping, not authenticated project history.
/// Source restore cannot establish which older JSON version accompanied a prior
/// native save. The App/project transaction and all actual scene producers remain
/// separate integration work. No operation emits native bytes or touches saves.
/// </summary>
public sealed class NativeMobyPersistentOwnerLedger
{
    private readonly NativeMobyPersistentOwnerLayout _layout;
    private readonly IReadOnlyDictionary<string, NativeMobyPersistentOwnerFamily> _families;
    public IReadOnlyList<NativeMobyPersistentOwnerEntry> Entries { get; }
    public int HighWater => _layout.Binding.OriginalRowCount + Entries.Count;
    public bool SourceCertified => _layout.Source != null;
    public bool NativeEmissionApproved => false;
    public bool RuntimeVerified => false;
    public bool CompleteDependencyFamilyProven => false;
    public bool PrivateOwnerRelationshipsProven => false;
    public bool ProjectTransactionIntegrated => false;

    internal NativeMobyPersistentOwnerLedger(NativeMobyPersistentOwnerLayout layout, IEnumerable<NativeMobyPersistentOwnerEntry> entries,
        IEnumerable<KeyValuePair<string, NativeMobyPersistentOwnerFamily>> families)
    {
        _layout = layout;
        Entries = Array.AsReadOnly(entries.ToArray());
        _families = new System.Collections.ObjectModel.ReadOnlyDictionary<string, NativeMobyPersistentOwnerFamily>(
            families.ToDictionary(f => f.Key, f => f.Value, StringComparer.Ordinal));
    }

    public NativeMobyPersistentOwnerLedger Append(IEnumerable<NativeMobyPersistentOwnerRequest> requests)
    {
        var batch = RequireRequests(requests);
        List<NativeMobyPersistentOwnerEntry> entries = [.. Entries];
        Dictionary<string, NativeMobyPersistentOwnerFamily> families = new(_families, StringComparer.Ordinal);
        foreach (var request in batch.OrderBy(r => r.Id, StringComparer.Ordinal))
        {
            var family = _layout.RequireFamily(request.Family);
            if (families.TryGetValue(request.Id, out var old))
            {
                if (!old.SameIdentity(family) || entries.Any(e => e.FamilyId == request.Id && e.Retired))
                    throw new InvalidDataException("Persistent role/source changed or retired identity needs explicit same-identity reactivation.");
                continue;
            }
            if (family.Roles.Count > NativeMobyPersistentOwnerLayout.Capacity - _layout.Binding.OriginalRowCount - entries.Count)
                throw new InvalidDataException("Complete native placed high-water must remain<=256, including nonreward/retired roles.");
            foreach (var role in family.Roles)
                entries.Add(new(request.Id, family.PolicyId, role, _layout.Binding.OriginalRowCount + entries.Count, false));
            families.Add(request.Id, family);
        }
        return new(_layout, entries, families);
    }

    public NativeMobyPersistentOwnerLedger Retire(IEnumerable<string> familyIds)
    {
        var batch = NativeMobyPersistentOwnerLayout.Snapshot(familyIds, "retirement requests");
        HashSet<string> selected = new(StringComparer.Ordinal);
        foreach (var id in batch)
        {
            NativeMobyPersistentOwnerLayout.RequireId(id);
            if (!selected.Add(id) || !_families.ContainsKey(id))
                throw new InvalidDataException("Retirement requires unique already reserved family identities.");
        }
        return new(_layout, Entries.Select(e => selected.Contains(e.FamilyId) ? e.WithRetired(true) : e), _families);
    }

    public NativeMobyPersistentOwnerLedger Reactivate(IEnumerable<NativeMobyPersistentOwnerRequest> requests)
    {
        var batch = RequireRequests(requests);
        foreach (var request in batch)
        {
            var family = _layout.RequireFamily(request.Family);
            if (!_families.TryGetValue(request.Id, out var old) || !old.SameIdentity(family) ||
                Entries.Any(e => e.FamilyId == request.Id && !e.Retired))
                throw new InvalidDataException("Reactivation requires the exact same fully retired source-bound family.");
        }
        var selected = batch.Select(r => r.Id).ToHashSet(StringComparer.Ordinal);
        return new(_layout, Entries.Select(e => selected.Contains(e.FamilyId) ? e.WithRetired(false) : e), _families);
    }

    public void RequireNoUnmaterializedRetirements()
    {
        if (Entries.Any(e => e.Retired))
            throw new InvalidDataException("Reserved retired native rows lack a source-proven tombstone/materializer; emitting FF/FD/zero is not authorized.");
    }

    public byte[] Serialize()
    {
        using MemoryStream output = new();
        using (Utf8JsonWriter writer = new(output))
        {
            var b = _layout.Binding;
            writer.WriteStartObject(); writer.WriteNumber("schemaVersion", 1);
            writer.WriteString("sourceImageSha256", b.SourceImageSha256); writer.WriteNumber("levelId", b.LevelId);
            writer.WriteNumber("saveSlot", b.SaveSlot); writer.WriteNumber("originalRowCount", b.OriginalRowCount);
            writer.WriteString("originalRowsSha256", b.OriginalRowsSha256); writer.WriteNumber("highWater", HighWater);
            writer.WriteStartArray("entries");
            foreach (var e in Entries)
            {
                writer.WriteStartObject(); writer.WriteString("familyId", e.FamilyId); writer.WriteString("roleId", e.RoleId);
                writer.WriteString("policyId", e.PolicyId); writer.WriteNumber("donorLevelId", e.Role.DonorLevelId);
                writer.WriteNumber("donorTrueIndex", e.Role.DonorTrueIndex); writer.WriteNumber("nativeClass", e.NativeClass);
                writer.WriteString("sourceVariantId", e.SourceVariantId); writer.WriteNumber("nativeIndex", e.NativeIndex);
                writer.WriteBoolean("retired", e.Retired); writer.WriteEndObject();
            }
            writer.WriteEndArray(); writer.WriteEndObject();
        }
        return output.ToArray();
    }

    private static NativeMobyPersistentOwnerRequest[] RequireRequests(IEnumerable<NativeMobyPersistentOwnerRequest> requests)
    {
        var batch = NativeMobyPersistentOwnerLayout.Snapshot(requests, "append/reactivation requests");
        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (var request in batch)
        {
            if (request == null || request.Family == null) throw new InvalidDataException("Missing persistent family request.");
            NativeMobyPersistentOwnerLayout.RequireId(request.Id);
            if (!ids.Add(request.Id)) throw new InvalidDataException("Duplicate persistent family request.");
        }
        return batch;
    }
}
