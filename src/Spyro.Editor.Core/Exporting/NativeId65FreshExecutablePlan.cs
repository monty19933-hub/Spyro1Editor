using System.Security.Cryptography;
using Spyro.Editor.Core.Music;

namespace Spyro.Editor.Core.Exporting;

public sealed record NativeId65FreshExecutableWindow(string Name, int FileOffset, string BeforeHex, string AfterHex);

/// <summary>
/// Exact original bootstrap, unchanged existing built-in music compiler, then
/// fixed LAB title. The sequential bridge is not an independent disjoint patch.
/// No native runtime, glyph allocation, all-state or disc publication authority.
/// </summary>
public sealed class NativeId65FreshExecutablePlan
{
    internal const string OriginalExecutableSha256 = "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998";
    private readonly byte[] _original;
    private readonly byte[] _final;

    private NativeId65FreshExecutablePlan(byte[] original, int track)
    {
        _original = original;
        byte[] current = original.ToArray();
        var stages = new List<NativeId65FreshExecutableWindow>();
        Stage("id65-resident-Town-initializer", 0x1CA8, "E0B60580", "98A80580");
        Stage("hidden-selector-selected-ID-minus-ten-limit", 0x1DF44, "3700422C", "3800422C");
        Stage("hidden-selector-normal-global-state-entry", 0x1E034, "37B6000800000000", "1C0684AF880680AF");
        Stage("conditional-normal-global-state-classifier", 0x40C0, "6666023C", "005E023C");
        BootstrapSha256 = Hash(current);
        if (BootstrapSha256 != "0f6996babd64e815529df648fd7136f478c42f8187567d71439462df12b931ec")
            throw new InvalidDataException("Fresh executable bootstrap did not reproduce its complete source-bound intermediate.");
        Stage("temporary-existing-music-certificate-name", 0x6007C, "64550780", "E4010180");
        ExistingMusicInputSha256 = Hash(current);
        var certificate = Id65MusicSourceCertificate.FromFrozenExecutable(current);
        Id65MusicSelectionPlan music = Id65BuiltInMusicSelectionCompiler.Compile(certificate, current, track);
        foreach (Id65MusicFilePatch patch in music.Patches)
            Stage(patch.Name, patch.FileOffset, patch.BeforeHex, patch.AfterHex);
        if (!current.SequenceEqual(music.CopyOutputExecutable()))
            throw new InvalidDataException("Fresh executable music stage lost the unchanged compiler's complete output.");
        MusicOutputSha256 = Hash(current);
        Stage("restore-owned-title-pointer", 0x6007C, "E4010180", "64550780");
        Stage("fixed-four-byte-LAB-title", 0x65D64, "41000000", "4C414200");
        if (!current.AsSpan(0x65D5C, 8).SequenceEqual(original.AsSpan(0x65D5C, 8)) ||
            !current.AsSpan(0x65D68, 8).SequenceEqual("JACQUES\0"u8))
            throw new InvalidDataException("Fixed LAB title crossed an indexed neighbor or separate Jacques string owner.");
        byte[] inverse = current.ToArray();
        foreach (NativeId65FreshExecutableWindow stage in stages.AsEnumerable().Reverse())
            ApplyWindow(inverse, stage with { BeforeHex = stage.AfterHex, AfterHex = stage.BeforeHex });
        if (!inverse.SequenceEqual(original))
            throw new InvalidDataException("Sequential executable inverse did not restore the complete original.");
        _final = current;
        SelectedBuiltInTrack = track;
        SequentialStages = stages.AsReadOnly();
        var folded = new List<NativeId65FreshExecutableWindow>();
        for (int at = 0; at < original.Length;)
        {
            if (original[at] == current[at])
            {
                at++;
                continue;
            }
            int start = at++;
            while (at < original.Length && original[at] != current[at])
                at++;
            folded.Add(new($"final-original-diff-{folded.Count}", start,
                Convert.ToHexString(original.AsSpan(start, at - start)), Convert.ToHexString(current.AsSpan(start, at - start))));
        }
        FoldedWindows = folded.AsReadOnly();
        byte[] replay = original.ToArray();
        foreach (var window in FoldedWindows)
            ApplyWindow(replay, window);
        if (!replay.SequenceEqual(current))
            throw new InvalidDataException("Folded executable windows lost their exact untouched complement.");
        FinalSha256 = Hash(current);

        void Stage(string name, int offset, string before, string after)
        {
            var window = new NativeId65FreshExecutableWindow(name, offset, before, after);
            ApplyWindow(current, window);
            stages.Add(window);
        }
    }

    public int SelectedBuiltInTrack { get; }
    public string BootstrapSha256 { get; }
    public string ExistingMusicInputSha256 { get; }
    public string MusicOutputSha256 { get; }
    public string FinalSha256 { get; }
    public bool SourceCertified => true;
    public bool GlobalTransitionState12Supported => false;
    public bool GlyphAllocationOrRenderingProven => false;
    public bool RuntimeVerified => false;
    public bool PublicationApproved => false;
    public IReadOnlyList<NativeId65FreshExecutableWindow> SequentialStages { get; }
    public IReadOnlyList<NativeId65FreshExecutableWindow> FoldedWindows { get; }
    public byte[] CopyExecutable() => _final.ToArray();

    internal static NativeId65FreshExecutablePlan Compile(ReadOnlySpan<byte> original, int selectedBuiltInTrack)
    {
        if (MusicTrackCatalog.Find(selectedBuiltInTrack)?.IsSelectable != true)
            throw new ArgumentOutOfRangeException(nameof(selectedBuiltInTrack), "Unknown or reserved native music track.");
        if (original.Length != 0x66000)
            throw new InvalidDataException("Fresh executable requires the bounded complete original image.");
        byte[] snapshot = original.ToArray();
        RequireOriginal(snapshot);
        return new(snapshot, selectedBuiltInTrack);
    }

    internal static void RequireOriginal(ReadOnlySpan<byte> original)
    {
        if (original.Length != 0x66000 || Hash(original) != OriginalExecutableSha256)
            throw new InvalidDataException("Fresh executable cannot re-label edited or unrelated bytes as the exact retail source.");
    }

    internal byte[] RevertExact(ReadOnlySpan<byte> afterimage)
    {
        if (!afterimage.SequenceEqual(_final))
            throw new InvalidDataException("Fresh executable inverse requires the complete exact final afterimage.");
        return _original.ToArray();
    }

    private static void ApplyWindow(byte[] bytes, NativeId65FreshExecutableWindow window)
    {
        byte[] before = Convert.FromHexString(window.BeforeHex);
        byte[] after = Convert.FromHexString(window.AfterHex);
        if (before.Length == 0 || before.Length != after.Length || window.FileOffset < 0 ||
            window.FileOffset > bytes.Length - before.Length ||
            !bytes.AsSpan(window.FileOffset, before.Length).SequenceEqual(before))
            throw new InvalidDataException("Fresh executable sequential window failed its exact preimage guard.");
        after.CopyTo(bytes, window.FileOffset);
    }

    internal static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
