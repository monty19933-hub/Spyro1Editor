using System.Buffers.Binary;
using System.Text.Json;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.Core.Scene;

/// <summary>Per-statue body and shine colour. This changes no shared palette or model.</summary>
public static class DragonStatueColor
{
    public const string EditField = "dragonStatueColor";
    public static bool IsStatue(Moby moby) => moby.SourceByte36 == 0xFA && moby.SourceByte37 == 0;
    public static ColorRgba? Read(JsonElement edit)
    {
        if (!edit.TryGetProperty(EditField, out var value) || value.ValueKind == JsonValueKind.Null) return null;
        if (value.ValueKind != JsonValueKind.String || !ColorRgba.TryParseHex(value.GetString(), out var color))
            throw new InvalidDataException("Dragon colour must use #RRGGBB.");
        return color;
    }
    public static string? Hex(ColorRgba? color) => color is { } c ? $"#{c.R:X2}{c.G:X2}{c.B:X2}" : null;

    public static void Apply(Span<byte> row, ColorRgba color)
    {
        if (row.Length != 0x58 || BinaryPrimitives.ReadUInt16LittleEndian(row[0x36..]) != 0xFA)
            throw new InvalidDataException("A dragon colour can only be applied to a dragon statue.");
        // Keep its draw distance. The installed renderer recognises this RGB888 tag,
        // tints its private draw palette, then restores native type-zero specular shading.
        row[0x4B] |= 0x80;
        uint packed = NativeDragonStatueColors.ColourTag | color.R | (uint)color.G << 8 | (uint)color.B << 16;
        BinaryPrimitives.WriteUInt32LittleEndian(row[0x4C..], packed);
    }
}
