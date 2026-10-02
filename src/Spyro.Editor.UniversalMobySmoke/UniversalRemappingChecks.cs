using System.Buffers.Binary;
using System.Text.Json;
using Spyro.Editor.Core.Universal;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Analysis;

internal static class UniversalRemappingChecks
{
    public static void Run(Action<bool, string> check)
    {
        uint b = UniversalOverlay.Base;
        uint[] code = [UniversalMips.EncodeI(15, 0, 8, 0x8007), UniversalMips.EncodeI(35, 8, 8, 0x58CC),
            UniversalMips.EncodeI(9, 0, 4, 0xD4), 0x0100F809, 0, UniversalMips.JrRa, 0];
        byte[] bytes = new byte[code.Length * 4];
        for (int i = 0; i < code.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4 * i), code[i]);
        var bindings = new Dictionary<uint, UniversalExeBinding>();
        foreach (uint global in new[] { UniversalExeBindings.UpdateGlobal, UniversalExeBindings.InitGlobal, UniversalExeBindings.CopyBufferGlobal })
            bindings[global] = new(global, 0, 0, global == UniversalExeBindings.CopyBufferGlobal ? b + (uint)bytes.Length : b);
        var o = new UniversalOverlay(bytes, bindings);
        var instructions = Enumerable.Range(0, code.Length).Select(i => b + (uint)(4 * i)).ToHashSet();
        var spawns = UniversalClassRemapping.ConstantSpawns(o, instructions);
        check(spawns.Count == 1 && spawns[0].ClassId == 0xD4 && spawns[0].ConstantPc == b + 8 && spawns[0].CallPc == b + 12,
            "spawn remapping owns the exact constant a0 assignment consumed by the native init global");
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 0x02002021); // a0 <- dynamic s0
        bool dynamic = false;
        try { UniversalClassRemapping.ConstantSpawns(o, instructions); } catch (InvalidDataException) { dynamic = true; }
        check(dynamic, "unproved dynamic child-spawn class arguments cannot be silently remapped");
        ClassificationOwnership(check);
        SimplePackageBounds(check);
        RowLinkReadback(check);
        TerrainHeaderBounds(check);
        uint tight = UniversalMobyComposer.NormalBufferFloor - 700;
        check(UniversalMobyComposer.PreservesNativeRemovalBudget(1, 0, 0, tight, tight, tight),
            "delete-only with unchanged verified native resident end preserves an original sub-margin budget");
        check(UniversalMobyComposer.PreservesNativeRemovalBudget(1, 0, 0, tight, tight, tight - 2048),
            "delete-only with reclaimed resident bytes preserves the verified original budget");
        check(!UniversalMobyComposer.PreservesNativeRemovalBudget(1, 0, 336, tight, tight, tight + 336) &&
            !UniversalMobyComposer.PreservesNativeRemovalBudget(1, 1, 0, tight, tight, tight) &&
            !UniversalMobyComposer.PreservesNativeRemovalBudget(0, 0, 0, tight, tight, tight) &&
            !UniversalMobyComposer.PreservesNativeRemovalBudget(1, 0, 0, tight + 1, tight, tight),
            "mapped holes, new actors, unchanged scenes and unverified enlarged baselines retain the full RAM guard");
    }

    private static void TerrainHeaderBounds(Action<bool, string> check)
    {
        var read = typeof(NativeTexturePageOwnershipScanner).GetMethod("TryReadTerrainHeader",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        (bool Valid, int Count, int HighOffset) Probe(uint length, uint count, int available)
        {
            byte[] bytes = new byte[available];
            if (available >= 4) BinaryPrimitives.WriteUInt32LittleEndian(bytes, length);
            if (available >= 8) BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), count);
            object?[] args = [bytes, 0, 0, null];
            bool valid = (bool)read.Invoke(null, args)!;
            return (valid, (int)args[1]!, (int)args[2]!);
        }
        check(Probe(8, 0, 8) == (true, 0, 8), "exact empty authored terrain component closes zero terrain descriptors");
        check(Probe(8, 0, 24) == (true, 0, 8), "empty terrain component allows separately bounded following components");
        check(!Probe(8, 0, 7).Valid, "truncated empty terrain header is refused");
        check(!Probe(0, 0, 8).Valid && !Probe(4, 0, 8).Valid && !Probe(12, 0, 12).Valid,
            "zero texture count only admits the exact eight-byte terrain component");
        check(!Probe(8, uint.MaxValue, 8).Valid, "negative signed terrain count cannot enter empty-component ownership");
        check(Probe(192, 1, 192) == (true, 1, 24), "one complete positive-count native LQ/HQ record remains accepted");
        check(!Probe(8, 1, 192).Valid && !Probe(192, 1, 191).Valid,
            "positive counts still require complete matching component length and available bytes");
        check(!Probe(192, int.MaxValue, 192).Valid, "overflowing positive terrain record count remains refused");
    }

    private static void ClassificationOwnership(Action<bool, string> check)
    {
        uint b = UniversalOverlay.Base;
        UniversalOverlay Overlay(uint[] code)
        {
            byte[] bytes = new byte[code.Length * 4];
            for (int i = 0; i < code.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i * 4), code[i]);
            return new(bytes, new Dictionary<uint, UniversalExeBinding> {
                [UniversalExeBindings.UpdateGlobal] = new(UniversalExeBindings.UpdateGlobal, 0, 0, b),
                [UniversalExeBindings.InitGlobal] = new(UniversalExeBindings.InitGlobal, 0, 0, b),
                [UniversalExeBindings.CopyBufferGlobal] = new(UniversalExeBindings.CopyBufferGlobal, 0, 0, b + (uint)bytes.Length) });
        }
        uint[] model = [UniversalMips.EncodeI(33, 19, 2, 0x36), 0, 0x00021080u,
            UniversalMips.EncodeI(15, 0, 1, 0x8007), 0x00220821u, UniversalMips.EncodeI(35, 1, 2, 0x6378)];
        check(UniversalClassRemapping.FeedsModelTable(Overlay(model), b), "native model-table class lookup retains the mapped destination ID");
        model[^1] = UniversalMips.EncodeI(35, 1, 2, 0x637C);
        check(!UniversalClassRemapping.FeedsModelTable(Overlay(model), b), "a different global table has no class-model ownership");
        model[^1] = UniversalMips.EncodeI(35, 1, 2, 0x6378); model[4] = UniversalMips.EncodeI(9, 0, 1, 0);
        check(!UniversalClassRemapping.FeedsModelTable(Overlay(model), b), "overwritten model-table address provenance is refused");
        uint[] classify = [UniversalMips.EncodeI(9, 0, 2, 14), UniversalMips.EncodeI(33, 19, 3, 0x36), 0,
            UniversalMips.EncodeI(5, 3, 2, 3), 0, UniversalMips.EncodeI(9, 3, 3, -14), 0,
            UniversalMips.EncodeI(9, 0, 3, 0), UniversalMips.JrRa, 0];
        var insns = Enumerable.Range(0, classify.Length).Select(i => b + (uint)(i * 4)).ToHashSet();
        check(UniversalClassRemapping.ComparisonOnly(Overlay(classify), insns, b + 8, 3, [14, 15]),
            "source-class classification preserves a proved literal defined before the class read");
        classify[7] = UniversalMips.EncodeI(43, 19, 3, 0x10);
        check(!UniversalClassRemapping.ComparisonOnly(Overlay(classify), insns, b + 8, 3, [14, 15]),
            "a class comparison that leaks the class into a stored field cannot be translated");
        classify[7] = UniversalMips.EncodeI(9, 0, 3, 0); classify[0] = UniversalMips.EncodeJ(2, b + 4);
        check(!UniversalClassRemapping.ComparisonOnly(Overlay(classify), insns, b + 8, 3, [14, 15]),
            "an alternative entry cannot inherit an unproved earlier comparison constant");
    }

    private static void SimplePackageBounds(Action<bool, string> check)
    {
        byte[] pkg = new byte[40]; pkg[0] = 1; pkg[1] = 1; pkg[3] = 0x80;
        BinaryPrimitives.WriteInt32LittleEndian(pkg.AsSpan(4), 16);
        BinaryPrimitives.WriteInt32LittleEndian(pkg.AsSpan(8), 20);
        BinaryPrimitives.WriteInt32LittleEndian(pkg.AsSpan(12), 24);
        pkg[24] = 3; // flat RGB command, not indexed RGB
        BinaryPrimitives.WriteUInt32LittleEndian(pkg.AsSpan(28), 0xFFFFFFFF);
        check(UniversalSharedModelAssets.CountDerivedSimplePackage(pkg).AsSpan().SequenceEqual(pkg),
            "flat simple-model faces preserve immediate color words and the discarded read-ahead window");
        bool truncated = false;
        try { UniversalSharedModelAssets.CountDerivedSimplePackage(pkg.AsSpan(0, 36)); } catch (InvalidDataException) { truncated = true; }
        check(truncated, "shared alias requires ownership of its complete pipeline read-ahead window");
        pkg[24] = 0;
        bool badColor = false;
        try { UniversalSharedModelAssets.CountDerivedSimplePackage(pkg); } catch (InvalidDataException) { badColor = true; }
        check(badColor, "indexed simple-model faces cannot reference undeclared RGB entries");
        byte[] normal = new byte[52]; normal[0] = 2; normal[1] = 1; normal[3] = 0x80;
        BinaryPrimitives.WriteInt32LittleEndian(normal.AsSpan(4), 24);
        BinaryPrimitives.WriteInt32LittleEndian(normal.AsSpan(8), 32);
        BinaryPrimitives.WriteInt32LittleEndian(normal.AsSpan(12), 36);
        normal[16] = 127; normal[19] = 128;
        uint indices = (1u << 16) | (1u << 2);
        BinaryPrimitives.WriteUInt32LittleEndian(normal.AsSpan(36), indices);
        BinaryPrimitives.WriteUInt32LittleEndian(normal.AsSpan(40), indices);
        check(UniversalSharedModelAssets.CountDerivedSimplePackage(normal).AsSpan().SequenceEqual(normal),
            "normal-prefix faces own per-vertex generated colors despite a one-word root RGB table");
        BinaryPrimitives.WriteUInt32LittleEndian(normal.AsSpan(40), 2u << 16);
        bool badNormalIndex = false;
        try { UniversalSharedModelAssets.CountDerivedSimplePackage(normal); } catch (InvalidDataException) { badNormalIndex = true; }
        check(badNormalIndex, "normal-prefix color indices cannot escape the generated vertex-count color owner");
        BinaryPrimitives.WriteInt32LittleEndian(normal.AsSpan(4), 28);
        bool badPrefix = false;
        try { UniversalSharedModelAssets.CountDerivedSimplePackage(normal); } catch (InvalidDataException) { badPrefix = true; }
        check(badPrefix, "a guessed or shifted normal prefix cannot be admitted as an alias package");
    }

    private static void RowLinkReadback(Action<bool, string> check)
    {
        const int so = 0x2000;
        byte[] bytes = new byte[so + 0x800];
        for (int i = 0; i < 4; i++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(i * 8), i == 3 ? so : 0x800 + i * 0x800);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(i * 8 + 4), 0x800);
        }
        int p = so + 0x88;
        for (int i = 0; i < 7; i++, p += 4) BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(p), 4);
        int mob = p; BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(p), 0x200); p += 0x200;
        int props = p; BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(p), 4); p += 4;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(p), 4); p += 4;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(p), 4); p += 4;
        var data = new UniversalLevelData(bytes);
        byte[] row = new byte[UniversalLevelData.RowStride];
        var t = new UniversalRowTemplate("source", 0, row, new byte[4], [], 0)
            { ExternalRowPointers = [(0, 1, 0x0C)], ImportedRowPointers = [(0, 1, 0x0C)] };
        var groupData = new UniversalLevelData(bytes.ToArray());
        var a = t with { ImportedRowPointers = [], DroppedExternalPointers = 1, LinkGroupId = "first-copy" };
        var b = a with { LinkGroupId = "second-copy" };
        var target = new UniversalRowTemplate("SOURCE", 1, row, new byte[4], [], 0) { LinkGroupId = "first-copy" };
        var grouped = new[] { a, target, b, target with { LinkGroupId = "second-copy" } };
        grouped[0] = UniversalObjectRowLinks.Resolve(grouped[0], grouped);
        grouped[2] = UniversalObjectRowLinks.Resolve(grouped[2], grouped);
        groupData.AppendRows(grouped.Select(template => (row, template)).ToArray());
        int Pointer(int owner) => BinaryPrimitives.ReadInt32LittleEndian(groupData.Bytes.AsSpan(so + BinaryPrimitives.ReadInt32LittleEndian(groupData.Row(owner))));
        check(Pointer(0) == groupData.Scene().RowsOffset + UniversalLevelData.RowStride + 12 &&
            Pointer(2) == groupData.Scene().RowsOffset + 3 * UniversalLevelData.RowStride + 12,
            "two copies retain independent native pointers after physical allocation");
        var missing = UniversalObjectRowLinks.Resolve(a, [a, grouped[3]]);
        check(missing.ImportedRowPointers.Count == 0 && missing.DroppedExternalPointers == 1,
            "a target from another group cannot silently replace a missing companion");
        var ambiguous = UniversalObjectRowLinks.Resolve(a, [a, target, target]);
        check(ambiguous.ImportedRowPointers.Count == 0 && ambiguous.DroppedExternalPointers == 1,
            "duplicate source rows inside one group remain ambiguous");
        var legacy = UniversalObjectRowLinks.Resolve(a with { LinkGroupId = null }, [target with { LinkGroupId = "" }]);
        check(legacy.ImportedRowPointers.Count == 1, "legacy ungrouped pointers still resolve uniquely");
        bool crossGroup = false;
        try { UniversalObjectRowLinks.RequireTarget(grouped[0], grouped, 0, 3, 12); }
        catch (InvalidDataException) { crossGroup = true; }
        check(crossGroup, "a forged cross-group physical pointer is rejected by the final writer");
        data.AppendRows([(row, t), (row, new("source", 1, row, new byte[4], [], 0))]);
        var scene = data.Scene();
        int link = BinaryPrimitives.ReadInt32LittleEndian(data.Bytes.AsSpan(props + 4));
        check(link == scene.RowsOffset + UniversalLevelData.RowStride + 0x0C && scene.Fixups.Contains(props - so + 4),
            "restored source-object links target the appended counterpart and carry a native scene fixup");
        byte[] before = data.Bytes.ToArray();
        bool linkedRemoval = false;
        try { UniversalRowRemoval.RemoveOwnedPropertiesAndClearRows(data, [1]); }
        catch (InvalidDataException) { linkedRemoval = true; }
        check(linkedRemoval && data.Bytes.AsSpan().SequenceEqual(before),
            "a surviving pointer to a deleted actor refuses removal before mutation instead of binding a replacement");
        byte[] survivor = data.Row(1);
        int freed = UniversalRowRemoval.RemoveOwnedPropertiesAndClearRows(data, [0]);
        var (_, _, reused) = data.AppendRows([(row, new("source", 2, row, new byte[4], [], 0))], [0]);
        check(freed == 4 && reused.SequenceEqual([0]) && data.Scene().RowCount == 2,
            "permanent deletion frees owned properties and reuses the physical hole before extending the native rows");
        check(data.Row(1).AsSpan(4).SequenceEqual(survivor.AsSpan(4)) &&
            BinaryPrimitives.ReadInt32LittleEndian(data.Row(1)) == BinaryPrimitives.ReadInt32LittleEndian(survivor) - 4,
            "native survivors retain their physical index and every nonrelocated byte");
        for (int iteration = 0; iteration < 12; iteration++)
        {
            UniversalRowRemoval.RemoveOwnedPropertiesAndClearRows(data, [0]);
            var (_, _, again) = data.AppendRows([(row, new("source", iteration, row, new byte[4], [], 0))], [0]);
            if (!again.SequenceEqual([0]) || data.Scene().RowCount != 2) throw new InvalidDataException("Repeated delete/add grew physical actor capacity.");
        }
        check(data.Scene().RowCount == 2 && data.Scene().PropertiesSize == 12,
            "repeated delete/add round trips do not accumulate actor rows or private property bytes");
        UniversalRowRemoval.RemoveOwnedPropertiesAndClearRows(data, [0, 1]);
        data.AppendRows([], [0, 1]);
        check(data.Scene().RowCount == 0 && data.Scene().PropertiesSize == 4 && data.Scene().Fixups.Count == 0,
            "delete-only trailing removal trims every actor row and releases its owned properties/fixups");
    }

    public static int DeferredHooks(string original, string output, LevelCatalog catalog, string japanesePath)
    {
        var donors = new UniversalSourceLibrary(original, catalog); var jp = new UniversalJapaneseSource(japanesePath);
        var level = catalog.FindByKey("toasty")!; int nativeRows = donors.Get(level.Key).Data.Scene().RowCount;
        var placements = new Dictionary<string, IReadOnlyList<UniversalMobyPlacement>> { [level.Key] = [
            new("", -1, 130000, 115000, 16200, SpecialId: "dragonfly-egg-pink"),
            new("", -1, 131000, 115000, 16200, SpecialId: UniversalDragonflyEggs.SignId, Text: ["DEFERRED TEST"]) ] };
        var stage = UniversalMobyExport.Apply(donors, original, output, catalog, placements, japanese: jp, deferSpecialHooks: true);
        if (!stage.SpecialHooksDeferred || stage.ActiveDragonflyEggs.Count != 1 || stage.SignLevelKeys.Count != 1 || !stage.SignLevelKeys.Contains(level.Key) ||
            stage.ActiveDragonflyEggs[0] != (UniversalMobyExport.CollectedMaskIndex(level), nativeRows, UniversalDragonflyEggs.VariantIndex("dragonfly-egg-pink")))
            throw new InvalidDataException("Deferred stage manifests do not carry the exact active native binding.");
        var read = UniversalDiscImage.Read(output);
        foreach (var (address, length) in new (uint, int)[] {
            (UniversalSpecialBehaviors.CaveStart, (int)(UniversalSpecialBehaviors.CaveEnd - UniversalSpecialBehaviors.CaveStart)),
            (UniversalSpecialBehaviors.EggInventoryHost, (int)(UniversalSpecialBehaviors.EggInventoryHostEnd - UniversalSpecialBehaviors.EggInventoryHost)),
            (UniversalSpecialBehaviors.SignHookCallSite, 4), (UniversalSpecialBehaviors.EggInventoryCallSite, 4) })
        {
            int offset = (int)(address - UniversalExeBindings.ExeBase);
            if (!donors.Disc.Exe.AsSpan(offset, length).SequenceEqual(read.Exe.AsSpan(offset, length)))
                throw new InvalidDataException("An incomplete aggregate stage installed a shared special hook.");
        }
        foreach (var other in catalog.Levels.Where(l => l.SourceWadEntry > 0 && l.Key != level.Key))
            if (!donors.Disc.EntryBytes(other.SourceWadEntry - 1).AsSpan().SequenceEqual(read.EntryBytes(other.SourceWadEntry - 1)))
                throw new InvalidDataException("A deferred stage changed another overlay's special unlock gates.");
        Console.WriteLine(JsonSerializer.Serialize(new { StageStatus = "deferred-internal-incomplete", ActiveEgg = new {
            stage.ActiveDragonflyEggs[0].LevelIndex, stage.ActiveDragonflyEggs[0].Row, stage.ActiveDragonflyEggs[0].Colour },
            SignLevelKeys = stage.SignLevelKeys, SharedHooks = "original-unmodified", OtherOverlayGates = "original-unmodified",
            RuntimeStatus = "not-tested", stage.OutputImageSha256, ImplementationModuleVersionId = typeof(UniversalMobyExport).Module.ModuleVersionId.ToString() },
            new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("UNIVERSAL_DEFERRED_HOOK_STAGE_READBACK_OK"); return 0;
    }

    public static int Readback(string original, string output, LevelCatalog catalog, string destinationKey, string sourceKey, int sourceRow, int? destinationRow = null, int? dependencyClass = null, int? dependencyDestinationClass = null)
    {
        var lib = new UniversalSourceLibrary(original, catalog);
        var destination = catalog.FindByKey(destinationKey)!;
        var originalDestination = lib.Get(destinationKey);
        var donor = lib.Get(sourceKey).Data;
        var image = UniversalDiscImage.Read(output);
        var data = new UniversalLevelData(image.EntryBytes(destination.SourceWadEntry));
        int sourceRowClass = BinaryPrimitives.ReadUInt16LittleEndian(donor.Row(sourceRow).AsSpan(0x36));
        int destinationRowClass = BinaryPrimitives.ReadUInt16LittleEndian(data.Row(destinationRow ?? originalDestination.Data.Scene().RowCount).AsSpan(0x36));
        int sourceClass = dependencyClass ?? sourceRowClass;
        int mapped = dependencyDestinationClass ?? destinationRowClass;
        if (sourceClass == mapped) throw new InvalidDataException("Expected a concrete source-class collision remap.");
        bool hasModel = donor.Registrations().Any(r => r.ClassId == sourceClass) || lib.SharedClasses.Contains(sourceClass);
        if (hasModel && !data.Registrations().Any(r => r.ClassId == mapped)) throw new InvalidDataException("Mapped registration absent.");
        foreach (var native in originalDestination.Data.Registrations())
        {
            byte[] pkg = originalDestination.Data.Package(native.ClassId), after = data.Package(native.ClassId);
            if (!pkg.AsSpan().SequenceEqual(after)) throw new InvalidDataException($"Native model 0x{native.ClassId:X3} was changed by remapping.");
            foreach (int f in UniversalAssetImport.TextureDescriptorOffsets(pkg))
                ValidateTexture(pkg.AsSpan(f, 12), originalDestination.Data.Vram(), after.AsSpan(f, 12), data.Vram());
        }
        if (!lib.Disc.EntryBytes(8).AsSpan().SequenceEqual(image.EntryBytes(8))) throw new InvalidDataException("Resident PETE shared models changed.");
        for (int r = 0; r < originalDestination.Data.Scene().RowCount; r++)
            if (!originalDestination.Data.Row(r).AsSpan().SequenceEqual(data.Row(r))) throw new InvalidDataException("Native row changed.");
        byte[] expected = !hasModel ? [] : donor.Registrations().Any(r => r.ClassId == sourceClass) ? donor.Package(sourceClass) : lib.SharedModelPackage(sourceClass), actual = !hasModel ? [] : data.Package(mapped);
        IReadOnlyList<int> descriptors = hasModel ? UniversalAssetImport.TextureDescriptorOffsets(expected) : [];
        byte[] geometry = expected.ToArray(), outputGeometry = actual.AsSpan(0, expected.Length).ToArray();
        foreach (int f in descriptors)
        {
            ValidateTexture(expected.AsSpan(f, 12), donor.Vram(), actual.AsSpan(f, 12), data.Vram());
            Array.Clear(geometry, f, 12); Array.Clear(outputGeometry, f, 12);
        }
        if (expected.Length > 0 && BinaryPrimitives.ReadInt32LittleEndian(expected) > 0)
        {
            var sourceSounds = SoundBank(donor); var targetSounds = SoundBank(data);
            for (int i = 4; i < 20; i++)
            {
                if (expected[i] == 0xFF) { if (actual[i] != 0xFF) throw new InvalidDataException("Unused sound slot changed."); continue; }
                var a = sourceSounds.Descriptors[expected[i]]; var z = targetSounds.Descriptors[actual[i]];
                if (a.Sample.Sha256 != z.Sample.Sha256 || !a.CopyDescriptor().AsSpan(4).SequenceEqual(z.CopyDescriptor().AsSpan(4)))
                    throw new InvalidDataException("Mapped model sound changed.");
            }
            Array.Clear(geometry, 4, 16); Array.Clear(outputGeometry, 4, 16);
        }
        if (!geometry.AsSpan().SequenceEqual(outputGeometry)) throw new InvalidDataException("Source model geometry changed.");
        byte[] overlay = image.EntryBytes(destination.SourceWadEntry - 1);
        uint[] words = Enumerable.Range(0, overlay.Length / 4).Select(i => BinaryPrimitives.ReadUInt32LittleEndian(overlay.AsSpan(i * 4))).ToArray();
        if (!words.Contains(UniversalMips.EncodeI(9, 0, 1, mapped)) || !words.Contains(UniversalMips.EncodeI(9, 0, 25, mapped)) ||
            !words.Contains(UniversalMips.EncodeI(41, 19, 25, 0x36))) throw new InvalidDataException("Mapped dispatch/constructor missing.");
        if (dependencyClass != null && (!words.Contains(UniversalMips.EncodeI(9, 0, 4, mapped)) && !words.Contains(UniversalMips.EncodeI(13, 0, 4, mapped))))
            throw new InvalidDataException("Mapped child-spawn operand missing.");
        int nativeOverlay = (int)(originalDestination.Overlay.CopyBuffer - UniversalOverlay.Base);
        if (!originalDestination.Overlay.Bytes[..nativeOverlay].SequenceEqual(overlay.AsSpan(0, nativeOverlay))) throw new InvalidDataException("Native overlay code changed.");
        Console.WriteLine(JsonSerializer.Serialize(new { Destination = destinationKey, Source = sourceKey, SourceRow = sourceRow,
            SourceRowClassId = sourceRowClass, DestinationRowClassId = destinationRowClass,
            SourceClassId = sourceClass, DestinationClassId = mapped, ChildSpawnOperand = dependencyClass == null ? "not-applicable" : "mapped", ModelGeometry = hasModel ? "source-exact" : "behavior-only-no-model", Textures = descriptors.Count,
            NativeModelsAndRows = "unchanged", NativeOverlay = "unchanged", DispatchAndConstructor = "mapped", RuntimeStatus = "not-tested" }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("UNIVERSAL_REMAP_EXPORTED_READBACK_OK"); return 0;
    }

    private static NativeSoundBank SoundBank(UniversalLevelData data)
    {
        int p = data.U32(8);
        for (int i = 0; i < 6; i++) p += data.U32(p);
        int portals = data.U32(p); p += 4;
        for (int i = 0; i < portals; i++) { p += 0x34 + data.U32(p + 4) * 12; p += data.U32(p); }
        p += data.U32(p);
        int length = data.U32(p), bank = data.U32(0) + 0x80000;
        return NativeSoundBankCodec.Decode(data.Bytes.AsSpan(p, length), data.Bytes.AsSpan(bank, data.U32(4) - 0x80000));
    }

    private static void ValidateTexture(ReadOnlySpan<byte> a, ReadOnlySpan<byte> av, ReadOnlySpan<byte> b, ReadOnlySpan<byte> bv)
    {
        int ap = BinaryPrimitives.ReadUInt16LittleEndian(a[6..]), bp = BinaryPrimitives.ReadUInt16LittleEndian(b[6..]);
        int depth = (ap >> 7) & 3;
        if (depth != ((bp >> 7) & 3) || (ap & 0x60) != (bp & 0x60)) throw new InvalidDataException("Texture flags changed.");
        int du = b[0] - a[0], dv = b[1] - a[1], minU = 255, maxU = 0, minV = 255, maxV = 0;
        foreach (int f in new[] { 0, 4, 8, 10 })
        {
            if (b[f] - a[f] != du || b[f + 1] - a[f + 1] != dv) throw new InvalidDataException("Texture shape changed.");
            minU = Math.Min(minU, a[f]); maxU = Math.Max(maxU, a[f]); minV = Math.Min(minV, a[f + 1]); maxV = Math.Max(maxV, a[f + 1]);
        }
        int Pixel(ReadOnlySpan<byte> vram, int page, int u, int v)
        {
            int offset = (((page & 16) != 0 ? 256 : 0) + v) * 1024 + (page & 15) * 128 - 1024 + (depth == 0 ? u / 2 : u << (depth - 1));
            return depth == 0 ? (vram[offset] >> ((u & 1) * 4)) & 15 : depth == 1 ? vram[offset] : BinaryPrimitives.ReadUInt16LittleEndian(vram[offset..]);
        }
        for (int y = minV; y <= maxV; y++) for (int x = minU; x <= maxU; x++)
            if (Pixel(av, ap, x, y) != Pixel(bv, bp, x + du, y + dv)) throw new InvalidDataException("Texture pixels changed.");
        if (depth < 2)
        {
            int ac = BinaryPrimitives.ReadUInt16LittleEndian(a[2..]), bc = BinaryPrimitives.ReadUInt16LittleEndian(b[2..]);
            int Offset(int c) => (c >> 6) * 1024 + (c & 63) * 32 - 1024;
            if (!av.Slice(Offset(ac), depth == 0 ? 32 : 512).SequenceEqual(bv.Slice(Offset(bc), depth == 0 ? 32 : 512))) throw new InvalidDataException("Texture palette changed.");
        }
    }
}
