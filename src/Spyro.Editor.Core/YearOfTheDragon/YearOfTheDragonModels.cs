using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Music;

namespace Spyro.Editor.Core.YearOfTheDragon;

/// <summary>A face of a Spyro: Year of the Dragon object model: its corners (3 or 4, in perimeter order, counter-clockwise
/// seen from the side it shows, in the model's own axes), their colours, and its texture when it has one.</summary>
public sealed record YotdFace(int[] Vertices, (byte R, byte G, byte B)[] Colours, bool TwoSided, YotdTexture? Texture);

/// <summary>A textured face's texture: its page byte and flags byte as the game stores them (page x/64 in bits 0-3,
/// y/256 in bit 4, 8-bit in bit 7; flags bit 0 = 15-bit, bit 7 = see-through), its palette's VRAM position and
/// each corner's position on the page (texels).</summary>
public sealed record YotdTexture(int Page, int Flags, int ClutX, int ClutY, (int U, int V)[] Uv)
{
    /// <summary>0: 4-bit, 1: 8-bit, 2: 15-bit.</summary>
    public int Depth => ((Page >> 7) & 1) | ((Flags & 1) << 1);
    public bool SeeThrough => (Flags & 0x80) != 0;
}

/// <summary>One animation: per frame, every vertex in the game's own model axes (x forward, y, z down, the axes Spyro
/// the Dragon's objects use too) and units (world units after the shift), and the faces drawn with it.
/// <paramref name="FrameSounds"/>: per frame, the model's sound slot the game plays when the animation steps onto it
/// (the frame record's second word, bits 16-23; -1 none).</summary>
public sealed record YotdAnimation(int Shift, IReadOnlyList<(int X, int Y, int Z)[]> Frames, IReadOnlyList<YotdFace> Faces,
    IReadOnlyList<int> FrameSounds);

/// <summary>A sound effect: its SPU ADPCM blocks through the end-flagged one (a one-shot, as Spyro the Dragon's own
/// sounds are) and its SPU pitch (0x1000 = 44,100 Hz; the middle of a random range).</summary>
public sealed record YotdSound(byte[] Adpcm, int Pitch);

/// <summary>The object models of a Spyro: Year of the Dragon level as its disc stores them: the level's model block
/// (a WAD sub-file the game copies to memory, pointers stored relative) and its half of the texture memory, read
/// from the user's own disc (USA Rev 1) and checked against the release. The animated-model decoding follows the
/// game's own renderer (USA Rev 1 0x80031148): per vertex, a lookup byte adds one of 128 packed steps from the
/// executable's table (0x8006634C) to the vertex's base step, or a 16-bit word gives a step or (with an extra word)
/// an absolute position.</summary>
public sealed class YearOfTheDragonModels
{
    public const int LostFleetEntry = 137, VramSubfile = 0, ModelSubfile = 2, Subfiles = 18;
    public const string LostFleetModelsSha256 = "5ddada8fa22a39b7df98d597611dea479c476620c80b215f9b361dde946df979";
    public const string LostFleetVramSha256 = "e8df6810da949d16d7ddeb868022541022ee69d850a5817cc4fae3176520c577";
    public const int ShiftTableOffset = 0x56B4C;   // 0x8006634C in the executable loaded at 0x80010000
    public const string ShiftTableSha256 = "fe61611bbee71014ca92e9aca8ae30d16545fa2c6bcce17ca8a343d44e898f6d";
    public const int VramBytes = 0x80000;           // x 512..1023, 512 rows
    // The level's sound effects: the SPU sample bank follows the texture memory in sub-file 0; the sound table (length,
    // a 256-byte map for the game's own sounds, count, 20-byte descriptors) is in sub-file 1. A creature's sound slot
    // byte (model set +4) indexes the descriptors directly (USA Rev 1 0x8003BAE0 -> 0x8003BB74).
    public const int SoundBankBytes = 0x74800, SoundTableOffset = 0x4FFE8;
    public const string LostFleetSoundBankSha256 = "b7dc4363b3f50b7eed5672fb585a2fcfe17e09e7b7bc1502a9049b2bcb21aca8";
    public const string LostFleetSoundTableSha256 = "73a13e3756f9cc3611f43979a29386f0ee725a7ba1647d4e82e1b895b5e3a47b";

    private readonly byte[] _models;
    private readonly uint[] _shift;
    private readonly byte[] _bank, _sounds;

    /// <summary>The level's half of texture memory (x 512..1023, rows 0..511), as Spyro the Dragon's levels hold theirs.</summary>
    public byte[] Vram { get; }

    private YearOfTheDragonModels(byte[] models, byte[] vram, uint[] shift, byte[] bank, byte[] sounds)
    { _models = models; Vram = vram; _shift = shift; _bank = bank; _sounds = sounds; }

    /// <summary>Lost Fleet's main-area models and textures.</summary>
    public static YearOfTheDragonModels LostFleet(YearOfTheDragonDisc disc)
    {
        byte[] entry = disc.WadEntry(LostFleetEntry);
        byte[] Sub(int k)
        {
            int offset = BinaryPrimitives.ReadInt32LittleEndian(entry.AsSpan(8 * k)), size = BinaryPrimitives.ReadInt32LittleEndian(entry.AsSpan(8 * k + 4));
            if (offset < 8 * Subfiles || size <= 0 || offset + size > entry.Length) throw new InvalidDataException("Lost Fleet's level file is not laid out as the game's.");
            return entry.AsSpan(offset, size).ToArray();
        }
        byte[] models = Sub(ModelSubfile), vram = Sub(VramSubfile), terrain = Sub(1);
        if (vram.Length < VramBytes + SoundBankBytes || terrain.Length < SoundTableOffset + 4) throw new InvalidDataException("Lost Fleet's texture memory and sounds are not where the game keeps them.");
        byte[] bank = vram.AsSpan(VramBytes, SoundBankBytes).ToArray();
        int tableBytes = BinaryPrimitives.ReadInt32LittleEndian(terrain.AsSpan(SoundTableOffset));
        if (tableBytes < 0x108 || SoundTableOffset + tableBytes > terrain.Length) throw new InvalidDataException("Lost Fleet's sound table is not where the game keeps it.");
        byte[] sounds = terrain.AsSpan(SoundTableOffset, tableBytes).ToArray();
        vram = vram.AsSpan(0, VramBytes).ToArray();
        byte[] exe = disc.Executable();
        byte[] table = exe.AsSpan(ShiftTableOffset, 512).ToArray();
        if (Hash(models) != LostFleetModelsSha256 || Hash(vram) != LostFleetVramSha256 || Hash(table) != ShiftTableSha256 ||
            Hash(bank) != LostFleetSoundBankSha256 || Hash(sounds) != LostFleetSoundTableSha256)
            throw new InvalidDataException("This Spyro: Year of the Dragon disc's Lost Fleet differs from the USA Rev 1 release.");
        uint[] shift = new uint[128];
        for (int i = 0; i < 128; i++) shift[i] = BinaryPrimitives.ReadUInt32LittleEndian(table.AsSpan(4 * i));
        return new(models, vram, shift, bank, sounds);
    }

    private static string Hash(byte[] b) => Convert.ToHexStringLower(SHA256.HashData(b));

    private uint U32(int at) => BinaryPrimitives.ReadUInt32LittleEndian(_models.AsSpan(at));
    private int U16(int at) => BinaryPrimitives.ReadUInt16LittleEndian(_models.AsSpan(at));
    private short S16(int at) => BinaryPrimitives.ReadInt16LittleEndian(_models.AsSpan(at));

    /// <summary>Animations of the animated model set at <paramref name="set"/> in the model block: +0 count, +0x38 the
    /// shared data block, +0x3C one animation header per animation (both relative to the set; the header's own
    /// pointers relative to the data block).</summary>
    public IReadOnlyList<YotdAnimation> Animated(int set)
    {
        if (set < 0 || set + 0x40 > _models.Length) throw new ArgumentOutOfRangeException(nameof(set));
        int count = (int)U32(set);
        if (count is < 1 or > 64) throw new InvalidDataException("Not an animated model set.");
        int data = set + (int)U32(set + 0x38);
        List<YotdAnimation> result = [];
        for (int a = 0; a < count; a++)
        {
            int h = set + (int)U32(set + 0x3C + 4 * a);
            int Ptr(int k) => data + (int)U32(h + k);
            int frames = _models[h], shift = _models[h + 2], vertices = _models[h + 4];
            if (frames == 0 || vertices == 0 || shift > 8) throw new InvalidDataException("Not an animated model set.");
            var faces = Faces(Ptr(0x10), Ptr(0x14));
            List<(int, int, int)[]> list = [];
            List<int> sounds = [];
            for (int f = 0; f < frames; f++)
            {
                list.Add(Frame(h, f, vertices, shift, Ptr(0xC), Ptr(0x20)));
                int slot = (int)(U32(h + 0x28 + 8 * f) >> 16 & 0xFF);
                sounds.Add(slot >= 0x80 ? -1 : slot);
            }
            result.Add(new(shift, list, faces, sounds));
        }
        return result;
    }

    /// <summary>The 16 sound slots of the model set at <paramref name="set"/>: descriptor numbers, 0xFF none.</summary>
    public byte[] SoundSlots(int set) => _models.AsSpan(set + 4, 16).ToArray();

    /// <summary>Sound effect <paramref name="id"/> of the level's sound table.</summary>
    public YotdSound Sound(int id)
    {
        int count = BinaryPrimitives.ReadInt32LittleEndian(_sounds.AsSpan(0x104));
        if (id < 0 || id >= count) throw new InvalidDataException($"Lost Fleet has no sound {id}.");
        var d = _sounds.AsSpan(0x108 + 20 * id, 20);
        int offset = BinaryPrimitives.ReadInt32LittleEndian(d);
        int pitch = (BinaryPrimitives.ReadUInt16LittleEndian(d[0xC..]) + BinaryPrimitives.ReadUInt16LittleEndian(d[0xE..])) / 2;
        if (offset < 0 || offset % 16 != 0 || offset >= _bank.Length) throw new InvalidDataException($"Lost Fleet's sound {id} is not in its sample bank.");
        int end = offset;
        while (true)
        {
            if (end + 16 > _bank.Length) throw new InvalidDataException($"Lost Fleet's sound {id} has no end.");
            bool last = (_bank[end + 1] & 1) != 0;
            end += 16;
            if (last) break;
        }
        return new(_bank.AsSpan(offset, end - offset).ToArray(), pitch);
    }

    private (int X, int Y, int Z)[] Frame(int h, int frame, int count, int shift, int basePtr, int streamPtr)
    {
        uint w0 = U32(h + 0x24 + 8 * frame);
        int stream = streamPtr + (int)(w0 & 0xFFFF) * 2;
        int look = stream + (int)((w0 >> 15) & 0x3FE);
        bool explicitNext = ((w0 >> 25) & 1) != 0;
        int x = 0, y = 0, z = 0;
        var result = new (int, int, int)[count];
        for (int i = 0; i < count; i++)
        {
            uint p = U32(basePtr + 4 * i);
            if (!explicitNext)
            {
                byte l = _models[look++];
                explicitNext = (l & 1) != 0;
                uint t = unchecked(_shift[(l >> 1) & 0x7F] + p);
                x += (int)t >> 21; y -= (int)(t << 11) >> 21; z -= (int)(t << 22) >> 21;
            }
            else
            {
                int d = S16(stream); stream += 2;
                explicitNext = (d & 1) != 0;
                if ((d & 2) != 0)
                {
                    int e = S16(stream); stream += 2;
                    z = (d & 0xFFC) << 20 >> 20;
                    y = (e << 26 >> 21) | ((d & 0xF000) >> 11);
                    x = e >> 5;
                }
                else
                {
                    x += d >> 9; y -= d << 21 >> 25; z -= (d << 26 >> 27) << 2;
                    x += (int)p >> 21; y -= (int)(p << 11) >> 21; z -= (int)(p << 22) >> 21;
                }
            }
            result[i] = (x << shift, y << shift, z << shift);
        }
        return result;
    }

    /// <summary>A face list: byte length, then per face the corner indices (bytes i0..i3; i0 == i1 makes a triangle),
    /// a word with the colour indices (c0 bits 10-16, c1 17-23, c2 24-30, c3 3-9; triangle flags in bits 3-5: 3
    /// two-sided, 4 a sprite) and the textured bit (31), then 12 texture bytes when textured. The game draws the corners
    /// as i3, i2, i1, i0 (a quad as a strip): triangles are reversed so every face turns the same way.</summary>
    private List<YotdFace> Faces(int faces, int colours)
    {
        int length = U16(faces), p = faces + 4, end = p + length;
        List<YotdFace> result = [];
        (byte, byte, byte) Colour(int i) { uint c = U32(colours + 4 * i); return ((byte)c, (byte)(c >> 8), (byte)(c >> 16)); }
        while (p < end)
        {
            uint idx = U32(p), e = U32(p + 4);
            int i0 = (int)(idx & 0xFF), i1 = (int)(idx >> 8 & 0xFF), i2 = (int)(idx >> 16 & 0xFF), i3 = (int)(idx >> 24);
            int c0 = (int)(e >> 10 & 0x7F), c1 = (int)(e >> 17 & 0x7F), c2 = (int)(e >> 24 & 0x7F), c3 = (int)(e >> 3 & 0x7F);
            bool triangle = i0 == i1, textured = (e >> 31) != 0;
            (int U, int V)[]? uv = null; int page = 0, flags = 0, clut = 0;
            if (textured)
            {
                var b = _models.AsSpan(p + 8, 12);
                uv = [(b[0], b[1]), (b[4], b[5]), (b[8], b[9]), (b[10], b[11])];
                clut = b[2] | b[3] << 8; page = b[6]; flags = b[7];
                p += 20;
            }
            else p += 8;
            (int V, int C, int T)[] corners = [(i3, c2, 0), (i2, c1, 1), (i1, c0, 2), (i0, c3, 3)];
            if (triangle && (e & 0x10) != 0) continue;            // a sprite drawn at one corner, not a polygon
            var order = triangle ? new[] { corners[2], corners[1], corners[0] } : [corners[0], corners[1], corners[3], corners[2]];
            YotdTexture? texture = uv == null ? null
                : new(page, flags, (clut & 0x3F) * 16, clut >> 6 & 0x1FF, order.Select(c => uv[c.T]).ToArray());
            result.Add(new([.. order.Select(c => c.V)], [.. order.Select(c => Colour(c.C))], triangle && (e & 8) != 0, texture));
        }
        return result;
    }
}
