using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.App.Views;

// Presentation metadata only. Neither these records nor a listing can issue a
// source Inventory, native asset/dependency package or compilation capability.
internal sealed record NativeFreshMobyCatalogueItem(string SourceVariantId, string SourceLevelKey,
    string SourceLevelName, int? SourceLevelId, int? SourceWadEntry, int? TrueIndex, int? RegistrationSlot,
    ushort ActorId, NativeMobyCatalogueEntryKind Kind, string Label, string Category, string ClassificationEvidence);

internal sealed class NativeFreshMobyCatalogueListing
{
    private const string OriginalRetailVariant = "spyro1-usa-retail-mode2-2352-v1";
    private readonly string _sourceImageSha256;
    private readonly Dictionary<string, NativeFreshMobyCatalogueItem> _byId;
    private readonly Func<string?, string?, NativeMobyCatalogueView, IReadOnlyList<NativeFreshMobyCatalogueItem>>? _actualSearch;

    internal NativeFreshMobyCatalogueListing(string sourceImageSha256, IEnumerable<NativeFreshMobyCatalogueItem> entries)
        : this(sourceImageSha256, entries, null) { }

    private NativeFreshMobyCatalogueListing(string sourceImageSha256, IEnumerable<NativeFreshMobyCatalogueItem> entries,
        Func<string?, string?, NativeMobyCatalogueView, IReadOnlyList<NativeFreshMobyCatalogueItem>>? actualSearch)
    {
        ArgumentNullException.ThrowIfNull(sourceImageSha256);
        ArgumentNullException.ThrowIfNull(entries);
        _sourceImageSha256 = sourceImageSha256;
        Entries = Array.AsReadOnly(entries.ToArray());
        _byId = Entries.ToDictionary(e => e.SourceVariantId, StringComparer.Ordinal);
        _actualSearch = actualSearch;
    }

    internal IReadOnlyList<NativeFreshMobyCatalogueItem> Entries { get; }

    internal static NativeFreshMobyCatalogueListing FromInventory(NativeMobySourceInventory inventory)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        var catalogue = new NativeMobyCatalogue(inventory);
        var items = catalogue.Search(view: NativeMobyCatalogueView.All).Select(entry => new NativeFreshMobyCatalogueItem(
            entry.SourceVariantId, entry.SourceLevelKey, entry.SourceLevelName, entry.SourceLevelId,
            entry.SourceWadEntry, entry.TrueIndex, entry.RegistrationSlot, entry.ActorId,
            entry.Kind, entry.Label, entry.Category, entry.ClassificationEvidence)).ToArray();
        var byId = items.ToDictionary(e => e.SourceVariantId, StringComparer.Ordinal);
        // Carry the ACTUAL issued Inventory's full image identity. Do not replace
        // it with a known-original constant, a row hash or a selected source path.
        // GetAuthoringSource keeps any non-original future inventory inspect-only.
        return new(inventory.SourceImageSha256, items, (query, key, view) =>
            Array.AsReadOnly(catalogue.Search(query, key, view).Select(entry => byId[entry.SourceVariantId]).ToArray()));
    }

    internal IReadOnlyList<NativeFreshMobyCatalogueItem> Search(string? query = null, string? sourceLevelKey = null,
        NativeMobyCatalogueView view = NativeMobyCatalogueView.PlacedRows)
    {
        // Production delegates to the existing catalogue. Detached neutral test
        // listings use the same bounded metadata predicate, without an Inventory.
        if (_actualSearch != null) return _actualSearch(query, sourceLevelKey, view);
        if (!Enum.IsDefined(view) || query?.Length > 256)
            throw new InvalidDataException("Catalogue query/view exceeds its bounded metadata scope.");
        string[] terms = (query ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string key = string.IsNullOrWhiteSpace(sourceLevelKey) ? "" : LevelCatalog.NormalizeKey(sourceLevelKey);
        return Array.AsReadOnly(Entries.Where(entry =>
            (view == NativeMobyCatalogueView.All || (view == NativeMobyCatalogueView.PlacedRows) == (entry.Kind == NativeMobyCatalogueEntryKind.PlacedRow)) &&
            (key.Length == 0 || entry.SourceLevelKey == key) &&
            terms.All(term => $"{entry.Label} {entry.Category} {entry.SourceLevelName} {entry.SourceLevelKey} 0x{entry.ActorId:X4} T{entry.TrueIndex}"
                .Contains(term, StringComparison.OrdinalIgnoreCase))).ToArray());
    }

    internal NativeFreshRetailMobyReference? GetAuthoringSource(NativeFreshMobyCatalogueItem? entry)
    {
        if (!string.Equals(_sourceImageSha256, NativeMobySourceInventory.OriginalImageSha256, StringComparison.OrdinalIgnoreCase) ||
            entry == null || !_byId.TryGetValue(entry.SourceVariantId, out var owned) || !ReferenceEquals(entry, owned) ||
            entry.Kind != NativeMobyCatalogueEntryKind.PlacedRow || entry.TrueIndex is not int index ||
            entry.SourceLevelId == null || entry.SourceWadEntry is not int sourceWadEntry)
            return null;
        try { return NativeFreshRetailMobyReference.Create(OriginalRetailVariant, sourceWadEntry, index, entry.ActorId); }
        catch (InvalidDataException) { return null; }
    }
}

internal interface INativeFreshMobyCatalogueProvider
{
    // Inventory.Capture has no cancellation input. Its actual task must drain
    // before the caller releases the editor owner, even after canceled delivery.
    Task<NativeFreshMobyCatalogueListing> CaptureAsync(string image);
}

internal sealed class NativeFreshMobyCatalogueProvider(LevelCatalog catalog) : INativeFreshMobyCatalogueProvider
{
    public Task<NativeFreshMobyCatalogueListing> CaptureAsync(string image) => Task.Run(() =>
        NativeFreshMobyCatalogueListing.FromInventory(NativeMobySourceInventory.Capture(image, catalog)));
}
