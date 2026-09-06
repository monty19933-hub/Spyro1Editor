using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Spyro.Editor.Core.Analysis;

public sealed class NativeAdpcmSample
{
    private readonly byte[] _bytes;
    internal NativeAdpcmSample(int offset, ReadOnlySpan<byte> bytes)
    {
        BankOffset = offset; _bytes = bytes.ToArray(); Sha256 = NativeSoundBankCodec.Hash(bytes);
        var flags = new byte[bytes.Length / 16];
        for (int i = 0; i < flags.Length; i++) flags[i] = bytes[i * 16 + 1];
        FrameFlags = Array.AsReadOnly(flags);
    }
    public int BankOffset { get; }
    public int ByteLength => _bytes.Length;
    public int FrameCount => _bytes.Length / 16;
    public string Sha256 { get; }
    public IReadOnlyList<byte> FrameFlags { get; }
    public bool IsSimpleOneShot => FrameFlags.Count > 0 && FrameFlags[^1] == 1 && FrameFlags.Take(FrameFlags.Count - 1).All(f => f == 0);
    public byte[] CopyBytes() => _bytes.ToArray();
}

public sealed class NativeSoundDescriptor
{
    private readonly byte[] _bytes;
    internal NativeSoundDescriptor(int index, ReadOnlySpan<byte> bytes, NativeAdpcmSample sample)
    {
        Index = index; _bytes = bytes.ToArray(); Sample = sample; Sha256 = NativeSoundBankCodec.Hash(bytes);
    }
    public int Index { get; }
    public uint SampleOffset => BinaryPrimitives.ReadUInt32LittleEndian(_bytes);
    public ushort Pitch => BinaryPrimitives.ReadUInt16LittleEndian(_bytes.AsSpan(10));
    public string Sha256 { get; }
    public NativeAdpcmSample Sample { get; }
    public bool IsSupportedOneShot => Sample.IsSimpleOneShot && Pitch is > 0 and <= 0x3FFF &&
        BinaryPrimitives.ReadUInt32LittleEndian(_bytes.AsSpan(4)) == 0 &&
        BinaryPrimitives.ReadUInt32LittleEndian(_bytes.AsSpan(12)) == 0 &&
        BinaryPrimitives.ReadUInt32LittleEndian(_bytes.AsSpan(16)) == 0;
    public byte[] CopyDescriptor() => _bytes.ToArray();
}

/// <summary>Source-form mapping/descriptors and bounded ADPCM extents, not playback/voice/reverb authority.</summary>
public sealed class NativeSoundBank
{
    private readonly byte[] _component, _bank;
    internal NativeSoundBank(ReadOnlySpan<byte> component, ReadOnlySpan<byte> bank, IEnumerable<NativeSoundDescriptor> descriptors)
    {
        _component = component.ToArray(); _bank = bank.ToArray();
        Map = Array.AsReadOnly(component.Slice(4, 256).ToArray()); Descriptors = Array.AsReadOnly(descriptors.ToArray());
        ComponentSha256 = NativeSoundBankCodec.Hash(component); BankSha256 = NativeSoundBankCodec.Hash(bank);
    }
    public IReadOnlyList<byte> Map { get; }
    public IReadOnlyList<NativeSoundDescriptor> Descriptors { get; }
    public string ComponentSha256 { get; }
    public string BankSha256 { get; }
    public int BankByteLength => _bank.Length;
    public int ComponentByteLength => _component.Length;
    public byte[] CopyComponent() => _component.ToArray();
    public byte[] CopyBank() => _bank.ToArray();
    public bool RuntimePlaybackProven => false;
}

public static class NativeSoundBankCodec
{
    public const int SpuUploadBase = 0x1010;
    public const int SpuUploadByteLength = 0x7EFF0;
    public const int MaximumDecodedSampleBytes = 0x400000;
    public static NativeSoundBank Decode(ReadOnlySpan<byte> component, ReadOnlySpan<byte> bank)
    {
        if (component.Length < 0x11C || component.Length > 0x1508 ||
            BinaryPrimitives.ReadUInt32LittleEndian(component) != component.Length ||
            bank.Length == 0 || bank.Length > SpuUploadByteLength || bank.Length % 16 != 0)
            throw new InvalidDataException("Native sound component or SPU bank exceeds its bounded owner.");
        uint count = BinaryPrimitives.ReadUInt32LittleEndian(component[0x104..]);
        if (count is 0 or > 256 || component.Length != 0x108 + count * 20)
            throw new InvalidDataException("Native sound component requires exactly 1..256 twenty-byte descriptors.");
        foreach (byte index in component.Slice(4, 256))
            if (index >= count) throw new InvalidDataException("Native global sound map selects an unowned descriptor.");
        Dictionary<int, NativeAdpcmSample> samples = [];
        List<NativeSoundDescriptor> descriptors = [];
        int decodedBytes = 0;
        for (int index = 0; index < count; index++)
        {
            ReadOnlySpan<byte> descriptor = component.Slice(0x108 + index * 20, 20);
            uint rawOffset = BinaryPrimitives.ReadUInt32LittleEndian(descriptor);
            if (rawOffset % 16 != 0 || rawOffset + 16L > bank.Length)
                throw new InvalidDataException("Native sound sample offset is not a bounded aligned SPU-bank-relative reference.");
            int offset = (int)rawOffset;
            if (!samples.TryGetValue(offset, out NativeAdpcmSample? sample))
            {
                int end = offset;
                while (true)
                {
                    if (end + 16L > bank.Length)
                        throw new InvalidDataException("Native ADPCM sample does not terminate within its SPU bank.");
                    byte format = bank[end], flags = bank[end + 1];
                    if ((format >> 4) > 4 || (format & 15) > 12 || flags > 7)
                        throw new InvalidDataException("Native ADPCM frame uses an unsupported predictor, shift or flag encoding.");
                    end += 16;
                    if ((flags & 1) != 0) break;
                }
                int length = end - offset;
                if (length > MaximumDecodedSampleBytes - decodedBytes)
                    throw new InvalidDataException("Native sound unique-sample decoding exceeds its work budget.");
                decodedBytes += length;
                foreach (var existing in samples.Values)
                    if (offset < existing.BankOffset + existing.ByteLength && existing.BankOffset < end)
                        throw new InvalidDataException("Distinct native sample starts partially alias; an explicit sharing profile is required.");
                sample = new(offset, bank.Slice(offset, length)); samples.Add(offset, sample);
            }
            descriptors.Add(new(index, descriptor, sample));
        }
        return new(component, bank, descriptors);
    }
    internal static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
