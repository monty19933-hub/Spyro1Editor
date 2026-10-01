using System.Buffers.Binary;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Universal;

/// <summary>A placeable source variant: one native row in one source level.</summary>
public sealed record UniversalMobyVariant(int ClassId, string SourceLevelKey, int SourceRow, int PropertiesBytes,
    int ModelBytes, int BehaviorBytes, bool HasLocalModel, bool SharedModel, bool HasBehavior)
{
    public string? RestrictionReason { get; init; }
    public int DroppedExternalPointers { get; init; }
    public bool Extractable => RestrictionReason == null;
}

public sealed record UniversalMobyCatalogEntry(
    int ClassId,
    string Name,
    string Category,
    string SubCategory,
    IReadOnlyList<UniversalMobyVariant> Variants,
    UniversalMobyVariant Default,
    IReadOnlyList<string> SourceLevels,
    string? RestrictionReason,
    string? Notes = null)
{
    public string ClassHex => $"0x{ClassId:X3}";
    public bool Placeable => RestrictionReason == null;
    public string SearchText => $"{Name} {Category} {SubCategory} {ClassHex} {string.Join(' ', SourceLevels)}".ToLowerInvariant();
}

/// <summary>Estimated cost of placing a catalogue entry into a specific destination.</summary>
public sealed record UniversalDestinationEstimate(bool Native, bool NeedsModel, bool NeedsBehavior, int EstimatedRamBytes,
    int RamHeadroomBytes, bool Fits, string Summary)
{
    public bool TexturesFit { get; init; } = true;
}

public static class UniversalMobyCatalogBuilder
{
    public delegate (string Name, string Kind)? NameResolver(string levelKey, int rowIndex);

    /// <summary>Default variant per placed class: a source level that owns the model (or uses the shared
    /// model table) and native behavior, preferring the smallest total import cost.</summary>
    public static IReadOnlyList<UniversalMobyVariant> DefaultVariants(UniversalSourceLibrary lib, LevelCatalog catalog) =>
        AllVariants(lib, catalog).GroupBy(v => v.ClassId).Select(g => Pick(g)).OrderBy(v => v.ClassId).ToList();

    private static UniversalMobyVariant Pick(IEnumerable<UniversalMobyVariant> g) => g
        .OrderBy(v => v.Extractable ? 0 : 1)
        .ThenBy(v => v.HasLocalModel || v.SharedModel ? 0 : 1)
        .ThenBy(v => v.HasBehavior ? 0 : 1)
        .ThenBy(v => v.ModelBytes + v.BehaviorBytes)
        .ThenBy(v => v.SourceLevelKey, StringComparer.Ordinal).ThenBy(v => v.SourceRow).First();

    private static readonly Dictionary<string, List<UniversalMobyVariant>> VariantCache = [];

    public static IReadOnlyList<UniversalMobyVariant> AllVariants(UniversalSourceLibrary lib, LevelCatalog catalog)
    {
        lock (VariantCache) if (VariantCache.TryGetValue(lib.ImagePath, out var hit)) return hit;
        List<UniversalMobyVariant> list = [];
        foreach (var level in catalog.Levels.Where(l => l.SourceWadEntry > 0).OrderBy(l => l.LevelId))
        {
            var (_, data, overlay) = lib.Get(level.Key);
            var regs = data.Registrations();
            var sw = UniversalDispatchAnalysis.FindUpdateSwitch(overlay);
            var sc = data.Scene();
            Dictionary<int, int> behaviorBytes = [];
            Dictionary<int, string> behaviorFailures = [];
            for (int i = 0; i < sc.RowCount; i++)
            {
                byte[] row = data.Row(i);
                int c = BinaryPrimitives.ReadUInt16LittleEndian(row.AsSpan(0x36));
                string? restriction = c == 0 ? "Spyro's animation stream is not a placeable object."
                    : c >= 0x200 ? "Class ID is outside the engine's supported class range." : null;
                if (!behaviorBytes.TryGetValue(c, out int bb))
                {
                    if (!UniversalDispatchAnalysis.Handles(overlay, sw, c)) bb = 0;
                    else
                    {
                        try { bb = UniversalBehaviorBundleBuilder.BuildPrunedUpdate(overlay, [c], "e").Bytes; }
                        catch (InvalidDataException e) { bb = -1; behaviorFailures[c] = e.Message; }
                    }
                    behaviorBytes[c] = bb;
                }
                if (bb < 0) restriction ??= "Behavior cannot be extracted: " + behaviorFailures[c];
                bool local = regs.Any(r => r.ClassId == c);
                int model = local ? data.Package(c).Length : 0;
                var t = data.CaptureRow(level.Key, i);
                list.Add(new(c, level.Key, i, t.Properties.Length, model, Math.Max(0, bb), local, lib.SharedClasses.Contains(c),
                    UniversalDispatchAnalysis.Handles(overlay, sw, c)) { RestrictionReason = restriction, DroppedExternalPointers = t.DroppedExternalPointers });
            }
        }
        lock (VariantCache) VariantCache[lib.ImagePath] = list;
        return list;
    }

    public static IReadOnlyList<UniversalMobyCatalogEntry> Build(UniversalSourceLibrary lib, LevelCatalog catalog, NameResolver? names = null)
    {
        List<UniversalMobyCatalogEntry> entries = [];
        foreach (var g in AllVariants(lib, catalog).GroupBy(v => v.ClassId).OrderBy(g => g.Key))
        {
            var def = Pick(g);
            // Prefer a confident label from any variant, falling back to the default variant.
            (string Name, string Kind)? label = null;
            foreach (var v in g.OrderBy(v => v == def ? 0 : 1))
            {
                var r = names?.Invoke(v.SourceLevelKey, v.SourceRow);
                if (r is { } x && !string.IsNullOrWhiteSpace(x.Name) && !IsWeak(x.Name)) { label = x; break; }
                label ??= r;
            }
            string name = label?.Name is { Length: > 0 } n ? n : $"Object 0x{g.Key:X3}";
            var (cat, sub) = Categorize(name, label?.Kind ?? "");
            string? restriction = def.RestrictionReason;
            if (!def.HasLocalModel && !def.SharedModel && def.HasBehavior)
                restriction = def.RestrictionReason; // model-less controllers import as behavior only
            if (g.Key == 0) restriction = "Spyro's own animation stream is not a placeable object.";
            entries.Add(new(g.Key, name, cat, sub, g.ToList(), def,
                g.Select(v => v.SourceLevelKey).Distinct().ToList(), restriction));
        }
        return entries;
    }

    static bool IsWeak(string label) => label.Contains("unknown", StringComparison.OrdinalIgnoreCase) ||
        label.Contains("candidate", StringComparison.OrdinalIgnoreCase) || label.StartsWith("Type ", StringComparison.OrdinalIgnoreCase);

    public static (string Category, string Sub) Categorize(string name, string kind)
    {
        string s = (name + " " + kind).ToLowerInvariant();
        bool Has(params string[] k) => k.Any(s.Contains);
        if (Has("dragonfly egg", "japanese egg")) return ("Collectibles", "Dragonfly Eggs");
        if (Has("sign")) return ("Interactive", "Signs");
        if (Has("gem", "treasure")) return Has("chest", "basket", "vase", "jar", "pot") ? ("Interactive", "Chests & Containers") : ("Collectibles", "Gems");
        if (Has("dragon statue", "dragon rescue", "rescued dragon", "dragon elder") || (Has("dragon") && Has("statue", "rescue", "elder")))
            return ("Collectibles", "Dragons");
        if (Has("egg thief", "egg-thief", "thief")) return ("Enemies", "Egg Thieves");
        if (Has("egg")) return ("Collectibles", "Eggs");
        if (Has("key")) return ("Interactive", "Keys");
        if (Has("chest", "basket", "vase", "jar", "barrel", "crate")) return ("Interactive", "Chests & Containers");
        if (Has("portal", "door", "gate", "switch", "trigger", "lever", "platform", "elevator", "lift", "ladder", "whirlwind", "supercharge", "ramp"))
            return ("Interactive", "Doors, Platforms & Triggers");
        if (Has("balloon", "npc", "fairy", "zoe", "keeper", "elder")) return ("NPCs", "");
        if (Has("sheep", "cow", "frog", "chicken", "rabbit", "fodder", "butterfly", "bird", "fish", "seal", "lamb", "goat", "ram ", "turkey", "animal"))
            return ("NPCs", "Fodder & Animals");
        if (Has("gnorc", "wizard", "enemy", "boss", "druid", "knight", "armor", "bull", "gnasty", "shaman", "cannon", "attack", "hostile", "guard", "thrower", "lizard", "monster"))
            return ("Enemies", "");
        if (Has("effect", "particle", "sparkle", "smoke", "fire", "flame", "explosion", "lightning", "debris"))
            return ("Effects", "");
        if (Has("prop", "scenery", "tree", "flag", "rock", "plant", "banner", "statue", "decoration", "lamp", "torch", "bush"))
            return ("Props", "");
        return ("Other / Unknown", "");
    }

    /// <summary>Fast pre-placement estimate for the browser; the composer remains the authority at export.</summary>
    public static UniversalDestinationEstimate Estimate(UniversalSourceLibrary lib, LevelDefinition destination,
        UniversalMobyVariant v, int alreadyPlannedBytes = 0)
    {
        var (_, data, overlay) = lib.Get(destination.Key);
        if (v.RestrictionReason != null) return new(false, false, false, 0, 0, false, v.RestrictionReason);
        if (UniversalSpecialCatalog.IsSpecial(v))
        {
            bool have = data.Registrations().Any(r => r.ClassId == v.ClassId);
            int need = (have ? 0 : UniversalLevelData.Align(v.ModelBytes, 0x800)) + v.BehaviorBytes + UniversalLevelData.Align(v.PropertiesBytes, 4) + 8;
            int room = (int)(UniversalMobyComposer.NormalBufferFloor - UniversalMobyComposer.RamSafetyMargin -
                UniversalMobyComposer.RuntimeEnd(data, overlay.CopyBuffer)) - alreadyPlannedBytes;
            return new(false, !have, true, need, room, need <= room, $"Imports model + behavior · ~{need / 1024.0:0.0} KB of {Math.Max(0, room) / 1024.0:0.0} KB free");
        }
        var sw = UniversalDispatchAnalysis.FindUpdateSwitch(overlay);
        bool native = UniversalDispatchAnalysis.Handles(overlay, sw, v.ClassId);
        bool registered = data.Registrations().Any(r => r.ClassId == v.ClassId) || lib.SharedClasses.Contains(v.ClassId);
        bool needModel = !registered && v.HasLocalModel;
        bool needBehavior = !native && v.HasBehavior;
        int depModels = 0;
        if (needBehavior)
        {
            var so = lib.Get(v.SourceLevelKey);
            var ssw = UniversalDispatchAnalysis.FindUpdateSwitch(so.Overlay);
            var tr = UniversalDispatchAnalysis.Trace(so.Overlay, ssw.TreeStart, v.ClassId, ssw.Seeds, ssw.ClassRegister);
            try
            {
                var (clo, _) = UniversalDispatchAnalysis.Closure(so.Overlay, ssw.Function, [tr.Stop], new HashSet<uint> { ssw.TreeStart });
                foreach (int k in UniversalDispatchAnalysis.Dependencies(so.Overlay, clo).SpawnedClasses)
                    if (!data.Registrations().Any(r => r.ClassId == k) && !lib.SharedClasses.Contains(k) && so.Data.Registrations().Any(r => r.ClassId == k))
                        depModels += UniversalLevelData.Align(so.Data.Package(k).Length, 0x800);
            }
            catch (InvalidDataException) { }
        }
        int ram = depModels + (needModel ? UniversalLevelData.Align(v.ModelBytes, 0x800) : 0)
            + (needBehavior ? v.BehaviorBytes + EstimateInitBytes(lib, v.SourceLevelKey) + 0x60 : 0)
            + UniversalLevelData.Align(v.PropertiesBytes, 4) + 8;
        int headroom = (int)(UniversalMobyComposer.NormalBufferFloor - UniversalMobyComposer.RamSafetyMargin -
            UniversalMobyComposer.RuntimeEnd(data, overlay.CopyBuffer)) - alreadyPlannedBytes;
        bool texturesFit = !needModel || TexturesFit(lib, destination, v);
        bool fits = ram <= headroom && texturesFit;
        string summary = native && registered ? "Native to this level"
            : !texturesFit ? "Its textures do not fit in this level's free texture memory"
            : $"Imports {(needModel ? "model" : "")}{(needModel && needBehavior ? " + " : "")}{(needBehavior ? "behavior" : "")} · ~{ram / 1024.0:0.0} KB of {Math.Max(0, headroom) / 1024.0:0.0} KB free";
        return new(native && registered, needModel, needBehavior, ram, headroom, fits, summary) { TexturesFit = texturesFit };
    }

    /// <summary>Exact check: composes the destination's existing universal placements plus the candidate
    /// against the original disc. Returns null on success or the refusal message.</summary>
    public static (UniversalComposeResult? Result, string? Error) DryRun(UniversalSourceLibrary lib, LevelDefinition destination,
        IReadOnlyList<UniversalMobyPlacement> placements, Func<string, UniversalSpecialAsset?>? special = null,
        IReadOnlyCollection<int>? removedRows = null)
    {
        try
        {
            var d = lib.Get(destination.Key);
            List<(long, int)>? vram;
            lock (ProtectedCache) ProtectedCache.TryGetValue(lib.ImagePath + "|" + destination.Key, out vram);
            if (vram == null)
            {
                vram = UniversalMobyExport.DefaultProtectedVram(lib.ImagePath, destination).ToList();
                lock (ProtectedCache) ProtectedCache[lib.ImagePath + "|" + destination.Key] = vram;
            }
            var r = UniversalMobyComposer.Compose(destination, d.Data.Bytes, d.Overlay.Bytes.ToArray(), lib.Disc.Exe,
                lib.Get, lib.SharedClasses, vram, placements, special, removedRows,
                dropped => UniversalMobyExport.DefaultProtectedVram(lib.ImagePath, destination, dropped), lib.SharedModelPackage);
            return (r, null);
        }
        catch (Exception e) when (e is InvalidDataException or IOException) { return (null, e.Message); }
    }

    private static readonly Dictionary<string, List<(long, int)>> ProtectedCache = [];
    private static readonly Dictionary<string, UniversalVramAllocator> BaseVram = [];
    private static readonly Dictionary<(string, int, string), bool> TextureFit = [];

    public static bool TexturesFit(UniversalSourceLibrary lib, LevelDefinition destination, UniversalMobyVariant v)
    {
        var key = (lib.ImagePath + "|" + destination.Key, v.ClassId, v.SourceLevelKey);
        lock (TextureFit) if (TextureFit.TryGetValue(key, out bool hit)) return hit;
        UniversalVramAllocator? baseAlloc;
        lock (BaseVram) BaseVram.TryGetValue(lib.ImagePath + "|" + destination.Key, out baseAlloc);
        if (baseAlloc == null)
        {
            baseAlloc = new UniversalVramAllocator(UniversalMobyExport.DefaultProtectedVram(lib.ImagePath, destination));
            lock (BaseVram) BaseVram[lib.ImagePath + "|" + destination.Key] = baseAlloc;
        }
        var src = lib.Get(v.SourceLevelKey).Data;
        byte[] pkg = src.Package(v.ClassId);
        byte[] vram = lib.Get(destination.Key).Data.Vram().ToArray();
        bool fits;
        try { UniversalAssetImport.ImportTextures(pkg, src.Vram(), vram, baseAlloc.Clone()); fits = true; }
        catch (InvalidDataException) { fits = false; }
        lock (TextureFit) TextureFit[key] = fits;
        return fits;
    }

    private static readonly Dictionary<string, int> InitSize = [];
    private static int EstimateInitBytes(UniversalSourceLibrary lib, string key)
    {
        lock (InitSize) if (InitSize.TryGetValue(lib.ImagePath + "|" + key, out int v)) return v;
        var o = lib.Get(key).Overlay;
        int bytes = UniversalBehaviorBundleBuilder.BuildWholeFunction(o, o.Init, "i").Bytes;
        lock (InitSize) InitSize[lib.ImagePath + "|" + key] = bytes;
        return bytes;
    }
}
