using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.Core.Universal;

/// <summary>Retains all descriptor indices, global selectors, controls and complete ADPCM payloads.
/// Native DMA already rounds the fixed 0x7EFF0 request to 0x7F000; only its final zero
/// storage frame may lie beyond usable SPU memory. This does not enlarge the upload.</summary>
internal static class UniversalSoundBankStorage
{
    internal const int MaximumStorageBytes = 0x7F000;
    internal static void RequireExecutable(ReadOnlySpan<byte> executable)
    {
        if (executable.Length != 0x66000) throw new InvalidDataException("Sound storage reclamation requires the complete native executable owner.");
        foreach (var (offset, length, hash) in new (int, int, string)[]
        {
            (13552, 104, "ecd95b895b0f1dd85b2aed1c926a6e5e818ac3c41ddaf38565e34459808f89a0"),
            (24240, 560, "f3cdb7634a2e36f2329959401df8bc6006cd623bd2ea7233e4ca43ea53e42c15"),
            (287352, 2092, "d3a63784c8dc509583c63499ccb7ad493ba8bf4af0aaeb4a6bd9fa5aa851a06d"),
            (312308, 660, "a21dba15beee45866129f9f3adeffa8fe95e3eac47fcc0781f1f5d0007b261f9"),
            (314304, 76, "ef132cef57f73b1110a2b080b8f56b633baa023a939816beb1d6d62fed848058"),
            (316100, 96, "434a098a521f8a9fc6d1cf1841b4c9ae1ebd1ff946baf5ca019dba734c25cce3"),
            (316196, 88, "356db920d194b5b0823ce052e38699d4b15b6aa9a3223896dbab4c87a8ef6065"),
            (318260, 60, "f4f9af30ac702db2ebc8bc8b0b22445bb2f1a56040ebe321f1c8422343c56202"),
            (314760, 196, "68b10fb374a70300499c0d27c1d50bdd2ec55f39585fb8e2c0444bdf9112a33a")
        })
            if (Convert.ToHexStringLower(SHA256.HashData(executable.Slice(offset, length))) != hash)
                throw new InvalidDataException("Sound storage descriptor, DMA or disabled-reverb consumer differs from its certified native owner.");
    }

    /// <summary>Unknown padding consumers and external absolute loop addresses are refused.
    /// Original banks own one flags7 guard frame in each gap, then optional zero tail.
    /// Every looping payload must set its own flags4 loop-start before its flags3 end.</summary>
    internal static int Compact(UniversalLevelData destination, NativeLevelSoundLayout layout)
    {
        var sound = layout.Sound;
        byte[] bank = sound.CopyBank(), component = sound.CopyComponent();
        var starts = sound.Descriptors.Select(d => d.Sample).DistinctBy(s => s.BankOffset).OrderBy(s => s.BankOffset).ToArray();
        int cursor = 0;
        foreach (var sample in starts)
        {
            RequireGap(bank.AsSpan(cursor, sample.BankOffset - cursor), false);
            if (sample.FrameFlags[^1] == 3 && !sample.FrameFlags.Any(f => (f & 4) != 0))
                throw new InvalidDataException("Sound storage reclamation requires an internal ADPCM loop-start owner.");
            cursor = sample.BankOffset + sample.ByteLength;
        }
        RequireGap(bank.AsSpan(cursor), true);
        if (sound.Descriptors.Any(d => BinaryPrimitives.ReadUInt32LittleEndian(d.CopyDescriptor().AsSpan(4)) > 1))
            throw new InvalidDataException("Sound storage reclamation refuses an external native loop-address consumer.");

        Dictionary<string, (int Offset, byte[] Bytes)> payloads = [];
        byte[] packed = new byte[bank.Length]; int used = 0;
        foreach (var descriptor in sound.Descriptors)
        {
            byte[] payload = descriptor.Sample.CopyBytes();
            if (!payloads.TryGetValue(descriptor.Sample.Sha256, out var found))
            {
                found = (used, payload); payload.CopyTo(packed, used); used += payload.Length;
                payloads.Add(descriptor.Sample.Sha256, found);
            }
            else if (!found.Bytes.AsSpan().SequenceEqual(payload))
                throw new InvalidDataException("Sound storage sample digest collision.");
            BinaryPrimitives.WriteInt32LittleEndian(component.AsSpan(0x108 + descriptor.Index * 20), found.Offset);
        }
        // Independent decoder checks exact semantic retention before applying the edit.
        var check = NativeSoundBankCodec.Decode(component, packed);
        if (!check.Map.SequenceEqual(sound.Map) || check.Descriptors.Count != sound.Descriptors.Count)
            throw new InvalidDataException("Sound storage packing changed a global sound owner.");
        foreach (var descriptor in sound.Descriptors)
        {
            var changed = check.Descriptors[descriptor.Index];
            if (!changed.CopyDescriptor().AsSpan(4).SequenceEqual(descriptor.CopyDescriptor().AsSpan(4)) ||
                !changed.Sample.CopyBytes().AsSpan().SequenceEqual(descriptor.Sample.CopyBytes()))
                throw new InvalidDataException("Sound storage packing changed retained descriptor controls or audio bytes.");
        }
        packed.CopyTo(destination.Bytes, layout.BankOffset);
        component.CopyTo(destination.Bytes, layout.SoundOffset);
        return used;
    }

    private static void RequireGap(ReadOnlySpan<byte> gap, bool tail)
    {
        if (gap.IsEmpty || tail && gap.IndexOfAnyExcept((byte)0) < 0) return;
        if (gap.Length < 16 || !tail && gap.Length != 16 || (gap[0] >> 4) > 4 || (gap[0] & 15) > 12 || gap[1] != 7 ||
            !(gap.Slice(2, 14).IndexOfAnyExcept((byte)0) < 0 || gap.Slice(2, 14).IndexOfAnyExcept((byte)7) < 0) ||
            gap[16..].IndexOfAnyExcept((byte)0) >= 0)
            throw new InvalidDataException("Sound storage reclamation refuses unmodeled native padding ownership.");
    }
}
