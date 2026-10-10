using System.Buffers.Binary;

namespace Spyro.Editor.Core.Music;

/// <summary>CD-XA ADPCM audio as Spyro's music streams use it: 4-bit, stereo, 37.8 kHz, one 2324-byte Mode 2 Form 2
/// sector holds 18 sound groups of 128 bytes (2016 stereo frames).</summary>
public static class XaAudio
{
    public const int SampleRate = 37800;
    public const int FramesPerSector = 2016;
    public const int SectorDataBytes = 2324;
    private const int GroupBytes = 128, Groups = 18, FramesPerGroup = 112;
    private static readonly int[] K0 = [0, 60, 115, 98], K1 = [0, 0, -52, -55];

    /// <summary>The decoder's memory for one channel: the last two output samples.</summary>
    public struct ChannelState { public int Old, Older; }

    /// <summary>Decodes one sector's 2324 data bytes to interleaved stereo (2016 frames).</summary>
    public static void DecodeSector(ReadOnlySpan<byte> data, ref ChannelState left, ref ChannelState right, Span<short> stereo)
    {
        for (int g = 0; g < Groups; g++)
        {
            var group = data.Slice(g * GroupBytes, GroupBytes);
            for (int unit = 0; unit < 8; unit++)
            {
                ref ChannelState state = ref (unit & 1) == 0 ? ref left : ref right;
                byte parameter = group[4 + unit];
                int shift = Math.Min(parameter & 15, 12), filter = parameter >> 4 & 3;
                int frame = g * FramesPerGroup + (unit >> 1) * 28;
                for (int j = 0; j < 28; j++)
                {
                    int nibble = group[16 + 4 * j + (unit >> 1)] >> ((unit & 1) * 4) & 15;
                    int s = Predict((short)(nibble << 12) >> shift, state, filter);
                    state.Older = state.Old; state.Old = s;
                    stereo[2 * (frame + j) + (unit & 1)] = (short)s;
                }
            }
        }
    }

    /// <summary>Encodes 2016 stereo frames (interleaved) into one sector's 2324 data bytes, choosing for every
    /// 28-sample unit the filter and range that decode closest to the input.</summary>
    public static void EncodeSector(ReadOnlySpan<short> stereo, ref ChannelState left, ref ChannelState right, Span<byte> data)
    {
        data[..SectorDataBytes].Clear();
        Span<int> input = stackalloc int[28];
        Span<byte> best = stackalloc byte[28], trial = stackalloc byte[28];
        for (int g = 0; g < Groups; g++)
        {
            var group = data.Slice(g * GroupBytes, GroupBytes);
            for (int unit = 0; unit < 8; unit++)
            {
                ref ChannelState state = ref (unit & 1) == 0 ? ref left : ref right;
                int frame = g * FramesPerGroup + (unit >> 1) * 28;
                for (int j = 0; j < 28; j++) input[j] = stereo[2 * (frame + j) + (unit & 1)];
                long bestError = long.MaxValue; byte bestParameter = 0; ChannelState bestState = state;
                for (int filter = 0; filter < 4; filter++)
                    for (int shift = 0; shift <= 12; shift++)
                    {
                        ChannelState s = state;
                        long error = 0;
                        for (int j = 0; j < 28 && error < bestError; j++)
                        {
                            int predicted = Predict(0, s, filter);
                            // Round the remainder to the nearest step the nibble can express at this range.
                            int step = 1 << (12 - shift);
                            int nibble = Math.Clamp((int)Math.Round((input[j] - predicted) / (double)step, MidpointRounding.AwayFromZero), -8, 7);
                            int decoded = Predict((short)(nibble << 12) >> shift, s, filter);
                            s.Older = s.Old; s.Old = decoded;
                            long d = decoded - input[j];
                            error += d * d;
                            trial[j] = (byte)(nibble & 15);
                        }
                        if (error >= bestError) continue;
                        bestError = error; bestParameter = (byte)(filter << 4 | shift); bestState = s;
                        trial.CopyTo(best);
                    }
                state = bestState;
                group[4 + unit] = bestParameter;
                for (int j = 0; j < 28; j++) group[16 + 4 * j + (unit >> 1)] |= (byte)(best[j] << ((unit & 1) * 4));
            }
            group.Slice(4, 4).CopyTo(group);          // bytes 0-3 repeat 4-7, bytes 12-15 repeat 8-11
            group.Slice(8, 4).CopyTo(group[12..]);
        }
    }

    private static int Predict(int sample, ChannelState state, int filter) =>
        Math.Clamp(sample + ((state.Old * K0[filter] + state.Older * K1[filter] + 32) >> 6), -32768, 32767);

    private static readonly uint[] EdcTable = BuildEdcTable();
    private static uint[] BuildEdcTable()
    {
        uint[] table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint e = i;
            for (int k = 0; k < 8; k++) e = (e >> 1) ^ ((e & 1) != 0 ? 0xD8018001u : 0);
            table[i] = e;
        }
        return table;
    }

    /// <summary>The error-detection code of a raw Mode 2 Form 2 sector (over the subheader and data).</summary>
    public static uint Form2Edc(ReadOnlySpan<byte> rawSector)
    {
        uint edc = 0;
        foreach (byte b in rawSector.Slice(16, 8 + SectorDataBytes)) edc = (edc >> 8) ^ EdcTable[(edc ^ b) & 0xFF];
        return edc;
    }

    /// <summary>Writes new audio into a raw (2352-byte) XA sector, keeping its header and subheader, and its EDC (Spyro's
    /// disc has one on every audio sector).</summary>
    public static void ReplaceSectorAudio(Span<byte> rawSector, ReadOnlySpan<byte> data)
    {
        data[..SectorDataBytes].CopyTo(rawSector.Slice(24, SectorDataBytes));
        BinaryPrimitives.WriteUInt32LittleEndian(rawSector[(24 + SectorDataBytes)..], Form2Edc(rawSector));
    }
}
