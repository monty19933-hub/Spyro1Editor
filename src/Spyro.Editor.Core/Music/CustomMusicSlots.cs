using System.Buffers.Binary;
using System.Collections.Concurrent;
using Spyro.Editor.Core.Crash;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.Core.Music;

/// <summary>Songs from other games in Spyro's unused XA slots. Spyro's music is six streams of eight interleaved XA
/// channels (track t is channel t % 8 of PETEXA{t / 8}.STR: every eighth sector). A track is its audio sectors, the
/// last flagged end-of-file, then non-audio filler; the executable's end table holds, per track, the file sector the
/// game seeks back to the start from (two sectors after the track's last). The new song is laid out the same way.</summary>
public static class CustomMusicSlots
{
    private const int Channels = 8, Streams = 6, TrackCount = 48;
    private const int SubmodeAudio = 0x64, SubmodeAudioEnd = 0xE4, StereoCoding = 0x01;
    /// <summary>The loop mark sits this many sectors after the track's last sector, as in Spyro's own tracks.</summary>
    private const int EndMarkAfterLast = 2;

    public sealed record EncodedSong(MusicTrackEntry Track, int Slot, int Passes, byte[][] Sectors)
    {
        public int EndMark => Channels * (Sectors.Length - 1) + Slot % Channels + EndMarkAfterLast;
        public double Seconds => Sectors.Length * (double)XaAudio.FramesPerSector / XaAudio.SampleRate;
    }

    private static readonly ConcurrentDictionary<(string Disc, int Track, int Capacity), EncodedSong> Cache = new();

    /// <summary>Renders and encodes a song for its slot: two passes of it when they fit (the game's loop seek is then
    /// half as frequent), each a whole number of sectors long. The slot's capacity comes from the image.</summary>
    public static EncodedSong Encode(MusicTrackEntry track, CrashWarpedDisc? crash, string imagePath, YearOfTheDragonDisc? yotd = null)
    {
        int slot = track.CustomSlot ?? throw new ArgumentException($"{track.DisplayName} is one of Spyro's own tracks.", nameof(track));
        if (YearOfTheDragonDisc.LevelSongs.FirstOrDefault(s => s.TrackId == track.TrackId) is { } level)
        {
            // Already Spyro-format XA: the sectors are copied as they are, twice when two passes fit.
            if (yotd == null)
                throw new InvalidDataException($"{track.DisplayName} comes from Spyro: Year of the Dragon. Choose your Year of the Dragon disc in Game Settings → Music, or pick another track.");
            int room = Capacity(imagePath, slot);
            return Cache.GetOrAdd((yotd.ImagePath, track.TrackId, room), _ =>
            {
                byte[][] pass = yotd.LevelSong(level);
                int passes = 2 * pass.Length <= room ? 2 : pass.Length <= room ? 1
                    : throw new InvalidDataException($"{track.DisplayName} is longer than the free music slot ({room * (double)XaAudio.FramesPerSector / XaAudio.SampleRate:0} s).");
                return new EncodedSong(track, slot, passes, [.. Enumerable.Repeat(pass, passes).SelectMany(p => p)]);
            });
        }
        var song = CrashMusic.Songs.FirstOrDefault(s => s.TrackId == track.TrackId)
            ?? throw new InvalidDataException($"{track.DisplayName} has no source.");
        if (crash == null)
            throw new InvalidDataException($"{track.DisplayName} comes from Crash Bandicoot: Warped. Choose your Crash disc with \"Crash disc…\" in the Add Object browser, or pick another track.");
        int capacity = Capacity(imagePath, slot);
        return Cache.GetOrAdd((crash.ImagePath, track.TrackId, capacity), _ =>
        {
            var (instruments, sequence) = CrashMusic.Load(crash, song);
            int sectorsPerPass = Math.Max(1, (int)Math.Round(CrashMusic.PassSeconds(sequence) * XaAudio.SampleRate / XaAudio.FramesPerSector));
            int passes = 2 * sectorsPerPass <= capacity ? 2 : sectorsPerPass <= capacity ? 1
                : throw new InvalidDataException($"{track.DisplayName} is longer than the free music slot ({capacity * (double)XaAudio.FramesPerSector / XaAudio.SampleRate:0} s).");
            short[] pcm = CrashMusic.RenderLoop(instruments, sequence, XaAudio.SampleRate, passes, sectorsPerPass * XaAudio.FramesPerSector);
            byte[][] sectors = new byte[pcm.Length / 2 / XaAudio.FramesPerSector][];
            var left = new XaAudio.ChannelState(); var right = left;
            for (int i = 0; i < sectors.Length; i++)
            {
                sectors[i] = new byte[XaAudio.SectorDataBytes];
                XaAudio.EncodeSector(pcm.AsSpan(2 * i * XaAudio.FramesPerSector, 2 * XaAudio.FramesPerSector), ref left, ref right, sectors[i]);
            }
            return new EncodedSong(track, slot, passes, sectors);
        });
    }

    /// <summary>How many sectors the slot's channel can hold with its loop mark still inside the stream file.</summary>
    public static int Capacity(string imagePath, int slot)
    {
        var (_, _, sectors) = Stream(imagePath, slot / Channels);
        return (sectors - EndMarkAfterLast - 1 - slot % Channels) / Channels + 1;
    }

    /// <summary>Sets the slot's loop mark in the executable's end table (just before the level-to-track table).</summary>
    public static void PatchEndTable(byte[] exe, string imagePath, EncodedSong song)
    {
        int table = EndTableOffset(exe, imagePath);
        int fileSectors = Stream(imagePath, song.Slot / Channels).Sectors;
        if (song.EndMark >= fileSectors) throw new InvalidDataException("The song does not fit its music slot.");
        BinaryPrimitives.WriteInt32LittleEndian(exe.AsSpan(table + 4 * song.Slot), song.EndMark);
    }

    public static int EndTableOffset(byte[] exe, string imagePath)
    {
        int[] lbas = Enumerable.Range(0, Streams).Select(i => Stream(imagePath, i).Lba).ToArray();
        int mapping = LevelMusicPatchExporter.MappingTableOffset(exe, lbas);
        int table = mapping - TrackCount * 4;
        for (int t = 0; t < TrackCount; t++)
        {
            int end = BinaryPrimitives.ReadInt32LittleEndian(exe.AsSpan(table + 4 * t));
            if (end <= 0 || end >= Stream(imagePath, t / Channels).Sectors)
                throw new InvalidDataException("The executable's music end table is not where this editor expects it.");
        }
        return table;
    }

    /// <summary>Writes the song's sectors into its channel, the last flagged end-of-file, then turns the old track's
    /// remaining audio into filler like the sectors after Spyro's own tracks.</summary>
    public static void WriteAudio(string imagePath, EncodedSong song)
    {
        var (lba, _, sectors) = Stream(imagePath, song.Slot / Channels);
        int channel = song.Slot % Channels;
        DiscLayout layout = DiscImage.DetectLayout(imagePath);
        if (layout.SectorSize != 2352 || layout.UserOffset != 24)
            throw new InvalidDataException("Custom music needs a raw MODE2/2352 disc image.");
        using var stream = File.Open(imagePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
        byte[] raw = new byte[2352];
        void Read(int k) { stream.Seek((long)(lba + k) * 2352, SeekOrigin.Begin); stream.ReadExactly(raw); }
        void Write(int k) { stream.Seek((long)(lba + k) * 2352, SeekOrigin.Begin); stream.Write(raw); }
        byte[]? filler = null;
        for (int k = sectors - 1; k >= 0 && filler == null; k--)
        {
            Read(k);
            if (raw[18] == 0 && raw[22] == 0) filler = raw[16..].ToArray();   // a non-audio filler sector (Form 1, valid ECC)
        }
        Read(channel);
        byte fileNumber = raw[16];
        for (int i = 0; i < song.Sectors.Length; i++)
        {
            int k = Channels * i + channel;
            Read(k);
            if (raw[17] != channel && raw[18] != 0) throw new InvalidDataException($"PETEXA{song.Slot / Channels}.STR is not interleaved as expected.");
            byte submode = (byte)(i == song.Sectors.Length - 1 ? SubmodeAudioEnd : SubmodeAudio);
            for (int copy = 0; copy < 2; copy++)
            {
                raw[16 + 4 * copy] = fileNumber; raw[17 + 4 * copy] = (byte)channel; raw[18 + 4 * copy] = submode; raw[19 + 4 * copy] = StereoCoding;
            }
            XaAudio.ReplaceSectorAudio(raw, song.Sectors[i]);
            Write(k);
        }
        // Old audio after the new end: the drive may read a sector or two past the loop mark before its seek.
        for (int k = Channels * song.Sectors.Length + channel; k < sectors; k += Channels)
        {
            Read(k);
            if ((raw[18] & 0x04) == 0) break;   // the old track's filler starts here
            if (filler != null) filler.CopyTo(raw.AsSpan(16));
            else
            {
                raw[18] = raw[22] = SubmodeAudio;
                XaAudio.ReplaceSectorAudio(raw, new byte[XaAudio.SectorDataBytes]);   // silence
            }
            Write(k);
        }
    }

    /// <summary>Writes songs into a finished image: each one's audio, then its loop mark in the image's executable.</summary>
    public static IReadOnlyList<EncodedSong> Install(string imagePath, IEnumerable<MusicTrackEntry> tracks, CrashWarpedDisc? crash, YearOfTheDragonDisc? yotd = null)
    {
        var list = tracks.DistinctBy(t => t.TrackId).ToArray();
        if (list.Select(t => t.CustomSlot).Distinct().Count() != list.Length)
            throw new InvalidDataException("Two songs from other games share one music slot.");
        List<EncodedSong> installed = [];
        foreach (var track in list)
        {
            var song = Encode(track, crash, imagePath, yotd);
            WriteAudio(imagePath, song);
            DiscLayout layout = DiscImage.DetectLayout(imagePath);
            int exeLba; byte[] exe;
            using (var source = File.OpenRead(imagePath))
            {
                var record = DiscImage.FindRootFileRecord(source, layout, LevelMusicPatchExporter.IsExecutableName);
                exeLba = record.Lba;
                exe = DiscImage.ReadFileBytes(source, layout, record.Lba, 0, record.Size);
            }
            int table = EndTableOffset(exe, imagePath);
            using var stream = File.Open(imagePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
            DiscImage.WriteFileBytes(stream, layout, exeLba, table + 4 * song.Slot, BitConverter.GetBytes(song.EndMark));
            installed.Add(song);
        }
        return installed;
    }

    private static (int Lba, int Size, int Sectors) Stream(string imagePath, int index)
    {
        DiscLayout layout = DiscImage.DetectLayout(imagePath);
        using FileStream source = File.OpenRead(imagePath);
        var record = DiscImage.FindRootFileRecord(source, layout, name => name.Equals($"PETEXA{index}.STR", StringComparison.OrdinalIgnoreCase));
        return (record.Lba, record.Size, record.Size / 2048);
    }
}
