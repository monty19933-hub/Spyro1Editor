using System.Buffers.Binary;
using Spyro.Editor.Core.Analysis;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Conditional source capability, not a runtime-state assertion. Only a fresh
/// normal loader clears the unavailable superflame state in this profile.
/// </summary>
public sealed class NativeId65FreshNormalEntryProfile
{
    private NativeId65FreshNormalEntryProfile() { }
    public const string ProfileId = "native-id65-fresh-normal-no-superflame-clean-usa-v1";
    public bool RequiresFreshNormalLoader => true;
    public bool SuperflameAvailable => false;
    public bool CheckpointStateSupported => false;
    public bool ForeignStateSupported => false;
    public bool PowerGrantingBehaviorSupported => false;
    public bool AllStateClosureProven => false;
    public bool RuntimeVerified => false;

    internal static NativeId65FreshNormalEntryProfile Capture(ReadOnlySpan<byte> executable)
    {
        NativeId65FreshSceneSource.ValidateSourceExecutable(executable);
        return new();
    }
}

/// <summary>Private original-image issuer for the fresh scene and its presentation owners.</summary>
internal sealed class NativeId65FreshSceneSource
{
    private const string ExecutableSha256 = "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998";
    private readonly byte[] _executable;
    private readonly byte[] _atlas;
    private static readonly NativeId65EngineEvidence[] Consumers =
    [
        new("ordinary-entry-authored-spawn-widths", "EXE", 0x3F7C, 0x3C, "6b7d387efa46a06550a5784b4e9329337b763718e08e5440e05b87012ba01e66"),
        new("scene-header-presentation-word-copies", "EXE", 0x4388, 0xEC, "be89b0a7f46830d578c18164a5494ca86d6fa2fd5a3a741b6714e7d5b62075bc"),
        new("scene-arena-capacity-properties-pods-buckets-fixups-loader", "EXE", 0x4724, 0x164, "9d22b63e3b17a4ee6fce60b4c34784102091d309342b004e73809f766825d2c8"),
        new("bidirectional-dynamic-allocator-without-capacity-refusal-including-return-delay", "EXE", 0x42CC4, 0xA4, "6625282792e81215f1f35e1aa8b4240937206bcc8bd494eef9e000f65a4bbf9e"),
        new("separate-native-particle-pool-and-required-class78-request", "EXE", 0x4C1C, 0x74, "0b54dc9bc7189e4b54bb874da4b2def42e3a3a4f26090f573b782dbe81c98881"),
        new("shadow-consumer-generates-native-UV-height", "EXE", 0x4ABBC, 0x80, "59a75846aefd843627dd07673dc502553b5d806b44acea36023248e4b1984198"),
        new("specular-native-dynamic-UV-consumer", "EXE", 0x129E4, 0xDC, "6cc6162ba1ddad558c227e2af2f4a9f8f3281b0255e0eaf1f03927d723535faf"),
        new("specular-output-adds-fade-before-dynamic-UV-store", "EXE", 0x12A94, 0x2C, "c949c0e9a1586ae21925e07f0f06874f93b8b4418e04b4a577014bcab09f2b0b"),
        new("all-native-fade-offset-arithmetic-paths-and-GTE-boundaries", "EXE", 0x11DA8, 0x1A0, "22d2f2acf631d0483fdd3a21a95fdf90520649fbcb95b6426c2a11f2f5e97677"),
        new("native-two-bank-unsigned-coordinate-bucket-index", "EXE", 0x42F0C, 0x40, "12b2b291e4ae7df487ed94f7d314d8199b743837935f0fd2d5854d99a49c94eb"),
        new("native-collision-second-bucket-bank-consumer", "EXE", 0x3B6A8, 0x34, "e412b4607cbbc85d8125a6c8bf8623397482680b8973de8e695efa2d404b329f"),
        new("fresh-normal-loader-flame-state-zero-call", "EXE", 0x4368, 0x14, "2ffd895e4284cdb5afd65390cfec5b27f21c3bee059380c9f98aba8724a04c85"),
        new("native-word-fill-helper-and-return-delay", "EXE", 0x7114, 0x1C, "572bd776ba509d54a3848293f5cf78731da439e3b203d100b9cb6fb2ec60decc"),
        new("source-unavailable-superflame-header-copy", "EXE", 0x4424, 0x24, "37ae2d160568592d31f019cc5c507c7d4a45e3f28205d6a491c025bca20b3069"),
        new("flame-renderer-fixed-state-base", "EXE", 0x495B4, 0x14, "b486b1552ae6126737000018b05e013b937f48b82a46d9cf68678eedde7e578a"),
        new("flame-renderer-active-load-and-GTE-boundary", "EXE", 0x49670, 0x28, "17ceae0e6cbb66d4e093f9f20b4cbf8919f58d342146e2808970ba4136fda74b"),
        new("flame-renderer-selected-descriptor-has-no-null-guard", "EXE", 0x49A60, 0x24, "c4743b1846b8c358f06432bf40ce1c0627e30f6dc0b4d96246ddd1237a43405b"),
        new("normal-flame-start-duration-select", "EXE", 0x3A2D8, 0x34, "19a717857adee44ff1a6958324d23c5d8cbac4eedd304028bafba86d2f9c51c6"),
        new("alternate-flame-start-duration-select", "EXE", 0x3A4E4, 0x34, "10b19c02635d0f5fe294fa11000d9576ea0fff5c863bd5781587a8a1fffdf3be"),
        new("normal-flame-start-one-in-branch-delay", "EXE", 0x3A268, 0xC, "cc8e696a22f0cb78394cd64314d2c2cc598688d437e73d41a9483dc40679e106"),
        new("zero-duration-update-branch-before-subtraction", "EXE", 0x39518, 0x40, "4d1b5120b46aadd77ebaa28da5f16026a237d535bcb04cc5e9dc50a9b94ad888")
    ];

    private NativeId65FreshSceneSource(NativeId65NormalResourceSource source)
    {
        _executable = source.CopyExecutable();
        ValidateSourceExecutable(_executable);
        byte[] data = source.CopyData();
        if (BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(24)) != 0x1D0000 ||
            BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(28)) != 0x8800)
            throw new InvalidDataException("Native Town scene descriptor ownership changed.");
        HeaderTextures = NativeId65SceneHeaderTextureOwners.Capture(data.AsSpan(0x1D0000, 0x88));
        _atlas = data.AsSpan(0x800, 0x80000).ToArray();
        EntryProfile = NativeId65FreshNormalEntryProfile.Capture(_executable);
        Evidence = Array.AsReadOnly(source.Evidence.Concat(Consumers).Append(
            new NativeId65EngineEvidence("complete-source-header-presentation-not-donor-spawn", "WAD",
                0x136E000, 0x88, NativeId65SceneHeaderTextureOwners.SourceHeaderSha256)).ToArray());
    }

    internal NativeId65SceneHeaderTextureOwners HeaderTextures { get; }
    internal NativeId65FreshNormalEntryProfile EntryProfile { get; }
    internal IReadOnlyList<NativeId65EngineEvidence> Evidence { get; }
    internal byte[] CopyExecutable() => _executable.ToArray();
    internal byte[] CopyAtlas() => _atlas.ToArray();

    internal static async Task<NativeId65FreshSceneSource> CaptureAsync(string originalImagePath, CancellationToken token)
    {
        NativeId65NormalResourceSource source = await NativeId65NormalResourceSource.CaptureAsync(originalImagePath, token);
        token.ThrowIfCancellationRequested();
        return new(source);
    }

    internal static void ValidateSourceExecutable(ReadOnlySpan<byte> executable)
    {
        if (executable.Length != 0x66000 || NativeId65NormalResourceSource.Hash(executable) != ExecutableSha256)
            throw new InvalidDataException("Fresh scene consumers require the complete exact original USA executable.");
        foreach (var span in Consumers)
            NativeId65NormalResourceSource.RequireHash(executable.Slice(checked((int)span.Offset), span.ByteLength), span.Sha256, span.Name);
    }
}
