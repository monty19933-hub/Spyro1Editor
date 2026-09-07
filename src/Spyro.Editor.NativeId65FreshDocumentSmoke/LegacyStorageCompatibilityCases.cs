using System.Reflection;
using System.Security.Cryptography;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Persistence;

// Characterizes the existing implementation BEFORE any shared-storage extraction.
// These are real tiny private file operations, never source/candidate publication.
internal static class LegacyStorageCompatibilityCases
{
    private static int _checks;
    private const string DocumentName = "native-id65-fresh-document-v1.json";
    private const string WriterName = ".native-id65-fresh-document.writer.lock";

    internal static void Run()
    {
        Shape();
        string temporary = Directory.CreateTempSubdirectory("spyro-legacy-storage-compat-").FullName;
        try
        {
            var project = new EditorProjectLayout(temporary, "compat");
            byte[] manifest = "{\"schemaVersion\":1,\"projectId\":\"compat\",\"unrelated\":{\"preserve\":true}}\r\n"u8.ToArray();
            File.WriteAllBytes(project.ManifestPath, manifest);
            string modern = Path.Combine(temporary, "native-fresh-level.json");
            const string modernSentinel = "Unrelated aggregate sentinel, not a legacy v1 document";
            File.WriteAllText(modern, modernSentinel);
            var intent = NativeId65FreshDocumentIntent.Create(8448, 8448, 8704, 1024, 1024,
                8960, 8960, 64, new(80, 128, 112), new(128, 96, 64), 19);
            byte[] canonical = NativeId65FreshDocumentCodec.SerializeCanonical(intent);
            var absent = NativeId65FreshDocumentStore.Capture(project);
            string writer = Path.Combine(temporary, WriterName);
            Check(NativeId65FreshDocumentStore.FileName == DocumentName && absent.Path == Path.Combine(temporary, DocumentName),
                "exact legacy filename/path remains distinct from aggregate");
            Check(absent.RootPath == Path.TrimEndingDirectorySeparator(Path.GetFullPath(temporary)) && absent.ProjectId == "compat" &&
                absent.ManifestSha256 == Hash(manifest), "normalized root/project/raw manifest observation");

            using var canceled = new CancellationTokenSource(); canceled.Cancel();
            Expect<OperationCanceledException>(() => NativeId65FreshDocumentStore.Capture(null!, canceled.Token));
            Expect<ArgumentNullException>(() => NativeId65FreshDocumentStore.Save(null!, null!, canceled.Token), "expected");
            Expect<ArgumentNullException>(() => NativeId65FreshDocumentStore.Save(absent, null!, canceled.Token), "intent");
            Expect<OperationCanceledException>(() => NativeId65FreshDocumentStore.AcquireReadLease(null!, canceled.Token));
            Expect<ArgumentNullException>(() => NativeId65FreshDocumentStore.ReadBounded(null!, 3, canceled.Token), "input");
            Check(!File.Exists(writer) && !File.Exists(absent.Path), "argument/cancellation precedence creates no owner");

            List<string> phases = [];
            var saved = NativeId65FreshDocumentStore.SaveForSmoke(absent, intent, phase =>
            {
                phases.Add(phase);
                Check(File.Exists(writer) && new FileInfo(writer).Length == 0, "legacy Save lease remains exact empty held owner");
                if (phase == "staged")
                {
                    string[] stages = Directory.GetFiles(temporary, ".native-id65-fresh-document-*.tmp");
                    Check(stages.Length == 1, "one exact legacy stage namespace");
                    string middle = Path.GetFileName(stages[0])[".native-id65-fresh-document-".Length..^4];
                    Check(middle.Length == 32 && Guid.TryParseExact(middle, "N", out _), "legacy stage keeps32hex GUID shape");
                    Check(!File.Exists(absent.Path) && File.ReadAllBytes(stages[0]).SequenceEqual(canonical),
                        "staged callback follows full stage readback and precedes destination publication");
                }
                else if (phase == "published")
                    Check(File.ReadAllBytes(absent.Path).SequenceEqual(canonical) &&
                        Directory.GetFiles(temporary, ".native-id65-fresh-document-*.tmp").Length == 0,
                        "published callback follows actual move of complete document");
                else throw new InvalidDataException("Unexpected legacy phase: " + phase);
            });
            Check(phases.SequenceEqual(new[] { "staged", "published" }), "exact legacy callback order, once each");
            Check(saved.ContentSha256 == Hash(canonical) && saved.RetainedRecoveryDirectory == "" && !File.Exists(writer),
                "real readback and clean writer release precede successful return");

            // A canonical no-op must not reach the transaction or either callback.
            // A retained foreign transaction lock makes that distinction observable.
            string transactionLock = Path.Combine(temporary, ".export-publication.lock");
            File.WriteAllText(transactionLock, "foreign transaction owner");
            File.SetLastWriteTimeUtc(saved.Path, new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            DateTime beforeTime = File.GetLastWriteTimeUtc(saved.Path);
            string[] beforeNames = Directory.GetFileSystemEntries(temporary).Order().ToArray();
            var noOp = NativeId65FreshDocumentStore.SaveForSmoke(saved, intent, _ => throw new InvalidDataException("No-op callback must not run"));
            Check(noOp.ContentSha256 == saved.ContentSha256 && File.ReadAllBytes(saved.Path).SequenceEqual(canonical) &&
                File.GetLastWriteTimeUtc(saved.Path) == beforeTime, "canonical no-op does not rewrite document or timestamp");
            Check(Directory.GetFileSystemEntries(temporary).Order().SequenceEqual(beforeNames) &&
                File.ReadAllText(transactionLock) == "foreign transaction owner", "no-op never reaches foreign publication lock or leaves stage/lease");
            File.Delete(transactionLock);

            byte[] noncanonical = canonical.Concat(" \r\n"u8.ToArray()).ToArray();
            File.WriteAllBytes(saved.Path, noncanonical);
            var raw = NativeId65FreshDocumentStore.Capture(project);
            Check(raw.ContentSha256 == Hash(noncanonical) && raw.ContentSha256 != saved.ContentSha256 &&
                NativeId65FreshDocumentCodec.SerializeCanonical(raw.Intent!).SequenceEqual(canonical), "raw hash differs despite equal canonical intent");
            phases.Clear();
            var normalized = NativeId65FreshDocumentStore.SaveForSmoke(raw, raw.Intent!, phase =>
            {
                phases.Add(phase);
                Check(File.ReadAllBytes(raw.Path).SequenceEqual(phase == "staged" ? noncanonical : canonical),
                    "raw canonicalization preserves old bytes through staged callback and replaces only at publication");
            });
            Check(phases.SequenceEqual(new[] { "staged", "published" }) && normalized.ContentSha256 == Hash(canonical),
                "equal meaning with noncanonical raw bytes is not a skipped save");

            // Root/path/ID/raw bytes are the contract, not a filesystem inode promise.
            string replacement = Path.Combine(temporary, "private-replacement");
            File.WriteAllBytes(replacement, canonical); File.Move(replacement, normalized.Path, overwrite: true);
            var sameBytes = NativeId65FreshDocumentStore.SaveForSmoke(normalized, intent, _ => throw new InvalidDataException("Equivalent replacement remains no-op"));
            Check(sameBytes.ContentSha256 == normalized.ContentSha256, "same-byte regular replacement remains valid");
            using (var readLease = NativeId65FreshDocumentStore.AcquireReadLease(normalized))
                Check(readLease.LeasePath == writer && readLease.RetainedEvidencePath == "" && new FileInfo(writer).Length == 32,
                    "same-byte replacement admits old final guard and its distinct32-byte marker");
            File.AppendAllText(normalized.Path, " ");
            Expect<InvalidDataException>(() => NativeId65FreshDocumentStore.SaveForSmoke(normalized, intent,
                _ => throw new InvalidOperationException("Stale version must fail before a stage callback")));
            Check(File.ReadAllBytes(normalized.Path).SequenceEqual(canonical.Concat(" "u8.ToArray())), "stale equal-meaning bytes are preserved, not overwritten");
            Check(File.ReadAllBytes(project.ManifestPath).SequenceEqual(manifest) && File.ReadAllText(modern) == modernSentinel,
                "unrelated raw manifest fields and aggregate-path sentinel untouched");
            Check(Directory.GetFileSystemEntries(temporary).Select(Path.GetFileName).Order().SequenceEqual(
                new[] { DocumentName, "native-fresh-level.json", "spyro-project.json" }.Order()), "only exact fixture inputs/documents remain");
        }
        finally { Directory.Delete(temporary, recursive: true); }
        Console.WriteLine($"NATIVE_ID65_LEGACY_STORAGE_COMPATIBILITY_OK checks={_checks} extraction=false");
    }

    private static void Shape()
    {
        Type store = typeof(NativeId65FreshDocumentStore), snapshot = typeof(NativeId65FreshDocumentSnapshot);
        var properties = snapshot.GetProperties().ToDictionary(p => p.Name, p => p.PropertyType);
        Check(properties.Count == 8 && properties["RootPath"] == typeof(string) && properties["ProjectId"] == typeof(string) &&
            properties["Path"] == typeof(string) && properties["ManifestSha256"] == typeof(string) && properties["ContentSha256"] == typeof(string) &&
            properties["Exists"] == typeof(bool) && properties["Intent"] == typeof(NativeId65FreshDocumentIntent) &&
            properties["RetainedRecoveryDirectory"] == typeof(string), "exact public legacy snapshot property types");
        MethodInfo capture = store.GetMethod("Capture", [typeof(EditorProjectLayout), typeof(CancellationToken)])!;
        MethodInfo save = store.GetMethod("Save", [snapshot, typeof(NativeId65FreshDocumentIntent), typeof(CancellationToken)])!;
        Check(capture.ReturnType == snapshot && save.ReturnType == snapshot && capture.GetParameters()[1].HasDefaultValue &&
            save.GetParameters()[2].HasDefaultValue, "exact synchronous typed public signatures/default tokens");
        Type lease = typeof(NativeId65FreshDocumentStore.ReadLease);
        Check(lease.IsNestedAssembly && lease.IsSealed && typeof(IDisposable).IsAssignableFrom(lease) && lease.GetConstructors().Length == 0,
            "legacy internal nested disposable guard retained without public construction");
        Check(lease.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Select(m => m.Name).SequenceEqual(new[] { "Dispose" }), "guard exposes only disposal publicly");
        foreach (string name in new[] { "LeasePath", "RetainedEvidencePath" })
        {
            var property = lease.GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
            Check(property.PropertyType == typeof(string) && property.GetMethod!.IsAssembly &&
                (property.SetMethod == null || property.SetMethod.IsPrivate), "internal readonly-to-consumer lease evidence: " + name);
        }
        var acquire = store.GetMethod("AcquireReadLease", BindingFlags.Static | BindingFlags.NonPublic, [snapshot, typeof(CancellationToken)])!;
        Check(acquire.IsAssembly && acquire.ReturnType == lease && acquire.GetParameters()[1].HasDefaultValue,
            "exact internal saved-context final-guard signature remains available");
    }
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Check(bool value, string label) { if (!value) throw new InvalidDataException(label); _checks++; }
    private static void Expect<T>(Action action, string? parameter = null) where T : Exception
    {
        try { action(); }
        catch (T error)
        {
            if (parameter != null) Check(error is ArgumentException argument && argument.ParamName == parameter, "argument validation precedence: " + parameter);
            _checks++; return;
        }
        throw new InvalidDataException("Expected existing compatibility refusal " + typeof(T).Name);
    }
}
