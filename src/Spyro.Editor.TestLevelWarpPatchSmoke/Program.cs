using System.Globalization;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

string workspaceRoot = ResolveWorkspaceRoot(
    args.FirstOrDefault(argument =>
        !argument.StartsWith("--", StringComparison.Ordinal)));
string sourceImagePath = Path.Combine(
    workspaceRoot,
    "Spyro the Dragon (USA).bin");
Assert(
    File.Exists(sourceImagePath),
    $"Missing exact USA retail source image: {sourceImagePath}");

LevelDefinition treeTops =
    LevelCatalog.Load(workspaceRoot).FindByKey("treetops")
    ?? throw new InvalidOperationException(
        "Tree Tops is missing from the level catalog.");
Assert(
    treeTops.LevelId == 43 &&
    TestLevelWarpPatch.TargetSelectionText(treeTops.LevelId) ==
        "Right, then Triangle",
    "Tree Tops' retail level selector changed.");

string temporaryRoot = Path.Combine(
    Path.GetTempPath(),
    $"spyro-test-level-warp-patch-smoke-{Guid.NewGuid():N}");
Directory.CreateDirectory(temporaryRoot);
try
{
    string candidateImagePath = Path.Combine(
        temporaryRoot,
        "tree-tops-disposable-diagnostic.bin");
    File.Copy(
        sourceImagePath,
        candidateImagePath,
        overwrite: false);

    MobySourcePatch patch =
        TestLevelWarpPatch.ApplyToDisposableDiagnosticImage(
            candidateImagePath,
            treeTops);
    Assert(
        TestLevelWarpPatch.TryGetTargetLevelId(
            patch,
            out int targetLevelId) &&
        targetLevelId == treeTops.LevelId &&
        patch.WadRelativeOffset == "exe:0x8002D834" &&
        patch.BeforeHexPreview == "37 B6 00 08 00 00 00 00" &&
        patch.AfterHexPreview == "1C 06 84 AF 88 06 80 AF" &&
        patch.ByteLength == 8,
        "Standalone diagnostic helper returned an unexpected guarded patch.");

    long patchImageOffset = ParseHexOffset(patch.ImageOffset);
    byte[] expectedBefore = ParseHex(patch.BeforeHexPreview);
    byte[] expectedAfter = ParseHex(patch.AfterHexPreview);
    int differenceCount = 0;
    using (FileStream source = File.OpenRead(sourceImagePath))
    using (FileStream candidate = File.OpenRead(candidateImagePath))
    {
        Assert(
            source.Length == candidate.Length,
            "Standalone diagnostic helper changed the image length.");
        byte[] sourceBuffer = new byte[1024 * 1024];
        byte[] candidateBuffer = new byte[sourceBuffer.Length];
        for (long position = 0; position < source.Length;)
        {
            int count = checked((int)Math.Min(
                sourceBuffer.Length,
                source.Length - position));
            source.ReadExactly(sourceBuffer.AsSpan(0, count));
            candidate.ReadExactly(candidateBuffer.AsSpan(0, count));
            for (int index = 0; index < count; index++)
            {
                long imageOffset = position + index;
                if (sourceBuffer[index] == candidateBuffer[index])
                    continue;

                differenceCount++;
                Assert(
                    imageOffset >= patchImageOffset &&
                    imageOffset < patchImageOffset + patch.ByteLength,
                    $"Standalone diagnostic helper changed byte 0x{imageOffset:X} outside the guarded patch.");
                int patchIndex = checked((int)(
                    imageOffset - patchImageOffset));
                Assert(
                    sourceBuffer[index] == expectedBefore[patchIndex] &&
                    candidateBuffer[index] == expectedAfter[patchIndex],
                    $"Standalone diagnostic helper wrote an unexpected byte at 0x{imageOffset:X}.");
            }

            position += count;
        }
    }
    Assert(
        differenceCount == patch.ByteLength,
        $"Standalone diagnostic helper changed {differenceCount} bytes instead of exactly {patch.ByteLength}.");

    try
    {
        TestLevelWarpPatch.ApplyToDisposableDiagnosticImage(
            candidateImagePath,
            treeTops);
        throw new InvalidOperationException(
            "A second standalone warp application unexpectedly passed.");
    }
    catch (InvalidOperationException exception)
        when (exception.Message.Contains(
            "guarded to the exact verified USA",
            StringComparison.Ordinal))
    {
        // Expected: after one write, the executable no longer has the retail
        // SHA/preimage and cannot be patched again.
    }

    Console.WriteLine(
        "Standalone diagnostic level warp: PASS exact-USA executable guard, " +
        "exact 8-byte delta, Tree Tops selector Right then Triangle, " +
        "second-apply rejection, and no normal Create BIN integration.");
}
finally
{
    if (Directory.Exists(temporaryRoot))
        Directory.Delete(temporaryRoot, recursive: true);
}

static string ResolveWorkspaceRoot(string? candidate)
{
    string current = Path.GetFullPath(
        candidate ?? Directory.GetCurrentDirectory());
    for (DirectoryInfo? directory = new(current);
         directory != null;
         directory = directory.Parent)
    {
        if (File.Exists(Path.Combine(
                directory.FullName,
                "spyro-level-catalog.json")) &&
            Directory.Exists(Path.Combine(
                directory.FullName,
                "src",
                "Spyro.Editor.Core")))
        {
            return directory.FullName;
        }
    }

    return current;
}

static long ParseHexOffset(string text)
{
    string value = text.StartsWith(
        "0x",
        StringComparison.OrdinalIgnoreCase)
        ? text[2..]
        : text;
    return long.Parse(
        value,
        NumberStyles.HexNumber,
        CultureInfo.InvariantCulture);
}

static byte[] ParseHex(string text) =>
    text.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries)
        .Select(part => byte.Parse(
            part,
            NumberStyles.HexNumber,
            CultureInfo.InvariantCulture))
        .ToArray();

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
