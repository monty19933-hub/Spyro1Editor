using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using Spyro.Editor.Core.Music;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Immutable authority for the already inspected, frozen relocated USA SCUS.
/// There is deliberately no JSON/UI constructor or synthetic production issuer.
/// A certificate is not disc-placement, writer or playback authority.
/// </summary>
internal sealed class Id65MusicSourceCertificate
{
    private readonly byte[] _source;
    private readonly ReadOnlyDictionary<string, string> _regions;
    private static readonly (string Name, int Offset, int Length)[] Regions =
    [
        ("psx-header", 0, 0x800),
        ("initial-mapping", 0x5F79C, 48 * 4),
        ("initial-consumer", 0x6504, 0x24),
        ("late-table", 0x5F85C, 35 * 3 * 4),
        ("petexa-lbas", 0x5FA00, 6 * 4),
        ("late-consumer", 0x1C57C, 0x8C),
        ("prng", 0x52F2C, 0x30),
        ("initial-mapping-reference", 0x651C, 4),
        ("late-mapping-reference", 0x1C5EC, 4),
        ("late-base-reference", 0x1C5A0, 4),
        ("petexa-base-reference", 0x2D80, 4),
        ("initial-call-0", 0x1E7E8, 8), ("initial-call-1", 0x1FB04, 8),
        ("initial-call-2", 0x1FB80, 8), ("initial-call-3", 0x22D3C, 8),
        ("initial-call-4", 0x23534, 8), ("initial-call-5", 0x23904, 8),
        ("initial-call-6", 0x23960, 8), ("initial-call-7", 0x23CBC, 8),
        ("late-call-0", 0x6D94, 8), ("late-call-1", 0x1EB40, 8),
        ("late-call-2", 0x2350C, 8), ("late-call-3", 0x2353C, 8),
        ("late-call-4", 0x238B0, 8), ("late-call-5", 0x23A88, 8),
        ("late-call-6", 0x23EA0, 8), ("late-call-7", 0x2407C, 8)
    ];

    private Id65MusicSourceCertificate(byte[] source)
    {
        _source = source.ToArray();
        SourceExecutableSha256 = Hash(_source);
        _regions = new(Regions.ToDictionary(region => region.Name,
            region => Hash(_source.AsSpan(region.Offset, region.Length)), StringComparer.Ordinal));
    }

    public string SourceExecutableSha256 { get; }
    public IReadOnlyDictionary<string, string> RegionSha256 => _regions;
    public byte[] CopyExecutable() => _source.ToArray();

    internal static Id65MusicSourceCertificate FromFrozenExecutable(byte[] source)
    {
        ArgumentNullException.ThrowIfNull(source);
        byte[] snapshot = source.ToArray();
        if (snapshot.Length != 0x66000 || Hash(snapshot) != UnusedLevel65MusicOwnershipClosure.ExecutableSha256)
            throw new InvalidDataException("Music source does not match the frozen relocated USA executable.");
        if (!snapshot.AsSpan(0, 8).SequenceEqual(Convert.FromHexString("50532D5820455845")) ||
            BinaryPrimitives.ReadUInt32LittleEndian(snapshot.AsSpan(0x18, 4)) != 0x80010000 ||
            BinaryPrimitives.ReadUInt32LittleEndian(snapshot.AsSpan(0x1C, 4)) != 0x65800)
            throw new InvalidDataException("The frozen music PS-X EXE load range changed.");
        return new(snapshot);
    }

    internal void ValidateBinding(byte[] source)
    {
        if (source.Length != _source.Length || Hash(source) != SourceExecutableSha256 || !source.AsSpan().SequenceEqual(_source))
            throw new InvalidDataException("Music source does not match its immutable executable certificate.");
        foreach ((string name, int offset, int length) in Regions)
        {
            if (Hash(source.AsSpan(offset, length)) != _regions[name])
                throw new InvalidDataException($"Music source region {name} has stale evidence.");
        }
    }

    internal static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}

internal sealed record Id65MusicFilePatch(
    string Name, int FileOffset, int ByteLength, string BeforeHex, string AfterHex, string Semantics)
{
    public Id65MusicFilePatch Invert() => this with { BeforeHex = AfterHex, AfterHex = BeforeHex };
}

internal sealed class Id65MusicSelectionPlan
{
    private readonly byte[] _output;
    internal Id65MusicSelectionPlan(int selectedTrackId, string sourceSha256, byte[] output, IEnumerable<Id65MusicFilePatch> patches)
    {
        SelectedTrackId = selectedTrackId; SourceExecutableSha256 = sourceSha256;
        _output = output.ToArray(); OutputExecutableSha256 = Id65MusicSourceCertificate.Hash(_output);
        Patches = Array.AsReadOnly(patches.ToArray());
        InversePatches = Array.AsReadOnly(Patches.Select(patch => patch.Invert()).ToArray());
    }
    public int SelectedTrackId { get; }
    public string SourceExecutableSha256 { get; }
    public string OutputExecutableSha256 { get; }
    public IReadOnlyList<Id65MusicFilePatch> Patches { get; }
    public IReadOnlyList<Id65MusicFilePatch> InversePatches { get; }
    public byte[] CopyOutputExecutable() => _output.ToArray();
}

internal static class Id65BuiltInMusicSelectionCompiler
{
    private const int MappingOffset = 0x5F79C;
    private const int Slot35Offset = MappingOffset + 35 * 4;
    private const int LateOffset = 0x5F85C;
    private const int PeteXaOffset = 0x5FA00;
    private static readonly Id65MusicFilePatch[] BridgePatches =
    [
        new("preload-scus-high-in-condition-branch-delay", 0x1C57C, 4,
            "00000000", "0780083C",
            "The existing condition-branch delay slot loads t0=0x80070000; t0 is caller-saved and otherwise dead in this function."),
        new("preload-continuous-index-in-prng-jal-delay", 0x1C584, 4,
            "00000000", "6459098D",
            "The existing PRNG JAL delay slot loads t1=[0x80075964]; the exact PRNG callee does not write t0 or t1."),
        new("select-bounded-row-and-retain-id65-flag", 0x1C594, 32,
            "0780033C6459638C0780043C5CF08424402803002128A300802805002128A400",
            "2300262D2118260150F00425402803002128A300802805002128A4000100C638",
            "For retail, (index + 1) against base row -1 equals the original row; ID65 reads row 34, then retains an ID65-only +1 flag."),
        new("promote-row34-source-to-slot35-and-reload-timer", 0x1C5D8, 8,
            "0780033CC858638C", "21104600C858038D",
            "Only ID65 adds one to row 34's source value 34, then the unchanged lookup resolves mapping slot 35; the timer reload uses pinned t0.")
    ];

    /// <summary>
    /// Generates only the exact fixed bridge and one built-in track word. All
    /// addresses are executable-file-relative. Source provenance belongs to
    /// the immutable certificate; disc placement, sector integrity, publication
    /// and runtime acceptance remain outside this pure compiler.
    /// </summary>
    public static Id65MusicSelectionPlan Compile(Id65MusicSourceCertificate certificate, ReadOnlyMemory<byte> sourceExecutable, int trackId)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        MusicTrackEntry? track = MusicTrackCatalog.Find(trackId);
        if (track == null || !track.IsSelectable)
            throw new ArgumentOutOfRangeException(nameof(trackId), trackId, "The selected track is unknown or reserved, not a selectable built-in track.");
        byte[] source = sourceExecutable.ToArray();
        certificate.ValidateBinding(source);
        ValidateTables(source);

        byte[] trackWord = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(trackWord, trackId);
        Id65MusicFilePatch[] patches = BridgePatches.Append(new Id65MusicFilePatch(
            "author-independent-slot35-witness-track", Slot35Offset, 4,
            "13000000", Convert.ToHexString(trackWord),
            string.Create(CultureInfo.InvariantCulture,
                $"The independent ID65 initial slot selects built-in {track.DisplayName} track {trackId} for the static witness transaction."))).ToArray();
        byte[] output = source.ToArray();
        int previousEnd = 0;
        foreach (Id65MusicFilePatch patch in patches)
        {
            if (patch.FileOffset < previousEnd)
                throw new InvalidDataException("Music owned patch windows overlap or are not ordered.");
            Apply(output, patch);
            previousEnd = checked(patch.FileOffset + patch.ByteLength);
        }
        if (!source.AsSpan(LateOffset, PeteXaOffset + 24 - LateOffset)
                .SequenceEqual(output.AsSpan(LateOffset, PeteXaOffset + 24 - LateOffset)))
            throw new InvalidDataException("The music leaf changed protected late-table or PETEXA storage.");

        // Reversing only the owned windows must recover the entire input, not
        // merely their immediate preimages: that also proves the complement.
        byte[] inverse = output.ToArray();
        foreach (Id65MusicFilePatch patch in patches) Apply(inverse, patch.Invert());
        if (!inverse.SequenceEqual(source))
            throw new InvalidDataException("The music leaf lost its protected complement or exact inverse.");
        return new(trackId, certificate.SourceExecutableSha256, output, patches);
    }

    private static void ValidateTables(byte[] source)
    {
        if (source.Length != 0x66000 || LateOffset + 35 * 3 * 4 != PeteXaOffset)
            throw new InvalidDataException("The music source table bounds are not the exact native layout.");
        for (int index = 0; index < 48; index++)
        {
            int value = ReadWord(source, MappingOffset + index * 4);
            if (value is < 0 or >= 48)
                throw new InvalidDataException($"Music mapping entry {index} is outside the native track table.");
        }
        for (int index = 0; index < 35 * 3; index++)
        {
            int value = ReadWord(source, LateOffset + index * 4);
            if (value is < 0 or >= 48)
                throw new InvalidDataException($"Music late-table entry {index} is outside the mapping table.");
            if (value == 35)
                throw new InvalidDataException("A retail late-table consumer aliases independently authored slot35.");
            if (index >= 34 * 3 && value != 34)
                throw new InvalidDataException("Music row34 is no longer the exact three-value ID65 bridge.");
        }
    }

    private static void Apply(byte[] destination, Id65MusicFilePatch patch)
    {
        byte[] before = Convert.FromHexString(patch.BeforeHex), after = Convert.FromHexString(patch.AfterHex);
        if (patch.FileOffset < 0 || patch.ByteLength <= 0 || patch.ByteLength != before.Length || before.Length != after.Length ||
            patch.FileOffset > destination.Length - before.Length || !destination.AsSpan(patch.FileOffset, before.Length).SequenceEqual(before))
            throw new InvalidDataException($"Music patch {patch.Name} failed its exact fixed-size preimage guard.");
        after.CopyTo(destination, patch.FileOffset);
    }
    private static int ReadWord(byte[] bytes, int offset) => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, 4));
}
