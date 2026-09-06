using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;

namespace Spyro.Editor.Core.Exporting;

public sealed record NativeId65SceneListOwner(int ComponentIndex, string Id,
    int ComponentOffset, int ByteLength, uint NativeCountGlobal, uint NativePointerGlobal,
    uint FirstEntrySceneRelativeOffset, bool OwnsGuardWord);

public sealed record NativeId65SceneListPlacementRequirement(int SceneRelativeOffset,
    int ByteLength, int NextComponentSceneRelativeOffset);

/// <summary>
/// Seven fresh zero-count native scene components, with storage for the three
/// zero-count branch-delay reads. These are not a scene header, an initialized
/// scene, or authority to omit the remaining scene components.
/// </summary>
public sealed class NativeId65ZeroSceneAnimationLists
{
    private const int EncodedLength = 0x44;
    private const string ExecutableSha256 = "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998";
    private readonly byte[] _components;
    private readonly byte[]? _executable;
    private static readonly NativeId65EngineEvidence[] ConsumerSpans =
    [
        new("seven-scene-list-loader-physical-order-and-reset-call", "EXE", 0x4440, 0x2E4,
            "3d5c033d15f4e510c4326b9699c0b4b6d5def3cd3c288f528e0c7f823274b80e"),
        new("ordinary-scene-list-updater-zero-paths-and-three-delay-reads", "EXE", 0x1AEFC, 0xC94,
            "040a3b6ab61a418501453135f4b7cf41b94e57081377f301730f93d68970166b"),
        new("reset-scene-list-updater-zero-paths-and-three-delay-reads", "EXE", 0x1BCAC, 0x520,
            "b71153d2ffb77871623e5f3f85e9adb9e4793d66368eee9d30b7fd96bef74354"),
        new("ordinary-update-call-before-level-callback", "EXE", 0x24250, 0x60,
            "12f23f264ca3b413c8a05ecba7a428f56bff1f127b6ad24f734f60bd002d7e97"),
        new("texture-metadata-count-and-pointer-loader", "EXE", 0x3558, 0x54,
            "25aede725485af9da35013da9c1adbcb8d4f336c588c272e29edb6c7115aca0c")
    ];

    private NativeId65ZeroSceneAnimationLists(byte[]? executable)
    {
        if (executable != null)
        {
            ValidateSourceExecutable(executable);
            _executable = executable.ToArray();
        }
        Lists = Array.AsReadOnly(new[]
        {
            Owner(0, "texture-animation", 0, 12, 0x80078560, true),
            Owner(1, "texture-scroll", 12, 12, 0x80078568, true),
            Owner(2, "numeric-list-20", 24, 8, 0x80078580, false),
            Owner(3, "numeric-list-10", 32, 8, 0x80078570, false),
            Owner(4, "collision-animation", 40, 12, 0x800785A0, true),
            Owner(5, "numeric-list-28", 52, 8, 0x80078588, false),
            Owner(6, "numeric-list-18", 60, 8, 0x80078578, false)
        });
        _components = EncodeCanonical();
        ValidateCanonicalBytes(_components);
        ComponentsSha256 = Hash(_components);
        Evidence = Array.AsReadOnly(_executable == null ? [] : ConsumerSpans.ToArray());
    }

    public const string ProfileId = "native-id65-zero-scene-animation-lists-clean-usa-v1";
    public string? SourceImageSha256 => SourceCertified ? NativeId65EngineProfileContract.OriginalImageSha256 : null;
    public bool SourceCertified => _executable != null;
    public bool SceneInitialized => false;
    public bool PublicationApproved => false;
    public string ComponentsSha256 { get; }
    public IReadOnlyList<NativeId65SceneListOwner> Lists { get; }
    public IReadOnlyList<NativeId65EngineEvidence> Evidence { get; }
    public NativeId65SceneListPlacementRequirement PlacementRequirement { get; } = new(0x88, EncodedLength, 0xCC);
    public byte[] CopyComponents() => _components.ToArray();

    public void RequireSourceExecutable(ReadOnlySpan<byte> executable)
    {
        if (_executable == null || !executable.SequenceEqual(_executable))
            throw new InvalidDataException("Scene-list authority requires its exact captured native executable, not neutral bytes or metadata.");
    }

    public static async Task<NativeId65ZeroSceneAnimationLists> CaptureAsync(string originalImagePath,
        CancellationToken cancellationToken = default)
    {
        byte[] executable = await NativeId65AuthoredSurfaceConsumers.ReadOriginalExecutableAsync(
            originalImagePath, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return new(executable);
    }

    internal static NativeId65ZeroSceneAnimationLists CreateNeutral() => new(null);

    internal static void ValidateSourceExecutable(ReadOnlySpan<byte> executable)
    {
        if (executable.Length != 0x66000 || Hash(executable) != ExecutableSha256)
            throw new InvalidDataException("Scene-list consumers require the complete exact original USA executable.");
        foreach (var span in ConsumerSpans)
        {
            if (Hash(executable.Slice(checked((int)span.Offset), span.ByteLength)) != span.Sha256)
                throw new InvalidDataException("A bound native scene-list consumer span changed.");
        }
    }

    internal static void ValidateCanonicalBytes(ReadOnlySpan<byte> bytes)
    {
        // Fixed length is checked before any allocation or native pointer walk.
        // Negative counts happen to skip BLEZ loops but are not this grammar.
        if (bytes.Length != EncodedLength || !bytes.SequenceEqual(EncodeCanonical()))
            throw new InvalidDataException("Scene lists must be the exact seven zero-count components and three owned zero guards.");
    }

    private static byte[] EncodeCanonical()
    {
        byte[] bytes = new byte[EncodedLength];
        int cursor = 0;
        foreach (int length in new[] { 12, 12, 8, 8, 12, 8, 8 })
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(cursor), length);
            cursor += length;
        }
        return bytes;
    }

    private static NativeId65SceneListOwner Owner(int index, string id, int offset,
        int length, uint countGlobal, bool guard) =>
        new(index, id, offset, length, countGlobal, countGlobal + 4, (uint)(0x88 + offset + 8), guard);

    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
