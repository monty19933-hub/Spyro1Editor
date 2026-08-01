using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Retires only Tree Tops' unique class 0x0125 update handler in a disposable
/// diagnostic image. The adjacent class 0x0126 actor remains on the native
/// handler. This is intentionally exact-USA and is never used by Create BIN.
/// </summary>
public static class TreeTopsOpeningGemThiefDiagnosticPatch
{
    public const string PatchKind =
        "tree-tops-opening-gem-thief-handler-retirement-diagnostic";
    public const int SupportedLevelId = 43;
    public const int OverlayWadEntry = 51;
    public const int OpeningActorTrueIndex = 0;
    public const ushort OpeningActorClassId = 0x0125;
    public const ushort AdjacentActorClassId = 0x0126;
    public const uint DispatchRuntimeAddress = 0x8007AEAC;
    public const uint NativeHandlerRuntimeAddress = 0x80082F30;
    public const uint DefaultIterationRuntimeAddress = 0x80086AEC;
    public const long DispatchOverlayFileOffset = 0x474;

    private const long ExpectedOverlayWadOffset = 0x3D79000;
    private const int ExpectedOverlayByteLength = 0xF000;
    private const string ExpectedOverlaySha256 =
        "f7363f0f2a03e6ddcc906fd68c914905c3704e49e058220941ac9a0ece488405";
    private const string ExpectedPatchedOverlaySha256 =
        "fd003b3c8c89ccb52a0102aea9b2c4588a7d17b0d41b5ba503126ca7172c5a41";

    private static readonly byte[] ExpectedNativeDispatch =
        [0x30, 0x2F, 0x08, 0x80];
    private static readonly byte[] ExpectedDefaultDispatch =
        [0xEC, 0x6A, 0x08, 0x80];
    private static readonly byte[] ExpectedHandlerPrefix =
    [
        0x0B, 0x00, 0x02, 0x3C,
        0x18, 0x00, 0x83, 0x8E,
        0x00, 0x00, 0x91, 0x8E,
        0x24, 0x18, 0x62, 0x00
    ];
    private static readonly byte[] ExpectedDefaultIterationPrefix =
    [
        0x60, 0x02, 0xA9, 0x8F,
        0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x34, 0x8D,
        0x04, 0x00, 0x29, 0x25,
        0x0B, 0xD3, 0x80, 0x16
    ];

    // Runtime addresses map to overlay file offsets with a four-byte load header.
    private const long HandlerOverlayFileOffset = 0x84F8;
    private const long DefaultIterationOverlayFileOffset = 0xC0B4;

    /// <summary>
    /// Applies the exact four-byte dispatcher change to an already-generated
    /// disposable Tree Tops image. No actor row, properties, route, or other
    /// class dispatch entry is changed.
    /// </summary>
    public static MobySourcePatch ApplyToDisposableDiagnosticImage(
        string imagePath,
        LevelDefinition level)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);
        ArgumentNullException.ThrowIfNull(level);
        if (level.LevelId != SupportedLevelId ||
            !level.Key.Equals("treetops", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The opening gem-thief diagnostic patch is restricted to Tree Tops (level 43).");
        }

        string fullPath = Path.GetFullPath(imagePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                "Disposable Tree Tops diagnostic image was not found.",
                fullPath);
        }

        using FileStream image = File.Open(
            fullPath,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.Read);
        DiscLayout layout = DiscImage.DetectLayout(fullPath);
        DiscFileRecord wad = DiscImage.FindRootFileRecord(
            image,
            layout,
            IsWadName);
        (long overlayOffset, int overlayLength) =
            ReadWadEntry(image, layout, wad, OverlayWadEntry);
        GuardOverlayPreimage(
            image,
            layout,
            wad,
            overlayOffset,
            overlayLength);

        long dispatchWadOffset =
            checked(overlayOffset + DispatchOverlayFileOffset);
        DiscImage.WriteFileBytes(
            image,
            layout,
            wad.Lba,
            dispatchWadOffset,
            ExpectedDefaultDispatch);
        image.Flush(flushToDisk: true);

        VerifyBytes(
            image,
            layout,
            wad,
            dispatchWadOffset,
            ExpectedDefaultDispatch,
            "class 0x0125 dispatcher readback");
        VerifyBytes(
            image,
            layout,
            wad,
            dispatchWadOffset + sizeof(uint),
            ExpectedNativeDispatch,
            "adjacent class 0x0126 dispatcher preservation");
        byte[] patchedOverlay = DiscImage.ReadFileBytes(
            image,
            layout,
            wad.Lba,
            overlayOffset,
            overlayLength);
        VerifySha256(
            patchedOverlay,
            ExpectedPatchedOverlaySha256,
            "patched Tree Tops overlay");

        return new MobySourcePatch(
            Label: "Retire Tree Tops opening gem-thief T0 handler",
            Kind: PatchKind,
            LevelKey: level.Key,
            MobyLabel: "Opening gem thief T0 / class 0x0125",
            TrueIndex: OpeningActorTrueIndex,
            RecordOffset: "overlay-dispatch:class-0x0125",
            WadRelativeOffset: $"0x{dispatchWadOffset:X}",
            ImageOffset:
                $"0x{DiscImage.ConvertFileOffsetToImageOffset(layout, wad.Lba, dispatchWadOffset):X}",
            ByteLength: sizeof(uint),
            BeforeHexPreview: ToHex(ExpectedNativeDispatch),
            AfterHexPreview: ToHex(ExpectedDefaultDispatch),
            Description:
                "Disposable Tree Tops load-freeze discriminator only. " +
                "The unique opening actor's class 0x0125 dispatch changes from " +
                "the native route/cinematic handler 0x80082F30 to the dispatcher's " +
                "normal next-actor path 0x80086AEC. The actor row, properties, " +
                "route bytes, class 0x0126 entry, and every other overlay byte remain unchanged.");
    }

    private static void GuardOverlayPreimage(
        FileStream image,
        DiscLayout layout,
        DiscFileRecord wad,
        long overlayOffset,
        int overlayLength)
    {
        if (overlayOffset != ExpectedOverlayWadOffset ||
            overlayLength != ExpectedOverlayByteLength)
        {
            throw new InvalidDataException(
                $"Tree Tops overlay entry {OverlayWadEntry} changed: expected " +
                $"0x{ExpectedOverlayWadOffset:X}/0x{ExpectedOverlayByteLength:X}, " +
                $"found 0x{overlayOffset:X}/0x{overlayLength:X}. No diagnostic write was accepted.");
        }

        byte[] overlay = DiscImage.ReadFileBytes(
            image,
            layout,
            wad.Lba,
            overlayOffset,
            overlayLength);
        VerifySha256(
            overlay,
            ExpectedOverlaySha256,
            "retail Tree Tops overlay preimage");
        VerifyBytes(
            image,
            layout,
            wad,
            overlayOffset + DispatchOverlayFileOffset - sizeof(uint),
            ExpectedDefaultDispatch,
            "class 0x0124 dispatcher");
        VerifyBytes(
            image,
            layout,
            wad,
            overlayOffset + DispatchOverlayFileOffset,
            ExpectedNativeDispatch,
            "class 0x0125 dispatcher preimage");
        VerifyBytes(
            image,
            layout,
            wad,
            overlayOffset + DispatchOverlayFileOffset + sizeof(uint),
            ExpectedNativeDispatch,
            "class 0x0126 dispatcher");
        VerifyBytes(
            image,
            layout,
            wad,
            overlayOffset + HandlerOverlayFileOffset,
            ExpectedHandlerPrefix,
            "native class 0x0125/0x0126 handler prefix");
        VerifyBytes(
            image,
            layout,
            wad,
            overlayOffset + DefaultIterationOverlayFileOffset,
            ExpectedDefaultIterationPrefix,
            "dispatcher next-actor path prefix");
    }

    private static (long Offset, int Length) ReadWadEntry(
        FileStream image,
        DiscLayout layout,
        DiscFileRecord wad,
        int entryIndex)
    {
        byte[] entry = DiscImage.ReadFileBytes(
            image,
            layout,
            wad.Lba,
            checked(entryIndex * 8L),
            8);
        return (
            BinaryPrimitives.ReadUInt32LittleEndian(entry.AsSpan(0, 4)),
            checked((int)BinaryPrimitives.ReadUInt32LittleEndian(
                entry.AsSpan(4, 4))));
    }

    private static void VerifyBytes(
        FileStream image,
        DiscLayout layout,
        DiscFileRecord wad,
        long wadOffset,
        byte[] expected,
        string label)
    {
        byte[] actual = DiscImage.ReadFileBytes(
            image,
            layout,
            wad.Lba,
            wadOffset,
            expected.Length);
        if (!actual.SequenceEqual(expected))
        {
            throw new InvalidDataException(
                $"{label} changed at WAD+0x{wadOffset:X}: expected " +
                $"{ToHex(expected)}, found {ToHex(actual)}. No unsafe diagnostic write was accepted.");
        }
    }

    private static void VerifySha256(
        ReadOnlySpan<byte> bytes,
        string expected,
        string label)
    {
        string actual = Convert.ToHexString(
            SHA256.HashData(bytes)).ToLowerInvariant();
        if (!actual.Equals(expected, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"{label} SHA-256 changed: expected {expected}, found {actual}. " +
                "No unsafe diagnostic write was accepted.");
        }
    }

    private static bool IsWadName(string name) =>
        name.Equals("WAD.WAD", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("WAD", StringComparison.OrdinalIgnoreCase);

    private static string ToHex(IEnumerable<byte> bytes) =>
        string.Join(' ', bytes.Select(value => $"{value:X2}"));
}
