using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;

if (args.Length == 2) { SourceReadback.Verify(args[0], args[1]); return; }
if (args.Length != 0) throw new ArgumentException("Use no arguments for source-free tests, or explicit source/output paths for read-only raw source verification.");
string directory = Directory.CreateTempSubdirectory("spyro-wad-growth-gap-").FullName;
int checks = 0;
try
{
    Type relocator = typeof(NativeTerrainTextureRecordSectorRelocationComposer).Assembly
        .GetType("Spyro.Editor.Core.Exporting.NativeSkyWadRelocator", throwOnError: true)!;
    Type payloadType = relocator.Assembly.GetType("Spyro.Editor.Core.Exporting.NativeSkyRelocationPayload", true)!;
    foreach (int sectorSize in new[] { 2352, 2336, 2048 })
    foreach (int growthSectors in new[] { 1, 2, 3 })
    foreach ((byte wadFlags, byte exeFlags) in new (byte, byte)[] { (0x81, 0x81), (0x01, 0x80), (0x80, 0x01), (0, 0) })
    {
        string source = Path.Combine(directory, $"source-{sectorSize}.bin");
        string output = Path.Combine(directory, $"output-{sectorSize}.bin");
        string analysis = Path.Combine(directory, "synthetic-wad-analysis.json");
        byte[] before = GapFixture.Image(sectorSize);
        if (wadFlags == 1) GapFixture.AddDirectory(before, sectorSize);
        if (sectorSize != 2048)
        {
            GapFixture.EditSector(before, sectorSize, 42, raw => raw[18] = raw[22] = (byte)(8 | wadFlags));
            GapFixture.EditSector(before, sectorSize, 44, raw => raw[18] = raw[22] = (byte)(8 | exeFlags));
            // The owned padding can be a mixture of already-Form1 and Form2.
            GapFixture.EditSector(before, sectorSize, 46, raw => { raw.AsSpan(24).Clear(); raw[18] = raw[22] = 8; });
        }
        File.WriteAllBytes(source, before);
        File.WriteAllText(analysis, """
            {"wad":{"lba":37,"size":12288},"entries":[
             {"index":0,"offset":2048,"size":6144},{"index":1,"offset":8192,"size":4096}]}
            """);
        Array payloads = Array.CreateInstance(payloadType, 1);
        payloads.SetValue(Activator.CreateInstance(payloadType,
            [0, 37, 0x1800L, 0, 0x800, 0, new byte[growthSectors * 2048], 0, false]), 0);
        object plan = Invoke("BuildPlan", source, analysis, payloads)!;
        // Regression red: the existing writer reaches the first Form2 padding
        // sector after already changing its disposable output, then throws.
        Invoke("WriteExpandedImage", source, output, analysis, plan, payloads);
        Check(File.ReadAllBytes(source).SequenceEqual(before), "Structural growth changed its source.");
        byte[] after = File.ReadAllBytes(output);
        Check(after.Length == before.Length, "Structural growth extended the physical image.");
        int userOffset = sectorSize == 2352 ? 24 : sectorSize == 2336 ? 8 : 0;
        for (int lba = 37; lba < 45 + growthSectors; lba++)
        {
            GapFixture.CheckForm1(after, sectorSize, lba, Check);
            if (sectorSize != 2048)
            {
                int submode = lba * sectorSize + userOffset - 6;
                byte wanted = (byte)(8 | (lba == 42 + growthSectors ? wadFlags : lba == 44 + growthSectors ? exeFlags : 0));
                Check(after[submode] == wanted && after[submode + 4] == wanted,
                    "WAD/EXE old, new or shared EOF/EOR boundary has the wrong exact flags.");
                if (sectorSize == 2352) Check(before.AsSpan(lba * sectorSize, 16).SequenceEqual(after.AsSpan(lba * sectorSize, 16)),
                    "Structural relocation changed unrelated sync/MSF/header bytes.");
            }
        }
        for (int sector = 0; sector < 2; sector++)
            Check(before.AsSpan((43 + sector) * sectorSize + userOffset, 2048)
                    .SequenceEqual(after.AsSpan((43 + growthSectors + sector) * sectorSize + userOffset, 2048)),
                "Relocated executable logical bytes changed.");
        for (int lba = 0; lba < 64; lba++)
            if (lba != 20 && (lba < 37 || lba >= 45 + growthSectors))
                Check(before.AsSpan(lba * sectorSize, sectorSize).SequenceEqual(after.AsSpan(lba * sectorSize, sectorSize)),
                    "Structural allocation changed an unowned physical sector.");
    }
    NegativeCases.Run(directory, payloadType, (name, args) => Invoke(name, args), Check);
    Console.WriteLine($"WAD_GROWTH_GAP_SMOKE_OK checks={checks}");

    object? Invoke(string name, params object?[] args)
    {
        try { return relocator.GetMethod(name, BindingFlags.Public | BindingFlags.Static)!.Invoke(null, args); }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        { ExceptionDispatchInfo.Capture(ex.InnerException).Throw(); throw; }
    }
    void Check(bool condition, string message) { checks++; if (!condition) throw new InvalidOperationException(message); }
}
finally { Directory.Delete(directory, recursive: true); }
