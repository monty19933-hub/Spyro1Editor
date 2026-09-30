using System.Buffers.Binary;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Universal;

/// <summary>One Moby placed in a destination level from any source level's native row.</summary>
public sealed record UniversalMobyPlacement(string SourceLevelKey, int SourceRowIndex, int X, int Y, int Z,
    int? Yaw = null, IReadOnlyDictionary<int, byte>? RowByteOverrides = null, string? EditorId = null, string? SpecialId = null,
    IReadOnlyList<string>? Text = null, int? PersistentSlot = null, bool Retired = false);

public sealed record UniversalClassImport(int ClassId, string SourceLevelKey, bool ModelImported, bool BehaviorImported,
    int ModelBytes, UniversalTextureReport? Textures, UniversalSoundReport? Sounds, bool DependencyOnly);

public sealed record UniversalRamBudget(uint OriginalEnd, uint FinalEnd, uint Limit, int OriginalHeadroom, int FinalHeadroom,
    int OverlayGrowth, int ModelGrowth, int SceneGrowth, int DataGrowth);

public sealed record UniversalComposeResult(
    string DestinationKey,
    IReadOnlyList<UniversalClassImport> Classes,
    IReadOnlyList<int> NewRowIndices,
    UniversalRamBudget Ram,
    int ArenaSlotsRemaining,
    int VramBytesUsed,
    IReadOnlyList<UniversalBehaviorSourceReport> Behavior,
    IReadOnlyList<string> Warnings,
    int TreasureDelta,
    byte[] Overlay,
    byte[] LevelData,
    byte[] Executable)
{
    /// <summary>Native models dropped because every placed copy was removed; with the bytes freed.</summary>
    public IReadOnlyList<int> DroppedModelClasses { get; init; } = [];
    public int DroppedModelBytes { get; init; }
    public int ReclaimedVramBytes { get; init; }
    public IReadOnlyList<UniversalParticleDependency> ParticleDependencies { get; init; } = [];
}

/// <summary>
/// Composes universal Moby imports into one destination level: models (with textures and sounds),
/// placed rows with private properties, and relinked source behavior chained onto the destination's
/// native update/init callbacks. The source image is never modified.
/// </summary>
public static class UniversalMobyComposer
{
    public const uint NormalBufferFloor = 0x80187BB0; // lowest normal reserved buffer (see runtime memory ownership)
    public const int RamSafetyMargin = 0x400;
    public const int MinimumDynamicSlots = 16;

    public const uint TreasureTableAddress = 0x8006F438;

    /// <summary>Gem value a native row contributes: a visible gem (type 0x18) or its reward gem ID (+0x53).</summary>
    public static int TreasureValue(byte[] row)
    {
        if (row[0x50] == 0x18 && GemValue.TryFromEncoding(row[0x36], row[0x4F], out GemValue visible)) return visible.Value;
        return GemValue.TryFromIdByte(row[0x53], out GemValue reward) ? reward.Value : 0;
    }

    public static uint RuntimeEnd(UniversalLevelData d, uint copyBuffer)
    {
        var snd = new NativeLevelSoundLayout(d.Bytes);
        var sf = d.Subfiles();
        return (uint)(copyBuffer + (snd.SoundEnd - snd.DataOffset) + sf[2].Length + sf[3].Length);
    }

    public static int ArenaSlots(UniversalLevelData d)
    {
        var sc = d.Scene();
        return (sc.ArenaEnd - (sc.RowsOffset + sc.RowCount * UniversalLevelData.RowStride)) / 112;
    }

    /// <param name="sources">Original-image source level data and overlays keyed by level key.</param>
    /// <param name="shared">Classes resident in the shared PETE model table.</param>
    public static UniversalComposeResult Compose(
        LevelDefinition destination,
        byte[] destinationData,
        byte[] destinationOverlay,
        byte[] executable,
        Func<string, (LevelDefinition Level, UniversalLevelData Data, UniversalOverlay Overlay)> source,
        IReadOnlySet<int> shared,
        IEnumerable<(long Offset, int Length)> protectedVram,
        IReadOnlyList<UniversalMobyPlacement> placements,
        Func<string, UniversalSpecialAsset?>? special = null,
        IReadOnlyCollection<int>? removedRows = null,
        Func<IReadOnlySet<int>, IEnumerable<(long Offset, int Length)>>? protectedVramAfterDrop = null)
    {
        if (placements.Count == 0) throw new ArgumentException("No universal placements were supplied.");
        int[] incomingOrder = Enumerable.Range(0, placements.Count).ToArray();
        if (placements.Any(p => p.PersistentSlot != null))
        {
            if (placements.Any(p => p.PersistentSlot == null || string.IsNullOrWhiteSpace(p.EditorId)) ||
                placements.Select(p => p.EditorId).Distinct(StringComparer.Ordinal).Count() != placements.Count ||
                !placements.Select(p => p.PersistentSlot!.Value).Order().SequenceEqual(Enumerable.Range(0, placements.Count)))
                throw new InvalidDataException("Persistent placement ledger requires unique IDs and contiguous lifetime slots starting at zero.");
            incomingOrder = incomingOrder.OrderBy(i => placements[i].PersistentSlot).ToArray();
            placements = incomingOrder.Select(i => placements[i]).ToArray();
        }
        UniversalLevelData dest = new(destinationData.ToArray());
        byte[] exe = executable.ToArray();
        var bindings = UniversalExeBindings.Read(exe, destination.LevelId);
        UniversalOverlay destOverlay = new(destinationOverlay.ToArray(), bindings);
        uint originalEnd = RuntimeEnd(dest, destOverlay.CopyBuffer);
        var destSwitch = UniversalDispatchAnalysis.FindUpdateSwitch(destOverlay);
        bool DestHandles(int c) => UniversalDispatchAnalysis.Handles(destOverlay, destSwitch, c);
        HashSet<int> destRegistered = [.. dest.Registrations().Select(r => r.ClassId)];
        List<string> warnings = [];

        // 1. Resolve classes and dependency closure (spawned child classes come from the same source level).
        Dictionary<int, (string Source, bool Dependency)> classes = [];
        var originalDestination = source(destination.Key);
        bool originalDestinationAssets = destinationData.AsSpan().SequenceEqual(originalDestination.Data.Bytes) &&
            destinationOverlay.AsSpan().SequenceEqual(originalDestination.Overlay.Bytes);
        bool EquivalentDestination(int c, string sk, out string reason)
        {
            if (originalDestinationAssets || string.Equals(sk, destination.Key, StringComparison.OrdinalIgnoreCase))
                return EquivalentSources(c, destination.Key, sk, out reason);
            var donor = source(sk);
            return UniversalSourceIdentity.Equivalent(dest, destOverlay, donor.Data, donor.Overlay, c, shared, out reason);
        }
        bool EquivalentSources(int c, string a, string b, out string reason)
        {
            if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase)) { reason = "same source level"; return true; }
            var sa = source(a); var sb = source(b);
            return UniversalSourceIdentity.Equivalent(sa.Data, sa.Overlay, sb.Data, sb.Overlay, c, shared, out reason);
        }
        Queue<(int Class, string Source, bool Dependency)> work = new();
        List<UniversalRowTemplate> templates = [];
        Dictionary<(string Source, int Type), UniversalParticleDependency> particleDependencies = [];
        Dictionary<int, UniversalSpecialAsset> specialClasses = [];
        foreach (var p in placements)
        {
            if (p.Retired)
            {
                byte[] inert = new byte[UniversalLevelData.RowStride];
                BinaryPrimitives.WriteUInt16LittleEndian(inert.AsSpan(0x36), InertClass);
                inert[0x53] = 0xFF;
                templates.Add(new UniversalRowTemplate("", -1, inert, [], [], 0));
                continue;
            }
            if (p.SpecialId != null)
            {
                var asset = special?.Invoke(p.SpecialId) ?? throw new InvalidDataException(
                    $"'{p.SpecialId}' needs the original Japanese disc. Select it in the editor settings.");
                byte[] row = asset.RowTemplate.ToArray();
                BinaryPrimitives.WriteUInt16LittleEndian(row.AsSpan(0x36), (ushort)asset.ClassId);
                row[0x53] = 0xFF; // no gem reward
                byte[] props = asset.Behavior == UniversalSpecialBehavior.Sign
                    ? UniversalSpecialBehaviors.EncodeSignText(p.Text ?? [])
                    : new byte[16];
                templates.Add(new UniversalRowTemplate("japan", -1, row, props, [], 0));
                specialClasses[asset.ClassId] = asset;
                continue;
            }
            var s = source(p.SourceLevelKey);
            var t = s.Data.CaptureRow(p.SourceLevelKey, p.SourceRowIndex);
            if (t.DroppedExternalPointers > 0)
                warnings.Add($"{p.SourceLevelKey} row {p.SourceRowIndex}: {t.DroppedExternalPointers} link(s) to other source objects were cleared.");
            templates.Add(t);
            work.Enqueue((t.ClassId, p.SourceLevelKey, false));
        }
        while (work.Count > 0)
        {
            var (c, sk, dep) = work.Dequeue();
            if (c <= 0 || c >= 0x200)
                throw new InvalidDataException($"Class 0x{c:X3} from {sk} is outside the supported placement range.");
            if (classes.TryGetValue(c, out var existing))
            {
                if (!EquivalentSources(c, existing.Source, sk, out string reason))
                    throw new UniversalSourceCollisionException($"Class 0x{c:X3} conflicts between {existing.Source} and {sk}: {reason}. This variant needs a separately remapped class; no source is silently substituted.");
                if (existing.Dependency && !dep) classes[c] = (existing.Source, false);
                continue;
            }
            classes[c] = (sk, dep);
            if ((DestHandles(c) || destRegistered.Contains(c)) &&
                !EquivalentDestination(c, sk, out string nativeReason))
                throw new UniversalSourceCollisionException($"Class 0x{c:X3} from {sk} conflicts with the resident class in {destination.Key}: {nativeReason}. This variant needs a separately remapped class; destination assets are not substituted.");
            var s = source(sk);
            var sw = UniversalDispatchAnalysis.FindUpdateSwitch(s.Overlay);
            if (!UniversalDispatchAnalysis.Handles(s.Overlay, sw, c))
            {
                if (!s.Data.Registrations().Any(r => r.ClassId == c) && !shared.Contains(c))
                    throw new InvalidDataException($"Class 0x{c:X3} from {sk} has neither a model nor an extractable handler; its dependency is unresolved.");
                continue;
            }
            var tr = UniversalDispatchAnalysis.Trace(s.Overlay, sw.TreeStart, c, sw.Seeds, sw.ClassRegister);
            var (closure, _) = UniversalDispatchAnalysis.Closure(s.Overlay, sw.Function, [tr.Stop], new HashSet<uint> { sw.TreeStart });
            var (spawns, particles) = UniversalDispatchAnalysis.Dependencies(s.Overlay, closure);
            foreach (int particle in particles)
            {
                var key = (sk, particle);
                if (!particleDependencies.ContainsKey(key))
                    particleDependencies[key] = UniversalParticleDependencies.Inspect(sk, particle, s.Data, dest,
                        string.Equals(sk, destination.Key, StringComparison.OrdinalIgnoreCase));
            }
            foreach (int k in spawns) if (!classes.ContainsKey(k)) work.Enqueue((k, sk, true));
        }

        foreach (int c in specialClasses.Keys)
            if (classes.ContainsKey(c) || destRegistered.Contains(c) || shared.Contains(c) || DestHandles(c))
                throw new UniversalSourceCollisionException($"Special class 0x{c:X3} collides with a native or imported class in {destination.Key}.");

        foreach (var particle in particleDependencies.Values.Where(p => p.Status == "dependency-unverified"))
            warnings.Add($"Particle 0x{particle.TypeId:X2} from {particle.SourceLevelKey}: {particle.Detail}");

        // 1b. Free the models of native object types the user removed completely (see DroppableClasses).
        var (dropped, inertRows) = DroppableClasses(dest, destOverlay, exe, shared, removedRows,
            classes.Keys.Concat(specialClasses.Keys).ToHashSet());
        int droppedBytes = 0;
        if (dropped.Count > 0)
        {
            foreach (int r in inertRows) dest.SetRowClass(r, InertClass);
            droppedBytes = dest.RemoveModels(dropped);
            foreach (int c in dropped) destRegistered.Remove(c);
        }

        // 2. Models, textures and sounds.
        UniversalVramAllocator baselineVram = new(protectedVram);
        UniversalVramAllocator vram = dropped.Count > 0 && protectedVramAfterDrop != null
            ? new(protectedVramAfterDrop(dropped.ToHashSet())) : baselineVram;
        int reclaimedVram = vram.FreeBytes - baselineVram.FreeBytes;
        if (reclaimedVram < 0) throw new InvalidDataException("Dropped-model ownership scan unexpectedly reduced free texture space.");
        int freeBefore = vram.FreeBytes;
        List<UniversalClassImport> imports = [];
        int modelGrowth = 0;
        var ordered = classes.OrderBy(k => k.Key).Select(k => (Class: k.Key, Source: k.Value.Source, Dependency: k.Value.Dependency)).ToList();
        bool NeedModel(int c, string sk) => !destRegistered.Contains(c) && !shared.Contains(c) && source(sk).Data.Registrations().Any(r => r.ClassId == c);
        var modelJobs = ordered.Where(o => NeedModel(o.Class, o.Source))
            .Select(o => (o.Class, o.Source, o.Dependency, Pkg: source(o.Source).Data.Package(o.Class), Vram: source(o.Source).Data.Vram().ToArray(), Data: source(o.Source).Data)).ToList();
        foreach (var (c, asset) in specialClasses.OrderBy(k => k.Key))
            if (!destRegistered.Contains(c))
                modelJobs.Add((c, "japan", false, asset.Package.ToArray(), asset.SourceVram, asset.SourceData));
        var particleJobs = particleDependencies.Values.Where(p => p.Status == "texture-import-required")
            .GroupBy(p => p.TypeId).Select(g =>
            {
                var first = g.First(); var donor = source(first.SourceLevelKey).Data;
                byte[] tile = UniversalParticleDependencies.Records(donor)[first.TypeId];
                foreach (var other in g.Skip(1))
                {
                    var od = source(other.SourceLevelKey).Data;
                    if (!UniversalParticleDependencies.EquivalentTexture(tile, donor.Vram(), UniversalParticleDependencies.Records(od)[other.TypeId], od.Vram()))
                        throw new InvalidDataException($"Particle dependency 0x{first.TypeId:X2} conflicts between {first.SourceLevelKey} and {other.SourceLevelKey}; particle ID remapping is required.");
                }
                return (Type: first.TypeId, Source: first.SourceLevelKey, Package: UniversalParticleDependencies.TexturePackage(tile), Vram: donor.Vram().ToArray());
            }).ToList();
        var textured = modelJobs.Where(j => BinaryPrimitives.ReadInt32LittleEndian(j.Pkg) >= 0).ToList();
        var texReports = UniversalAssetImport.ImportTexturesBatch(textured.Select(j => (j.Pkg, j.Vram,
            $"class 0x{j.Class:X3} from {j.Source}{(j.Dependency ? ", a helper object it spawns" : "")}"))
            .Concat(particleJobs.Select(j => (j.Package, j.Vram, $"particle 0x{j.Type:X2} from {j.Source}"))).ToList(), dest.Vram(), vram);
        foreach (var particle in particleJobs)
            UniversalParticleDependencies.AppendTexture(dest, particle.Type, particle.Package);
        foreach (var key in particleDependencies.Keys.ToArray())
            if (particleDependencies[key].Status == "texture-import-required")
            {
                particleDependencies[key] = particleDependencies[key] with { Status = "dependency-unverified",
                    Detail = "Missing source texture record/pixels/palette imported; per-ID spawn/update semantics remain unverified." };
                warnings.Add($"Particle 0x{key.Type:X2} from {key.Source}: {particleDependencies[key].Detail}");
            }
        List<(int, byte[])> pendingModels = [];
        foreach (var (c, asset) in specialClasses.OrderBy(k => k.Key))
        {
            var job = modelJobs.FirstOrDefault(j => j.Class == c);
            UniversalTextureReport? tex = null; UniversalSoundReport? snd = null;
            if (job.Pkg != null)
            {
                int ti = textured.FindIndex(j => j.Class == c);
                if (ti >= 0) { tex = texReports[ti]; snd = UniversalAssetImport.ImportSounds(job.Pkg, asset.SourceData, dest); }
                pendingModels.Add((c, job.Pkg)); destRegistered.Add(c);
            }
            imports.Add(new(c, "japan", job.Pkg != null, true, job.Pkg?.Length ?? 0, tex, snd, false));
        }
        foreach (var (c, sk, dep) in ordered)
        {
            var s = source(sk);
            var sourceSwitch = UniversalDispatchAnalysis.FindUpdateSwitch(s.Overlay);
            bool needBehavior = !DestHandles(c) && UniversalDispatchAnalysis.Handles(s.Overlay, sourceSwitch, c);
            var job = modelJobs.FirstOrDefault(j => j.Class == c);
            bool needModel = job.Pkg != null;
            if (!needModel && !destRegistered.Contains(c) && !shared.Contains(c) && !dep)
                warnings.Add($"Class 0x{c:X3} has no model registration in {sk}; it is imported as behavior only.");
            UniversalTextureReport? tex = null; UniversalSoundReport? snd = null; int size = 0;
            if (needModel)
            {
                byte[] pkg = job.Pkg!;
                int ti = textured.FindIndex(j => j.Class == c);
                if (ti >= 0) { tex = texReports[ti]; snd = UniversalAssetImport.ImportSounds(pkg, s.Data, dest); }
                size = pkg.Length;
                pendingModels.Add((c, pkg));
                destRegistered.Add(c);
            }
            imports.Add(new(c, sk, needModel, needBehavior, size, tex, snd, dep));
        }

        if (pendingModels.Count > 0)
        {
            int before = dest.Subfiles()[2].Length;
            dest.AppendModels(pendingModels);
            modelGrowth = dest.Subfiles()[2].Length - before;
        }

        // 3. Placed rows.
        List<(byte[], UniversalRowTemplate)> rows = [];
        for (int i = 0; i < placements.Count; i++)
        {
            var p = placements[i]; var t = templates[i];
            byte[] r = t.Row.ToArray();
            if (p.Retired) { rows.Add((r, t)); continue; }
            BinaryPrimitives.WriteInt32LittleEndian(r.AsSpan(0x0C), p.X);
            BinaryPrimitives.WriteInt32LittleEndian(r.AsSpan(0x10), p.Y);
            BinaryPrimitives.WriteInt32LittleEndian(r.AsSpan(0x14), p.Z);
            if (p.Yaw is int yaw) r[0x46] = (byte)yaw;
            r[0x43] = 0xFF; // detach from the source level's pod/group activation table
            if (p.RowByteOverrides != null)
                foreach (var (o, b) in p.RowByteOverrides)
                {
                    if (o is 0x36 or 0x37 && b != r[o])
                        throw new InvalidDataException("Row overrides cannot change the imported class identity.");
                    if (o is > 3 and < 0x58) r[o] = b;
                }
            rows.Add((r, t));
        }
        // Gem rewards carried by imported rows raise the level's in-game treasure target.
        int treasure = rows.Where(r => r.Item2.SourceLevelKey != "japan").Sum(r => TreasureValue(r.Item1));
        if (treasure != 0)
        {
            int realm = destination.LevelId / 10 - 1, slot = destination.LevelId % 10;
            int o = (int)(TreasureTableAddress - UniversalExeBindings.ExeBase) + (realm * 6 + slot) * 2;
            int total = BinaryPrimitives.ReadUInt16LittleEndian(exe.AsSpan(o)) + treasure;
            if (total > ushort.MaxValue) throw new InvalidDataException("The level's treasure total would overflow.");
            BinaryPrimitives.WriteUInt16LittleEndian(exe.AsSpan(o), (ushort)total);
        }
        if (dest.Scene().RowCount + rows.Count > 256)
            throw new InvalidDataException($"{destination.DisplayName} would exceed the engine's 256 persistent placed-object limit.");
        int sceneBefore = dest.Subfiles()[3].Length;
        var (_, sceneGrowth, newRows) = dest.AppendRows(rows);
        int arena = ArenaSlots(dest);
        if (arena < MinimumDynamicSlots)
            throw new InvalidDataException($"Only {arena} dynamic object slots would remain in {destination.DisplayName}; at least {MinimumDynamicSlots} are required.");

        // 4. Behavior bundle chained onto the destination's own callbacks.
        List<UniversalBehaviorSourceReport> behavior = [];
        byte[] overlay = destinationOverlay.ToArray();
        uint copyBuffer = destOverlay.CopyBuffer;
        var byBehaviorSource = imports.Where(i => i.BehaviorImported && i.SourceLevelKey != "japan").GroupBy(i => i.SourceLevelKey)
            .Select(g => (g.Key, source(g.Key).Overlay, (IReadOnlyList<int>)g.Select(i => i.ClassId).Order().ToList())).ToList();
        var eggClasses = specialClasses.Values.Where(a => a.Behavior == UniversalSpecialBehavior.DragonflyEgg).Select(a => a.ClassId).Order().ToList();
        var signClasses = specialClasses.Values.Where(a => a.Behavior == UniversalSpecialBehavior.Sign).Select(a => a.ClassId).Order().ToList();
        byBehaviorSource = byBehaviorSource.Where(b => b.Key != "japan").ToList();
        if (byBehaviorSource.Count > 0 || eggClasses.Count > 0 || signClasses.Count > 0)
        {
            uint baseAddress = (copyBuffer + 7) & ~7u;
            var bundle = UniversalBehaviorBundleBuilder.Build(destOverlay, byBehaviorSource, baseAddress, eggClasses, signClasses);
            uint newCopy = (uint)((baseAddress + bundle.Code.Length + 15) & ~15);
            int keep = (int)(copyBuffer - UniversalOverlay.Base);
            byte[] ov = new byte[UniversalLevelData.Align((int)(newCopy - UniversalOverlay.Base), 0x800)];
            destinationOverlay.AsSpan(0, keep).CopyTo(ov);
            bundle.Code.CopyTo(ov, (int)(baseAddress - UniversalOverlay.Base));
            overlay = ov;
            UniversalExeBindings.Patch(exe, bindings[UniversalExeBindings.UpdateGlobal], bundle.UpdateWrapper);
            UniversalExeBindings.Patch(exe, bindings[UniversalExeBindings.InitGlobal], bundle.InitWrapper);
            UniversalExeBindings.Patch(exe, bindings[UniversalExeBindings.CopyBufferGlobal], newCopy);
            copyBuffer = newCopy;
            behavior.AddRange(bundle.Sources);
        }

        uint finalEnd = RuntimeEnd(dest, copyBuffer);
        uint limit = NormalBufferFloor - RamSafetyMargin;
        var ram = new UniversalRamBudget(originalEnd, finalEnd, NormalBufferFloor,
            (int)(NormalBufferFloor - originalEnd), (int)((long)NormalBufferFloor - finalEnd),
            (int)(copyBuffer - destOverlay.CopyBuffer), modelGrowth, dest.Subfiles()[3].Length - sceneBefore,
            (int)(finalEnd - originalEnd) - (int)(copyBuffer - destOverlay.CopyBuffer) - modelGrowth - (dest.Subfiles()[3].Length - sceneBefore));
        if (finalEnd > limit)
            throw new UniversalBudgetException(
                $"{destination.DisplayName} does not have enough game RAM for these objects: they need {(finalEnd - originalEnd) / 1024.0:0.0} KB " +
                $"but the level has {Math.Max(0, (int)(limit - originalEnd)) / 1024.0:0.0} KB available.", ram);
        int[] mappedRows = new int[newRows.Count];
        for (int i = 0; i < newRows.Count; i++) mappedRows[incomingOrder[i]] = newRows[i];
        return new(destination.Key, imports, mappedRows, ram, arena, freeBefore - vram.FreeBytes, behavior, warnings,
            treasure, overlay, dest.Bytes, exe) { DroppedModelClasses = dropped.Order().ToList(), DroppedModelBytes = droppedBytes, ReclaimedVramBytes = reclaimedVram, ParticleDependencies = particleDependencies.Values.ToList() };
    }

    /// <summary>Class a fully removed native row is switched to: no model, no behavior in any level, never
    /// spawned. The row keeps its index, so pods, links and the saved collected bits are unchanged.</summary>
    public const ushort InertClass = 0x1FE;

    /// <summary>Locally registered classes whose every placed row was removed by the user and that nothing can
    /// still create: not shared, not imported/placed/special, not spawned by any code in the destination
    /// overlay or the executable, and not named in any remaining row's properties (spawner data).</summary>
    public static (List<int> Classes, List<int> InertRows) DroppableClasses(UniversalLevelData dest, UniversalOverlay destOverlay,
        byte[] exe, IReadOnlySet<int> shared, IReadOnlyCollection<int>? removedRows, IReadOnlySet<int> needed)
    {
        if (removedRows is not { Count: > 0 }) return ([], []);
        var sc = dest.Scene();
        var removed = removedRows.Where(r => r >= 0 && r < sc.RowCount).ToHashSet();
        Dictionary<int, List<int>> rowsByClass = [];
        for (int i = 0; i < sc.RowCount; i++)
        {
            int c = BinaryPrimitives.ReadUInt16LittleEndian(dest.Row(i).AsSpan(0x36));
            if (!rowsByClass.TryGetValue(c, out var l)) rowsByClass[c] = l = [];
            l.Add(i);
        }
        var registered = dest.Registrations().Select(r => r.ClassId).ToHashSet();
        if (registered.Contains(InertClass) || shared.Contains(InertClass) || rowsByClass.ContainsKey(InertClass)) return ([], []);
        var candidates = rowsByClass.Where(k => registered.Contains(k.Key) && !shared.Contains(k.Key) && !needed.Contains(k.Key) &&
            k.Value.All(removed.Contains)).Select(k => k.Key).ToHashSet();
        if (candidates.Count == 0) return ([], []);
        // Spawned by code: the whole destination overlay and the executable (spawns go through the init global).
        SortedSet<uint> all = [];
        for (uint a = destOverlay.CodeStart; a + 4 <= UniversalOverlay.Base + (uint)destOverlay.Bytes.Length; a += 4) all.Add(a);
        candidates.ExceptWith(UniversalDispatchAnalysis.Dependencies(destOverlay, all).SpawnedClasses);
        candidates.ExceptWith(ExecutableSpawnedClasses(exe));
        // Named by a remaining row's properties (generators/spawners keep their data there).
        for (int i = 0; i < sc.RowCount && candidates.Count > 0; i++)
        {
            if (removed.Contains(i)) continue;
            byte[] row = dest.Row(i);
            int props = BinaryPrimitives.ReadInt32LittleEndian(row);
            if (props <= 0) continue;
            int start = dest.Subfiles()[3].Offset + props, end = Math.Min(start + 0x80, dest.Bytes.Length - 2); // scene-relative; blocks are small
            for (int p = start; p < end; p += 2) candidates.Remove(BinaryPrimitives.ReadUInt16LittleEndian(dest.Bytes.AsSpan(p)));
        }
        var inert = candidates.SelectMany(c => rowsByClass[c]).Order().ToList();
        return (candidates.ToList(), inert);
    }

    private static readonly Dictionary<string, HashSet<int>> ExeSpawnCache = [];

    /// <summary>Class constants passed in a0 to calls through the init/spawn global (0x800758CC) in the EXE.</summary>
    public static HashSet<int> ExecutableSpawnedClasses(byte[] exe)
    {
        string key = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(exe));
        lock (ExeSpawnCache) if (ExeSpawnCache.TryGetValue(key, out var hit)) return hit;
        HashSet<int> result = [];
        int n = exe.Length / 4;
        uint W(int i) => BinaryPrimitives.ReadUInt32LittleEndian(exe.AsSpan(4 * i));
        for (int i = 0x800 / 4; i < n; i++)
        {
            uint w = W(i);
            if (!UniversalMips.IsJalr(w)) continue;
            int rs = UniversalMips.Rs(w);
            bool viaInit = false;
            for (int b = i - 1; b > i - 8 && b > 0; b--)
            {
                uint w2 = W(b);
                if (UniversalMips.Op(w2) == 35 && UniversalMips.Rt(w2) == rs)
                {
                    viaInit = UniversalMips.SImm(w2) == unchecked((short)(UniversalExeBindings.InitGlobal & 0xFFFF));
                    break;
                }
            }
            if (!viaInit) continue;
            for (int b = i + 1; b > i - 14 && b > 0; b--)
            {
                uint w3 = W(b);
                if (UniversalMips.Rt(w3) == 4 && UniversalMips.Rs(w3) == 0 && UniversalMips.Op(w3) is 9 or 13) { result.Add(UniversalMips.Imm(w3)); break; }
            }
        }
        lock (ExeSpawnCache) ExeSpawnCache[key] = result;
        return result;
    }
}

public sealed class UniversalBudgetException(string message, UniversalRamBudget budget) : IOException(message)
{
    public UniversalRamBudget Budget { get; } = budget;
}
