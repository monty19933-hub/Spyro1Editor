using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Persistence;

namespace Spyro.Editor.Core.Exporting;

/// <summary>A detached audit leaf of the complete authored build, not independent write authority.</summary>
internal sealed class Id65AuthoringMusicDiscWrite
{
    private readonly byte[] _before;
    private readonly byte[] _after;
    internal Id65AuthoringMusicDiscWrite(string id, NativeMappedExtent address, byte[] before, byte[] after)
    {
        if (before.Length == 0 || before.Length != after.Length || before.LongLength != address.Final.ByteLength)
            throw new InvalidDataException("An authored disc leaf must have one exact fixed-size preimage and afterimage.");
        Id = id; Address = address; _before = before.ToArray(); _after = after.ToArray();
    }
    public string Id { get; }
    public NativeMappedExtent Address { get; }
    public byte[] CopyBefore() => _before.ToArray();
    public byte[] CopyAfter() => _after.ToArray();
    internal ReadOnlySpan<byte> Before => _before;
    internal ReadOnlySpan<byte> After => _after;
}

/// <summary>
/// Pure composition of the unchanged schema-6 WAD80 plan and the certified
/// built-in music compiler. The two native file domains map into the same
/// logical disc domain and must be disjoint. No file is read or written here;
/// this plan does not confer publication or runtime authority.
/// </summary>
internal sealed class Id65AuthoringMusicDiscPlan
{
    public const string ProfileId = "id65-schema6-built-in-music-disc-plan-v1";
    public const int RawSectorByteLength = 2352;
    public const int LogicalSectorByteLength = 2048;
    public const long SourceImageByteLength = 661_547_040;
    public const int ExecutableLba = 55_382;
    public const int ExecutableByteLength = 0x66000;
    private readonly byte[] _sourceRow80;
    private readonly byte[] _outputRow80;
    private readonly byte[] _sourceExecutable;
    private readonly byte[] _outputExecutable;
    private readonly ReadOnlyCollection<Id65AuthoringMusicDiscWrite> _writes;
    private readonly ReadOnlyCollection<int> _affectedRawSectors;
    private readonly HashSet<int> _affectedSectorSet;
    private readonly Id65MusicSelectionPlan _music;

    private Id65AuthoringMusicDiscPlan(
        Id65AuthoringMusicBuildInput input,
        Id65AuthoringBlankCycloramaAggregatePlan baseline,
        Id65MusicSelectionPlan music,
        byte[] sourceRow80,
        byte[] sourceExecutable)
    {
        Input = input; BaselinePlan = baseline; _music = music;
        _sourceRow80 = sourceRow80.ToArray(); _sourceExecutable = sourceExecutable.ToArray();
        _outputRow80 = Id65AuthoringBlankCycloramaAggregatePlanProjector.ApplyExact(baseline, _sourceRow80, reverse: false);
        _outputExecutable = music.CopyOutputExecutable();
        DiscSpace = new(NativeAddressKind.DiscLogical, SourceImageSha256, NativeAddressRevision.Final,
            SourceImageByteLength / RawSectorByteLength * LogicalSectorByteLength);
        NativeAddressSpace wad = new(NativeAddressKind.WadFile, SourceImageSha256 + ":WAD.WAD", NativeAddressRevision.Original, 0x6C18800);
        NativeAddressSpace executable = new(NativeAddressKind.ExecutableFile, music.SourceExecutableSha256, NativeAddressRevision.Original, ExecutableByteLength);
        const long rowOffset = Id65AuthoringBlankCycloramaAggregatePlanProjector.TargetDataWadOffset;
        Wad80DiscExtent = new(DiscSpace, 37L * LogicalSectorByteLength + rowOffset, _sourceRow80.Length);
        ExecutableDiscExtent = new(DiscSpace, (long)ExecutableLba * LogicalSectorByteLength, ExecutableByteLength);
        NativeRelocationMap.RequireDisjoint([Wad80DiscExtent, ExecutableDiscExtent]);
        NativeRelocationMap wadMap = new(wad, DiscSpace,
            [new("schema6-wad80", new(wad, rowOffset, _sourceRow80.Length), Wad80DiscExtent, LogicalSectorByteLength)]);
        NativeRelocationMap executableMap = new(executable, DiscSpace,
            [new("certified-relocated-executable", new(executable, 0, ExecutableByteLength), ExecutableDiscExtent, LogicalSectorByteLength)]);
        List<Id65AuthoringMusicDiscWrite> writes = [];
        foreach (Id65AuthoringBlankCycloramaAggregateOwnedRange range in baseline.OwnedRanges)
        {
            byte[] before = range.CopyBefore(), after = range.CopyAfter();
            writes.Add(new("wad80:" + range.StableId,
                wadMap.Map(new(wad, range.WadOffset, before.Length)), before, after));
        }
        foreach (Id65MusicFilePatch patch in music.Patches)
            writes.Add(new("executable:" + patch.Name,
                executableMap.Map(new(executable, patch.FileOffset, patch.ByteLength)),
                Convert.FromHexString(patch.BeforeHex), Convert.FromHexString(patch.AfterHex)));
        NativeRelocationMap.RequireDisjoint(writes.Select(write => write.Address.Final));
        if (writes.Select(write => write.Id).Distinct(StringComparer.Ordinal).Count() != writes.Count)
            throw new InvalidDataException("Authored disc leaves have duplicate identities.");
        _writes = Array.AsReadOnly(writes.OrderBy(write => write.Address.Final.Offset).ToArray());
        _affectedSectorSet = [];
        foreach (Id65AuthoringMusicDiscWrite write in _writes)
        {
            int first = checked((int)(write.Address.Final.Offset / LogicalSectorByteLength));
            int last = checked((int)((write.Address.Final.EndExclusive - 1) / LogicalSectorByteLength));
            for (int sector = first; sector <= last; sector++) _affectedSectorSet.Add(sector);
        }
        _affectedRawSectors = Array.AsReadOnly(_affectedSectorSet.Order().ToArray());
        RequireLogicalReadback(_outputRow80, _outputExecutable);
        DeterministicPlanSha256 = Hash(JsonSerializer.SerializeToUtf8Bytes(new
        {
            profileId = ProfileId,
            buildInputSha256 = input.CanonicalSha256,
            baselineAggregatePlanSha256 = baseline.DeterministicAggregatePlanSha256,
            sourceImageSha256 = SourceImageSha256,
            sourceExecutableSha256 = music.SourceExecutableSha256,
            outputExecutableSha256 = music.OutputExecutableSha256,
            outputRow80Sha256 = baseline.OutputRow80Sha256,
            writes = _writes.Select(write => new
            {
                id = write.Id, domain = write.Address.Original.Space.Kind.ToString(),
                fileOffset = write.Address.Original.Offset, logicalDiscOffset = write.Address.Final.Offset,
                byteLength = write.Address.Final.ByteLength, beforeSha256 = Hash(write.Before), afterSha256 = Hash(write.After)
            })
        }));
    }

    public Id65AuthoringMusicBuildInput Input { get; }
    public Id65AuthoringBlankCycloramaAggregatePlan BaselinePlan { get; }
    public NativeAddressSpace DiscSpace { get; }
    public NativeByteExtent Wad80DiscExtent { get; }
    public NativeByteExtent ExecutableDiscExtent { get; }
    public IReadOnlyList<Id65AuthoringMusicDiscWrite> Writes => _writes;
    public IReadOnlyList<int> AffectedRawSectors => _affectedRawSectors;
    public string SourceImageSha256 => BaselinePlan.Destination.SourceImageSha256;
    public string SourceExecutableSha256 => _music.SourceExecutableSha256;
    public string OutputExecutableSha256 => _music.OutputExecutableSha256;
    public string OutputRow80Sha256 => BaselinePlan.OutputRow80Sha256;
    public string DeterministicPlanSha256 { get; }
    public byte[] CopyOutputRow80() => _outputRow80.ToArray();
    public byte[] CopyOutputExecutable() => _outputExecutable.ToArray();

    public static Id65AuthoringMusicDiscPlan Compile(
        Id65AuthoringMusicBuildInput input,
        Id65AuthoringBlankCycloramaAggregatePlanRequest baselineRequest)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(baselineRequest);
        ArgumentNullException.ThrowIfNull(baselineRequest.StoredDocument);
        input = Id65AuthoringMusicBuildInput.Parse(input.SerializeUtf8());
        input.RequireExactBaseline(baselineRequest.StoredDocument.Document, baselineRequest.StoredDocument.EnvelopeCanonicalSha256);
        if (baselineRequest.ExpectedEnvelopeCanonicalSha256 != input.BaselineEnvelopeCanonicalSha256)
            throw new InvalidDataException("The music aggregate expected envelope differs from its authored build input.");
        Id65AuthoringBlankCycloramaAggregatePlan baseline = Id65AuthoringBlankCycloramaAggregatePlanProjector.Project(baselineRequest);
        byte[] sourceExecutable = baselineRequest.NativeInputs.LockedSource.CopyGlobalExecutable();
        Id65MusicSourceCertificate certificate = Id65MusicSourceCertificate.FromFrozenExecutable(sourceExecutable);
        Id65MusicSelectionPlan music = Id65BuiltInMusicSelectionCompiler.Compile(certificate, sourceExecutable, input.SelectedTrackId);
        return new(input, baseline, music, baselineRequest.NativeInputs.LockedSource.CopyRow80(), sourceExecutable);
    }

    public void RequireLogicalReadback(ReadOnlySpan<byte> outputRow80, ReadOnlySpan<byte> outputExecutable)
    {
        if (!outputRow80.SequenceEqual(_outputRow80) || !outputExecutable.SequenceEqual(_outputExecutable))
            throw new InvalidDataException("The combined music build does not match both exact logical file afterimages.");
        byte[] restored = Id65AuthoringBlankCycloramaAggregatePlanProjector.ApplyExact(BaselinePlan, outputRow80.ToArray(), reverse: true);
        if (!restored.AsSpan().SequenceEqual(_sourceRow80))
            throw new InvalidDataException("The combined build lost the WAD80 baseline inverse or protected complement.");
        byte[] inverseExecutable = outputExecutable.ToArray();
        foreach (Id65MusicFilePatch inverse in _music.InversePatches)
        {
            byte[] before = Convert.FromHexString(inverse.BeforeHex), after = Convert.FromHexString(inverse.AfterHex);
            if (!inverseExecutable.AsSpan(inverse.FileOffset, inverse.ByteLength).SequenceEqual(before))
                throw new InvalidDataException("The combined music executable inverse has a stale preimage.");
            after.CopyTo(inverseExecutable, inverse.FileOffset);
        }
        if (!inverseExecutable.AsSpan().SequenceEqual(_sourceExecutable))
            throw new InvalidDataException("The combined build lost the entire executable inverse or protected complement.");
    }

    /// <summary>
    /// Computes one exact full raw-sector afterimage from its frozen-source
    /// preimage. Prefix, subheader, unowned user bytes and EDC/ECC are all checked.
    /// A caller cannot use this helper for an unowned sector.
    /// </summary>
    public byte[] CreateExpectedRawSector(int lba, ReadOnlySpan<byte> sourceRawSector)
    {
        if (!_affectedSectorSet.Contains(lba) || sourceRawSector.Length != RawSectorByteLength)
            throw new InvalidDataException("The requested raw sector is outside the authored music build's complete owned sector set.");
        RawMode2Form1SectorIntegrity.RequireRawSectorIntegrity(sourceRawSector, lba);
        long logicalStart = (long)lba * LogicalSectorByteLength;
        NativeByteExtent owner = logicalStart >= ExecutableDiscExtent.Offset ? ExecutableDiscExtent : Wad80DiscExtent;
        byte[] sourceFile = ReferenceEquals(owner, ExecutableDiscExtent) ? _sourceExecutable : _sourceRow80;
        int sourceOffset = checked((int)(logicalStart - owner.Offset));
        if (sourceOffset < 0 || sourceOffset > sourceFile.Length - LogicalSectorByteLength ||
            !sourceRawSector.Slice(24, LogicalSectorByteLength).SequenceEqual(sourceFile.AsSpan(sourceOffset, LogicalSectorByteLength)))
            throw new InvalidDataException("The raw-sector payload does not match the certified complete native file preimage.");
        byte[] expected = sourceRawSector.ToArray();
        foreach (Id65AuthoringMusicDiscWrite write in _writes)
        {
            long first = Math.Max(logicalStart, write.Address.Final.Offset);
            long end = Math.Min(logicalStart + LogicalSectorByteLength, write.Address.Final.EndExclusive);
            if (first >= end) continue;
            int writeOffset = checked((int)(first - write.Address.Final.Offset));
            int sectorOffset = checked((int)(first - logicalStart)) + 24;
            int length = checked((int)(end - first));
            if (!sourceRawSector.Slice(sectorOffset, length).SequenceEqual(write.Before.Slice(writeOffset, length)))
                throw new InvalidDataException("A mapped disc leaf has a stale exact preimage.");
            write.After.Slice(writeOffset, length).CopyTo(expected.AsSpan(sectorOffset, length));
        }
        return RawMode2Form1SectorIntegrity.RebuildRawSectorCopy(expected, lba);
    }

    internal static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}

internal sealed record Id65AuthoringMusicPhysicalReadback(
    string SourceImageSha256, string OutputImageSha256, string PlanSha256,
    int VerifiedOwnedRawSectorCount, int ChangedRawSectorCount, long ChangedPhysicalByteCount);

/// <summary>
/// Read-only whole-disc verification. Every unowned byte must remain exactly
/// equal to the frozen source; every owned raw sector must equal the pure
/// plan's independently reconstructed afterimage. Does not publish an image.
/// </summary>
internal static class Id65AuthoringMusicDiscReadback
{
    public static Id65AuthoringMusicPhysicalReadback Verify(
        Id65AuthoringMusicDiscPlan plan, Stream lockedSource, Stream output, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(lockedSource);
        ArgumentNullException.ThrowIfNull(output);
        if (!lockedSource.CanRead || !lockedSource.CanSeek || !output.CanRead || !output.CanSeek ||
            lockedSource.Length != Id65AuthoringMusicDiscPlan.SourceImageByteLength || output.Length != lockedSource.Length)
            throw new InvalidDataException("Music physical readback requires both exact complete seekable disc images.");
        lockedSource.Position = 0; output.Position = 0;
        const int rawSize = Id65AuthoringMusicDiscPlan.RawSectorByteLength;
        byte[] before = new byte[rawSize * 256], after = new byte[rawSize * 256];
        HashSet<int> owned = plan.AffectedRawSectors.ToHashSet();
        using IncrementalHash sourceHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using IncrementalHash outputHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        int sector = 0, verified = 0, changedSectors = 0;
        long changedBytes = 0;
        while (lockedSource.Position < lockedSource.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int count = (int)Math.Min(before.Length, lockedSource.Length - lockedSource.Position);
            lockedSource.ReadExactly(before.AsSpan(0, count)); output.ReadExactly(after.AsSpan(0, count));
            sourceHash.AppendData(before, 0, count); outputHash.AppendData(after, 0, count);
            for (int offset = 0; offset < count; offset += rawSize, sector++)
            {
                ReadOnlySpan<byte> sourceRaw = before.AsSpan(offset, rawSize), outputRaw = after.AsSpan(offset, rawSize);
                if (owned.Contains(sector))
                {
                    if (!outputRaw.SequenceEqual(plan.CreateExpectedRawSector(sector, sourceRaw)))
                        throw new InvalidDataException($"Owned music-build raw sector {sector} has a wrong afterimage, prefix, subheader or EDC/ECC.");
                    verified++;
                }
                else if (!sourceRaw.SequenceEqual(outputRaw))
                    throw new InvalidDataException($"The music build changed protected physical complement sector {sector}.");
                if (!sourceRaw.SequenceEqual(outputRaw))
                {
                    changedSectors++;
                    for (int index = 0; index < rawSize; index++) if (sourceRaw[index] != outputRaw[index]) changedBytes++;
                }
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (lockedSource.Length != Id65AuthoringMusicDiscPlan.SourceImageByteLength ||
            output.Length != Id65AuthoringMusicDiscPlan.SourceImageByteLength ||
            lockedSource.Position != Id65AuthoringMusicDiscPlan.SourceImageByteLength ||
            output.Position != Id65AuthoringMusicDiscPlan.SourceImageByteLength)
            throw new InvalidDataException("A music readback image changed length or lost its complete read boundary during verification.");
        string sourceSha256 = Convert.ToHexStringLower(sourceHash.GetHashAndReset());
        if (sourceSha256 != plan.SourceImageSha256 || verified != owned.Count)
            throw new InvalidDataException("The music readback source is not the complete frozen disc or its ownership coverage is incomplete.");
        return new(sourceSha256, Convert.ToHexStringLower(outputHash.GetHashAndReset()), plan.DeterministicPlanSha256,
            verified, changedSectors, changedBytes);
    }
}
