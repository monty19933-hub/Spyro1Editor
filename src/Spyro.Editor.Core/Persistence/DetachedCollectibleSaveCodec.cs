namespace Spyro.Editor.Core.Persistence;

/// <summary>
/// Immutable detached earned state for exactly three independent ordinary-slot identities.
/// No native card address, buffer, save/reset hook, completion formula or I/O is authorized.
/// </summary>
internal sealed class DetachedCollectibleSave
{
    internal const int OrdinarySlotCount = 3;
    private readonly byte[] bits;
    internal DetachedCollectibleManifest Manifest { get; }
    internal int BytesPerSlot => (Manifest.LifetimeCount + 7) / 8;

    private DetachedCollectibleSave(DetachedCollectibleManifest manifest, ReadOnlySpan<byte> bits)
    { Manifest = manifest; this.bits = bits.ToArray(); }

    /// <summary>Explicit new detached state only. Never used as recovery from rejected/legacy bytes.</summary>
    internal static DetachedCollectibleSave CreateNew(DetachedCollectibleManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return new(manifest, new byte[OrdinarySlotCount * ((manifest.LifetimeCount + 7) / 8)]);
    }

    internal bool IsEarned(int ordinarySlot, Guid instanceId)
    {
        RequireSlot(ordinarySlot);
        int index = Manifest.GetEntry(instanceId).BitIndex;
        return (bits[ordinarySlot * BytesPerSlot + index / 8] & (1 << (index % 8))) != 0;
    }

    internal DetachedCollectibleSave WithCollected(int ordinarySlot, Guid instanceId)
    {
        RequireSlot(ordinarySlot);
        DetachedCollectibleManifest.Entry entry = Manifest.GetEntry(instanceId);
        if (entry.Retired) throw new InvalidDataException("A retired identity cannot be newly collected.");
        byte[] next = bits.ToArray();
        next[ordinarySlot * BytesPerSlot + entry.BitIndex / 8] |= (byte)(1 << (entry.BitIndex % 8));
        return new(Manifest, next);
    }

    internal IReadOnlyList<Guid> EarnedActiveInstances(int ordinarySlot)
    {
        RequireSlot(ordinarySlot);
        return Array.AsReadOnly(Manifest.Entries.Where(entry => !entry.Retired && IsEarned(ordinarySlot, entry.InstanceId))
            .Select(entry => entry.InstanceId).ToArray());
    }

    /// <summary>Explicit direct-successor migration; retired earned bits are retained, new bits are zero.</summary>
    internal DetachedCollectibleSave MigrateTo(DetachedCollectibleManifest successor)
    {
        ArgumentNullException.ThrowIfNull(successor);
        successor.RequireDirectSuccessorOf(Manifest);
        int nextStride = (successor.LifetimeCount + 7) / 8;
        var next = new byte[OrdinarySlotCount * nextStride];
        for (int slot = 0; slot < OrdinarySlotCount; slot++)
            bits.AsSpan(slot * BytesPerSlot, BytesPerSlot).CopyTo(next.AsSpan(slot * nextStride, BytesPerSlot));
        return new(successor, next);
    }

    internal byte[] CopySlotBits(int ordinarySlot)
    {
        RequireSlot(ordinarySlot);
        return bits.AsSpan(ordinarySlot * BytesPerSlot, BytesPerSlot).ToArray();
    }

    internal static DetachedCollectibleSave FromValidatedBits(DetachedCollectibleManifest manifest, ReadOnlySpan<byte> bytes)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        int stride = (manifest.LifetimeCount + 7) / 8;
        if (bytes.Length != OrdinarySlotCount * stride) throw new InvalidDataException("Detached slot bitset length does not match its manifest.");
        int remainder = manifest.LifetimeCount % 8;
        if (remainder != 0)
            for (int slot = 0; slot < OrdinarySlotCount; slot++)
                if ((bytes[(slot + 1) * stride - 1] & ~((1 << remainder) - 1)) != 0)
                    throw new InvalidDataException("Unassigned collectible bits must be zero, not silently discarded.");
        return new(manifest, bytes);
    }

    private static void RequireSlot(int slot)
    {
        if (slot < 0 || slot >= OrdinarySlotCount) throw new ArgumentOutOfRangeException(nameof(slot), "Only ordinary slots 0, 1 and 2 exist in this detached format.");
    }
}

internal enum DetachedCollectibleReadStatus { Compatible, UnknownOrLegacy, UnsupportedVersion, CorruptOrMalformed, ManifestMismatch }

/// <summary>A refusal owns an exact opaque copy, never an empty replacement state or an automatic rewrite.</summary>
internal sealed class DetachedCollectibleReadResult
{
    private readonly byte[] original;
    internal DetachedCollectibleReadResult(byte[] original, DetachedCollectibleReadStatus status, string detail, DetachedCollectibleSave? state)
    { this.original = original.ToArray(); Status = status; Detail = detail; State = state; OriginalSha256 = DetachedCollectibleBinary.Hash(original); }
    internal DetachedCollectibleReadStatus Status { get; }
    internal string Detail { get; }
    internal DetachedCollectibleSave? State { get; }
    internal string OriginalSha256 { get; }
    internal byte[] CopyOriginalBytes() => original.ToArray();
    internal DetachedCollectibleSave RequireCompatible() => State ?? throw new InvalidDataException(Detail);
}

internal static class DetachedCollectibleSaveCodec
{
    internal const ushort FormatVersion = 1;
    internal const int HeaderBytes = 96;
    internal const int MaximumEncodedBytes = HeaderBytes + 3 * (DetachedCollectibleManifest.MaximumLifetimeInstances / 8) + 32;
    private static ReadOnlySpan<byte> Magic => "SPYCSAV\0"u8;

    internal static byte[] Serialize(DetachedCollectibleSave state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var bytes = new byte[HeaderBytes + DetachedCollectibleSave.OrdinarySlotCount * state.BytesPerSlot + 32];
        Magic.CopyTo(bytes);
        DetachedCollectibleBinary.Put16(bytes, 8, FormatVersion);
        DetachedCollectibleBinary.Put16(bytes, 0xA, HeaderBytes);
        DetachedCollectibleBinary.Put32(bytes, 0xC, (uint)bytes.Length);
        DetachedCollectibleBinary.PutGuid(bytes, 0x10, state.Manifest.ContentId);
        DetachedCollectibleBinary.Put32(bytes, 0x20, state.Manifest.Revision);
        DetachedCollectibleBinary.Put32(bytes, 0x24, (uint)state.Manifest.LifetimeCount);
        DetachedCollectibleBinary.Put16(bytes, 0x28, DetachedCollectibleSave.OrdinarySlotCount);
        DetachedCollectibleBinary.Put16(bytes, 0x2A, (ushort)state.BytesPerSlot);
        Convert.FromHexString(state.Manifest.Sha256).CopyTo(bytes, 0x30);
        for (int slot = 0; slot < DetachedCollectibleSave.OrdinarySlotCount; slot++)
            state.CopySlotBits(slot).CopyTo(bytes, HeaderBytes + slot * state.BytesPerSlot);
        DetachedCollectibleBinary.WriteChecksum(bytes);
        return bytes;
    }

    /// <summary>
    /// Exact detached payload only; trailing card bytes/padding are not implicitly accepted.
    /// Unknown/legacy, future-version, damaged and other-manifest bytes are preserved as opaque data.
    /// </summary>
    internal static DetachedCollectibleReadResult Inspect(ReadOnlySpan<byte> input, DetachedCollectibleManifest expectedManifest)
    {
        ArgumentNullException.ThrowIfNull(expectedManifest);
        byte[] bytes = input.ToArray();
        DetachedCollectibleReadResult Refuse(DetachedCollectibleReadStatus status, string detail) => new(bytes, status, detail, null);
        if (bytes.Length < 8 || !bytes.AsSpan(0, 8).SequenceEqual(Magic))
            return Refuse(DetachedCollectibleReadStatus.UnknownOrLegacy, "Unrecognized/legacy bytes are retained; explicit new-state creation is a separate decision.");
        if (bytes.Length < 10) return Refuse(DetachedCollectibleReadStatus.CorruptOrMalformed, "Recognized detached save header is truncated.");
        if (DetachedCollectibleBinary.U16(bytes, 8) != FormatVersion)
            return Refuse(DetachedCollectibleReadStatus.UnsupportedVersion, "Detached save version is unsupported; original bytes must be preserved.");
        if (bytes.Length < HeaderBytes + 32 || bytes.Length > MaximumEncodedBytes)
            return Refuse(DetachedCollectibleReadStatus.CorruptOrMalformed, "Detached save length is outside this bounded format.");
        uint count = DetachedCollectibleBinary.U32(bytes, 0x24);
        int stride = DetachedCollectibleBinary.U16(bytes, 0x2A);
        if (DetachedCollectibleBinary.U16(bytes, 0xA) != HeaderBytes || DetachedCollectibleBinary.U32(bytes, 0xC) != bytes.Length ||
            count > DetachedCollectibleManifest.MaximumLifetimeInstances || stride != (count + 7) / 8 ||
            DetachedCollectibleBinary.U16(bytes, 0x28) != DetachedCollectibleSave.OrdinarySlotCount ||
            bytes.Length != HeaderBytes + DetachedCollectibleSave.OrdinarySlotCount * stride + 32 ||
            !DetachedCollectibleBinary.IsZero(bytes.AsSpan(0x2C, 4)) || !DetachedCollectibleBinary.IsZero(bytes.AsSpan(0x50, 16)))
            return Refuse(DetachedCollectibleReadStatus.CorruptOrMalformed, "Detached save lengths, slot ownership or reserved fields are invalid.");
        try { DetachedCollectibleBinary.RequireChecksum(bytes); }
        catch (InvalidDataException error) { return Refuse(DetachedCollectibleReadStatus.CorruptOrMalformed, error.Message); }
        if (new Guid(bytes.AsSpan(0x10, 16), bigEndian: true) != expectedManifest.ContentId ||
            DetachedCollectibleBinary.U32(bytes, 0x20) != expectedManifest.Revision || count != expectedManifest.LifetimeCount ||
            !bytes.AsSpan(0x30, 32).SequenceEqual(Convert.FromHexString(expectedManifest.Sha256)))
            return Refuse(DetachedCollectibleReadStatus.ManifestMismatch, "Detached save belongs to a different content identity or manifest revision; explicit predecessor migration is required.");
        try
        {
            var state = DetachedCollectibleSave.FromValidatedBits(expectedManifest, bytes.AsSpan(HeaderBytes, 3 * stride));
            return new(bytes, DetachedCollectibleReadStatus.Compatible, "Exact detached manifest and integrity checks passed; no native storage ownership is implied.", state);
        }
        catch (InvalidDataException error) { return Refuse(DetachedCollectibleReadStatus.CorruptOrMalformed, error.Message); }
    }
}
