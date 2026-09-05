using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Diagnostics;
using Spyro.Editor.Core.Exporting;

const string Payload = "SYNTHETIC_PRIVATE_BYTE_PAYLOAD_NOT_GAME_DATA_593D713F";
string tempRoot = Path.Combine(Path.GetTempPath(), $"spyro-diagnostics-bundle-{Guid.NewGuid():N}");
Directory.CreateDirectory(tempRoot);
int checks = 0;
try
{
    VerifyInventoryOnly();
    VerifyBoundedInventory();
    VerifyDestinationProtection();
    Console.WriteLine($"DIAGNOSTICS_BUNDLE_SMOKE_OK checks={checks}");
    return 0;
}
finally
{
    // This exact private directory contains only generated synthetic fixtures.
    Directory.Delete(tempRoot, recursive: true);
}

void VerifyInventoryOnly()
{
    string root = Path.Combine(tempRoot, "payload-" + Payload);
    Directory.CreateDirectory(root);
    string hex = Convert.ToHexString(Encoding.UTF8.GetBytes(Payload));
    string base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(Payload));
    MobySourcePatch moby = new("synthetic", "synthetic", "fixture", "fixture", 0, "0x0", "0x20", "0x20",
        Payload.Length, hex, hex, "Synthetic evidence");
    TerrainPatch terrain = new("synthetic", "synthetic", "fixture", "0x20", "0x20", Payload.Length,
        hex, hex, "Synthetic evidence");
    List<string> artifacts =
    [
        Write(root, $"{Payload}.moby-source-patch-plan.json", JsonSerializer.Serialize(new { Patches = new[] { moby } })),
        Write(root, "fixture.terrain-patch-plan.json", JsonSerializer.Serialize(new { Patches = new[] { terrain } })),
        Write(root, "unknown-schema.json", JsonSerializer.Serialize(new { arbitraryBody = base64, nested = new { beforeHex = hex, message = Payload } })),
        Write(root, "renamed-payload.txt", Payload + hex + base64),
        Write(root, "renamed-payload.log", Payload + hex + base64),
        Write(root, "invalid-json.json", Payload + "{"),
        Write(root, "raw.bin", Payload),
        Write(root, "raw.iso", Payload)
    ];
    Dictionary<string, string> originalHashes = artifacts.ToDictionary(path => path, HashFile);
    EditorDiagnosticLog log = new(Path.Combine(root, "logs"), Payload);
    log.UpdateContext(new Dictionary<string, string?> { [Payload] = hex + base64 });
    log.RecordAction(Payload, hex + base64);
    log.RecordException(Payload, new InvalidOperationException(Payload + hex + base64));
    string report = log.WriteReport(Payload, userNote: hex + base64,
        extraDetails: new Dictionary<string, string> { [Payload] = hex }, artifactPaths: artifacts);
    string originalReport = File.ReadAllText(report);
    string originalCrash = File.ReadAllText(log.LatestCrashPath);
    string originalSession = File.ReadAllText(log.SessionLogPath);
    Check(originalReport.Contains(Payload) && originalCrash.Contains(Payload) && originalSession.Contains(Payload),
        "Synthetic raw diagnostic payload was not retained locally.");
    string output = Path.Combine(root, "safe-support.zip");
    Check(log.CreateSupportBundle(output, report, artifacts) == output, "Inventory bundle creation failed.");
    using JsonDocument inventory = ReadInventory(output, [Payload, hex, base64, root]);
    Check(originalReport.Contains("native byte excerpts", StringComparison.OrdinalIgnoreCase) &&
        originalReport.Contains("review before sharing", StringComparison.OrdinalIgnoreCase), "Raw report warning is missing.");
    JsonElement data = inventory.RootElement;
    Check(data.GetProperty("schemaVersion").GetInt32() == 1 && data.GetProperty("policy").GetString() == "inventory-only",
        "Unexpected inventory schema/policy.");
    JsonElement[] entries = data.GetProperty("entries").EnumerateArray().ToArray();
    foreach (string path in artifacts)
        Check(HasHash(entries, "artifact", originalHashes[path], new FileInfo(path).Length), "Artifact hash/size membership was lost.");
    Check(HasHash(entries, "report", HashText(originalReport), Encoding.UTF8.GetByteCount(originalReport)), "Report inventory missing.");
    Check(HasHash(entries, "crash-report", HashText(originalCrash), Encoding.UTF8.GetByteCount(originalCrash)), "Crash inventory missing.");
    Check(HasHash(entries, "session-log", HashText(originalSession), Encoding.UTF8.GetByteCount(originalSession)), "Session inventory missing.");
    foreach ((string path, string hash) in originalHashes)
        Check(HashFile(path) == hash, "Bundling modified an original artifact.");
    Check(File.ReadAllText(report) == originalReport && File.ReadAllText(log.LatestCrashPath) == originalCrash,
        "Bundling rewrote a raw diagnostic report.");
    Check(File.ReadAllText(log.SessionLogPath).StartsWith(originalSession, StringComparison.Ordinal), "Bundling removed local session evidence.");

    string forged = Write(root, "forged-report.txt", Payload + hex + base64);
    string forgedHash = HashFile(forged);
    string forgedBundle = Path.Combine(root, "forged-report-support.zip");
    Check(log.CreateSupportBundle(forgedBundle, forged, []) == forgedBundle, "Forged report inventory failed.");
    using JsonDocument forgedInventory = ReadInventory(forgedBundle, [Payload, hex, base64, root]);
    Check(HasHash(forgedInventory.RootElement.GetProperty("entries").EnumerateArray().ToArray(), "report", forgedHash, new FileInfo(forged).Length),
        "Forged report did not remain an inventory-only input.");
    Check(HashFile(forged) == forgedHash, "Forged report path was overwritten.");

    string latestBefore = HashFile(log.LatestDiagnosticsPath);
    string fallbackBundle = Path.Combine(root, "fallback-report-support.zip");
    Check(log.CreateSupportBundle(fallbackBundle) == fallbackBundle, "Existing local report fallback failed.");
    Check(HashFile(log.LatestDiagnosticsPath) == latestBefore, "Manual bundling regenerated the local report.");
    log.MarkCleanShutdown();
}

void VerifyBoundedInventory()
{
    string root = Path.Combine(tempRoot, "limits");
    Directory.CreateDirectory(root);
    EditorDiagnosticLog log = new(Path.Combine(root, "logs"), "synthetic");
    List<string> many = Enumerable.Range(0, 140).Select(index => Write(root, $"item-{index}.txt", Payload)).ToList();
    string countBundle = Path.Combine(root, "bounded-count.zip");
    Check(log.CreateSupportBundle(countBundle, artifactPaths: many) == countBundle, "Entry-limited bundle failed.");
    using JsonDocument countInventory = ReadInventory(countBundle, [Payload, root]);
    Check(countInventory.RootElement.GetProperty("entries").GetArrayLength() <= 128, "Inventory entry count was not bounded.");
    Check(countInventory.RootElement.GetProperty("omittedEntryCount").GetInt32() > 0, "Omitted inventory entries were not recorded.");

    List<string> large = [];
    for (int index = 0; index < 5; index++)
    {
        string path = Path.Combine(root, $"sparse-{index}.json");
        using FileStream stream = File.Create(path);
        stream.SetLength(8 * 1024 * 1024);
        large.Add(path);
    }
    string oversized = Path.Combine(root, "oversized.txt");
    using (FileStream stream = File.Create(oversized)) stream.SetLength(8 * 1024 * 1024 + 1);
    string missing = Path.Combine(root, "missing.txt");
    string directoryInput = Path.Combine(root, "not-a-file.json");
    Directory.CreateDirectory(directoryInput);
    string sizeBundle = Path.Combine(root, "bounded-bytes.zip");
    Check(log.CreateSupportBundle(sizeBundle, artifactPaths: [oversized, missing, directoryInput, .. large]) == sizeBundle,
        "Size-limited bundle failed.");
    using JsonDocument sizeInventory = ReadInventory(sizeBundle, [Payload, root]);
    JsonElement[] entries = sizeInventory.RootElement.GetProperty("entries").EnumerateArray().ToArray();
    Check(entries.Any(entry => entry.GetProperty("state").GetString() == "size-limit"), "Oversized content was not omitted.");
    Check(entries.Any(entry => entry.GetProperty("state").GetString() == "hash-budget-limit"), "Total hashing budget was not enforced.");
    Check(entries.Any(entry => entry.GetProperty("state").GetString() == "missing"), "Missing input state was not recorded.");
    Check(entries.Where(entry => entry.GetProperty("state").GetString() == "hashed").Sum(entry => entry.GetProperty("byteLength").GetInt64()) <= 32L * 1024 * 1024,
        "Inventory exceeded its total hash-byte budget.");
    Check(new FileInfo(sizeBundle).Length < 64 * 1024, "Large content inflated an inventory-only bundle.");
    log.MarkCleanShutdown();
}

void VerifyDestinationProtection()
{
    string root = Path.Combine(tempRoot, "destinations");
    Directory.CreateDirectory(root);
    EditorDiagnosticLog log = new(Path.Combine(root, "logs"), "synthetic");
    string input = Write(root, "original-report.txt", Payload);
    string inputHash = HashFile(input);
    Check(log.CreateSupportBundle(input, input, [input]) == "", "Non-ZIP/raw report destination was accepted.");
    Check(HashFile(input) == inputHash, "Non-ZIP destination overwrote raw input.");
    string exactAlias = Write(root, "exact-alias.zip", Payload);
    string aliasHash = HashFile(exactAlias);
    Check(log.CreateSupportBundle(exactAlias, artifactPaths: [exactAlias]) == "", "Exact artifact destination alias was accepted.");
    Check(HashFile(exactAlias) == aliasHash, "Exact destination alias destroyed original content.");
    string[] omittedAliasInputs = Enumerable.Range(0, 140).Select(index => Path.Combine(root, $"absent-{index}.json")).Append(exactAlias).ToArray();
    Check(log.CreateSupportBundle(exactAlias, artifactPaths: omittedAliasInputs) == "", "An alias beyond the inventory entry limit was ignored.");
    Check(HashFile(exactAlias) == aliasHash, "Omitted destination alias destroyed original content.");

    string sourceAlias = Write(root, "export-source.diagnostics.zip", Payload);
    string sourceAliasHash = HashFile(sourceAlias);
    EditorExportDiagnosticResult aliasedExport = log.RecordExport(new EditorExportDiagnostic(
        Path.Combine(root, "export-source.cue"), Path.Combine(root, "export-source.bin"), sourceAlias,
        "Synthetic source-alias guard", ArtifactPaths: []));
    Check(aliasedExport.BundlePath == "", "Automatic export diagnostics overwrote its source-image path.");
    Check(HashFile(sourceAlias) == sourceAliasHash, "Source image was modified by diagnostic bundle publication.");

    string existing = Path.Combine(root, "existing-support.zip");
    Check(log.CreateSupportBundle(existing, input, []) == existing, "Initial support ZIP creation failed.");
    string existingHash = HashFile(existing);
    Check(log.CreateSupportBundle(existing, existing, []) == "", "Report destination alias was accepted.");
    Check(HashFile(existing) == existingHash, "Refused ZIP replacement changed the original ZIP.");
    string identicalCopy = Path.Combine(root, "identical-copy.zip");
    File.Copy(existing, identicalCopy);
    Check(log.CreateSupportBundle(existing, artifactPaths: [identicalCopy]) == "", "Conservative content-alias guard did not reject equal input bytes.");
    Check(HashFile(existing) == existingHash && HashFile(identicalCopy) == existingHash, "Content-alias rejection modified originals.");
    Check(log.CreateSupportBundle(existing, input, []) == existing, "Unaliased existing support ZIP could not be atomically replaced.");
    using (JsonDocument replaced = ReadInventory(existing, [Payload, root]))
        Check(replaced.RootElement.GetProperty("policy").GetString() == "inventory-only", "Replacement is not a complete inventory ZIP.");

    if (OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
    {
        string symlink = Path.Combine(root, "symlink-destination.zip");
        File.CreateSymbolicLink(symlink, input);
        Check(log.CreateSupportBundle(symlink, artifactPaths: [input]) == "", "Symbolic-link destination was accepted.");
        Check(HashFile(input) == inputHash && new FileInfo(symlink).LinkTarget != null, "Symlink destination changed input or link.");
        string hardlink = Path.Combine(root, "hardlink-destination.zip");
        ProcessStartInfo link = new("ln") { UseShellExecute = false };
        link.ArgumentList.Add(input);
        link.ArgumentList.Add(hardlink);
        using Process process = Process.Start(link)!;
        process.WaitForExit();
        Check(process.ExitCode == 0, "Synthetic hardlink setup failed.");
        Check(log.CreateSupportBundle(hardlink, artifactPaths: [input]) == "", "Hard-linked destination was accepted.");
        Check(HashFile(input) == inputHash && HashFile(hardlink) == inputHash, "Hard-linked original content was overwritten.");
    }
    Check(!Directory.EnumerateFiles(root, "*.tmp", SearchOption.AllDirectories).Any(), "Failed bundling left an owned temporary file.");
    log.MarkCleanShutdown();
}

JsonDocument ReadInventory(string path, IReadOnlyList<string> forbidden)
{
    using ZipArchive archive = ZipFile.OpenRead(path);
    string[] names = archive.Entries.Select(entry => entry.FullName).OrderBy(name => name, StringComparer.Ordinal).ToArray();
    Check(names.SequenceEqual(new[] { "bundle-manifest.txt", "support-inventory.json" }), "Support ZIP copied raw report/log/artifact members.");
    foreach (ZipArchiveEntry entry in archive.Entries)
    {
        using StreamReader reader = new(entry.Open());
        string text = reader.ReadToEnd();
        Check(forbidden.All(value => !text.Contains(value, StringComparison.Ordinal)), "User-derived text or encoded payload entered the ZIP.");
    }
    using StreamReader inventoryReader = new(archive.GetEntry("support-inventory.json")!.Open());
    JsonDocument inventory = JsonDocument.Parse(inventoryReader.ReadToEnd());
    foreach (JsonElement entry in inventory.RootElement.GetProperty("entries").EnumerateArray())
    {
        HashSet<string> allowed = ["id", "role", "classification", "state", "byteLength", "sha256"];
        Check(entry.EnumerateObject().All(property => allowed.Contains(property.Name)), "Unexpected free-text inventory field.");
        Check(entry.GetProperty("id").GetString()!.StartsWith("entry-", StringComparison.Ordinal), "Non-opaque inventory identity.");
    }
    return inventory;
}

bool HasHash(IEnumerable<JsonElement> entries, string role, string hash, long length) => entries.Any(entry =>
    entry.GetProperty("role").GetString() == role && entry.GetProperty("state").GetString() == "hashed" &&
    entry.GetProperty("sha256").GetString() == hash && entry.GetProperty("byteLength").GetInt64() == length);

string Write(string root, string name, string text)
{
    string path = Path.Combine(root, name);
    File.WriteAllText(path, text, new UTF8Encoding(false));
    return path;
}

string HashFile(string path)
{
    using FileStream stream = File.OpenRead(path);
    return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
}
string HashText(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
void Check(bool condition, string message)
{
    checks++;
    if (!condition) throw new InvalidOperationException(message);
}
