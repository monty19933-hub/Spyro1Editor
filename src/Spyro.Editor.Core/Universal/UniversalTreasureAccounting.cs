using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Runtime.CompilerServices;
using Spyro.Editor.Core.Scene;

[assembly: InternalsVisibleTo("Spyro.Editor.UniversalTreasureSmoke")]

namespace Spyro.Editor.Core.Universal;

/// <summary>Known initial treasure only: canonical visible gems and the existing
/// one-gem native reward selector. Random/compound drops are not invented totals.</summary>
internal static class UniversalTreasureAccounting
{
    internal const int ConsumerOffset = 0xC8CC;
    internal const int ConsumerLength = 0x34;
    internal const string ConsumerSha256 = "f9504fc22bd7daca311dafd8843738ccda340eac012b575f115fe1e26a930b58";

    internal static int KnownValue(byte[] sourceSemanticRow)
    {
        if (sourceSemanticRow.Length != UniversalLevelData.RowStride)
            throw new InvalidDataException("Treasure accounting requires a complete native source-semantic actor row.");
        int sourceClass = BinaryPrimitives.ReadUInt16LittleEndian(sourceSemanticRow.AsSpan(0x36));
        // A visible gem owns its value encoding. Its unrelated reward byte must
        // not become a second interpretation when an authored kind/value changes.
        if (GemValue.TryFromIdByte(sourceClass, out _))
            return sourceSemanticRow[0x50] == 0x18 && GemValue.TryFromEncoding(sourceClass, sourceSemanticRow[0x4F], out var gem)
                ? gem.Value : 0;
        return GemValue.TryFromIdByte(sourceSemanticRow[0x53], out var reward) ? reward.Value : 0;
    }

    internal static void RequireConsumer(byte[] executable)
    {
        if (executable.Length != 0x66000 ||
            Convert.ToHexStringLower(SHA256.HashData(executable.AsSpan(ConsumerOffset, ConsumerLength))) != ConsumerSha256)
            throw new InvalidDataException("Treasure target accounting requires the certified native inventory target consumer.");
    }

    internal static void ApplyDelta(byte[] executable, int levelId, int delta)
    {
        if (delta == 0) return;
        RequireConsumer(executable);
        int index = checked((levelId / 10 - 1) * 6 + levelId % 10);
        if (levelId is < 10 or > 65 || levelId % 10 > 5 || index is < 0 or > 35)
            throw new InvalidDataException("Treasure target has no owned native level-table slot.");
        int offset = checked((int)(UniversalMobyComposer.TreasureTableAddress - UniversalExeBindings.ExeBase) + index * 2);
        // Certified inventory instruction0xC8E8 is LH a2,-0xBC8(at), not LHU.
        int prior = BinaryPrimitives.ReadInt16LittleEndian(executable.AsSpan(offset));
        int total = checked(prior + delta);
        if (prior < 0 || total is < 0 or > short.MaxValue)
            throw new InvalidDataException("Known added/removed treasure would underflow or overflow the native level target.");
        BinaryPrimitives.WriteUInt16LittleEndian(executable.AsSpan(offset), (ushort)total);
    }
}
