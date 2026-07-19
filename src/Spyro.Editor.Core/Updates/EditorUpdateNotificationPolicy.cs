namespace Spyro.Editor.Core.Updates;

public static class EditorUpdateNotificationPolicy
{
    public static bool IsRecentCheck(
        DateTimeOffset nowUtc,
        DateTimeOffset lastCheckedUtc,
        TimeSpan maximumAge)
    {
        if (maximumAge <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(maximumAge));
        TimeSpan age = nowUtc - lastCheckedUtc;
        return age >= TimeSpan.Zero && age < maximumAge;
    }

    public static bool ShouldShow(
        int currentBetaVersion,
        int availableBetaVersion,
        int lastNotifiedBetaVersion)
    {
        if (currentBetaVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(currentBetaVersion));
        return availableBetaVersion > currentBetaVersion &&
            lastNotifiedBetaVersion < availableBetaVersion;
    }
}
