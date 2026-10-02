using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;

[assembly: InternalsVisibleTo("Spyro.Editor.UniversalHighValueGemSmoke")]

namespace Spyro.Editor.Core.Universal;

/// <summary>Original editor collectibles backed by private copies of the USA purple gem.
/// Retail classes, reward selectors, value encodings and the recent-gem HUD ring are unchanged.</summary>
public static partial class UniversalHighValueGems
{
    public const string SourceKey = "editor";
    public const string TealId = "teal-gem-50", RainbowId = "rainbow-gem-100";
    // Every original USA row, local/shared registration, update dispatch and integer
    // immediate in all 35 overlays/the executable excludes these two IDs.
    public const int TealClass = 0x1CF, RainbowClass = 0x1DA;
    public const int PopupFrames = 45;
    public const string PurplePackageSha256 = "0c7823412b2a1a1df1da587d6bc9d9cde824062d4b9387715075112db241caea";
    internal const uint TealParticleColour = 0x2C808000, RainbowParticleColour = 0x2C808080;

    public static bool IsId(string? id) => id is TealId or RainbowId;
    public static bool IsClass(int classId) => classId is TealClass or RainbowClass;
    public static int ValueForClass(int classId) => classId switch { TealClass => 50, RainbowClass => 100, _ => 0 };
    public static string? IdForClass(int classId) => classId switch { TealClass => TealId, RainbowClass => RainbowId, _ => null };

    public static UniversalSpecialAsset? Find(UniversalSourceLibrary library, string id) =>
        IsId(id) ? Assets(library).Single(a => a.Id == id) : null;

    private static readonly ConditionalWeakTable<UniversalSourceLibrary, AssetCache> CachedAssets = new();
    private sealed record AssetCache(IReadOnlyList<UniversalSpecialAsset> Assets);

    public static IReadOnlyList<UniversalSpecialAsset> Assets(UniversalSourceLibrary library) =>
        CachedAssets.GetValue(library, key => new(BuildAssets(key))).Assets;

    private static IReadOnlyList<UniversalSpecialAsset> BuildAssets(UniversalSourceLibrary library)
    {
        RequireNativeOwner(library.Disc.Exe);
        byte[] purple = library.SharedModelPackage(0x57);
        if (Convert.ToHexStringLower(SHA256.HashData(purple)) != PurplePackageSha256)
            throw new InvalidDataException("High-value gems require the certified original USA purple shared-model package.");
        var donor = library.Get("jacques").Data;
        byte[] row = donor.Row(40);
        if (BinaryPrimitives.ReadUInt16LittleEndian(row.AsSpan(0x36)) != 0x57 || row[0x4F] != 5 || row[0x50] != 0x18)
            throw new InvalidDataException("High-value gems require their original Jacques purple loose-gem row.");
        // The shared simple gem is untextured. SourceData is retained for the normal
        // local-model relocation grammar; no extracted game assets are committed.
        UniversalSpecialAsset Asset(string id, string name, int cls)
        {
            byte[] template = row.ToArray();
            BinaryPrimitives.WriteUInt16LittleEndian(template.AsSpan(0x36), (ushort)cls);
            template[0x4F] = 0; // Simple model has no animation/value frame.
            template[0x53] = 0xFF; // This placed gem is never an enemy reward selector.
            return new(id, name, cls, UniversalSpecialBehavior.HighValueGem,
                Recolour(purple, cls), donor.Vram().ToArray(), donor, template) { SourceKey = SourceKey };
        }
        return [Asset(TealId, "Teal gem (50)", TealClass), Asset(RainbowId, "Rainbow gem (100)", RainbowClass)];
    }

    /// <summary>Recolours every RGB entry of the bounded untextured simple model;
    /// geometry, offsets and read-ahead bytes remain exact. Native profile faces
    /// become admitted indexed-RGB faces, so the authored colours reach the renderer.</summary>
    public static byte[] Recolour(byte[] package, int classId)
    {
        if (!IsClass(classId)) throw new InvalidDataException("Unknown high-value gem class.");
        byte[] admitted = UniversalSharedModelAssets.CountDerivedSimplePackage(package);
        if (admitted.Length != package.Length)
            throw new InvalidDataException("High-value gem simple model contains unowned trailing bytes.");
        byte[] copy = package.ToArray();
        int rgb = BinaryPrimitives.ReadInt32LittleEndian(copy.AsSpan(8));
        int faces = BinaryPrimitives.ReadInt32LittleEndian(copy.AsSpan(12));
        int count = (faces - rgb) / 4;
        if (count <= 0 || rgb < 16 || faces > copy.Length)
            throw new InvalidDataException("High-value gem RGB interval is not owned by its simple model.");
        (byte R, byte G, byte B)[] rainbow = [(240, 48, 64), (240, 144, 32), (240, 224, 48), (48, 224, 80), (32, 208, 224), (64, 96, 240), (176, 64, 240)];
        for (int i = 0; i < count; i++)
        {
            int p = rgb + i * 4;
            int shade = Math.Clamp((int)Math.Max(copy[p], Math.Max(copy[p + 1], copy[p + 2])), 48, 255);
            var tint = classId == TealClass ? ((byte)32, (byte)224, (byte)208) : rainbow[i % rainbow.Length];
            copy[p] = (byte)(tint.Item1 * shade / 255);
            copy[p + 1] = (byte)(tint.Item2 * shade / 255);
            copy[p + 2] = (byte)(tint.Item3 * shade / 255);
        }
        int faceCount = copy[1];
        for (int i = 0; i < faceCount; i++)
        {
            int p = faces + i * 8;
            uint vertices = BinaryPrimitives.ReadUInt32LittleEndian(copy.AsSpan(p));
            // Source profile faces obtain colour through native lighting rather than
            // the white RGB table. Indexed mode0 is the engine's own ordinary RGB
            // path and uses one authored colour per unchanged geometry vertex.
            BinaryPrimitives.WriteUInt32LittleEndian(copy.AsSpan(p), vertices & ~3u);
            BinaryPrimitives.WriteUInt32LittleEndian(copy.AsSpan(p + 4), vertices & ~3u);
        }
        _ = UniversalSharedModelAssets.CountDerivedSimplePackage(copy);
        return copy;
    }

    /// <summary>Authenticates the native placed retirement path and HUD text owners;
    /// award accepts the original helper or the canonical physical/save-row mapping.</summary>
    public static void RequireNativeOwner(byte[] executable)
    {
        if (executable.Length != 0x66000) throw new InvalidDataException("High-value gems require the complete USA executable.");
        void Require(uint address, int bytes, string hash)
        {
            if (Convert.ToHexStringLower(SHA256.HashData(executable.AsSpan((int)(address - UniversalExeBindings.ExeBase), bytes))) != hash)
                throw new InvalidDataException($"High-value gem native owner 0x{address:X8} changed from the certified USA source.");
        }
        Require(0x80053570, 0x98, "2eefe927534faee54464ea9af9e53a9382ff3fafa0f90d50b6a68982dce61ddb");
        Require(0x80053608, 0x9C, "3bf6f2ab0aaba682bc34bb5c4f734e872e144df43022b4e3f389784aa5a11be8");
        Require(0x80052568, 0x64, "602fc31b1351212e5d5c72c39ac59cc86a9afd0226a9a0549192855588498505");
        Require(0x800181AC, 0x2A0, "abd220f600bf905329074305b9ad933a9d2ca24fe8f8902962aa38bad51d4e64");
        Require(0x80018880, 0x80, "820b951358c0ed3f9a032141d0b29987735d2475347764f788072c0b541577a7");
        var mark = NativeMobySaveMap.Windows().Single(w => w.Address == NativeMobySaveMap.MarkAddress);
        ReadOnlySpan<byte> current = executable.AsSpan((int)(mark.Address - UniversalExeBindings.ExeBase), mark.Code.Length);
        if (!current.SequenceEqual(mark.Code) && Convert.ToHexStringLower(SHA256.HashData(current)) != mark.SourceSha256)
            throw new InvalidDataException("High-value gem award helper is neither original nor the certified native save-map image.");
    }
}
