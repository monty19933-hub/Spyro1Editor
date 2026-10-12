using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
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
    public const int ModelScale = 2, IdleSparkleFrames = 32, IdleSparkleLimit = 8, IdleSparkleRadius = 0x1000;
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
        if (BinaryPrimitives.ReadUInt16LittleEndian(row.AsSpan(0x36)) != 0x57 || row[0x4F] != 5 || row[0x50] != 0x18 || (row[0x4E] & 0xE0) != 0)
            throw new InvalidDataException("High-value gems require their original Jacques purple loose-gem row.");
        // The shared simple gem is untextured. SourceData is retained for the normal
        // local-model relocation grammar; no extracted game assets are committed.
        byte[] model = ShinyModel(purple);
        UniversalSpecialAsset Asset(string id, string name, int cls)
        {
            byte[] template = row.ToArray();
            BinaryPrimitives.WriteUInt16LittleEndian(template.AsSpan(0x36), (ushort)cls);
            // +0x4F selects the gem's colour pair in the renderer's shine table (with +0x4E's clear top bits):
            // teal owns entry 0; the rainbow gem starts red and its update walks the six hues.
            template[0x4F] = cls == RainbowClass ? (byte)RainbowFirstEntry : (byte)TealEntry;
            template[0x53] = 0xFF; // This placed gem is never an enemy reward selector.
            return new(id, name, cls, UniversalSpecialBehavior.HighValueGem,
                model.ToArray(), donor.Vram().ToArray(), donor, template) { SourceKey = SourceKey };
        }
        return [Asset(TealId, "Teal gem (50)", TealClass), Asset(RainbowId, "Rainbow gem (100)", RainbowClass)];
    }

    // Native shine. Every face of the five retail gems carries flags 3 and its own signed-byte normal instead of
    // RGB indices. The simple-model renderer (0x80023720) lights such a face from that normal and draws it flat,
    // colour = base + highlight x intensity, plus white above intensity 0x5C0 (the glint); front faces near the
    // camera are semi-transparent over dimmer opaque back faces. The pair is read from the executable's table
    // 0x8006E44C at byte offset (moby+0x4C)>>21, i.e. 8 x moby+0x4F: entries 1-5 are red, green, blue, gold and
    // purple. Entry 0 is zero and no placed USA object uses it, so the update writes the teal pair there.
    public const uint ShineTable = 0x8006E44C, CosineTable = 0x8006CC78, SineTable = 0x8006CBF8, FrameStep = 0x800756CC;
    public const uint TealShineBase = 0x00485000, TealShineHighlight = 0x00E8FF00; // 0x00BBGGRR: teal 00/50/48 to 00/FF/E8
    public const int TealEntry = 0, RainbowFirstEntry = 1, PhaseByte = 8, PhaseSeedShift = 4;
    /// <summary>Rainbow hue order as table entries, one per nibble: red, gold, green, teal, blue, purple.</summary>
    public const uint RainbowHueOrder = 0x00530241;
    public const int RainbowHues = 6;
    internal const string ShineTableSha256 = "3b2e3e3dd0f0b17cc6a0869cc4bc4005a93da4a8cb902136e340a1d42efa8492", SineCosineSha256 = "795a93bee7b4d49bef89d4df8c8628a4b4b3a91d694053dbc2b26b4293d4fa55";
    internal const string ShineRendererSha256 = "c9cef2546764b5b14a58c4d9bc33a938512d378758258b97a469ef900f80cfa8";

    /// <summary>The colour pair (base, highlight) the renderer reads for a shine-table entry 0-5.</summary>
    public static (uint Base, uint Highlight) ShinePair(int entry) => entry switch
    {
        TealEntry => (TealShineBase, TealShineHighlight),
        1 => (0x00000050, 0x000000FF), 2 => (0x00005000, 0x0000FF00), 3 => (0x00500000, 0x00FF0000),
        4 => (0x00005050, 0x0000FFFF), 5 => (0x00500050, 0x00FF00FF),
        _ => throw new ArgumentOutOfRangeException(nameof(entry))
    };

    /// <summary>The shine-table entry a rainbow gem shows at wobble phase 0-255 (six hues per wobble turn).</summary>
    public static int RainbowEntry(int phase) => (int)(RainbowHueOrder >> (((phase & 0xFF) * RainbowHues >> 8) * 4) & 0xF);

    /// <summary>Doubles the signed-byte vertex coordinates of the bounded untextured simple purple model and keeps
    /// its eighteen native shine faces (flags 3, one signed-byte normal each) and its RGB table byte for byte, so the
    /// gem is lit and glints exactly like the retail gems. Its colour comes from the row's shine-table entry.</summary>
    public static byte[] ShinyModel(byte[] package)
    {
        byte[] admitted = UniversalSharedModelAssets.CountDerivedSimplePackage(package);
        if (admitted.Length != package.Length)
            throw new InvalidDataException("High-value gem simple model contains unowned trailing bytes.");
        byte[] copy = package.ToArray();
        int vertexStart = BinaryPrimitives.ReadInt32LittleEndian(copy.AsSpan(4));
        int vertexEnd = checked(vertexStart + unchecked((sbyte)copy[0]) * 3);
        for (int p = vertexStart; p < vertexEnd; p++)
        {
            int scaled = unchecked((sbyte)copy[p]) * ModelScale;
            if (scaled < sbyte.MinValue || scaled > sbyte.MaxValue)
                throw new InvalidDataException("High-value gem doubled vertex exceeds its native signed-byte coordinate.");
            copy[p] = unchecked((byte)(sbyte)scaled);
        }
        _ = ShineFaces(copy);
        return copy;
    }

    /// <summary>A shine face: its 3 or 4 vertex indices and its signed-byte normal in vertex-byte order.</summary>
    public sealed record ShineFace(int[] Vertices, int NormalX, int NormalY, int NormalZ);

    /// <summary>Strict reader of a simple model whose faces are all native shine faces: negative header, vertices at
    /// root+0x10 (no normal prefix), flags 3 on every face, owned vertex indices and a non-zero normal per face.</summary>
    public static (IReadOnlyList<(int X, int Y, int Z)> Vertices, IReadOnlyList<ShineFace> Faces) ShineFaces(byte[] package)
    {
        if (UniversalSharedModelAssets.CountDerivedSimplePackage(package).Length != package.Length)
            throw new InvalidDataException("Shine model has unowned trailing bytes.");
        int vertices = unchecked((sbyte)package[0]), faces = package[1];
        int vertexStart = BinaryPrimitives.ReadInt32LittleEndian(package.AsSpan(4)), faceStart = BinaryPrimitives.ReadInt32LittleEndian(package.AsSpan(12));
        if (vertexStart != 0x10) throw new InvalidDataException("Shine model needs its vertices at root+0x10.");
        var v = Enumerable.Range(0, vertices).Select(i => (unchecked((sbyte)package[0x10 + 3 * i]) * 2,
            unchecked((sbyte)package[0x11 + 3 * i]) * 2, unchecked((sbyte)package[0x12 + 3 * i]) * 2)).ToArray();
        List<ShineFace> list = [];
        for (int f = 0; f < faces; f++)
        {
            uint word = BinaryPrimitives.ReadUInt32LittleEndian(package.AsSpan(faceStart + 8 * f));
            uint normal = BinaryPrimitives.ReadUInt32LittleEndian(package.AsSpan(faceStart + 8 * f + 4));
            if ((word & 3) != 3) throw new InvalidDataException("Every high-value gem face must be a native shine face.");
            int[] idx = [(int)(word >> 23 & 127), (int)(word >> 16 & 127), (int)(word >> 9 & 127), (int)(word >> 2 & 127)];
            if (idx.Any(i => i >= vertices)) throw new InvalidDataException("Shine face references an unowned vertex.");
            // The renderer (0x80023784) loads IR1 = bits 16-23, IR2 = bits 8-15, IR3 = bits 24-31, matching vertex bytes
            // 1, 2 and 0 (0x80023010); returned here in vertex-byte order like the vertices.
            int nx = unchecked((sbyte)(normal >> 24)), ny = unchecked((sbyte)(normal >> 16)), nz = unchecked((sbyte)(normal >> 8));
            if (nx == 0 && ny == 0 && nz == 0) throw new InvalidDataException("Shine face has no normal.");
            list.Add(new(idx[2] == idx[3] ? idx[..3] : idx, nx, ny, nz));
        }
        return (v, list);
    }

    /// <summary>Editor preview of a shiny gem: the same geometry as a flat indexed-RGB simple model (one RGB word per
    /// face), each face shaded by the native shine formula under one fixed light. Rainbow faces walk its six hues.
    /// The result passes <see cref="NativeSimpleGouraudModelGeometry"/>; it is never exported.</summary>
    public static byte[] PreviewPackage(int classId, byte[] shiny)
    {
        if (!IsClass(classId)) throw new InvalidDataException("Unknown high-value gem class.");
        var (_, faces) = ShineFaces(shiny);
        int vertices = unchecked((sbyte)shiny[0]);
        int rgb = UniversalLevelData.Align(0x10 + vertices * 3, 4), faceStart = rgb + faces.Count * 4;
        int oldFaces = BinaryPrimitives.ReadInt32LittleEndian(shiny.AsSpan(12));
        byte[] preview = new byte[faceStart + faces.Count * 8 + 8];
        shiny.AsSpan(0, rgb).CopyTo(preview);
        BinaryPrimitives.WriteInt32LittleEndian(preview.AsSpan(8), rgb);
        BinaryPrimitives.WriteInt32LittleEndian(preview.AsSpan(12), faceStart);
        (double X, double Y, double Z) light = (0.5, 0.6, -0.62); // toward the preview camera and its top
        for (int f = 0; f < faces.Count; f++)
        {
            var face = faces[f];
            double length = Math.Sqrt(face.NormalX * face.NormalX + face.NormalY * face.NormalY + face.NormalZ * face.NormalZ);
            double dot = Math.Max(0, (face.NormalX * light.X + face.NormalY * light.Y + face.NormalZ * light.Z) / length);
            int intensity = (int)(dot * 0x7F0);
            var (lo, hi) = ShinePair(classId == TealClass ? TealEntry : (int)(RainbowHueOrder >> (f % RainbowHues * 4) & 0xF));
            uint colour = 0;
            for (int shift = 0; shift < 24; shift += 8)
            {
                int channel = (int)(lo >> shift & 255) * 16 + (intensity * (int)(hi >> shift & 255) * 16 >> 12) + Math.Max(0, intensity - 0x5C0) * 8;
                colour |= (uint)Math.Clamp(channel >> 4, 0, 255) << shift;
            }
            BinaryPrimitives.WriteUInt32LittleEndian(preview.AsSpan(rgb + 4 * f), colour);
            uint word = BinaryPrimitives.ReadUInt32LittleEndian(shiny.AsSpan(oldFaces + 8 * f)) & ~3u;
            uint same = (uint)(f << 23 | f << 16 | f << 9 | f << 2);
            BinaryPrimitives.WriteUInt32LittleEndian(preview.AsSpan(faceStart + 8 * f), word);
            BinaryPrimitives.WriteUInt32LittleEndian(preview.AsSpan(faceStart + 8 * f + 4), same);
        }
        _ = NativeSimpleGouraudModelGeometry.Decode(preview);
        return preview;
    }

    /// <summary>Authenticates native placed retirement, the amount-to-numeral popup and the shine renderer, colour and
    /// wobble tables; award accepts the original helper or the canonical physical/save-row mapping.</summary>
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
        Require(0x800557CC, 0x190, "ede979bdf91a0602de89166b225633adf582a1c1a4eabd32e81f131271723709");
        // Shine and wobble: the simple-model face renderer, its six-entry colour table (entry 0 still zero) and the
        // sine/cosine table the retail gem wobble reads.
        Require(0x80023268, 0x6F0, ShineRendererSha256);
        Require(ShineTable, 48, ShineTableSha256);
        Require(SineTable, 640, SineCosineSha256);
        var mark = NativeMobySaveMap.Windows().Single(w => w.Address == NativeMobySaveMap.MarkAddress);
        ReadOnlySpan<byte> current = executable.AsSpan((int)(mark.Address - UniversalExeBindings.ExeBase), mark.Code.Length);
        if (!current.SequenceEqual(mark.Code) && Convert.ToHexStringLower(SHA256.HashData(current)) != mark.SourceSha256)
            throw new InvalidDataException("High-value gem award helper is neither original nor the certified native save-map image.");
    }
}
