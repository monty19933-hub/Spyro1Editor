using System.Security.Cryptography;

internal sealed record FixedTailExpectedImage(long ByteLength, string SourceSha256,
    string CurrentSha256, string LegacySha256, int ChangedSectors);

// Test-only static evidence. None of these values is runtime authorization.
internal sealed record FixedTailImageProof(
    string CurrentImageSha256,
    string LegacyRepresentationSha256,
    int IndependentlyVerifiedChangedSectors,
    bool PhysicalIntegrityVerified,
    bool HistoricalLogicalAndHeaderEquivalenceVerified)
{
    internal const string CurrentStaticReadbackSha256 =
        "2d2ccc25b3e5ad301ad11366236fede8673925ebf813b8089a6cb9862c2d7261";

    internal static FixedTailImageProof Verify(string sourcePath, string outputPath, string historicalStaticSha256)
    {
        FixedTailImageProofCases.Run();
        using FileStream source = File.OpenRead(sourcePath), output = File.OpenRead(outputPath);
        return VerifyStreams(source, output, new FixedTailExpectedImage(
            661547040,
            "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37",
            CurrentStaticReadbackSha256, historicalStaticSha256, 539));
    }

    internal static FixedTailImageProof VerifyStreams(Stream source, Stream output, FixedTailExpectedImage expected)
    {
        const int sectorSize = 2352, trailerOffset = 2072;
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(expected);
        long expectedLength = expected.ByteLength;
        Require(expectedLength > 0 && expectedLength % sectorSize == 0 && expectedLength / sectorSize <= int.MaxValue &&
                source.CanRead && source.CanSeek && output.CanRead && output.CanSeek &&
                source.Position == 0 && output.Position == 0 &&
                source.Length == expectedLength && output.Length == expectedLength,
            "Current static proof changed its exact original size, layout or stream position.");
        using IncrementalHash sourceHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using IncrementalHash currentHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using IncrementalHash legacyHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] before = new byte[sectorSize], after = new byte[sectorSize];
        int changedSectors = 0;
        for (int lba = 0; lba < expectedLength / sectorSize; lba++)
        {
            source.ReadExactly(before); output.ReadExactly(after);
            sourceHash.AppendData(before);
            currentHash.AppendData(after);
            Require(before.AsSpan(0, 24).SequenceEqual(after.AsSpan(0, 24)), "Current static proof changed a sync/MSF/subheader byte.");
            if (!before.AsSpan().SequenceEqual(after))
            {
                changedSectors++;
                // Separate bitwise EDC/GF arithmetic, never the production codec.
                GapFixture.CheckPhysicalSector(after, sectorSize, lba, Require);
            }
            // Hash, but never write, the exact representation emitted by the
            // historical user-byte-only writer. A match binds every logical and
            // header byte to that fixed historical image, not merely one face.
            before.AsSpan(trailerOffset).CopyTo(after.AsSpan(trailerOffset));
            legacyHash.AppendData(after);
        }
        Require(source.Length == expectedLength && output.Length == expectedLength &&
                source.Position == expectedLength && output.Position == expectedLength,
            "Source/output length or position changed during complete fixed-tail readback.");
        Require(Convert.ToHexString(sourceHash.GetHashAndReset()).Equals(expected.SourceSha256, StringComparison.OrdinalIgnoreCase),
            "The complete original source image differs from its frozen identity.");
        string current = Convert.ToHexString(currentHash.GetHashAndReset());
        string legacy = Convert.ToHexString(legacyHash.GetHashAndReset());
        Require(current.Equals(expected.CurrentSha256, StringComparison.OrdinalIgnoreCase), "The current parity-correct static image differs from its test-only pin.");
        Require(legacy.Equals(expected.LegacySha256, StringComparison.OrdinalIgnoreCase), "Current logical/header bytes no longer reproduce the exact historical static image under its old trailer representation.");
        Require(changedSectors == expected.ChangedSectors, "The identified Artisans static recipe changed its physical write footprint.");
        return new(current, legacy, changedSectors, true, true);
    }

    internal static bool TryRunReadOnlyCommand(string[] args)
    {
        if (args is ["--source-free-image-proof"])
        {
            FixedTailImageProofCases.Run();
            Console.WriteLine("FIXED_TAIL_IMAGE_PROOF_CASES_OK");
            return true;
        }
        if (args is ["--verify-retained", string source, string output])
        {
            FixedTailImageProof proof = Verify(source, output,
                Spyro.Editor.Core.Exporting.AppendedPrivateTerrainTexturePromotionProfileRegistry.ArtisansFixedTailStaticProofOutputSha256);
            Console.WriteLine($"FIXED_TAIL_RETAINED_IMAGE_PROOF_OK rawSectors={proof.IndependentlyVerifiedChangedSectors} currentSha256={proof.CurrentImageSha256} legacySha256={proof.LegacyRepresentationSha256} RuntimeVerified=false");
            return true;
        }
        return false;
    }

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidDataException(message); }
}
