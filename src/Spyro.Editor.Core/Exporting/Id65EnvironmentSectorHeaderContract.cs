namespace Spyro.Editor.Core.Exporting;

internal sealed record Id65EnvironmentSectorHeaderReadback(
    int LowDetailVertexCount,
    int LowDetailColorCount,
    int LowDetailFaceCount,
    int HighDetailVertexCount,
    int HighDetailColorCount,
    int HighDetailFaceCount,
    int HighDetailVertexWordOffset,
    int ExactSectorByteLength,
    int PointerSpanByteLength,
    int PointerGapByteLength);

/// <summary>
/// Owns the two non-count invariants in a Spyro 1 environment-sector header.
/// Writers stamp the header only after all six counts and all tables are final;
/// readers independently derive the HP-table word offset and exact table end.
/// </summary>
internal static class Id65EnvironmentSectorHeaderContract
{
    internal const int HeaderByteLength = 28;
    internal const int LowDetailVertexCountOffset = 0x10;
    internal const int LowDetailColorCountOffset = 0x11;
    internal const int LowDetailFaceCountOffset = 0x12;
    internal const int ReservedOffset = 0x13;
    internal const int HighDetailVertexCountOffset = 0x14;
    internal const int HighDetailColorCountOffset = 0x15;
    internal const int HighDetailFaceCountOffset = 0x16;
    internal const int HighDetailVertexWordOffsetOffset = 0x17;

    internal static void Stamp(Span<byte> sector)
    {
        if (sector.Length < HeaderByteLength)
            throw new InvalidDataException("The environment sector header is truncated before stamping.");

        int hpVertexWordOffset = checked(
            sector[LowDetailVertexCountOffset] +
            sector[LowDetailColorCountOffset] +
            (2 * sector[LowDetailFaceCountOffset]));
        if (hpVertexWordOffset > byte.MaxValue)
            throw new InvalidDataException("The environment sector LP tables exceed the one-byte HP-table offset.");

        sector[ReservedOffset] = 0;
        sector[HighDetailVertexWordOffsetOffset] = checked((byte)hpVertexWordOffset);
    }

    internal static Id65EnvironmentSectorHeaderReadback ReadExactSector(
        ReadOnlySpan<byte> sector,
        string label) =>
        ReadPointerSpan(sector, sector.Length, requireExactEnd: true, label);

    internal static Id65EnvironmentSectorHeaderReadback ReadPointerSpan(
        ReadOnlySpan<byte> sectorAndGap,
        int pointerSpanByteLength,
        bool requireExactEnd,
        string label)
    {
        if (string.IsNullOrWhiteSpace(label))
            throw new ArgumentException("A non-empty environment-sector label is required.", nameof(label));
        if (pointerSpanByteLength < 0 || pointerSpanByteLength > sectorAndGap.Length)
            throw new InvalidDataException($"The {label} pointer span is outside its environment bounds.");
        if (pointerSpanByteLength < HeaderByteLength)
            throw new InvalidDataException($"The {label} header is truncated by its pointer span.");

        ReadOnlySpan<byte> pointerSpan = sectorAndGap[..pointerSpanByteLength];
        int lpVertices = pointerSpan[LowDetailVertexCountOffset];
        int lpColors = pointerSpan[LowDetailColorCountOffset];
        int lpFaces = pointerSpan[LowDetailFaceCountOffset];
        int hpVertices = pointerSpan[HighDetailVertexCountOffset];
        int hpColors = pointerSpan[HighDetailColorCountOffset];
        int hpFaces = pointerSpan[HighDetailFaceCountOffset];

        if (pointerSpan[ReservedOffset] != 0)
            throw new InvalidDataException($"The {label} reserved header byte +0x13 is not zero.");

        int independentlyDerivedHpWordOffset = checked(lpVertices + lpColors + (2 * lpFaces));
        if (independentlyDerivedHpWordOffset > byte.MaxValue)
            throw new InvalidDataException($"The {label} LP tables overflow the one-byte HP-table offset.");
        int storedHpWordOffset = pointerSpan[HighDetailVertexWordOffsetOffset];
        if (storedHpWordOffset != independentlyDerivedHpWordOffset)
        {
            throw new InvalidDataException(
                $"The {label} HP-table offset +0x17 is {storedHpWordOffset}, expected {independentlyDerivedHpWordOffset}.");
        }

        int hpTableStartFromStoredHeader = checked(HeaderByteLength + (4 * storedHpWordOffset));
        int hpTableStartFromCounts = checked(
            HeaderByteLength + (4 * lpVertices) + (4 * lpColors) + (8 * lpFaces));
        if (hpTableStartFromStoredHeader != hpTableStartFromCounts)
            throw new InvalidDataException($"The {label} HP-table start does not match its LP table partition.");

        int exactSectorByteLength = checked(
            hpTableStartFromCounts + (4 * hpVertices) + (8 * hpColors) + (16 * hpFaces));
        if (exactSectorByteLength > pointerSpanByteLength)
        {
            throw new InvalidDataException(
                $"The {label} tables are truncated: need {exactSectorByteLength} bytes, pointer span has {pointerSpanByteLength}.");
        }

        int gapByteLength = pointerSpanByteLength - exactSectorByteLength;
        if (gapByteLength > 0 && pointerSpan[exactSectorByteLength..].IndexOfAnyExcept((byte)0) >= 0)
            throw new InvalidDataException($"The {label} pointer gap contains nonzero bytes.");
        if (requireExactEnd && gapByteLength != 0)
            throw new InvalidDataException($"The {label} tables do not end at the exact pointer boundary.");

        return new(
            lpVertices,
            lpColors,
            lpFaces,
            hpVertices,
            hpColors,
            hpFaces,
            storedHpWordOffset,
            exactSectorByteLength,
            pointerSpanByteLength,
            gapByteLength);
    }
}
