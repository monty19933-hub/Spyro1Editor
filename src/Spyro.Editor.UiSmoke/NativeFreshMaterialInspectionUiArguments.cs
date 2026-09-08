// PRIVATE strict UI routes, distinct from the existing worker-only routes.
internal static class NativeFreshMaterialInspectionUiArguments
{
    private const string Free = "--fresh-material-inspection-only";
    private const string Source = "--fresh-material-inspection-source";
    internal static bool Handles(string[] args) =>
        args.Any(a => a.StartsWith("--fresh-material-inspection-", StringComparison.Ordinal)) &&
        !NativeFreshMaterialInspectionWorkerCases.Handles(args);
    internal static bool RequireSource(string[] args)
    {
        if (args.Length == 1 && args[0] == Free) return false;
        if (args.Length == 3 && args[0] == Source && args.Skip(1).All(a =>
            !string.IsNullOrWhiteSpace(a) && !a.Contains('\0') && !a.StartsWith('-') && Path.IsPathFullyQualified(a))) return true;
        throw new ArgumentException("Usage: UiSmoke --fresh-material-inspection-only OR --fresh-material-inspection-source absolute-original.bin absolute-original.cue");
    }
    internal static void Run()
    {
        int checks = 0;
        Check(!RequireSource([Free]) && RequireSource([Source, "/not-opened.bin", "/not-opened.cue"]), "exact free/source modes");
        Check(Handles([Free]) && !Handles(["--fresh-material-inspection-worker-only"]) &&
            !Handles(["--fresh-material-inspection-worker-source", "/bin", "/cue"]) &&
            !Handles(["--fresh-material-picker-only"]), "existing worker/picker routing preserved");
        foreach (var args in new[] { Array.Empty<string>(), new[] { Free, "extra" }, new[] { Free, Free },
            new[] { Source }, new[] { Source, "/bin" }, new[] { Source, "/bin", "/cue", "extra" },
            new[] { Source, Free, "/cue" }, new[] { Source, "/bin", "--unknown" },
            new[] { Source, "relative", "/cue" }, new[] { Source, "/bin", " " }, new[] { Source, "/bin\0", "/cue" },
            new[] { "--fresh-material-inspection-unknown" }, new[] { Free, "--fresh-material-inspection-worker-only" } })
        {
            bool refused = false;
            try { RequireSource(args); } catch (ArgumentException) { refused = true; }
            Check(refused, "malformed/mixed request before headless/source/project setup");
        }
        Console.WriteLine($"FRESH_MATERIAL_INSPECTION_UI_ARGUMENTS_OK checks={checks} sourceOpened=false");
        void Check(bool condition, string message)
        { if (!condition) throw new InvalidDataException(message); checks++; }
    }
}
