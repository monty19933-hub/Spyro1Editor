using Spyro.Editor.Core.Persistence;

if (args.Length != 0) throw new ArgumentException("This preference regression is source-free and takes no arguments.");
int checks = 0;
string root = Directory.CreateTempSubdirectory("spyro-free-flight-settings-").FullName;
try
{
    var original = NativeFreeFlightSettingsStore.Capture(root);
    Check(!original.Exists && !original.IsEnabled(14) && !original.HasExplicitSelection(14), "absent means retail default, without saved intent");
    Check(!Directory.EnumerateFileSystemEntries(root).Any(), "capture is read-only");
    var enabled = NativeFreeFlightSettingsStore.Save(original, 14, true);
    Check(enabled.Exists && enabled.IsEnabled(14) && enabled.HasExplicitSelection(14) && !enabled.NativeExportApproved,
        "enabled is durable intent, not output authority");
    byte[] enabledBytes = File.ReadAllBytes(enabled.Path);
    Check(NativeFreeFlightSettingsStore.Capture(root).IsEnabled(14), "actual persisted reload");
    Refuse(() => NativeFreeFlightSettingsStore.Save(original, 13, true), "stale absence must not replace an existing selection");
    Check(File.ReadAllBytes(enabled.Path).SequenceEqual(enabledBytes), "stale refusal preserves bytes");
    var off = NativeFreeFlightSettingsStore.Save(enabled, 14, false);
    Check(off.Exists && !off.IsEnabled(14) && off.HasExplicitSelection(14), "off remains explicit so old enabled BIN must be rebuilt");
    byte[] offBytes = File.ReadAllBytes(off.Path);
    Refuse(() => NativeFreeFlightSettingsStore.Save(enabled, 13, true), "stale previous setting cannot win");
    Check(File.ReadAllBytes(off.Path).SequenceEqual(offBytes), "second stale refusal preserves bytes");
    var second = NativeFreeFlightSettingsStore.Save(off, 12, true);
    Check(second.Preferences.Select(p => p.LevelId).SequenceEqual(new[] { 12, 14 }) && second.HasExplicitSelection(14) && !second.IsEnabled(14),
        "per-level changes preserve other explicit disabled settings");
    var same = NativeFreeFlightSettingsStore.Save(second, 12, true);
    Check(same.ContentSha256 == second.ContentSha256, "same choice retains deterministic bytes");
    Refuse(() => NativeFreeFlightSettingsStore.Save(second, 14, true, new CancellationToken(true)), "canceled publication");
    Check(NativeFreeFlightSettingsStore.Capture(root).ContentSha256 == second.ContentSha256, "canceled save does not write");
    foreach (int invalid in new[] { -1, 0, 9, 15, 16, 19, 25, 35, 45, 55, 65, int.MaxValue })
        Refuse(() => NativeFreeFlightSettingsStore.Save(second, invalid, true), "unsupported native identity");
    Check(NativeFreeFlightSettingsStore.Capture(root).ContentSha256 == second.ContentSha256, "bad identities leave settings unchanged");

    string publicationLock = Path.Combine(root, ".export-publication.lock");
    File.WriteAllText(publicationLock, "owned by another fixture operation");
    Refuse(() => NativeFreeFlightSettingsStore.Save(second, 14, true), "competing publisher");
    Check(File.ReadAllText(publicationLock) == "owned by another fixture operation" &&
        NativeFreeFlightSettingsStore.Capture(root).ContentSha256 == second.ContentSha256, "publisher lock and old settings retained");
    File.Delete(publicationLock);

    string valid = File.ReadAllText(second.Path);
    string[] malformed =
    [
        "", "null", "{}", "[]", valid.Replace("\"version\": 1", "\"version\": 2"),
        valid.Replace("\"version\": 1", "\"version\": 1, \"version\": 1"),
        valid.Replace("\"version\": 1", "\"version\": 1, \"nativeExportApproved\": true"),
        valid.Replace("\"levelId\": 12", "\"levelId\": 14"),
        valid.Replace("\"levelId\": 12", "\"levelId\": 65"),
        valid.Replace("\"enableFreeFlight\": true", "\"enableFreeFlight\": \"true\""),
        valid.Replace("\"enableFreeFlight\": true", "\"enableFreeFlight\": true, \"enableFreeFlight\": false"),
        valid.Replace("\"enableFreeFlight\": true", "\"enableFreeFlight\": true, \"ignored\": 1"),
        new string(' ', 16385)
    ];
    foreach (string bad in malformed)
    {
        File.WriteAllText(second.Path, bad);
        Refuse(() => NativeFreeFlightSettingsStore.Capture(root), "corrupt/unknown settings must not silently become retail defaults");
        Check(File.ReadAllText(second.Path) == bad, "failed read never repairs or overwrites unknown data");
    }
    File.WriteAllText(second.Path, valid);
    Check(NativeFreeFlightSettingsStore.Capture(root).ContentSha256 == second.ContentSha256, "exact restoration rebinds snapshot");
    File.Delete(second.Path);
    Directory.CreateDirectory(second.Path);
    Refuse(() => NativeFreeFlightSettingsStore.Capture(root), "settings path is a directory");
    Directory.Delete(second.Path);
    string unrelated = Path.Combine(root, "unrelated.json");
    File.WriteAllText(unrelated, "protected");
    if (!OperatingSystem.IsWindows())
    {
        File.CreateSymbolicLink(second.Path, unrelated);
        Refuse(() => NativeFreeFlightSettingsStore.Capture(root), "linked preference destination");
        Check(File.ReadAllText(unrelated) == "protected", "linked external data is not overwritten");
        File.Delete(second.Path);
    }
    Check(!Directory.EnumerateFiles(root).Any(p => Path.GetFileName(p).StartsWith(".free-flight-", StringComparison.Ordinal)),
        "owned staging files and writer lease are cleaned after success/refusal");
    ProjectExportIntentCases.Run(Check, Refuse);
    Console.WriteLine($"FREE_FLIGHT_SETTINGS_SMOKE_OK checks={checks}: default-off, explicit disable, durable per-level intent, stale-CAS, strict decode, cancellation, production intent and publication failure preservation.");
}
finally { Directory.Delete(root, recursive: true); }
void Check(bool value, string label) { if (!value) throw new InvalidOperationException(label); checks++; }
void Refuse(Action action, string label)
{
    try { action(); }
    catch (Exception error) when (error is IOException or InvalidDataException or InvalidOperationException or ArgumentException or OperationCanceledException)
    { checks++; return; }
    throw new InvalidOperationException("Expected refusal: " + label);
}
