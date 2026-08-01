using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Exporting;

public sealed class NativeSkyFlightDonorException : InvalidOperationException
{
    public NativeSkyFlightDonorException(LevelDefinition target, Spyro1LevelSkyBlockLayout donor)
        : base(
            $"{donor.DisplayName} is a flight-stage sky. Flight skies are authored around flight-stage camera bounds and can visibly pop when used in non-flight destination {target.DisplayName}. " +
            "Choose a non-flight sky donor or use a palette-only edit. No BIN, CUE, or partial sky patch was written.")
    {
        TargetLevelKey = target.Key;
        TargetLevelName = target.DisplayName;
        DonorLevelKey = donor.Key;
        DonorLevelName = donor.DisplayName;
    }

    public string TargetLevelKey { get; }
    public string TargetLevelName { get; }
    public string DonorLevelKey { get; }
    public string DonorLevelName { get; }
}

public static class NativeSkyGeometrySafety
{
    private static readonly HashSet<string> FlightLevelKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "sunnyflight",
        "nightflight",
        "crystalflight",
        "wildflight",
        "icyflight"
    };

    public static bool IsFlight(string levelKey) =>
        FlightLevelKeys.Contains(LevelCatalog.NormalizeKey(levelKey));

    public static void ThrowIfFlightDonorTargetsNonFlight(
        LevelDefinition target,
        Spyro1LevelSkyBlockLayout donor,
        bool allowResearchOnly)
    {
        if (!allowResearchOnly && !IsFlight(target.Key) && IsFlight(donor.Key))
            throw new NativeSkyFlightDonorException(target, donor);
    }
}
