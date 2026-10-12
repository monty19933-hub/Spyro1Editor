using System.Buffers.Binary;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.Core.Exporting;

/// <summary>The last level writer: changes the entry surface while retaining each doorway's
/// unique native return token and moved path. All validation happens on private copies.</summary>
public static class NativePortalRouteComposer
{
    public static void Apply(UniversalSourceLibrary donors, LevelCatalog catalog, byte[] executable,
        Dictionary<int, byte[]> replacements, Func<int, byte[]> readEntry, PortalRouteDocument routes,
        bool includesFreshLevel = false,
        IReadOnlyDictionary<int, IReadOnlyList<NativePortalReturnClass>>? returnClasses = null, NativeFreshParticleBinding? freshParticleBinding = null, Action<NativeFreshParticleBinding>? reportFreshParticleBinding = null)
    {
        routes.Validate();
        if (!routes.HasEdits) return;
        if (!includesFreshLevel && (routes.Destinations.Any(d => d.DestinationLevelId == 65) || routes.Returns.Any(r => r.LevelId == 65)))
            throw new InvalidDataException("A portal uses the new level (ID 65), but that level is not included. Save the new level or change its portal route before creating the BIN.");
        byte[] exe = executable.ToArray();
        if (includesFreshLevel)
        {
            var fresh = UniversalExeBindings.Read(exe, 65);
            if(freshParticleBinding!=null)
            {
                byte[] ov=replacements.TryGetValue(79,out var replaced)?replaced:readEntry(79);
                NativePortalRouteFreshBinding.RequireOwnedParticleDestination(donors.Disc.Exe,exe,new(ov,fresh),freshParticleBinding);
            }
            else NativePortalRouteFreshBinding.Rebind(donors.Disc.Exe, exe,
                fresh[UniversalExeBindings.UpdateGlobal].Value, fresh[UniversalExeBindings.CopyBufferGlobal].Value);
        }
        byte[] bindingBaseline = exe.ToArray();
        Dictionary<int, byte[]> changed = [];
        byte[] Read(int entry) => changed.TryGetValue(entry, out var bytes) ? bytes :
            replacements.TryGetValue(entry, out bytes) ? bytes : readEntry(entry);
        byte[] Edit(int entry)
        {
            if (!changed.TryGetValue(entry, out var bytes)) changed[entry] = bytes = Read(entry).ToArray();
            return bytes;
        }
        var endpoints = routes.Destinations.Select(d => d.Portal).Concat(routes.Returns.Select(r => r.Portal)).Distinct().ToArray();
        Dictionary<PortalEndpoint, EndpointOwner> owners = [];
        foreach (var group in endpoints.GroupBy(p => p.HomeworldKey))
        {
            var level = catalog.FindByKey(group.Key) ?? throw new InvalidDataException("A portal homeworld is missing from the level catalog.");
            var original = PortalSourceDataLocator.Locate(donors.ImagePath, level);
            var data = new UniversalLevelData(Edit(level.SourceWadEntry));
            foreach (var endpoint in group)
                owners.Add(endpoint, RequireEndpoint(data, level, original, endpoint,
                    donors.Get(level.Key).Data.CaptureRow(level.Key, endpoint.Resolve().CompanionTrueIndex - original.SourceMobyIndexBias).ClassId));
        }
        foreach (var edit in routes.Destinations)
        {
            var owner = owners[edit.Portal];
            byte[] data = Edit(owner.Level.SourceWadEntry);
            Put(data, owner.SurfaceOffset + 4, edit.DestinationLevelId);
            Put(data, owner.LetteringPropertiesOffset, (edit.DestinationLevelId / 10 - 1) * 6 + edit.DestinationLevelId % 10);
        }
        if (routes.Returns.Count > 0)
        {
            NativePortalRouteRuntime.PatchPauseExit(donors.Disc.Exe, exe);
            // Every unchanged callback must have enough resident code for the tag read,
            // and must not accidentally look like an edited portal wrapper.
            foreach (var level in catalog.Levels.Where(l => PortalRouteDocument.IsLevelId(l.LevelId) && l.LevelId != 65))
            {
                var binding = UniversalExeBindings.Read(exe, level.LevelId);
                var update = binding[UniversalExeBindings.UpdateGlobal];
                var overlay = Read(level.SourceWadEntry - 1);
                int offset = checked((int)(update.Value - UniversalOverlay.Base));
                if (offset < 0 || offset + 20 > overlay.Length || U(overlay, offset + 16) == NativePortalRouteRuntime.Tag)
                    throw new InvalidDataException($"{level.DisplayName}'s existing return callback cannot be identified safely.");
            }
            foreach (var edit in routes.Returns)
            {
                var level = catalog.Levels.SingleOrDefault(l => l.LevelId == edit.LevelId)
                    ?? throw new InvalidDataException($"Return Home level {edit.LevelId} is not included in the export catalog.");
                var endpoint = owners[edit.Portal];
                var bindings = UniversalExeBindings.Read(exe, level.LevelId);
                byte[] overlay = Read(level.SourceWadEntry - 1);
                var result = NativePortalRouteRuntime.AppendReturnWrapper(overlay,
                    bindings[UniversalExeBindings.UpdateGlobal].Value, bindings[UniversalExeBindings.CopyBufferGlobal].Value,
                    endpoint.Level.LevelId, edit.Portal.Resolve().DestinationLevelId);
                var data = new UniversalLevelData(Read(level.SourceWadEntry));
                NativePortalRouteRuntime.PatchReturnHomeCalls(donors, result, bindings, data, level.Key,
                    returnClasses?.GetValueOrDefault(level.LevelId));
                if (!GameRam.Fits(data, result.CopyBuffer))
                    throw new InvalidDataException($"{level.DisplayName} needs {GameRam.Shortfall(data, result.CopyBuffer):N0} more bytes for its Return Home route. Remove unused objects or level assets and try again.");
                if (level.LevelId == 65)
                {
                    if(freshParticleBinding!=null)
                    {
                        var continuation=NativePortalRouteRuntime.ReissueParticleContinuation(donors,overlay,result,freshParticleBinding.Continuation);
                        freshParticleBinding=NativePortalRouteFreshBinding.RebindOwnedParticleContinuation(donors.Disc.Exe,exe,
                            new(result.Overlay,bindings),continuation,result.UpdatePointer,bindings[UniversalExeBindings.InitGlobal].Value,result.CopyBuffer);
                    }
                    else NativePortalRouteFreshBinding.Rebind(donors.Disc.Exe, exe, result.UpdatePointer, result.CopyBuffer);
                }
                else if (includesFreshLevel && level.LevelId == 13)
                {
                    if(freshParticleBinding!=null)freshParticleBinding=NativePortalRouteFreshBinding.RebindOwnedParticleTown(donors.Disc.Exe,exe,
                        new(Read(79),UniversalExeBindings.Read(exe,65)),freshParticleBinding,result.UpdatePointer,result.CopyBuffer);
                    else NativePortalRouteFreshBinding.RebindTown(donors.Disc.Exe, exe, result.UpdatePointer, result.CopyBuffer);
                }
                else
                {
                    UniversalExeBindings.Patch(exe, bindings[UniversalExeBindings.UpdateGlobal], result.UpdatePointer);
                    UniversalExeBindings.Patch(exe, bindings[UniversalExeBindings.CopyBufferGlobal], result.CopyBuffer);
                }
                changed[level.SourceWadEntry - 1] = result.Overlay;
                // Callback instructions may be shared by native initializer tails. A route
                // for one level must never silently redirect another level's resident code.
                foreach (var other in catalog.Levels.Where(l => PortalRouteDocument.IsLevelId(l.LevelId) && l.LevelId != level.LevelId && (l.LevelId != 65 || includesFreshLevel)))
                {
                    var old = UniversalExeBindings.Read(bindingBaseline, other.LevelId);
                    var now = UniversalExeBindings.Read(exe, other.LevelId);
                    if (routes.Returns.Any(r => r.LevelId == other.LevelId)) continue;
                    foreach (uint global in new[] { UniversalExeBindings.UpdateGlobal, UniversalExeBindings.CopyBufferGlobal })
                        if (old[global].Value != now[global].Value)
                            throw new InvalidDataException($"{level.DisplayName} shares a native return binding with {other.DisplayName}; these routes cannot be rebuilt independently.");
                }
            }
            NativePortalRouteRuntime.PatchFlightExit(donors.Disc.Exe, exe);
        }
        if (routes.Destinations.Any(d => d.DestinationLevelId == 65))
        {
            NativePortalRouteFreshBinding.PatchOrdinaryPortalArrival(donors.Disc.Exe, exe);
            const int entryLimit = 0x47910;
            uint source = U(donors.Disc.Exe, entryLimit), current = U(exe, entryLimit);
            if (source != UniversalMips.EncodeI(10, 2, 2, 65) || current != source)
                throw new InvalidDataException("The native portal destination limit conflicts with another executable edit.");
            Put(exe, entryLimit, UniversalMips.EncodeI(10, 2, 2, 66));
        }
        if(freshParticleBinding!=null)reportFreshParticleBinding?.Invoke(freshParticleBinding);
        exe.CopyTo(executable, 0);
        foreach (var (entry, bytes) in changed) replacements[entry] = bytes;
    }

    public static void ApplyImage(UniversalSourceLibrary donors, LevelCatalog catalog, string inputImage,
        string outputImage, PortalRouteDocument routes, bool includesFreshLevel = false,
        IReadOnlyDictionary<int, IReadOnlyList<NativePortalReturnClass>>? returnClasses = null)
    {
        ExportArtifactTransaction.RequireSourceSeparation(donors.ImagePath, null, outputImage);
        ExportArtifactTransaction.RequireSourceSeparation(inputImage, null, outputImage);
        if (File.Exists(outputImage)) throw new IOException("Portal routing requires a new private output image.");
        var baseline = UniversalDiscImage.Read(inputImage);
        byte[] exe = baseline.Exe.ToArray(); Dictionary<int, byte[]> changes = [];
        Apply(donors, catalog, exe, changes, baseline.EntryBytes, routes, includesFreshLevel, returnClasses);
        baseline.WriteImage(outputImage, baseline.RebuildWad(changes), exe);
        var read = UniversalDiscImage.Read(outputImage);
        if (!read.Exe.AsSpan().SequenceEqual(exe)) throw new InvalidDataException("Portal route executable failed independent readback.");
        for (int entry = 0; entry < 256; entry++)
            if (!read.EntryBytes(entry).AsSpan().SequenceEqual(changes.TryGetValue(entry, out var bytes) ? bytes : baseline.EntryBytes(entry)))
                throw new InvalidDataException($"Portal route WAD entry {entry} failed independent readback.");
    }

    private sealed record EndpointOwner(LevelDefinition Level, int SurfaceOffset, int LetteringPropertiesOffset);

    private static EndpointOwner RequireEndpoint(UniversalLevelData data, LevelDefinition level,
        PortalSourceLevelData original, PortalEndpoint endpoint, int companionClass)
    {
        var definition = endpoint.Resolve();
        var source = original.Portals.Single(p => p.SourcePathMobyTrueIndex == endpoint.PathTrueIndex);
        var body = data.Subfiles()[1]; int cursor = body.Offset;
        int Next(int at)
        {
            int size = checked((int)U(data.Bytes, at));
            if (size < 4 || (long)at + size > body.Offset + body.Length) throw Missing();
            return at + size;
        }
        InvalidDataException Missing() => new($"{level.DisplayName}'s {definition.DestinationName} doorway was removed or no longer has its entry and return controls. Restore that doorway or choose another portal route.");
        for (int i = 0; i < 6; i++) cursor = Next(cursor);
        int count = checked((int)U(data.Bytes, cursor)); cursor += 4;
        if (count is < 1 or > 6) throw Missing();
        int match = -1;
        for (int i = 0; i < count; i++)
        {
            int pointCount = checked((int)U(data.Bytes, cursor + 4));
            if (pointCount is < 1 or > 64) throw Missing();
            if (U(data.Bytes, cursor + 0x1C) == definition.DestinationLevelId)
            {
                if (match != -1 || U(data.Bytes, cursor + 0x18) != source.PathMobyIndex) throw Missing();
                match = i;
            }
            cursor = Next(checked(cursor + 0x2C + (pointCount - 1) * 12 + 20));
        }
        if (match < 0) throw Missing();
        var scene = data.Scene(); int sceneAt = data.Subfiles()[3].Offset;
        int Props(int physical, int requiredClass, int minimumLength)
        {
            if (physical < 0 || physical >= scene.RowCount) throw Missing();
            int row = sceneAt + scene.RowsOffset + physical * UniversalLevelData.RowStride;
            if (BinaryPrimitives.ReadUInt16LittleEndian(data.Bytes.AsSpan(row + 0x36)) != requiredClass) throw Missing();
            int props = checked((int)U(data.Bytes, row));
            if (props < scene.PropertiesComponent + 4 || props + minimumLength > scene.PropertiesEnd ||
                !scene.Fixups.Contains(scene.RowsOffset + physical * UniversalLevelData.RowStride)) throw Missing();
            return props;
        }
        int pathProps = Props(source.PathMobyIndex, 0x18E, 0x1C);
        int path = checked((int)U(data.Bytes, sceneAt + pathProps));
        if (path < scene.PropertiesComponent + 4 || path + 40 > scene.PropertiesEnd ||
            !scene.Fixups.Contains(pathProps) || data.Bytes[sceneAt + path] != 2) throw Missing();
        int lettering = Props(definition.LetteringTrueIndex - original.SourceMobyIndexBias, 1, 4);
        _ = Props(definition.CompanionTrueIndex - original.SourceMobyIndexBias, companionClass, 4);
        var surfaces = PortalSourceDataLocator.ParseTerrainSurfaces(data.Bytes.AsSpan(body.Offset, body.Length).ToArray(), body.Offset, level.Key, level.DisplayName);
        var owned = surfaces.SpecialSurfaces.Where(s => s.Type == 6 && s.PortalIndex == match).ToArray();
        if (owned.Length != 1 || !surfaces.CollisionSurfaceTriangles.Any(t => t.SurfaceType == 6 && t.Param2 == match)) throw Missing();
        if (owned[0].DestinationLevelId != definition.DestinationLevelId) throw new InvalidDataException("Portal routing must run once, after the ordinary level writers.");
        return new(level, checked((int)owned[0].WadOffset), sceneAt + lettering);
    }

    private static uint U(byte[] bytes, int at) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at, 4));
    private static void Put(byte[] bytes, int at, int value) => BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(at, 4), value);
    private static void Put(byte[] bytes, int at, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at, 4), value);
}
