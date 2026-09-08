using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;
using Spyro.Editor.Core.Primitives;

// Only temporary authoring documents: no source image, compilation or publication authority.
internal static class AggregateStorageCases
{
    private static int _checks;
    internal static void Run()
    {
        string temporary = Directory.CreateTempSubdirectory("spyro-aggregate-storage-").FullName;
        try
        {
            var project = Project(temporary, "main");
            byte[] manifest = File.ReadAllBytes(project.ManifestPath);
            string[] before = Names(project);
            var absent = NativeFreshLevelDocumentStore.Capture(project);
            Check(!absent.Exists && absent.Intent == null && absent.ContentSha256 == null &&
                absent.RetainedRecoveryDirectory == "" && absent.ProjectId == "main" &&
                absent.RootPath == project.RootPath && absent.ManifestSha256 == Hash(manifest), "actual absent aggregate observation");
            Check(Names(project).SequenceEqual(before), "absent capture writes nothing");
            Check(NativeFreshLevelDocumentStore.FileName == "native-fresh-level.json" &&
                absent.Path == Path.Combine(project.RootPath, "native-fresh-level.json"), "distinct fixed modern filename");
            Check(typeof(NativeFreshLevelDocumentSnapshot).IsSealed &&
                typeof(NativeFreshLevelDocumentSnapshot).GetConstructors().Length == 0 &&
                typeof(NativeFreshLevelDocumentSnapshot).GetProperties().All(p => p.SetMethod == null), "immutable snapshot with no public constructor");
            Check(typeof(NativeFreshLevelDocumentSnapshot).GetProperties().Select(p => p.Name).Order().SequenceEqual(
                new[] { "ContentSha256", "Exists", "Intent", "ManifestSha256", "Path", "ProjectId", "RetainedRecoveryDirectory", "RootPath" }), "exact observation surface without native flags");
            Check(typeof(NativeFreshLevelDocumentStore).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Select(m => m.Name).Order().SequenceEqual(new[] { "Capture", "ForkLegacyFloor", "Save" }), "only typed Capture/Save/explicit Fork, no generic writer");
            using (var canceled = new CancellationTokenSource())
            {
                canceled.Cancel();
                Throws<OperationCanceledException>(() => NativeFreshLevelDocumentStore.Capture(null!, canceled.Token), "capture token before null");
                Check(Throws<ArgumentNullException>(() => NativeFreshLevelDocumentStore.Save(null!, null!, canceled.Token), "save expected before token").ParamName == "expected", "expected name");
                Check(Throws<ArgumentNullException>(() => NativeFreshLevelDocumentStore.Save(absent, null!, canceled.Token), "save intent before token").ParamName == "intent", "intent name");
                Throws<OperationCanceledException>(() => NativeFreshLevelDocumentStore.Save(absent, Document(), canceled.Token), "save precancel");
                Check(Names(project).SequenceEqual(before), "argument/cancel paths do not create owners");
            }

            var placements = Enumerable.Range(1, 128).Select(i => Cases.Wizard(i.ToString("x32"))).ToArray();
            var all = NativeFreshLevelDocumentIntent.Create(Cases.Base(20), placements);
            byte[] canonical = NativeFreshLevelDocumentCodec.SerializeCanonical(all);
            var saved = NativeFreshLevelDocumentStore.Save(absent, all);
            Check(saved.Exists && saved.ContentSha256 == Hash(canonical) && File.ReadAllBytes(saved.Path).SequenceEqual(canonical), "all128 exact canonical bytes");
            var reopened = NativeFreshLevelDocumentStore.Capture(project);
            Check(reopened.Intent!.Placements.Select(p => p.EditorId).SequenceEqual(placements.Select(p => p.EditorId)) &&
                reopened.Intent.Placements.Cast<NativeFreshWizardRouteIntent>().All(p => p.OriginZ == 8909) &&
                reopened.Intent.Base.MusicTrackId == 20 && reopened.ManifestSha256 == saved.ManifestSha256, "all IDs/order/non-grid Z/base retained after actual reopen");
            Throws<InvalidDataException>(() => NativeFreshLevelDocumentStore.Save(absent, all), "absent cannot overwrite newly existing document");
            string replacement = Path.Combine(project.RootPath, "same-bytes.tmp");
            File.WriteAllBytes(replacement, canonical);
            File.Move(replacement, saved.Path, overwrite: true);
            Check(NativeFreshLevelDocumentStore.Save(saved, all).ContentSha256 == saved.ContentSha256, "same-byte inode replacement remains current");

            foreach (var changed in new[]
            {
                NativeFreshLevelDocumentIntent.Create(all.Base, placements.Select((p, i) => i == 0 ? Cases.Wizard("ffffffffffffffffffffffffffffffff") : p)),
                NativeFreshLevelDocumentIntent.Create(all.Base, placements.Reverse()),
                NativeFreshLevelDocumentIntent.Create(all.Base, placements.Select((p, i) => i == 0 ? Cases.Wizard(p.EditorId, z: 8910) : p))
            })
            {
                File.WriteAllBytes(saved.Path, NativeFreshLevelDocumentCodec.SerializeCanonical(changed));
                byte[] external = File.ReadAllBytes(saved.Path);
                Throws<InvalidDataException>(() => NativeFreshLevelDocumentStore.Save(saved, all), "ID/order/placement raw conflict");
                Check(File.ReadAllBytes(saved.Path).SequenceEqual(external), "external aggregate content not overwritten");
                File.WriteAllBytes(saved.Path, canonical);
            }
            File.AppendAllText(saved.Path, " \n");
            Throws<InvalidDataException>(() => NativeFreshLevelDocumentStore.Save(saved, all), "raw whitespace conflict");
            var spaced = NativeFreshLevelDocumentStore.Capture(project);
            Check(spaced.ContentSha256 != saved.ContentSha256 &&
                NativeFreshLevelDocumentStore.Save(spaced, all).ContentSha256 == saved.ContentSha256, "noncanonical raw observation can explicitly canonicalize");
            File.Delete(saved.Path);
            Throws<InvalidDataException>(() => NativeFreshLevelDocumentStore.Save(saved, all), "external deletion is stale");
            File.WriteAllBytes(saved.Path, canonical);
            File.AppendAllText(project.ManifestPath, " ");
            Throws<InvalidDataException>(() => NativeFreshLevelDocumentStore.Save(saved, all), "manifest raw version conflict");
            File.WriteAllBytes(project.ManifestPath, manifest);

            var legacy = NativeId65FreshDocumentStore.Save(NativeId65FreshDocumentStore.Capture(project), Cases.Base());
            byte[] legacyBytes = File.ReadAllBytes(legacy.Path);
            var modern = NativeFreshLevelDocumentStore.Save(saved, Document());
            Check(File.ReadAllBytes(legacy.Path).SequenceEqual(legacyBytes), "modern save preserves existing legacy document");
            byte[] modernBytes = File.ReadAllBytes(modern.Path);
            NativeId65FreshDocumentStore.Save(legacy, Cases.Base(21));
            Check(File.ReadAllBytes(modern.Path).SequenceEqual(modernBytes), "legacy save preserves modern document");
            File.WriteAllText(legacy.Path, "malformed other-profile sentinel");
            var next = NativeFreshLevelDocumentStore.Save(NativeFreshLevelDocumentStore.Capture(project), Document(Cases.Id2));
            Check(File.ReadAllText(legacy.Path) == "malformed other-profile sentinel", "modern ignores malformed legacy file");
            File.WriteAllBytes(legacy.Path, legacyBytes);
            File.WriteAllText(next.Path, "malformed aggregate sentinel");
            NativeId65FreshDocumentStore.Save(NativeId65FreshDocumentStore.Capture(project), Cases.Base(22));
            Check(File.ReadAllText(next.Path) == "malformed aggregate sentinel", "legacy ignores malformed modern file");
            File.WriteAllBytes(next.Path, modernBytes);
            string modernLease = Path.Combine(project.RootPath, ".native-fresh-level.writer.lock");
            File.WriteAllText(modernLease, "foreign modern owner");
            Throws<IOException>(() => NativeFreshLevelDocumentStore.Save(NativeFreshLevelDocumentStore.Capture(project), Document()), "foreign modern writer refuses");
            NativeId65FreshDocumentStore.Save(NativeId65FreshDocumentStore.Capture(project), Cases.Base(23));
            Check(File.ReadAllText(modernLease) == "foreign modern owner", "legacy does not acquire or delete modern lease");
            File.Delete(modernLease);
            var editedProject = Project(temporary, "base-edits");
            var editedBase = NativeId65FreshDocumentIntent.Create(8192, 8192, 8704, 2048, 2048, 9216, 9216, 128,
                new ColorRgba(120, 132, 72), new ColorRgba(100, 66, 44), 21);
            var editedAggregate = NativeFreshLevelDocumentIntent.Create(editedBase, [Cases.Wizard()]);
            var editedSnapshot = NativeFreshLevelDocumentStore.Save(NativeFreshLevelDocumentStore.Capture(editedProject), editedAggregate);
            Check(NativeId65FreshDocumentCodec.SerializeCanonical(NativeFreshLevelDocumentStore.Capture(editedProject).Intent!.Base)
                .SequenceEqual(NativeId65FreshDocumentCodec.SerializeCanonical(editedBase)), "edited floor/spawn/heading/colors/music retained, not replaced with starter base");
            var emptyAggregate = NativeFreshLevelDocumentIntent.Create(editedBase, []);
            editedSnapshot = NativeFreshLevelDocumentStore.Save(editedSnapshot, emptyAggregate);
            Check(editedSnapshot.Intent!.Placements.Count == 0 && File.ReadAllBytes(editedSnapshot.Path)
                .SequenceEqual(NativeFreshLevelDocumentCodec.SerializeCanonical(emptyAggregate)), "zero-placement document remains explicit neutral content");
            Clean(editedProject);
            TypedProfilesAndLeases(temporary);
            Lifecycle(temporary);
            MalformedAndBounds(temporary);
            Check(File.ReadAllBytes(project.ManifestPath).SequenceEqual(manifest), "entire manifest untouched");
            Clean(project);
        }
        finally { Directory.Delete(temporary, recursive: true); }
        Console.WriteLine($"NATIVE_FRESH_LEVEL_DOCUMENT_STORAGE_OK checks={_checks} sourceFree=true");
    }

    private static void TypedProfilesAndLeases(string temporary)
    {
        var project = Project(temporary, "profiles");
        var legacy = NativeId65FreshDocumentStore.Capture(project);
        var aggregate = NativeFreshLevelDocumentStore.Capture(project);
        var oldObservation = NativeFreshDocumentStorage.StorageObservation.FromLegacy(legacy);
        var newObservation = NativeFreshDocumentStorage.StorageObservation.FromAggregate(aggregate);
        var oldIntent = Cases.Base();
        var newIntent = Document();
        var oldPayload = NativeFreshDocumentStorage.StoragePayload.Legacy(oldIntent);
        var newPayload = NativeFreshDocumentStorage.StoragePayload.Aggregate(newIntent);
        Check(oldObservation.ContentSha256 == null && newObservation.ContentSha256 == null &&
            oldObservation.ManifestSha256 == newObservation.ManifestSha256, "genuine absent observations share raw versions, not profile");
        Check(ReferenceEquals(oldPayload.LegacyIntent, oldIntent) && oldPayload.AggregateIntent == null &&
            ReferenceEquals(newPayload.AggregateIntent, newIntent) && newPayload.LegacyIntent == null &&
            ReferenceEquals(newObservation, aggregate.Observation), "typed payloads and aggregate observation retain exact immutable inputs");
        string[] before = Names(project);
        NativeFreshDocumentStorage.RequireCompatible(oldObservation, oldPayload);
        NativeFreshDocumentStorage.RequireCompatible(newObservation, newPayload);
        Throws<InvalidDataException>(() => NativeFreshDocumentStorage.RequireCompatible(oldObservation, newPayload), "actual absent old observation with modern arm");
        Throws<InvalidDataException>(() => NativeFreshDocumentStorage.RequireCompatible(newObservation, oldPayload), "actual absent modern observation with old arm");
        Throws<InvalidDataException>(() => oldObservation.ToAggregate(), "old observation cannot issue modern snapshot");
        Throws<InvalidDataException>(() => newObservation.ToLegacy(), "modern observation cannot issue legacy snapshot");
        Throws<InvalidDataException>(() => new NativeFreshLevelDocumentSnapshot(oldObservation), "typed snapshot constructor refuses actual foreign arm");
        Throws<InvalidDataException>(() => NativeFreshDocumentStorage.StorageDescriptor.For((NativeFreshDocumentStorage.StorageProfile)99), "actual fixed resolver refuses undefined profile");
        Check(Names(project).SequenceEqual(before), "all mixed/undefined checks precede any lease or write");
        legacy = NativeId65FreshDocumentStore.Save(legacy, oldIntent);
        using (var lease = NativeId65FreshDocumentStore.AcquireReadLease(legacy))
        {
            Check(new FileInfo(lease.LeasePath).Length == 32, "actual legacy publication guard retains32-byte marker");
            Throws<IOException>(() => NativeId65FreshDocumentStore.Save(legacy, Cases.Base(20)), "old guard still blocks old writer");
            aggregate = NativeFreshLevelDocumentStore.Save(aggregate, newIntent);
            Check(aggregate.Exists && new FileInfo(lease.LeasePath).Length == 32 && lease.RetainedEvidencePath == "", "modern writer neither shares nor consumes old read lease");
        }
        oldObservation = NativeFreshDocumentStorage.StorageObservation.FromLegacy(NativeId65FreshDocumentStore.Capture(project));
        newObservation = NativeFreshDocumentStorage.StorageObservation.FromAggregate(NativeFreshLevelDocumentStore.Capture(project));
        Throws<InvalidDataException>(() => NativeFreshDocumentStorage.RequireCompatible(oldObservation, newPayload), "actual present old observation with modern arm");
        Throws<InvalidDataException>(() => NativeFreshDocumentStorage.RequireCompatible(newObservation, oldPayload), "actual present modern observation with old arm");
        Check(typeof(NativeId65FreshSavedDocumentExportContext).GetProperty("Snapshot")!.PropertyType == typeof(NativeId65FreshDocumentSnapshot) &&
            typeof(NativeId65FreshSavedDocumentExportContext).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .All(m => m.GetParameters().All(p => p.ParameterType != typeof(NativeFreshLevelDocumentSnapshot))), "saved native context remains legacy typed only");
        Clean(project);
    }

    private static void Lifecycle(string temporary)
    {
        var project = Project(temporary, "lifecycle");
        var absent = NativeFreshLevelDocumentStore.Capture(project);
        byte[] manifest = File.ReadAllBytes(project.ManifestPath);
        var value = Document();
        byte[] bytes = NativeFreshLevelDocumentCodec.SerializeCanonical(value);
        string leasePath = Path.Combine(project.RootPath, ".native-fresh-level.writer.lock");
        List<string> phases = [];
        var saved = NativeFreshLevelDocumentStore.SaveForSmoke(absent, value, phase =>
        {
            phases.Add(phase);
            Check(new FileInfo(leasePath).Length == 0, "same empty Save lease timing, modern fixed namespace");
            string[] stages = Directory.GetFiles(project.RootPath, ".native-fresh-level-*.tmp");
            if (phase == "staged")
            {
                Check(stages.Length == 1 && File.ReadAllBytes(stages[0]).SequenceEqual(bytes) && !File.Exists(absent.Path), "complete canonical stage before actual commit");
                string name = Path.GetFileName(stages[0]);
                Check(Guid.TryParseExact(name[20..^4], "N", out _), "modern fixed prefix with32hex stage suffix");
            }
            else Check(phase == "published" && stages.Length == 0 && File.ReadAllBytes(absent.Path).SequenceEqual(bytes), "actual published checkpoint after transaction");
        });
        Check(phases.SequenceEqual(new[] { "staged", "published" }) && saved.RetainedRecoveryDirectory == "", "same checkpoint order and real verified saved result");
        Clean(project);
        string publicationLock = Path.Combine(project.RootPath, ".export-publication.lock");
        File.WriteAllText(publicationLock, "foreign publication lock");
        DateTime stamp = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(saved.Path, stamp);
        var noOp = NativeFreshLevelDocumentStore.SaveForSmoke(saved, value, _ => throw new Exception("no-op callback must not run"));
        Check(noOp.ContentSha256 == saved.ContentSha256 && File.GetLastWriteTimeUtc(saved.Path) == stamp &&
            File.ReadAllText(publicationLock) == "foreign publication lock", "canonical no-op bypasses transaction and preservesmtime/foreign lock");
        Throws<IOException>(() => NativeFreshLevelDocumentStore.Save(saved, Document(Cases.Id2)), "actual changed save respects shared transaction lock");
        Check(File.ReadAllBytes(saved.Path).SequenceEqual(bytes) && File.ReadAllText(publicationLock) == "foreign publication lock" && !File.Exists(leasePath), "failed shared lock leaves old bytes and cleans own stage/lease");
        File.Delete(publicationLock);
        File.AppendAllText(saved.Path, " ");
        phases.Clear();
        saved = NativeFreshLevelDocumentStore.SaveForSmoke(NativeFreshLevelDocumentStore.Capture(project), value, phase => phases.Add(phase));
        Check(phases.SequenceEqual(new[] { "staged", "published" }) && File.ReadAllBytes(saved.Path).SequenceEqual(bytes), "raw noncanonical same intent is a real canonicalizing commit");
        var stagedError = new IOException("owned precommit fault");
        Check(ReferenceEquals(Throws<IOException>(() => NativeFreshLevelDocumentStore.SaveForSmoke(saved, Document(Cases.Id2), phase =>
        { if (phase == "staged") throw stagedError; }), "staged fault"), stagedError), "clean precommit rethrows exact primary error");
        Check(File.ReadAllBytes(saved.Path).SequenceEqual(bytes), "precommit fault preserves prior bytes");
        using (var cancel = new CancellationTokenSource())
            Throws<OperationCanceledException>(() => NativeFreshLevelDocumentStore.SaveForSmoke(saved, Document(Cases.Id2), phase =>
            { if (phase == "staged") cancel.Cancel(); }, cancel.Token), "actual precommit cancellation");
        Check(File.ReadAllBytes(saved.Path).SequenceEqual(bytes), "precommit cancellation preserves old bytes");
        Clean(project);
        Throws<InvalidDataException>(() => NativeFreshLevelDocumentStore.SaveForSmoke(saved, Document(Cases.Id2), phase =>
        { if (phase == "staged") File.AppendAllText(project.ManifestPath, " "); }), "final precommit manifest recheck");
        Check(File.ReadAllBytes(saved.Path).SequenceEqual(bytes), "late manifest conflict does not publish");
        File.WriteAllBytes(project.ManifestPath, manifest);
        byte[] external = NativeFreshLevelDocumentCodec.SerializeCanonical(Cases.Document(Cases.Wizard(z: 8910)));
        Throws<InvalidDataException>(() => NativeFreshLevelDocumentStore.SaveForSmoke(saved, Document(Cases.Id2), phase =>
        { if (phase == "staged") File.WriteAllBytes(saved.Path, external); }), "final precommit aggregate raw recheck");
        Check(File.ReadAllBytes(saved.Path).SequenceEqual(external), "late external aggregate preserved");
        File.WriteAllBytes(saved.Path, bytes);
        using (var late = new CancellationTokenSource())
        {
            saved = NativeFreshLevelDocumentStore.SaveForSmoke(saved, Document(Cases.Id2), phase =>
            { if (phase == "published") late.Cancel(); }, late.Token);
            Check(late.IsCancellationRequested && saved.ContentSha256 == NativeFreshLevelDocumentStore.Capture(project).ContentSha256 &&
                saved.Intent!.Placements[0].EditorId == Cases.Id2, "actual committed save finishes real readback noncancellably");
        }
        Clean(project);
        foreach (string changed in new[] { "document", "manifest" })
        {
            var failureProject = Project(temporary, "committed-" + changed);
            var expected = NativeFreshLevelDocumentStore.Capture(failureProject);
            var failure = Throws<IOException>(() => NativeFreshLevelDocumentStore.SaveForSmoke(expected, value, phase =>
            {
                if (phase == "published") File.AppendAllText(changed == "document" ? expected.Path : failureProject.ManifestPath, " ");
            }), "actual committed raw readback mismatch");
            Check(failure.Message.StartsWith("Document save committed but readback/cleanup did not complete.", StringComparison.Ordinal) &&
                failure.InnerException is InvalidDataException && File.Exists(expected.Path) &&
                File.Exists(Path.Combine(failureProject.RootPath, ".native-fresh-level.writer.lock")), "known commit preserves actual output and recovery lease without false cancellation");
            Check(Directory.GetFiles(failureProject.RootPath, ".native-fresh-level-*.tmp").Length == 0, "known committed mismatch does not invent a retained stage");
        }
        Check(File.ReadAllBytes(project.ManifestPath).SequenceEqual(manifest), "lifecycle manifest bytes restored by fixture only");
        Clean(project);
    }

    private static void MalformedAndBounds(string temporary)
    {
        var project = Project(temporary, "bounds");
        string path = Path.Combine(project.RootPath, NativeFreshLevelDocumentStore.FileName);
        byte[] exact = NativeFreshLevelDocumentCodec.SerializeCanonical(Document());
        foreach (byte[] bytes in new[] { Array.Empty<byte>(), "{}"u8.ToArray(), Encoding.UTF8.GetBytes(Cases.Canonical.Replace("\"schema\":1", "\"schema\":2")),
            NativeId65FreshDocumentCodec.SerializeCanonical(Cases.Base()), new byte[65537] })
        {
            File.WriteAllBytes(path, bytes);
            Throws<InvalidDataException>(() => NativeFreshLevelDocumentStore.Capture(project), "empty/malformed/newer/legacy/oversize modern refusal");
            Check(File.ReadAllBytes(path).SequenceEqual(bytes), "refused document not defaulted or rewritten");
        }
        File.WriteAllText(path, Encoding.UTF8.GetString(exact) + new string(' ', 65536 - exact.Length));
        Check(NativeFreshLevelDocumentStore.Capture(project).Exists, "modern raw64KiB accepts its exact bound");
        string oldPath = Path.Combine(project.RootPath, NativeId65FreshDocumentStore.FileName);
        byte[] old = NativeId65FreshDocumentCodec.SerializeCanonical(Cases.Base());
        File.WriteAllText(oldPath, Encoding.UTF8.GetString(old) + new string(' ', 16385 - old.Length));
        Throws<InvalidDataException>(() => NativeId65FreshDocumentStore.Capture(project), "old raw16KiB bound remains narrower");
        Check(NativeFreshLevelDocumentStore.Capture(project).Exists, "modern64KiB capture ignores over-bound old file");
        File.Delete(path);
        Directory.CreateDirectory(path);
        Throws<InvalidDataException>(() => NativeFreshLevelDocumentStore.Capture(project), "directory modern path");
        Directory.Delete(path);
        File.CreateSymbolicLink(path, oldPath);
        Throws<InvalidDataException>(() => NativeFreshLevelDocumentStore.Capture(project), "linked modern path");
        File.Delete(path);
        File.CreateSymbolicLink(path, Path.Combine(project.RootPath, "absent-target"));
        Throws<InvalidDataException>(() => NativeFreshLevelDocumentStore.Capture(project), "dangling modern path");
        File.Delete(path);
        var absent = NativeFreshLevelDocumentStore.Capture(project);
        byte[] manifest = File.ReadAllBytes(project.ManifestPath);
        foreach (string invalidManifest in new[]
        {
            "{\"schemaVersion\":1,\"projectId\":\"bounds\",\"ProjectId\":\"bounds\"}",
            "{\"schemaVersion\":2,\"projectId\":\"bounds\"}",
            "{\"schemaVersion\":1,\"projectId\":\"\\ud800\"}"
        })
        {
            File.WriteAllText(project.ManifestPath, invalidManifest);
            Throws<InvalidDataException>(() => NativeFreshLevelDocumentStore.Capture(project), "modern shares strict manifest identity/header refusal");
            Check(File.ReadAllText(project.ManifestPath) == invalidManifest && !File.Exists(path), "malformed manifest never defaulted or rewritten");
        }
        File.WriteAllBytes(project.ManifestPath, new byte[65537]);
        Throws<InvalidDataException>(() => NativeFreshLevelDocumentStore.Capture(project), "modern manifest cap remains64KiB");
        File.WriteAllBytes(project.ManifestPath, manifest);
        File.WriteAllText(project.ManifestPath, "{\"schemaVersion\":1,\"projectId\":\"other\"}");
        Throws<InvalidDataException>(() => NativeFreshLevelDocumentStore.Save(absent, Document()), "actual project ID mismatch");
        Throws<InvalidDataException>(() => NativeFreshLevelDocumentStore.Capture(new(project.RootPath, "BOUNDS")), "unnormalized project ID");
        string moved = project.RootPath + "-moved";
        Directory.Move(project.RootPath, moved);
        Throws<InvalidDataException>(() => NativeFreshLevelDocumentStore.Save(absent, Document()), "moved root does not retarget");
        Check(!Directory.Exists(project.RootPath), "save never recreates moved root");
    }

    private static NativeFreshLevelDocumentIntent Document(string id = Cases.Id) => Cases.Document(Cases.Wizard(id));
    private static EditorProjectLayout Project(string temporary, string id)
    {
        string root = Path.Combine(temporary, id);
        Directory.CreateDirectory(root);
        var project = new EditorProjectLayout(root, id);
        File.WriteAllText(project.ManifestPath, "{\"schemaVersion\":1,\"projectId\":\"" + id + "\",\"unrelated\":{\"sentinel\":true}}\n");
        return project;
    }
    private static string[] Names(EditorProjectLayout project) => Directory.GetFileSystemEntries(project.RootPath).Order().ToArray();
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Clean(EditorProjectLayout project) => Check(!Names(project).Any(p => Path.GetFileName(p).StartsWith('.')), "no unowned stage/lease/recovery residue");
    private static void Check(bool condition, string label) { if (!condition) throw new InvalidDataException(label); _checks++; }
    private static T Throws<T>(Action action, string label) where T : Exception
    {
        try { action(); }
        catch (T error) { _checks++; return error; }
        throw new InvalidDataException("Expected " + typeof(T).Name + ": " + label);
    }
}
