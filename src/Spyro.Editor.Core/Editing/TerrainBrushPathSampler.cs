using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Editing;

public static class TerrainBrushPathSampler
{
    public static IReadOnlyList<TerrainBrushPathSample> Build(Vector2f previous, Vector2f current, double stepDistance, double residualThresholdRatio = 0.35)
    {
        double safeStepDistance = Math.Max(0.001, stepDistance);
        double thresholdRatio = Math.Clamp(residualThresholdRatio, 0.05, 1.0);
        float dx = current.X - previous.X;
        float dy = current.Y - previous.Y;
        double distance = Math.Sqrt((dx * dx) + (dy * dy));
        if (distance < safeStepDistance)
        {
            double ratio = distance / safeStepDistance;
            return ratio >= thresholdRatio
                ? [new TerrainBrushPathSample(current, Math.Clamp(ratio, thresholdRatio, 1.0))]
                : Array.Empty<TerrainBrushPathSample>();
        }

        int stepCount = Math.Max(1, (int)Math.Floor(distance / safeStepDistance));
        List<TerrainBrushPathSample> samples = new(stepCount + 1);
        for (int step = 1; step <= stepCount; step++)
        {
            double t = Math.Min(1, (step * safeStepDistance) / distance);
            samples.Add(new TerrainBrushPathSample(
                new Vector2f(
                    previous.X + (float)(dx * t),
                    previous.Y + (float)(dy * t)),
                1.0));
        }

        double remainder = distance - (stepCount * safeStepDistance);
        if (remainder >= safeStepDistance * thresholdRatio)
            samples.Add(new TerrainBrushPathSample(current, Math.Clamp(remainder / safeStepDistance, thresholdRatio, 1.0)));

        return samples;
    }

    public static TerrainBrushPathSample? BuildFinalResidual(Vector2f previous, Vector2f current, double stepDistance, double minimumStrengthRatio = 0.12)
    {
        double safeStepDistance = Math.Max(0.001, stepDistance);
        double minimumRatio = Math.Clamp(minimumStrengthRatio, 0.01, 1.0);
        float dx = current.X - previous.X;
        float dy = current.Y - previous.Y;
        double distance = Math.Sqrt((dx * dx) + (dy * dy));
        if (distance <= 0.001 || distance >= safeStepDistance)
            return null;

        double ratio = distance / safeStepDistance;
        return ratio >= minimumRatio
            ? new TerrainBrushPathSample(current, Math.Clamp(ratio, minimumRatio, 1.0))
            : null;
    }
}

public readonly record struct TerrainBrushPathSample(Vector2f Center, double StrengthScale);
