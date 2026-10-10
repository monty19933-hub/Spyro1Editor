using System.Buffers.Binary;

namespace Spyro.Editor.Core.Crash;

/// <summary>A Crash 3 song the editor can bring into Spyro: a music entry (Sony SEP sequences, a VAB instrument header)
/// and its wave banks, in one level file.</summary>
public sealed record CrashSong(int TrackId, string Name, string File, string Sha256, string Entry, int Sequence);

/// <summary>Renders Crash 3 music to PCM the way the PlayStation's sound chip plays it (SPU ADPCM samples, the SPU's
/// ADSR envelopes, the VAB's tone mapping), so it can be stored as Spyro XA audio. Crash streams nothing: its music is
/// note data played on instruments in sound memory.</summary>
public static class CrashMusic
{
    /// <summary>Toad Village's theme (the first sequence of its music entry).</summary>
    public static readonly CrashSong ToadVillage = new(100, "Toad Village", "S0/S000000B.NSF",
        "f5744a2434bcbae0adf067232c0dddbcef0ad915aa59386c44abaae123cdd44c", "Sn0bM", 0);
    public static IReadOnlyList<CrashSong> Songs { get; } = [ToadVillage];

    private const int MusicEntryType = 13, WaveBankType = 14;

    public sealed record Tone(int Volume, int Pan, int Center, int Shift, int Min, int Max, int BendRange, int Adsr1, int Adsr2, int Vag);
    public sealed record Program(int Volume, IReadOnlyList<Tone> Tones);
    public sealed record Instruments(int MasterVolume, IReadOnlyDictionary<int, Program> Programs, short[][] Samples, int[] LoopStart, bool[] Looped);
    public sealed record Sequence(int Resolution, int Tempo, byte[] Events);

    /// <summary>Every entry of a level file, whatever its chunk type (wave banks live in their own chunks).</summary>
    public static Dictionary<string, CrashEntry> Entries(byte[] nsf)
    {
        Dictionary<string, CrashEntry> result = new(StringComparer.Ordinal);
        for (int c = 0; c + CrashWarpedDisc.ChunkBytes <= nsf.Length; c += CrashWarpedDisc.ChunkBytes)
        {
            var chunk = nsf.AsSpan(c, CrashWarpedDisc.ChunkBytes);
            if (BinaryPrimitives.ReadUInt16LittleEndian(chunk) != 0x1234 || BinaryPrimitives.ReadUInt16LittleEndian(chunk[2..]) == 1) continue;
            int count = BinaryPrimitives.ReadInt32LittleEndian(chunk[8..]);
            if (count is < 0 or > 1024) continue;
            for (int i = 0; i < count; i++)
            {
                int start = BinaryPrimitives.ReadInt32LittleEndian(chunk[(16 + 4 * i)..]), end = BinaryPrimitives.ReadInt32LittleEndian(chunk[(20 + 4 * i)..]);
                if (start < 0 || end > chunk.Length || end - start < 16) continue;
                var e = chunk[start..end];
                if (BinaryPrimitives.ReadUInt32LittleEndian(e) != 0x0100FFFF) continue;
                uint id = BinaryPrimitives.ReadUInt32LittleEndian(e[4..]);
                int type = BinaryPrimitives.ReadInt32LittleEndian(e[8..]), items = BinaryPrimitives.ReadInt32LittleEndian(e[12..]);
                if (items is < 0 or > 256 || 16 + 4 * (items + 1) > e.Length) continue;
                List<byte[]> list = [];
                for (int k = 0; k < items; k++)
                {
                    int a = BinaryPrimitives.ReadInt32LittleEndian(e[(16 + 4 * k)..]), b = BinaryPrimitives.ReadInt32LittleEndian(e[(20 + 4 * k)..]);
                    if (a < 0 || b > e.Length || b < a) { list.Clear(); break; }
                    list.Add(e[a..b].ToArray());
                }
                result[CrashNsf.Name(id)] = new CrashEntry(CrashNsf.Name(id), type, list);
            }
        }
        return result;
    }

    /// <summary>A song's instruments and note data. The music entry's header names its VAB header (its own, or another
    /// music entry's) and the wave banks, whose data concatenated is the VAB's sample body.</summary>
    public static (Instruments Instruments, Sequence Sequence) Load(CrashWarpedDisc disc, CrashSong song)
    {
        byte[] bytes = disc.ReadFile(song.File);
        if (Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes)) != song.Sha256)
            throw new InvalidDataException($"The Crash disc's {song.File} differs from the original Crash Bandicoot: Warped (USA) release.");
        var entries = Entries(bytes);
        if (!entries.TryGetValue(song.Entry, out var music) || music.Type != MusicEntryType || music.Items.Count < 3)
            throw new InvalidDataException($"{song.Name}: the music entry {song.Entry} is missing.");
        byte[] header = music.Items[0];
        int sequences = BinaryPrimitives.ReadInt32LittleEndian(header);
        string vhName = CrashNsf.Name(BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(4)));
        if (!entries.TryGetValue(vhName, out var vhEntry) || vhEntry.Items.Count < 2 || vhEntry.Items[1].Length == 0)
            throw new InvalidDataException($"{song.Name}: the instrument header {vhName} is missing.");
        List<byte> body = [];
        for (int o = 8; o + 4 <= header.Length; o += 4)
        {
            string bank = CrashNsf.Name(BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(o)));
            if (entries.TryGetValue(bank, out var wave) && wave.Type == WaveBankType && wave.Items.Count >= 2) body.AddRange(wave.Items[1]);
        }
        var list = ReadSep(music.Items[2], sequences);
        if (song.Sequence >= list.Count) throw new InvalidDataException($"{song.Name}: sequence {song.Sequence} is missing.");
        return (ReadVab(vhEntry.Items[1], [.. body]), list[song.Sequence]);
    }

    public static Instruments ReadVab(byte[] vh, byte[] body)
    {
        if (vh.Length < 32 || BinaryPrimitives.ReadUInt32LittleEndian(vh) != 0x56414270) throw new InvalidDataException("Not a VAB instrument header.");
        int programs = BinaryPrimitives.ReadUInt16LittleEndian(vh.AsSpan(0x12)), vags = BinaryPrimitives.ReadUInt16LittleEndian(vh.AsSpan(0x16));
        Dictionary<int, Program> result = [];
        int toneBase = 32 + 128 * 16, block = 0;
        for (int p = 0; p < 128 && block < programs; p++)
        {
            var attributes = vh.AsSpan(32 + p * 16, 16);
            if (attributes[0] == 0) continue;
            List<Tone> tones = [];
            for (int t = 0; t < attributes[0]; t++)
            {
                var a = vh.AsSpan(toneBase + block * 16 * 32 + t * 32, 32);
                tones.Add(new(a[2], a[3], a[4], a[5], a[6], a[7], a[0xD], BinaryPrimitives.ReadUInt16LittleEndian(a[0x10..]),
                    BinaryPrimitives.ReadUInt16LittleEndian(a[0x12..]), BinaryPrimitives.ReadUInt16LittleEndian(a[0x16..])));
            }
            result[p] = new(attributes[1], tones);
            block++;
        }
        var sizes = vh.AsSpan(toneBase + programs * 16 * 32);
        short[][] samples = new short[vags + 1][]; int[] loop = new int[vags + 1]; bool[] looped = new bool[vags + 1];
        for (int v = 0, offset = 0; v <= vags; v++)
        {
            int size = BinaryPrimitives.ReadUInt16LittleEndian(sizes[(2 * v)..]) * 8;
            if (offset + size > body.Length) throw new InvalidDataException("The instrument samples are shorter than their header says.");
            (samples[v], loop[v], looped[v]) = DecodeAdpcm(body.AsSpan(offset, size));
            offset += size;
        }
        return new(vh[0x18], result, samples, loop, looped);
    }

    private static readonly int[,] Filters = { { 0, 0 }, { 60, 0 }, { 115, -52 }, { 98, -55 }, { 122, -60 } };

    /// <summary>SPU ADPCM: 16-byte blocks of 28 samples; flag 1 ends the sample, 2 with it repeats from the last block
    /// flagged 4.</summary>
    public static (short[] Pcm, int LoopStart, bool Looped) DecodeAdpcm(ReadOnlySpan<byte> data)
    {
        List<short> pcm = []; int s1 = 0, s2 = 0, loopStart = 0; bool looped = false;
        for (int b = 0; b + 16 <= data.Length; b += 16)
        {
            int shift = Math.Min(12, data[b] & 15), filter = Math.Min(4, data[b] >> 4), flags = data[b + 1];
            if ((flags & 4) != 0) loopStart = pcm.Count;
            for (int i = 0; i < 28; i++)
            {
                int s = (short)((data[b + 2 + i / 2] >> ((i & 1) * 4) & 15) << 12) >> shift;
                s = Math.Clamp(s + ((s1 * Filters[filter, 0] + s2 * Filters[filter, 1] + 32) >> 6), -32768, 32767);
                pcm.Add((short)s); s2 = s1; s1 = s;
            }
            if ((flags & 1) != 0) { looped = (flags & 2) != 0; break; }
        }
        return ([.. pcm], loopStart, looped);
    }

    /// <summary>The sequences of a SEP: "pQES", a version, then per sequence its number, resolution, tempo (µs per
    /// quarter note), rhythm, data length and MIDI events.</summary>
    public static List<Sequence> ReadSep(byte[] sep, int count)
    {
        if (sep.Length < 8 || BinaryPrimitives.ReadUInt32LittleEndian(sep) != 0x53455170) throw new InvalidDataException("Not a SEP sequence file.");
        List<Sequence> list = [];
        for (int i = 0, o = 6; i < count && o + 13 <= sep.Length; i++)
        {
            o += 2;
            int resolution = sep[o] << 8 | sep[o + 1], tempo = sep[o + 2] << 16 | sep[o + 3] << 8 | sep[o + 4];
            int length = Math.Min(sep[o + 7] << 24 | sep[o + 8] << 16 | sep[o + 9] << 8 | sep[o + 10], sep.Length - o - 11);
            o += 11;
            list.Add(new(resolution, tempo, sep.AsSpan(o, length).ToArray()));
            o += length;
        }
        return list;
    }

    private sealed class Voice
    {
        public int Channel, Note; public Tone Tone = null!; public short[] Pcm = []; public int LoopStart; public bool Looped;
        public double Position, Step, Left, Right; public bool Dead;
        public int Level; private int _phase, _counter;
        public void KeyOff() { if (_phase < 3) { _phase = 3; _counter = 0; } }

        /// <summary>One tick of the SPU envelope (44.1 kHz): attack, decay to the sustain level, sustain, release.</summary>
        public void Envelope()
        {
            int a1 = Tone.Adsr1, a2 = Tone.Adsr2;
            bool exponential, decrease; int shift, step;
            switch (_phase)
            {
                case 0: exponential = (a1 >> 15) != 0; shift = a1 >> 10 & 31; step = 7 - (a1 >> 8 & 3); decrease = false; break;
                case 1: exponential = true; shift = a1 >> 4 & 15; step = -8; decrease = true; break;
                case 2:
                    exponential = (a2 >> 15) != 0; shift = a2 >> 8 & 31; decrease = (a2 >> 14 & 1) != 0;
                    step = decrease ? -8 + (a2 >> 6 & 3) : 7 - (a2 >> 6 & 3);
                    break;
                default: exponential = (a2 >> 5 & 1) != 0; shift = a2 & 31; step = -8; decrease = true; break;
            }
            int cycles = 1 << Math.Max(0, shift - 11), delta = step << Math.Max(0, 11 - shift);
            if (exponential && !decrease && Level > 0x6000) cycles *= 4;
            if (exponential && decrease) delta = delta * Level >> 15;
            if (++_counter < cycles) return;
            _counter = 0;
            Level = Math.Clamp(Level + delta, 0, 0x7FFF);
            if (_phase == 0 && Level >= 0x7FFF) _phase = 1;
            else if (_phase == 1 && Level <= ((a1 & 15) + 1) * 0x800) _phase = 2;
            else if (_phase == 3 && Level <= 0) Dead = true;
        }
    }

    /// <summary>How long one pass of the sequence lasts at its own tempo.</summary>
    public static double PassSeconds(Sequence sequence)
    {
        var events = Parse(sequence, out long end);
        return Seconds(events, sequence, end);
    }

    /// <summary>Renders the song as a seamless loop at <paramref name="rate"/> Hz: <paramref name="passes"/> passes of the
    /// sequence, each exactly <paramref name="framesPerPass"/> frames (the tempo is adjusted by the tiny amount that
    /// takes). A warm-up pass is rendered first and dropped, so the start already carries the notes still ringing from
    /// the pass before, as it does when the game loops back to it. Returns interleaved stereo.</summary>
    public static short[] RenderLoop(Instruments instruments, Sequence sequence, int rate, int passes, int framesPerPass)
    {
        var events = Parse(sequence, out long ticksPerPass);
        int frames = framesPerPass;
        double scale = frames / (Seconds(events, sequence, ticksPerPass) * rate);
        var (left, right) = Mix(instruments, sequence, events, ticksPerPass, passes + 1, rate, scale);
        int total = frames * passes;
        float[] l = new float[total], r = new float[total];
        Array.Copy(left, frames, l, 0, Math.Min(total, left.Length - frames));
        Array.Copy(right, frames, r, 0, Math.Min(total, right.Length - frames));
        return Master(l, r, rate);
    }

    private sealed record Event(long Tick, int Order, byte[] Data);

    private static List<Event> Parse(Sequence sequence, out long endTick)
    {
        List<Event> events = [];
        var e = sequence.Events; int i = 0; byte running = 0; long tick = 0;
        int Vlq() { int v = 0; while (i < e.Length) { byte b = e[i++]; v = v << 7 | b & 127; if (b < 128) break; } return v; }
        while (i < e.Length)
        {
            tick += Vlq();
            if (i >= e.Length) break;
            byte status = e[i];
            if (status >= 0x80) { i++; if (status < 0xF0) running = status; } else status = running;
            if (status == 0xFF)
            {
                byte type = e[i++];
                if (type == 0x2F) break;
                int length = type == 0x51 ? 3 : e[i++];
                events.Add(new(tick, 0, [0xFF, type, .. e.AsSpan(i, Math.Min(length, e.Length - i))]));
                i += length;
                continue;
            }
            if (status < 0x80) throw new InvalidDataException("The song's note data is damaged.");
            int argc = (status & 0xF0) is 0xC0 or 0xD0 ? 1 : 2;
            byte[] data = [status, .. e.AsSpan(i, Math.Min(argc, e.Length - i))];
            i += argc;
            bool on = (status & 0xF0) == 0x90 && data.Length > 2 && data[2] > 0, off = (status & 0xF0) == 0x80 || (status & 0xF0) == 0x90 && !on;
            // Within one tick: instrument and controller changes, then releases, then new notes (the songs set a
            // channel's instrument on the same tick as its first note, after it).
            events.Add(new(tick, on ? 2 : off ? 1 : 0, data));
        }
        endTick = tick;
        return [.. events.OrderBy(x => x.Tick).ThenBy(x => x.Order)];
    }

    private static double Seconds(List<Event> events, Sequence sequence, long endTick)
    {
        double seconds = 0, tempo = sequence.Tempo; long last = 0;
        foreach (var e in events.Where(e => e.Data[0] == 0xFF && e.Data[1] == 0x51 && e.Data.Length >= 5))
        {
            seconds += (e.Tick - last) * tempo / 1e6 / sequence.Resolution; last = e.Tick;
            tempo = e.Data[2] << 16 | e.Data[3] << 8 | e.Data[4];
        }
        return seconds + (endTick - last) * tempo / 1e6 / sequence.Resolution;
    }

    private static (float[] Left, float[] Right) Mix(Instruments instruments, Sequence sequence, List<Event> events, long ticksPerPass,
        int passes, int rate, double scale)
    {
        List<float> left = [], right = [];
        List<Voice> voices = [];
        int[] program = new int[16], volume = new int[16], expression = new int[16], pan = new int[16];
        double[] bend = new double[16];
        Array.Fill(volume, 127); Array.Fill(expression, 127); Array.Fill(pan, 64);
        double ratio = 44100.0 / rate, envelopeTicks = 0, now = 0, tempo = sequence.Tempo;
        void Emit(double until)
        {
            while (left.Count < until)
            {
                double l = 0, r = 0;
                envelopeTicks += ratio;
                int ticks = (int)envelopeTicks; envelopeTicks -= ticks;
                foreach (var v in voices)
                {
                    if (v.Dead) continue;
                    for (int t = 0; t < ticks; t++) v.Envelope();
                    int p = (int)v.Position;
                    if (p >= v.Pcm.Length - 1)
                    {
                        if (!v.Looped || v.LoopStart >= v.Pcm.Length - 1) { v.Dead = true; continue; }
                        v.Position = v.LoopStart + (v.Position - p); p = (int)v.Position;
                    }
                    double fraction = v.Position - p, s = (v.Pcm[p] * (1 - fraction) + v.Pcm[p + 1] * fraction) * (v.Level / 32767.0);
                    l += s * v.Left; r += s * v.Right;
                    v.Position += v.Step;
                }
                left.Add((float)l); right.Add((float)r);
            }
            voices.RemoveAll(v => v.Dead);
        }
        for (int pass = 0; pass < passes; pass++)
        {
            long last = 0;
            tempo = sequence.Tempo;
            foreach (var e in events)
            {
                now += (e.Tick - last) * tempo / 1e6 * rate / sequence.Resolution * scale; last = e.Tick;
                Emit(now);
                byte status = e.Data[0];
                if (status == 0xFF)
                {
                    if (e.Data[1] == 0x51 && e.Data.Length >= 5) tempo = e.Data[2] << 16 | e.Data[3] << 8 | e.Data[4];
                    continue;
                }
                int ch = status & 15;
                switch (e.Order, status & 0xF0)
                {
                    case (2, _):
                    {
                        if (!instruments.Programs.TryGetValue(program[ch], out var prog)) break;
                        foreach (var tone in prog.Tones.Where(t => e.Data[1] >= t.Min && e.Data[1] <= t.Max))
                        {
                            if (tone.Vag <= 0 || tone.Vag >= instruments.Samples.Length || instruments.Samples[tone.Vag].Length < 2) continue;
                            // The VAB's fine tune (1/128 semitone) raises the pitch: measured against the samples' own pitch.
                            double semitones = e.Data[1] - tone.Center + tone.Shift / 128.0 + bend[ch] * tone.BendRange;
                            double gain = e.Data[2] / 127.0 * tone.Volume / 127.0 * prog.Volume / 127.0 * instruments.MasterVolume / 127.0 *
                                volume[ch] / 127.0 * expression[ch] / 127.0;
                            double angle = Math.Clamp(tone.Pan + pan[ch] - 64, 0, 127) / 127.0 * Math.PI / 2;
                            voices.Add(new Voice
                            {
                                Channel = ch, Note = e.Data[1], Tone = tone, Pcm = instruments.Samples[tone.Vag],
                                LoopStart = instruments.LoopStart[tone.Vag], Looped = instruments.Looped[tone.Vag],
                                Step = Math.Min(4, Math.Pow(2, semitones / 12)) * ratio, Left = gain * Math.Cos(angle), Right = gain * Math.Sin(angle)
                            });
                        }
                        break;
                    }
                    case (1, _):
                        foreach (var v in voices.Where(v => v.Channel == ch && v.Note == e.Data[1])) v.KeyOff();
                        break;
                    case (_, 0xB0):
                        if (e.Data[1] == 7) volume[ch] = e.Data[2];
                        else if (e.Data[1] == 11) expression[ch] = e.Data[2];
                        else if (e.Data[1] == 10) pan[ch] = e.Data[2];
                        break;
                    case (_, 0xC0): program[ch] = e.Data[1]; break;
                    case (_, 0xE0) when e.Data.Length > 2:
                        bend[ch] = ((e.Data[2] << 7 | e.Data[1]) - 8192) / 8192.0;
                        foreach (var v in voices.Where(v => v.Channel == ch))
                            v.Step = Math.Min(4, Math.Pow(2, (v.Note - v.Tone.Center + v.Tone.Shift / 128.0 + bend[ch] * v.Tone.BendRange) / 12)) * ratio;
                        break;
                }
            }
            now += (ticksPerPass - last) * tempo / 1e6 * rate / sequence.Resolution * scale;
            Emit(now);
        }
        // Let the last notes ring out.
        foreach (var v in voices) v.KeyOff();
        Emit(left.Count + rate * 2);
        return ([.. left], [.. right]);
    }

    /// <summary>Room reverb (the game plays its music through the SPU's reverb), then a level matching Spyro's own
    /// music: a look-ahead limiter keeps the drum peaks inside 16 bits while the song is brought up to Spyro's loudness.</summary>
    private static short[] Master(float[] l, float[] r, int rate)
    {
        int n = l.Length;
        float[] wetL = new float[n], wetR = new float[n];
        foreach (int delay44 in new[] { 1557, 1617, 1491, 1422 })
        {
            int d = delay44 * rate / 44100;
            for (int k = 0; k < n; k++)
            {
                int j = (k - d + n) % n;   // the loop wraps, so the reverb does too
                wetL[k] += 0.25f * (l[j] + 0.6f * wetL[j]); wetR[k] += 0.25f * (r[j] + 0.6f * wetR[j]);
            }
        }
        for (int k = 0; k < n; k++) { l[k] += 0.25f * wetL[k]; r[k] += 0.25f * wetR[k]; }
        double power = 0;
        for (int k = 0; k < n; k++) power += (double)l[k] * l[k] + (double)r[k] * r[k];
        double rms = Math.Sqrt(power / Math.Max(1, 2 * n));
        float gain = rms > 0 ? (float)(TargetRms / rms) : 1;
        // Gain each sample needs to stay under the ceiling; the minimum over a short look-ahead, averaged over the same
        // span (so the gain glides down and reaches the needed value exactly at the peak), then a slow release.
        const float Ceiling = 30000;
        int span = Math.Max(1, rate * 3 / 1000);
        float[] need = new float[n];
        for (int k = 0; k < n; k++) need[k] = Math.Min(1f, Ceiling / Math.Max(1e-3f, gain * Math.Max(Math.Abs(l[k]), Math.Abs(r[k]))));
        float[] ahead = new float[n];
        int[] queue = new int[n]; int head = 0, tail = 0;   // indices, need increasing from head to tail
        for (int k = n - 1; k >= 0; k--)
        {
            while (tail > head && need[queue[tail - 1]] >= need[k]) tail--;
            queue[tail++] = k;
            while (queue[head] > k + span - 1) head++;
            ahead[k] = need[queue[head]];
        }
        short[] pcm = new short[2 * n];
        float release = 1 - (float)Math.Exp(-1.0 / (rate * 0.08)), current = 1;
        double sum = 0;
        for (int k = 0; k < n; k++)
        {
            sum += ahead[k] - (k >= span ? ahead[k - span] : 1);
            float smooth = (float)Math.Min(1, (sum + span) / span);
            current = smooth < current ? smooth : current + (smooth - current) * release;
            float g = gain * current;
            pcm[2 * k] = (short)Math.Clamp(l[k] * g, -32767, 32767);
            pcm[2 * k + 1] = (short)Math.Clamp(r[k] * g, -32767, 32767);
        }
        return pcm;
    }

    /// <summary>Spyro's level music averages about this RMS (Artisans 4600, Dark Hollow 6500, Town Square 9000).</summary>
    public const double TargetRms = 6000;
}
