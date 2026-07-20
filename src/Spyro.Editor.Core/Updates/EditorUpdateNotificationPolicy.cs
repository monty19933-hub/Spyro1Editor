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
        int lastNotifiedBetaVersion) =>
        ShouldShow(
            new EditorBetaReleaseVersion(currentBetaVersion),
            availableBetaVersion > 0 ? new EditorBetaReleaseVersion(availableBetaVersion) : null,
            lastNotifiedBetaVersion > 0 ? new EditorBetaReleaseVersion(lastNotifiedBetaVersion) : null);

    public static bool ShouldShow(
        EditorBetaReleaseVersion currentVersion,
        EditorBetaReleaseVersion? availableVersion,
        EditorBetaReleaseVersion? lastNotifiedVersion)
    {
        ArgumentNullException.ThrowIfNull(currentVersion);
        return availableVersion != null &&
            availableVersion.CompareTo(currentVersion) > 0 &&
            (lastNotifiedVersion == null || lastNotifiedVersion.CompareTo(availableVersion) < 0);
    }
}
