using System.Security.Cryptography;

namespace Spyro.Editor.Core.Exporting;

// One source-authenticated sector loop shared by actual fresh Disc plans.
// No filenames, sector delegates or caller hashes grant byte authority.
internal static class NativeId65FreshDiscStreaming
{
    internal static NativeId65FreshDiscVerification Measure(NativeId65FreshDiscPlan disc,
        Stream source, CancellationToken token, Stream? output = null, Action? firstSector = null) =>
        MeasureCore(disc, null, source, token, output, firstSector);

    internal static NativeId65FreshDiscVerification Measure(NativeId65FreshWizardResearchDiscPlan disc,
        Stream source, CancellationToken token, Stream? output = null, Action? firstSector = null)
    {
        token.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(disc);
        disc.RequireAncestry(disc.ExecutableAncestor);
        return MeasureCore(null, disc, source, token, output, firstSector);
    }

    // Only concrete, source-issued plans reach this shared loop. No public
    // interface or caller-provided sector callback can supply native bytes.
    private static NativeId65FreshDiscVerification MeasureCore(NativeId65FreshDiscPlan? disc,
        NativeId65FreshWizardResearchDiscPlan? wizard, Stream source, CancellationToken token,
        Stream? output, Action? firstSector)
    {
        token.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(source);
        if (!source.CanRead || !source.CanSeek || source.Position != 0 || source.Length != NativeId65FreshDiscSource.ImageByteLength)
            throw new InvalidDataException("Fresh-document expected image requires a complete readable original at offset zero.");
        if (output != null && (!output.CanWrite || !output.CanSeek || output.Position != 0 || output.Length != 0))
            throw new InvalidDataException("Fresh-document streaming requires a new empty writable owner.");
        using var originalHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var expectedHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] before = new byte[2352];
        int changed = 0;
        for (int lba = 0; lba < NativeId65FreshDiscSource.SectorCount; lba++)
        {
            token.ThrowIfCancellationRequested();
            source.ReadExactly(before);
            // Every sector, including untouched metadata/data, is authenticated
            // against the SAME private index captured with the original SHA.
            byte[] after = wizard is null ? disc!.CreateExpectedRawSector(lba, before) :
                wizard.CreateExpectedRawSector(lba, before);
            originalHash.AppendData(before);
            expectedHash.AppendData(after);
            if (!before.AsSpan().SequenceEqual(after)) changed++;
            if (output != null)
            {
                output.Write(after);
                if (lba == 0) firstSector?.Invoke();
            }
        }
        NativeId65FreshDiscSource.RequireActualEof(source, token);
        if (Convert.ToHexStringLower(originalHash.GetHashAndReset()) != NativeId65FreshDiscSource.OriginalSha256)
            throw new InvalidDataException("Fresh-document expected image lost its complete original source binding.");
        if (output != null && (output.Position != NativeId65FreshDiscSource.ImageByteLength || output.Length != output.Position))
            throw new InvalidDataException("Fresh-document streamed output length or position changed.");
        token.ThrowIfCancellationRequested();
        return new(Convert.ToHexStringLower(expectedHash.GetHashAndReset()), changed);
    }
}
