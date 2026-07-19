namespace Spyro.Editor.Core.Rendering;

/// <summary>
/// Exact source words from a retail scene-sector header. Words 2 and 3 are the
/// <c>xyPos</c> (+0x08) and <c>zPos</c> (+0x0C) values consumed by
/// <c>r_environment</c>'s high-poly projection path. Words 0 and 1 retain the
/// provenance needed to validate the editor's derived center, radius, and
/// render flags.
/// </summary>
public readonly record struct NativeTerrainHpSectorCoordinatePayload(
    uint NativeCenterXyWord,
    uint NativeCenterZRadiusFlagsWord,
    uint NativeXyPositionWord,
    uint NativeZPositionWord,
    SpyroRetailTerrainCoordinateCertification Certification)
{
    public int CenterX => (int)(NativeCenterXyWord >> 16);
    public int CenterY => (int)(NativeCenterXyWord & 0xFFFF);
    public int CenterZ => (int)(NativeCenterZRadiusFlagsWord >> 16);
    public int Radius => (int)(NativeCenterZRadiusFlagsWord & 0x1FFF);
    public bool UsesSpecialZScale => (NativeCenterZRadiusFlagsWord & 0x1000) != 0;
    public bool DisableLowDetail => (NativeCenterZRadiusFlagsWord & 0x2000) != 0;
    public bool DisableHighDetail => (NativeCenterZRadiusFlagsWord & 0x4000) != 0;
    public bool ForceLowDetail => (NativeCenterZRadiusFlagsWord & 0x8000) != 0;

    public int DecodedOriginX => (int)(NativeXyPositionWord >> 16);
    public int DecodedOriginY => (int)(NativeXyPositionWord & 0xFFFF);
    public int DecodedOriginZ => (int)(((NativeZPositionWord >> 14) & 0xFFFF) >> 2);
    public int XOriginQuarterResidue => (int)((NativeXyPositionWord >> 14) & 0x03);
    public int ZOriginQuarterResidue => (int)((NativeZPositionWord >> 14) & 0x03);
}

public static class NativeTerrainHighPolyCoordinates
{
    public const string Contract = "raw-sector-header-words0-3-integer-world-xz-quarter-residue-v1";

    // Native HP vertex fields carry X/Y local offsets through mask 0x1FFC and
    // Z through mask 0x0FFC. The editor's compatibility points divide those
    // four-unit values by four.
    public const int MaximumLocalX = 0x1FFC >> 2;
    public const int MaximumLocalY = 0x1FFC >> 2;
    public const int MaximumLocalZ = 0x0FFC >> 2;

    /// <summary>
    /// Reconstructs an exact absolute X4/Y4/Z4 point from a certified raw
    /// sector header and the integer world point decoded from one of that
    /// sector's original HP vertex words.
    /// </summary>
    /// <exception cref="InvalidDataException">
    /// The point is outside the native HP local lattice, or the sector uses the
    /// unsupported header-bit-12 Z scaling path.
    /// </exception>
    public static SpyroRetailHighPolyWorldPoint4 ReconstructCertifiedWorldPointX4(
        NativeTerrainHpSectorCoordinatePayload sector,
        int decodedWorldX,
        int decodedWorldY,
        int decodedWorldZ)
    {
        if (sector.Certification != SpyroRetailTerrainCoordinateCertification.RetailPackedSceneWords)
        {
            throw new InvalidDataException(
                "Native HP coordinate reconstruction requires certified retail scene-sector header words.");
        }
        if (sector.UsesSpecialZScale)
        {
            throw new InvalidDataException(
                "Native HP coordinate reconstruction does not support scene-sector header bit 12 (special Z scaling).");
        }

        int localX = checked(decodedWorldX - sector.DecodedOriginX);
        int localY = checked(decodedWorldY - sector.DecodedOriginY);
        int localZ = checked(decodedWorldZ - sector.DecodedOriginZ);
        if (localX is < 0 or > MaximumLocalX ||
            localY is < 0 or > MaximumLocalY ||
            localZ is < 0 or > MaximumLocalZ)
        {
            throw new InvalidDataException(
                $"Decoded HP point ({decodedWorldX},{decodedWorldY},{decodedWorldZ}) is outside its sector's native local lattice " +
                $"from ({sector.DecodedOriginX},{sector.DecodedOriginY},{sector.DecodedOriginZ}).");
        }

        int x4 = checked((int)(sector.NativeXyPositionWord >> 14) + checked(localX << 2));
        int y4 = checked((int)((sector.NativeXyPositionWord & 0xFFFF) << 2) + checked(localY << 2));
        int z4 = checked((int)(sector.NativeZPositionWord >> 14) + checked(localZ << 2));
        SpyroRetailHighPolyWorldPoint4 result = new(
            x4,
            y4,
            z4,
            SpyroRetailTerrainCoordinateCertification.IndependentlyVerifiedNativeCoordinates);
        if ((result.X4 & 0x03) != sector.XOriginQuarterResidue ||
            (result.Z4 & 0x03) != sector.ZOriginQuarterResidue)
        {
            throw new InvalidDataException("Native HP coordinate reconstruction lost a sector-origin quarter residue.");
        }

        return result;
    }
}
