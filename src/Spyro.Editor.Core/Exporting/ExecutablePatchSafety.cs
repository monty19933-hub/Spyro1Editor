namespace Spyro.Editor.Core.Exporting;

internal static class ExecutablePatchSafety
{
    // Retail PsyQ interrupt-system state begins here. The surrounding range is
    // referenced by startup code and contains interrupt state/tables, despite a
    // long zero preimage on disc. Writing instructions here caused two matching
    // pre-logo 0-FPS failures, including Artisans Key v2.
    private const uint PsyQInterruptStateStart = 0x80073924;
    private const uint PsyQInterruptStateEndExclusive = 0x800749AC;

    public static void GuardPatchRange(uint runtimeAddress, int byteLength, string label)
    {
        if (byteLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(byteLength), "Executable patch length must be positive.");

        ulong start = runtimeAddress;
        ulong end = checked(start + (uint)byteLength);
        bool overlapsInterruptState =
            start < PsyQInterruptStateEndExclusive &&
            end > PsyQInterruptStateStart;
        if (!overlapsInterruptState)
            return;

        throw new InvalidDataException(
            $"{label} overlaps live PsyQ interrupt-system state " +
            $"0x{PsyQInterruptStateStart:X8}-0x{PsyQInterruptStateEndExclusive - 1:X8}. " +
            "A zero executable preimage is not proof of a safe code cave; this range is permanently blocked.");
    }
}
