using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.Core.Exporting;

/// <summary>Whether a built level's terrain textures fit its texture memory, decided as Create BIN decides it: a built
/// level (Start Blank or a new level) draws with the ID65 bootstrap's texture memory, whatever retail level it replaces.
/// <see cref="NativeFreshMainCompilation"/> copies every material's retail texture (its pixels and palettes, in the
/// texture's own layout) into the free part of it; then the object composer (<see cref="UniversalMobyComposer"/>) copies
/// the textures of the bootstrap body's dragon rescue scenes (its subfiles 4 on, textured from the replaced level's
/// original texture memory) and of every object into what is left. The free memory is captured once, the way that
/// export captures it; each material set is then laid out with the same allocator, the rescue scenes are placed with the
/// composer's own texture import (everything the terrain left occupied protected), and what remains is reported for
/// objects. Read-only: nothing is written to any disc.</summary>
internal sealed class NativeAuthoredPictureVram
{
    private const int GpuLength = 0x80000;
    private readonly string _originalUsa;
    private readonly byte[] _upload, _rescueVram;
    private readonly IReadOnlyList<NativeId65NormalResourceOwner> _owners;
    private readonly IReadOnlyList<byte[]> _rescueScenes;
    private readonly Dictionary<(int Wad, int Id), NativeStaticTerrainTextureSource> _sources = [];

    private NativeAuthoredPictureVram(string originalUsa, byte[] upload, IReadOnlyList<NativeId65NormalResourceOwner> owners, IReadOnlyList<byte[]> rescueScenes, byte[] rescueVram)
    { _originalUsa = originalUsa; _upload = upload; _owners = owners; _rescueScenes = rescueScenes; _rescueVram = rescueVram; }

    /// <summary>The bootstrap's texture memory with its reward-model fallbacks installed, as
    /// <see cref="NativeFreshMainCompilation.CaptureAsync"/> prepares it for a scene whose first material is
    /// <paramref name="first"/> (the throwaway seed floor it compiles first), and the bootstrap body's rescue scenes;
    /// <paramref name="replacedLevelVram"/>: the replaced retail level's texture memory (the rescue scenes' source).</summary>
    public static async Task<NativeAuthoredPictureVram> CaptureAsync(string originalUsa, AuthoredMaterial first, byte[]? replacedLevelVram, CancellationToken token = default)
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

    private NativeStaticTerrainTextureSource Source(AuthoredMaterial m)
    {
        if (!_sources.TryGetValue((m.SourceWadEntry, m.TextureId), out var source))
            _sources[(m.SourceWadEntry, m.TextureId)] = source = NativeStaticTerrainTextureSource.Capture(_originalUsa, m.SourceWadEntry, m.TextureId);
        return source;
    }

    /// <summary>Lays the materials out (in order, as the export numbers them), then the rescue scenes' textures; the
    /// texture memory left for objects, or the allocator's reason.</summary>
    public (bool Fits, int FreeBytes, string? Reason) Test(IReadOnlyList<AuthoredMaterial> materials)
    {
        if (materials.Count == 0) return (true, -1, null);
        try
        {
            // Each capture reads and audits its whole retail level: read the new ones side by side.
            var missing = materials.Select(m => (m.SourceWadEntry, m.TextureId)).Distinct().Where(k => !_sources.ContainsKey(k)).ToArray();
            var captured = new NativeStaticTerrainTextureSource[missing.Length];
            Parallel.For(0, missing.Length, i => captured[i] = NativeStaticTerrainTextureSource.Capture(_originalUsa, missing[i].SourceWadEntry, missing[i].TextureId));
            for (int i = 0; i < missing.Length; i++) _sources[missing[i]] = captured[i];
            var sources = materials.Select(Source).ToArray();
            var allocation = NativeStaticTerrainTextureAllocation.CompileSelected(_upload, _owners, sources.Select((s, i) =>
                new NativeStaticTerrainTextureInput(i, s.Handle, "source-right-half-vram:wad-" + s.SourceWadEntry,
                    NativeStaticTerrainTextureAllocation.RelocationPolicyId, s.Layout.CopyRecord(), s.CopySourceVram())), 0x8000);
            byte[] vram = allocation.CopyUploadPayload().AsSpan(0, GpuLength).ToArray();
            // Occupied after the terrain: every upload owner that is not free background, every allocated texture row,
            // and any other byte that is not zero (the export's ownership scan protects unclassified bytes too).
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
