using System.Buffers.Binary;
using System.Globalization;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Rendering;

namespace Spyro.Editor.Core.Exporting;

public readonly record struct DragonRunToEndpoint(int RawX, int RawY)
{
    public double EditorX => RawX / (double)SpyroNativeTerrainCamera.CameraPositionScale;
    public double EditorY => RawY / (double)SpyroNativeTerrainCamera.CameraPositionScale;
}

public readonly record struct DragonRunToEncoding(int Angle, int Radius);

public sealed record DragonRunToValidation(
    MobyBuildSafetyStatus Status,
    string Code,
    string Message,
    long DeltaRawX,
    long DeltaRawY,
    int RadiusRaw)
{
    public bool CanPatch => Status != MobyBuildSafetyStatus.Blocked;
}

public sealed record DragonRunToEncodingResult(
    DragonRunToEndpoint RequestedEndpoint,
    DragonRunToEndpoint EncodedEndpoint,
    DragonRunToEncoding Encoding,
    DragonRunToValidation Validation,
    double QuantizationErrorRaw)
{
    public bool CanPatch => Validation.CanPatch;
}

public sealed record DragonRunToFieldPatch(
    string Kind,
    int FieldOffset,
    long WadOffset,
    int BeforeValue,
    int AfterValue,
    string Description)
{
    public MobySourcePatch ToMobySourcePatch(
        LevelDefinition level,
        int dragonTrueIndex,
        string dragonLabel)
    {
        return new MobySourcePatch(
            Label: $"{dragonLabel} run-to {FieldName(FieldOffset)}",
            Kind: Kind,
            LevelKey: level.Key,
            MobyLabel: dragonLabel,
            TrueIndex: dragonTrueIndex,
            RecordOffset: $"properties+0x{FieldOffset:X}",
            WadRelativeOffset: $"0x{WadOffset:X}",
            ImageOffset: "",
            ByteLength: sizeof(int),
            BeforeHexPreview: ToHex(BeforeValue),
            AfterHexPreview: ToHex(AfterValue),
            Description: Description);
    }

    private static string FieldName(int offset) => offset switch
    {
        DragonRescueRunTo.AngleFieldOffset => "angle",
        DragonRescueRunTo.RadiusFieldOffset => "radius",
        _ => $"field-0x{offset:X}"
    };

    private static string ToHex(int value) => string.Join(
        " ",
        BitConverter.GetBytes(value).Select(part => part.ToString("X2", CultureInfo.InvariantCulture)));
}

public sealed record DragonRunToPatchPlan(
    DragonRescueCameraData Scene,
    DragonRunToEncodingResult Result,
    IReadOnlyList<DragonRunToFieldPatch> FieldPatches)
{
    public bool CanPatch => Result.CanPatch;
    public int PatchCount => FieldPatches.Count;

    public IReadOnlyList<MobySourcePatch> ToMobySourcePatches(
        LevelDefinition level,
        string dragonLabel)
    {
        if (!CanPatch)
            return Array.Empty<MobySourcePatch>();

        return FieldPatches
            .Select(patch => patch.ToMobySourcePatch(level, Scene.DragonTrueIndex, dragonLabel))
            .ToArray();
    }
}

/// <summary>
/// Decodes and encodes the procedural XY destination used while Spyro runs to
/// a rescued dragon. The packed properties record stores a 4096-step planar
/// angle at +0x28 and a raw fixed-16 radius at +0x2C. +0x30 is deliberately
/// preimage-guarded but never changed because retail does not read it as the
/// run destination.
/// </summary>
public static class DragonRescueRunTo
{
    private const int WadLba = 37;

    public const int AngleFieldOffset = 0x28;
    public const int RadiusFieldOffset = 0x2C;
    public const int AuxiliaryFieldOffset = 0x30;
    public const int PackedPropertiesByteLength = 0x44;

    // Complete USA retail-disc audit: 79 native dragon scenes in 28 levels.
    public const int NativeMinimumRadiusRaw = 2_289;
    public const int NativeMaximumRadiusRaw = 6_670;

    // Retail multiplies a signed Q12 trig component by radius and consumes the
    // low signed word before >> 12. Staying below this value prevents overflow
    // for either +4096 or -4096 components.
    public const int MaximumSafeRadiusRaw = (int.MaxValue / SpyroNativeTerrainCamera.MatrixOne);

    private const int AngleSearchRadius = 4;
    private const int RadialSearchRadius = 2;
    private const double ErrorTieTolerance = 0.0000001;

    public static int NormalizeAngle(int angle) => angle & 0xFFF;

    public static bool IsNativeAngleValue(int angle) => angle is >= -0x800 and <= 0xFFF;

    public static DragonRunToEndpoint DecodeEndpoint(
        int dragonRawX,
        int dragonRawY,
        int angle,
        int radius)
    {
        if (radius is <= 0 or > MaximumSafeRadiusRaw)
        {
            throw new ArgumentOutOfRangeException(
                nameof(radius),
                radius,
                $"Dragon run-to radius must be between 1 and {MaximumSafeRadiusRaw} raw units.");
        }

        int deltaX = MultiplyQ12(SpyroNativeTerrainCamera.CosQ12(angle), radius);
        int deltaY = MultiplyQ12(SpyroNativeTerrainCamera.SinQ12(angle), radius);
        return new DragonRunToEndpoint(
            checked(dragonRawX + deltaX),
            checked(dragonRawY + deltaY));
    }

    public static DragonRunToValidation ValidateEndpoint(
        int dragonRawX,
        int dragonRawY,
        int endpointRawX,
        int endpointRawY)
    {
        long deltaX = (long)endpointRawX - dragonRawX;
        long deltaY = (long)endpointRawY - dragonRawY;
        if (deltaX == 0 && deltaY == 0)
        {
            return new DragonRunToValidation(
                MobyBuildSafetyStatus.Blocked,
                "dragon-run-to-zero-radius",
                "Spyro's dragon run-to endpoint cannot occupy the exact dragon origin.",
                deltaX,
                deltaY,
                0);
        }

        double magnitude = Math.Sqrt(
            ((double)deltaX * deltaX) +
            ((double)deltaY * deltaY));
        if (!double.IsFinite(magnitude) || magnitude > MaximumSafeRadiusRaw + 0.499999)
        {
            return new DragonRunToValidation(
                MobyBuildSafetyStatus.Blocked,
                "dragon-run-to-radius-overflow",
                $"The dragon run-to radius exceeds the safe signed Q12 limit of {MaximumSafeRadiusRaw} raw units.",
                deltaX,
                deltaY,
                magnitude >= int.MaxValue ? int.MaxValue : (int)Math.Round(magnitude, MidpointRounding.AwayFromZero));
        }

        int radius = checked((int)Math.Round(magnitude, MidpointRounding.AwayFromZero));
        if (radius == 0)
        {
            return new DragonRunToValidation(
                MobyBuildSafetyStatus.Blocked,
                "dragon-run-to-zero-radius",
                "Spyro's dragon run-to endpoint is too close to the dragon to encode a nonzero radius.",
                deltaX,
                deltaY,
                radius);
        }

        if (radius < NativeMinimumRadiusRaw || radius > NativeMaximumRadiusRaw)
        {
            return new DragonRunToValidation(
                MobyBuildSafetyStatus.Review,
                "dragon-run-to-outside-retail-radius",
                $"The run-to radius is {radius} raw units ({radius / 16d:0.0} editor units), outside the audited retail range {NativeMinimumRadiusRaw}-{NativeMaximumRadiusRaw}; verify the route does not cross walls or voids.",
                deltaX,
                deltaY,
                radius);
        }

        return new DragonRunToValidation(
            MobyBuildSafetyStatus.Stable,
            "dragon-run-to-native-radius",
            $"The run-to radius is inside the audited 79-scene retail range ({NativeMinimumRadiusRaw}-{NativeMaximumRadiusRaw} raw units).",
            deltaX,
            deltaY,
            radius);
    }

    public static DragonRunToEncodingResult EncodeEndpoint(
        int dragonRawX,
        int dragonRawY,
        int endpointRawX,
        int endpointRawY)
    {
        return EncodeEndpointCore(
            dragonRawX,
            dragonRawY,
            endpointRawX,
            endpointRawY,
            preferredEncoding: null);
    }

    public static DragonRunToEncodingResult EncodeEndpoint(
        int dragonRawX,
        int dragonRawY,
        int endpointRawX,
        int endpointRawY,
        DragonRunToEncoding preferredEncoding)
    {
        return EncodeEndpointCore(
            dragonRawX,
            dragonRawY,
            endpointRawX,
            endpointRawY,
            preferredEncoding);
    }

    private static DragonRunToEncodingResult EncodeEndpointCore(
        int dragonRawX,
        int dragonRawY,
        int endpointRawX,
        int endpointRawY,
        DragonRunToEncoding? preferredEncoding)
    {
        DragonRunToEndpoint requested = new(endpointRawX, endpointRawY);
        DragonRunToValidation validation = ValidateEndpoint(
            dragonRawX,
            dragonRawY,
            endpointRawX,
            endpointRawY);
        if (!validation.CanPatch)
        {
            return new DragonRunToEncodingResult(
                requested,
                requested,
                default,
                validation,
                double.PositiveInfinity);
        }

        if (preferredEncoding is DragonRunToEncoding preferred &&
            preferred.Radius is > 0 and <= MaximumSafeRadiusRaw)
        {
            try
            {
                DragonRunToEndpoint preferredEndpoint = DecodeEndpoint(
                    dragonRawX,
                    dragonRawY,
                    preferred.Angle,
                    preferred.Radius);
                if (preferredEndpoint == requested)
                {
                    return new DragonRunToEncodingResult(
                        requested,
                        requested,
                        new DragonRunToEncoding(NormalizeAngle(preferred.Angle), preferred.Radius),
                        validation,
                        0);
                }
            }
            catch (OverflowException)
            {
                // The preferred source pair is not usable at this edited
                // origin. Continue through the ordinary validated encoder,
                // which reports a blocked result if its nearest pair also
                // overflows.
            }
        }

        double angleTurns = Math.Atan2(validation.DeltaRawY, validation.DeltaRawX) /
            (Math.PI * 2d);
        int initialAngle = NormalizeAngle((int)Math.Round(
            angleTurns * SpyroNativeTerrainCamera.AngleUnitsPerTurn,
            MidpointRounding.AwayFromZero));
        int initialRadius = validation.RadiusRaw;

        int bestAngle = initialAngle;
        int bestRadius = initialRadius;
        DragonRunToEndpoint bestRelative = DecodeEndpoint(0, 0, bestAngle, bestRadius);
        double bestErrorSquared = ErrorSquared(
            validation.DeltaRawX,
            validation.DeltaRawY,
            bestRelative.RawX,
            bestRelative.RawY);
        int bestAngleDistance = 0;
        int bestRadiusDistance = 0;

        for (int angleDelta = -AngleSearchRadius; angleDelta <= AngleSearchRadius; angleDelta++)
        {
            int candidateAngle = NormalizeAngle(initialAngle + angleDelta);
            for (int radiusDelta = -RadialSearchRadius; radiusDelta <= RadialSearchRadius; radiusDelta++)
            {
                int candidateRadius = initialRadius + radiusDelta;
                if (candidateRadius is <= 0 or > MaximumSafeRadiusRaw)
                    continue;

                DragonRunToEndpoint candidateRelative = DecodeEndpoint(0, 0, candidateAngle, candidateRadius);
                double candidateErrorSquared = ErrorSquared(
                    validation.DeltaRawX,
                    validation.DeltaRawY,
                    candidateRelative.RawX,
                    candidateRelative.RawY);
                int candidateAngleDistance = Math.Abs(angleDelta);
                int candidateRadiusDistance = Math.Abs(radiusDelta);
                if (!IsBetterCandidate(
                    candidateErrorSquared,
                    candidateAngleDistance,
                    candidateRadiusDistance,
                    candidateAngle,
                    bestErrorSquared,
                    bestAngleDistance,
                    bestRadiusDistance,
                    bestAngle))
                {
                    continue;
                }

                bestAngle = candidateAngle;
                bestRadius = candidateRadius;
                bestRelative = candidateRelative;
                bestErrorSquared = candidateErrorSquared;
                bestAngleDistance = candidateAngleDistance;
                bestRadiusDistance = candidateRadiusDistance;
            }
        }

        DragonRunToEndpoint encoded;
        try
        {
            encoded = new DragonRunToEndpoint(
                checked(dragonRawX + bestRelative.RawX),
                checked(dragonRawY + bestRelative.RawY));
        }
        catch (OverflowException)
        {
            DragonRunToValidation blocked = validation with
            {
                Status = MobyBuildSafetyStatus.Blocked,
                Code = "dragon-run-to-coordinate-overflow",
                Message = "The nearest encoded dragon run-to endpoint overflows the signed raw coordinate range."
            };
            return new DragonRunToEncodingResult(
                requested,
                requested,
                default,
                blocked,
                double.PositiveInfinity);
        }

        return new DragonRunToEncodingResult(
            requested,
            encoded,
            new DragonRunToEncoding(bestAngle, bestRadius),
            validation,
            Math.Sqrt(bestErrorSquared));
    }

    public static bool TryFromEditorUnits(
        double editorX,
        double editorY,
        out DragonRunToEndpoint endpoint,
        out string error)
    {
        endpoint = default;
        if (!double.IsFinite(editorX) || !double.IsFinite(editorY))
        {
            error = "Dragon run-to X and Y must be finite numbers.";
            return false;
        }

        double rawX = Math.Round(
            editorX * SpyroNativeTerrainCamera.CameraPositionScale,
            MidpointRounding.AwayFromZero);
        double rawY = Math.Round(
            editorY * SpyroNativeTerrainCamera.CameraPositionScale,
            MidpointRounding.AwayFromZero);
        if (rawX < int.MinValue || rawX > int.MaxValue || rawY < int.MinValue || rawY > int.MaxValue)
        {
            error = "Dragon run-to X or Y is outside the signed raw coordinate range.";
            return false;
        }

        endpoint = new DragonRunToEndpoint((int)rawX, (int)rawY);
        error = "";
        return true;
    }

    public static byte[] ReadPackedProperties(
        string sourceImagePath,
        DragonRescueCameraData scene)
    {
        if (!File.Exists(sourceImagePath))
            throw new FileNotFoundException("Missing source disc image.", sourceImagePath);
        ArgumentNullException.ThrowIfNull(scene);

        DiscLayout layout = DiscImage.DetectLayout(sourceImagePath);
        using FileStream stream = File.OpenRead(sourceImagePath);
        return DiscImage.ReadFileBytes(
            stream,
            layout,
            WadLba,
            scene.CameraDataWadOffset,
            PackedPropertiesByteLength);
    }

    public static DragonRunToPatchPlan BuildPatchPlan(
        DragonRescueCameraData scene,
        int finalDragonRawX,
        int finalDragonRawY,
        int endpointRawX,
        int endpointRawY,
        ReadOnlySpan<byte> packedProperties)
    {
        ArgumentNullException.ThrowIfNull(scene);
        if (packedProperties.Length < PackedPropertiesByteLength)
        {
            throw new ArgumentException(
                $"Dragon rescue properties must contain at least 0x{PackedPropertiesByteLength:X} bytes.",
                nameof(packedProperties));
        }

        RequirePreimage(packedProperties, AngleFieldOffset, scene.RunToAngle, "run-to angle");
        RequirePreimage(packedProperties, RadiusFieldOffset, scene.RunToRadius, "run-to radius");
        RequirePreimage(packedProperties, AuxiliaryFieldOffset, scene.RunToAuxiliary, "run-to auxiliary +0x30");

        DragonRunToEncodingResult result = EncodeEndpoint(
            finalDragonRawX,
            finalDragonRawY,
            endpointRawX,
            endpointRawY,
            new DragonRunToEncoding(scene.RunToAngle, scene.RunToRadius));
        if (!result.CanPatch)
            return new DragonRunToPatchPlan(scene, result, Array.Empty<DragonRunToFieldPatch>());

        List<DragonRunToFieldPatch> patches = [];
        if (NormalizeAngle(scene.RunToAngle) != result.Encoding.Angle)
        {
            patches.Add(new DragonRunToFieldPatch(
                Kind: "dragon-rescue-run-to-angle",
                FieldOffset: AngleFieldOffset,
                WadOffset: scene.CameraDataWadOffset + AngleFieldOffset,
                BeforeValue: scene.RunToAngle,
                AfterValue: result.Encoding.Angle,
                Description: $"Set Spyro's run-to angle for dragon T{scene.DragonTrueIndex} to 0x{result.Encoding.Angle:X3}, preserving camera, timing, and +0x30 auxiliary data."));
        }

        if (scene.RunToRadius != result.Encoding.Radius)
        {
            patches.Add(new DragonRunToFieldPatch(
                Kind: "dragon-rescue-run-to-radius",
                FieldOffset: RadiusFieldOffset,
                WadOffset: scene.CameraDataWadOffset + RadiusFieldOffset,
                BeforeValue: scene.RunToRadius,
                AfterValue: result.Encoding.Radius,
                Description: $"Set Spyro's run-to radius for dragon T{scene.DragonTrueIndex} to {result.Encoding.Radius} raw units, preserving camera, timing, and +0x30 auxiliary data."));
        }

        return new DragonRunToPatchPlan(scene, result, patches);
    }

    private static int MultiplyQ12(short component, int radius)
    {
        long product = (long)component * radius;
        if (product < int.MinValue || product > int.MaxValue)
            throw new OverflowException("Dragon run-to Q12 component multiplication exceeded the signed 32-bit retail range.");
        return ((int)product) >> 12;
    }

    private static double ErrorSquared(long requestedX, long requestedY, int encodedX, int encodedY)
    {
        double errorX = requestedX - encodedX;
        double errorY = requestedY - encodedY;
        return (errorX * errorX) + (errorY * errorY);
    }

    private static bool IsBetterCandidate(
        double candidateErrorSquared,
        int candidateAngleDistance,
        int candidateRadiusDistance,
        int candidateAngle,
        double bestErrorSquared,
        int bestAngleDistance,
        int bestRadiusDistance,
        int bestAngle)
    {
        if (candidateErrorSquared < bestErrorSquared - ErrorTieTolerance)
            return true;
        if (Math.Abs(candidateErrorSquared - bestErrorSquared) > ErrorTieTolerance)
            return false;
        if (candidateAngleDistance != bestAngleDistance)
            return candidateAngleDistance < bestAngleDistance;
        if (candidateRadiusDistance != bestRadiusDistance)
            return candidateRadiusDistance < bestRadiusDistance;
        return candidateAngle < bestAngle;
    }

    private static void RequirePreimage(
        ReadOnlySpan<byte> packedProperties,
        int fieldOffset,
        int expected,
        string label)
    {
        int actual = BinaryPrimitives.ReadInt32LittleEndian(packedProperties.Slice(fieldOffset, sizeof(int)));
        if (actual != expected)
        {
            throw new InvalidOperationException(
                $"Dragon {label} preimage at +0x{fieldOffset:X} is {actual}, expected {expected}; refusing to plan a patch against different source data.");
        }
    }
}
