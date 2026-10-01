using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.Core.Exporting;

internal static class NativeFreshTreasureTarget
{
    internal const int Offset = 0x5FC7E;
    internal static void RequireZeroBaseline(byte[] original, byte[] staged)
    {
        NativeId65FreshExecutablePlan.RequireOriginal(original);
        UniversalTreasureAccounting.RequireConsumer(staged);
        if (Convert.ToHexStringLower(SHA256.HashData(original.AsSpan(0x5FC38,0x48))) !=
            "641867a8c3d0c8af833f4ce678cd27ff7520e7ddafa4cecfd19c7a388f625ee6" ||
            BinaryPrimitives.ReadUInt16LittleEndian(original.AsSpan(Offset)) != 0 ||
            BinaryPrimitives.ReadUInt16LittleEndian(staged.AsSpan(Offset)) != 0)
            throw new InvalidDataException("Fresh treasure composition requires its source-owned zero slot35 baseline; a staged target conflict cannot be reset.");
    }
}
