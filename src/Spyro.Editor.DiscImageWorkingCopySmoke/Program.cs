using Spyro.Editor.Core.Exporting;

string temporaryRoot = Path.Combine(
    Path.GetTempPath(),
    $"spyro-editor-disc-working-copy-{Guid.NewGuid():N}");

try
{
    Directory.CreateDirectory(temporaryRoot);

    byte[] expectedBytes = BuildFixtureBytes();
    string preservedSourcePath = Path.Combine(temporaryRoot, "preserved-source.bin");
    string copiedDestinationPath = Path.Combine(temporaryRoot, "copied-destination.bin");
    await File.WriteAllBytesAsync(preservedSourcePath, expectedBytes);
    await File.WriteAllBytesAsync(copiedDestinationPath, [0xFF]);

    await DiscImageWorkingCopy.StageAsync(
        preservedSourcePath,
        copiedDestinationPath,
        consumeDisposableSource: false,
        CancellationToken.None);

    Assert(
        File.Exists(preservedSourcePath),
        "Default staging removed its source disc image.");
    AssertExactBytes(
        preservedSourcePath,
        expectedBytes,
        "Default staging changed its source disc image.");
    AssertExactBytes(
        copiedDestinationPath,
        expectedBytes,
        "Default staging did not copy the source bytes exactly.");

    string consumedSourcePath = Path.Combine(temporaryRoot, "consumed-source.bin");
    string movedDestinationPath = Path.Combine(temporaryRoot, "moved-destination.bin");
    await File.WriteAllBytesAsync(consumedSourcePath, expectedBytes);
    await File.WriteAllBytesAsync(movedDestinationPath, [0xEE, 0xDD]);

    await DiscImageWorkingCopy.StageAsync(
        consumedSourcePath,
        movedDestinationPath,
        consumeDisposableSource: true,
        CancellationToken.None);

    Assert(
        !File.Exists(consumedSourcePath),
        "Consume staging left the disposable source disc image behind.");
    AssertExactBytes(
        movedDestinationPath,
        expectedBytes,
        "Consume staging did not preserve the source bytes exactly.");

    Console.WriteLine("Disc-image working-copy smoke passed.");
    Console.WriteLine("- default copy preserved the source and copied exact bytes");
    Console.WriteLine("- consume mode moved the disposable source and preserved exact bytes");
}
finally
{
    if (Directory.Exists(temporaryRoot))
        Directory.Delete(temporaryRoot, recursive: true);
}

static byte[] BuildFixtureBytes()
{
    byte[] bytes = new byte[64 * 1024 + 37];
    for (int index = 0; index < bytes.Length; index++)
        bytes[index] = (byte)((index * 73 + index / 251 + 19) & 0xFF);

    return bytes;
}

static void AssertExactBytes(
    string path,
    ReadOnlySpan<byte> expected,
    string message)
{
    Assert(File.Exists(path), $"{message} The destination file is missing.");
    byte[] actual = File.ReadAllBytes(path);
    Assert(actual.AsSpan().SequenceEqual(expected), message);
}

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
