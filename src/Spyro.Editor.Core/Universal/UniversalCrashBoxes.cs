using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Spyro.Editor.Core.Crash;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Universal;

/// <summary>Crash Bandicoot boxes for any ground level: crates that break like baskets (flame, charge or a jump
/// on top) and drop a gem, TNT that counts down 3-2-1 after a jump on it and explodes at once when flamed or
/// charged, and Nitro that explodes on any touch and takes Spyro's last life point. Box faces come from the
/// user's Crash Bandicoot: Warped disc; the cube model keeps Dark Hollow's basket package layout and its
/// collision block, so the engine's own flame/charge contact and standing-on-top work unchanged.</summary>
public static class UniversalCrashBoxes
{
    public const string SourceKey = "crash";
    public const string TntId = "crash-tnt", NitroId = "crash-nitro";
    public const int TntClass = 0x129, Tnt3Class = 0x133, Tnt2Class = 0x13F, Tnt1Class = 0x14E, NitroClass = 0x152;
    public const int BasketClass = 0xC2;
    public const string BasketPackageSha256 = "79bc229395e861933e0dc9e2acb3114c4ae7e0250058722f57775b6a973c1391";

    // Class IDs: not in the shared model table, no retail level registers, places or dispatches them, and no
    // executable or overlay instruction uses them as an immediate (Spyro.Editor.CrashBoxSmoke proves this against the original disc).
    /// <summary>Crates by contents; the reward byte is the native enemy reward (0xFF = a butterfly).</summary>
    public static readonly (string Id, string Name, int Class, byte Reward)[] Crates =
    [
        ("crash-crate", "Crash crate (butterfly)", 0x112, 0xFF),
        ("crash-crate-red", "Crash crate (red gem)", 0x114, 0x53),
        ("crash-crate-green", "Crash crate (green gem)", 0x117, 0x54),
        ("crash-crate-blue", "Crash crate (blue gem)", 0x11A, 0x55),
        ("crash-crate-gold", "Crash crate (gold gem)", 0x123, 0x56),
        ("crash-crate-purple", "Crash crate (purple gem)", 0x124, 0x57),
    ];

    public static IReadOnlyList<int> CrateClasses => Crates.Select(c => c.Class).ToArray();
    public static IReadOnlyList<int> TntClasses => [TntClass, Tnt3Class, Tnt2Class, Tnt1Class];
    public static IReadOnlyList<int> AllClasses => [.. CrateClasses, .. TntClasses, NitroClass];

    public static bool IsId(string? id) => id != null && (id == TntId || id == NitroId || Crates.Any(c => c.Id == id));
    public static bool IsClass(int classId) => AllClasses.Contains(classId);
    public static bool IsPlaceableClass(int classId) => CrateClasses.Contains(classId) || classId is TntClass or NitroClass;

    public static string? IdForClass(int classId) =>
        classId == TntClass ? TntId : classId == NitroClass ? NitroId : Crates.FirstOrDefault(c => c.Class == classId).Id;

    public static string? NameForClass(int classId) =>
        classId == TntClass ? "TNT crate" : classId == NitroClass ? "Nitro crate" : Crates.FirstOrDefault(c => c.Class == classId).Name;

    /// <summary>Treasure the box adds to its level's total (a crate's gem).</summary>
    public static int TreasureValue(int classId)
    {
        var crate = Crates.FirstOrDefault(c => c.Class == classId);
        return crate.Id != null && GemValue.TryFromIdByte(crate.Reward, out var gem) ? gem.Value : 0;
    }

    private enum Kind { Crate, Tnt, Tnt3, Tnt2, Tnt1, Nitro }

    // Box faces: (model, index within the model's distinct textures) in the Crash box page Cr10T.
    private static readonly (string Model, int Index)[] Faces =
    [
        ("BF10G", 6),   // 0 crate (orange X)
        ("BT10G", 0),   // 1 TNT side
        ("BT10G", 2),   // 2 TNT / countdown top (red X)
        ("BT40G", 0),   // 3 "3"
        ("BT30G", 0),   // 4 "2"
        ("BT20G", 0),   // 5 "1"
        ("Bn10G", 0),   // 6 NITRO side
        ("Bn10G", 2),   // 7 Nitro top (green X)
    ];
    private const string BoxPage = "Cr10T";
    private const int TexturePage = 8;              // VRAM x 512, y 0, 4 bpp
    private const int ClutRow = 480;                // palettes at VRAM (512, 480 + face)

    private sealed record Cache(string DiscPath, IReadOnlyList<UniversalSpecialAsset> Assets);
    private static readonly ConditionalWeakTable<UniversalSourceLibrary, Cache> Cached = new();

    public static IReadOnlyList<UniversalSpecialAsset> Assets(UniversalSourceLibrary library, CrashWarpedDisc disc)
    {
        if (Cached.TryGetValue(library, out var hit) && hit.DiscPath == disc.ImagePath) return hit.Assets;
        var assets = BuildAssets(library, disc);
        Cached.AddOrUpdate(library, new(disc.ImagePath, assets));
        return assets;
    }

    private static IReadOnlyList<UniversalSpecialAsset> BuildAssets(UniversalSourceLibrary library, CrashWarpedDisc disc)
    {
        var dark = library.Get("darkhollow").Data;
        byte[] basket = dark.Package(BasketClass);
        RequireBasketTemplate(basket);
        int basketRow = Enumerable.Range(0, dark.Scene().RowCount).First(i => BinaryPrimitives.ReadUInt16LittleEndian(dark.Row(i).AsSpan(0x36)) == BasketClass);
        byte[] row = dark.Row(basketRow);

        var nsf = disc.BoxLevel();
        var faces = Faces.Select(f =>
        {
            var list = nsf.ModelTextures(f.Model, BoxPage);
            var t = f.Index < list.Count ? list[f.Index] : throw new InvalidDataException($"Crash box face {f.Model}/{f.Index} is missing.");
            return t.Width == 32 && t.Height == 32 ? t : throw new InvalidDataException($"Crash box face {f.Model}/{f.Index} is not 32x32.");
        }).ToArray();
        byte[] vram = new byte[0x80000];
        for (int i = 0; i < faces.Length; i++)
        {
            for (int y = 0; y < 32; y++)
                faces[i].Texels.AsSpan(y * 16, 16).CopyTo(vram.AsSpan((i / 8 * 32 + y) * 1024 + i % 8 * 16));
            faces[i].Clut.CopyTo(vram.AsSpan((ClutRow + i) * 1024));
        }

        var sounds = SoundSource(dark, nsf);
        UniversalSpecialAsset Asset(string id, string name, int cls, Kind kind, byte reward)
        {
            byte[] template = row.ToArray();
            BinaryPrimitives.WriteUInt16LittleEndian(template.AsSpan(0x36), (ushort)cls);
            template[0x53] = reward;
            byte[] package = Package(basket, kind, faces);
            // Sound slots: 0 = break or explosion, 1 = the TNT beep or the Nitro rattle (indices into SoundSource).
            package.AsSpan(4, 16).Fill(0xFF);
            (package[4], package[5]) = kind switch
            {
                Kind.Crate => ((byte)0, (byte)0xFF), Kind.Nitro => ((byte)3, (byte)4), _ => ((byte)1, (byte)2),
            };
            return new(id, name, cls, UniversalSpecialBehavior.CrashBox, package, vram, sounds, template) { SourceKey = SourceKey };
        }
        List<UniversalSpecialAsset> list = [.. Crates.Select(c => Asset(c.Id, c.Name, c.Class, Kind.Crate, c.Reward))];
        var countdown = new[] { (Tnt3Class, Kind.Tnt3), (Tnt2Class, Kind.Tnt2), (Tnt1Class, Kind.Tnt1) }
            .Select(k => Asset($"crash-tnt-{k.Item2.ToString().ToLowerInvariant()[3..]}", "TNT crate (counting)", k.Item1, k.Item2, 0xFF)).ToArray();
        list.Add(Asset(TntId, "TNT crate", TntClass, Kind.Tnt, 0xFF) with { Companions = countdown });
        list.Add(Asset(NitroId, "Nitro crate", NitroClass, Kind.Nitro, 0xFF));
        return list;
    }

    /// <summary>The destination must be a ground level whose particle table has the flash/smoke (2) and spark
    /// (70) effects, and the executable routines the boxes call must be the original USA code.</summary>
    public static void RequireDestination(UniversalOverlay destination, byte[] executable)
    {
        if (executable.Length != 0x66000) throw new InvalidDataException("Crash boxes need the complete USA executable.");
        void Require(uint address, int bytes, string hash, string what)
        {
            if (Convert.ToHexStringLower(SHA256.HashData(executable.AsSpan((int)(address - UniversalExeBindings.ExeBase), bytes))) != hash)
                throw new InvalidDataException($"Crash boxes need the original {what} (0x{address:X8}).");
        }
        Require(0x8003851C, 0xA0, "af902cc21e59f2794bc459d6cfaf528f36c28b73f39fc4221c1fe6d384331e91", "model sound routine");
        Require(0x8003ABC0, 0x500, "12a522887f1e55fc228acc3c5b5a748485b060c1bea0530eb93d2a729737d496", "reward drop routine");
        Require(UniversalNativePlayerDamageCalls.Helper, 0xE0, UniversalNativePlayerDamageCalls.HelperSha256, "player damage test");
        Require(0x8004E3C8, 0x7A0, "693a336fb88b9f8943ee08c0b93ebae4f2f2b81c6dc0c414d1a3e258d78b8a90", "object contact routine");
        UniversalHighValueGems.RequireNativeOwner(executable);
        var delete = Spyro.Editor.Core.Exporting.NativeMobySaveMap.Windows().Single(w => w.Address == Spyro.Editor.Core.Exporting.NativeMobySaveMap.DeleteAddress);
        ReadOnlySpan<byte> current = executable.AsSpan((int)(delete.Address - UniversalExeBindings.ExeBase), delete.Code.Length);
        if (!current.SequenceEqual(delete.Code) && Convert.ToHexStringLower(SHA256.HashData(current)) != delete.SourceSha256)
            throw new InvalidDataException("Crash boxes need the original object removal routine (0x8003B7C0) or its save-map image.");
        var types = ParticleTypes(destination);
        if (types == null || !types.Contains(2) || !types.Contains(70))
            throw new InvalidDataException("Crash boxes can only be placed in ground levels (this level has no explosion effects).");
    }

    /// <summary>The particle types the level's own spawn routine implements (its 81-entry jump table minus the
    /// out-of-range case), or null when the level has no such routine (flight levels).</summary>
    public static IReadOnlySet<int>? ParticleTypes(UniversalOverlay o)
    {
        for (uint at = UniversalOverlay.Base; at + 0x20 <= o.End; at += 4)
        {
            uint w = o.Word(at);
            if (UniversalMips.Op(w) != 11 || UniversalMips.Imm(w) != 0x51) continue;          // sltiu rt, rs, 81
            uint branch = o.Word(at + 4);
            if (UniversalMips.Op(branch) != 4 || UniversalMips.Rt(branch) != 0) continue;       // beqz rt, default
            uint fallback = UniversalMips.BranchTarget(branch, at + 4);
            uint? high = null;
            for (uint k = at + 8; k < at + 0x20; k += 4)
            {
                uint x = o.Word(k);
                if (UniversalMips.Op(x) == 15 && UniversalMips.Rt(x) == 1) high = (uint)UniversalMips.Imm(x) << 16;
                else if (UniversalMips.Op(x) == 35 && UniversalMips.Rs(x) == 1 && high is uint h)
                {
                    uint table = unchecked(h + (uint)UniversalMips.SImm(x));
                    if (!o.Contains(table) || !o.Contains(table + 80 * 4)) break;
                    return Enumerable.Range(0, 81).Where(i => o.Word(table + (uint)i * 4) != fallback).ToHashSet();
                }
            }
        }
        return null;
    }

    /// <summary>Crash 3's own box sounds, by name in the box level file: crate break, TNT explosion, TNT countdown
    /// beep, Nitro explosion, Nitro rattle (identified by length and shape: the beep is a steady 1 kHz tone of
    /// 0.12 s, the explosions are long noise bursts). Crash plays them at 11025 Hz (SPU pitch 0x400).</summary>
    public static readonly string[] SoundNames = ["brk0A", "tnt0A", "Ttb0A", "nbr0A", "nbu0A"];
    private const ushort SoundPitch = 0x400;

    /// <summary>A copy of Dark Hollow's data whose first sound descriptors and SPU bank hold the Crash sounds, so the
    /// editor's ordinary sound import copies them into the destination like any imported object's sounds.</summary>
    private static UniversalLevelData SoundSource(UniversalLevelData dark, CrashNsf nsf)
    {
        byte[] data = dark.Bytes.ToArray();
        var layout = new Spyro.Editor.Core.Exporting.NativeLevelSoundLayout(data);
        int count = layout.Sound.Descriptors.Count, bank = layout.Sound.BankByteLength, cursor = 0;
        if (count < SoundNames.Length) throw new InvalidDataException("Dark Hollow's sound table is too small to carry the Crash sounds.");
        byte[][] samples = [.. SoundNames.Select(nsf.Sound)];
        data.AsSpan(layout.BankOffset, bank).Clear();
        int[] offsets = new int[samples.Length];
        for (int i = 0; i < samples.Length; i++)
        {
            if (cursor + samples[i].Length > bank) throw new InvalidDataException("The Crash sounds do not fit a sound bank.");
            offsets[i] = cursor;
            samples[i].CopyTo(data, layout.BankOffset + cursor);
            cursor += UniversalLevelData.Align(samples[i].Length, 16);
        }
        for (int i = 0; i < count; i++)
        {
            // A plain one-shot: sample offset, no flags, Spyro's usual volume byte, the pitch, no extras. Descriptors
            // past the Crash sounds repeat the first one so the table keeps its length.
            var d = data.AsSpan(layout.SoundOffset + 0x108 + i * 20, 20);
            d.Clear();
            BinaryPrimitives.WriteInt32LittleEndian(d, offsets[i < samples.Length ? i : 0]);
            d[8] = 0x32;
            BinaryPrimitives.WriteUInt16LittleEndian(d[10..], SoundPitch);
        }
        var check = new Spyro.Editor.Core.Exporting.NativeLevelSoundLayout(data);
        if (check.Sound.Descriptors.Take(samples.Length).Any(d => !d.IsSupportedOneShot))
            throw new InvalidDataException("The Crash sounds did not become native one-shot sounds.");
        return new UniversalLevelData(data);
    }

    private static void RequireBasketTemplate(byte[] basket)
    {
        int md = BinaryPrimitives.ReadInt32LittleEndian(basket.AsSpan(0x34)), anim = BinaryPrimitives.ReadInt32LittleEndian(basket.AsSpan(0x38));
        uint trailer = BinaryPrimitives.ReadUInt32LittleEndian(basket.AsSpan(0x14));
        if (Convert.ToHexStringLower(SHA256.HashData(basket)) != BasketPackageSha256 || basket.Length != 1000 || BinaryPrimitives.ReadInt32LittleEndian(basket) != 1 || md != 0x68 || anim != 0x3C || trailer != 0x800003D0 ||
            BinaryPrimitives.ReadUInt16LittleEndian(basket.AsSpan(0x3D0)) != 0x2E0)
            throw new InvalidDataException("Crash boxes need Dark Hollow's original basket model as their template.");
    }

    // Cube: 0..3 at the base (z = 0), 4..7 on top (model z is negative upwards). Each vertex word stores
    // x's low bit as y's sign and y's low bit as z's sign, so coordinates are nudged by one unit to match.
    private const int Half = 368, Height = 736;

    private static (int X, int Y, int Z)[] Vertices()
    {
        List<(int, int, int)> v = [];
        foreach (int z in new[] { 0, -Height })
            foreach (var (sx, sy) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
            {
                int y = sy * Half + (z < 0 ? sy : 0);
                int x = sx * Half;
                if (y < 0 && x % 2 == 0) x += sx;
                if (y >= 0 && x % 2 != 0) x -= sx;
                v.Add((x, y, z));
            }
        return [.. v];
    }

    private static uint VertexWord((int X, int Y, int Z) v)
    {
        if ((v.X & 1) != (v.Y < 0 ? 1 : 0) || (v.Y & 1) != (v.Z < 0 ? 1 : 0) || v.Z % 4 != 0)
            throw new InvalidOperationException("Crash box vertex does not satisfy the packed sign bits.");
        return (uint)((v.X & 0x7FF) << 21 | (v.Y & 0x7FF) << 11 | (v.Z >> 1 & 0xFFF));
    }

    /// <summary>The cube in the basket's layout: header, animation record and collision trailer stay; faces,
    /// colours and vertices are rewritten between the model data start and the trailer.</summary>
    private static byte[] Package(byte[] template, Kind kind, CrashTexture[] faces)
    {
        (int side, int top) = kind switch
        {
            Kind.Crate => (0, 0), Kind.Tnt => (1, 2), Kind.Tnt3 => (3, 2), Kind.Tnt2 => (4, 2), Kind.Tnt1 => (5, 2), _ => (6, 7),
        };
        byte[] Field(int face, bool upright)
        {
            int u0 = face % 8 * 32, v0 = face / 8 * 32, u1 = u0 + 31, v1 = v0 + 31;
            // Seen from outside, a side's first base vertex is on the right: read the image right to left there,
            // so lettering ("NITRO", 3-2-1) is not mirrored.
            if (upright) (u0, u1) = (u1, u0);
            int clut = (ClutRow + face) << 6 | 512 / 16;
            // Corners a, b, c, d take (u0, va), (u0, vb), (u1, va), (u1, vb): a and c are the base vertices of a side.
            int va = upright ? v1 : v0, vb = upright ? v0 : v1;
            byte[] f = new byte[12];
            f[0] = (byte)u0; f[1] = (byte)va; BinaryPrimitives.WriteUInt16LittleEndian(f.AsSpan(2), (ushort)clut);
            f[4] = (byte)u0; f[5] = (byte)vb; BinaryPrimitives.WriteUInt16LittleEndian(f.AsSpan(6), TexturePage);
            f[8] = (byte)u1; f[9] = (byte)va; f[10] = (byte)u1; f[11] = (byte)vb;
            return f;
        }
        // (a, b, c, d, colour index, texture) - sides, base, top.
        var quads = new (int A, int B, int C, int D, int Colour, byte[] Tex)[]
        {
            (0, 4, 1, 5, 1, Field(side, true)), (3, 7, 2, 6, 2, Field(side, true)),
            (1, 5, 3, 7, 1, Field(side, true)), (2, 6, 0, 4, 2, Field(side, true)),
            (2, 0, 3, 1, 3, Field(top, false)), (4, 6, 5, 7, 0, Field(top, false)),
        };
        static uint Q(int a, int b, int c, bool textured) => 0x80000000u | (uint)(a << 22 | b << 13 | c << 4) | (textured ? 2u : 0u);
        static uint C(int i) => (uint)(i << 19 | i << 10 | i << 1);
        static uint D(int d, int i) => (uint)(d << 2 | i << 11);
        using var near = new MemoryStream();
        using var far = new MemoryStream();
        void W(Stream s, uint w) { Span<byte> b = stackalloc byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(b, w); s.Write(b); }
        foreach (var q in quads)
        {
            W(near, Q(q.A, q.B, q.C, true)); W(near, C(q.Colour)); W(near, D(q.D, q.Colour)); near.Write(q.Tex);
            W(far, Q(q.A, q.B, q.C, false)); W(far, C(q.Colour)); W(far, D(q.D, q.Colour));
        }
        // Near colours modulate the texture (0x80 = as drawn): lit top, two side shades, dark base.
        byte[][] nearColours = [[0xA8, 0xA8, 0xA8, 0xFF], [0x90, 0x90, 0x90, 0xFF], [0x74, 0x74, 0x74, 0xFF], [0x50, 0x50, 0x50, 0xFF]];
        var (r, g, b) = Average(faces[side]);
        byte[][] farColours = [.. new[] { 1.15, 0.95, 0.8, 0.55 }.Select(k => new[] { Clamp(r * k), Clamp(g * k), Clamp(b * k), (byte)0 })];
        var verts = Vertices();

        var body = new MemoryStream();
        int offNear = (int)body.Length; W(body, (uint)near.Length); near.WriteTo(body);
        int offFar = (int)body.Length; W(body, (uint)far.Length); far.WriteTo(body);
        int offNearColours = (int)body.Length; foreach (var c in nearColours) body.Write(c);
        int offFarColours = (int)body.Length; foreach (var c in farColours) body.Write(c);
        int offVertices = (int)body.Length; foreach (var v in verts) W(body, VertexWord(v));

        byte[] p = template.ToArray();
        int md = BinaryPrimitives.ReadInt32LittleEndian(p.AsSpan(0x34)), anim = BinaryPrimitives.ReadInt32LittleEndian(p.AsSpan(0x38));
        int trailer = (int)(BinaryPrimitives.ReadUInt32LittleEndian(p.AsSpan(0x14)) & 0x7FFFFFFF);
        if (md + body.Length > trailer) throw new InvalidOperationException("Crash box model overflows its template.");
        p.AsSpan(md, trailer - md).Clear();
        body.ToArray().CopyTo(p, md);
        BinaryPrimitives.WriteUInt16LittleEndian(p.AsSpan(anim + 2), (ushort)nearColours.Length);
        p[anim + 8] = (byte)verts.Length; p[anim + 9] = (byte)verts.Length;
        BinaryPrimitives.WriteInt32LittleEndian(p.AsSpan(anim + 0x14), offNear);
        BinaryPrimitives.WriteInt32LittleEndian(p.AsSpan(anim + 0x18), offNearColours);
        BinaryPrimitives.WriteInt32LittleEndian(p.AsSpan(anim + 0x1C), offFar);
        BinaryPrimitives.WriteInt32LittleEndian(p.AsSpan(anim + 0x20), offFarColours);
        uint packed = BinaryPrimitives.ReadUInt32LittleEndian(p.AsSpan(anim + 0x24)) & ~0x1FFFFFu | (uint)offVertices;
        uint aux = (uint)verts.Length << 24 | BinaryPrimitives.ReadUInt32LittleEndian(p.AsSpan(anim + 0x28)) & 0x00FF0000;
        BinaryPrimitives.WriteUInt32LittleEndian(p.AsSpan(anim + 0x24), packed);
        BinaryPrimitives.WriteUInt32LittleEndian(p.AsSpan(anim + 0x28), aux);
        return p;
    }

    private static byte Clamp(double v) => (byte)Math.Clamp(Math.Round(v), 0, 255);

    private static (double R, double G, double B) Average(CrashTexture t)
    {
        double r = 0, g = 0, b = 0; int n = 0;
        foreach (byte pair in t.Texels)
            foreach (int index in new[] { pair & 15, pair >> 4 })
            {
                ushort c = BinaryPrimitives.ReadUInt16LittleEndian(t.Clut.AsSpan(index * 2));
                if ((c & 0x7FFF) == 0) continue;
                r += (c & 31) << 3; g += (c >> 5 & 31) << 3; b += (c >> 10 & 31) << 3; n++;
            }
        return n == 0 ? (128, 128, 128) : (r / n, g / n, b / n);
    }
}
