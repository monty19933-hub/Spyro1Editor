using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Exporting;

internal sealed record NativeGreenWizardPropertiesCandidateRequest(
    string DonorLevelKey, int DonorTrueIndex, string TargetLevelKey, IReadOnlyList<int> TargetTrueIndices);

internal sealed record NativeGreenWizardCandidateLevelBinding(
    string LevelKey, int DataEntry, long DataWadOffset, int DataByteLength, string DataSha256,
    int SceneDataOffset, int SceneByteLength, string SceneSha256,
    int OverlayEntry, string OverlaySha256, uint WizardActorRoot, uint LightningActorRoot,
    int SourceTableSceneOffset, int SourceRecordCount, int NativeWizardTrueIndex,
    int PropertiesComponentOffset, int PropertiesComponentByteLength, int FixupOffset, int FixupCount);

internal sealed class NativeGreenWizardCandidateLevelSnapshot(NativeGreenWizardCandidateLevelBinding binding, byte[] scene)
{
    private readonly byte[] _scene = scene.ToArray();
    public NativeGreenWizardCandidateLevelBinding Binding { get; } = binding;
    public byte[] CopyScene() => _scene.ToArray();
}

/// <summary>
/// A complete original-retail source certificate. Private construction prevents
/// UI/JSON from asserting source identity or native runtime/resource residency.
/// Scene and resource snapshots cannot be rebound to another disc by a caller.
/// </summary>
internal sealed class NativeGreenWizardCandidateSource
{
    public const string OriginalImageSha256 = "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37";
    public const long OriginalImageByteLength = 661_547_040;
    private readonly ReadOnlyDictionary<string, NativeGreenWizardCandidateLevelSnapshot> _levels;
    private NativeGreenWizardCandidateSource(string sourceImagePath, Dictionary<string, NativeGreenWizardCandidateLevelSnapshot> levels)
    { SourceImagePath = sourceImagePath; _levels = new(levels); }
    public string SourceImagePath { get; }
    public string SourceImageSha256 => OriginalImageSha256;
    public NativeGreenWizardCandidateLevelSnapshot GetLevel(string key) => key != null && _levels.TryGetValue(key, out var level)
        ? level : throw new InvalidDataException("This source-bound candidate has no verified native runtime for the requested level.");

    public static NativeGreenWizardCandidateSource Inspect(string sourceImagePath)
    {
        string path = Path.GetFullPath(sourceImagePath);
        using FileStream image = File.OpenRead(path);
        if (image.Length != OriginalImageByteLength || HashStream(image) != OriginalImageSha256)
            throw new InvalidDataException("Native Wizard candidate source is not the exact original USA disc image.");
        DiscLayout layout = DiscImage.DetectLayout(path);
        Dictionary<string, NativeGreenWizardCandidateLevelSnapshot> levels = new(StringComparer.Ordinal);
        Load("wizardpeak", 40, 0x28A000, 0x1CE800, 0x11800,
            "1e204311534f03f09804d55ca833c62a1a3cc3665dc21d88de743d820103a951", 39, 0x10000,
            "5d3996111567ffac6eca08d340f2f691824bb7969ba2a0cd95e9346ccfe157e1",
            0x64, 0x19B518, 0xB4, 0x1CAC88, 0x6178, 165, 6, 0xC230, 0x19D4, 0xFC3C, 0xBF);
        Load("magiccrafters", 34, 0x2BE800, 0x1CC800, 0x12000,
            "a143056b97866b7d5bb65e93ec08b99ae20a9178a2460fb86483d9813bed30f8", 33, 0x14000,
            "98e135e18876ea094ca9484b33cfb38438e2b99b72ee4d6d66e907722c744a1d",
            0x98, 0x1BAFDC, 0xBC, 0x1C8A54, 0x88EC, 135, 107, 0xDF54, 0x18B4, 0x11830, 0xBE);
        if (image.Length != OriginalImageByteLength)
            throw new InvalidDataException("The original native Wizard source changed length during inspection.");
        return new(path, levels);

        void Load(string key, int dataEntry, int dataLength, int sceneOffset, int sceneLength, string sceneHash,
            int overlayEntry, int overlayLength, string overlayHash, int wizardRootSlot, uint wizardRoot,
            int lightningRootSlot, uint lightningRoot, int tableOffset, int count, int nativeWizard,
            int propertiesOffset, int propertiesLength, int fixupOffset, int fixupCount)
        {
            (long dataOffset, byte[] data) = ReadEntry(dataEntry, dataLength);
            (_, byte[] overlay) = ReadEntry(overlayEntry, overlayLength);
            if (Hash(overlay) != overlayHash || Word(data, 0x18) != sceneOffset || Word(data, 0x1C) != sceneLength)
                throw new InvalidDataException("The native Wizard behavior overlay or scene descriptor differs from the source certificate.");
            byte[] scene = data.AsSpan(sceneOffset, sceneLength).ToArray();
            if (Hash(scene) != sceneHash || Word(data, wizardRootSlot) != wizardRoot || Word(data, lightningRootSlot) != lightningRoot ||
                ActorAtRoot(data, wizardRootSlot) != 0x011B || ActorAtRoot(data, lightningRootSlot) != 0x0026 ||
                Word(scene, tableOffset - 4) != count || Word(scene, propertiesOffset) != propertiesLength || Word(scene, fixupOffset) != fixupCount)
                throw new InvalidDataException("The native Wizard owner table, properties, fixups or resident actor/lightning roots changed.");
            levels.Add(key, new(new(key, dataEntry, dataOffset, dataLength, Hash(data), sceneOffset, sceneLength, sceneHash,
                overlayEntry, overlayHash, wizardRoot, lightningRoot, tableOffset, count, nativeWizard,
                propertiesOffset, propertiesLength, fixupOffset, fixupCount), scene));
        }
        (long Offset, byte[] Bytes) ReadEntry(int index, int expectedLength)
        {
            byte[] header = DiscImage.ReadFileBytes(image, layout, 37, index * 8L, 8);
            long offset = Word(header, 0);
            if (Word(header, 4) != expectedLength)
                throw new InvalidDataException("The source Wizard WAD entry has a stale native byte length.");
            return (offset, DiscImage.ReadFileBytes(image, layout, 37, offset, expectedLength));
        }
    }
    private static ushort ActorAtRoot(byte[] data, int rootSlot) => BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(0x150 + ((rootSlot - 0x50) / 4) * 2));
    private static uint Word(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset));
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static string HashStream(Stream stream) => Convert.ToHexStringLower(SHA256.HashData(stream));
}

internal enum NativeGreenWizardCandidateWriteKind { SourceRow, PrivateProperties, PointerFixups }
internal sealed record NativeGreenWizardCandidateWrite(
    string Id, NativeGreenWizardCandidateWriteKind Kind, NativeMappedExtent Address, string BeforeHex, string AfterHex);
internal sealed record NativeGreenWizardCandidateTarget(
    int TrueIndex, ushort OriginalActorId, int PointerFieldSceneOffset, int PropertiesOffset, int OwnedExtentByteLength,
    string OriginalRowSha256, string OutputRowSha256, string OutputPropertiesSha256,
    byte PreservedPod, byte PreservedReward, IReadOnlyList<WizardPeakGreenWizardRoutePoint> RoutePoints);
internal sealed record NativeGreenWizardResidentDependency(
    string Role, ushort ActorId, uint NativeRoot, string LevelDataSha256, string BehaviorOverlaySha256,
    bool Imported, string PreservationContract);

internal sealed class NativeGreenWizardPropertiesCandidatePlan
{
    private readonly byte[] _sourceScene;
    private readonly byte[] _outputScene;
    internal NativeGreenWizardPropertiesCandidatePlan(string sourceImagePath,
        NativeGreenWizardCandidateLevelBinding donor, NativeGreenWizardCandidateLevelBinding target,
        int donorTrueIndex, string donorRowSha256, string donorPayloadSha256,
        IReadOnlyList<NativeGreenWizardCandidateTarget> targets, IReadOnlyList<NativeGreenWizardCandidateWrite> writes,
        NativeMutableScenePropertiesLinkRequest dependencyRequest, byte[] sourceScene, byte[] outputScene, string planSha256)
    {
        SourceImagePath = sourceImagePath; Donor = donor; Target = target; DonorTrueIndex = donorTrueIndex;
        DonorRowSha256 = donorRowSha256; DonorPayloadSha256 = donorPayloadSha256;
        Targets = Array.AsReadOnly(targets.Select(item => item with { RoutePoints = Array.AsReadOnly(item.RoutePoints.ToArray()) }).ToArray());
        Writes = Array.AsReadOnly(writes.ToArray());
        _dependencyRequest = CopyRequest(dependencyRequest); _sourceScene = sourceScene.ToArray(); _outputScene = outputScene.ToArray();
        ResidentDependencies = Array.AsReadOnly(new[]
        {
            new NativeGreenWizardResidentDependency("wizard", 0x011B, target.WizardActorRoot, target.DataSha256,
                target.OverlaySha256, false, "Existing target-native actor resources and behavior remain unchanged; only instance row/private route properties are authored."),
            new NativeGreenWizardResidentDependency("lightning", 0x0026, target.LightningActorRoot, target.DataSha256,
                target.OverlaySha256, false, "Existing target-native lightning behavior/resources remain unchanged; this candidate imports no missing overlay code or resource root.")
        });
        PlanSha256 = planSha256;
    }
    public const string ProfileId = "native-green-wizard-owned-properties-candidate-v1";
    public string SourceImagePath { get; }
    public string SourceImageSha256 => NativeGreenWizardCandidateSource.OriginalImageSha256;
    public long SourceImageByteLength => NativeGreenWizardCandidateSource.OriginalImageByteLength;
    public NativeGreenWizardCandidateLevelBinding Donor { get; }
    public NativeGreenWizardCandidateLevelBinding Target { get; }
    public int DonorTrueIndex { get; }
    public string DonorRowSha256 { get; }
    public string DonorPayloadSha256 { get; }
    public IReadOnlyList<NativeGreenWizardCandidateTarget> Targets { get; }
    public IReadOnlyList<NativeGreenWizardCandidateWrite> Writes { get; }
    public IReadOnlyList<NativeGreenWizardResidentDependency> ResidentDependencies { get; }
    private readonly NativeMutableScenePropertiesLinkRequest _dependencyRequest;
    internal NativeMutableScenePropertiesLinkRequest DependencyRequest => CopyRequest(_dependencyRequest);
    public string PlanSha256 { get; }
    public bool CandidateOnly => true;
    public bool RuntimeVerified => false;
    public bool NormalCreateBinReady => false;
    public long TargetSceneWadOffset => Target.DataWadOffset + Target.SceneDataOffset;
    public byte[] CopySourceScene() => _sourceScene.ToArray();
    public byte[] CopyOutputScene() => _outputScene.ToArray();
    public string OutputSceneSha256 => Convert.ToHexStringLower(SHA256.HashData(_outputScene));
    public IReadOnlyList<string> RuntimeRequirements { get; } = Array.AsReadOnly(new[]
    {
        "Unproven candidate: verify visibility, translated route and lightning attack/hit behavior.",
        "Verify damage, death, retirement, preserved reward and normal nearby actors/collision.",
        "Multiple private instances require their own concurrent gameplay evidence; no old profile proof is inherited."
    });
    private static NativeMutableScenePropertiesLinkRequest CopyRequest(NativeMutableScenePropertiesLinkRequest request) =>
        request with { SourceScene = request.SourceScene.ToArray(), Instances = Array.AsReadOnly(request.Instances.Select(instance => instance with
        {
            Payload = instance.Payload.ToArray(), InternalPointers = Array.AsReadOnly(instance.InternalPointers.ToArray()),
            FixupRewrites = Array.AsReadOnly(instance.FixupRewrites.ToArray()),
            DonorScene = instance.DonorScene is { } donor ? donor with { Scene = donor.Scene.ToArray() } : null
        }).ToArray()) };
}

internal static class NativeGreenWizardPropertiesCandidateCompiler
{
    private const int RecordBytes = 0x58, PayloadBytes = 0x50;

    /// <summary>
    /// Creates a new candidate identity, never an old resident-swap approval.
    /// The imported route/properties use their donor's address space; all actor
    /// resources, behavior overlay and instance policy stay native to the target.
    /// Exact native owner schemas establish storage, not zero-gap inference.
    /// </summary>
    public static NativeGreenWizardPropertiesCandidatePlan Compile(
        NativeGreenWizardCandidateSource source, NativeGreenWizardPropertiesCandidateRequest request)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(request);
        NativeGreenWizardCandidateLevelSnapshot donorLevel = source.GetLevel(request.DonorLevelKey);
        NativeGreenWizardCandidateLevelSnapshot targetLevel = source.GetLevel(request.TargetLevelKey);
        if (request.DonorLevelKey != "wizardpeak" || request.DonorTrueIndex != 6)
            throw new InvalidDataException("This candidate has no checked imported donor schema beyond Wizard Peak T6.");
        int[] indices = request.TargetTrueIndices?.ToArray() ?? [];
        if (indices.Length == 0 || indices.Length > targetLevel.Binding.SourceRecordCount)
            throw new InvalidDataException("A candidate requires a nonempty bounded target instance selection.");
        if (indices.Distinct().Count() != indices.Length)
            throw new InvalidDataException("Duplicate mutable target instances cannot share an owner.");
        if (indices.Any(index => index < 0 || index >= targetLevel.Binding.SourceRecordCount))
            throw new InvalidDataException("A requested native target index is outside its certified source table.");
        Array.Sort(indices);
        NativeGreenWizardCandidateLevelBinding donorBinding = donorLevel.Binding, targetBinding = targetLevel.Binding;
        byte[] donorScene = donorLevel.CopyScene(), scene = targetLevel.CopyScene();
        byte[] donorRow = ReadRow(donorScene, donorBinding, request.DonorTrueIndex);
        byte[] nativeRow = ReadRow(scene, targetBinding, targetBinding.NativeWizardTrueIndex);
        int donorOffset = ReadPointer(donorRow, 0);
        byte[] donorProperties = ReadWizardProperties(donorScene, donorBinding, donorRow);
        byte[] nativeProperties = ReadWizardProperties(scene, targetBinding, nativeRow);
        if (!donorProperties.AsSpan(4, 0x2C).SequenceEqual(nativeProperties.AsSpan(4, 0x2C)))
            throw new InvalidDataException("The imported Wizard scalar schema is not equivalent to the resident behavior's native instance.");
        string donorRowHash = Hash(donorRow), donorPayloadHash = Hash(donorProperties);
        NativePropertiesFixupEnvelope fixups = ReadFixups(scene, targetBinding);
        NativePropertiesDonorScene? independentDonor = donorBinding.LevelKey == targetBinding.LevelKey ? null :
            new(donorScene, donorBinding.SceneSha256, ReadFixups(donorScene, donorBinding));
        List<NativeMutablePropertiesInstance> instances = [];
        List<(int Index, int RowOffset, int PropertiesOffset, int Extent, byte[] Before, byte[] After,
            IReadOnlyList<WizardPeakGreenWizardRoutePoint> Routes)> rows = [];
        foreach (int index in indices)
        {
            byte[] before = ReadRow(scene, targetBinding, index);
            int rowOffset = checked(targetBinding.SourceTableSceneOffset + index * RecordBytes);
            (int propertiesOffset, int extent, NativePropertiesFixupRewrite[] rewrites, string ownerSchema) =
                DecodeOwner(scene, targetBinding, index, before);
            byte[] payload = donorProperties.ToArray();
            IReadOnlyList<WizardPeakGreenWizardRoutePoint> routes = TranslateRoutes(donorRow, donorProperties, before, payload);
            // Target runtime fields come from its resident native Wizard, never
            // from an unrelated donor overlay. Only native slot placement and
            // its detached pod, culling and reward identity are retained.
            byte[] after = nativeRow.ToArray();
            before.AsSpan(0, 4).CopyTo(after);
            before.AsSpan(0x0C, 12).CopyTo(after.AsSpan(0x0C));
            before.AsSpan(0x20, 18).CopyTo(after.AsSpan(0x20));
            foreach (int offset in new[] { 0x43, 0x46, 0x4A, 0x53 }) after[offset] = before[offset];
            string proof = Hash(JsonSerializer.SerializeToUtf8Bytes(new
            {
                schema = ownerSchema, source.SourceImageSha256, donorBinding, targetBinding,
                donorTrueIndex = request.DonorTrueIndex, donorRowHash, donorPayloadHash,
                nativeRuntimeRowSha256 = Hash(nativeRow), nativeRuntimePropertiesSha256 = Hash(nativeProperties),
                targetTrueIndex = index, targetRowSha256 = Hash(before), propertiesOffset, extent,
                ownedExtentSha256 = Hash(scene.AsSpan(propertiesOffset, extent)), rewrites
            }));
            NativeMutablePropertiesInstance instance = new(
                $"{donorBinding.LevelKey}.T{request.DonorTrueIndex}-to-{targetBinding.LevelKey}.T{index}",
                donorOffset, donorPayloadHash, payload, Hash(payload),
                new(NativePropertiesReservationKind.OwnedComponentExtent, propertiesOffset, extent,
                    Hash(scene.AsSpan(propertiesOffset, extent)), proof),
                new($"{targetBinding.LevelKey}.T{index}", rowOffset, false), [new(0, 0x28)], rewrites, null)
            { DonorScene = independentDonor };
            instance = instance with { OwnershipAuthority = NativePropertiesMutableOwnershipAuthority.FromValidatedOwnedInstanceEvidence(
                proof, targetBinding.SceneSha256, fixups, instance) };
            instances.Add(instance);
            rows.Add((index, rowOffset, propertiesOffset, extent, before, after, routes));
        }
        NativeMutableScenePropertiesLinkRequest dependencyRequest = new(scene, targetBinding.SceneSha256, fixups, instances);
        NativeScenePropertiesLinkPlan linked = NativeScenePropertiesLinker.CompileMutable(dependencyRequest);
        NativeAddressSpace original = new(NativeAddressKind.Scene, targetBinding.SceneSha256, NativeAddressRevision.Original, scene.Length);
        NativeAddressSpace final = new(NativeAddressKind.Scene, targetBinding.SceneSha256, NativeAddressRevision.Final, scene.Length);
        NativeRelocationMap map = new(original, final, [new("native-scene-fixed-owner", new(original, 0, scene.Length), new(final, 0, scene.Length), 4)]);
        List<NativeGreenWizardCandidateWrite> writes = [];
        foreach (NativePropertiesSceneWrite write in linked.Writes)
        {
            NativeGreenWizardCandidateWriteKind kind = write.Offset == fixups.CountOffset
                ? NativeGreenWizardCandidateWriteKind.PointerFixups : NativeGreenWizardCandidateWriteKind.PrivateProperties;
            AddWrite($"{kind}:{write.Offset:X}", kind, write.Offset, Convert.FromHexString(write.BeforeHex), Convert.FromHexString(write.AfterHex));
        }
        List<NativeGreenWizardCandidateTarget> targets = [];
        foreach (var row in rows)
        {
            NativePropertiesPointerBinding binding = linked.Bindings.Single(item => item.FieldOffset == row.RowOffset);
            BinaryPrimitives.WriteUInt32LittleEndian(row.After, binding.After);
            AddWrite($"row:T{row.Index}", NativeGreenWizardCandidateWriteKind.SourceRow, row.RowOffset, row.Before, row.After);
            string installedPayload = linked.Writes.Single(item => item.Offset == row.PropertiesOffset).AfterHex;
            targets.Add(new(row.Index, ActorId(row.Before), row.RowOffset, row.PropertiesOffset, row.Extent,
                Hash(row.Before), Hash(row.After), Hash(Convert.FromHexString(installedPayload)), row.Before[0x43], row.Before[0x53], row.Routes));
        }
        NativeRelocationMap.RequireDisjoint(writes.Select(write => write.Address.Original));
        NativeRelocationMap.RequireDisjoint(writes.Select(write => write.Address.Final));
        writes = writes.OrderBy(write => write.Address.Final.Offset).ToList();
        byte[] output = scene.ToArray();
        foreach (NativeGreenWizardCandidateWrite write in writes)
        {
            int offset = checked((int)write.Address.Final.Offset);
            byte[] before = Convert.FromHexString(write.BeforeHex), after = Convert.FromHexString(write.AfterHex);
            if (!output.AsSpan(offset, before.Length).SequenceEqual(before))
                throw new InvalidDataException("A typed native candidate write lost its complete source preimage.");
            after.CopyTo(output, offset);
        }
        string planSha = Hash(JsonSerializer.SerializeToUtf8Bytes(new
        {
            profile = NativeGreenWizardPropertiesCandidatePlan.ProfileId, source.SourceImageSha256,
            donorBinding, targetBinding, donorTrueIndex = request.DonorTrueIndex, donorRowHash, donorPayloadHash, targets,
            dependencyProofs = instances.Select(instance => instance.Reservation.OwnershipEvidenceSha256),
            writes = writes.Select(write => new { write.Id, write.Kind, offset = write.Address.Final.Offset,
                length = write.Address.Final.ByteLength, write.BeforeHex, write.AfterHex }), outputSceneSha256 = Hash(output)
        }));
        return new(source.SourceImagePath, donorBinding, targetBinding, request.DonorTrueIndex, donorRowHash,
            donorPayloadHash, targets, writes, dependencyRequest, scene, output, planSha);

        void AddWrite(string id, NativeGreenWizardCandidateWriteKind kind, int offset, byte[] before, byte[] after)
        {
            if (before.Length != after.Length || before.Length == 0)
                throw new InvalidDataException("Native candidate writes must retain exact fixed owner lengths.");
            writes.Add(new(id, kind, map.Map(new(original, offset, before.Length)), Convert.ToHexString(before), Convert.ToHexString(after)));
        }
    }

    private static (int Offset, int Length, NativePropertiesFixupRewrite[] Rewrites, string Schema) DecodeOwner(
        byte[] scene, NativeGreenWizardCandidateLevelBinding level, int index, byte[] row)
    {
        if (level.LevelKey == "wizardpeak" && index == 10)
            throw new InvalidDataException("Wizard Peak T10 is a retired non-attacking runtime experiment; matching bytes do not revive its proof.");
        int offset = ReadPointer(row, 0);
        int length;
        NativePropertiesFixupRewrite[] rewrites;
        string schema;
        if (level.LevelKey == "wizardpeak")
        {
            if (ActorId(row) != 0x011D || row[0x43] != 0xFF)
                throw new InvalidDataException("The target is not a detached native Elder Wizard owner.");
            RequireComponentExtent(level, offset, 0x24);
            if (ReadPointer(scene, offset + 0x0C) != offset + 0x1C)
                throw new InvalidDataException("The Elder owner does not have its native internal route-header pointer schema.");
            uint count = BinaryPrimitives.ReadUInt32LittleEndian(scene.AsSpan(offset + 0x1C));
            if (count == 0 || count > (level.PropertiesComponentByteLength - 0x24) / 0x10)
                throw new InvalidDataException("The Elder route count cannot establish a bounded native owned extent.");
            length = checked(0x24 + (int)count * 0x10);
            rewrites = [new(offset + 0x0C, 0)];
            schema = "native-elder-wizard-route-count-owned-extent-v1";
        }
        else
        {
            // The mover's full generic schema is not yet decoded. This one
            // source-pinned native owner already has exact extent/fixup proof;
            // no adjacent gap or actor-name inference extends it to other slots.
            if (index != 27 || ActorId(row) != 0x010F || row[0x43] != 0xFF || offset != 0xE9BC ||
                Hash(row) != "7e54f1df23673304757bd2b9a7a4e17212b4ca568495a461db45e0e888b1a5e0")
                throw new InvalidDataException("The target has no checked private Magic Crafters owner schema.");
            length = 0x74;
            RequireComponentExtent(level, offset, length);
            if (Hash(scene.AsSpan(offset, length)) != "d7e56d7467104802bc4226a20399a8732644881105b51502fdeea744f57f38ee")
                throw new InvalidDataException("The exact native Magic Crafters owner extent preimage changed.");
            rewrites = [new(offset, 0), new(offset + 0x3C, null)];
            schema = "magiccrafters-native-T27-source-bound-owned-extent-v1";
        }
        RequireComponentExtent(level, offset, length);
        if (length < PayloadBytes)
            throw new InvalidDataException("The decoded native owner extent is too small for the Wizard's private properties.");
        return (offset, length, rewrites, schema);
    }

    private static byte[] ReadWizardProperties(byte[] scene, NativeGreenWizardCandidateLevelBinding level, byte[] row)
    {
        if (ActorId(row) != 0x011B || row[0x43] != 0xFF || ReadPointer(row, 8) != 0)
            throw new InvalidDataException("The native Wizard donor/runtime instance has unsupported class, pod or additional data dependencies.");
        int offset = ReadPointer(row, 0);
        RequireComponentExtent(level, offset, PayloadBytes);
        byte[] payload = scene.AsSpan(offset, PayloadBytes).ToArray();
        if (ReadPointer(payload, 0) != offset + 0x28 || ReadPointer(payload, 0x28) != 2 ||
            BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(0x2C)) != 0xFFFF0000 ||
            ReadPointer(payload, 0x3C) != 0 || ReadPointer(payload, 0x4C) != 0)
            throw new InvalidDataException("The native Wizard donor does not match the complete two-point scalar/internal-pointer schema.");
        return payload;
    }
    private static IReadOnlyList<WizardPeakGreenWizardRoutePoint> TranslateRoutes(byte[] donorRow, byte[] properties, byte[] targetRow, byte[] payload)
    {
        List<WizardPeakGreenWizardRoutePoint> routes = [];
        for (int offset = 0x30; offset < PayloadBytes; offset += 0x10)
        {
            int x = Axis(offset, 0x0C), y = Axis(offset + 4, 0x10), z = Axis(offset + 8, 0x14);
            routes.Add(new(x, y, z));
        }
        return Array.AsReadOnly(routes.ToArray());
        int Axis(int propertiesOffset, int rowOffset)
        {
            int value = checked(BinaryPrimitives.ReadInt32LittleEndian(targetRow.AsSpan(rowOffset)) +
                BinaryPrimitives.ReadInt32LittleEndian(properties.AsSpan(propertiesOffset)) -
                BinaryPrimitives.ReadInt32LittleEndian(donorRow.AsSpan(rowOffset)));
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(propertiesOffset), value);
            return value;
        }
    }
    private static NativePropertiesFixupEnvelope ReadFixups(byte[] scene, NativeGreenWizardCandidateLevelBinding level) =>
        new(level.FixupOffset, level.FixupCount, level.FixupCount,
            Hash(scene.AsSpan(level.FixupOffset, 4 + 4 * level.FixupCount)), Hash([]));
    private static void RequireComponentExtent(NativeGreenWizardCandidateLevelBinding level, int offset, int length)
    {
        if ((offset & 3) != 0 || length <= 0 || offset < level.PropertiesComponentOffset + 4 ||
            (long)offset + length > (long)level.PropertiesComponentOffset + level.PropertiesComponentByteLength)
            throw new InvalidDataException("The declared native properties extent is outside its certified component owner.");
    }
    private static byte[] ReadRow(byte[] scene, NativeGreenWizardCandidateLevelBinding level, int index)
    {
        if (index < 0 || index >= level.SourceRecordCount)
            throw new InvalidDataException("Native donor/owner index is outside its certified table.");
        return scene.AsSpan(checked(level.SourceTableSceneOffset + index * RecordBytes), RecordBytes).ToArray();
    }
    private static int ReadPointer(byte[] bytes, int offset)
    {
        uint value = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset));
        if (value > int.MaxValue) throw new InvalidDataException("A native scene pointer/count exceeds its bounded address space.");
        return (int)value;
    }
    private static ushort ActorId(byte[] row) => BinaryPrimitives.ReadUInt16LittleEndian(row.AsSpan(0x36));
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
