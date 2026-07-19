using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace Spyro.Editor.Core.Diagnostics;

public sealed record EditorExportDiagnostic(
    string OutputCuePath,
    string OutputImagePath,
    string SourceImagePath,
    string Summary,
    IReadOnlyDictionary<string, string>? Details = null,
    IReadOnlyList<string>? ArtifactPaths = null);

public sealed record EditorExportDiagnosticResult(
    string ReportPath,
    string BundlePath);

public sealed class EditorDiagnosticLog
{
    private const int MaxRecentEvents = 240;
    private const int MaxArtifactBytes = 8 * 1024 * 1024;
    private const int MaxDetailLength = 32 * 1024;
    private static readonly HashSet<string> BundleExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".json",
        ".txt",
        ".log"
    };

    private readonly object _gate = new();
    private readonly Queue<DiagnosticEvent> _recentEvents = new();
    private readonly Dictionary<string, string> _context = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _appVersion;
    private readonly string _sessionId;
    private readonly DateTimeOffset _startedAt;
    private List<string> _lastArtifactPaths = [];
    private string _lastException = "";
    private string _previousSessionLogPath = "";
    private string _lastReportPath = "";
    private string _lastBundlePath = "";

    public EditorDiagnosticLog(string requestedLogDirectory, string? appVersion = null)
    {
        _appVersion = string.IsNullOrWhiteSpace(appVersion) ? ResolveVersion() : appVersion.Trim();
        _startedAt = DateTimeOffset.Now;
        _sessionId = $"{_startedAt:yyyyMMdd-HHmmss-fff}-p{Environment.ProcessId}";
        LogDirectory = EnsureWritableDirectory(requestedLogDirectory);
        SessionLogPath = Path.Combine(LogDirectory, $"SpyroEditor-session-{_sessionId}.log");
        LatestDiagnosticsPath = Path.Combine(LogDirectory, "SpyroEditor-latest-diagnostics.txt");
        LatestCrashPath = Path.Combine(LogDirectory, "SpyroEditor-latest-crash.txt");
        ActiveSessionMarkerPath = Path.Combine(LogDirectory, "SpyroEditor-active-session.json");

        ReadPreviousSessionMarker();
        WriteActiveSessionMarker();
        PruneOldSessionLogs();
        Record("INFO", "Editor session started", $"Session {_sessionId}; app {_appVersion}");

        if (!string.IsNullOrWhiteSpace(_previousSessionLogPath))
        {
            Record("WARNING", "Previous editor session ended unexpectedly", _previousSessionLogPath);
            WriteReport(
                "The previous editor session did not record a clean shutdown.",
                LatestCrashPath,
                userNote: "This can mean an editor crash, forced quit, or computer shutdown.");
        }
    }

    public string LogDirectory { get; }
    public string SessionLogPath { get; }
    public string LatestDiagnosticsPath { get; }
    public string LatestCrashPath { get; }
    public string ActiveSessionMarkerPath { get; }
    public string LastReportPath => _lastReportPath;
    public string LastBundlePath => _lastBundlePath;

    public void UpdateContext(IReadOnlyDictionary<string, string?> values)
    {
        lock (_gate)
        {
            foreach ((string key, string? value) in values)
            {
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                if (string.IsNullOrWhiteSpace(value))
                    _context.Remove(key.Trim());
                else
                    _context[key.Trim()] = Limit(value.Trim(), MaxDetailLength);
            }
        }
    }

    public void RecordAction(string action, string? details = null)
    {
        Record("ACTION", action, details);
    }

    public void RecordWarning(string action, string? details = null)
    {
        Record("WARNING", action, details);
    }

    public void RecordException(string operation, Exception exception, bool fatal = false)
    {
        string detail = Limit(exception.ToString(), MaxDetailLength);
        lock (_gate)
            _lastException = detail;

        Record(fatal ? "FATAL" : "ERROR", operation, detail);
        WriteReport(
            fatal ? $"Fatal editor error while {operation}." : $"Editor error while {operation}.",
            LatestCrashPath);
    }

    public string WriteReport(
        string reason,
        string? destinationPath = null,
        string? userNote = null,
        IReadOnlyDictionary<string, string>? extraDetails = null,
        IReadOnlyList<string>? artifactPaths = null)
    {
        try
        {
            string report;
            lock (_gate)
                report = BuildReportUnsafe(reason, userNote, extraDetails, artifactPaths);

            WriteTextAtomically(LatestDiagnosticsPath, report);
            string path = string.IsNullOrWhiteSpace(destinationPath)
                ? LatestDiagnosticsPath
                : Path.GetFullPath(destinationPath);
            if (!string.Equals(path, LatestDiagnosticsPath, StringComparison.OrdinalIgnoreCase))
                WriteTextAtomically(path, report);

            lock (_gate)
                _lastReportPath = path;
            return path;
        }
        catch
        {
            return "";
        }
    }

    public EditorExportDiagnosticResult RecordExport(EditorExportDiagnostic export)
    {
        string outputCuePath = Path.GetFullPath(export.OutputCuePath);
        string outputImagePath = Path.GetFullPath(export.OutputImagePath);
        string prefix = outputCuePath.EndsWith(".cue", StringComparison.OrdinalIgnoreCase)
            ? outputCuePath[..^4]
            : outputCuePath;
        string reportPath = $"{prefix}.diagnostics.txt";
        string bundlePath = $"{prefix}.diagnostics.zip";
        List<string> artifacts = (export.ArtifactPaths ?? Array.Empty<string>())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        UpdateContext(new Dictionary<string, string?>
        {
            ["Last output CUE"] = outputCuePath,
            ["Last output BIN"] = outputImagePath,
            ["Last source image"] = export.SourceImagePath,
            ["Last export summary"] = export.Summary
        });
        lock (_gate)
            _lastArtifactPaths = artifacts;

        RecordAction("Create BIN completed", export.Summary);
        string writtenReport = WriteReport(
            "Create BIN completed. Use this report if DuckStation freezes or the edited level behaves incorrectly.",
            reportPath,
            extraDetails: export.Details,
            artifactPaths: artifacts);
        string writtenBundle = CreateSupportBundle(bundlePath, writtenReport, artifacts);
        return new EditorExportDiagnosticResult(writtenReport, writtenBundle);
    }

    public string CreateSupportBundle(
        string? destinationPath = null,
        string? reportPath = null,
        IReadOnlyList<string>? artifactPaths = null)
    {
        try
        {
            string report = string.IsNullOrWhiteSpace(reportPath) || !File.Exists(reportPath)
                ? WriteReport("Manual support bundle requested.")
                : Path.GetFullPath(reportPath);
            string bundlePath = string.IsNullOrWhiteSpace(destinationPath)
                ? Path.Combine(LogDirectory, $"SpyroEditor-support-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.zip")
                : Path.GetFullPath(destinationPath);
            Directory.CreateDirectory(Path.GetDirectoryName(bundlePath) ?? LogDirectory);

            string tempPath = $"{bundlePath}.{Guid.NewGuid():N}.tmp";
            List<string> artifacts;
            lock (_gate)
            {
                artifacts = (artifactPaths ?? _lastArtifactPaths)
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Select(Path.GetFullPath)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            using (ZipArchive archive = ZipFile.Open(tempPath, ZipArchiveMode.Create))
            {
                AddBundleFile(archive, report, "reports/latest-diagnostics.txt");
                AddBundleFile(archive, SessionLogPath, $"logs/{Path.GetFileName(SessionLogPath)}");
                if (File.Exists(LatestCrashPath))
                    AddBundleFile(archive, LatestCrashPath, "reports/latest-crash.txt");

                int artifactNumber = 0;
                foreach (string artifact in artifacts)
                {
                    if (!CanBundleArtifact(artifact))
                        continue;

                    string entryName = $"artifacts/{++artifactNumber:00}-{Path.GetFileName(artifact)}";
                    AddBundleFile(archive, artifact, entryName);
                }

                ZipArchiveEntry manifestEntry = archive.CreateEntry("bundle-manifest.txt", CompressionLevel.Optimal);
                using StreamWriter writer = new(manifestEntry.Open(), new UTF8Encoding(false));
                writer.WriteLine("Spyro Editor support bundle");
                writer.WriteLine($"Generated: {DateTimeOffset.Now:O}");
                writer.WriteLine("Contains diagnostics and editor-generated JSON/TXT/LOG metadata only.");
                writer.WriteLine("BIN, CUE, ISO, ROM, BIOS, images, audio, and game data are excluded.");
            }

            File.Move(tempPath, bundlePath, true);
            lock (_gate)
                _lastBundlePath = bundlePath;
            RecordAction("Support bundle created", bundlePath);
            return bundlePath;
        }
        catch (Exception exception)
        {
            Record("ERROR", "Could not create support bundle", exception.Message);
            return "";
        }
    }

    public void MarkCleanShutdown()
    {
        Record("INFO", "Editor session ended cleanly");
        try
        {
            if (File.Exists(ActiveSessionMarkerPath))
                File.Delete(ActiveSessionMarkerPath);
        }
        catch
        {
        }
    }

    private void Record(string severity, string action, string? details = null)
    {
        try
        {
            DiagnosticEvent item = new(
                DateTimeOffset.Now,
                string.IsNullOrWhiteSpace(severity) ? "INFO" : severity.Trim().ToUpperInvariant(),
                Limit(string.IsNullOrWhiteSpace(action) ? "Unspecified event" : action.Trim(), 512),
                Limit(details?.Trim() ?? "", MaxDetailLength));

            lock (_gate)
            {
                _recentEvents.Enqueue(item);
                while (_recentEvents.Count > MaxRecentEvents)
                    _recentEvents.Dequeue();

                Directory.CreateDirectory(LogDirectory);
                File.AppendAllText(SessionLogPath, FormatEvent(item), new UTF8Encoding(false));
            }
        }
        catch
        {
        }
    }

    private string BuildReportUnsafe(
        string reason,
        string? userNote,
        IReadOnlyDictionary<string, string>? extraDetails,
        IReadOnlyList<string>? artifactPaths)
    {
        StringBuilder report = new();
        report.AppendLine("Spyro Editor Diagnostic Report");
        report.AppendLine("==============================");
        report.AppendLine($"Generated: {DateTimeOffset.Now:O}");
        report.AppendLine($"Reason: {Limit(reason, 4096)}");
        if (!string.IsNullOrWhiteSpace(userNote))
            report.AppendLine($"Tester note: {Limit(userNote.Trim(), MaxDetailLength)}");
        report.AppendLine();
        report.AppendLine("This report contains paths, edit metadata, patch summaries, and error text only.");
        report.AppendLine("It does not contain BIN/CUE/ISO/ROM/BIOS data, textures, audio, or game files.");
        report.AppendLine();

        report.AppendLine("SYSTEM");
        report.AppendLine($"App version: {_appVersion}");
        report.AppendLine($"Session: {_sessionId}");
        report.AppendLine($"Session started: {_startedAt:O}");
        report.AppendLine($"Session log: {SessionLogPath}");
        report.AppendLine($"OS: {RuntimeInformation.OSDescription}");
        report.AppendLine($"Process architecture: {RuntimeInformation.ProcessArchitecture}");
        report.AppendLine($"OS architecture: {RuntimeInformation.OSArchitecture}");
        report.AppendLine($".NET: {RuntimeInformation.FrameworkDescription}");
        report.AppendLine($"Culture: {System.Globalization.CultureInfo.CurrentCulture.Name}");
        report.AppendLine();

        report.AppendLine("EDITOR CONTEXT");
        if (_context.Count == 0)
        {
            report.AppendLine("(No editor context recorded yet.)");
        }
        else
        {
            foreach ((string key, string value) in _context.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
                report.AppendLine($"{key}: {value}");
        }
        if (extraDetails != null)
        {
            foreach ((string key, string value) in extraDetails.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
                report.AppendLine($"{key}: {Limit(value, MaxDetailLength)}");
        }
        report.AppendLine();

        IReadOnlyList<string> artifacts = artifactPaths ?? _lastArtifactPaths;
        report.AppendLine("DIAGNOSTIC ARTIFACTS");
        if (artifacts.Count == 0)
        {
            report.AppendLine("(No export artifacts recorded yet.)");
        }
        else
        {
            foreach (string artifact in artifacts.Distinct(StringComparer.OrdinalIgnoreCase))
                report.AppendLine(DescribeArtifact(artifact));
        }
        report.AppendLine();

        if (!string.IsNullOrWhiteSpace(_lastException))
        {
            report.AppendLine("LATEST EXCEPTION");
            report.AppendLine(_lastException);
            report.AppendLine();
        }

        report.AppendLine("RECENT ACTIVITY");
        if (_recentEvents.Count == 0)
            report.AppendLine("(No activity recorded.)");
        else
            foreach (DiagnosticEvent item in _recentEvents)
                report.Append(FormatEvent(item));

        AppendPreviousSessionTail(report);
        return report.ToString();
    }

    private void ReadPreviousSessionMarker()
    {
        try
        {
            if (!File.Exists(ActiveSessionMarkerPath))
                return;

            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(ActiveSessionMarkerPath));
            if (document.RootElement.TryGetProperty("sessionLogPath", out JsonElement pathElement))
            {
                string? path = pathElement.GetString();
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    _previousSessionLogPath = Path.GetFullPath(path);
                    _context["Previous unclean session log"] = _previousSessionLogPath;
                }
            }
        }
        catch
        {
        }
    }

    private void WriteActiveSessionMarker()
    {
        try
        {
            string json = JsonSerializer.Serialize(new
            {
                sessionId = _sessionId,
                startedAt = _startedAt,
                sessionLogPath = SessionLogPath
            }, new JsonSerializerOptions { WriteIndented = true });
            WriteTextAtomically(ActiveSessionMarkerPath, json);
        }
        catch
        {
        }
    }

    private void AppendPreviousSessionTail(StringBuilder report)
    {
        if (string.IsNullOrWhiteSpace(_previousSessionLogPath) || !File.Exists(_previousSessionLogPath))
            return;

        try
        {
            string[] lines = File.ReadLines(_previousSessionLogPath).TakeLast(160).ToArray();
            report.AppendLine();
            report.AppendLine("PREVIOUS UNCLEAN SESSION TAIL");
            foreach (string line in lines)
                report.AppendLine(line);
        }
        catch
        {
        }
    }

    private void PruneOldSessionLogs()
    {
        try
        {
            foreach (FileInfo file in new DirectoryInfo(LogDirectory)
                .EnumerateFiles("SpyroEditor-session-*.log")
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .Skip(20))
            {
                if (!string.Equals(file.FullName, SessionLogPath, StringComparison.OrdinalIgnoreCase))
                    file.Delete();
            }
        }
        catch
        {
        }
    }

    private static string EnsureWritableDirectory(string requestedPath)
    {
        string path = string.IsNullOrWhiteSpace(requestedPath)
            ? Path.Combine(Path.GetTempPath(), "SpyroEditor", "Logs")
            : Path.GetFullPath(requestedPath);
        try
        {
            Directory.CreateDirectory(path);
            string probe = Path.Combine(path, $".write-test-{Guid.NewGuid():N}");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return path;
        }
        catch
        {
            string fallback = Path.Combine(Path.GetTempPath(), "SpyroEditor", "Logs");
            Directory.CreateDirectory(fallback);
            return fallback;
        }
    }

    private static void WriteTextAtomically(string path, string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        string tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(tempPath, contents, new UTF8Encoding(false));
        File.Move(tempPath, path, true);
    }

    private static bool CanBundleArtifact(string path)
    {
        if (!File.Exists(path) || !BundleExtensions.Contains(Path.GetExtension(path)))
            return false;

        try
        {
            return new FileInfo(path).Length <= MaxArtifactBytes;
        }
        catch
        {
            return false;
        }
    }

    private static void AddBundleFile(ZipArchive archive, string path, string entryName)
    {
        if (!CanBundleArtifact(path))
            return;

        archive.CreateEntryFromFile(path, entryName, CompressionLevel.Optimal);
    }

    private static string DescribeArtifact(string path)
    {
        try
        {
            FileInfo info = new(path);
            if (!info.Exists)
                return $"- Missing: {path}";
            return $"- {path} ({info.Length:N0} bytes, modified {info.LastWriteTime:O})";
        }
        catch
        {
            return $"- {path}";
        }
    }

    private static string FormatEvent(DiagnosticEvent item)
    {
        string prefix = $"[{item.Timestamp:O}] {item.Severity,-7} {item.Action}";
        if (string.IsNullOrWhiteSpace(item.Details))
            return prefix + Environment.NewLine;

        string indented = item.Details
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Replace("\n", Environment.NewLine + "  ", StringComparison.Ordinal);
        return prefix + Environment.NewLine + "  " + indented + Environment.NewLine;
    }

    private static string Limit(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            return value ?? "";
        return value[..maxLength] + "\n[truncated]";
    }

    private static string ResolveVersion()
    {
        Assembly? assembly = Assembly.GetEntryAssembly();
        return assembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly?.GetName().Version?.ToString()
            ?? "unknown";
    }

    private sealed record DiagnosticEvent(
        DateTimeOffset Timestamp,
        string Severity,
        string Action,
        string Details);
}

public static class EditorDiagnostics
{
    private static readonly object Gate = new();
    private static EditorDiagnosticLog? _current;

    public static EditorDiagnosticLog Current
    {
        get
        {
            lock (Gate)
                return _current ??= new EditorDiagnosticLog(DefaultLogDirectory());
        }
    }

    public static void Initialize(string? appVersion = null)
    {
        lock (Gate)
            _current ??= new EditorDiagnosticLog(DefaultLogDirectory(), appVersion);
    }

    public static void UpdateContext(IReadOnlyDictionary<string, string?> values) => Current.UpdateContext(values);
    public static void RecordAction(string action, string? details = null) => Current.RecordAction(action, details);
    public static void RecordWarning(string action, string? details = null) => Current.RecordWarning(action, details);
    public static void RecordException(string operation, Exception exception, bool fatal = false) => Current.RecordException(operation, exception, fatal);
    public static string WriteReport(string reason, string? userNote = null) => Current.WriteReport(reason, userNote: userNote);
    public static EditorExportDiagnosticResult RecordExport(EditorExportDiagnostic export) => Current.RecordExport(export);
    public static string CreateSupportBundle() => Current.CreateSupportBundle();
    public static void MarkCleanShutdown() => Current.MarkCleanShutdown();

    public static string DefaultLogDirectory()
    {
        string? overridePath = Environment.GetEnvironmentVariable("SPYRO_EDITOR_DIAGNOSTICS_DIR");
        if (!string.IsNullOrWhiteSpace(overridePath))
            return Path.GetFullPath(overridePath);

        if (OperatingSystem.IsMacOS())
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, "Library", "Logs", "Spyro Editor");
        }

        string localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localData))
            localData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        return Path.Combine(localData, "Spyro Editor", "Logs");
    }
}
