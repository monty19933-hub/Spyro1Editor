namespace Spyro.Editor.Core.Universal;

/// <summary>Bounded, observed native interaction failures, separate from resource fit.</summary>
public static class UniversalRuntimeInteractionGuards
{
    public const string AlpineArtisansFailure = "This source variant failed an in-game interaction check; choose another source variant.";

    /// <summary>Only the exact Alpine row/class/destination combination proved to fail is refused.
    /// Model equivalence and a successful static compose are not interaction acceptance.</summary>
    public static string? ReasonFor(string destinationKey, string sourceKey, int sourceRow, int sourceClass) =>
        string.Equals(destinationKey, "artisans", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(sourceKey, "alpineridge", StringComparison.OrdinalIgnoreCase) && sourceRow == 1 && sourceClass == 0x10F
            ? AlpineArtisansFailure : null;
}
