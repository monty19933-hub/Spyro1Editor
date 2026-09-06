using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Persistence;

namespace Spyro.Editor.Core.Exporting;

internal sealed record NativeExactByteStage(string Id, byte[] Before, byte[] After,
    IReadOnlyList<Id65AuthoringCompositeOwnedRange> Ranges);

/// <summary>
/// Shared fixed-size byte composition. Each independent producer must supply
/// a complete stage preimage/afterimage and exact owned windows. Ownership is
/// validated before differences are folded; a final diff never grants it.
/// This leaf neither discovers native ownership nor relocates or writes files.
/// </summary>
internal sealed class NativeExactByteComposition
{
    private readonly byte[] _source, _output;
    private NativeExactByteComposition(byte[] source, byte[] output,
        IReadOnlyList<Id65AuthoringCompositeOwnedRange> ranges, int stageCount, string stageEvidence)
    {
        _source = source; _output = output;
        OwnedRanges = Array.AsReadOnly(ranges.Select(r => r.DeepCopy()).ToArray());
        StageCount = stageCount;
        DeterministicSha256 = Hash(Encoding.UTF8.GetBytes("native-exact-byte-composition-v1\n" + stageEvidence +
            Hash(source) + "\n" + Hash(output)));
    }
    public IReadOnlyList<Id65AuthoringCompositeOwnedRange> OwnedRanges { get; }
    public string DeterministicSha256 { get; }
    public int StageCount { get; }
    public byte[] CopyOutput() => _output.ToArray();
    public byte[] ApplyExact(ReadOnlySpan<byte> input, bool reverse = false)
    {
        byte[] expected = reverse ? _output : _source, final = reverse ? _source : _output;
        if (!input.SequenceEqual(expected)) throw new InvalidDataException("Exact byte composition lost its complete input identity.");
        byte[] result = input.ToArray();
        foreach (var range in OwnedRanges)
        {
            byte[] before = reverse ? range.CopyAfter() : range.CopyBefore();
            byte[] after = reverse ? range.CopyBefore() : range.CopyAfter();
            if (!result.AsSpan(range.DataRelativeOffset, before.Length).SequenceEqual(before))
                throw new InvalidDataException("Exact byte composition lost an owned preimage.");
            after.CopyTo(result, range.DataRelativeOffset);
        }
        if (!result.AsSpan().SequenceEqual(final)) throw new InvalidDataException("Exact byte composition lost its inverse or protected complement.");
        return result;
    }
    public static NativeExactByteComposition Compile(byte[] original, IEnumerable<NativeExactByteStage> stages)
    {
        ArgumentNullException.ThrowIfNull(original); ArgumentNullException.ThrowIfNull(stages);
        if (original.Length == 0) throw new InvalidDataException("Byte composition needs a nonempty bounded original.");
        byte[] source = original.ToArray(), current = source.ToArray();
        bool[] ownership = new bool[source.Length];
        HashSet<string> stageIds = new(StringComparer.Ordinal);
        StringBuilder evidence = new();
        foreach (NativeExactByteStage stage in stages)
        {
            if (stage is null || string.IsNullOrWhiteSpace(stage.Id) || !stageIds.Add(stage.Id) ||
                stage.Before is null || stage.After is null || stage.Ranges is null)
                throw new InvalidDataException("Byte composition requires unique complete stage identities.");
            byte[] before = stage.Before.ToArray(), after = stage.After.ToArray();
            if (before.Length != source.Length || after.Length != source.Length || !current.AsSpan().SequenceEqual(before))
                throw new InvalidDataException("Byte composition rejected a stale, reordered or resizing stage.");
            var ranges = stage.Ranges.Select(r => r?.DeepCopy() ?? throw new InvalidDataException("A stage range is missing."))
                .OrderBy(r => r.DataRelativeOffset).ToArray();
            if (ranges.Select(r => r.StableId).Distinct(StringComparer.Ordinal).Count() != ranges.Length)
                throw new InvalidDataException("A byte stage has duplicate range identities.");
            int cursor = 0;
            foreach (var range in ranges)
            {
                byte[] preimage = range.CopyBefore(), afterimage = range.CopyAfter();
                if (string.IsNullOrWhiteSpace(range.StableId) || string.IsNullOrWhiteSpace(range.OwnerId) ||
                    range.DataRelativeOffset < cursor || preimage.Length == 0 || preimage.Length != afterimage.Length ||
                    range.DataRelativeOffset > source.Length - preimage.Length ||
                    !before.AsSpan(range.DataRelativeOffset, preimage.Length).SequenceEqual(preimage) ||
                    !after.AsSpan(range.DataRelativeOffset, afterimage.Length).SequenceEqual(afterimage))
                    throw new InvalidDataException("A byte stage range lost ownership bounds, exact preimage, afterimage or nonoverlap.");
                afterimage.CopyTo(current, range.DataRelativeOffset);
                Array.Fill(ownership, true, range.DataRelativeOffset, preimage.Length);
                cursor = checked(range.DataRelativeOffset + preimage.Length);
                evidence.Append(stage.Id).Append('|').Append(range.StableId).Append('|').Append(range.OwnerId)
                    .Append('|').Append(range.DataRelativeOffset).Append('|').Append(preimage.Length)
                    .Append('|').Append(Hash(preimage)).Append('|').Append(Hash(afterimage)).Append('\n');
            }
            if (!current.AsSpan().SequenceEqual(after))
                throw new InvalidDataException("A byte stage changed undeclared protected bytes.");
            byte[] inverse = after.ToArray();
            foreach (var range in ranges.Reverse()) range.CopyBefore().CopyTo(inverse, range.DataRelativeOffset);
            if (!inverse.AsSpan().SequenceEqual(before)) throw new InvalidDataException("A byte stage lost its complete inverse.");
            evidence.Append(stage.Id).Append('|').Append(Hash(before)).Append('|').Append(Hash(after)).Append('\n');
        }
        if (stageIds.Count == 0) throw new InvalidDataException("Byte composition needs at least one validated stage.");
        List<Id65AuthoringCompositeOwnedRange> direct = [];
        for (int index = 0; index < source.Length;)
        {
            if (source[index] == current[index]) { index++; continue; }
            int start = index;
            while (index < source.Length && source[index] != current[index])
            {
                if (!ownership[index]) throw new InvalidDataException("A composed output byte has no validated stage owner.");
                index++;
            }
            direct.Add(new($"composed.{direct.Count:D5}", "validated-sequential-stage-union", start,
                source.AsSpan(start, index - start).ToArray(), current.AsSpan(start, index - start).ToArray()));
        }
        NativeExactByteComposition result = new(source, current, direct, stageIds.Count, evidence.ToString());
        _ = result.ApplyExact(source); _ = result.ApplyExact(current, reverse: true);
        return result;
    }
    internal static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}

internal sealed record Id65AuthoredCollisionMusicWrite(string Id, NativeMappedExtent Address, byte[] Before, byte[] After);
internal sealed record Id65AuthoredCollisionMusicReadback(string SourceImageSha256, string OutputImageSha256,
    string PlanSha256, int OwnedRawSectors, int ChangedRawSectors, long ChangedPhysicalBytes);

/// <summary>New fixed-capacity profile. Old document and candidate formats are unchanged.</summary>
internal sealed class Id65AuthoredCollisionMusicPlan
{
    public const string ProfileId = "id65-authored-collision-music-source-bound-plan-v1";
    public const long ImageBytes = Id65AuthoringMusicDiscPlan.SourceImageByteLength;
    public const int RawBytes = 2352, LogicalBytes = 2048;
    private readonly byte[] _sourceRow, _outputRow, _sourceExe, _outputExe;
    private readonly NativeExactByteComposition _rowComposition;
    private readonly Id65MusicSelectionPlan _music;
    private readonly IReadOnlyList<Id65AuthoredCollisionMusicWrite> _writes;
    private readonly HashSet<int> _ownedSectors;

    private Id65AuthoredCollisionMusicPlan(Id65AuthoredCollisionMusicInput input,
        Id65AuthoringBlankCycloramaAggregatePlan baseline, string nativeInputSha,
        NativeExactByteComposition rowComposition, Id65CompiledAuthoringCollisionIsolation collision,
        Id65MusicSelectionPlan music, byte[] sourceRow, byte[] sourceExe)
    {
        Input = input; BaselineAggregatePlanSha256 = baseline.DeterministicAggregatePlanSha256;
        SourceImageSha256 = baseline.Destination.SourceImageSha256; NativeInputSetSha256 = nativeInputSha;
        _rowComposition = rowComposition; _music = music; Collision = collision.Readback;
        _sourceRow = sourceRow.ToArray(); _outputRow = rowComposition.CopyOutput();
        _sourceExe = sourceExe.ToArray(); _outputExe = music.CopyOutputExecutable();
        if (!collision.CopyOutputRow80().AsSpan().SequenceEqual(_outputRow))
            throw new InvalidDataException("The direct composition differs from the exact collision compiler output.");
        CollisionPlanSha256 = collision.DeterministicPlanSha256;
        DiscSpace = new(NativeAddressKind.DiscLogical, SourceImageSha256, NativeAddressRevision.Final,
            ImageBytes / RawBytes * LogicalBytes);
        NativeAddressSpace wad = new(NativeAddressKind.WadFile, SourceImageSha256 + ":WAD.WAD", NativeAddressRevision.Original, 0x6C18800);
        NativeAddressSpace exe = new(NativeAddressKind.ExecutableFile, music.SourceExecutableSha256, NativeAddressRevision.Original, _sourceExe.Length);
        long rowOffset = Id65AuthoringBlankCycloramaCompiler.TargetDataWadOffset;
        RowDiscExtent = new(DiscSpace, 37L * LogicalBytes + rowOffset, _sourceRow.Length);
        ExecutableDiscExtent = new(DiscSpace, (long)Id65AuthoringMusicDiscPlan.ExecutableLba * LogicalBytes, _sourceExe.Length);
        NativeRelocationMap.RequireDisjoint([RowDiscExtent, ExecutableDiscExtent]);
        NativeRelocationMap rowMap = new(wad, DiscSpace, [new("authored-wad80", new(wad, rowOffset, _sourceRow.Length), RowDiscExtent, LogicalBytes)]);
        NativeRelocationMap exeMap = new(exe, DiscSpace, [new("music-executable", new(exe, 0, _sourceExe.Length), ExecutableDiscExtent, LogicalBytes)]);
        List<Id65AuthoredCollisionMusicWrite> writes = [];
        foreach (var range in rowComposition.OwnedRanges)
            writes.Add(new("wad:" + range.StableId, rowMap.Map(new(wad, rowOffset + range.DataRelativeOffset, range.ByteLength)),
                range.CopyBefore(), range.CopyAfter()));
        foreach (var patch in music.Patches)
            writes.Add(new("exe:" + patch.Name, exeMap.Map(new(exe, patch.FileOffset, patch.ByteLength)),
                Convert.FromHexString(patch.BeforeHex), Convert.FromHexString(patch.AfterHex)));
        NativeRelocationMap.RequireDisjoint(writes.Select(w => w.Address.Final));
        _writes = Array.AsReadOnly(writes.OrderBy(w => w.Address.Final.Offset).ToArray());
        _ownedSectors = writes.SelectMany(w =>
        {
            int first = checked((int)(w.Address.Final.Offset / LogicalBytes));
            int last = checked((int)((w.Address.Final.EndExclusive - 1) / LogicalBytes));
            return Enumerable.Range(first, last - first + 1);
        }).ToHashSet();
        AffectedRawSectors = Array.AsReadOnly(_ownedSectors.Order().ToArray());
        DeterministicPlanSha256 = Hash(JsonSerializer.SerializeToUtf8Bytes(new
        {
            profile = ProfileId, input = Input.CanonicalSha256, source = SourceImageSha256,
            native = NativeInputSetSha256, baseline = BaselineAggregatePlanSha256,
            composed = rowComposition.DeterministicSha256, collision = CollisionPlanSha256,
            sourceRow = Hash(_sourceRow), outputRow = Hash(_outputRow),
            sourceExe = Hash(_sourceExe), outputExe = Hash(_outputExe),
            writes = _writes.Select(w => new { w.Id, domain = w.Address.Original.Space.Kind.ToString(),
                original = w.Address.Original.Offset, final = w.Address.Final.Offset,
                length = w.Before.Length, before = Hash(w.Before), after = Hash(w.After) })
        }));
        RequireLogicalReadback(_outputRow, _outputExe);
    }
    public Id65AuthoredCollisionMusicInput Input { get; }
    public string SourceImageSha256 { get; }
    public string NativeInputSetSha256 { get; }
    public string BaselineAggregatePlanSha256 { get; }
    public string CollisionPlanSha256 { get; }
    public string DeterministicPlanSha256 { get; }
    public Id65CollisionIsolationReadback Collision { get; }
    public NativeAddressSpace DiscSpace { get; }
    public NativeByteExtent RowDiscExtent { get; }
    public NativeByteExtent ExecutableDiscExtent { get; }
    public IReadOnlyList<int> AffectedRawSectors { get; }
    public string OutputRow80Sha256 => Hash(_outputRow);
    public string OutputExecutableSha256 => Hash(_outputExe);
    public string SourceExecutableSha256 => Hash(_sourceExe);
    public byte[] CopyOutputRow80() => _outputRow.ToArray();
    public byte[] CopyOutputExecutable() => _outputExe.ToArray();

    public static Id65AuthoredCollisionMusicPlan Compile(Id65AuthoredCollisionMusicInput input,
        Id65AuthoringBlankCycloramaAggregatePlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(input); ArgumentNullException.ThrowIfNull(request);
        input = Id65AuthoredCollisionMusicInput.Parse(input.SerializeUtf8());
        input.Music.RequireExactBaseline(request.StoredDocument.Document, request.StoredDocument.EnvelopeCanonicalSha256);
        if (request.ExpectedEnvelopeCanonicalSha256 != input.Music.BaselineEnvelopeCanonicalSha256)
            throw new InvalidDataException("The composition input is bound to a different stored-document envelope.");
        // This established projector validates exact store, manifest and native
        // source generation before any new native stage is admitted.
        var baseline = Id65AuthoringBlankCycloramaAggregatePlanProjector.Project(request);
        var native = request.NativeInputs;
        Id65AuthoringSingleSectorSubstrateCompileRequest nativeRequest = new(input.Music.BaselineDocument,
            native.LockedSource, request.V3Template, native.PrivateTextureWitness, native.GrassBundle);
        var single = Id65AuthoringSingleSectorSubstrateCompiler.Compile(nativeRequest);
        List<NativeExactByteStage> stages = [new(single.ProfileId, native.LockedSource.CopyRow80(), single.CopyOutputRow80(), single.OwnedRanges)];
        if (input.Substrate == Id65AuthoredCollisionMusicSubstrate.ExactCollapsedSky)
        {
            var collapsed = Id65AuthoringCollapsedCycloramaGeometryProbeCompiler.CompileFromSingleSector(single);
            stages.Add(new(collapsed.ProfileId, single.CopyOutputRow80(), collapsed.CopyOutputRow80(), collapsed.OwnedRanges));
        }
        var collision = Id65AuthoringCollisionIsolationCompiler.Compile(nativeRequest,
            collapsedCyclorama: input.Substrate == Id65AuthoredCollisionMusicSubstrate.ExactCollapsedSky);
        stages.Add(new(Id65AuthoringCollisionIsolationCompiler.ProfileId, collision.CopyBaselineRow80(),
            collision.CopyOutputRow80(), collision.OwnedRanges));
        NativeExactByteComposition composed = NativeExactByteComposition.Compile(native.LockedSource.CopyRow80(), stages);
        byte[] exe = native.LockedSource.CopyGlobalExecutable();
        var music = Id65BuiltInMusicSelectionCompiler.Compile(Id65MusicSourceCertificate.FromFrozenExecutable(exe), exe, input.Music.SelectedTrackId);
        return new(input, baseline, native.CanonicalInputSetSha256, composed, collision, music, native.LockedSource.CopyRow80(), exe);
    }
    public void RequireLogicalReadback(ReadOnlySpan<byte> row, ReadOnlySpan<byte> exe)
    {
        if (!row.SequenceEqual(_outputRow) || !exe.SequenceEqual(_outputExe) ||
            !_rowComposition.ApplyExact(row, true).AsSpan().SequenceEqual(_sourceRow))
            throw new InvalidDataException("Authored collision/music logical output or complete WAD inverse changed.");
        byte[] inverse = exe.ToArray();
        foreach (var patch in _music.InversePatches)
        {
            byte[] before = Convert.FromHexString(patch.BeforeHex), after = Convert.FromHexString(patch.AfterHex);
            if (!inverse.AsSpan(patch.FileOffset, patch.ByteLength).SequenceEqual(before))
                throw new InvalidDataException("The complete music inverse has a stale window.");
            after.CopyTo(inverse, patch.FileOffset);
        }
        if (!inverse.AsSpan().SequenceEqual(_sourceExe)) throw new InvalidDataException("The music inverse changed protected executable bytes.");
    }
    public byte[] CreateExpectedRawSector(int lba, ReadOnlySpan<byte> sourceRaw)
    {
        if (!_ownedSectors.Contains(lba) || sourceRaw.Length != RawBytes)
            throw new InvalidDataException("The raw sector is outside this complete authored composition.");
        RawMode2Form1SectorIntegrity.RequireRawSectorIntegrity(sourceRaw, lba);
        long start = (long)lba * LogicalBytes;
        NativeByteExtent owner = start >= ExecutableDiscExtent.Offset ? ExecutableDiscExtent : RowDiscExtent;
        byte[] file = ReferenceEquals(owner, ExecutableDiscExtent) ? _sourceExe : _sourceRow;
        int fileOffset = checked((int)(start - owner.Offset));
        if (fileOffset < 0 || fileOffset > file.Length - LogicalBytes ||
            !sourceRaw.Slice(24, LogicalBytes).SequenceEqual(file.AsSpan(fileOffset, LogicalBytes)))
            throw new InvalidDataException("The raw sector is not the complete certified native file preimage.");
        byte[] expected = sourceRaw.ToArray();
        foreach (var write in _writes)
        {
            long first = Math.Max(start, write.Address.Final.Offset), end = Math.Min(start + LogicalBytes, write.Address.Final.EndExclusive);
            if (first >= end) continue;
            int offset = checked((int)(first - write.Address.Final.Offset));
            int rawOffset = checked((int)(first - start)) + 24, count = checked((int)(end - first));
            if (!sourceRaw.Slice(rawOffset, count).SequenceEqual(write.Before.AsSpan(offset, count)))
                throw new InvalidDataException("A composed native disc window has a stale preimage.");
            write.After.AsSpan(offset, count).CopyTo(expected.AsSpan(rawOffset, count));
        }
        return RawMode2Form1SectorIntegrity.RebuildRawSectorCopy(expected, lba);
    }
    public Id65AuthoredCollisionMusicReadback Verify(Stream source, Stream output, CancellationToken cancellationToken = default)
    {
        if (!source.CanRead || !source.CanSeek || !output.CanRead || !output.CanSeek || source.Length != ImageBytes || output.Length != ImageBytes)
            throw new InvalidDataException("Composition readback requires two exact complete seekable images.");
        source.Position = output.Position = 0;
        byte[] before = new byte[RawBytes * 256], after = new byte[RawBytes * 256];
        using var sourceHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var outputHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        int sector = 0, verified = 0, changedSectors = 0; long changedBytes = 0;
        while (source.Position < ImageBytes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int count = (int)Math.Min(before.Length, ImageBytes - source.Position);
            source.ReadExactly(before.AsSpan(0, count)); output.ReadExactly(after.AsSpan(0, count));
            sourceHash.AppendData(before.AsSpan(0, count)); outputHash.AppendData(after.AsSpan(0, count));
            for (int offset = 0; offset < count; offset += RawBytes, sector++)
            {
                ReadOnlySpan<byte> original = before.AsSpan(offset, RawBytes), actual = after.AsSpan(offset, RawBytes);
                if (_ownedSectors.Contains(sector))
                {
                    if (!actual.SequenceEqual(CreateExpectedRawSector(sector, original)))
                        throw new InvalidDataException($"Authored owned raw sector {sector} has a wrong payload, header or EDC/ECC.");
                    verified++;
                }
                else if (!original.SequenceEqual(actual)) throw new InvalidDataException($"Authored output changed protected raw sector {sector}.");
                if (!original.SequenceEqual(actual))
                {
                    changedSectors++;
                    for (int index = 0; index < RawBytes; index++) if (original[index] != actual[index]) changedBytes++;
                }
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        string sourceSha = Convert.ToHexStringLower(sourceHash.GetHashAndReset());
        if (source.Length != ImageBytes || output.Length != ImageBytes || source.Position != ImageBytes || output.Position != ImageBytes ||
            sourceSha != SourceImageSha256 || verified != _ownedSectors.Count)
            throw new InvalidDataException("Composition readback lost the frozen source digest, complete lengths or ownership coverage.");
        return new(sourceSha, Convert.ToHexStringLower(outputHash.GetHashAndReset()), DeterministicPlanSha256, verified, changedSectors, changedBytes);
    }
    internal static string Hash(ReadOnlySpan<byte> bytes) => NativeExactByteComposition.Hash(bytes);
}
