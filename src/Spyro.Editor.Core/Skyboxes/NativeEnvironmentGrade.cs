using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Skyboxes;

public sealed record NativeEnvironmentGradePlan(
    int Version,
    bool Enabled,
    string Mode,
    string DonorLevelKey,
    int StrengthPercent,
    int BrightnessPercent,
    int SaturationPercent,
    string TintHex,
    int TintStrengthPercent,
    bool GradeSceneColors,
    bool GradeTexturePalettes,
    bool GradeActors,
    bool GradeChests,
    bool GradeScenery,
    bool GradeDragons)
{
    public const string MatchSkySourceMode = "match-sky-source";
    public const bool SupportsObjectPaletteMatching = true;
    public const bool SupportsScopedObjectPaletteMatching = false;

    public static NativeEnvironmentGradePlan Disabled { get; } = new(
        Version: 1,
        Enabled: false,
        Mode: MatchSkySourceMode,
        DonorLevelKey: "",
        StrengthPercent: 100,
        BrightnessPercent: 100,
        SaturationPercent: 100,
        TintHex: "",
        TintStrengthPercent: 0,
        GradeSceneColors: true,
        GradeTexturePalettes: true,
        GradeActors: false,
        GradeChests: false,
        GradeScenery: false,
        GradeDragons: false);

    public bool GradeAnyMobys => SupportsObjectPaletteMatching &&
        (GradeActors || GradeChests || GradeScenery || GradeDragons);

    public static NativeEnvironmentGradePlan MatchSkySource(string donorLevelKey) => Disabled with
    {
        Enabled = true,
        DonorLevelKey = donorLevelKey ?? ""
    };

    public NativeEnvironmentGradePlan Normalize(string skyDonorLevelKey = "")
    {
        string donor = string.IsNullOrWhiteSpace(DonorLevelKey) ? skyDonorLevelKey : DonorLevelKey;
        string tint = ColorRgba.TryParseHex(TintHex, out ColorRgba parsedTint)
            ? $"#{parsedTint.R:X2}{parsedTint.G:X2}{parsedTint.B:X2}"
            : "";
        return this with
        {
            Version = Math.Max(1, Version),
            Mode = MatchSkySourceMode,
            DonorLevelKey = donor ?? "",
            StrengthPercent = Math.Clamp(StrengthPercent, 0, 100),
            BrightnessPercent = Math.Clamp(BrightnessPercent, 40, 160),
            SaturationPercent = Math.Clamp(SaturationPercent, 0, 160),
            TintHex = tint,
            TintStrengthPercent = Math.Clamp(TintStrengthPercent, 0, 100),
            GradeActors = SupportsObjectPaletteMatching && GradeActors,
            GradeChests = SupportsObjectPaletteMatching && GradeChests,
            GradeScenery = SupportsObjectPaletteMatching && GradeScenery,
            GradeDragons = SupportsObjectPaletteMatching && GradeDragons
        };
    }
}

public sealed record NativeEnvironmentColorStatistics(
    int SampleCount,
    double MeanRed,
    double MeanGreen,
    double MeanBlue,
    double MeanLuminance,
    double MedianLuminance,
    double MeanSaturation,
    double LuminanceStandardDeviation,
    double DeepShadowPercent,
    string MeanColorHex)
{
    public static NativeEnvironmentColorStatistics FromColors(IEnumerable<ColorRgba> colors)
    {
        ColorRgba[] samples = colors
            .Where(color => color.R != 0 || color.G != 0 || color.B != 0)
            .ToArray();
        if (samples.Length == 0)
            return new NativeEnvironmentColorStatistics(0, 0, 0, 0, 0, 0, 0, 0, 0, "#000000");

        double meanRed = samples.Average(color => color.R / 255.0);
        double meanGreen = samples.Average(color => color.G / 255.0);
        double meanBlue = samples.Average(color => color.B / 255.0);
        double[] luminance = samples.Select(Luminance).Order().ToArray();
        double median = luminance.Length % 2 == 0
            ? (luminance[(luminance.Length / 2) - 1] + luminance[luminance.Length / 2]) / 2.0
            : luminance[luminance.Length / 2];
        int r = (int)Math.Round(meanRed * 255);
        int g = (int)Math.Round(meanGreen * 255);
        int b = (int)Math.Round(meanBlue * 255);
        double meanLuminance = luminance.Average();
        return new NativeEnvironmentColorStatistics(
            SampleCount: samples.Length,
            MeanRed: meanRed,
            MeanGreen: meanGreen,
            MeanBlue: meanBlue,
            MeanLuminance: meanLuminance,
            MedianLuminance: median,
            MeanSaturation: samples.Average(Saturation),
            LuminanceStandardDeviation: Math.Sqrt(luminance.Average(value => Math.Pow(value - meanLuminance, 2))),
            DeepShadowPercent: luminance.Count(value => value < 0.12) * 100.0 / luminance.Length,
            MeanColorHex: $"#{r:X2}{g:X2}{b:X2}");
    }

    private static double Luminance(ColorRgba color) =>
        ((0.2126 * color.R) + (0.7152 * color.G) + (0.0722 * color.B)) / 255.0;

    private static double Saturation(ColorRgba color)
    {
        double max = Math.Max(color.R, Math.Max(color.G, color.B));
        double min = Math.Min(color.R, Math.Min(color.G, color.B));
        return max <= 0 ? 0 : (max - min) / max;
    }
}

public sealed record NativeEnvironmentColorTransform(
    double RedScale,
    double GreenScale,
    double BlueScale,
    double SaturationScale,
    int StrengthPercent,
    string TintHex,
    int TintStrengthPercent,
    int HarmonizationPercent,
    double TargetMedianLuminance,
    double DonorMedianLuminance,
    double DonorMeanRed,
    double DonorMeanGreen,
    double DonorMeanBlue)
{
    public static NativeEnvironmentColorTransform Build(
        NativeEnvironmentColorStatistics target,
        NativeEnvironmentColorStatistics donor,
        NativeEnvironmentGradePlan plan)
    {
        if (target.SampleCount == 0 || donor.SampleCount == 0)
            throw new InvalidOperationException("The target and donor both need decoded environment colors.");

        NativeEnvironmentGradePlan normalized = plan.Normalize();
        double targetLuminance = Math.Max(0.015, target.MedianLuminance);
        double donorLuminance = Math.Max(0.015, donor.MedianLuminance);
        double luminanceScale = Math.Clamp(donorLuminance / targetLuminance, 0.28, 2.4);

        double targetMeanLuminance = Math.Max(0.015, target.MeanLuminance);
        double donorMeanLuminance = Math.Max(0.015, donor.MeanLuminance);
        double redBalance = Math.Clamp(
            (donor.MeanRed / donorMeanLuminance) / Math.Max(0.05, target.MeanRed / targetMeanLuminance),
            0.58,
            1.72);
        double greenBalance = Math.Clamp(
            (donor.MeanGreen / donorMeanLuminance) / Math.Max(0.05, target.MeanGreen / targetMeanLuminance),
            0.58,
            1.72);
        double blueBalance = Math.Clamp(
            (donor.MeanBlue / donorMeanLuminance) / Math.Max(0.05, target.MeanBlue / targetMeanLuminance),
            0.58,
            1.72);

        double projectedLuminance =
            (0.2126 * target.MeanRed * redBalance) +
            (0.7152 * target.MeanGreen * greenBalance) +
            (0.0722 * target.MeanBlue * blueBalance);
        double projectionScale = projectedLuminance <= 0.001
            ? luminanceScale
            : luminanceScale * targetMeanLuminance / projectedLuminance;
        double manualBrightness = normalized.BrightnessPercent / 100.0;
        double saturationScale = target.MeanSaturation <= 0.01
            ? 1.0
            : Math.Clamp(donor.MeanSaturation / target.MeanSaturation, 0.45, 1.65);
        saturationScale *= normalized.SaturationPercent / 100.0;
        double targetBalanceTotal = Math.Max(0.01, target.MeanRed + target.MeanGreen + target.MeanBlue);
        double donorBalanceTotal = Math.Max(0.01, donor.MeanRed + donor.MeanGreen + donor.MeanBlue);
        double balanceDistance = Math.Clamp(2.4 * Math.Sqrt(
            Math.Pow((target.MeanRed / targetBalanceTotal) - (donor.MeanRed / donorBalanceTotal), 2) +
            Math.Pow((target.MeanGreen / targetBalanceTotal) - (donor.MeanGreen / donorBalanceTotal), 2) +
            Math.Pow((target.MeanBlue / targetBalanceTotal) - (donor.MeanBlue / donorBalanceTotal), 2)), 0, 1);
        double paletteShift = Math.Max(balanceDistance, Math.Abs(target.MeanSaturation - donor.MeanSaturation));
        double strength = normalized.StrengthPercent / 100.0;
        double darkening = TerrainDarkening(target.MedianLuminance, donor.MedianLuminance);
        double shiftHarmonization = Math.Clamp((paletteShift - 0.08) * 2.0, 0, 0.82) *
            strength *
            (1.0 - darkening);
        double darkeningCompression = TerrainCompression(
            target.MedianLuminance,
            donor.MedianLuminance,
            normalized.StrengthPercent);
        int harmonizationPercent = (int)Math.Round(
            Math.Clamp(shiftHarmonization - darkeningCompression, 0, 0.82) * 100.0);

        return new NativeEnvironmentColorTransform(
            RedScale: redBalance * projectionScale * manualBrightness,
            GreenScale: greenBalance * projectionScale * manualBrightness,
            BlueScale: blueBalance * projectionScale * manualBrightness,
            SaturationScale: saturationScale,
            StrengthPercent: normalized.StrengthPercent,
            TintHex: normalized.TintHex,
            TintStrengthPercent: normalized.TintStrengthPercent,
            HarmonizationPercent: harmonizationPercent,
            TargetMedianLuminance: target.MedianLuminance,
            DonorMedianLuminance: donor.MedianLuminance,
            DonorMeanRed: donor.MeanRed,
            DonorMeanGreen: donor.MeanGreen,
            DonorMeanBlue: donor.MeanBlue);
    }

    public ColorRgba Apply(ColorRgba color)
    {
        if ((color.R == 0 && color.G == 0 && color.B == 0) || StrengthPercent <= 0)
            return color;

        double red = color.R * RedScale;
        double green = color.G * GreenScale;
        double blue = color.B * BlueScale;
        double gray = (0.2126 * red) + (0.7152 * green) + (0.0722 * blue);
        red = gray + ((red - gray) * SaturationScale);
        green = gray + ((green - gray) * SaturationScale);
        blue = gray + ((blue - gray) * SaturationScale);

        if (HarmonizationPercent > 0)
        {
            double donorLuminance = Math.Max(
                0.01,
                (0.2126 * DonorMeanRed) + (0.7152 * DonorMeanGreen) + (0.0722 * DonorMeanBlue));
            double harmonization = HarmonizationPercent / 100.0;
            red = Lerp(red, DonorMeanRed * gray / donorLuminance, harmonization);
            green = Lerp(green, DonorMeanGreen * gray / donorLuminance, harmonization);
            blue = Lerp(blue, DonorMeanBlue * gray / donorLuminance, harmonization);
        }

        if (TintStrengthPercent > 0 && ColorRgba.TryParseHex(TintHex, out ColorRgba tint))
        {
            double tintLuminance = Math.Max(1, (0.2126 * tint.R) + (0.7152 * tint.G) + (0.0722 * tint.B));
            double tintAmount = TintStrengthPercent / 100.0;
            red = Lerp(red, tint.R * gray / tintLuminance, tintAmount);
            green = Lerp(green, tint.G * gray / tintLuminance, tintAmount);
            blue = Lerp(blue, tint.B * gray / tintLuminance, tintAmount);
        }

        double strength = StrengthPercent / 100.0;
        return ColorRgba.FromArgb(
            color.A,
            (int)Math.Round(Lerp(color.R, red, strength)),
            (int)Math.Round(Lerp(color.G, green, strength)),
            (int)Math.Round(Lerp(color.B, blue, strength)));
    }

    public ColorRgba ApplyTerrainSmoothing(ColorRgba color)
    {
        ColorRgba graded = Apply(color);
        if ((color.R == 0 && color.G == 0 && color.B == 0) || StrengthPercent <= 0)
            return graded;

        double compression = TerrainCompression(TargetMedianLuminance, DonorMedianLuminance, StrengthPercent);
        if (compression <= 0.001)
            return graded;

        // This is intentionally affine. Spyro's Gouraud interpolation and HP/LP terrain
        // averages remain equivalent after an affine transform, avoiding block seams.
        return ColorRgba.FromArgb(
            graded.A,
            (int)Math.Round(Lerp(graded.R, DonorMeanRed * 255.0, compression)),
            (int)Math.Round(Lerp(graded.G, DonorMeanGreen * 255.0, compression)),
            (int)Math.Round(Lerp(graded.B, DonorMeanBlue * 255.0, compression)));
    }

    private static double TerrainCompression(
        double targetMedianLuminance,
        double donorMedianLuminance,
        int strengthPercent)
    {
        double darkening = TerrainDarkening(targetMedianLuminance, donorMedianLuminance);
        return 0.38 * darkening * (strengthPercent / 100.0);
    }

    private static double TerrainDarkening(double targetMedianLuminance, double donorMedianLuminance) =>
        Math.Clamp(
            (targetMedianLuminance - donorMedianLuminance) / Math.Max(0.08, targetMedianLuminance),
            0,
            1);

    private static double Lerp(double from, double to, double amount) => from + ((to - from) * amount);
}
