using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace Spyro.Editor.Core.Exporting;

internal sealed record NativeSkyOcclusionPatchPlan(
    int ExecutableLba,
    int ExecutableSize,
    int ExecutableFileOffset,
    long ImageOffset,
    string RuntimeAddress,
    string BeforeHex,
    string AfterHex,
    int ChangedByteCount);

internal static class NativeSkyOcclusionPatcher
{
    private const uint RendererOcclusionGroupLoadAddress = 0x80051F90;
    private const uint ExpectedInstruction = 0x8CC40000; // lw a0, 0(a2)
    private const uint RenderAllSectorsInstruction = 0x2404FFFF; // addiu a0, zero, -1
    private const int PsxExeHeaderBytes = 0x800;

    public static NativeSkyOcclusionPatchPlan BuildPlan(string sourceImagePath, int? outputExecutableLba = null)
    {
        DiscLayout layout = DiscImage.DetectLayout(sourceImagePath);
        using FileStream image = File.Open(sourceImagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        DiscFileRecord executable = DiscImage.FindRootFileRecord(image, layout, IsExecutableName);
        byte[] header = DiscImage.ReadFileBytes(image, layout, executable.Lba, 0, PsxExeHeaderBytes);
        if (header.Length < 0x20 || !Encoding.ASCII.GetString(header, 0, 8).StartsWith("PS-X EXE", StringComparison.Ordinal))
            throw new InvalidDataException("The Spyro executable does not have a valid PS-X EXE header.");

        uint loadAddress = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(0x18, 4));
        uint codeSize = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(0x1C, 4));
        if (RendererOcclusionGroupLoadAddress < loadAddress)
            throw new InvalidDataException("The sky renderer address is below the executable load address.");

        uint codeOffset = RendererOcclusionGroupLoadAddress - loadAddress;
        if (codeOffset + 4 > codeSize)
            throw new InvalidDataException("The sky renderer address is outside the executable code range.");
        int fileOffset = checked(PsxExeHeaderBytes + (int)codeOffset);
        if (fileOffset + 4 > executable.Size)
            throw new InvalidDataException("The sky occlusion instruction is outside the executable file extent.");

        byte[] before = DiscImage.ReadFileBytes(image, layout, executable.Lba, fileOffset, 4);
        uint instruction = BinaryPrimitives.ReadUInt32LittleEndian(before);
        if (instruction is not (ExpectedInstruction or RenderAllSectorsInstruction))
        {
            throw new InvalidDataException(
                $"The selected executable has an unsupported sky renderer instruction at 0x{RendererOcclusionGroupLoadAddress:X8}: 0x{instruction:X8}.");
        }

        byte[] after = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(after, RenderAllSectorsInstruction);
        int finalLba = outputExecutableLba ?? executable.Lba;
        return new NativeSkyOcclusionPatchPlan(
            ExecutableLba: finalLba,
            ExecutableSize: executable.Size,
            ExecutableFileOffset: fileOffset,
            ImageOffset: DiscImage.ConvertFileOffsetToImageOffset(layout, finalLba, fileOffset),
            RuntimeAddress: $"0x{RendererOcclusionGroupLoadAddress:X8}",
            BeforeHex: ToHex(before),
            AfterHex: ToHex(after),
            ChangedByteCount: before.Where((value, index) => value != after[index]).Count());
    }

    public static void Apply(string outputImagePath, NativeSkyOcclusionPatchPlan plan)
    {
        DiscLayout layout = DiscImage.DetectLayout(outputImagePath);
        using FileStream image = File.Open(outputImagePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
        DiscFileRecord executable = DiscImage.FindRootFileRecord(image, layout, IsExecutableName);
        if (executable.Lba != plan.ExecutableLba || executable.Size != plan.ExecutableSize)
            throw new InvalidDataException("The output executable extent does not match the sky occlusion patch plan.");

        byte[] before = DiscImage.ReadFileBytes(image, layout, executable.Lba, plan.ExecutableFileOffset, 4);
        uint instruction = BinaryPrimitives.ReadUInt32LittleEndian(before);
        if (instruction is not (ExpectedInstruction or RenderAllSectorsInstruction))
        {
            throw new InvalidDataException(
                $"The output executable changed before the sky occlusion patch at {plan.RuntimeAddress}: 0x{instruction:X8}.");
        }

        byte[] after = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(after, RenderAllSectorsInstruction);
        DiscImage.WriteFileBytes(image, layout, executable.Lba, plan.ExecutableFileOffset, after);
        image.Flush();

        byte[] readback = DiscImage.ReadFileBytes(image, layout, executable.Lba, plan.ExecutableFileOffset, 4);
        if (!readback.AsSpan().SequenceEqual(after))
            throw new InvalidDataException("The sky occlusion executable patch failed readback verification.");
    }

    private static bool IsExecutableName(string name) =>
        name.StartsWith("SCUS_", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("SLUS_", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("SLES_", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("SCES_", StringComparison.OrdinalIgnoreCase);

    private static string ToHex(byte[] bytes) =>
        string.Join(' ', bytes.Select(value => value.ToString("X2", CultureInfo.InvariantCulture)));
}
