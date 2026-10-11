using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.Core.Crash;

// MIRRORS the Year of the Dragon importer (source-yotd worktree: YearOfTheDragon/YearOfTheDragonLevelImport.cs Carriers,
// TextureMerger and KillHeight; Exporting/NativeAuthoredPictureVram.cs): the same carrier rules, merge and texture-memory
// test, copied here so the Crash importer does not depend on that worktree. Unify the two when both are merged.

/// <summary>Helpers an imported level's pictures need: the Spyro the Dragon texture slots that can carry a picture, the
/// similarity merge that brings a level's textures down to what the carriers and texture memory hold, and the replaced
/// retail level's kill height.</summary>
public static class CrashLevelPictures
{
    /// <summary>A Spyro the Dragon texture slot that can carry a picture.</summary>
    public sealed record Carrier(int Wad, int Id, int Bytes);

    /// <summary>Texture slots that can carry a picture: canonical layout (<see cref="NativeTerrainTextureImageWriter.Records"/>),
    /// no mirrored tiles, sharing no pixels or palettes with another texture, not animated or scrolled and not an
    /// animation's source; smallest texture-memory footprint first (the free memory holds more of them).</summary>
    public static List<Carrier> Carriers(UniversalSourceLibrary lib, LevelCatalog catalog, IEnumerable<string> keys, string spyroDisc, CancellationToken token = default)
    {
        List<Carrier> sized = [];
        foreach (string key in keys.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            token.ThrowIfCancellationRequested();
            var level = catalog.FindByKey(key);
            if (level == null || level.SourceWadEntry is < 10 or > 78) continue;
            var data = lib.Get(key).Data;
            var (mo, ml) = data.Subfiles()[1];
            var textureList = data.Bytes.AsSpan(mo, ml);
            var records = NativeTerrainTextureImageWriter.Records(textureList);
            var footprint = NativeTerrainTextureImageWriter.Footprint(textureList);
            var groups = NativeTerrainTextureImageWriter.SharingGroups(footprint);
            var audit = NativeTerrainTextureRuntimeControlScanner.Inspect(spyroDisc, level.SourceWadEntry);
            if (!audit.Complete) continue;
            foreach (var g in groups.Where(g => g.Count == 1))
            {
                int id = g[0];
                if (records[id] == null || records[id]!.Descriptors.Any(d => d.Mirrored)) continue;
                if (audit.IsRuntimePersistentTarget(id) && !audit.AnimationSourceTextureIds.Contains(id))
                    sized.Add(new(level.SourceWadEntry, id, footprint[id].Sum(r => r.Width * r.Rows)));
            }
        }
        return [.. sized.OrderBy(c => c.Bytes).ThenBy(c => c.Wad).ThenBy(c => c.Id)];
    }

    /// <summary>The lowest height (editor units) Spyro can live at in the rebuilt level <paramref name="levelId"/>: Create
    /// BIN lowers the slot's kill height under the level's floors, down to this (<see cref="NativeKillHeights.Minimum"/>).</summary>
    public static float KillHeight(byte[] spyroExe, int levelId) => NativeKillHeights.Minimum(spyroExe, levelId);

    /// <summary>Greedy texture merge (the Lost Fleet build's texture_merge.py): the picture whose merge costs least (its
    /// faces x the mean squared difference of 8x8 colour blocks to the nearest other kept picture) folds into that
    /// picture. Fixed pictures neither fold nor take others.</summary>
    public sealed class TextureMerger
    {
        private readonly Dictionary<int, double[]> _blocks = [];
        private readonly Dictionary<int, int> _weight, _target = [];
        private readonly HashSet<int> _fixed;
        private readonly SortedSet<int> _keep;
        public TextureMerger(IReadOnlyDictionary<int, Rgba32[]> pictures, IReadOnlyDictionary<int, int> use, IReadOnlySet<int> fixedPictures)
        {
            _weight = new(use); _fixed = [.. fixedPictures]; _keep = [.. use.Keys];
            foreach (var (t, p) in pictures)
            {
                var b = new double[64 * 3];
                for (int by = 0; by < 8; by++)
                    for (int bx = 0; bx < 8; bx++)
                    {
                        double r = 0, g = 0, bl = 0;
                        for (int y = by * 8; y < by * 8 + 8; y++)
                            for (int x = bx * 8; x < bx * 8 + 8; x++) { var c = p[y * 64 + x]; r += c.R; g += c.G; bl += c.B; }
                        int k = (by * 8 + bx) * 3; b[k] = r / 64; b[k + 1] = g / 64; b[k + 2] = bl / 64;
                    }
                _blocks[t] = b;
            }
            foreach (int t in use.Keys) _target[t] = t;
        }
        public IReadOnlyList<int> Kept => [.. _keep];
        public bool CanMerge => _keep.Count(t => !_fixed.Contains(t)) >= 2;
        public int Target(int picture) => _target[picture];
        public Dictionary<int, int> Merges() => _target.Where(x => x.Key != x.Value).ToDictionary(x => x.Key, x => x.Value);
        public double Distance(int a, int b)
        {
            double s = 0; var x = _blocks[a]; var y = _blocks[b];
            for (int i = 0; i < x.Length; i += 3) s += (x[i] - y[i]) * (x[i] - y[i]) + (x[i + 1] - y[i + 1]) * (x[i + 1] - y[i + 1]) + (x[i + 2] - y[i + 2]) * (x[i + 2] - y[i + 2]);
            return s / 64;
        }
        public void MergeTo(int count)
        {
            while (_keep.Count > count)
            {
                (double Cost, int T, int U)? best = null;
                foreach (int t in _keep.Where(t => !_fixed.Contains(t)))
                {
                    int u = -1; double d = double.MaxValue;
                    foreach (int k in _keep) if (k != t && !_fixed.Contains(k)) { double e = Distance(t, k); if (e < d) { d = e; u = k; } }
                    if (u < 0) continue;
                    double cost = _weight[t] * d;
                    if (best == null || cost < best.Value.Cost) best = (cost, t, u);
                }
                if (best is not { } b) throw new InvalidDataException("No picture is left that can be merged.");
                _keep.Remove(b.T); _weight[b.U] += _weight[b.T];
                foreach (var k in _target.Keys.ToList()) if (_target[k] == b.T) _target[k] = b.U;
            }
        }
    }
}

/// <summary>Whether a built level's terrain pictures fit its texture memory, decided as Create BIN decides it (a copy of
/// the Year of the Dragon importer's NativeAuthoredPictureVram): a built level draws with the ID65 bootstrap's texture
/// memory, whatever retail level it replaces. <see cref="NativeFreshMainCompilation"/> copies every material's retail
/// texture into the free part of it; the object composer then copies the bootstrap body's dragon rescue scenes and
/// every object into what is left. Read-only: nothing is written to any disc.</summary>
internal sealed class CrashPictureVram
{
    private const int GpuLength = 0x80000;
    private readonly string _originalUsa;
    private readonly byte[] _upload, _rescueVram;
    private readonly IReadOnlyList<NativeId65NormalResourceOwner> _owners;
    private readonly IReadOnlyList<byte[]> _rescueScenes;
    private readonly Dictionary<(int Wad, int Id), NativeStaticTerrainTextureSource> _sources = [];

    private CrashPictureVram(string originalUsa, byte[] upload, IReadOnlyList<NativeId65NormalResourceOwner> owners, IReadOnlyList<byte[]> rescueScenes, byte[] rescueVram)
    { _originalUsa = originalUsa; _upload = upload; _owners = owners; _rescueScenes = rescueScenes; _rescueVram = rescueVram; }

    /// <summary>The bootstrap's texture memory as <see cref="NativeFreshMainCompilation.CaptureAsync"/> prepares it for a
    /// scene whose first material is <paramref name="first"/>, and the bootstrap body's rescue scenes;
    /// <paramref name="replacedLevelVram"/>: the replaced retail level's texture memory (the rescue scenes' source).</summary>
    public static async Task<CrashPictureVram> CaptureAsync(string originalUsa, AuthoredMaterial first, byte[]? replacedLevelVram, CancellationToken token = default)
    {
        const int sx = 8960, sy = 8960, sz = 9216;
        int floor = sz - 512;
        var selected = NativeFreshFloorMaterialIntent.RetailTerrain("spyro1-usa-retail-mode2-2352-v1", first.SourceWadEntry, first.TextureId);
        var tile = NativeFreshFlatTileIntent.Create(Guid.NewGuid().ToString("N"), sx - 128, sy - 128, floor, 256, 256, new(128, 96, 64), NativeFreshFloorMaterialsIntent.Create(selected, selected));
        var seed = NativeFreshLevelDocumentIntent.CreateTiled(NativeFreshTiledLevelContent.Create([tile], sx, sy, sz, 64, new(80, 128, 112), 19), []);
        var issued = await NativeId65TiledDocumentCompilation.CaptureSelectedMaterialsAsync(originalUsa, seed, token);
        var rewards = await NativeFreshRewardFallbackResources.CaptureAsync(originalUsa, issued.FreshResourceAncestor, token);
        var scenes = replacedLevelVram == null ? [] : NativeDragonRescueCamera.Scenes(new UniversalLevelData(issued.DiscPlan.CopyBody())).Select(s => s.Package).ToList();
        return new(originalUsa, rewards.CopyUploadPayload(), rewards.UploadOwners, scenes, replacedLevelVram ?? []);
    }

    /// <summary>Lays the materials out (in order, as the export numbers them), then the rescue scenes' textures; the
    /// texture memory left for objects, or the allocator's reason.</summary>
    public (bool Fits, int FreeBytes, string? Reason) Test(IReadOnlyList<AuthoredMaterial> materials)
    {
        if (materials.Count == 0) return (true, -1, null);
        try
        {
            var missing = materials.Select(m => (m.SourceWadEntry, m.TextureId)).Distinct().Where(k => !_sources.ContainsKey(k)).ToArray();
            var captured = new NativeStaticTerrainTextureSource[missing.Length];
            Parallel.For(0, missing.Length, i => captured[i] = NativeStaticTerrainTextureSource.Capture(_originalUsa, missing[i].SourceWadEntry, missing[i].TextureId));
            for (int i = 0; i < missing.Length; i++) _sources[missing[i]] = captured[i];
            var sources = materials.Select(m => _sources[(m.SourceWadEntry, m.TextureId)]).ToArray();
            var allocation = NativeStaticTerrainTextureAllocation.CompileSelected(_upload, _owners, sources.Select((s, i) =>
                new NativeStaticTerrainTextureInput(i, s.Handle, "source-right-half-vram:wad-" + s.SourceWadEntry,
                    NativeStaticTerrainTextureAllocation.RelocationPolicyId, s.Layout.CopyRecord(), s.CopySourceVram())), 0x8000);
            byte[] vram = allocation.CopyUploadPayload().AsSpan(0, GpuLength).ToArray();
            var used = new bool[GpuLength];
            foreach (var owner in _owners.Where(o => o.Offset < GpuLength && o.Provenance != "fresh-zero-upload-background"))
                used.AsSpan(owner.Offset, Math.Min(owner.ByteLength, GpuLength - owner.Offset)).Fill(true);
            foreach (var region in allocation.Allocations)
                for (int row = 0; row < region.RowCount; row++)
                    used.AsSpan(region.TargetOffset + row * 1024, region.RowByteLength).Fill(true);
            for (int i = 0; i < GpuLength; i++) if (vram[i] != 0) used[i] = true;
            List<(long Offset, int Length)> taken = [];
            for (int i = 0; i < GpuLength;)
            {
                if (!used[i]) { i++; continue; }
                int start = i; while (i < GpuLength && used[i]) i++;
                taken.Add((start, i - start));
            }
            var alloc = new UniversalVramAllocator(taken);
            if (_rescueScenes.Count > 0)
                UniversalAssetImport.ImportTexturesBatch([.. _rescueScenes.Select(p => (p.ToArray(), _rescueVram, "a dragon rescue scene"))], vram, alloc);
            return (true, alloc.FreeBytes, null);
        }
        catch (InvalidDataException e) { return (false, 0, e.Message); }
        catch (AggregateException e) when (e.InnerExceptions.All(x => x is InvalidDataException)) { return (false, 0, e.InnerExceptions[0].Message); }
    }
}
