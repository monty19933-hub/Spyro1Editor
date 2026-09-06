using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;

namespace Spyro.Editor.Core.Exporting;

/// <summary>Exact original-source capture, never caller-supplied retail inventory or a new-level header.</summary>
internal sealed class NativeId65NormalResourceSource
{
    internal const string PlayerPackageHash = "46f570bf0ad2d433f8b42874ae40a3e5d7155d7206dddd1cd08a2d4be16e0e76";
    internal const string TownStreamHash = "7c9ca6faf03324ef102fe424e3e60ea7d7b6d54dde9c8382c589f960be9db4e0";
    private const string ExecutableHash = "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998";
    private const string TownDataHash = "7ddbf6d9a6ee6c0f64c8564a89e374ca0ca234ef608ec812aae68e8176de1dc0";
    private readonly byte[] _executable, _overlay, _data, _player;

    private NativeId65NormalResourceSource(byte[] executable, byte[] overlay, byte[] data, byte[] player)
    {
        _executable = executable; _overlay = overlay; _data = data; _player = player;
        Evidence = ValidateCapturedBytes(executable, overlay, data, player);
    }
    internal IReadOnlyList<NativeId65EngineEvidence> Evidence { get; }
    internal byte[] CopyExecutable() => _executable.ToArray();
    internal byte[] CopyOverlay() => _overlay.ToArray();
    internal byte[] CopyData() => _data.ToArray();
    internal byte[] CopyPlayerPackage() => _player.ToArray();

    internal static async Task<NativeId65NormalResourceSource> CaptureAsync(string path, CancellationToken token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        path = Path.GetFullPath(path);
        await using FileStream image = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        await RequireImageAsync(image, token);
        DiscLayout layout = DiscImage.DetectLayout(path);
        DiscFileRecord wad = DiscImage.FindRootFileRecord(image, layout,
            n => n.Equals("WAD.WAD", StringComparison.OrdinalIgnoreCase));
        DiscFileRecord exe = DiscImage.FindRootFileRecord(image, layout,
            n => n.Equals("SCUS_942.28", StringComparison.OrdinalIgnoreCase));
        if (layout.SectorSize != 2352 || layout.UserOffset != 24 || wad.Lba != 37 || wad.Size != 0x6927000 ||
            exe.Lba != 53875 || exe.Size != 0x66000)
            throw new InvalidDataException("Normal resources require exact native WAD/EXE directory and raw-sector owners.");
        byte[] directory = DiscImage.ReadFileBytes(image, layout, 37, 0, 0x800);
        RequireHash(directory, "15d3e9c45f07e27a0ddd3f3a49b23a077e2c7750021cc89c55b7b16c551186cd", "WAD directory");
        byte[] peteHeader = DiscImage.ReadFileBytes(image, layout, 37, 0x7B8800, 0x800);
        if (Word(peteHeader, 0) != 0x39C48 || Word(peteHeader, 4) != 0x800 ||
            Word(peteHeader, 8) != 0 || Word(peteHeader, 12) != 0x3361C)
            throw new InvalidDataException("Shared PETE player owner or following registered root changed.");
        var source = new NativeId65NormalResourceSource(
            DiscImage.ReadFileBytes(image, layout, 53875, 0, 0x66000),
            DiscImage.ReadFileBytes(image, layout, 37, 0x118E800, 0xF800),
            DiscImage.ReadFileBytes(image, layout, 37, 0x119E000, 0x2E2000),
            DiscImage.ReadFileBytes(image, layout, 37, 0x7B9000, 0x32E1C));
        await RequireImageAsync(image, token);
        return source;
    }

    internal static IReadOnlyList<NativeId65EngineEvidence> ValidateCapturedBytes(ReadOnlySpan<byte> executable,
        ReadOnlySpan<byte> overlay, ReadOnlySpan<byte> data, ReadOnlySpan<byte> player)
    {
        if (executable.Length != 0x66000 || overlay.Length != 0xF800 || data.Length != 0x2E2000 || player.Length != 0x32E1C)
            throw new InvalidDataException("Native resource source owners have unsupported complete lengths.");
        RequireHash(executable, ExecutableHash, "complete executable");
        RequireHash(overlay, "9bc923cc8d27703537b81b01f51fde351e58aabee87d38a8afc630f1031878a5", "complete Town overlay");
        RequireHash(data, TownDataHash, "complete Town level data");
        RequireHash(player, PlayerPackageHash, "complete resident player package");
        RequireHash(data.Slice(0x173000, 0xCC30), TownStreamHash, "Town player root0 allocation");
        if (Word(data, 0x50) != 0x173000 || Word(data, 0x54) != 0x17FC30 ||
            BinaryPrimitives.ReadUInt16LittleEndian(data[0x150..]) != 0 || Word(player, 0) != 46 || Word(player, 0x34) != 0xAA8)
            throw new InvalidDataException("Special player root ownership differs from native header/geometry grammar.");
        List<NativeId65EngineEvidence> evidence = [];
        foreach (var (id, pc, length, hash) in new[]
        {
            ("terrain-texture-count-loader-not-scene-consumer-authority", 0x80012D58u, 0x54, "25aede725485af9da35013da9c1adbcb8d4f336c588c272e29edb6c7115aca0c"),
            ("scene-animation-and-scroll-updater-with-speculative-list-read", 0x8002A6FCu, 0x36C, "3adae8c5a1749c4f6277e109ebbcec040eb269300ba7774226a3a075ed5eea3c"),
            ("scene-animation-reset-and-scroll-consumer", 0x8002B4A8u, 0x258, "b4756ae277eb986e027750309bb0444701842e04d0fad5622df948c3c46da773"),
            ("scene-owned-scroll-list-loader", 0x80013CC0u, 0xC0, "15bd30cb6ce91f3e08174633554141f41571cf5e1b20914aaf366735ac1b17e2"),
            ("kind2-particle-frame-low-byte-eight-byte-stride-renderer", 0x80057C08u, 0x254, "229d60dcd2291594588714e49166ad4d7a43416a9a62ef1764ceef4ee9ba021f"),
            ("generic-model-local-descriptor-resolver", 0x8003851Cu, 0x68, "f3cc3c1ef39b62f67547e76d6f268b2162504abf64b93690fc435c987416acd9"),
            ("player-global-sound-selectors-not-local-model-slots", 0x8004A638u, 0x10C, "85b931efc214ddfbe44e3a893e036bbceb09e7a15e0e2007d333a5ba323132ca"),
            ("special-player-stream-loader-and-resident-reset-boundary", 0x80013230u, 0x1B0, "0cca46f0e44c16b8adea088e3081637ec5b04a512dc2c8eac494f9d16732cefb"),
            ("native-raw-zero-portal-count", 0x8001308Cu, 0x30, "74d75f32860163db6441f170a40bf4f0da77bbcb5edabfab766ff9129ada8b3e"),
            ("particle-signed-ID-payload-table-loader", 0x800131A4u, 0x4C, "3115e1d9d7b01e036276015b0af24eeba63b0d1dae27dcd48a42f90a784fa19a")
        })
        {
            int offset = checked((int)(pc - 0x8000F800));
            RequireHash(executable.Slice(offset, length), hash, id);
            evidence.Add(new(id, "EXE", offset, length, hash));
        }
        foreach (var (id, pc, length, hash) in new[]
        {
            ("companion0C-unconditional-frame-zero-initializer", 0x800892C0u, 0x148, "c0966d0467e8b8c34566ed92d9223cc21cd4e568237d2993762fd9ce7490fce4"),
            ("companion0C-tick-preserves-frame-byte", 0x800881E8u, 0x74, "5de94cabf94c9014ae69e2241e7703d001ccb063c87951e0a4d1011b49f8b5f2"),
            ("Town-tick-cursor-is-raw-particle-plus-one", 0x80087E20u, 0x90, "9e4ac3dd36b64ded965468ffbc7d80544805cab1e99a40703567408a4b4ddb3b")
        })
        {
            int offset = checked((int)(pc - 0x8007AA38));
            RequireHash(overlay.Slice(offset, length), hash, id);
            evidence.Add(new(id, "WAD", 0x118E800L + offset, length, hash));
        }
        return evidence.AsReadOnly();
    }

    private static async Task RequireImageAsync(FileStream image, CancellationToken token)
    {
        if (image.Length != 661547040)
            throw new InvalidDataException("Native resources require the complete original USA image.");
        image.Position = 0;
        string digest = Convert.ToHexStringLower(await SHA256.HashDataAsync(image, token));
        if (digest != NativeId65EngineProfileContract.OriginalImageSha256 || image.Length != 661547040 || image.Position != 661547040)
            throw new InvalidDataException("Native resource source changed before/during frozen capture.");
    }
    internal static void RequireHash(ReadOnlySpan<byte> bytes, string hash, string name)
    {
        if (Hash(bytes) != hash) throw new InvalidDataException("Changed native resource owner: " + name);
    }
    internal static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static uint Word(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
}
