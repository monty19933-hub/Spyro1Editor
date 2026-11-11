using System.Globalization;
using System.Text;

namespace Spyro.Editor.Core.Persistence;

/// <summary>File ▾ › Clear Entire Project to Base Game…: takes everything the user made out of a project folder, so the
/// editor shows the original game again. What stays is what a new project holds once its game has been opened: the
/// project's manifest, the chosen disc images (<c>_local/settings/*-disc.json</c>), the game data read from the disc
/// (<c>editor-cache</c>, the WAD analysis, the texture pictures in <c>_local/authored-materials</c>), the editor's own
/// files (<c>support</c>, <c>_local/.app</c>) and the games already made (<c>output</c>). Everything else is the user's work or made from it. It is not deleted: it is moved, by
/// renaming (nothing is copied, so it is quick whatever its size), into a dated folder beside the projects, laid out so
/// that moving that folder's contents back over a cleared project brings the work back. Only the compiled stages Create
/// BIN keeps for speed (<c>_local/authored-native</c>: hundreds of MB, rebuilt from the work) are deleted. A clear
/// that fails part-way is undone.</summary>
public static class ProjectClear
{
    public const string KeptFolderName = "Old projects";
    public const string NoteFileName = "About this folder.txt";

    /// <summary>Folders of <c>_local</c> that Create BIN rebuilds from the work: deleted, not kept.</summary>
    private static readonly string[] BuildFolders = ["authored-native", "authored-aggregate"];
    private static readonly string[] FilesThatStay = ["spyro-project.json", "spyro-wad-analysis.json"];
    private static readonly string[] FoldersThatStay = ["support", "editor-cache", "output"];
    private const string Local = "_local", EditorState = ".app", Settings = "settings", DiscChoiceSuffix = "-disc.json";
    /// <summary>Pictures of the game's own textures, made from the disc when first shown: game data, not work.</summary>
    private const string TexturePictures = "authored-materials";
    /// <summary>The level the editor opens at a start: not work, but a cleared project starts where a new one does.</summary>
    private const string LastLevel = "last-level.json";

    /// <param name="KeptPath">The folder the work was moved to; null when the project held nothing to clear.</param>
    /// <param name="Moved">How many of the project's files and folders (counted at its top) went there.</param>
    public sealed record Result(string? KeptPath, int Moved);

    /// <summary>The folder cleared work is kept in: "Old projects" beside the "Projects" folder (beside the project
    /// itself when it lives somewhere else). Same disk as the project, so the work moves by renaming.</summary>
    public static string KeptRootFor(string projectRoot)
    {
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(projectRoot));
        string parent = Path.GetDirectoryName(root) ?? throw new InvalidOperationException("A whole disk cannot be cleared as a project.");
        string? above = Path.GetDirectoryName(parent);
        bool standard = above != null && string.Equals(Path.GetFileName(parent), "Projects", StringComparison.OrdinalIgnoreCase);
        return Path.Combine(standard ? above! : parent, KeptFolderName);
    }

    /// <summary>The project's files and folders (at its top) a clear would move away.</summary>
    public static IReadOnlyList<string> WhatWouldGo(string projectRoot)
    {
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(projectRoot));
        return Directory.Exists(root) ? [.. Work(root).Select(e => e.Name)] : [];
    }

    public static Result Clear(string projectRoot, DateTimeOffset now, Action<string>? faultForTesting = null)
    {
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(projectRoot));
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException($"The project folder was not found: {root}");
        // The compiled stages go first: they are not work, and are far too large to keep.
        string local = Path.Combine(root, Local);
        if (IsRealFolder(new DirectoryInfo(local)))
            foreach (string build in BuildFolders)
                try { if (IsRealFolder(new DirectoryInfo(Path.Combine(local, build)))) Directory.Delete(Path.Combine(local, build), recursive: true); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }   // Create BIN replaces it

        List<FileSystemInfo> work = Work(root);
        if (work.Count == 0)
        {
            ForgetLastLevel(local);
            return new(null, 0);
        }

        string keptRoot = KeptRootFor(root);
        PersistencePathSafety.EnsureTreesDoNotOverlap(root, keptRoot, "The folder that keeps cleared work must be outside the project.");
        string name = $"{Path.GetFileName(root)} (cleared {now.ToString("yyyy-MM-dd HH.mm", CultureInfo.InvariantCulture)})";
        string kept = Path.Combine(keptRoot, name);
        for (int n = 2; Directory.Exists(kept) || File.Exists(kept); n++) kept = Path.Combine(keptRoot, $"{name} {n}");

        List<(string From, string To)> done = [];
        bool localRemade = false;
        try
        {
            Directory.CreateDirectory(kept);
            foreach (FileSystemInfo entry in work)
            {
                faultForTesting?.Invoke(entry.Name);
                string to = Path.Combine(kept, entry.Name);
                Move(entry.FullName, to);
                done.Add((entry.FullName, to));
                if (!string.Equals(entry.Name, Local, StringComparison.OrdinalIgnoreCase) || !IsRealFolder(new DirectoryInfo(to))) continue;
                // _local moved whole (so the kept folder can be moved back as it is); what a new project keeps there
                // comes back: the editor's own state and the chosen disc images as copies, the texture pictures themselves.
                localRemade = true;
                Directory.CreateDirectory(local);
                faultForTesting?.Invoke(Local + "/");
                if (IsRealFolder(new DirectoryInfo(Path.Combine(to, TexturePictures)))) Directory.Move(Path.Combine(to, TexturePictures), Path.Combine(local, TexturePictures));
                if (IsRealFolder(new DirectoryInfo(Path.Combine(to, EditorState)))) CopyFolder(Path.Combine(to, EditorState), Path.Combine(local, EditorState));
                string settings = Path.Combine(to, Settings);
                if (IsRealFolder(new DirectoryInfo(settings)))
                    foreach (string choice in Directory.EnumerateFiles(settings, "*" + DiscChoiceSuffix))
                    {
                        Directory.CreateDirectory(Path.Combine(local, Settings));
                        File.Copy(choice, Path.Combine(local, Settings, Path.GetFileName(choice)), overwrite: true);
                    }
            }
            faultForTesting?.Invoke(NoteFileName);
            WriteNote(kept, root, now);
            if (!localRemade) ForgetLastLevel(local);   // _local held no work and stayed
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            bool restored = true;
            try
            {
                // The remade _local holds only copies (and the texture pictures, which go back first); it gives way to the real one.
                if (localRemade && Directory.Exists(local) && done.FirstOrDefault(d => string.Equals(d.From, local, StringComparison.OrdinalIgnoreCase)) is { To: { } keptLocal })
                {
                    if (Directory.Exists(Path.Combine(local, TexturePictures)) && !Directory.Exists(Path.Combine(keptLocal, TexturePictures)))
                        Directory.Move(Path.Combine(local, TexturePictures), Path.Combine(keptLocal, TexturePictures));
                    Directory.Delete(local, recursive: true);
                }
                for (int i = done.Count - 1; i >= 0; i--) Move(done[i].To, done[i].From);
                if (Directory.Exists(kept))
                {
                    File.Delete(Path.Combine(kept, NoteFileName));
                    if (!Directory.EnumerateFileSystemEntries(kept).Any()) Directory.Delete(kept);
                }
                if (Directory.Exists(keptRoot) && !Directory.EnumerateFileSystemEntries(keptRoot).Any()) Directory.Delete(keptRoot);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { restored = false; }
            throw new IOException(restored
                ? $"{failure.Message} Nothing was changed: your project is as it was."
                : $"{failure.Message} Part of your project could not be put back; it is safe in {kept}.", failure);
        }
        return new(kept, done.Count);
    }

    /// <summary>The top entries of a project that are the user's work (or made from it).</summary>
    private static List<FileSystemInfo> Work(string root)
    {
        List<FileSystemInfo> work = [];
        foreach (FileSystemInfo entry in new DirectoryInfo(root).EnumerateFileSystemInfos().OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase))
        {
            bool folder = entry is DirectoryInfo;
            if (IsLitter(entry)) continue;
            if (!folder && FilesThatStay.Contains(entry.Name, StringComparer.OrdinalIgnoreCase)) continue;
            if (folder && FoldersThatStay.Contains(entry.Name, StringComparer.OrdinalIgnoreCase)) continue;
            if (string.Equals(entry.Name, Local, StringComparison.OrdinalIgnoreCase) && IsRealFolder(entry) && !LocalHoldsWork((DirectoryInfo)entry)) continue;
            if (IsRealFolder(entry) && !HoldsFiles((DirectoryInfo)entry)) continue;   // an empty folder is nobody's work
            work.Add(entry);
        }
        return work;
    }

    /// <summary>False for the <c>_local</c> of a project that was just made or just cleared: only the editor's own state
    /// and the chosen disc images.</summary>
    private static bool LocalHoldsWork(DirectoryInfo local)
    {
        foreach (FileSystemInfo entry in local.EnumerateFileSystemInfos())
        {
            if (string.Equals(entry.Name, EditorState, StringComparison.OrdinalIgnoreCase) || IsLitter(entry)) continue;
            if (string.Equals(entry.Name, Settings, StringComparison.OrdinalIgnoreCase) && IsRealFolder(entry))
            {
                if (((DirectoryInfo)entry).EnumerateFileSystemInfos().Any(s => !IsLitter(s) && (s is not FileInfo ||
                        !s.Name.EndsWith(DiscChoiceSuffix, StringComparison.OrdinalIgnoreCase) && !string.Equals(s.Name, LastLevel, StringComparison.OrdinalIgnoreCase)))) return true;
                continue;
            }
            if (BuildFolders.Contains(entry.Name, StringComparer.OrdinalIgnoreCase) && IsRealFolder(entry)) continue;
            if (string.Equals(entry.Name, TexturePictures, StringComparison.OrdinalIgnoreCase) && IsRealFolder(entry)) continue;
            if (IsRealFolder(entry) && !HoldsFiles((DirectoryInfo)entry)) continue;   // the editor makes some folders before it needs them
            return true;
        }
        return false;
    }

    /// <summary>True when the folder holds anything but (possibly nested) empty folders.</summary>
    private static bool HoldsFiles(DirectoryInfo folder) =>
        folder.EnumerateFileSystemInfos().Any(entry => !IsLitter(entry) && (!IsRealFolder(entry) || HoldsFiles((DirectoryInfo)entry)));

    /// <summary>Files the computer's own file browser leaves in any folder it has shown.</summary>
    private static bool IsLitter(FileSystemInfo entry) =>
        entry is FileInfo && (entry.Name.Equals(".DS_Store", StringComparison.OrdinalIgnoreCase) ||
                              entry.Name.Equals("Thumbs.db", StringComparison.OrdinalIgnoreCase) || entry.Name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase));

    private static void ForgetLastLevel(string local)
    {
        try { string path = Path.Combine(local, Settings, LastLevel); if (File.Exists(path)) File.Delete(path); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }   // then the editor opens that level: no harm
    }

    private static bool IsRealFolder(FileSystemInfo entry) =>
        entry is DirectoryInfo && entry.Exists && !entry.Attributes.HasFlag(FileAttributes.ReparsePoint);

    private static void Move(string from, string to)
    {
        if (File.Exists(from)) File.Move(from, to);
        else Directory.Move(from, to);
    }

    private static void CopyFolder(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (FileSystemInfo entry in new DirectoryInfo(from).EnumerateFileSystemInfos())
        {
            if (entry.Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
            if (entry is DirectoryInfo) CopyFolder(entry.FullName, Path.Combine(to, entry.Name));
            else File.Copy(entry.FullName, Path.Combine(to, entry.Name), overwrite: true);
        }
    }

    private static void WriteNote(string kept, string projectRoot, DateTimeOffset now) =>
        File.WriteAllText(Path.Combine(kept, NoteFileName),
            "Spyro Editor: work from a project that was cleared\n\n" +
            "This folder holds everything that had been made in the project\n" +
            $"    {projectRoot}\n" +
            $"when it was cleared back to the original game on {now:d MMMM yyyy} at {now:HH:mm}\n" +
            "(File > Clear Entire Project to Base Game). The editor does not use this folder.\n" +
            "Delete it when you are sure you do not want this work back.\n\n" +
            "To bring this work back:\n" +
            "  1. In Spyro Editor, choose File > Clear Entire Project to Base Game, so that the project holds nothing else.\n" +
            "  2. Close Spyro Editor.\n" +
            "  3. Move everything in this folder (except this note) into the project folder above.\n" +
            "     If asked whether to replace a folder of the same name (such as _local), choose Replace.\n" +
            "  4. Open Spyro Editor again.\n",
            new UTF8Encoding(false));
}
