using System.Reflection;
using System.Text;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Persistence;
using Spyro.Editor.Core.Primitives;

internal static class StoreCases
{
    internal static int Run()
    {
        int checks = 0;
        string temporary = Directory.CreateTempSubdirectory("spyro-fresh-document-store-").FullName;
        try
        {
            var first = Project("first");
            var second = Project("second");
            byte[] manifest = File.ReadAllBytes(first.ManifestPath);
            string legacy = Path.Combine(first.RootPath, Id65AuthoringEditorDocumentStore.FileName);
            string source = Path.Combine(first.RootPath, "source-manifest.json");
            File.WriteAllText(legacy, "old Lab sentinel: not this document schema");
            File.WriteAllText(source, "source manifest sentinel: no ROM authority");
            string[] originalNames = Directory.GetFileSystemEntries(first.RootPath).Order().ToArray();
            var absent = NativeId65FreshDocumentStore.Capture(first);
            Check(!absent.Exists && absent.Intent == null && absent.ContentSha256 == null,
                "missing document is absent, never a default intent");
            Check(Directory.GetFileSystemEntries(first.RootPath).Order().SequenceEqual(originalNames), "capture writes nothing");
            Check(typeof(NativeId65FreshDocumentSnapshot).IsSealed &&
                typeof(NativeId65FreshDocumentSnapshot).GetConstructors().Length == 0 &&
                typeof(NativeId65FreshDocumentSnapshot).GetProperties().All(p => p.SetMethod == null), "snapshot immutable/publicly unforgeable");
            Check(typeof(NativeId65FreshDocumentStore).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Select(m => m.Name).Order().SequenceEqual(new[] { "Capture", "Save" }), "only capture/save, no delete/export/retarget API");
            var intent = Intent(19);
            byte[] canonical = NativeId65FreshDocumentCodec.SerializeCanonical(intent);
            var saved = NativeId65FreshDocumentStore.Save(absent, intent);
            Check(saved.Exists && saved.Intent?.MusicTrackId == 19 && saved.ProjectId == "first" &&
                saved.RetainedRecoveryDirectory == "", "create returns verified bound document");
            Check(File.ReadAllBytes(saved.Path).SequenceEqual(canonical), "exact codec bytes without persistence envelope");
            var reopened = NativeId65FreshDocumentStore.Capture(first);
            Check(reopened.ContentSha256 == saved.ContentSha256 && reopened.ManifestSha256 == saved.ManifestSha256,
                "independent reopen retains raw document and manifest identity");
            var repeated = NativeId65FreshDocumentStore.Save(reopened, intent);
            Check(repeated.ContentSha256 == saved.ContentSha256, "repeated save remains exact");
            Refuse(() => NativeId65FreshDocumentStore.Save(absent, intent), "missing snapshot cannot replace newly created document");
            var edited = NativeId65FreshDocumentStore.Save(reopened, Intent(20));
            Check(edited.Intent?.MusicTrackId == 20 && edited.ContentSha256 != saved.ContentSha256, "edit saves new canonical intent");
            Refuse(() => NativeId65FreshDocumentStore.Save(reopened, intent), "independent stale snapshot refuses overwrite");
            Check(!NativeId65FreshDocumentStore.Capture(second).Exists, "first project save cannot retarget second project");

            File.AppendAllText(edited.Path, " ");
            Refuse(() => NativeId65FreshDocumentStore.Save(edited, intent), "external whitespace is a stale byte change");
            var spaced = NativeId65FreshDocumentStore.Capture(first);
            Check(spaced.Intent?.MusicTrackId == 20 && spaced.ContentSha256 != edited.ContentSha256, "reopen accepts valid noncanonical bytes but binds them");
            File.Delete(spaced.Path);
            Refuse(() => NativeId65FreshDocumentStore.Save(spaced, intent), "external deletion is stale");
            var emptyAgain = NativeId65FreshDocumentStore.Capture(first);
            File.AppendAllText(first.ManifestPath, "\n");
            Refuse(() => NativeId65FreshDocumentStore.Save(emptyAgain, intent), "manifest whitespace edit requires recapture");
            File.WriteAllBytes(first.ManifestPath, manifest);

            var current = NativeId65FreshDocumentStore.Capture(first);
            Refuse(() => NativeId65FreshDocumentStore.SaveForSmoke(current, intent, phase =>
            {
                if (phase == "staged") throw new IOException("injected precommit fault");
            }), "precommit failure does not publish");
            Check(!File.Exists(current.Path), "forced staging failure leaves document absent");
            Clean(first);
            Refuse(() => NativeId65FreshDocumentStore.SaveForSmoke(current, intent, phase =>
            {
                if (phase == "staged") File.AppendAllText(first.ManifestPath, " ");
            }), "precommit recheck rejects externally changed manifest");
            Check(!File.Exists(current.Path), "late manifest conflict did not publish");
            File.WriteAllBytes(first.ManifestPath, manifest);
            Clean(first);
            Refuse(() => NativeId65FreshDocumentStore.SaveForSmoke(current, intent, phase =>
            {
                if (phase == "staged") File.WriteAllBytes(current.Path, canonical);
            }), "precommit recheck rejects externally created document");
            Check(File.ReadAllBytes(current.Path).SequenceEqual(canonical), "conflicting external document remains exact");
            File.Delete(current.Path);
            Clean(first);

            using (var canceled = new CancellationTokenSource())
            {
                canceled.Cancel();
                Cancel(() => NativeId65FreshDocumentStore.Capture(first, canceled.Token), "capture early cancellation");
                Cancel(() => NativeId65FreshDocumentStore.Save(current, intent, canceled.Token), "save early cancellation");
            }
            using (var late = new CancellationTokenSource())
                Cancel(() => NativeId65FreshDocumentStore.SaveForSmoke(current, intent, phase =>
                {
                    if (phase == "staged") late.Cancel();
                }, late.Token), "cancellation before publication leaves no document");
            Check(!File.Exists(current.Path), "precommit cancellation leaves absent target");
            Clean(first);
            using (var committed = new CancellationTokenSource())
            {
                var result = NativeId65FreshDocumentStore.SaveForSmoke(current, intent, phase =>
                {
                    if (phase == "published") committed.Cancel();
                }, committed.Token);
                Check(committed.IsCancellationRequested && result.ContentSha256 == NativeId65FreshDocumentStore.Capture(first).ContentSha256,
                    "postcommit cancellation cannot turn saved document into cancellation failure");
            }
            Clean(first);

            var existing = NativeId65FreshDocumentStore.Capture(first);
            byte[] oldDocument = File.ReadAllBytes(existing.Path);
            Refuse(() => NativeId65FreshDocumentStore.SaveForSmoke(existing, Intent(20), phase =>
            {
                if (phase == "staged") throw new IOException("injected existing-document precommit fault");
            }), "existing-document staged fault refuses");
            Check(File.ReadAllBytes(existing.Path).SequenceEqual(oldDocument), "existing-document fault preserves every old byte");
            Clean(first);
            string publicationLock = Path.Combine(first.RootPath, ".export-publication.lock");
            File.WriteAllText(publicationLock, "another publication or retained recovery");
            Refuse(() => NativeId65FreshDocumentStore.Save(existing, Intent(20)), "foreign publication lock refuses");
            Check(File.ReadAllText(publicationLock) == "another publication or retained recovery" &&
                File.ReadAllBytes(existing.Path).SequenceEqual(oldDocument), "foreign publication lock and old document preserved");
            File.Delete(publicationLock);
            Clean(first);

            var readbackProject = Project("readback");
            var readbackExpected = NativeId65FreshDocumentStore.Capture(readbackProject);
            bool honestReadbackFailure = false;
            try
            {
                NativeId65FreshDocumentStore.SaveForSmoke(readbackExpected, intent, phase =>
                {
                    if (phase == "published") File.WriteAllBytes(readbackExpected.Path,
                        NativeId65FreshDocumentCodec.SerializeCanonical(Intent(20)));
                });
            }
            catch (IOException error)
            {
                honestReadbackFailure = error.Message.Contains("save committed", StringComparison.Ordinal) &&
                    error.Message.Contains(readbackProject.RootPath, StringComparison.Ordinal);
            }
            Check(honestReadbackFailure && File.Exists(Path.Combine(readbackProject.RootPath,
                ".native-id65-fresh-document.writer.lock")), "real postcommit mismatch reports committed state and retains recovery lease");
            Check(NativeId65FreshDocumentStore.Capture(readbackProject).Intent?.MusicTrackId == 20,
                "postcommit readback failure does not delete or roll back independently changed document");

            string lease = Path.Combine(first.RootPath, ".native-id65-fresh-document.writer.lock");
            File.WriteAllText(lease, "another cooperating writer");
            Refuse(() => NativeId65FreshDocumentStore.Save(NativeId65FreshDocumentStore.Capture(first), intent), "existing writer lease refuses");
            Check(File.ReadAllText(lease) == "another cooperating writer", "foreign lease preserved");
            File.Delete(lease);

            foreach (byte[] bytes in new[] { Array.Empty<byte>(), "{}"u8.ToArray(), new byte[16385] })
            {
                File.WriteAllBytes(current.Path, bytes);
                Refuse(() => NativeId65FreshDocumentStore.Capture(first), "malformed/empty/oversized existing document never becomes default");
                Check(File.ReadAllBytes(current.Path).SequenceEqual(bytes), "bad document unchanged");
            }
            File.Delete(current.Path);
            Directory.CreateDirectory(current.Path);
            Refuse(() => NativeId65FreshDocumentStore.Capture(first), "document directory refuses");
            Directory.Delete(current.Path);
            File.CreateSymbolicLink(current.Path, source);
            Refuse(() => NativeId65FreshDocumentStore.Capture(first), "document symlink refuses");
            File.Delete(current.Path);
            File.CreateSymbolicLink(current.Path, Path.Combine(temporary, "missing"));
            Refuse(() => NativeId65FreshDocumentStore.Capture(first), "dangling document symlink refuses");
            File.Delete(current.Path);

            foreach (string malformed in new[]
            {
                "{}", "null", "[]", "{\"schemaVersion\":1}", "{\"schemaVersion\":1,\"projectId\":\"second\"}",
                "{\"schemaVersion\":2,\"projectId\":\"first\"}", "{\"schemaVersion\":1.0,\"projectId\":\"first\"}",
                "{\"SchemaVersion\":1,\"projectId\":\"first\"}",
                "{\"schemaVersion\":1,\"schemaVersion\":1,\"projectId\":\"first\"}",
                "{\"schemaVersion\":1,\"projectId\":\"first\",\"ProjectId\":\"first\"}",
                "{\"schemaVersion\":1,\"projectId\":\"first\",\"\\u0070rojectId\":\"first\"}",
                "{\"schemaVersion\":1,\"projectId\":\"\\ud800\"}",
                "{\"schemaVersion\":1,\"projectId\":\"first\",\"\\ud800\":0}"
            })
            {
                File.WriteAllText(first.ManifestPath, malformed);
                Refuse(() => NativeId65FreshDocumentStore.Capture(first), "manifest schema/identity/duplicate/case/Unicode refusal");
            }
            File.WriteAllBytes(first.ManifestPath, new byte[65537]);
            Refuse(() => NativeId65FreshDocumentStore.Capture(first), "manifest bound");
            File.Delete(first.ManifestPath);
            Refuse(() => NativeId65FreshDocumentStore.Capture(first), "missing manifest cannot create a project");
            File.CreateSymbolicLink(first.ManifestPath, second.ManifestPath);
            Refuse(() => NativeId65FreshDocumentStore.Capture(first), "manifest symlink refuses");
            File.Delete(first.ManifestPath);
            File.WriteAllBytes(first.ManifestPath, manifest);
            Refuse(() => NativeId65FreshDocumentStore.Capture(new(first.RootPath, "FIRST")), "caller project ID must be normalized");
            Refuse(() => NativeId65FreshDocumentStore.Capture(new(Path.Combine(temporary, "absent"), "absent")), "missing root is never created");
            string rootAlias = Path.Combine(temporary, "root-alias");
            Directory.CreateSymbolicLink(rootAlias, first.RootPath);
            Refuse(() => NativeId65FreshDocumentStore.Capture(new(rootAlias, "first")), "linked project root refuses");
            Directory.Delete(rootAlias);

            Check(File.ReadAllBytes(first.ManifestPath).SequenceEqual(manifest), "source project manifest exact after all saves");
            Check(File.ReadAllText(legacy) == "old Lab sentinel: not this document schema" &&
                File.ReadAllText(source) == "source manifest sentinel: no ROM authority", "legacy Lab/source sentinels preserved");
            Clean(first);
            StreamCases();
        }
        finally { Directory.Delete(temporary, recursive: true); }
        Console.WriteLine($"NATIVE_ID65_FRESH_DOCUMENT_STORE_OK checks={checks}");
        return checks;

        EditorProjectLayout Project(string id)
        {
            string root = Path.Combine(temporary, id);
            Directory.CreateDirectory(root);
            var project = new EditorProjectLayout(root, id);
            File.WriteAllText(project.ManifestPath, "{\"schemaVersion\":1,\"projectId\":\"" + id +
                "\",\"displayName\":\"untouched\",\"createdAtUtc\":\"2026-09-06T00:00:00Z\",\"lastOpenedAtUtc\":\"2026-09-06T00:00:00Z\",\"lastEditorVersion\":\"sentinel\",\"migratedFrom\":\"\"}");
            return project;
        }
        void Clean(EditorProjectLayout project) => Check(!Directory.GetFileSystemEntries(project.RootPath)
            .Any(p => Path.GetFileName(p).StartsWith('.')), "successful cleanup leaves no stage/lease/recovery");
        void Check(bool value, string label) { if (!value) throw new InvalidDataException(label); checks++; }
        void Refuse(Action action, string label)
        {
            try { action(); }
            catch (Exception e) when (e is InvalidDataException or IOException or ArgumentException) { checks++; return; }
            throw new InvalidDataException("Expected refusal: " + label);
        }
        void Cancel(Action action, string label)
        {
            try { action(); }
            catch (OperationCanceledException) { checks++; return; }
            throw new InvalidDataException("Expected cancellation: " + label);
        }
        void StreamCases()
        {
            using var exact = new MemoryStream("abc"u8.ToArray());
            Check(NativeId65FreshDocumentStore.ReadBounded(exact, 3).SequenceEqual("abc"u8.ToArray()), "bounded exact stream read");
            using var oversized = new FaultStream("a"u8.ToArray(), length: long.MaxValue);
            Refuse(() => NativeId65FreshDocumentStore.ReadBounded(oversized, 16384), "declared oversize before allocation/read");
            Check(oversized.Reads == 0, "oversized source never read");
            using var extra = new FaultStream("abcd"u8.ToArray(), length: 3);
            Refuse(() => NativeId65FreshDocumentStore.ReadBounded(extra, 3), "actual EOF required");
            using var shortInput = new FaultStream("ab"u8.ToArray(), length: 3);
            Refuse(() => NativeId65FreshDocumentStore.ReadBounded(shortInput, 3), "early EOF refuses");
            using var moved = new FaultStream("abc"u8.ToArray(), finalPositionFault: true);
            Refuse(() => NativeId65FreshDocumentStore.ReadBounded(moved, 3), "final Position required");
            using var changed = new FaultStream("abc"u8.ToArray(), finalLengthFault: true);
            Refuse(() => NativeId65FreshDocumentStore.ReadBounded(changed, 3), "final Length required");
            using var token = new CancellationTokenSource();
            using var cancel = new FaultStream("abc"u8.ToArray(), afterEof: token.Cancel);
            Cancel(() => NativeId65FreshDocumentStore.ReadBounded(cancel, 3, token.Token), "late EOF cancellation checked");
            using var midToken = new CancellationTokenSource();
            using var midCancel = new FaultStream(new byte[4097], afterRead: midToken.Cancel);
            Cancel(() => NativeId65FreshDocumentStore.ReadBounded(midCancel, 16384, midToken.Token), "mid-read cancellation checked");
            Check(midCancel.Reads == 1, "cancellation stops before next bounded chunk");
        }
    }

    private static NativeId65FreshDocumentIntent Intent(int track) => NativeId65FreshDocumentIntent.Create(
        8448, 8448, 8704, 1024, 1024, 8960, 8960, 64, new ColorRgba(80, 128, 112), new ColorRgba(128, 96, 64), track);

    // Composition avoids MemoryStream derived-overload recursion in fault fixtures.
    private sealed class FaultStream(byte[] bytes, long? length = null, bool finalPositionFault = false,
        bool finalLengthFault = false, Action? afterEof = null, Action? afterRead = null) : Stream
    {
        private readonly MemoryStream _inner = new(bytes);
        private bool _eof;
        internal int Reads { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => (length ?? _inner.Length) + (_eof && finalLengthFault ? 1 : 0);
        public override long Position { get => _inner.Position + (_eof && finalPositionFault ? 1 : 0); set => _inner.Position = value; }
        public override int Read(byte[] buffer, int offset, int count) { Reads++; int n = _inner.Read(buffer, offset, count); afterRead?.Invoke(); return n; }
        public override int Read(Span<byte> buffer) { Reads++; int n = _inner.Read(buffer); afterRead?.Invoke(); return n; }
        public override int ReadByte() { int b = _inner.ReadByte(); _eof = b == -1; if (_eof) afterEof?.Invoke(); return b; }
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void Flush() => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) _inner.Dispose(); base.Dispose(disposing); }
    }
}
