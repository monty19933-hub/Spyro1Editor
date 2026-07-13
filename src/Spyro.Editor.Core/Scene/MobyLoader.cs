using System.Text.Json;
using Spyro.Editor.Core;
using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Scene;

public static class MobyLoader
{
    public const int RuntimeRecordStride = 0x58;

    public static IReadOnlyList<Moby> LoadRamDump(string ramPath)
    {
        byte[] ram = File.ReadAllBytes(ramPath);
        List<Moby> result = new();
        if (ram.Length < 0x7582C)
            return result;

        uint pointer = BitConverter.ToUInt32(ram, 0x75828);
        uint dynamicPointer = BitConverter.ToUInt32(ram, 0x7573C);
        int start = (int)(pointer & 0x001FFFFF);
        int dynamicStart = (int)(dynamicPointer & 0x001FFFFF);
        if (start < 0 || start + RuntimeRecordStride > ram.Length)
            return result;

        int count = 512;
        if (dynamicStart > start && dynamicStart <= ram.Length && ((dynamicStart - start) % RuntimeRecordStride) == 0)
            count = Math.Min(512, (dynamicStart - start) / RuntimeRecordStride);

        for (int i = 0; i < count; i++)
        {
            int offset = start + (i * RuntimeRecordStride);
            if (offset + RuntimeRecordStride > ram.Length)
                break;

            int rawX = BitConverter.ToInt32(ram, offset + 0x0C);
            int rawY = BitConverter.ToInt32(ram, offset + 0x10);
            int rawZ = BitConverter.ToInt32(ram, offset + 0x14);
            int type = ram[offset + 0x50];
            int yawByte = ReadYawByteFromRuntimeRecord(ram, offset);
            string label = Moby.FallbackLabel(type);
            Vector3f position = new(rawX / 16f, rawY / 16f, rawZ / 16f);

            result.Add(new Moby
            {
                Index = i,
                TrueIndex = i,
                LegacyIndex = GetLegacyAliasIndex(i),
                Position = position,
                OriginalPosition = position,
                Type = type,
                OriginalType = type,
                State = ram[offset + 0x51],
                OriginalState = ram[offset + 0x51],
                YawByte = yawByte,
                OriginalYawByte = yawByte,
                RuntimeAddress = 0x80000000u + (uint)offset,
                SpecialDataPointer = BitConverter.ToUInt32(ram, offset + 0x08),
                SourceByte36 = ram[offset + 0x36],
                OriginalSourceByte36 = ram[offset + 0x36],
                SourceByte37 = ram[offset + 0x37],
                OriginalSourceByte37 = ram[offset + 0x37],
                SourceByte4F = ram[offset + 0x4F],
                OriginalSourceByte4F = ram[offset + 0x4F],
                Flag4A = ram[offset + 0x52],
                OriginalFlag4A = ram[offset + 0x52],
                Flag4B = ram[offset + 0x53],
                OriginalFlag4B = ram[offset + 0x53],
                Color = Moby.ColorForType(type),
                Label = label,
                OriginalLabel = label,
                PatchStatus = "loader-table-patchable",
                PatchLead = LoaderTablePatchLead(i)
            });
        }

        return result;
    }

    public static IReadOnlyList<Moby> LoadCached(string cachePath)
    {
        using FileStream stream = File.OpenRead(cachePath);
        using JsonDocument document = JsonDocument.Parse(stream);
        if (!document.RootElement.TryGetProperty("mobys", out JsonElement mobysElement) || mobysElement.ValueKind != JsonValueKind.Array)
            return Array.Empty<Moby>();

        List<Moby> result = new();
        int fallbackIndex = 0;
        foreach (JsonElement item in mobysElement.EnumerateArray())
        {
            int trueIndex = JsonValue.GetInt32(item, "trueIndex", JsonValue.GetInt32(item, "index", fallbackIndex));
            int type = JsonValue.GetInt32(item, "typeHex", JsonValue.GetInt32(item, "type", 0));
            int yawByte = JsonValue.GetInt32(item, "yawByteHex", JsonValue.GetInt32(item, "facingByteHex", -1));
            string label = Moby.FallbackLabel(type);
            Vector3f position = new(
                JsonValue.GetSingle(item, "x"),
                JsonValue.GetSingle(item, "y"),
                JsonValue.GetSingle(item, "z"));

            result.Add(new Moby
            {
                Index = JsonValue.GetInt32(item, "index", trueIndex),
                TrueIndex = trueIndex,
                LegacyIndex = JsonValue.GetInt32(item, "legacyIndex", GetLegacyAliasIndex(trueIndex)),
                Position = position,
                OriginalPosition = position,
                Type = type,
                OriginalType = type,
                State = JsonValue.GetInt32(item, "stateHex", JsonValue.GetInt32(item, "state", 0)),
                OriginalState = JsonValue.GetInt32(item, "stateHex", JsonValue.GetInt32(item, "state", 0)),
                YawByte = yawByte,
                OriginalYawByte = yawByte,
                RuntimeAddress = (uint)JsonValue.GetInt64(item, "runtimeAddress"),
                SpecialDataPointer = (uint)JsonValue.GetInt64(item, "specialDataPointer"),
                SourceByte36 = JsonValue.GetInt32(item, "sourceByte36Hex"),
                OriginalSourceByte36 = JsonValue.GetInt32(item, "sourceByte36Hex"),
                SourceByte37 = JsonValue.GetInt32(item, "sourceByte37Hex"),
                OriginalSourceByte37 = JsonValue.GetInt32(item, "sourceByte37Hex"),
                SourceByte4F = JsonValue.GetInt32(item, "sourceByte4FHex"),
                OriginalSourceByte4F = JsonValue.GetInt32(item, "sourceByte4FHex"),
                Flag4A = JsonValue.GetInt32(item, "flag4AHex"),
                OriginalFlag4A = JsonValue.GetInt32(item, "flag4AHex"),
                Flag4B = JsonValue.GetInt32(item, "flag4BHex"),
                OriginalFlag4B = JsonValue.GetInt32(item, "flag4BHex"),
                Color = Moby.ColorForType(type),
                Label = label,
                OriginalLabel = label,
                PatchStatus = JsonValue.GetString(item, "patchStatus", "portable-cache"),
                PatchLead = JsonValue.GetString(item, "patchLead", "Loaded from editor-cache; source patch status is applied after level load.")
            });
            fallbackIndex++;
        }

        return result;
    }

    public static int GetLegacyAliasIndex(int trueIndex)
    {
        int numerator = (trueIndex * RuntimeRecordStride) + 8;
        return numerator >= 0 && (numerator % 0x50) == 0 ? numerator / 0x50 : -1;
    }

    public static bool TryGetLoaderTableTrueIndex(int legacyIndex, out int trueIndex)
    {
        int numerator = (legacyIndex * 0x50) - 8;
        if (numerator >= 0 && (numerator % RuntimeRecordStride) == 0)
        {
            trueIndex = numerator / RuntimeRecordStride;
            return true;
        }

        trueIndex = -1;
        return false;
    }

    public static string LoaderTablePatchLead(int trueIndex)
    {
        return $"WAD entry 12, true record {trueIndex}, XYZ +0x0C/+0x10/+0x14";
    }

    private static int ReadYawByteFromRuntimeRecord(byte[] bytes, int offset)
    {
        if (offset < 0 || offset + RuntimeRecordStride > bytes.Length)
            return -1;

        short cos = BitConverter.ToInt16(bytes, offset + 0x20);
        short sin = BitConverter.ToInt16(bytes, offset + 0x24);
        return Moby.TryMatrixToYawByte(cos, sin, out int yawByte) ? yawByte : -1;
    }
}
