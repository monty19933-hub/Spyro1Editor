using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.Core.Exporting;

/// <summary>The wing flap of Spyro's hover (<see cref="NativeHover"/>): the flight levels' climbing flap, Spyro's animation
/// <see cref="NativeHover.FlapAnimation"/>, a level-local player record (4,512 bytes) that only the five flight levels and
/// Gnasty's Loot carry, byte-identical. Create BIN adds Sunny Flight's copy to the player stream of every other level of a
/// game with Hover (the game's own levels, levels built in the editor and the new world's), the way supercharge ground's
/// animations are added (<see cref="NativeSuperchargeAnimations"/>). A level whose game RAM in the current memory mode
/// cannot take it (with 2 MB: Tree Tops, Haunted Towers and Twilight Harbor as shipped) is left without: there the hover's
/// entry finds no record and keeps the drop pose.</summary>
public sealed class NativeHoverFlap
{
    public const int AnimationId = NativeHover.FlapAnimation, DonorLevelId = 15, ByteLength = 4512;
    internal const string RecordSha256 = "118ffb78244fdb8b121aca7d730f28d3f4f1b80211f1e1456092175ff9f71d54";
    private static readonly int[] NativeOwners = [15, 25, 35, 45, 55, 64];

    private readonly NativePlayerAnimationSource _source;
    private NativeHoverFlap(NativePlayerAnimationSource source) { _source = source; }

    /// <summary>The flap from the original USA image (every owner's copy checked).</summary>
    public static async Task<NativeHoverFlap> CaptureAsync(string originalUSA, CancellationToken token = default)
    {
        var source = await NativePlayerAnimationSource.CaptureAsync(originalUSA, token);
        foreach (int owner in NativeOwners)
        {
            var record = source.Levels.Single(l => l.LevelId == owner).Stream.Records.SingleOrDefault(r => r.AnimationId == AnimationId);
            if (record == null || record.Sha256 != RecordSha256 || record.ByteLength != ByteLength)
                throw new InvalidDataException($"Spyro's flap (animation {AnimationId}) differs from the USA game's in level {owner}.");
        }
        return new(source);
    }

    /// <summary>The original levels whose player stream already has the flap.</summary>
    public static bool CarriedBy(int levelId) => NativeOwners.Contains(levelId);

    /// <summary>The level data with the flap in its player stream: the same bytes when it already has it, null when the
    /// level's game RAM in the current memory mode cannot take it (a level already past the margin keeps its own end, as
    /// for object imports: <see cref="GameRam.Exceeds(UniversalLevelData, uint, UniversalLevelData, uint)"/>).</summary>
    public byte[]? Merge(byte[] levelData, uint copyBuffer)
    {
        var layout = _source.MergeCaptured(levelData, DonorLevelId, [AnimationId]);
        if (layout.AddedIds.Count == 0) return levelData;
        if (!layout.AddedIds.SequenceEqual([AnimationId]) || layout.ExactGrowth != ByteLength)
            throw new InvalidDataException("Adding Spyro's flap added unexpected player records.");
        var before = new UniversalLevelData(levelData);
        var merged = new UniversalLevelData(layout.CopyFinalBytes());
        if (GameRam.Exceeds(merged, copyBuffer, before, copyBuffer)) return null;
        var check = _source.MergeCaptured(merged.Bytes, DonorLevelId, [AnimationId]);
        if (check.AddedIds.Count != 0 || !check.CopyFinalBytes().AsSpan().SequenceEqual(merged.Bytes))
            throw new InvalidDataException("Spyro's flap is missing from the level's player stream after adding it.");
        return merged.Bytes;
    }

    /// <summary>Whether the level data's player stream has the flap.</summary>
    public bool Carries(byte[] levelData) => _source.MergeCaptured(levelData, DonorLevelId, [AnimationId]).AddedIds.Count == 0;

    /// <summary>Adds the flap to every listed level of an export that lacks it (data at the level's WAD entry, overlay
    /// just before it; the copy buffer from the executable's bindings). Changed levels go into
    /// <paramref name="replacements"/>; returns the names of the levels without room for it.</summary>
    public IReadOnlyList<string> ApplyForExport(IDictionary<int, byte[]> replacements, Func<int, byte[]> entryBytes, byte[] exe,
        IEnumerable<LevelDefinition> levels)
    {
        List<string> noRoom = [];
        foreach (var level in levels)
        {
            byte[] Current(int entry) => replacements.TryGetValue(entry, out var bytes) ? bytes : entryBytes(entry);
            byte[] data = Current(level.SourceWadEntry);
            uint copy = new UniversalOverlay(Current(level.SourceWadEntry - 1), UniversalExeBindings.Read(exe, level.LevelId)).CopyBuffer;
            byte[]? merged = Merge(data, copy);
            if (merged == null) noRoom.Add(level.DisplayName);
            else if (!ReferenceEquals(merged, data)) replacements[level.SourceWadEntry] = merged;
        }
        return noRoom;
    }

    /// <summary>The export step: with Hover on, the flap goes into every listed level that lacks it and has room (see
    /// <see cref="ApplyForExport(IDictionary{int, byte[]}, Func{int, byte[]}, byte[], IEnumerable{LevelDefinition})"/>);
    /// the levels without room are noted in the export's Hover scope. Nothing happens with Hover off.</summary>
    public static async Task ApplyForExportAsync(string originalUSA, IDictionary<int, byte[]> replacements, Func<int, byte[]> entryBytes,
        byte[] exe, IEnumerable<LevelDefinition> levels, CancellationToken token = default)
    {
        if (!NativeHover.Enabled) return;
        var flap = await CaptureAsync(originalUSA, token);
        NativeHover.RecordWithoutFlap(flap.ApplyForExport(replacements, entryBytes, exe, levels));
    }

    /// <summary>What Create BIN says when some levels had no room for the flap (nothing when all did).</summary>
    public static string NoRoomNote(IReadOnlyCollection<string> levels) => levels.Count == 0 ? ""
        : $" Hover: no room for Spyro's wing flap in {string.Join(", ", levels)}, so there he hovers without flapping.";
}
