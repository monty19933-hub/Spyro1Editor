namespace Spyro.Editor.Core.Analysis;

internal enum NativeGreenWizardCorrectionObservation
{
    Unknown,
    None,
    Required
}

internal sealed record NativeGreenWizardHeading(byte Yaw, bool WithinThreshold);
internal sealed record NativeGreenWizardUncorrectedMove(int X, int Y, int Z,
    int ResidentQueryRadius, int TerrainQueryRadius, int TerrainProbeZ,
    int FloorProbeZ, int FloorProbeRange, string Scope)
{
    public bool PlacementCertified => false;
    public bool BodyEnvelopeCertified => false;
    public bool RuntimeVerified => false;
}

/// <summary>
/// Bounded arithmetic transcriptions of the pinned native helpers. Supplied
/// floor/correction observations are caller inputs, never source authority.
/// This does not simulate collision queries, bearing selection, actor state,
/// animation transforms, rendered geometry, or a complete native trajectory.
/// </summary>
internal static class NativeGreenWizardMoverMath
{
    internal const string Scope = "Uncorrected arithmetic only; caller-supplied floor and no-correction observations are not verified. Movement probes are not a body envelope or placement/runtime certificate.";

    public static int ScaleArgument(int argument, int nativeGlobalValue)
    {
        if (argument is < 0 or > 4096)
            throw new InvalidDataException("Wizard mover argument is outside the bounded arithmetic profile.");
        return nativeGlobalValue switch
        {
            3 => argument + (argument >> 1),
            4 => argument * 2,
            _ => argument
        };
    }

    public static int SettleZ(int currentZ, int nativeFloorZ, short groundOffset)
    {
        ValidateCoordinate(currentZ);
        ValidateCoordinate(nativeFloorZ);
        int target = nativeFloorZ + groundOffset;
        int delta = target - currentZ;
        // Native slti 0x259 is true through absolute delta 600, not 601.
        return Math.Abs(delta) <= 600 ? target : currentZ + (delta > 0 ? 250 : -250);
    }

    public static int ArrivalMetric(int x, int y, int targetX, int targetY)
    {
        ValidateCoordinate(x);
        ValidateCoordinate(y);
        ValidateCoordinate(targetX);
        ValidateCoordinate(targetY);
        int dx = Math.Abs(targetX - x);
        int dy = Math.Abs(targetY - y);
        return Math.Max(dx, dy) + ((3 * Math.Min(dx, dy)) >> 3);
    }

    public static NativeGreenWizardHeading Turn(byte current, byte desired, int nativeGlobalValue)
    {
        // This is the Magic resident caller's fixed fifth-argument=1 profile,
        // not every mode of 0x80038EE0. Its shared dispatch initializes $fp=1
        // at 0x8007E420, and 0x800888C0 stores $fp as that stack argument.
        int wrapped = (desired - current) & 255;
        int distance = wrapped >= 128 ? 256 - wrapped : wrapped;
        int step = Math.Min(distance, ScaleArgument(8, nativeGlobalValue));
        int deadband = (step >> 1) + 1;
        int result = current;
        // 0x800179F0 chooses the positive direction for an exact half turn.
        if (wrapped <= 128)
        {
            if (wrapped >= deadband)
                result += step;
        }
        else if (wrapped - 256 + deadband <= 0)
        {
            result -= step;
        }
        // The return condition is evaluated before the step, at strict <16.
        return new((byte)(result & 255), distance < 16);
    }

    public static NativeGreenWizardUncorrectedMove ProposeUncorrected(
        int x, int y, int currentZ, short cosine, short sine, int nativeGlobalValue,
        short groundOffset, int callerObservedFloorZ,
        NativeGreenWizardCorrectionObservation residentCorrection,
        NativeGreenWizardCorrectionObservation terrainCorrection)
    {
        if (residentCorrection != NativeGreenWizardCorrectionObservation.None)
            throw new InvalidDataException("Wizard resident correction is unsupported or unresolved; no proposal issued.");
        if (terrainCorrection != NativeGreenWizardCorrectionObservation.None)
            throw new InvalidDataException("Wizard terrain correction is unsupported or unresolved; no proposal issued.");
        ValidateCoordinate(x);
        ValidateCoordinate(y);
        ValidateCoordinate(currentZ);
        if (cosine is < -4096 or > 4096 || sine is < -4096 or > 4096)
            throw new InvalidDataException("Wizard trig coefficient is outside the pinned native table range.");
        int speed = ScaleArgument(96, nativeGlobalValue);
        int nextX = x + ((cosine * speed) >> 12);
        int nextY = y + ((sine * speed) >> 12);
        return new(nextX, nextY, SettleZ(currentZ, callerObservedFloorZ, groundOffset),
            384, 384, currentZ + 300 + 384, currentZ + 1024, 5000, Scope);
    }

    private static void ValidateCoordinate(int value)
    {
        if (Math.Abs((long)value) > 1_000_000)
            throw new InvalidDataException("Wizard coordinate is outside the bounded native arithmetic profile.");
    }
}
