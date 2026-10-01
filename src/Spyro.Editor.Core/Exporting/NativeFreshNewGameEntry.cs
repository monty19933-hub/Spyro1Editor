using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Spyro.Editor.Core.Exporting;

/// <summary>Changes only the two exact USA new-game level immediates. Native save loading remains unchanged.</summary>
public static class NativeFreshNewGameEntry
{
    public static void Apply(byte[] original, byte[] staged, bool startInAuthoredLevel = true)
    {
        if (original.Length != 0x66000 || staged.Length != original.Length ||
            Convert.ToHexStringLower(SHA256.HashData(original)) != "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998")
            throw new InvalidDataException("Authored New Game entry requires the exact original USA executable.");
        Guard(0x2F7C, 0x44, "94b2e1903ccd206e0febf3f770b11c6c3f2a0a4d4eced421d539afe43d0ea9c6", 0x2F80);
        Guard(0x1DC40, 0x114, "5630474d6060b69a1dd2227ff3b6249996d3450ee2dcc63fa402eb352d3b6f01", 0x1DCF0);
        Guard(0x23308, 0x150, "d3ad247f70ad06102b483afadf5d3bcdf8f5939110f6bd3d7475a2216e9a46de", -1);
        Guard(0x49D94, 0x74, "25560fd39de61892a7e7ff1629ccec9da2dd812d02fffd3fe8b8c8e458b986a7", -1);
        foreach (int at in new[] { 0x2F80, 0x1DCF0 })
            BinaryPrimitives.WriteUInt32LittleEndian(staged.AsSpan(at), startInAuthoredLevel ? 0x24020041u : 0x2402000Au);

        void Guard(int start, int length, string hash, int change)
        {
            if (Convert.ToHexStringLower(SHA256.HashData(original.AsSpan(start, length))) != hash)
                throw new InvalidDataException("Authored New Game instruction owner changed.");
            for (int at = start; at < start + length; at += 4)
            {
                uint before = BinaryPrimitives.ReadUInt32LittleEndian(original.AsSpan(at));
                uint current = BinaryPrimitives.ReadUInt32LittleEndian(staged.AsSpan(at));
                if (current != before && !(at == change && before == 0x2402000A && current == 0x24020041))
                    throw new InvalidDataException($"Authored New Game conflicts with an executable owner at 0x{at:X}.");
            }
        }
    }
}
