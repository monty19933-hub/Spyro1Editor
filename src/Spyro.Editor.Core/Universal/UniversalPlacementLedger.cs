using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Universal;

public sealed record UniversalPlacementIdentity(string Id, int Slot);

/// <summary>Append-only native row identities. Retired placements keep their slot so an old
/// collected bit cannot move to another object. This is editor metadata, not a new card format.</summary>
public sealed record UniversalPlacementLedger(int SchemaVersion, IReadOnlyList<UniversalPlacementIdentity> Entries)
{
    public static UniversalPlacementLedger Empty => new(1, []);

    public static string IdentityOf(JsonElement edit)
    {
        if (edit.TryGetProperty("universalPlacementId", out var id) && id.ValueKind == JsonValueKind.String)
        {
            string value = id.GetString() ?? "";
            if (!Guid.TryParseExact(value, "N", out _)) throw new InvalidDataException("An object's saved placement identity is invalid.");
            return value;
        }
        // Beta52 had stable editor indices but no identity field. Migration preserves its exact
        // saved ordering, even when the first new edit deletes a previously exported object.
        if (!edit.TryGetProperty("trueIndex", out var index) || !index.TryGetInt32(out int n) || n < 0)
            throw new InvalidDataException("An older imported object has no stable editor index.");
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes($"spyro-universal-beta52:T{n}"));
        return Convert.ToHexStringLower(hash.AsSpan(0, 16));
    }

    public static UniversalPlacementLedger Read(JsonElement root)
    {
        if (root.TryGetProperty("universalPlacementLedger", out var value) && value.ValueKind != JsonValueKind.Null)
        {
            var ledger = value.Deserialize<UniversalPlacementLedger>() ?? throw new InvalidDataException("The placement identity ledger is empty.");
            ledger.Validate();
            return ledger;
        }
        List<UniversalPlacementIdentity> entries = [];
        if (root.TryGetProperty("edits", out var edits) && edits.ValueKind == JsonValueKind.Array)
            foreach (var edit in edits.EnumerateArray())
                if (UniversalPlacementReader.IsUniversalEdit(edit) &&
                    edit.TryGetProperty("added", out var a) && a.ValueKind == JsonValueKind.True &&
                    !(edit.TryGetProperty("removed", out var r) && r.ValueKind == JsonValueKind.True))
                    entries.Add(new(IdentityOf(edit), entries.Count));
        var migrated = new UniversalPlacementLedger(1, entries);
        migrated.Validate();
        return migrated;
    }

    public static UniversalPlacementLedger Reconcile(string path, IReadOnlyList<Moby> mobys)
    {
        UniversalPlacementLedger previous = Empty;
        if (File.Exists(path))
        {
            using var doc = JsonDocument.Parse(File.ReadAllBytes(path));
            previous = Read(doc.RootElement);
        }
        previous = Spyro.Editor.Core.Editing.StartBlankRecoveryStore.ReconcileNativeLedger(path, previous);
        List<UniversalPlacementIdentity> entries = [.. previous.Entries];
        HashSet<string> known = entries.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        HashSet<string> active = new(StringComparer.Ordinal);
        foreach (var moby in mobys.Where(m => m.IsAdded && m.CrossLevelRequiredExporterFeature == UniversalMobyExport.RequiredExporterFeature))
        {
            string id = moby.UniversalPlacementId;
            if (!Guid.TryParseExact(id, "N", out _) || !active.Add(id))
                throw new InvalidDataException("Two imported objects share a placement identity. Duplicate using Copy/Paste so each object gets its own identity.");
            if (known.Add(id)) entries.Add(new(id, entries.Count));
        }
        var result = new UniversalPlacementLedger(1, entries);
        result.Validate();
        return result;
    }

    public void Validate()
    {
        if (SchemaVersion != 1 || Entries == null || Entries.Count > 256)
            throw new InvalidDataException("This project's placement identity ledger has an unsupported version or exceeds the native 256-row limit.");
        HashSet<string> ids = new(StringComparer.Ordinal);
        for (int i = 0; i < Entries.Count; i++)
            if (Entries[i].Slot != i || !Guid.TryParseExact(Entries[i].Id, "N", out _) || !ids.Add(Entries[i].Id))
                throw new InvalidDataException("The placement identity ledger is invalid; export stopped to preserve collected state.");
    }
}
