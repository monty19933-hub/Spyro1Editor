using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Music;
using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.YearOfTheDragon;

/// <summary>Year of the Dragon's distant meshes. <see cref="FarMeshes(YearOfTheDragonDisc, YearOfTheDragonLevel)"/> reads any
/// level (<see cref="YearOfTheDragonLevelData"/>); the Lost Fleet reader below is pinned to that level's sector block.
/// Lost Fleet's terrain sectors as Year of the Dragon stores them (WAD entry 137, sub-file 1): a block of 116
/// sectors (kind word, count, one offset per sector from the block start), each a 0x1C-byte header (centre, radius and
/// flags, origin; the far and near tables' vertex, colour and face counts at +0x10 and +0x14) followed by its far
/// (distant, untextured) vertices, colours and faces and then its near ones. A sector's number is its place in the
/// block, the number the Lost Fleet scene's faces carry as <see cref="AuthoredSurface.Sector"/>.</summary>
public static class YearOfTheDragonTerrain
{
    public const int SectorBlockOffset = 0xDEC, Sectors = 116, SectorBlockBytes = 243668;
    public const string LostFleetSectorBlockSha256 = "18ac4dea98668554c99ac0101db95cd2f2c4ebfa907b853c200d78d49ee2c85b";
    /// <summary>Header flag: the near vertices' heights are in steps of 2 world units, not 16.</summary>
    private const int FineHeights = 0x1000;

    /// <summary>Every sector's own far mesh of any Year of the Dragon level's main area, in editor units, in the scene's
    /// corner convention (numbered by the sector's place in the level's list, as <see cref="AuthoredSurface.Sector"/>
    /// of an imported face; checked against the release through the level's WAD entry).</summary>
    public static IReadOnlyList<AuthoredFarMesh> FarMeshes(YearOfTheDragonDisc disc, YearOfTheDragonLevel level) =>
        [.. YearOfTheDragonLevelData.Read(disc, level).Sectors.Select(s => s.Far).OfType<AuthoredFarMesh>()];

    /// <summary>Every Lost Fleet sector's far mesh in editor units (16 world units), in the scene's corner convention.</summary>
    public static IReadOnlyList<AuthoredFarMesh> LostFleetFarMeshes(YearOfTheDragonDisc disc)
    {
        byte[] entry = disc.WadEntry(YearOfTheDragonModels.LostFleetEntry);
        int offset = BinaryPrimitives.ReadInt32LittleEndian(entry.AsSpan(8)), size = BinaryPrimitives.ReadInt32LittleEndian(entry.AsSpan(12));
        if (offset < 8 * YearOfTheDragonModels.Subfiles || size < SectorBlockOffset + SectorBlockBytes || offset + size > entry.Length)
            throw new InvalidDataException("Lost Fleet's level file is not laid out as the game's.");
        return FarMeshes(entry.AsSpan(offset + SectorBlockOffset, SectorBlockBytes).ToArray());
    }

    /// <summary>The far meshes of a sector block (checked against the release). Far faces store their corners as
    /// 7-bit indices at bits 10, 17, 24 and 3 of word 0 (colours at bits 11, 18, 25 and 4 of word 1; a triangle repeats
    /// corner 0 as corner 3); the game draws a quad as (0,2,1) and (0,1,3) and shows the side from which
    /// 0,2,1,3 turns clockwise (measured against the near faces each far face copies), so the scene's anticlockwise
    /// order is 0,3,1,2 (a triangle 0,1,2).</summary>
    public static IReadOnlyList<AuthoredFarMesh> FarMeshes(byte[] block)
    {
        if (block.Length != SectorBlockBytes || Convert.ToHexStringLower(SHA256.HashData(block)) != LostFleetSectorBlockSha256)
            throw new InvalidDataException("This Spyro: Year of the Dragon disc's Lost Fleet terrain differs from the USA Rev 1 release.");
        uint U32(byte[] b, int at) => BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(at));
        if (U32(block, 4) != Sectors) throw new InvalidDataException("Lost Fleet's sector block is not laid out as the game's.");
        List<AuthoredFarMesh> meshes = [];
        for (int sector = 0; sector < Sectors; sector++)
        {
            int a = (int)U32(block, 8 + 4 * sector);
            int Half(int k) => BinaryPrimitives.ReadUInt16LittleEndian(block.AsSpan(a + 2 * k));
            int flags = Half(2), originY = Half(4), originX = Half(5), originZ = Half(7);
            int vertices = block[a + 0x10], colours = block[a + 0x11], faces = block[a + 0x12];
            if (faces == 0) continue;
            int heightStep = (flags & FineHeights) != 0 ? 2 : 16;
            int at = a + 0x1C;
            var v = new Vector3f[vertices];
            for (int i = 0; i < vertices; i++, at += 4)
            {
                uint p = U32(block, at);
                v[i] = new(originX + (p >> 21), originY + (p >> 10 & 0x7FF), MathF.Round(originZ + ((p & 0x3FF) << 1) * heightStep / 16f));
            }
            var c = new ColorRgba[colours];
            for (int i = 0; i < colours; i++, at += 4) c[i] = new(block[at], block[at + 1], block[at + 2]);
            List<AuthoredFarFace> list = [];
            for (int i = 0; i < faces; i++, at += 8)
            {
                uint pv = U32(block, at), pc = U32(block, at + 4);
                int[] vi = [(int)(pv >> 10 & 0x7F), (int)(pv >> 17 & 0x7F), (int)(pv >> 24 & 0x7F), (int)(pv >> 3 & 0x7F)];
                int[] ci = [(int)(pc >> 11 & 0x7F), (int)(pc >> 18 & 0x7F), (int)(pc >> 25 & 0x7F), (int)(pc >> 4 & 0x7F)];
                int[] order = vi[0] == vi[3] ? [0, 1, 2] : [0, 3, 1, 2];
                int[] corners = [.. order.Select(k => vi[k])], cols = [.. order.Select(k => ci[k])];
                if (corners.Any(k => k >= vertices) || cols.Any(k => k >= colours))
                    throw new InvalidDataException("A Lost Fleet far face points outside its sector's tables.");
                if (corners.Distinct().Count() == corners.Length) list.Add(new(corners, cols));
            }
            if (list.Count > 0) meshes.Add(new(sector, v, c, list));
        }
        return meshes;
    }
}
