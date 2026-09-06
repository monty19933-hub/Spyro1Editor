using System.Security.Cryptography;

namespace Spyro.Editor.Core.Exporting;

public sealed record NativeId65SurfaceConsumerEvidence(string Id, int ExecutableFileOffset,
    uint RuntimeAddress, int ByteLength, string Sha256);

/// <summary>
/// Native consumer evidence for this detached fixed surface grammar. A full
/// executable/hash capture establishes source identity, not a whole-engine
/// dependency, scene-resource, runtime RAM, no-rescue, or gameplay certificate.
/// </summary>
internal static class NativeId65AuthoredSurfaceConsumers
{
    internal const string ImageSha256 = "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37";
    private const long ImageByteLength = 661547040;
    private const int ExecutableByteLength = 0x66000;
    private const string ExecutableSha256 = "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998";
    private static readonly NativeId65SurfaceConsumerEvidence[] Spans =
    [
        new("surface-component-loader-five-collision-pointers-and-cyclorama", 0x3558, 0x80012D58, 0x4D8,
            "82ceb2a2a0ae9ddcc86caca26ceed4b24e2c6e1187c061750fec4e186f6f7ea3"),
        new("environment-selection-origin-and-four-negative-scene-index-sentinels", 0x161CC, 0x800259CC, 0x6A4,
            "14a4fc173cabab25d5e158af23a6cfcc6f892be7d0262b8187167c9aae1f68be"),
        new("LP-packed-vertices-faces-and-Gouraud-colors", 0x16A3C, 0x8002623C, 0x580,
            "55d17fb751dd5cd000c2de0393cfb5131c8b1609f8fac0e2fa40e6b8ab6c2c28"),
        new("HP-packed-vertices-direct-triangle-FF-Gouraud-and-face-prefetch", 0x16FB8, 0x800267B8, 0xEF0,
            "2591af6f870d5177b750577b8d8a0bf5a30c5e7be53bfdd470d42cdb95ef1a62"),
        new("native-collision-tree-and-compressed-triangle-query", 0x3C64C, 0x8004BE4C, 0xD34,
            "3d19782b4ad17cffbdb5239dcdd6412a9d25bceaccdb1671a302865edb52589e"),
        new("special-surface-descriptor-byte-consumer", 0x47BA4, 0x800573A4, 0x24,
            "027317de8d9827ad9e3f5fe32dc357d1b94774b6ac3abb118886236bf659d59d"),
        new("normal-active-cyclorama-global-copy", 0x4FF8, 0x800147F8, 0x3C,
            "986c74fc0165d7a9deb010e6358748dad1560dd46587040319daaf4fcba884c4"),
        new("grouped-cyclorama-FF-list-and-count-zero-part-loop", 0x3F414, 0x8004EC14, 0xD0,
            "d705f1040bac2ed90500d7f4255f3a112c4500bde3e2f670db9f56a670fccf2a"),
        new("all-parts-cyclorama-count-zero-and-delay-slot-pointer-read", 0x3F874, 0x8004F074, 0x90,
            "7f35996cf5b088adb950356451495ad2ad4cc36541871edab2461d879b15ebcf"),
        new("frame-zero-active-cyclorama-guard", 0xA8E0, 0x8001A0E0, 0x14,
            "33169ab3b5b0c9183354b7475005f25ac6c5d46d524ec3219d75f18650a46eb1")
    ];

    internal static IReadOnlyList<NativeId65SurfaceConsumerEvidence> Capture(ReadOnlySpan<byte> executable)
    {
        if (executable.Length != ExecutableByteLength || Hash(executable) != ExecutableSha256)
            throw new InvalidDataException("Authored surface consumer authority requires the complete exact original USA executable.");
        foreach (var span in Spans)
        {
            if ((long)span.ExecutableFileOffset + 0x8000F800L != span.RuntimeAddress ||
                Hash(executable.Slice(span.ExecutableFileOffset, span.ByteLength)) != span.Sha256)
                throw new InvalidDataException("A native surface consumer span or executable/runtime address binding changed.");
        }
        return Array.AsReadOnly(Spans.ToArray());
    }

    internal static async Task<byte[]> ReadOriginalExecutableAsync(string path, CancellationToken token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        path = Path.GetFullPath(path);
        await using FileStream image = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        await RequireImageAsync(image, token);
        DiscLayout layout = DiscImage.DetectLayout(path);
        if (layout.SectorSize != 2352 || layout.UserOffset != 24)
            throw new InvalidDataException("Authored surface source requires the exact original raw-disc geometry.");
        DiscFileRecord executable = DiscImage.FindRootFileRecord(image, layout,
            n => n.Equals("SCUS_942.28", StringComparison.OrdinalIgnoreCase));
        if (executable.Lba != 53875 || executable.Size != ExecutableByteLength)
            throw new InvalidDataException("Authored surface native executable directory owner changed.");
        byte[] bytes = DiscImage.ReadFileBytes(image, layout, executable.Lba, 0, ExecutableByteLength);
        _ = Capture(bytes);
        await RequireImageAsync(image, token);
        return bytes;
    }

    private static async Task RequireImageAsync(FileStream image, CancellationToken token)
    {
        if (image.Length != ImageByteLength)
            throw new InvalidDataException("Authored surface source image length is not the exact original USA disc.");
        image.Position = 0;
        string hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(image, token));
        if (hash != ImageSha256 || image.Length != ImageByteLength || image.Position != ImageByteLength)
            throw new InvalidDataException("Authored surface source image changed before or during the frozen capture.");
    }

    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
