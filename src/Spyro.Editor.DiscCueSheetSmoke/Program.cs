using System.Buffers.Binary;
using System.Text;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;

string root = Path.Combine(Path.GetTempPath(), $"spyro-cue-{Guid.NewGuid():N}");
Directory.CreateDirectory(root);
int checks = 0;
List<string> failures = [];
try
{
    string raw = Image("data.bin", 2352, 24);
    string cooked = Image("cooked.iso", 2048, 0);
    string headerless = Image("headerless.bin", 2336, 8);
    Run("single-file multitrack is byte-compatible", () =>
    {
        string cue = Cue("single.cue", "REM preserve this\nFILE \"data.bin\" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n  TRACK 02 AUDIO\n    INDEX 00 12:34:00\n    INDEX 01 12:36:00\n");
        Require(Build(cue, raw) == File.ReadAllText(cue).Replace("data.bin", "result.bin"), "Single-file track geometry changed.");
    });
    Run("missing CUE cooked2048", () => Require(Build("", cooked).Contains("TRACK 01 MODE1/2048"), "Cooked image declared as raw2352."));
    Run("missing CUE raw2336", () => Require(Build("", headerless).Contains("TRACK 01 MODE2/2336"), "Headerless image declared as raw2352."));
    Run("missing CUE raw2352", () => Require(Build("", raw).Contains("TRACK 01 MODE2/2352"), "Raw image track mode changed."));
    Run("existing cooked MODE2/2048 is preserved", () =>
        Require(Build(Cue("cooked-mode2.cue", "FILE cooked.iso BINARY\n TRACK 01 MODE2/2048\n INDEX 01 00:00:00\n"), cooked)
            .Contains("TRACK 01 MODE2/2048"), "Valid cooked MODE2 track declaration was changed or rejected."));
    Run("stored first-track pregap is rejected", () => Rejected(() => Build(Cue("stored-gap.cue", "FILE data.bin BINARY\n TRACK 01 MODE2/2352\n INDEX 00 00:00:00\n INDEX 01 00:02:00\n"), raw)));
    Run("leading audio within edited FILE is rejected", () => Rejected(() => Build(Cue("same-file-audio-first.cue", "FILE data.bin BINARY\n TRACK 01 AUDIO\n INDEX 01 00:00:00\n TRACK 02 MODE2/2352\n INDEX 01 00:02:00\n"), raw)));
    Run("first data INDEX01 must exist", () => Rejected(() => Build(Cue("missing-index.cue", "FILE data.bin BINARY\n TRACK 01 MODE2/2352\n"), raw)));
    Run("invalid INDEX time is rejected", () => Rejected(() => Build(Cue("invalid-index.cue", "FILE data.bin BINARY\n TRACK 01 MODE2/2352\n INDEX 01 00:60:00\n"), raw)));
    Run("lowercase source FILE remains legacy-compatible", () =>
        Require(Build(Cue("lower.cue", "file data.bin binary\n track 01 mode2/2352\n index 01 00:00:00\n"), raw)
            .StartsWith("FILE \"result.bin\" BINARY\n"), "Selected binary FILE casing changed its legacy output."));
    string audio = Path.Combine(root, "audio track.bin");
    File.WriteAllBytes(audio, new byte[2352]);
    Run("leading binary audio plus data", () =>
    {
        string cue = Cue("audio-first.cue", "FILE \"audio track.bin\" BINARY\n  TRACK 01 AUDIO\n    INDEX 01 00:00:00\nFILE \"data.bin\" BINARY\n  TRACK 02 MODE2/2352\n    PREGAP 00:02:00\n    INDEX 01 00:00:00\n");
        string before = File.ReadAllText(cue);
        string output = Build(cue, raw);
        Require(output.StartsWith($"FILE \"{audio}\" BINARY\n") && output.Contains("FILE \"result.bin\" BINARY\n  TRACK 02"), "Wrong FILE was replaced or secondary path stayed relative.");
        Require(output.Contains("PREGAP 00:02:00") && File.ReadAllText(cue) == before, "Pregap or original CUE changed.");
        Require(DiscImageLocator.ResolveSelection(cue).ImagePath == raw, "Locator selected the leading audio file.");
    });
    Run("secondary WAVE path is preserved", () =>
    {
        string cue = Cue("wave.cue", "FILE data.bin BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\nFILE \"audio track.bin\" WAVE\n  TRACK 02 AUDIO\n    INDEX 01 00:00:00\n");
        Require(Build(cue, raw).Contains($"FILE \"{audio}\" WAVE"), "Secondary WAVE reference did not follow its original directory.");
    });
    Run("source CUE mismatch", () => Rejected(() => Build(Cue("wrong.cue", "FILE data.bin BINARY\n TRACK 01 MODE2/2352\n INDEX 01 00:00:00\n"), cooked)));
    Run("track mode mismatch", () => Rejected(() => Build(Cue("mode.cue", "FILE data.bin BINARY\n TRACK 01 MODE1/2048\n INDEX 01 00:00:00\n"), raw)));
    Run("malformed FILE", () => Rejected(() => Build(Cue("bad.cue", "FILE \"unclosed BINARY\n TRACK 01 MODE2/2352\n"), raw)));
    Run("missing secondary file", () => Rejected(() => Build(Cue("missing.cue", "FILE data.bin BINARY\n TRACK 01 MODE2/2352\n INDEX 01 00:00:00\nFILE gone.wav WAVE\n TRACK 02 AUDIO\n INDEX 01 00:00:00\n"), raw)));
    Run("moved disposable image retains CUE identity", () =>
    {
        string cue = Cue("moved.cue", "FILE consumed.bin BINARY\n TRACK 01 MODE2/2336\n INDEX 01 00:00:00\n");
        Require(Build(cue, Path.Combine(root, "consumed.bin"), headerless).Contains("TRACK 01 MODE2/2336"), "Moved input could not use live output layout.");
    });
    Run("ambiguous data source", () => Rejected(() => DiscImageLocator.ResolveSelection(Cue("ambiguous.cue", "FILE data.bin BINARY\n TRACK 01 MODE2/2352\n INDEX 01 00:00:00\nFILE headerless.bin BINARY\n TRACK 02 MODE2/2336\n INDEX 01 00:00:00\n"))));
    Run("same-name unrelated cue is not selected", () =>
    {
        string other = Image("other.bin", 2352, 24);
        Cue("other.cue", "FILE data.bin BINARY\n TRACK 01 MODE2/2352\n INDEX 01 00:00:00\n");
        Require(!File.Exists(DiscImageLocator.FindCueForImage(other)), "Unrelated preferred CUE was accepted.");
    });
    Run("invalid UTF8 preferred cue does not block discovery", () =>
    {
        string candidate = Image("unicode-source.bin", 2352, 24);
        File.WriteAllBytes(Path.ChangeExtension(candidate, ".cue"), [0xC3, 0x28]);
        string good = Cue("unicode-valid.cue", "FILE unicode-source.bin BINARY\n TRACK 01 MODE2/2352\n INDEX 01 00:00:00\n");
        Require(DiscImageLocator.FindCueForImage(candidate) == good, "An unrelated malformed text file prevented finding the valid CUE.");
    });
    Run("NUL filename preferred cue does not block discovery", () =>
    {
        string candidate = Image("nul-source.bin", 2352, 24);
        Cue("nul-source.cue", "FILE \"bad\0.bin\" BINARY\n TRACK 01 MODE2/2352\n INDEX 01 00:00:00\n");
        string good = Cue("nul-valid.cue", "FILE nul-source.bin BINARY\n TRACK 01 MODE2/2352\n INDEX 01 00:00:00\n");
        Require(DiscImageLocator.FindCueForImage(candidate) == good, "An invalid filename prevented finding the valid CUE.");
    });
    Run("UTF8 no BOM preserves non-ASCII paths", () =>
    {
        string text = "FILE \"音楽.bin\" BINARY\n";
        Require(DiscCueSheet.TextEncoding.GetPreamble().Length == 0 && DiscCueSheet.TextEncoding.GetString(DiscCueSheet.TextEncoding.GetBytes(text)) == text,
            "CUE text encoding loses Unicode paths or changes ASCII hashes with a BOM.");
    });
    if (failures.Count > 0) throw new InvalidOperationException($"{failures.Count}/{checks} failed:\n" + string.Join('\n', failures));
    Console.WriteLine($"DISC_CUE_SHEET_SMOKE_OK checks={checks}; layout/identity, multitrack geometry, secondary audio, moved source, Unicode and source preservation");
    if (args.Length == 1)
    {
        string source = Path.GetFullPath(args[0]);
        string cue = DiscImageLocator.FindCueForImage(source);
        byte[] before = File.ReadAllBytes(cue);
        string[] lines = File.ReadAllLines(cue, Encoding.ASCII);
        int dataFile = Array.FindIndex(lines, line => line.Trim().StartsWith("FILE ", StringComparison.OrdinalIgnoreCase) && line.Trim().EndsWith(" BINARY", StringComparison.OrdinalIgnoreCase));
        Require(dataFile >= 0, "Retail CUE has no legacy binary FILE declaration.");
        lines[dataFile] = "FILE \"result.bin\" BINARY";
        byte[] expected = Encoding.ASCII.GetBytes(string.Join('\n', lines) + "\n");
        byte[] actual = DiscCueSheet.TextEncoding.GetBytes(Build(cue, source));
        Require(actual.AsSpan().SequenceEqual(expected), "Actual retail CUE changed relative to legacy publication.");
        Require(File.ReadAllBytes(cue).AsSpan().SequenceEqual(before), "Actual source CUE changed.");
        Console.WriteLine($"RETAIL_CUE_COMPATIBILITY_OK sourceCueSha256={Convert.ToHexStringLower(SHA256.HashData(before))}; exact legacy output bytes and source preservation");
    }
}
finally { Directory.Delete(root, recursive: true); }

string Build(string cue, string source, string? live = null) => DiscImage.BuildCueText(cue, "result.bin", source, live);
void Run(string name, Action action) { checks++; try { action(); } catch (Exception ex) { failures.Add($"{name}: {ex.Message}"); } }
string Cue(string name, string content) { string path = Path.Combine(root, name); File.WriteAllText(path, content); return path; }
static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
static void Rejected(Action action)
{
    try { action(); } catch (Exception ex) when (ex is InvalidDataException or FileNotFoundException) { return; }
    throw new InvalidOperationException("Invalid/ambiguous CUE was accepted.");
}
string Image(string name, int size, int userOffset)
{
    byte[] bytes = new byte[24 * size];
    Span<byte> pvd = bytes.AsSpan(16 * size + userOffset, 2048);
    pvd[0] = 1; pvd[6] = 1; pvd[156] = 34; pvd[188] = 1;
    Encoding.ASCII.GetBytes("CD001").CopyTo(pvd[1..]);
    BinaryPrimitives.WriteUInt16LittleEndian(pvd[128..], 2048);
    BinaryPrimitives.WriteUInt16BigEndian(pvd[130..], 2048);
    BinaryPrimitives.WriteInt32LittleEndian(pvd[158..], 22);
    BinaryPrimitives.WriteInt32BigEndian(pvd[162..], 22);
    BinaryPrimitives.WriteInt32LittleEndian(pvd[166..], 2048);
    BinaryPrimitives.WriteInt32BigEndian(pvd[170..], 2048);
    string path = Path.Combine(root, name); File.WriteAllBytes(path, bytes); return path;
}
