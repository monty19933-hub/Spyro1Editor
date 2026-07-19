namespace Spyro.Editor.Core.Rendering;

/// <summary>
/// Certification attached to an explicit retail camera input. The camera
/// builder deliberately rejects uncatalogued/editor-float inputs instead of
/// silently relabelling them as the fixed-point state used by the game.
/// </summary>
public enum SpyroRetailCameraInputCertification : byte
{
    None = 0,
    RetailFixed16PositionAndSigned4096TurnRotation = 1,
    EditorDerivedQuantizedFixed16PositionAndSigned4096TurnRotation = 2
}

/// <summary>
/// Certification attached to terrain coordinates. Only raw retail scene words
/// or independently verified coordinates may enter the native adapters.
/// </summary>
public enum SpyroRetailTerrainCoordinateCertification : byte
{
    None = 0,
    RetailPackedSceneWords = 1,
    IndependentlyVerifiedNativeCoordinates = 2
}

/// <summary>
/// Spyro's signed 32-bit camera position. One world unit is represented by 16
/// position ticks in retail state.
/// </summary>
public readonly record struct SpyroRetailFixed16Position(int X, int Y, int Z);

/// <summary>
/// Signed camera rotations stored by retail Spyro. A complete turn is 4096;
/// the retail Sin/Cos routines mask every component to twelve bits.
/// </summary>
public readonly record struct SpyroRetailRotation4096(short X, short Y, short Z);

public readonly record struct SpyroRetailTerrainCameraInput(
    SpyroRetailFixed16Position Position,
    SpyroRetailRotation4096 Rotation,
    SpyroRetailCameraInputCertification Certification);

/// <summary>
/// Raw scene words needed to reproduce the LP/HP coordinate decode in
/// r_environment. SectorXyWord is sector +0x08, SectorZWord is +0x0C, and
/// PackedVertexWord is the selected four-byte vertex record.
/// </summary>
public readonly record struct SpyroRetailPackedTerrainVertexInput(
    uint SectorXyWord,
    uint SectorZWord,
    uint PackedVertexWord,
    SpyroRetailTerrainCoordinateCertification Certification);

/// <summary>Retail LP world coordinates in the renderer's one-times lattice.</summary>
public readonly record struct SpyroRetailLowPolyWorldPoint(
    int X,
    int Y,
    int Z,
    SpyroRetailTerrainCoordinateCertification Certification);

/// <summary>Retail HP world coordinates in the renderer's four-times lattice.</summary>
public readonly record struct SpyroRetailHighPolyWorldPoint4(
    int X4,
    int Y4,
    int Z4,
    SpyroRetailTerrainCoordinateCertification Certification);

/// <summary>
/// Certified, standalone retail camera state. ViewRotation is the matrix copied
/// to g_Camera.m_ViewMatrix and loaded by r_environment. ProjectionRegisters
/// therefore retain that unscaled matrix.
/// </summary>
public readonly record struct SpyroRetailTerrainCameraState(
    SpyroRetailFixed16Position Position,
    SpyroRetailRotation4096 Rotation,
    PsxGteRotationMatrix ViewRotation,
    PsxGteProjectionRegisters ProjectionRegisters,
    SpyroRetailCameraInputCertification Certification)
{
    public bool IsCertified =>
        Certification is
            SpyroRetailCameraInputCertification.RetailFixed16PositionAndSigned4096TurnRotation or
            SpyroRetailCameraInputCertification.EditorDerivedQuantizedFixed16PositionAndSigned4096TurnRotation;

    public bool IsEditorDerived =>
        Certification ==
        SpyroRetailCameraInputCertification.EditorDerivedQuantizedFixed16PositionAndSigned4096TurnRotation;
}

/// <summary>
/// Exact fixed-point foundation for Spyro 1's retail terrain camera.
///
/// This class reconstructs camera/register state only from explicit certified
/// integers. It has no dependency on the Fly 3D camera and makes no claim about
/// any particular gameplay frame.
/// </summary>
public static class SpyroNativeTerrainCamera
{
    public const string Contract = "spyro1-native-terrain-camera-registers-v1";
    public const string CoordinateAdapterContract = "spyro1-r-environment-lp-hp-world-to-gte-v1";

    public const int AngleUnitsPerTurn = 4096;
    public const int MatrixOne = 4096;
    public const int CameraPositionScale = 16;
    public const int HighPolyWorldScale = 4;
    public const int FramebufferCenterX = 256;
    public const int FramebufferCenterY = 120;
    public const ushort GeometryScreenH = 341;
    public const short DepthQueueA = 0x100;
    public const int DepthQueueB = 0;

    // Exact 16-angle-step Q12 cosine table at D_8006CC78. The first 257
    // entries are the interpolation endpoints consumed by Cos; the retail data
    // contains one additional zero word after them which the function cannot
    // address and is intentionally excluded.
    private static readonly ushort[] RetailCosineTable =
    [
        0x1000, 0x0FFF, 0x0FFB, 0x0FF5, 0x0FEC, 0x0FE1, 0x0FD4, 0x0FC4,
        0x0FB1, 0x0F9C, 0x0F85, 0x0F6C, 0x0F50, 0x0F31, 0x0F11, 0x0EEE,
        0x0EC8, 0x0EA1, 0x0E77, 0x0E4B, 0x0E1C, 0x0DEC, 0x0DB9, 0x0D85,
        0x0D4E, 0x0D15, 0x0CDA, 0x0C9D, 0x0C5E, 0x0C1E, 0x0BDB, 0x0B97,
        0x0B50, 0x0B08, 0x0ABF, 0x0A73, 0x0A26, 0x09D8, 0x0988, 0x0937,
        0x08E4, 0x088F, 0x083A, 0x07E3, 0x078B, 0x0732, 0x06D7, 0x067C,
        0x061F, 0x05C2, 0x0564, 0x0505, 0x04A5, 0x0444, 0x03E3, 0x0381,
        0x031F, 0x02BC, 0x0259, 0x01F5, 0x0191, 0x012D, 0x00C9, 0x0065,
        0x0000, 0xFF9B, 0xFF37, 0xFED3, 0xFE6F, 0xFE0B, 0xFDA7, 0xFD44,
        0xFCE1, 0xFC7F, 0xFC1D, 0xFBBC, 0xFB5B, 0xFAFB, 0xFA9C, 0xFA3E,
        0xF9E1, 0xF984, 0xF929, 0xF8CE, 0xF875, 0xF81D, 0xF7C6, 0xF771,
        0xF71C, 0xF6C9, 0xF678, 0xF628, 0xF5DA, 0xF58D, 0xF541, 0xF4F8,
        0xF4B0, 0xF469, 0xF425, 0xF3E2, 0xF3A2, 0xF363, 0xF326, 0xF2EB,
        0xF2B2, 0xF27B, 0xF247, 0xF214, 0xF1E4, 0xF1B5, 0xF189, 0xF15F,
        0xF138, 0xF112, 0xF0EF, 0xF0CF, 0xF0B0, 0xF094, 0xF07B, 0xF064,
        0xF04F, 0xF03C, 0xF02C, 0xF01F, 0xF014, 0xF00B, 0xF005, 0xF001,
        0xF000, 0xF001, 0xF005, 0xF00B, 0xF014, 0xF01F, 0xF02C, 0xF03C,
        0xF04F, 0xF064, 0xF07B, 0xF094, 0xF0B0, 0xF0CF, 0xF0EF, 0xF112,
        0xF138, 0xF15F, 0xF189, 0xF1B5, 0xF1E4, 0xF214, 0xF247, 0xF27B,
        0xF2B2, 0xF2EB, 0xF326, 0xF363, 0xF3A2, 0xF3E2, 0xF425, 0xF469,
        0xF4B0, 0xF4F8, 0xF541, 0xF58D, 0xF5DA, 0xF628, 0xF678, 0xF6C9,
        0xF71C, 0xF771, 0xF7C6, 0xF81D, 0xF875, 0xF8CE, 0xF929, 0xF984,
        0xF9E1, 0xFA3E, 0xFA9C, 0xFAFB, 0xFB5B, 0xFBBC, 0xFC1D, 0xFC7F,
        0xFCE1, 0xFD44, 0xFDA7, 0xFE0B, 0xFE6F, 0xFED3, 0xFF37, 0xFF9B,
        0x0000, 0x0065, 0x00C9, 0x012D, 0x0191, 0x01F5, 0x0259, 0x02BC,
        0x031F, 0x0381, 0x03E3, 0x0444, 0x04A5, 0x0505, 0x0564, 0x05C2,
        0x061F, 0x067C, 0x06D7, 0x0732, 0x078B, 0x07E3, 0x083A, 0x088F,
        0x08E4, 0x0937, 0x0988, 0x09D8, 0x0A26, 0x0A73, 0x0ABF, 0x0B08,
        0x0B50, 0x0B97, 0x0BDB, 0x0C1E, 0x0C5E, 0x0C9D, 0x0CDA, 0x0D15,
        0x0D4E, 0x0D85, 0x0DB9, 0x0DEC, 0x0E1C, 0x0E4B, 0x0E77, 0x0EA1,
        0x0EC8, 0x0EEE, 0x0F11, 0x0F31, 0x0F50, 0x0F6C, 0x0F85, 0x0F9C,
        0x0FB1, 0x0FC4, 0x0FD4, 0x0FE1, 0x0FEC, 0x0FF5, 0x0FFB, 0x0FFF,
        0x1000
    ];

    public static ReadOnlySpan<ushort> RetailCosineSamples => RetailCosineTable;

    /// <summary>Exact retail Cos, including 12-bit wrap and linear interpolation.</summary>
    public static short CosQ12(int angle) => InterpolateSamples(angle, cosine: true);

    /// <summary>Exact retail Sin, including 12-bit wrap and linear interpolation.</summary>
    public static short SinQ12(int angle) => InterpolateSamples(angle, cosine: false);

    public static PsxGteRotationMatrix BuildViewRotation(SpyroRetailRotation4096 rotation)
    {
        short sinX = SinQ12(rotation.X);
        short cosX = CosQ12(rotation.X);
        short sinY = SinQ12(rotation.Y);
        short cosY = CosQ12(rotation.Y);
        short sinZ = SinQ12(rotation.Z);
        short cosZ = CosQ12(rotation.Z);

        PsxGteRotationMatrix rotateX = new(
            MatrixOne, 0, 0,
            0, cosY, unchecked((short)-sinY),
            0, sinY, cosY);
        PsxGteRotationMatrix rotateY = new(
            cosZ, 0, sinZ,
            0, MatrixOne, 0,
            unchecked((short)-sinZ), 0, cosZ);
        PsxGteRotationMatrix rotateNegativeZ = new(
            cosX, sinX, 0,
            unchecked((short)-sinX), cosX, 0,
            0, 0, MatrixOne);

        return Multiply(Multiply(rotateX, rotateY), rotateNegativeZ);
    }

    public static bool TryBuildProjectionRegisters(
        in SpyroRetailTerrainCameraInput input,
        out PsxGteProjectionRegisters registers)
    {
        if (input.Certification is not (
            SpyroRetailCameraInputCertification.RetailFixed16PositionAndSigned4096TurnRotation or
            SpyroRetailCameraInputCertification.EditorDerivedQuantizedFixed16PositionAndSigned4096TurnRotation))
        {
            registers = default;
            return false;
        }

        registers = new PsxGteProjectionRegisters(
            BuildViewRotation(input.Rotation),
            PsxGteTranslation.Zero,
            FramebufferCenterX << 16,
            FramebufferCenterY << 16,
            GeometryScreenH,
            DepthQueueA,
            DepthQueueB);
        return true;
    }

    public static bool TryBuildState(
        in SpyroRetailTerrainCameraInput input,
        out SpyroRetailTerrainCameraState state)
    {
        if (!TryBuildProjectionRegisters(input, out PsxGteProjectionRegisters registers))
        {
            state = default;
            return false;
        }

        state = new SpyroRetailTerrainCameraState(
            input.Position,
            input.Rotation,
            BuildViewRotation(input.Rotation),
            registers,
            input.Certification);
        return true;
    }

    public static bool TryDecodeLowPolyWorldPoint(
        in SpyroRetailPackedTerrainVertexInput input,
        out SpyroRetailLowPolyWorldPoint point)
    {
        if (input.Certification != SpyroRetailTerrainCoordinateCertification.RetailPackedSceneWords)
        {
            point = default;
            return false;
        }

        int sectorX = (int)(input.SectorXyWord >> 16);
        int sectorY = (int)(input.SectorXyWord & 0xFFFFu);
        int sectorZ = (int)(input.SectorZWord >> 16);
        int localX = (int)(input.PackedVertexWord >> 21);
        int localY = (int)((input.PackedVertexWord >> 10) & 0x7FFu);
        int localZ = (int)(input.PackedVertexWord & 0x3FFu);

        point = new SpyroRetailLowPolyWorldPoint(
            checked(sectorX + localX),
            checked(sectorY + localY),
            checked(sectorZ + localZ),
            SpyroRetailTerrainCoordinateCertification.RetailPackedSceneWords);
        return true;
    }

    public static bool TryDecodeHighPolyWorldPoint4(
        in SpyroRetailPackedTerrainVertexInput input,
        out SpyroRetailHighPolyWorldPoint4 point)
    {
        if (input.Certification != SpyroRetailTerrainCoordinateCertification.RetailPackedSceneWords)
        {
            point = default;
            return false;
        }

        // These shifts/masks intentionally retain the two low origin bits which
        // are lost by a display-space divide-by-four conversion.
        int sectorX4 = (int)(input.SectorXyWord >> 14);
        int sectorY4 = checked((int)(input.SectorXyWord & 0xFFFFu) << 2);
        int sectorZ4 = (int)(input.SectorZWord >> 14);
        int localX4 = (int)((input.PackedVertexWord >> 19) & 0x1FFCu);
        int localY4 = (int)((input.PackedVertexWord >> 8) & 0x1FFCu);
        int localZ4 = (int)((input.PackedVertexWord << 2) & 0x0FFCu);

        point = new SpyroRetailHighPolyWorldPoint4(
            checked(sectorX4 + localX4),
            checked(sectorY4 + localY4),
            checked(sectorZ4 + localZ4),
            SpyroRetailTerrainCoordinateCertification.RetailPackedSceneWords);
        return true;
    }

    public static bool TryAdaptLowPolyWorldToGte(
        in SpyroRetailTerrainCameraState camera,
        in SpyroRetailLowPolyWorldPoint world,
        out PsxGteVector vertex)
    {
        if (!camera.IsCertified || !IsCoordinateCertified(world.Certification))
        {
            vertex = default;
            return false;
        }

        long cameraX = camera.Position.X >> 4;
        long cameraY = camera.Position.Y >> 4;
        long cameraZ = camera.Position.Z >> 4;
        return TryCreateVector(
            cameraY - world.Y,
            cameraZ - world.Z,
            world.X - cameraX,
            out vertex);
    }

    public static bool TryAdaptHighPolyWorldToGte(
        in SpyroRetailTerrainCameraState camera,
        in SpyroRetailHighPolyWorldPoint4 world,
        out PsxGteVector vertex)
    {
        if (!camera.IsCertified || !IsCoordinateCertified(world.Certification))
        {
            vertex = default;
            return false;
        }

        // Reproduce the retail MIPS srl/sub/add wrapping path first. The GTE
        // receives only each result's low halfword. A separate signed semantic
        // range check then fails closed if that halfword would be a wrapped
        // coordinate rather than an exact signed reconstruction.
        uint cameraX4 = unchecked((uint)camera.Position.X) >> 2;
        uint cameraY4 = unchecked((uint)camera.Position.Y) >> 2;
        uint cameraZ4 = unchecked((uint)camera.Position.Z) >> 2;
        uint rawX = unchecked(cameraY4 - (uint)world.Y4);
        uint rawY = unchecked(cameraZ4 - (uint)world.Z4);
        uint rawZ = unchecked((uint)world.X4 - cameraX4);

        long semanticX = ((long)camera.Position.Y >> 2) - world.Y4;
        long semanticY = ((long)camera.Position.Z >> 2) - world.Z4;
        long semanticZ = world.X4 - ((long)camera.Position.X >> 2);
        if (!FitsSignedHalfword(semanticX) || !FitsSignedHalfword(semanticY) || !FitsSignedHalfword(semanticZ))
        {
            vertex = default;
            return false;
        }

        vertex = new PsxGteVector(
            unchecked((short)rawX),
            unchecked((short)rawY),
            unchecked((short)rawZ));
        return vertex.X == (short)semanticX &&
               vertex.Y == (short)semanticY &&
               vertex.Z == (short)semanticZ;
    }

    public static bool TryAdaptPackedLowPolyVertexToGte(
        in SpyroRetailTerrainCameraState camera,
        in SpyroRetailPackedTerrainVertexInput input,
        out PsxGteVector vertex)
    {
        if (!TryDecodeLowPolyWorldPoint(input, out SpyroRetailLowPolyWorldPoint world))
        {
            vertex = default;
            return false;
        }

        return TryAdaptLowPolyWorldToGte(camera, world, out vertex);
    }

    public static bool TryAdaptPackedHighPolyVertexToGte(
        in SpyroRetailTerrainCameraState camera,
        in SpyroRetailPackedTerrainVertexInput input,
        out PsxGteVector vertex)
    {
        if (!TryDecodeHighPolyWorldPoint4(input, out SpyroRetailHighPolyWorldPoint4 world))
        {
            vertex = default;
            return false;
        }

        return TryAdaptHighPolyWorldToGte(camera, world, out vertex);
    }

    private static short InterpolateSamples(int angle, bool cosine)
    {
        int wrapped = angle & 0xFFF;
        int sampleIndex = wrapped >> 4;
        int fraction = wrapped & 0xF;
        int value0 = cosine
            ? GetCosineSample(sampleIndex)
            : GetSineSample(sampleIndex);
        if (fraction == 0)
            return checked((short)value0);

        int value1 = cosine
            ? GetCosineSample(sampleIndex + 1)
            : GetSineSample(sampleIndex + 1);
        return checked((short)(value0 + (((value1 - value0) * fraction) >> 4)));
    }

    private static short GetCosineSample(int index) => unchecked((short)RetailCosineTable[index]);

    private static short GetSineSample(int index)
    {
        // D_8006CBF8 begins 64 samples before D_8006CC78. Its readable
        // interpolation range maps exactly to cosine samples 192..255,0..192.
        int cosineIndex = index <= 64 ? index + 192 : index - 64;
        return GetCosineSample(cosineIndex);
    }

    private static PsxGteRotationMatrix Multiply(
        PsxGteRotationMatrix left,
        PsxGteRotationMatrix right) =>
        new(
            MultiplyCell(left.M11, left.M12, left.M13, right.M11, right.M21, right.M31),
            MultiplyCell(left.M11, left.M12, left.M13, right.M12, right.M22, right.M32),
            MultiplyCell(left.M11, left.M12, left.M13, right.M13, right.M23, right.M33),
            MultiplyCell(left.M21, left.M22, left.M23, right.M11, right.M21, right.M31),
            MultiplyCell(left.M21, left.M22, left.M23, right.M12, right.M22, right.M32),
            MultiplyCell(left.M21, left.M22, left.M23, right.M13, right.M23, right.M33),
            MultiplyCell(left.M31, left.M32, left.M33, right.M11, right.M21, right.M31),
            MultiplyCell(left.M31, left.M32, left.M33, right.M12, right.M22, right.M32),
            MultiplyCell(left.M31, left.M32, left.M33, right.M13, right.M23, right.M33));

    private static short MultiplyCell(
        short left0,
        short left1,
        short left2,
        short right0,
        short right1,
        short right2)
    {
        long mac = ((long)left0 * right0) + ((long)left1 * right1) + ((long)left2 * right2);
        long shifted = mac >> 12;
        return (short)Math.Clamp(shifted, short.MinValue, short.MaxValue);
    }

    private static bool IsCoordinateCertified(SpyroRetailTerrainCoordinateCertification certification) =>
        certification is SpyroRetailTerrainCoordinateCertification.RetailPackedSceneWords or
            SpyroRetailTerrainCoordinateCertification.IndependentlyVerifiedNativeCoordinates;

    private static bool TryCreateVector(long x, long y, long z, out PsxGteVector vertex)
    {
        if (!FitsSignedHalfword(x) || !FitsSignedHalfword(y) || !FitsSignedHalfword(z))
        {
            vertex = default;
            return false;
        }

        vertex = new PsxGteVector((short)x, (short)y, (short)z);
        return true;
    }

    private static bool FitsSignedHalfword(long value) =>
        value >= short.MinValue && value <= short.MaxValue;
}
