using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Rendering;

const string expectedUnrTableSha256 = "749321F5C391C354CEAE093365B71196DEBA24F60D6C46F682CCFE14073D78D7";
const string expectedRandomFixtureSha256 = "B335D2D3CFD67A6CAC9E9C580BC237EEB148471F501E454660357BA657C285FC";
const string expectedRetailCosineTableSha256 = "84565C097F34B5722AC4EB294CB799846852BD56C4C760A630B540496C34EDEA";
const string expectedCameraFixtureSha256 = "FFA14387AA9061304B0C993C4F01F77A0AE2ADB7CA8DDF664B513D9964D6E79D";

Assert(PsxGteProjection.Contract == "psx-gte-rtps-rtpt-explicit-registers-v1",
    "GTE projection contract name changed.");
Assert(SpyroNativeTerrainCamera.Contract == "spyro1-native-terrain-camera-registers-v1" &&
       SpyroNativeTerrainCamera.CoordinateAdapterContract == "spyro1-r-environment-lp-hp-world-to-gte-v1",
    "Spyro native terrain camera contract name changed.");
Assert(SpyroEditorDerivedTerrainCamera.Contract == "spyro-editor-fly-camera-to-fixed16-4096turn-v1" &&
       SpyroEditorDerivedTerrainCamera.PositionQuantization == "nearest-away-from-zero-times16-v1" &&
       SpyroEditorDerivedTerrainCamera.RotationQuantization == "nearest-away-from-zero-wrap-signed12-v1",
    "Editor-derived native terrain camera adapter contract changed.");
AssertFlagLayout();
AssertUnrTable();
AssertDivideBoundaries();
AssertIdentityRtps();
AssertRtpsFifo();
AssertRtptFifo();
AssertFlagBoundaries();
string randomFixtureSha256 = BuildRandomFixtureSha256();
Assert(randomFixtureSha256 == expectedRandomFixtureSha256,
    $"DuckStation-derived GTE projection oracle changed: {randomFixtureSha256}.");
string retailCosineTableSha256 = BuildRetailCosineTableSha256();
string cameraFixtureSha256 = BuildCameraFixtureSha256();
AssertRetailCameraFoundation();
Assert(retailCosineTableSha256 == expectedRetailCosineTableSha256,
    $"Retail Spyro cosine table changed: {retailCosineTableSha256}.");
Assert(cameraFixtureSha256 == expectedCameraFixtureSha256,
    $"Retail Spyro terrain camera fixture changed: {cameraFixtureSha256}.");

Console.WriteLine("PS1 GTE RTPS/RTPT smoke passed.");
Console.WriteLine($"- Contract: {PsxGteProjection.Contract}");
Console.WriteLine("- Spyro instruction: sf=1, lm=0; explicit RT/TR/OFX/OFY/H/DQA/DQB and FIFO inputs");
Console.WriteLine("- Locked behavior: signed-44-bit MAC, UNR division, SXY/SZ FIFOs, saturation/FLAG summary");
Console.WriteLine($"- Pinned 257-byte UNR table SHA-256: {expectedUnrTableSha256}");
Console.WriteLine($"- 4,096-vector RTPS/RTPT SHA-256: {randomFixtureSha256}");
Console.WriteLine($"- Spyro terrain camera: {SpyroNativeTerrainCamera.Contract}");
Console.WriteLine("- Camera inputs: certified fixed-16 position, signed 4096-turn rotation, raw LP/HP scene words");
Console.WriteLine($"- Named Fly adapter: {SpyroEditorDerivedTerrainCamera.Contract}");
Console.WriteLine($"- Retail Spyro 257-word cosine table SHA-256: {retailCosineTableSha256}");
Console.WriteLine($"- 4,096-camera/register/coordinate SHA-256: {cameraFixtureSha256}");

static void AssertFlagLayout()
{
    Assert((uint)PsxGteFlag.Ir0Saturated == 1u << 12 &&
           (uint)PsxGteFlag.Sy2Saturated == 1u << 13 &&
           (uint)PsxGteFlag.Sx2Saturated == 1u << 14 &&
           (uint)PsxGteFlag.Mac0Underflow == 1u << 15 &&
           (uint)PsxGteFlag.Mac0Overflow == 1u << 16 &&
           (uint)PsxGteFlag.DivideOverflow == 1u << 17 &&
           (uint)PsxGteFlag.Sz3OrOtzSaturated == 1u << 18 &&
           (uint)PsxGteFlag.ColorBSaturated == 1u << 19 &&
           (uint)PsxGteFlag.ColorGSaturated == 1u << 20 &&
           (uint)PsxGteFlag.ColorRSaturated == 1u << 21 &&
           (uint)PsxGteFlag.Ir3Saturated == 1u << 22 &&
           (uint)PsxGteFlag.Ir2Saturated == 1u << 23 &&
           (uint)PsxGteFlag.Ir1Saturated == 1u << 24 &&
           (uint)PsxGteFlag.Mac3Underflow == 1u << 25 &&
           (uint)PsxGteFlag.Mac2Underflow == 1u << 26 &&
           (uint)PsxGteFlag.Mac1Underflow == 1u << 27 &&
           (uint)PsxGteFlag.Mac3Overflow == 1u << 28 &&
           (uint)PsxGteFlag.Mac2Overflow == 1u << 29 &&
           (uint)PsxGteFlag.Mac1Overflow == 1u << 30 &&
           (uint)PsxGteFlag.Error == 1u << 31,
        "GTE FLAG enum no longer matches control register 63.");
    Assert(PsxGteProjection.FlagErrorSummaryMask == 0x7F87E000,
        "GTE FLAG bit-31 summary mask changed.");
}

static void AssertUnrTable()
{
    ReadOnlySpan<byte> table = PsxGteProjection.UnrTableEntries;
    Assert(table.Length == 257 && table[0] == 0xFF && table[1] == 0xFD &&
           table[255] == 0 && table[256] == 0,
        "GTE UNR reciprocal table shape/endpoints changed.");
    string sha256 = Convert.ToHexString(SHA256.HashData(table));
    Assert(sha256 == expectedUnrTableSha256,
        $"Pinned DuckStation UNR table changed: {sha256}.");
}

static void AssertDivideBoundaries()
{
    AssertDivide(0x0000, 0x0000, 0x1FFFF, overflow: true);
    AssertDivide(0x0000, 0x0001, 0x00000, overflow: false);
    AssertDivide(0x0001, 0x0001, 0x10000, overflow: false);
    AssertDivide(0x0280, 0x0140, 0x1FFFF, overflow: true);
    AssertDivide(0x027F, 0x0140, 0x1FF34, overflow: false);
    AssertDivide(0xFFFF, 0x8000, 0x1FFFE, overflow: false);

    // These two hardware boundary pairs round to 0x20000 internally and are
    // clamped to 0x1FFFF without setting divide overflow.
    AssertDivide(0xFE3F, 0x7F20, 0x1FFFF, overflow: false);
    AssertDivide(0xF015, 0x780B, 0x1FFFF, overflow: false);
}

static void AssertIdentityRtps()
{
    PsxGteProjectionRegisters registers = IdentityRegisters(
        ofx: 160 << 16,
        ofy: 120 << 16,
        h: 256,
        dqa: 0,
        dqb: 0);
    PsxGteProjectionResult result = PsxGteProjection.Rtps(
        registers,
        PsxGteProjectionFifo.Empty,
        new PsxGteVector(10, -20, 256),
        PsxGteProjectionCommand.SpyroEnvironment);

    Assert(result.Fifo.Sxy2 == new PsxGteScreenPoint(170, 100),
        $"Identity RTPS projected to {result.Fifo.Sxy2}, expected (170,100).");
    Assert(result.Fifo.Sz3 == 256 && result.LastPerspectiveFactor == 0x10000,
        "Identity RTPS changed exact H/SZ projection at unit perspective.");
    Assert(result.Mac1 == 10 && result.Mac2 == -20 && result.Mac3 == 256 &&
           result.Ir1 == 10 && result.Ir2 == -20 && result.Ir3 == 256,
        "Identity RTPS changed sf=1 MAC/IR transform results.");
    Assert(result.Flag == PsxGteFlag.None,
        $"Identity RTPS unexpectedly raised FLAG 0x{(uint)result.Flag:X8}.");
}

static void AssertRtpsFifo()
{
    PsxGteProjectionFifo initial = new(
        new PsxGteScreenPoint(-3, -4),
        new PsxGteScreenPoint(5, 6),
        new PsxGteScreenPoint(7, 8),
        11,
        12,
        13,
        14);
    PsxGteProjectionResult result = PsxGteProjection.Rtps(
        IdentityRegisters(0, 0, 64, 0, 0),
        initial,
        new PsxGteVector(1, 2, 64),
        PsxGteProjectionCommand.SpyroEnvironment);

    Assert(result.Fifo.Sxy0 == initial.Sxy1 &&
           result.Fifo.Sxy1 == initial.Sxy2 &&
           result.Fifo.Sxy2 == new PsxGteScreenPoint(1, 2),
        "RTPS did not shift the three-entry SXY FIFO exactly once.");
    Assert(result.Fifo.Sz0 == initial.Sz1 &&
           result.Fifo.Sz1 == initial.Sz2 &&
           result.Fifo.Sz2 == initial.Sz3 &&
           result.Fifo.Sz3 == 64,
        "RTPS did not shift the four-entry SZ FIFO exactly once.");
}

static void AssertRtptFifo()
{
    PsxGteProjectionFifo initial = new(
        new PsxGteScreenPoint(-3, -4),
        new PsxGteScreenPoint(5, 6),
        new PsxGteScreenPoint(7, 8),
        11,
        12,
        13,
        14);
    PsxGteProjectionResult result = PsxGteProjection.Rtpt(
        IdentityRegisters(0, 0, 64, 0, 0),
        initial,
        new PsxGteVector(1, 2, 64),
        new PsxGteVector(3, 4, 64),
        new PsxGteVector(5, 6, 64),
        PsxGteProjectionCommand.SpyroEnvironment);

    Assert(result.Fifo.Sxy0 == new PsxGteScreenPoint(1, 2) &&
           result.Fifo.Sxy1 == new PsxGteScreenPoint(3, 4) &&
           result.Fifo.Sxy2 == new PsxGteScreenPoint(5, 6),
        "RTPT did not leave all three projected vertices in SXY0..2.");
    Assert(result.Fifo.Sz0 == initial.Sz3 &&
           result.Fifo.Sz1 == 64 && result.Fifo.Sz2 == 64 && result.Fifo.Sz3 == 64,
        "RTPT did not shift three projected depths through SZ0..3.");
}

static void AssertFlagBoundaries()
{
    PsxGteProjectionResult ir3Only = PsxGteProjection.Rtps(
        new PsxGteProjectionRegisters(
            new PsxGteRotationMatrix(0, 0, 0, 0, 0, 0, 0, 0, 0),
            new PsxGteTranslation(0, 0, 40_000),
            0,
            0,
            1,
            0,
            0),
        PsxGteProjectionFifo.Empty,
        default,
        PsxGteProjectionCommand.SpyroEnvironment);
    Assert(ir3Only.Flag == PsxGteFlag.Ir3Saturated && !ir3Only.HasError &&
           ir3Only.Ir3 == short.MaxValue && ir3Only.Fifo.Sz3 == 40_000,
        $"IR3-only saturation/summary quirk changed: FLAG 0x{(uint)ir3Only.Flag:X8}.");

    PsxGteProjectionResult saturated = PsxGteProjection.Rtps(
        IdentityRegisters(0, 0, 256, short.MaxValue, int.MaxValue),
        PsxGteProjectionFifo.Empty,
        new PsxGteVector(short.MaxValue, short.MinValue, -1),
        PsxGteProjectionCommand.SpyroEnvironment);
    PsxGteFlag required = PsxGteFlag.Error |
                          PsxGteFlag.Sx2Saturated |
                          PsxGteFlag.Sy2Saturated |
                          PsxGteFlag.DivideOverflow |
                          PsxGteFlag.Sz3OrOtzSaturated |
                          PsxGteFlag.Mac0Overflow |
                          PsxGteFlag.Ir0Saturated;
    Assert((saturated.Flag & required) == required,
        $"RTPS saturation fixture missed FLAG bits: 0x{(uint)saturated.Flag:X8}.");

    PsxGteProjectionResult sf0Ir3Quirk = PsxGteProjection.Rtps(
        IdentityRegisters(0, 0, 1, 0, 0),
        PsxGteProjectionFifo.Empty,
        new PsxGteVector(0, 0, short.MaxValue),
        new PsxGteProjectionCommand(ShiftFraction: false, LimitMode: false));
    Assert(sf0Ir3Quirk.Ir3 == short.MaxValue &&
           (sf0Ir3Quirk.Flag & PsxGteFlag.Ir3Saturated) == 0,
        "sf=0 RTPS incorrectly based IR3 FLAG.22 on the lm-clamped MAC3 register.");

    PsxGteProjectionResult mac44 = PsxGteProjection.Rtps(
        new PsxGteProjectionRegisters(
            new PsxGteRotationMatrix(
                short.MaxValue, 0, 0,
                0, 0, 0,
                0, 0, 0),
            new PsxGteTranslation(int.MaxValue, 0, 1),
            0,
            0,
            1,
            0,
            0),
        PsxGteProjectionFifo.Empty,
        new PsxGteVector(short.MaxValue, 0, 0),
        PsxGteProjectionCommand.SpyroEnvironment);
    Assert((mac44.Flag & (PsxGteFlag.Mac1Overflow | PsxGteFlag.Error)) ==
           (PsxGteFlag.Mac1Overflow | PsxGteFlag.Error),
        "Sequential signed-44-bit RTPS MAC1 overflow was not preserved in FLAG.");

    Assert(PsxGteProjection.ApplyErrorSummary(PsxGteFlag.Ir3Saturated) == PsxGteFlag.Ir3Saturated &&
           PsxGteProjection.ApplyErrorSummary(PsxGteFlag.DivideOverflow) ==
           (PsxGteFlag.DivideOverflow | PsxGteFlag.Error),
        "FLAG bit-31 summary mask changed its hardware exclusions/inclusions.");
}

static void AssertRetailCameraFoundation()
{
    Assert(SpyroNativeTerrainCamera.RetailCosineSamples.Length == 257,
        "Retail cosine interpolation table length changed.");
    Assert(SpyroNativeTerrainCamera.SinQ12(0) == 0 &&
           SpyroNativeTerrainCamera.SinQ12(1) == 6 &&
           SpyroNativeTerrainCamera.SinQ12(1024) == 4096 &&
           SpyroNativeTerrainCamera.SinQ12(2048) == 0 &&
           SpyroNativeTerrainCamera.SinQ12(3072) == -4096 &&
           SpyroNativeTerrainCamera.SinQ12(4095) == -7 &&
           SpyroNativeTerrainCamera.SinQ12(-1) == -7,
        "Retail interpolated Sin fixtures changed.");
    Assert(SpyroNativeTerrainCamera.CosQ12(0) == 4096 &&
           SpyroNativeTerrainCamera.CosQ12(1) == 4095 &&
           SpyroNativeTerrainCamera.CosQ12(1024) == 0 &&
           SpyroNativeTerrainCamera.CosQ12(2048) == -4096 &&
           SpyroNativeTerrainCamera.CosQ12(3072) == 0 &&
           SpyroNativeTerrainCamera.CosQ12(4095) == 4095 &&
           SpyroNativeTerrainCamera.CosQ12(-1) == 4095,
        "Retail interpolated Cos fixtures changed.");

    AssertMatrix(
        SpyroNativeTerrainCamera.BuildViewRotation(default),
        PsxGteRotationMatrix.Identity,
        "zero camera rotation");
    AssertMatrix(
        SpyroNativeTerrainCamera.BuildViewRotation(new SpyroRetailRotation4096(1024, 0, 0)),
        new PsxGteRotationMatrix(
            0, 4096, 0,
            -4096, 0, 0,
            0, 0, 4096),
        "retail rot.x quarter turn");
    AssertMatrix(
        SpyroNativeTerrainCamera.BuildViewRotation(new SpyroRetailRotation4096(0, 1024, 0)),
        new PsxGteRotationMatrix(
            4096, 0, 0,
            0, 0, -4096,
            0, 4096, 0),
        "retail rot.y quarter turn");
    AssertMatrix(
        SpyroNativeTerrainCamera.BuildViewRotation(new SpyroRetailRotation4096(0, 0, 1024)),
        new PsxGteRotationMatrix(
            0, 0, 4096,
            0, 4096, 0,
            -4096, 0, 0),
        "retail rot.z quarter turn");

    SpyroRetailTerrainCameraInput uncertified = new(
        default,
        default,
        SpyroRetailCameraInputCertification.None);
    Assert(!SpyroNativeTerrainCamera.TryBuildProjectionRegisters(uncertified, out _) &&
           !SpyroNativeTerrainCamera.TryBuildState(uncertified, out _),
        "Uncertified camera input did not fail closed.");

    Assert(!SpyroEditorDerivedTerrainCamera.TryCreate(
            1, 2, 3, 0, 0,
            gameViewYFlipped: false,
            out _) &&
           !SpyroEditorDerivedTerrainCamera.TryCreate(
            double.NaN, 2, 3, 0, 0,
            gameViewYFlipped: true,
            out _),
        "Unsupported editor Fly camera input did not fail closed.");
    Assert(SpyroEditorDerivedTerrainCamera.TryCreate(
            1.03125,
            -2.03125,
            -3.03125,
            Math.PI / 2.0,
            -Math.PI / 4.0,
            gameViewYFlipped: true,
            out SpyroRetailTerrainCameraInput editorDerivedInput) &&
           editorDerivedInput.Position == new SpyroRetailFixed16Position(17, 33, -49) &&
           editorDerivedInput.Rotation == new SpyroRetailRotation4096(0, 512, -1024) &&
           editorDerivedInput.Certification ==
               SpyroRetailCameraInputCertification.EditorDerivedQuantizedFixed16PositionAndSigned4096TurnRotation &&
           SpyroNativeTerrainCamera.TryBuildState(editorDerivedInput, out SpyroRetailTerrainCameraState editorDerivedState) &&
           editorDerivedState.IsCertified && editorDerivedState.IsEditorDerived,
        $"Editor-derived fixed camera quantization changed: {editorDerivedInput}.");

    SpyroRetailTerrainCameraInput cameraInput = new(
        new SpyroRetailFixed16Position(16_015, 32_007, 48_015),
        new SpyroRetailRotation4096(-257, 511, 1025),
        SpyroRetailCameraInputCertification.RetailFixed16PositionAndSigned4096TurnRotation);
    Assert(SpyroNativeTerrainCamera.TryBuildState(cameraInput, out SpyroRetailTerrainCameraState camera),
        "Certified retail camera did not build.");
    PsxGteProjectionRegisters registers = camera.ProjectionRegisters;
    Assert(registers.Rotation == camera.ViewRotation &&
           registers.Translation == PsxGteTranslation.Zero &&
           registers.Ofx == 256 << 16 && registers.Ofy == 120 << 16 &&
           registers.H == 341 && registers.Dqa == 0x100 && registers.Dqb == 0,
        "Retail terrain control-register constants or unscaled view rotation changed.");

    uint packedVertex = (5u << 21) | (7u << 10) | 9u;
    SpyroRetailPackedTerrainVertexInput packed = new(
        (1000u << 16) | 2000u,
        (3000u << 16) | 0x1234u,
        packedVertex,
        SpyroRetailTerrainCoordinateCertification.RetailPackedSceneWords);
    Assert(SpyroNativeTerrainCamera.TryDecodeLowPolyWorldPoint(packed, out SpyroRetailLowPolyWorldPoint lpWorld) &&
           lpWorld == new SpyroRetailLowPolyWorldPoint(
               1005, 2007, 3009,
               SpyroRetailTerrainCoordinateCertification.RetailPackedSceneWords),
        $"Retail LP packed decode changed: {lpWorld}.");
    Assert(SpyroNativeTerrainCamera.TryDecodeHighPolyWorldPoint4(packed, out SpyroRetailHighPolyWorldPoint4 hpWorld) &&
           hpWorld == new SpyroRetailHighPolyWorldPoint4(
               4020, 8028, 12036,
               SpyroRetailTerrainCoordinateCertification.RetailPackedSceneWords),
        $"Retail HP packed decode changed: {hpWorld}.");
    Assert(SpyroNativeTerrainCamera.TryAdaptPackedLowPolyVertexToGte(camera, packed, out PsxGteVector lpVertex) &&
           lpVertex == new PsxGteVector(-7, -9, 5),
        $"Retail LP world-to-GTE permutation changed: {lpVertex}.");
    Assert(SpyroNativeTerrainCamera.TryAdaptPackedHighPolyVertexToGte(camera, packed, out PsxGteVector hpVertex) &&
           hpVertex == new PsxGteVector(-27, -33, 17),
        $"Retail HP world-to-GTE permutation/scale changed: {hpVertex}.");

    SpyroRetailPackedTerrainVertexInput retainedOriginBits = packed with
    {
        SectorXyWord = (1000u << 16) | 0xC123u,
        SectorZWord = (3000u << 16) | 0x8123u
    };
    Assert(SpyroNativeTerrainCamera.TryDecodeHighPolyWorldPoint4(
               retainedOriginBits,
               out SpyroRetailHighPolyWorldPoint4 retained) &&
           retained == new SpyroRetailHighPolyWorldPoint4(
               4023, 197800, 12038,
               SpyroRetailTerrainCoordinateCertification.RetailPackedSceneWords),
        $"HP sector-origin low-bit retention changed: {retained}.");

    SpyroRetailPackedTerrainVertexInput unprovenPacked = packed with
    {
        Certification = SpyroRetailTerrainCoordinateCertification.None
    };
    Assert(!SpyroNativeTerrainCamera.TryDecodeLowPolyWorldPoint(unprovenPacked, out _) &&
           !SpyroNativeTerrainCamera.TryDecodeHighPolyWorldPoint4(unprovenPacked, out _) &&
           !SpyroNativeTerrainCamera.TryAdaptPackedLowPolyVertexToGte(camera, unprovenPacked, out _) &&
           !SpyroNativeTerrainCamera.TryAdaptPackedHighPolyVertexToGte(camera, unprovenPacked, out _),
        "Uncertified packed terrain coordinates did not fail closed.");

    SpyroRetailLowPolyWorldPoint lpBoundary = new(
        short.MaxValue,
        32768,
        short.MaxValue,
        SpyroRetailTerrainCoordinateCertification.IndependentlyVerifiedNativeCoordinates);
    SpyroRetailTerrainCameraInput zeroInput = new(
        default,
        default,
        SpyroRetailCameraInputCertification.RetailFixed16PositionAndSigned4096TurnRotation);
    Assert(SpyroNativeTerrainCamera.TryBuildState(zeroInput, out SpyroRetailTerrainCameraState zeroCamera),
        "Certified zero retail camera did not build.");
    Assert(SpyroNativeTerrainCamera.TryAdaptLowPolyWorldToGte(zeroCamera, lpBoundary, out PsxGteVector lpBoundaryVertex) &&
           lpBoundaryVertex == new PsxGteVector(short.MinValue, -short.MaxValue, short.MaxValue),
        $"LP signed-halfword boundary changed: {lpBoundaryVertex}.");
    Assert(!SpyroNativeTerrainCamera.TryAdaptLowPolyWorldToGte(
            zeroCamera,
            lpBoundary with { Y = 32769 },
            out _) &&
           !SpyroNativeTerrainCamera.TryAdaptLowPolyWorldToGte(
            zeroCamera,
            lpBoundary with { X = 32768 },
            out _),
        "LP overflow did not fail closed before GTE halfword packing.");

    SpyroRetailTerrainCameraInput negativeInput = new(
        new SpyroRetailFixed16Position(-4, -8, -12),
        default,
        SpyroRetailCameraInputCertification.RetailFixed16PositionAndSigned4096TurnRotation);
    Assert(SpyroNativeTerrainCamera.TryBuildState(negativeInput, out SpyroRetailTerrainCameraState negativeCamera),
        "Certified negative fixed-16 camera did not build.");
    SpyroRetailHighPolyWorldPoint4 hpNegativeFixture = new(
        -7,
        -11,
        -15,
        SpyroRetailTerrainCoordinateCertification.IndependentlyVerifiedNativeCoordinates);
    Assert(SpyroNativeTerrainCamera.TryAdaptHighPolyWorldToGte(
               negativeCamera,
               hpNegativeFixture,
               out PsxGteVector negativeHp) &&
           negativeHp == new PsxGteVector(9, 12, -6),
        $"HP logical-srl/wrapped-subtraction fixture changed: {negativeHp}.");
    Assert(!SpyroNativeTerrainCamera.TryAdaptHighPolyWorldToGte(
            zeroCamera,
            hpNegativeFixture with { Y4 = 32769 },
            out _) &&
           !SpyroNativeTerrainCamera.TryAdaptHighPolyWorldToGte(
            zeroCamera,
            hpNegativeFixture with { X4 = 32768 },
            out _),
        "HP overflow did not fail closed after exact MIPS low-halfword reconstruction.");
}

static string BuildRetailCosineTableSha256()
{
    ReadOnlySpan<ushort> table = SpyroNativeTerrainCamera.RetailCosineSamples;
    byte[] bytes = new byte[table.Length * sizeof(ushort)];
    for (int index = 0; index < table.Length; index++)
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(index * sizeof(ushort)), table[index]);
    return Convert.ToHexString(SHA256.HashData(bytes));
}

static string BuildCameraFixtureSha256()
{
    const int fixtureCount = 4096;
    using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    uint random = 0xC4A3E19D;
    Span<byte> row = stackalloc byte[52];

    for (int index = 0; index < fixtureCount; index++)
    {
        int cameraX = (int)(Next(ref random) & 0x7FFFu);
        int cameraY = (int)(Next(ref random) & 0x7FFFu);
        int cameraZ = (int)(Next(ref random) & 0x7FFFu);
        SpyroRetailTerrainCameraInput input = new(
            new SpyroRetailFixed16Position(
                checked((cameraX * 16) + (int)(Next(ref random) & 0xFu)),
                checked((cameraY * 16) + (int)(Next(ref random) & 0xFu)),
                checked((cameraZ * 16) + (int)(Next(ref random) & 0xFu))),
            new SpyroRetailRotation4096(
                NextShort(ref random),
                NextShort(ref random),
                NextShort(ref random)),
            SpyroRetailCameraInputCertification.RetailFixed16PositionAndSigned4096TurnRotation);
        Assert(SpyroNativeTerrainCamera.TryBuildState(input, out SpyroRetailTerrainCameraState camera),
            "Certified random retail camera failed to build.");

        uint sectorX = Next(ref random) & 0x7FFFu;
        uint sectorY = Next(ref random) & 0x7FFFu;
        uint sectorZ = Next(ref random) & 0x7FFFu;
        uint localX = Next(ref random) & 0x7FFu;
        uint localY = Next(ref random) & 0x7FFu;
        uint localZ = Next(ref random) & 0x3FFu;
        SpyroRetailPackedTerrainVertexInput packed = new(
            (sectorX << 16) | sectorY,
            (sectorZ << 16) | (Next(ref random) & 0x3FFFu),
            (localX << 21) | (localY << 10) | localZ,
            SpyroRetailTerrainCoordinateCertification.RetailPackedSceneWords);

        bool lpSuccess = SpyroNativeTerrainCamera.TryAdaptPackedLowPolyVertexToGte(camera, packed, out PsxGteVector lp);
        bool hpSuccess = SpyroNativeTerrainCamera.TryAdaptPackedHighPolyVertexToGte(camera, packed, out PsxGteVector hp);
        row.Clear();
        int offset = 0;
        WriteMatrix(camera.ViewRotation, row, ref offset);
        WriteMatrix(camera.ProjectionRegisters.Rotation, row, ref offset);
        row[offset++] = lpSuccess ? (byte)1 : (byte)0;
        WriteVector(lp, row, ref offset);
        row[offset++] = hpSuccess ? (byte)1 : (byte)0;
        WriteVector(hp, row, ref offset);
        Assert(offset == 50, "Retail camera fixture row layout changed.");
        hash.AppendData(row);
    }

    return Convert.ToHexString(hash.GetHashAndReset());
}

static void AssertMatrix(PsxGteRotationMatrix actual, PsxGteRotationMatrix expected, string label) =>
    Assert(actual == expected, $"{label} matrix changed: {actual}; expected {expected}.");

static void WriteMatrix(PsxGteRotationMatrix matrix, Span<byte> destination, ref int offset)
{
    WriteInt16(destination, ref offset, matrix.M11);
    WriteInt16(destination, ref offset, matrix.M12);
    WriteInt16(destination, ref offset, matrix.M13);
    WriteInt16(destination, ref offset, matrix.M21);
    WriteInt16(destination, ref offset, matrix.M22);
    WriteInt16(destination, ref offset, matrix.M23);
    WriteInt16(destination, ref offset, matrix.M31);
    WriteInt16(destination, ref offset, matrix.M32);
    WriteInt16(destination, ref offset, matrix.M33);
}

static void WriteVector(PsxGteVector vector, Span<byte> destination, ref int offset)
{
    WriteInt16(destination, ref offset, vector.X);
    WriteInt16(destination, ref offset, vector.Y);
    WriteInt16(destination, ref offset, vector.Z);
}

static string BuildRandomFixtureSha256()
{
    const int fixtureCount = 4096;
    using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    uint random = 0x5A17C9E3;
    Span<byte> row = stackalloc byte[52];

    for (int index = 0; index < fixtureCount; index++)
    {
        PsxGteRotationMatrix rotation = new(
            NextShort(ref random), NextShort(ref random), NextShort(ref random),
            NextShort(ref random), NextShort(ref random), NextShort(ref random),
            NextShort(ref random), NextShort(ref random), NextShort(ref random));
        PsxGteProjectionRegisters registers = new(
            rotation,
            new PsxGteTranslation(NextInt(ref random), NextInt(ref random), NextInt(ref random)),
            NextInt(ref random),
            NextInt(ref random),
            NextUShort(ref random),
            NextShort(ref random),
            NextInt(ref random));
        PsxGteProjectionFifo fifo = new(
            new PsxGteScreenPoint(NextShort(ref random), NextShort(ref random)),
            new PsxGteScreenPoint(NextShort(ref random), NextShort(ref random)),
            new PsxGteScreenPoint(NextShort(ref random), NextShort(ref random)),
            NextUShort(ref random), NextUShort(ref random),
            NextUShort(ref random), NextUShort(ref random));
        PsxGteVector vertex0 = NextVector(ref random);
        PsxGteVector vertex1 = NextVector(ref random);
        PsxGteVector vertex2 = NextVector(ref random);
        PsxGteProjectionCommand command = new(
            ShiftFraction: (Next(ref random) & 1) != 0,
            LimitMode: (Next(ref random) & 1) != 0);

        PsxGteProjectionResult result = (index & 1) == 0
            ? PsxGteProjection.Rtps(registers, fifo, vertex0, command)
            : PsxGteProjection.Rtpt(registers, fifo, vertex0, vertex1, vertex2, command);
        SerializeResult(result, row);
        hash.AppendData(row);
    }

    return Convert.ToHexString(hash.GetHashAndReset());
}

static void SerializeResult(PsxGteProjectionResult result, Span<byte> destination)
{
    Assert(destination.Length == 52, "GTE fixture row size changed.");
    int offset = 0;
    WriteUInt32(destination, ref offset, (uint)result.Flag);
    WriteUInt32(destination, ref offset, result.Fifo.Sxy0.Packed);
    WriteUInt32(destination, ref offset, result.Fifo.Sxy1.Packed);
    WriteUInt32(destination, ref offset, result.Fifo.Sxy2.Packed);
    WriteUInt16(destination, ref offset, result.Fifo.Sz0);
    WriteUInt16(destination, ref offset, result.Fifo.Sz1);
    WriteUInt16(destination, ref offset, result.Fifo.Sz2);
    WriteUInt16(destination, ref offset, result.Fifo.Sz3);
    WriteInt32(destination, ref offset, result.Mac0);
    WriteInt32(destination, ref offset, result.Mac1);
    WriteInt32(destination, ref offset, result.Mac2);
    WriteInt32(destination, ref offset, result.Mac3);
    WriteInt16(destination, ref offset, result.Ir0);
    WriteInt16(destination, ref offset, result.Ir1);
    WriteInt16(destination, ref offset, result.Ir2);
    WriteInt16(destination, ref offset, result.Ir3);
    WriteUInt32(destination, ref offset, result.LastPerspectiveFactor);
    Assert(offset == destination.Length, "GTE fixture row was not filled exactly.");
}

static void WriteUInt16(Span<byte> destination, ref int offset, ushort value)
{
    BinaryPrimitives.WriteUInt16LittleEndian(destination[offset..], value);
    offset += sizeof(ushort);
}

static void WriteInt16(Span<byte> destination, ref int offset, short value)
{
    BinaryPrimitives.WriteInt16LittleEndian(destination[offset..], value);
    offset += sizeof(short);
}

static void WriteUInt32(Span<byte> destination, ref int offset, uint value)
{
    BinaryPrimitives.WriteUInt32LittleEndian(destination[offset..], value);
    offset += sizeof(uint);
}

static void WriteInt32(Span<byte> destination, ref int offset, int value)
{
    BinaryPrimitives.WriteInt32LittleEndian(destination[offset..], value);
    offset += sizeof(int);
}

static PsxGteProjectionRegisters IdentityRegisters(int ofx, int ofy, ushort h, short dqa, int dqb) =>
    new(PsxGteRotationMatrix.Identity, PsxGteTranslation.Zero, ofx, ofy, h, dqa, dqb);

static PsxGteVector NextVector(ref uint state) =>
    new(NextShort(ref state), NextShort(ref state), NextShort(ref state));

static uint Next(ref uint state)
{
    state = unchecked((state * 1_664_525u) + 1_013_904_223u);
    return state;
}

static int NextInt(ref uint state) => unchecked((int)Next(ref state));
static ushort NextUShort(ref uint state) => unchecked((ushort)Next(ref state));
static short NextShort(ref uint state) => unchecked((short)Next(ref state));

static void AssertDivide(ushort h, ushort sz, uint expected, bool overflow)
{
    uint actual = PsxGteProjection.DividePerspective(h, sz, out bool actualOverflow);
    Assert(actual == expected && actualOverflow == overflow,
        $"UNR divide {h:X4}/{sz:X4} produced {actual:X5}/{actualOverflow}, " +
        $"expected {expected:X5}/{overflow}.");
}

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
