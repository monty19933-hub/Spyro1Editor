using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Exporting;

/// <summary>Stable active-instance intent only. Native placement is deliberately unchanged.</summary>
public sealed record NativeDarkHollowWizardAppendInstance(string FamilyId);

/// <summary>
/// Source-issued one-role Blowhard33/T6 private-scene schema for Dark12. Every
/// compile uses a complete active ledger, fresh canonical final domains and the
/// existing native splice/graph validator. This is not a behavior, placement,
/// authenticated export-history, App transaction or publication producer.
/// </summary>
public sealed class NativeDarkHollowWizardAppendSource
{
    internal const int SceneLength = 0x11800;
    private const string SchemaPolicy = "native-dark-wizard-scene-v1";
    private const string DarkHash = "a937aecb3626ffd2311ba136684a34c9f36877054b52557b4524fcacee0e2d97";
    private const string DonorHash = "9973493f865b5e86bc17b32c5af9e3d7596e25615618c007ed28e0379161f2d8";
    private const string ExeHash = "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998";
    private readonly NativeMobyPersistentOwnerSource _ownerSource;
    private readonly NativeMobyPersistentOwnerFamily _family;
    private readonly NativeMobySceneAppendSource _scene;
    private readonly NativeAddressSpace _original, _donor, _executable;
    private readonly Dictionary<NativeAddressSpace, byte[]> _reads;
    private readonly NativeMobyDependencyInstanceTemplate _template;

    private NativeDarkHollowWizardAppendSource(NativeMobyPersistentOwnerSource ownerSource,
        byte[] dark, byte[] donorData, byte[] executable)
    {
        Require(dark, DarkHash); Require(donorData, DonorHash); Require(executable, ExeHash);
        if (Word(dark, 0x18) != 0x194000 || Word(dark, 0x1C) != SceneLength ||
            Word(donorData, 0x18) != 0x1CE800 || Word(donorData, 0x1C) != SceneLength)
            throw new InvalidDataException("Exact Dark/Blowhard native scene descriptor owners changed.");
        byte[] originalScene = dark.AsSpan(0x194000, SceneLength).ToArray();
        byte[] donorScene = donorData.AsSpan(0x1CE800, SceneLength).ToArray();
        Require(originalScene, "c48c344d3809f1ea5761e968ce6f18b02d5c7bbb82fcec4a7fd453343dbb84f8");
        Require(donorScene, "1e204311534f03f09804d55ca833c62a1a3cc3665dc21d88de743d820103a951");
        Require(donorScene.AsSpan(0x6388, 0x58), "59401eafeab453fd9d26f14eedca58390653dc5f4fcc3d2560f7b3e8fc7afa82");
        Require(donorScene.AsSpan(0xC350, 0x50), "5b3d762f6117ea30e959a21bf2778f3442b03eba93125c9a0e0a642cc58d345b");
        Require(executable.AsSpan(0x42CC4, 0xA0), "f3f404687b98b5208f97274a1932c967ba8d0bd04337011b53d831f25f592335");
        Require(executable.AsSpan(0x46F4, 0x168), "7bfb42fba56df4b4772f4a32f0114940090e0f4029456ed37ea8ac5e871be6d7");
        if (ownerSource.LevelId != 12 || ownerSource.SaveSlot != 2 || ownerSource.OriginalRowCount != 128 ||
            ownerSource.Binding.OriginalRowsSha256 != Hash(originalScene.AsSpan(0x8C38, 128 * 0x58)))
            throw new InvalidDataException("Actual Dark source row prefix does not bind this persistent owner issuer.");
        _ownerSource = ownerSource;
        _family = ownerSource.BindFamily([new("wizard", 33, 6)]);
        var role = _family.Roles.Single();
        if (role.NativeClass != 0x11B)
            throw new InvalidDataException("Source-selected Blowhard33/T6 is not the certified native Wizard row.");
        _original = new(NativeAddressKind.Scene, "dark-scene:" + Hash(originalScene), NativeAddressRevision.Original, SceneLength);
        _donor = new(NativeAddressKind.Scene, "wizard-scene:" + Hash(donorScene), NativeAddressRevision.Original, SceneLength);
        _executable = new(NativeAddressKind.ExecutableFile, "retail-exe:" + ExeHash, NativeAddressRevision.Original, executable.Length);
        _reads = new() { [_original] = originalScene, [_donor] = donorScene, [_executable] = executable.ToArray() };
        _scene = NativeMobySceneAppendSource.Capture(_original, Hash(originalScene), Read);
        if (_scene.PlacedCount != 128 || _scene.RowTableOffset != 0x8C38 || _scene.PropertiesOffset != 0xE038 ||
            _scene.PropertiesEnd != 0xEED4 || _scene.FixupOffset != 0x10EF0 || _scene.FixupCount != 147 || _scene.DynamicCapacity != 91)
            throw new InvalidDataException("Native Dark row/private/fixup/dynamic-pool grammar changed.");
        for (int i = 0; i < _scene.PlacedCount; i++)
            if (BinaryPrimitives.ReadUInt16LittleEndian(originalScene.AsSpan(_scene.RowTableOffset + i * 0x58 + 0x36)) == 0x11B)
                throw new InvalidDataException("The original target already contains an unaccounted native Wizard instance.");
        if (Word(donorScene, 0x6388) != 0xC350 || Word(donorScene, 0xC350) != 0xC378 ||
            donorScene[0x6388 + 0x43] != 0xFF || Word(donorScene, 0xFC3C) != 191)
            throw new InvalidDataException("Native donor private pointer/pod/fixup schema changed.");
        int[] fixups = Enumerable.Range(0, 191).Select(i => checked((int)Word(donorScene, 0xFC40 + i * 4))).ToArray();
        if (!fixups.Where(f => f >= 0x6388 && f < 0x63E0).SequenceEqual(new[] { 0x6388 }) ||
            !fixups.Where(f => f >= 0xC350 && f < 0xC3A0).SequenceEqual(new[] { 0xC350 }))
            throw new InvalidDataException("Complete imported row/private owners contain an unclosed native pointer field.");
        List<NativeMobyDependencyNode> schema = [];
        AddSchema("scene-schema.dynamic-allocator", _executable, 0x42CC4, 0xA0);
        AddSchema("scene-schema.native-loader", _executable, 0x46F4, 0x168);
        AddSchema("scene-schema.donor-fixups", _donor, 0xFC3C, 4 + 191 * 4);
        var shared = NativeMobyDependencyClosureCompiler.Compile(schema, schema.Select(n => n.Id), Read);
        _template = NativeMobyDependencyInstanceTemplate.Capture(SchemaPolicy,
            [new("wizard", 6, 0x11B, R(_donor, 0x6388, 0x58), R(_donor, 0xC350, 0x50), 0, [], true)], [], shared, Read);
        SourceSchemaSha256 = Hash(JsonSerializer.SerializeToUtf8Bytes(new
        {
            SchemaPolicy, SourceImageSha256, DarkHash, DonorHash, ExeHash, role.DonorLevelId, role.DonorTrueIndex,
            role.RoleId, role.NativeClass, role.SourceVariantId, Row = _template.Rows.Single().Row.ExpectedSha256,
            Private = _template.Rows.Single().Properties.ExpectedSha256, RowPointer = 0, PrivatePointer = 0,
            PrivateTarget = 0x28, PrivateLength = 0x50, Pod = 0xFF, DonorFixups = Hash(donorScene.AsSpan(0xFC3C, 4 + 191 * 4))
        }));
        return;
        void AddSchema(string id, NativeAddressSpace space, int offset, int length)
        {
            var read = R(space, offset, length);
            schema.Add(new(id, NativeMobyDependencyKind.NativeRoutine, NativeMobyDependencyResolution.ImportFromSource,
                NativeMobyDependencySharing.Immutable, read.ExpectedSha256, [], [read], c => new(c.Read("native"), [], [])));
        }
    }

    public string SourceImageSha256 => NativeMobySourceInventory.OriginalImageSha256;
    public string SourceSchemaSha256 { get; }
    public int LevelId => 12;
    public NativeMobyPersistentOwnerLedger EmptyOwners() => _ownerSource.Empty();

    /// <summary>Rebinds source identity only; it cannot authenticate which document accompanied an earlier export/save.</summary>
    public NativeMobyPersistentOwnerLedger RestoreOwners(ReadOnlySpan<byte> document)
    {
        var owners = _ownerSource.Restore(document);
        RequireRoles(owners);
        return owners;
    }

    public NativeDarkHollowWizardAppendPlan CompileActive(NativeMobyPersistentOwnerLedger acceptedOwners,
        IEnumerable<NativeDarkHollowWizardAppendInstance> activeInstances)
    {
        ArgumentNullException.ThrowIfNull(acceptedOwners);
        if (!acceptedOwners.SourceCertified)
            throw new InvalidDataException("Caller metadata cannot issue a native persistent scene ledger.");
        var active = NativeMobyPersistentOwnerLayout.Snapshot(activeInstances, "active Wizard scene requests");
        if (active.Length == 0 || active.Any(i => i == null))
            throw new InvalidDataException("Native Wizard scene requires a nonempty complete active family set.");
        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (var instance in active)
        {
            NativeMobyPersistentOwnerLayout.RequireId(instance.FamilyId);
            if (!ids.Add(instance.FamilyId)) throw new InvalidDataException("Duplicate active native Wizard family identity.");
        }
        var prior = RestoreOwners(acceptedOwners.Serialize());
        prior.RequireNoUnmaterializedRetirements();
        if (prior.Entries.Any(e => !ids.Contains(e.FamilyId)))
            throw new InvalidDataException("Native scene cannot omit, compact or forget a previously reserved active family.");
        var proposed = prior.Append(active.Select(i => new NativeMobyPersistentOwnerRequest(i.FamilyId, _family)));
        if (proposed.Entries.Count != active.Length)
            throw new InvalidDataException("Active one-role requests are not an exact persistent owner bijection.");
        NativeAddressSpace final = new(NativeAddressKind.Scene, "dark-wizard-owned-scene-v1", NativeAddressRevision.Final, SceneLength);
        var requests = active.Select(i => new NativeMobySceneAppendRequest(new(i.FamilyId, _template), [new("wizard", 0, 0x28)])).ToArray();
        var plan = NativeMobySceneAppendPlan.CompileWithPersistentOwners(_scene, final, [final], _executable,
            requests, Read, _ownerSource, proposed);
        if (plan.PlacedCountAfter != proposed.HighWater || plan.FixupCountAfter != 147 + 2 * active.Length)
            throw new InvalidDataException("Native scene materialization changed the proposed persistent row/fixup ownership.");
        // Nothing externally observable is committed until ALL native capacity,
        // private/shared pointer and full-byte readback checks above succeed.
        return NativeDarkHollowWizardAppendPlan.Create(this, proposed, plan);
    }

    public static NativeDarkHollowWizardAppendSource Capture(string originalImagePath, LevelCatalog catalog)
    {
        var owners = NativeMobyPersistentOwnerSource.Capture(originalImagePath, catalog, 12);
        using FileStream image = new(originalImagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        RequireOriginal(image);
        var layout = DiscImage.DetectLayout(originalImagePath);
        if (layout.SectorSize != 2352 || layout.UserOffset != 24)
            throw new InvalidDataException("Native Wizard append requires exact original MODE2/2352 geometry.");
        byte[] header = DiscImage.ReadFileBytes(image, layout, 37, 0, 0x800);
        if (Word(header, 14 * 8) != 0xF01000 || Word(header, 14 * 8 + 4) != 0x28D800 ||
            Word(header, 40 * 8) != 0x2F6B000 || Word(header, 40 * 8 + 4) != 0x28A000)
            throw new InvalidDataException("Native Dark/Blowhard original WAD data ownership changed.");
        var source = new NativeDarkHollowWizardAppendSource(owners,
            DiscImage.ReadFileBytes(image, layout, 37, 0xF01000, 0x28D800),
            DiscImage.ReadFileBytes(image, layout, 37, 0x2F6B000, 0x28A000),
            DiscImage.ReadFileBytes(image, layout, 53875, 0, 0x66000));
        RequireOriginal(image);
        return source;
    }

    private void RequireRoles(NativeMobyPersistentOwnerLedger owners)
    {
        var role = _family.Roles.Single();
        if (owners.Entries.Any(e => e.PolicyId != _family.PolicyId || e.Role != role) ||
            owners.Entries.Select(e => e.FamilyId).Distinct(StringComparer.Ordinal).Count() != owners.Entries.Count)
            throw new InvalidDataException("Source identity grouping is not the exact one-role Blowhard33/T6 Wizard private schema.");
    }
    private NativeMobyDependencyRead R(NativeAddressSpace space, int offset, int length) =>
        new("native", new(space, offset, length), Hash(Read(new(space, offset, length))));
    private byte[] Read(NativeByteExtent extent) => _reads.TryGetValue(extent.Space, out byte[]? bytes)
        ? bytes.AsSpan(checked((int)extent.Offset), checked((int)extent.ByteLength)).ToArray()
        : throw new InvalidDataException("Unbound source-native Wizard scene address domain.");
    private static void RequireOriginal(FileStream image)
    {
        if (image.Length != NativeMobySourceInventory.OriginalImageByteLength)
            throw new InvalidDataException("Native Wizard append source length changed.");
        image.Position = 0;
        string sha = Convert.ToHexStringLower(SHA256.HashData(image));
        if (sha != NativeMobySourceInventory.OriginalImageSha256 || image.Length != NativeMobySourceInventory.OriginalImageByteLength || image.Position != image.Length)
            throw new InvalidDataException("Native Wizard append requires the complete unchanged original source image.");
    }
    private static void Require(ReadOnlySpan<byte> bytes, string expected)
    { if (Hash(bytes) != expected) throw new InvalidDataException("Source-issued native Wizard private schema preimage changed."); }
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static uint Word(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
}
